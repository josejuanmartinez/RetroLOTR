# Neural-Network-Based AI for RetroLOTR

This document describes how to evolve the current rule-based AI (behaviour tree +
hand-tuned scoring) into a hybrid system where a small neural network learns to
score actions from self-play data, while the behaviour tree remains as a safety
skeleton.

---

## 1. Current architecture (starting point)

The AI today is fully deterministic, no ML involved:

| Layer | File | Role |
|---|---|---|
| Strategic priorities | `Assets/Scripts/AI/BehaviourTree.cs` (`AIBehaviourTreeBuilder.BuildDefault()`) | Selector/sequence tree: economy → attack → movement → best available → pass |
| Heuristic scoring | `Assets/Scripts/AI/AIContext.cs` (`ScoreAction()`, ~line 123) | Hand-written formula: difficulty, gold cost pressure, advisor affinity, distance bonuses |
| Action classification | `Assets/Scripts/CharacterAction.cs` (`DefaultAdvisorType`) | Each action declares its advisor (Economic, Militaristic, Intelligence, Magic, Diplomatic, Movement) |
| World facts | `Assets/Scripts/AI/AIContextDataBuilder.cs` + `AIContextCacheManager.cs` | Precomputed: closest enemy, gold per turn, artifact share, indirect-approach flag… |
| Decision logging | `Assets/Scripts/AI/AIActionLogger.cs` | Already logs one JSONL entry per executed action with pre/post resource and VP snapshots |

The key structural fact: every AI decision is already *"given a state, score a list
of candidate actions and pick the best"*. That is exactly the shape of a learned
ranking problem, which is why the migration is mostly plumbing.

## 2. Target architecture: a two-headed scorer network

One small MLP with a shared trunk and two output heads:

```
state features (from AIContext)          action features (per candidate)
        │                                        │
        ▼                                        │
  ┌─────────────┐                                │
  │   trunk     │  shared layers that            │
  │  (MLP, 2-3  │  learn to "understand          │
  │   layers)   │  the position"                 │
  └─────┬───────┘                                │
        ├────────────────┬───────────────────────┘
        ▼                ▼
  ┌──────────┐    ┌─────────────┐
  │ value    │    │ action head │
  │ head V(s)│    │ score per   │
  │ "who is  │    │ candidate   │
  │ winning?"│    │ order       │
  └──────────┘    └─────────────┘
```

- **Value head** — one scalar `V(s)`: expected discounted return from this state.
  Trained against the computed return `G_t` (see §6).
- **Action head** — one score per candidate action. Trained with the advantage
  `G_t − V(s_t)` as the signal (the value head is the baseline).

Why share the trunk instead of two separate networks:
1. Both tasks need the same understanding of the position (economy health, enemy
   proximity, army strength). Learn it once, use it twice.
2. Mutual regularization — helpful with small datasets.
3. One ONNX model, one inference call in Unity Sentis.

This is the AlphaZero policy+value architecture at miniature scale.

### Tensor shapes (training)

- `B` = batch size = number of *decisions* processed at once (each is one moment
  where a character had to choose; decisions may come from different games).
- `N` = max number of candidate actions in the batch (shorter decisions are
  zero-padded and masked).

```
state:  (B, STATE_SIZE)          one feature row per decision
action: (B, N, ACTION_SIZE)      N candidate feature rows per decision
trunk:  (B, STATE_SIZE) → (B, hidden)
value:  (B, hidden) → (B, 1) → squeeze → (B,)
z_exp:  (B, hidden) → unsqueeze/expand → (B, N, hidden)   # state embedding copied per candidate
concat: (B, N, hidden + ACTION_SIZE)
scores: → (B, N, 1) → squeeze → (B, N)
```

At inference in Unity, `B = 1` and `N` = the character's real candidate count —
no padding, no mask needed.

## 3. The feature contract (Piece 0 — do this first)

**The feature vector must be bit-identical between training (Python) and inference
(Unity).** The robust pattern: define encoding once in C#, log the raw vectors,
and have Python train only on logged vectors — Python never reconstructs features.

Create `Assets/Scripts/AI/AIFeatureEncoder.cs`:

```csharp
public static class AIFeatureEncoder
{
    // Bump these when the layout changes, and store them in every log entry.
    public const int Version = 1;
    public const int StateSize = 24;   // adjust to final layout
    public const int ActionSize = 12;

    public static float[] EncodeState(AIContext ctx)
    {
        return new float[] {
            ctx.GoldBuffer / 100f,               // normalize to roughly [-1, 1]
            ctx.GoldPerTurn / 20f,
            (float)ctx.EconomyStatus / 3f,
            ctx.ClosestEnemyDistance / 20f,
            ctx.NearestEnemyStrengthRatio,
            ctx.Character.GetCommander() / 5f,
            ctx.Character.GetAgent() / 5f,
            ctx.Character.GetMage() / 5f,
            ctx.Character.GetEmmissary() / 5f,
            ctx.Character.IsArmyCommander() ? 1f : 0f,
            ctx.NationPercentageArtifacts,
            ctx.NeedsIndirectApproach ? 1f : 0f,
            turn / 500f,
            // ... resources, VP relative to strongest rival, army strength, etc.
        };
    }

    public static float[] EncodeAction(CharacterAction a, AdvisorType advisor, int difficulty)
    {
        float[] v = new float[ActionSize];
        v[(int)advisor] = 1f;                    // one-hot advisor (reserve ~7 slots)
        v[7] = a.GetGoldCost() / 50f;
        v[8] = difficulty / 25f;
        v[9] = /* one-hot or category encoding of the action type */ 0f;
        // ...
        return v;
    }
}
```

Guidelines:
- Normalize everything to roughly `[-1, 1]` (divide by a sane max).
- Prefer *relative* features (VP vs strongest rival, strength ratio) over
  absolutes — they generalize across game phases.
- Version the layout; never silently reorder fields.

## 4. Data collection (Piece 1 — C# changes)

### 4.1 Per-decision record

Extend `AIActionLogEntry` (`Assets/Scripts/AI/AIActionLogger.cs`) with the
structured training payload:

```csharp
public string gameId;                    // Guid generated at game start (in Game)
public int decisionIndex;                // sequence number within (gameId, leader)
public int featureVersion;               // AIFeatureEncoder.Version
public float[] stateFeatures;            // EncodeState at decision time
public List<CandidateRecord> candidates; // ALL candidates, not just the chosen one
public int chosenIndex;                  // which candidate was executed

[Serializable]
public class CandidateRecord
{
    public string actionName;
    public float[] features;             // EncodeAction
    public float treeScore;              // the current heuristic score, for reference
}
```

`AIContext.RecordScoredAction()` (~line 397) already iterates every candidate —
that is the exact hook point to capture `EncodeAction` per candidate instead of
the current flat string.

### 4.2 What to label per order — and what NOT to

**`G_t` is never logged — it cannot be known during the game** (it depends on the
future). What must be recorded per order:

1. **Trajectory identity**: `gameId`, leader, turn, `decisionIndex` — so decisions
   can be ordered and walked backwards per (game, leader).
2. **State**: the feature vector.
3. **Candidates + chosen index.**
4. **Immediate outcome as RAW COMPONENTS, not a combined number**: ΔVP, Δgold,
   Δgold/turn, Δmilitary strength, Δresources… (the logger already captures most
   of these deltas). Raw components matter because the reward-shaping weights are
   hyperparameters you will retune — with raw components you re-weight and retrain
   without replaying a single game.

### 4.3 Per-game record

Write a second file `game_results.jsonl` once, when the game ends:

```json
{ "gameId": "...", "winnerName": "...", "totalTurns": 312, "finalVictoryPoints": { "...": 0 } }
```

Python joins the two files on `gameId`.

### 4.4 Mass self-play mode

A flag (command-line arg or ScriptableObject) that:
- starts all-AI games,
- runs with high `Time.timeScale` or skips animation waits,
- restarts automatically when a game ends.

Run the build with `-batchmode -nographics` in a loop overnight → hundreds of
games. This is what turns the project from "experiment" into "there is data".
Human-labeled feedback does not scale (a game yields tens of labelable decisions;
a network needs thousands) — game outcomes are the cheap, plentiful signal.

Optional but recommended: add exploration noise (epsilon-greedy over
`ScoreAction`) so the logs cover actions the tree would never normally pick.

## 5. Reward design: computing r_t

### 5.1 The core problem: heterogeneous assets

How do you compare *5 cities + 0 gold* vs *1 city + 100 gold*? They are different
asset classes:

- **Gold is a stock** — you have it once.
- **A city is a flow generator** — it produces gold/taxes/recruitment/VP every
  turn, indefinitely.

Convert flows to stock-equivalent using the same discount factor γ as training:

```
value of 1 gold/turn = 1 + γ + γ² + ... = 1/(1−γ)      with γ = 0.98 → 50
```

So with γ = 0.98, **1 gold/turn ≈ 50 flat gold**. If a city nets ~5 gold/turn:

```
5 cities, 0 gold  →  5 × 5 × 50        = 1250 gold-equivalent
1 city, 100 gold  →  1 × 250 + 100     =  350 gold-equivalent
```

The 5-city nation is far ahead — matching 4X intuition: gold gets spent, cities
compound. The exchange rate is not invented; it follows from the game's own
economy (production per city) and the γ you already chose.

### 5.2 Potential-based shaping

Instead of asking "how many points does this action give?", define a potential
function Φ(state) = total wealth in gold-equivalent, and reward the *change* in
potential:

```
Φ(s) = gold
     + Σ (resource_i × market_price_i)        ← prices already exist (SellTimber, BuyIron…)
     + goldPerTurn / (1 − γ)                  ← capitalizes flows (cities enter here)
     + militaryStrength × recruitment_cost_in_gold

r_t = [Φ(s_after) − Φ(s_before)] + w_vp · ΔVP        (VP weighted separately, high)
```

Why this form wins:
1. **Solves the city/gold comparison automatically** — founding a city is rewarded
   by the present value of its future production, not by a magic constant.
2. **Spending well is not punished; spending badly is** — buying iron at market
   price leaves Φ nearly unchanged (gold down, stock up): correctly neutral.
   Burning gold on a failed action drops Φ: negative. Potential-difference shaping
   captures *efficiency*, not activity.
3. **Theoretical guarantee** (Ng et al.): potential-based shaping does not change
   the optimal policy — it only speeds up learning. A wrong weight biases the
   speed, not the destination; the terminal win/loss bonus remains the final judge.

### 5.3 Calibration advice

- **Don't chase perfect weights.** Correct order of magnitude is enough. The
  hierarchy you want: `terminal bonus ≫ ΔVP ≫ economic terms`. If the economic
  term dominates you breed a hoarder that never closes out games.
- **The data audits your weights.** Once trained, the value head has learned from
  real games how much each feature predicts winning. If Φ disagrees with V(s)
  (e.g. early-game gold for the first army matters more than Φ says), iterate the
  Φ weights — or move to TD/bootstrapping where the learned V(s) replaces manual
  shaping. Hand shaping is the starter motor, not the engine.

## 6. Label computation: the discounted return G_t

### 6.1 Why not just "won / lost"?

A 500-turn game with the win/loss label alone spread over 500 turns × several
characters is a hopelessly diluted signal (the classic *credit assignment
problem*). The fixes, combined:

1. **Intermediate rewards** (§5) — an order in turn 30 gets signal in turn 30.
2. **Discounting** — the win "fades" backwards; an order at turn 10 genuinely has
   almost no influence on turn 400, so it shouldn't be credited for it.
3. **Value baseline / advantage** (§6.4) — separates "this action was good" from
   "I was already winning anyway".

### 6.2 Why conditioning on state teaches anything

Objection: "an action always adds the same VP whether you're winning or losing —
what does the net learn?" Answer: **the training label is not the immediate
effect, it is everything that came after**:

```
Action: "Attack enemy army"              immediate reward: +2 VP in both cases

State A: my army is 3× stronger          State B: I am outnumbered
  t:    +2 VP  (the action)                t:    +2 VP  (same action)
  t+1:  army survives                      t+1:  army destroyed
  t+2:  take their city, +8 VP             t+2:  lose my city, −8 VP
  end:  victory (+10)                      end:  defeat (−10)

  G_t ≈ +15                                G_t ≈ −12
```

Same action, same immediate reward, opposite labels. Over thousands of games the
net learns *which actions the eventual winners take in each kind of situation*.

### 6.3 The backward pass

```
G_t = r_t + γ·r_{t+1} + γ²·r_{t+2} + ... + γ^(T−t)·terminal_bonus
```

Computed **offline, after the game ends, walking the trajectory backwards** —
the accumulated `G` at each step already contains all the future processed so far:

```python
GAMMA = 0.98                       # effective horizon ≈ 50 turns; try 0.98–0.99
G = terminal_bonus                 # e.g. +10 won / −10 lost
for entry in reversed(trajectory): # per (gameId, leader), ordered by decisionIndex
    r = combine(entry.raw_deltas)  # §5 weights — cheap to re-run when retuning
    G = r + GAMMA * G
    entry.G = G                    # ← this is the actual training label
```

Worked example (4 orders, defeat = −10, γ = 0.9):

```
turn:   1      2      3      4     terminal
r_t:   +1     +2     −1     +3     −10

G ← −10
t4: G = 3 + 0.9·(−10)  = −6.0
t3: G = −1 + 0.9·(−6.0) = −6.4
t2: G = 2 + 0.9·(−6.4)  = −3.76
t1: G = 1 + 0.9·(−3.76) = −2.38    (equals the full forward sum — check it)
```

The forward formula and the backward recursion are the same thing; the recursion
is just the O(n) way to compute it.

### 6.4 The advantage baseline

Even with discounting, `G_t` mixes the merit of the action with the merit of the
*position you were already in*. In a lost position every action has low G — were
they all bad? The value head answers "from this state, expect G ≈ −8"; if the
best possible move achieves G = −5, its advantage is **+3**: better than expected
given the situation. A mediocre move in a winning position (G = +6 when
V(s) = +9) gets advantage **−3** despite the high absolute G.

```
A_t = G_t − V(s_t)
```

The advantage is **not a label** — it is computed on the fly in each training
batch using the network's own value head.

### Division of labor summary

| Where | What | When |
|---|---|---|
| C# (per order) | state, candidates, chosen index, raw immediate deltas | when the order executes |
| C# (per game) | final result | at game end |
| Python | `r_t` (shaping weights), `G_t` (backward pass) | offline, re-runnable for free |
| Network | `V(s)` and the advantage | during training |

Consequence: **logs are permanent and reusable**. Changing γ, shaping weights, or
the terminal bonus never invalidates data — only feature or game-mechanic changes
require regenerating games.

## 7. Training (Piece 2 — Python / PyTorch)

### 7.1 Model

```python
import torch, torch.nn as nn, torch.nn.functional as F

class TwoHeadNet(nn.Module):
    def __init__(self, state_size=24, action_size=12, hidden=64):
        super().__init__()
        self.trunk = nn.Sequential(
            nn.Linear(state_size, hidden), nn.ReLU(),
            nn.Linear(hidden, hidden), nn.ReLU())
        self.value_head = nn.Linear(hidden, 1)
        self.action_head = nn.Sequential(
            nn.Linear(hidden + action_size, hidden), nn.ReLU(),
            nn.Linear(hidden, 1))

    def forward(self, state, action):             # action: (B, N, action_size)
        z = self.trunk(state)                      # (B, hidden)
        value = self.value_head(z).squeeze(-1)     # (B,)
        z_exp = z.unsqueeze(1).expand(-1, action.size(1), -1)
        scores = self.action_head(
            torch.cat([z_exp, action], -1)).squeeze(-1)   # (B, N)
        return value, scores
```

### 7.2 Training step

```python
value, scores = model(state, candidates)          # candidates padded + boolean mask
adv = (batch.G - value).detach()                  # advantage, no gradient into V
loss_v = F.mse_loss(value, batch.G)
loss_pi = (F.cross_entropy(scores.masked_fill(~mask, -1e9),
                           batch.chosen_index, reduction='none')
           * adv.clamp(min=0)).mean()
loss = loss_v + 0.5 * loss_pi
```

`adv.clamp(min=0)` = imitate only decisions that turned out better than expected —
the simple, stable variant. Refinements exist (AWR, PPO-style ratios) but this is
enough to start.

### 7.3 Data hygiene

- **Correlation**: the ~500 decisions of one game are strongly correlated. Do NOT
  feed them all as independent samples — subsample a few decisions per game
  (AlphaGo used literally one position per game for its value net), or the model
  memorizes games instead of learning positions.
- **Splits**: split train/validation by `gameId` (never by row), or validation
  leaks.
- **Iterate**: new net plays vs previous version → more logs → retrain. That is
  soft self-play without formal RL infrastructure.

### 7.4 ONNX export

```python
torch.onnx.export(model, (dummy_state, dummy_actions), "ai_scorer.onnx",
                  input_names=["state", "actions"],
                  output_names=["value", "scores"],
                  dynamic_axes={"actions": {1: "n_candidates"},
                                "scores":  {1: "n_candidates"}})
```

## 8. Inference in Unity (Piece 3 — Sentis)

Install `com.unity.sentis` (Package Manager — successor of Barracuda), drop the
`.onnx` into Assets, and wrap it:

```csharp
public class AIScorerModel : MonoBehaviour
{
    [SerializeField] private ModelAsset modelAsset;
    private Worker worker;

    void Awake() => worker = new Worker(ModelLoader.Load(modelAsset), BackendType.CPU);

    public float[] Score(float[] state, float[][] candidateFeatures)
    {
        using var stateT = new Tensor<float>(
            new TensorShape(1, AIFeatureEncoder.StateSize), state);
        using var actionsT = new Tensor<float>(
            new TensorShape(1, candidateFeatures.Length, AIFeatureEncoder.ActionSize),
            Flatten(candidateFeatures));
        worker.Schedule(stateT, actionsT);
        using var scores = (worker.PeekOutput("scores") as Tensor<float>).ReadbackAndClone();
        return scores.DownloadToArray();
    }
}
```

The net is tiny — milliseconds on CPU, no external dependencies, no network calls.

### Integration: blend, don't replace

Surgical hook in `AIContext.ScoreAction()`:

```csharp
score = treeScore + netWeight * netScores[i];   // netWeight configurable; 0 = current AI
```

Starting with a blend instead of full replacement gives you a safety switch and an
A/B mechanism. The behaviour tree stays as the skeleton (hard priorities: don't go
bankrupt, pass when nothing is viable) — a debuggable hybrid instead of a black box.

## 9. Evaluation

- **The metric is win rate, not training loss.** Run tournaments: net-blend AI vs
  pure-tree AI, N games, count wins. Only promote a model that wins significantly.
- Track secondary health metrics per game: average turns to victory, economy
  collapses, pass rate (a degenerate net may learn to pass constantly).
- Keep every ONNX version; regression-test new models against the previous best,
  not just against the tree.

## 10. Roadmap

1. **Feature encoder + structured logging + gameId + game results** (~1 session).
   Nothing exists without this.
2. **Batch self-play mode** — turns the project into "there is data".
   Steps 1–2 are useful even if you never train anything (balancing, AI bug
   detection), so they are the zero-risk starting point.
3. Python script: parse JSONL → rewards → backward pass → train → ONNX.
4. Sentis wrapper + blend in `ScoreAction()` + evaluation tournament.

## 11. Rejected / deferred alternatives

- **Behavioral cloning of the tree alone**: guarantees a ceiling at the tree's
  level. Only useful as a warm start.
- **Pure human feedback labeling**: works as a fine-tuning layer, not as the main
  data source — humans can't label thousands of decisions.
- **Unity ML-Agents (PPO)**: the "official" RL path; supports action masking
  (maps directly to `AvailableActions`). Viable but a project in itself — designed
  for fast loops, and a 500-turn sparse-reward game needs heavy shaping anyway.
  Phase 2 if the supervised scorer plateaus.
- **MCTS + network (AlphaZero-style)**: requires cheap forward simulation of game
  state; with state living in Unity GameObjects, that means first extracting the
  game rules into a pure C# simulable model. Big undertaking; not the place to start.
- **LLM per turn**: latency and cost rule it out for the shipped game; at most an
  offline labeling aid.
