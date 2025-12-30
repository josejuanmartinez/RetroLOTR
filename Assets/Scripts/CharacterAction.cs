using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(GraphicRaycaster))]
public class CharacterAction : SearcherByName
{
    [Header("IDs")]
    public string actionName;
    public string description;
    public int actionId;
    [HideInInspector] public Character character;
        
    [Header("Rendering")]
    [HideInInspector]
    public string actionInitials;
    public Image background;
    public Sprite actionSprite;
    public Color actionColor;

    [Header("Game Objects")]
    public Image spriteImage;
    public Button button;
    public TextMeshProUGUI textUI;

    [Header("Hover")]
    public GameObject hoverPrefab;

    [Header("Failure rate (0-100) %")]
    public int difficulty = 0;
    [Header("Required skill")]
    public int commanderSkillRequired;
    public int agentSkillRequired;
    public int emissarySkillRequired;
    public int mageSkillRequired;

    [Header("Cost")]
    public int leatherCost;
    public int mountsCost;
    public int timberCost;
    public int ironCost;
    public int steelCost;
    public int mithrilCost;
    public int goldCost;
    public bool isBuyCaravans;
    public bool isSellCaravans;

    [Header("XP Chance (0-100) %")]
    public int commanderXP;
    public int agentXP;
    public int emmissaryXP;
    public int mageXP;

    [Header("XP Rewarded")]
    public int reward = 1;

    [Header("Availability")]
    // Function delegate that returns a bool to determine if action is available
    public Func<Character, bool> condition;

    [Header("AI")]
    public AdvisorType advisorType = AdvisorType.None;

    [Header("Effect")]
    // Function delegate that returns a bool to determine if action is available
    public Func<Character, bool> effect;
    // Optional async effect for actions that require awaiting UI/input
    public Func<Character, Task<bool>> asyncEffect;

    public bool LastExecutionSucceeded { get; private set; }
    
    private Game game;
    private StoresManager cachedStoresManager;
    private Hover hoverComponent;
    private bool initialized = false;
    private void RefreshVictoryPoints()
    {
        Game g = game != null ? game : FindFirstObjectByType<Game>();
        VictoryPoints.RecalculateAndAssign(g);
    }
    void Awake()
    {
        game = FindAnyObjectByType<Game>();
    }

    protected virtual AdvisorType DefaultAdvisorType => AdvisorType.None;

    public AdvisorType GetAdvisorType()
    {
        return advisorType == AdvisorType.None ? DefaultAdvisorType : advisorType;
    }


    public virtual void Initialize(Character character, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, Task<bool>> asyncEffect = null)
    {
        try
        {
            var originalCondition = condition;
            this.character = character;
            if (advisorType == AdvisorType.None) advisorType = DefaultAdvisorType;

            this.condition = (character) => { return 
                character != null && !character.killed
                && (!character.hasActionedThisTurn || this.actionName == FindFirstObjectByType<ActionsManager>().DEFAULT.actionName) 
                &&  ResourcesAvailable() 
                && (originalCondition == null || originalCondition(character)); 
            };
            this.effect = effect;
            this.asyncEffect = asyncEffect;

            if (!initialized)
            {
                if (hoverPrefab != null && button != null)
                {
                    GameObject hoverInstance = Instantiate(hoverPrefab, button.transform);
                    hoverComponent = hoverInstance.GetComponent<Hover>();
                    if (hoverComponent != null)
                    {
                        hoverComponent.Initialize(BuildHoverText(), Vector2.one * 40, 18, TextAlignmentOptions.Center);
                    }
                }

                actionInitials = ActionNameUtils.StripShortcut(actionName).ToUpperInvariant();
                if (textUI != null) textUI.text = actionName;
                initialized = true;
            }
            else if (hoverComponent != null)
            {
                hoverComponent.Initialize(BuildHoverText(), Vector2.one * 40, 18, TextAlignmentOptions.Center);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Unable to Initialize CharacterAction {actionName}");
            Debug.LogError(e.ToString());
        }        
    }

    public bool FulfillsConditions()
    {
        return condition(character);
    }

    public virtual bool IsRoleEligible(Character character)
    {
        return true;
    }

    public virtual bool ShouldShowWhenUnavailable()
    {
        return true;
    }

    public void UpdateHoverText(bool isAvailable)
    {
        if (hoverComponent == null) return;
        string text = isAvailable ? BuildHoverText() : BuildUnavailableHoverText();
        hoverComponent.Initialize(text, Vector2.one * 40, 18, TextAlignmentOptions.Center);
    }

    public void Reset()
    {
        character = null;
        condition = null;
        effect = null;
        asyncEffect = null;
        button.gameObject.SetActive(false);
    }

    public bool IsProvidedByArtifact()
    {
        if (character == null || character.artifacts == null) return false;
        string baseName = NormalizeSpellName(ActionNameUtils.StripShortcut(actionName));
        if (string.IsNullOrWhiteSpace(baseName)) return false;
        return character.artifacts.Exists(x => x != null && NormalizeSpellName(x.providesSpell) == baseName);
    }

    protected static string NormalizeSpellName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        string stripped = ActionNameUtils.StripShortcut(name);
        return new string(stripped.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }

    public bool ResourcesAvailable()
    {
        if (commanderSkillRequired > 0 && !IsProvidedByArtifact() && character.GetCommander() < commanderSkillRequired) return false;
        if (agentSkillRequired > 0 && !IsProvidedByArtifact() && character.GetAgent() < agentSkillRequired) return false;
        if (emissarySkillRequired > 0 && !IsProvidedByArtifact() && character.GetEmmissary() < emissarySkillRequired) return false;
        if (mageSkillRequired > 0 && !IsProvidedByArtifact() && character.GetMage() < mageSkillRequired) return false;

        if (character == null || character.GetOwner() == null) return false;

        ActionCostSnapshot snapshot = CalculateCostSnapshot();

        if (isBuyCaravans)
        {
            if (!snapshot.hasRequiredStock) return false;
            if (snapshot.goldDelta < 0 && character.GetOwner().goldAmount < -snapshot.goldDelta) return false;
            return true;
        }

        if (isSellCaravans)
        {
            if (!snapshot.hasRequiredStock) return false;
            foreach (var cost in snapshot.resourceCosts)
            {
                switch (cost.produce)
                {
                    case ProducesEnum.leather:
                        if (character.GetOwner().leatherAmount < cost.amount) return false;
                        break;
                    case ProducesEnum.timber:
                        if (character.GetOwner().timberAmount < cost.amount) return false;
                        break;
                    case ProducesEnum.mounts:
                        if (character.GetOwner().mountsAmount < cost.amount) return false;
                        break;
                    case ProducesEnum.iron:
                        if (character.GetOwner().ironAmount < cost.amount) return false;
                        break;
                    case ProducesEnum.steel:
                        if (character.GetOwner().steelAmount < cost.amount) return false;
                        break;
                    case ProducesEnum.mithril:
                        if (character.GetOwner().mithrilAmount < cost.amount) return false;
                        break;
                }
            }

            if (snapshot.goldDelta < 0 && character.GetOwner().goldAmount < -snapshot.goldDelta) return false;
            return true;
        }

        if (leatherCost > 0 && character.GetOwner().leatherAmount < leatherCost) return false;
        if (timberCost > 0 && character.GetOwner().timberAmount < timberCost) return false;
        if (mountsCost > 0 && character.GetOwner().mountsAmount < mountsCost) return false;
        if (ironCost > 0 && character.GetOwner().ironAmount < ironCost) return false;
        if (steelCost > 0 && character.GetOwner().steelAmount < steelCost) return false;
        if (mithrilCost > 0 && character.GetOwner().mithrilAmount < mithrilCost) return false;
        if (goldCost > 0 && character.GetOwner().goldAmount < goldCost) return false;

        return true;
    }

    public void Fail(bool isAI)
    {
        if (this is Spell)
        {
            ApplySpellFailurePenalty(isAI);
        }
        if (!isAI)
        {
            Sounds.Instance?.PlayActionFail();
        }
        string message = $"{actionName} failed";
        if (!isAI)
        {
            MessageDisplayNoUI.ShowMessage(character.hex, character, message, Color.red);
        }
        else if (PlayerCanSeeHex(character != null ? character.hex : null))
        {
            MessageDisplayNoUI.ShowMessage(character.hex, character, message, Color.red);
            BoardNavigator.Instance?.EnqueueEnemyFocus(character.hex, character.GetOwner());
        }
        if (!isAI) game.PointToCharacterWithMissingActions();
        if (!isAI) FindFirstObjectByType<Layout>().GetActionsManager().Refresh(character);
        if (!isAI) FindFirstObjectByType<Layout>().GetSelectedCharacterIcon().Refresh(character);
        return;
    }

    public async Task Execute()
    {
        bool isAI = !character.isPlayerControlled;
        LastExecutionSucceeded = false;
        try
        {
            if (!isAI)
            {
                Sounds.Instance?.PlayActionExecute();
            }
            Hex actionHex = character.hex;
            ActionCostSnapshot costSnapshot = CalculateCostSnapshot();
            bool providedByArtifact = IsProvidedByArtifact();
            // All characters
            character.hasActionedThisTurn = true;

            bool failed = false;
            int effectiveDifficulty = difficulty;
            if (isAI && ShouldApplyUnscoutedPenalty(character))
            {
                effectiveDifficulty = Mathf.Min(100, effectiveDifficulty + 25);
            }
            if (UnityEngine.Random.Range(0, 100) < effectiveDifficulty) failed = true;

            // Should be impossible as the button will show not show up but just in case
            if(!ResourcesAvailable()) failed = true;

            if (failed)
            {
                Fail(isAI);
                return;
            }

            if (effect != null) failed = !effect(character);

            if (!failed && asyncEffect != null) failed = !await asyncEffect(character);

            if (failed)
            {
                Fail(isAI);
                return;
            }

            NonPlayableLeader.RecordActionCompleted(character, actionName, actionHex);
            string message = actionName;            
            string rumourMessage = $"succeeds on {message}";
            bool isDoubledByPlayer = character.doubledBy.Contains(game.player);
            if (isAI)
            {
                // Always record AI actions privately; if doubled by the player, also promote to public immediately
                Rumour rumour = new Rumour() { leader = character.GetOwner(), character = character, characterName = character.characterName, rumour = rumourMessage, v2 = character.hex.v2 };
                RumoursManager.AddRumour(rumour, false);
                if (isDoubledByPlayer)
                {
                    RumoursManager.PromoteRumourToPublic(rumour);
                }
                if (PlayerCanSeeHex(character.hex))
                {
                    MessageDisplayNoUI.ShowMessage(character.hex, character, message, Color.yellow);
                    BoardNavigator.Instance?.EnqueueEnemyFocus(character.hex, character.GetOwner());
                }
            }
            
            if(!providedByArtifact && UnityEngine.Random.Range(0, 100) < commanderXP)
            {
                character.AddCommander(1);
                // Debug.Log($"{character.characterName} gets +1 to commander XP");
                if (!isAI) MessageDisplayNoUI.ShowMessage(character.hex, character,  "<sprite name=\"commander\"/> +1", Color.green);
            }

            if (!providedByArtifact && UnityEngine.Random.Range(0, 100) < agentXP)
            {
                character.AddAgent(1);
                // Debug.Log($"{character.characterName} gets +1 to agent XP");
                if (!isAI) MessageDisplayNoUI.ShowMessage(character.hex, character,  "<sprite name=\"agent\"/> +1", Color.green);
            }

            if (!providedByArtifact && UnityEngine.Random.Range(0, 100) < emmissaryXP)
            {
                character.AddEmmissary(1);
                // Debug.Log($"{character.characterName} gets +1 to emmissary XP");
                if (!isAI) MessageDisplayNoUI.ShowMessage(character.hex, character,  "<sprite name=\"emmissary\"/> +1", Color.green);
            }

            if (!providedByArtifact && UnityEngine.Random.Range(0, 100) < mageXP)
            {
                character.AddMage(1);
                // Debug.Log($"{character.characterName} gets +1 to commander XP");
                if (!isAI) MessageDisplayNoUI.ShowMessage(character.hex, character,  "<sprite name=\"mage\"/> +1", Color.green);
            }

            if (!isAI) FindFirstObjectByType<Layout>().GetActionsManager().Refresh(character);
            if (!isAI) FindFirstObjectByType<Layout>().GetSelectedCharacterIcon().Refresh(character);

            if (costSnapshot.isCaravan)
            {
                ApplyCaravanPayments(costSnapshot);
            }
            else
            {
                if(leatherCost > 0)
                {
                    character.GetOwner().RemoveLeather(leatherCost);
                    // Debug.Log($"{character.characterName} spends {leatherCost} leather");
                }
                if (timberCost > 0)
                {
                    character.GetOwner().RemoveTimber(timberCost);
                    // Debug.Log($"{character.characterName} spends {timberCost} timberCost");
                }
                if (mountsCost > 0)
                {
                    character.GetOwner().RemoveMounts(mountsCost);
                    // Debug.Log($"{character.characterName} spends {mountsCost} mounts");
                }

                if (ironCost > 0)
                {
                    character.GetOwner().RemoveIron(ironCost);
                    // Debug.Log($"{character.characterName} spends {ironCost} iron");
                }

                if (steelCost > 0)
                {
                    character.GetOwner().RemoveSteel(steelCost);
                    // Debug.Log($"{character.characterName} spends {steelCost} steel");
                }

                if (mithrilCost > 0)
                {
                    character.GetOwner().RemoveMithril(mithrilCost);
                    // Debug.Log($"{character.characterName} spends {mithrilCost} mithril");
                }

                if (goldCost > 0)
                {
                    character.GetOwner().RemoveGold(goldCost, false);
                    if (!isAI)
                    {
                        MessageDisplay.ShowMessage($"{character.GetOwner().characterName}: -{goldCost} <sprite name=\"gold\">", Color.red);
                    }
                    // Debug.Log($"{character.characterName} spends {goldCost} gold");
                }
            }

            StoresManager storesManager = GetStoresManager();
            if (!isAI && storesManager != null) storesManager.RefreshStores();

            if (!isAI && game != null)
            {
                game.PointToCharacterWithMissingActions();
                game.SelectNextCharacterOrFinishTurnPrompt();
            }

            RefreshVictoryPoints();
            LastExecutionSucceeded = true;
            if (!isAI)
            {
                Sounds.Instance?.PlayActionSuccess(actionName);
                Sounds.Instance?.PlayVoiceForAction(character, actionName);
            }
            return;
        }
        catch (Exception e)
        {
            Debug.Log(e);
            if (!isAI) FindFirstObjectByType<Layout>().GetActionsManager().Refresh(character);
            if (!isAI) FindFirstObjectByType<Layout>().GetSelectedCharacterIcon().Refresh(character);

            // Debug.LogError($"{character.characterName} was unable to Execute action {actionName} {actionId} {actionInitials} {e}");
            character.hasActionedThisTurn = true;
            return;
        }
        
    }

    // Wrapper so Unity UI Buttons (which require void return) can trigger this async action
    public async void ExecuteFromButton()
    {
        await Execute();
    }

    protected bool IsCharacterKnownAtHex(Character actor, Character target)
    {
        if (actor == null || target == null || actor.hex == null) return false;
        Leader actorOwner = actor.GetOwner();
        if (actorOwner == null) return false;

        if (target.GetOwner() == actorOwner) return true;

        AlignmentEnum actorAlignment = actor.GetAlignment();
        if (actorAlignment != AlignmentEnum.neutral && target.GetAlignment() == actorAlignment) return true;

        return ShouldIgnoreScouting(actorOwner) || actor.hex.IsScoutedBy(actorOwner);
    }

    private bool ShouldApplyUnscoutedPenalty(Character actor)
    {
        if (actor == null || actor.hex == null) return false;
        Leader owner = actor.GetOwner();
        if (owner == null) return false;
        if (!ShouldIgnoreScouting(owner)) return false;
        return !actor.hex.IsScoutedBy(owner);
    }

    private bool ShouldIgnoreScouting(Leader leader)
    {
        if (leader == null) return false;
        if (leader is NonPlayableLeader) return true;
        Game g = game != null ? game : FindFirstObjectByType<Game>();
        if (leader is PlayableLeader pl && g != null && g.player != pl) return true;
        return false;
    }

    protected Character FindEnemyCharacterTargetAtHex(Character c)
    {
        Character target = FindEnemyNonNeutralCharactersAtHexNoLeader(c);
        if (target) return target;
        target = FindEnemyCharactersAtHexNoLeaders(c);
        if (target) return target;
        target = FindEnemyNonNeutralCharactersAtHex(c);
        if (target) return target;
        target = FindEnemyCharacterAtHex(c);
        return target;
    }
    protected Character FindEnemyNonNeutralCharactersAtHexNoLeader(Character c)
    {
        // Always prioritize free people or dark servants  (but leaders will be difficult as they will be guarded)
        return c.hex.characters.Find(
            x => IsCharacterKnownAtHex(c, x) &&
            x.GetOwner() != c.GetOwner() &&
            x.GetAlignment() != c.GetAlignment() &&
            x.GetAlignment() != AlignmentEnum.neutral &&
            x is not PlayableLeader
        );
    }
    protected Character FindEnemyCharactersAtHexNoLeaders(Character c)
    {
        // Always prioritize free people or dark servants  (but leaders will be difficult as they will be guarded)
        return c.hex.characters.Find(
            x => IsCharacterKnownAtHex(c, x) &&
            x.GetOwner() != c.GetOwner() &&
            (x.GetAlignment() == AlignmentEnum.neutral || x.GetAlignment() != c.GetAlignment()) && (x is not PlayableLeader)
        );
    }

    protected Character FindEnemyNonNeutralCharactersAtHex(Character c)
    {
        // Always prioritize free people or dark servants  (but leaders will be difficult as they will be guarded)
        return c.hex.characters.Find(
            x => IsCharacterKnownAtHex(c, x) &&
            x.GetOwner() != c.GetOwner() &&
            x.GetAlignment() != c.GetAlignment() &&
            x.GetAlignment() != AlignmentEnum.neutral
        );
    }

    protected Character FindEnemyCharacterAtHex(Character c)
    {
        // Always prioritize free people or dark servants  (but leaders will be difficult as they will be guarded)
        return c.hex.characters.Find(
            x => IsCharacterKnownAtHex(c, x) &&
            x.GetOwner() != c.GetOwner() &&
            (x.GetAlignment() == AlignmentEnum.neutral || x.GetAlignment() != c.GetAlignment())
        );
    }
    protected List<Character> FindEnemyCharactersAtHex(Character c)
    {
        // Always prioritize free people or dark servants  (but leaders will be difficult as they will be guarded)
        return c.hex.characters.FindAll(
            x => IsCharacterKnownAtHex(c, x) &&
            x.GetOwner() != c.GetOwner() &&
            (x.GetAlignment() == AlignmentEnum.neutral || x.GetAlignment() != c.GetAlignment())
        );
    }
    protected List<Character> FindEnemyCharactersNotArmyCommandersAtHex(Character c)
    {
        // Always prioritize free people or dark servants  (but leaders will be difficult as they will be guarded)
        return c.hex.characters.FindAll(
            x => IsCharacterKnownAtHex(c, x) &&
            !x.IsArmyCommander() && x.GetOwner() != c.GetOwner() &&
            (x.GetAlignment() == AlignmentEnum.neutral || x.GetAlignment() != c.GetAlignment())
        );
    }

    protected Army FindEnemyArmyNotNeutralAtHex(Character c)
    {
        Character commander = c.hex.characters.Find(
            x => IsCharacterKnownAtHex(c, x) &&
            x.IsArmyCommander() && x.GetOwner() != c.GetOwner() &&
            (x.GetAlignment() != AlignmentEnum.neutral && x.GetAlignment() != c.GetAlignment())
        );
        if (commander != null && commander.GetArmy() != null) return commander.GetArmy();
        return null;
    }
    protected Army FindEnemyArmyAtHex(Character c)
    {
        Character commander = c.hex.characters.Find(
            x => IsCharacterKnownAtHex(c, x) &&
            x.IsArmyCommander() && x.GetOwner() != c.GetOwner() &&
            (x.GetAlignment() != c.GetAlignment())
        );
        if (commander != null && commander.GetArmy() != null) return commander.GetArmy();
        return null;
    }

    protected Army FindFriendlyArmyAtHex(Character c)
    {
        Character commander = c.hex.characters.Find(
            x => IsCharacterKnownAtHex(c, x) &&
            x.IsArmyCommander() && (x.GetOwner() == c.GetOwner() || (x.GetAlignment() == c.GetAlignment() && x.GetAlignment() != AlignmentEnum.neutral))
        );
        if (commander != null && commander.GetArmy() != null) return commander.GetArmy();
        return null;
    }

    protected PC FindEnemyTargetPCInRange(List<Hex> hexes, Character character)
    {
        // Get potential targets from each category and track their priority
        List<TargetScore> allCandidates = new List<TargetScore>();

        // Category 1: Non-neutral PCs that aren't capitals (highest priority)
        PC target1 = FindNonNeutralEnemyTargetPCNoCapitalInRange(hexes, character);
        if (target1 != null)
        {
            float distance = Vector2.Distance(character.hex.v2, target1.hex.v2);
            float defense = target1.GetDefense();
            float score = distance + defense;
            allCandidates.Add(new TargetScore
            {
                Target = target1,
                TotalScore = score,
                CategoryPriority = 1 // Highest priority
            });
        }

        // Category 2: Any PC that isn't a capital
        PC target2 = FindEnemyTargetPCNoCapitalsInRange(hexes, character);
        if (target2 != null)
        {
            float distance = Vector2.Distance(character.hex.v2, target2.hex.v2);
            float defense = target2.GetDefense();
            float score = distance + defense;
            allCandidates.Add(new TargetScore
            {
                Target = target2,
                TotalScore = score,
                CategoryPriority = 2
            });
        }

        // Category 3: Non-neutral PCs
        PC target3 = FindEnemyTargetNonNeutralPCInRange(hexes, character);
        if (target3 != null)
        {
            float distance = Vector2.Distance(character.hex.v2, target3.hex.v2);
            float defense = target3.GetDefense();
            float score = distance + defense;
            allCandidates.Add(new TargetScore
            {
                Target = target3,
                TotalScore = score,
                CategoryPriority = 3
            });
        }

        // Category 4: Any PC
        PC target4 = FindEnemyTargetPCNoRestrictionsInRange(hexes, character);
        if (target4 != null)
        {
            float distance = Vector2.Distance(character.hex.v2, target4.hex.v2);
            float defense = target4.GetDefense();
            float score = distance + defense;
            allCandidates.Add(new TargetScore
            {
                Target = target4,
                TotalScore = score,
                CategoryPriority = 4 // Lowest priority
            });
        }

        // Return null if no targets found
        if (allCandidates.Count == 0)
        {
            return null;
        }

        // Sort by score (lowest is best)
        allCandidates = allCandidates.OrderBy(c => c.TotalScore).ToList();

        // Get best candidate
        TargetScore bestCandidate = allCandidates[0];
        float bestScore = bestCandidate.TotalScore;

        // Check if any scores are within 10% of the best score
        // If so, use category priority as a tiebreaker
        List<TargetScore> closeScoreCandidates = new List<TargetScore>();
        closeScoreCandidates.Add(bestCandidate);

        for (int i = 1; i < allCandidates.Count; i++)
        {
            float scoreDifference = allCandidates[i].TotalScore - bestScore;
            float percentDifference = bestScore > 0 ? (scoreDifference / bestScore) * 100 : 100;

            // If score is within 10% of best score, add to close candidates
            if (percentDifference <= 10)
            {
                closeScoreCandidates.Add(allCandidates[i]);
            }
        }

        // If we have multiple candidates with close scores, sort by category priority
        if (closeScoreCandidates.Count > 1)
        {
            return closeScoreCandidates.OrderBy(c => c.CategoryPriority).First().Target;
        }

        // Otherwise, return the candidate with the best score
        return bestCandidate.Target;
    }

    // Helper class to track potential targets with their scores
    private class TargetScore
    {
        public PC Target { get; set; }
        public float DistanceScore { get; set; }
        public float DefenseScore { get; set; }
        public float TotalScore { get; set; }
        public int CategoryPriority { get; set; } // 1-4, lower is higher priority
    }

    private PC FindNonNeutralEnemyTargetPCNoCapitalInRange(List<Hex> hexes, Character character)
    {
        // Prioritize non-neutral PCs that aren't capitals
        List<TargetScore> potentialTargets = new List<TargetScore>();

        foreach (Hex hex in hexes)
        {
            PC pc = hex.GetPC();
            if (pc != null &&
                pc.owner != character.GetOwner() &&
                pc.owner.GetAlignment() != character.GetAlignment() &&
                pc.owner.GetAlignment() != AlignmentEnum.neutral &&
                !pc.isCapital)
            {
                // Calculate scores (lower is better)
                float distance = Vector2.Distance(character.hex.v2, hex.v2);
                float distanceScore = distance; // Lower distance = better score
                float defenseScore = pc.GetDefense(); // Lower defense = better score

                // Combine scores (lower total = better target)
                float totalScore = distanceScore + defenseScore;

                potentialTargets.Add(new TargetScore
                {
                    Target = pc,
                    DistanceScore = distanceScore,
                    DefenseScore = defenseScore,
                    TotalScore = totalScore
                });
            }
        }

        // Return the target with the lowest total score (best target)
        if (potentialTargets.Count > 0)
        {
            return potentialTargets.OrderBy(t => t.TotalScore).First().Target;
        }

        return null;
    }

    private PC FindEnemyTargetPCNoCapitalsInRange(List<Hex> hexes, Character character)
    {
        // Find any PC that isn't a capital, including neutral ones
        List<TargetScore> potentialTargets = new List<TargetScore>();

        foreach (Hex hex in hexes)
        {
            PC pc = hex.GetPC();
            if (pc != null &&
                pc.owner != character.GetOwner() &&
                (pc.owner.GetAlignment() == AlignmentEnum.neutral || pc.owner.GetAlignment() != character.GetAlignment()) &&
                !pc.isCapital)
            {
                // Calculate scores (lower is better)
                float distance = Vector2.Distance(character.hex.v2, hex.v2);
                float distanceScore = distance; // Lower distance = better score
                float defenseScore = pc.GetDefense(); // Lower defense = better score

                // Combine scores (lower total = better target)
                float totalScore = distanceScore + defenseScore;

                potentialTargets.Add(new TargetScore
                {
                    Target = pc,
                    DistanceScore = distanceScore,
                    DefenseScore = defenseScore,
                    TotalScore = totalScore
                });
            }
        }

        // Return the target with the lowest total score (best target)
        if (potentialTargets.Count > 0)
        {
            return potentialTargets.OrderBy(t => t.TotalScore).First().Target;
        }

        return null;
    }

    private PC FindEnemyTargetNonNeutralPCInRange(List<Hex> hexes, Character character)
    {
        // Prioritize non-neutral PCs, including capitals
        List<TargetScore> potentialTargets = new List<TargetScore>();

        foreach (Hex hex in hexes)
        {
            PC pc = hex.GetPC();
            if (pc != null &&
                pc.owner != character.GetOwner() &&
                pc.owner.GetAlignment() != character.GetAlignment() &&
                pc.owner.GetAlignment() != AlignmentEnum.neutral)
            {
                // Calculate scores (lower is better)
                float distance = Vector2.Distance(character.hex.v2, hex.v2);
                float distanceScore = distance; // Lower distance = better score
                float defenseScore = pc.GetDefense(); // Lower defense = better score

                // Combine scores (lower total = better target)
                float totalScore = distanceScore + defenseScore;

                potentialTargets.Add(new TargetScore
                {
                    Target = pc,
                    DistanceScore = distanceScore,
                    DefenseScore = defenseScore,
                    TotalScore = totalScore
                });
            }
        }

        // Return the target with the lowest total score (best target)
        if (potentialTargets.Count > 0)
        {
            return potentialTargets.OrderBy(t => t.TotalScore).First().Target;
        }

        return null;
    }

    private PC FindEnemyTargetPCNoRestrictionsInRange(List<Hex> hexes, Character character)
    {
        // Find any PC, including neutral ones and capitals
        List<TargetScore> potentialTargets = new List<TargetScore>();

        foreach (Hex hex in hexes)
        {
            PC pc = hex.GetPC();
            if (pc != null &&
                pc.owner != character.GetOwner() &&
                (pc.owner.GetAlignment() == AlignmentEnum.neutral || pc.owner.GetAlignment() != character.GetAlignment()))
            {
                // Calculate scores (lower is better)
                float distance = Vector2.Distance(character.hex.v2, hex.v2);
                float distanceScore = distance; // Lower distance = better score
                float defenseScore = pc.GetDefense(); // Lower defense = better score

                // Combine scores (lower total = better target)
                float totalScore = distanceScore + defenseScore;

                potentialTargets.Add(new TargetScore
                {
                    Target = pc,
                    DistanceScore = distanceScore,
                    DefenseScore = defenseScore,
                    TotalScore = totalScore
                });
            }
        }

        // Return the target with the lowest total score (best target)
        if (potentialTargets.Count > 0)
        {
            return potentialTargets.OrderBy(t => t.TotalScore).First().Target;
        }

        return null;
    }

    protected Army FindTargetEnemyArmyInRange(List<Hex> hexes, Character character)
    {
        // Get potential targets from each category
        List<ArmyTargetScore> allCandidates = new List<ArmyTargetScore>();

        // Category 1: Non-neutral Armies not in PCs (highest priority)
        Army target1 = FindTargetNonNeutralArmyNotInPCInRange(hexes, character);
        if (target1 != null)
        {
            float distance = Vector2.Distance(character.hex.v2, target1.commander.hex.v2);
            float strength = CalculateArmyStrength(target1);
            float score = distance + strength;
            allCandidates.Add(new ArmyTargetScore
            {
                Target = target1,
                TotalScore = score,
                CategoryPriority = 1 // Highest priority
            });
        }

        // Category 2: Any Army not in PC
        Army target2 = FindTargetAnyArmyNotInPCInRange(hexes, character);
        if (target2 != null)
        {
            float distance = Vector2.Distance(character.hex.v2, target2.commander.hex.v2);
            float strength = CalculateArmyStrength(target2);
            float score = distance + strength;
            allCandidates.Add(new ArmyTargetScore
            {
                Target = target2,
                TotalScore = score,
                CategoryPriority = 2
            });
        }

        // Category 3: Non-neutral Army (including in PCs)
        Army target3 = FindTargetNonNeutralArmyInRange(hexes, character);
        if (target3 != null)
        {
            float distance = Vector2.Distance(character.hex.v2, target3.commander.hex.v2);
            float strength = CalculateArmyStrength(target3);
            float score = distance + strength;
            allCandidates.Add(new ArmyTargetScore
            {
                Target = target3,
                TotalScore = score,
                CategoryPriority = 3
            });
        }

        // Category 4: Any Army
        Army target4 = FindTargetAnyEnemyArmyInRange(hexes, character);
        if (target4 != null)
        {
            float distance = Vector2.Distance(character.hex.v2, target4.commander.hex.v2);
            float strength = CalculateArmyStrength(target4);
            float score = distance + strength;
            allCandidates.Add(new ArmyTargetScore
            {
                Target = target4,
                TotalScore = score,
                CategoryPriority = 4 // Lowest priority
            });
        }

        // Return null if no targets found
        if (allCandidates.Count == 0)
        {
            return null;
        }

        // Sort by score (lowest is best)
        allCandidates = allCandidates.OrderBy(c => c.TotalScore).ToList();

        // Get best candidate
        ArmyTargetScore bestCandidate = allCandidates[0];
        float bestScore = bestCandidate.TotalScore;

        // Check if any scores are within 10% of the best score
        // If so, use category priority as a tiebreaker
        List<ArmyTargetScore> closeScoreCandidates = new List<ArmyTargetScore>();
        closeScoreCandidates.Add(bestCandidate);

        for (int i = 1; i < allCandidates.Count; i++)
        {
            float scoreDifference = allCandidates[i].TotalScore - bestScore;
            float percentDifference = bestScore > 0 ? (scoreDifference / bestScore) * 100 : 100;

            // If score is within 10% of best score, add to close candidates
            if (percentDifference <= 10)
            {
                closeScoreCandidates.Add(allCandidates[i]);
            }
        }

        // If we have multiple candidates with close scores, sort by category priority
        if (closeScoreCandidates.Count > 1)
        {
            return closeScoreCandidates.OrderBy(c => c.CategoryPriority).First().Target;
        }

        // Otherwise, return the candidate with the best score
        return bestCandidate.Target;
    }

    // Helper class to track potential army targets with their scores
    private class ArmyTargetScore
    {
        public Army Target { get; set; }
        public float DistanceScore { get; set; }
        public float StrengthScore { get; set; }
        public float TotalScore { get; set; }
        public int CategoryPriority { get; set; } // 1-4, lower is higher priority
    }

    // Helper method to calculate army strength (lower is better for attacker)
    private float CalculateArmyStrength(Army army)
    {
        return army.GetDefence();
    }

    private Army FindTargetNonNeutralArmyNotInPCInRange(List<Hex> hexes, Character character)
    {
        // Prioritize non-neutral Armies that aren't in PCs
        List<ArmyTargetScore> potentialTargets = new List<ArmyTargetScore>();

        foreach (Hex hex in hexes)
        {
            if (hex.armies != null && hex.armies.Count > 0 && hex.GetPC() == null)
            {
                foreach (Army army in hex.armies)
                {
                    if (army.commander != null &&
                        army.commander.GetOwner() != character.GetOwner() &&
                        army.commander.GetOwner().GetAlignment() != character.GetAlignment() &&
                        army.commander.GetOwner().GetAlignment() != AlignmentEnum.neutral)
                    {
                        // Calculate scores (lower is better)
                        float distance = Vector2.Distance(character.hex.v2, hex.v2);
                        float strengthScore = CalculateArmyStrength(army);

                        // Combine scores (lower total = better target)
                        float totalScore = distance + strengthScore;

                        potentialTargets.Add(new ArmyTargetScore
                        {
                            Target = army,
                            DistanceScore = distance,
                            StrengthScore = strengthScore,
                            TotalScore = totalScore
                        });
                    }
                }
            }
        }

        // Return the target with the lowest total score (best target)
        if (potentialTargets.Count > 0)
        {
            return potentialTargets.OrderBy(t => t.TotalScore).First().Target;
        }

        return null;
    }

    private Army FindTargetAnyArmyNotInPCInRange(List<Hex> hexes, Character character)
    {
        // Find any Army that isn't in a PC, including neutral ones
        List<ArmyTargetScore> potentialTargets = new List<ArmyTargetScore>();

        foreach (Hex hex in hexes)
        {
            if (hex.armies != null && hex.armies.Count > 0 && hex.GetPC() == null)
            {
                foreach (Army army in hex.armies)
                {
                    if (army.commander != null &&
                        army.commander.GetOwner() != character.GetOwner())
                    {
                        // Calculate scores (lower is better)
                        float distance = Vector2.Distance(character.hex.v2, hex.v2);
                        float strengthScore = CalculateArmyStrength(army);

                        // Combine scores (lower total = better target)
                        float totalScore = distance + strengthScore;

                        potentialTargets.Add(new ArmyTargetScore
                        {
                            Target = army,
                            DistanceScore = distance,
                            StrengthScore = strengthScore,
                            TotalScore = totalScore
                        });
                    }
                }
            }
        }

        // Return the target with the lowest total score (best target)
        if (potentialTargets.Count > 0)
        {
            return potentialTargets.OrderBy(t => t.TotalScore).First().Target;
        }

        return null;
    }

    private Army FindTargetNonNeutralArmyInRange(List<Hex> hexes, Character character)
    {
        // Prioritize non-neutral Armies, including those in PCs
        List<ArmyTargetScore> potentialTargets = new List<ArmyTargetScore>();

        foreach (Hex hex in hexes)
        {
            if (hex.armies != null && hex.armies.Count > 0)
            {
                foreach (Army army in hex.armies)
                {
                    if (army.commander != null &&
                        army.commander.GetOwner() != character.GetOwner() &&
                        army.commander.GetOwner().GetAlignment() != character.GetAlignment() &&
                        army.commander.GetOwner().GetAlignment() != AlignmentEnum.neutral)
                    {
                        // Calculate scores (lower is better)
                        float distance = Vector2.Distance(character.hex.v2, hex.v2);
                        float strengthScore = CalculateArmyStrength(army);

                        // Combine scores (lower total = better target)
                        float totalScore = distance + strengthScore;

                        potentialTargets.Add(new ArmyTargetScore
                        {
                            Target = army,
                            DistanceScore = distance,
                            StrengthScore = strengthScore,
                            TotalScore = totalScore
                        });
                    }
                }
            }
        }

        // Return the target with the lowest total score (best target)
        if (potentialTargets.Count > 0)
        {
            return potentialTargets.OrderBy(t => t.TotalScore).First().Target;
        }

        return null;
    }

    private Army FindTargetAnyEnemyArmyInRange(List<Hex> hexes, Character character)
    {
        // Find any Army, including neutral ones and those in PCs
        List<ArmyTargetScore> potentialTargets = new List<ArmyTargetScore>();

        foreach (Hex hex in hexes)
        {
            if (hex.armies != null && hex.armies.Count > 0)
            {
                foreach (Army army in hex.armies)
                {
                    if (army.commander != null &&
                        army.commander.GetOwner() != character.GetOwner())
                    {
                        // Calculate scores (lower is better)
                        float distance = Vector2.Distance(character.hex.v2, hex.v2);
                        float strengthScore = CalculateArmyStrength(army);

                        // Combine scores (lower total = better target)
                        float totalScore = distance + strengthScore;

                        potentialTargets.Add(new ArmyTargetScore
                        {
                            Target = army,
                            DistanceScore = distance,
                            StrengthScore = strengthScore,
                            TotalScore = totalScore
                        });
                    }
                }
            }
        }

        // Return the target with the lowest total score (best target)
        if (potentialTargets.Count > 0)
        {
            return potentialTargets.OrderBy(t => t.TotalScore).First().Target;
        }

        return null;
    }

    public int GetGoldCost()
    {
        return goldCost 
        + ironCost*StoresManager.IronSellValue 
        + mountsCost*StoresManager.MountsSellValue
        + timberCost*StoresManager.TimberSellValue
        + mithrilCost*StoresManager.MithrilSellValue
        + leatherCost*StoresManager.LeatherSellValue;
    }

    protected virtual string GetDescription()
    {
        return string.IsNullOrWhiteSpace(description) ? string.Empty : description;
    }

    private string BuildCostText()
    {
        if (isBuyCaravans) return BuildBuyCaravanCostText();
        if (isSellCaravans) return BuildSellCaravanCostText();
        return BuildStandardCostText();
    }

    private string BuildStandardCostText()
    {
        List<string> costs = new();
        if (leatherCost > 0) costs.Add($"<color=red>-<sprite name=\"leather\"/>[{leatherCost}]</color>");
        if (timberCost > 0) costs.Add($"<color=red>-<sprite name=\"timber\"/>[{timberCost}]</color>");
        if (mountsCost > 0) costs.Add($"<color=red>-<sprite name=\"mounts\"/>[{mountsCost}]</color>");
        if (ironCost > 0) costs.Add($"<color=red>-<sprite name=\"iron\"/>[{ironCost}]</color>");
        if (mithrilCost > 0) costs.Add($"<color=red>-<sprite name=\"mithril\"/>[{mithrilCost}]</color>");
        if (goldCost > 0) costs.Add($"<color=red>-<sprite name=\"gold\"/>[{goldCost}]</color>");
        return string.Join(" ", costs);
    }

    private IEnumerable<(int amount, string sprite, ProducesEnum produce)> GetResourceCosts()
    {
        if (leatherCost > 0) yield return (leatherCost, "leather", ProducesEnum.leather);
        if (timberCost > 0) yield return (timberCost, "timber", ProducesEnum.timber);
        if (mountsCost > 0) yield return (mountsCost, "mounts", ProducesEnum.mounts);
        if (ironCost > 0) yield return (ironCost, "iron", ProducesEnum.iron);
        if (steelCost > 0) yield return (steelCost, "steel", ProducesEnum.steel);
        if (mithrilCost > 0) yield return (mithrilCost, "mithril", ProducesEnum.mithril);
    }

    private struct ActionCostSnapshot
    {
        public bool isCaravan;
        public int goldDelta; // positive means gold gained, negative means gold spent
        public List<(ProducesEnum produce, int amount)> resourceCosts;
        public bool hasRequiredStock;
    }

    private ActionCostSnapshot CalculateCostSnapshot()
    {
        ActionCostSnapshot snapshot = new()
        {
            isCaravan = isBuyCaravans || isSellCaravans,
            goldDelta = 0,
            resourceCosts = new List<(ProducesEnum produce, int amount)>(),
            hasRequiredStock = true
        };

        if (!snapshot.isCaravan) return snapshot;

        StoresManager stores = GetStoresManager();
        if (stores == null)
        {
            snapshot.hasRequiredStock = false;
            return snapshot;
        }

        // goldCost is always treated as a spend (negative delta)
        snapshot.goldDelta -= goldCost;

        foreach (var cost in GetResourceCosts())
        {
            snapshot.resourceCosts.Add((cost.produce, cost.amount));
            if (isBuyCaravans)
            {
                if (!stores.HasStock(cost.produce, cost.amount)) snapshot.hasRequiredStock = false;
                snapshot.goldDelta -= stores.GetBuyPrice(cost.produce, cost.amount);
            }
            else if (isSellCaravans)
            {
                snapshot.goldDelta += stores.GetSellPrice(cost.produce, cost.amount);
            }
        }

        return snapshot;
    }

    private string BuildBuyCaravanCostText()
    {
        StoresManager stores = GetStoresManager();
        if (stores == null) return BuildStandardCostText();

        int totalGold = goldCost;
        foreach (var cost in GetResourceCosts())
        {
            totalGold += stores.GetBuyPrice(cost.produce, cost.amount);
        }

        if (totalGold <= 0) return string.Empty;
        return $"<color=red>-<sprite name=\"gold\"/>[{totalGold}]</color>";
    }

    private string BuildSellCaravanCostText()
    {
        StoresManager stores = GetStoresManager();
        if (stores == null) return BuildStandardCostText();

        List<string> parts = new();
        int totalGold = goldCost;

        foreach (var cost in GetResourceCosts())
        {
            parts.Add($"<color=red>-<sprite name=\"{cost.sprite}\"/>[{cost.amount}]</color>");
            totalGold += stores.GetSellPrice(cost.produce, cost.amount);
        }

        if (totalGold > 0)
        {
            parts.Add($"<color=green>+<sprite name=\"gold\"/>[{totalGold}]</color>");
        }

        return string.Join(" ", parts);
    }

    protected virtual string BuildHoverText()
    {
        string strippedTitle = ActionNameUtils.StripShortcut(actionName);
        string title = string.IsNullOrWhiteSpace(strippedTitle) ? actionName : strippedTitle;

        List<string> parts = new() { title };
        string desc = GetDescription();
        if (!string.IsNullOrWhiteSpace(desc)) parts.Add($"<br><size=80%>{desc}</size>");

        string detail = BuildCaravanDetailText();
        if (string.IsNullOrWhiteSpace(detail))
        {
            detail = BuildStandardDetailText();
        }
        if (!string.IsNullOrWhiteSpace(detail)) parts.Add($"<br><size=80%>{detail}</size>");

        return string.Join("", parts);
    }

    private string BuildUnavailableHoverText()
    {
        string baseText = BuildHoverText();
        string missing = BuildMissingRequirementsText();
        if (string.IsNullOrWhiteSpace(missing)) return baseText;
        return $"{baseText}<br><size=80%><color=red>{missing}</color></size>";
    }

    private string BuildMissingRequirementsText()
    {
        if (character == null) return "No character selected.";

        List<string> parts = new();
        bool providedByArtifact = IsProvidedByArtifact();

        if (character.killed) parts.Add("Character is dead.");
        if (character.hasActionedThisTurn && actionName != FindFirstObjectByType<ActionsManager>().DEFAULT.actionName)
        {
            parts.Add("Already actioned this turn.");
        }

        if (!providedByArtifact)
        {
            if (commanderSkillRequired > 0 && character.GetCommander() < commanderSkillRequired)
            {
                parts.Add($"Need <sprite name=\"commander\">[{commanderSkillRequired}] (have {character.GetCommander()})");
            }
            if (agentSkillRequired > 0 && character.GetAgent() < agentSkillRequired)
            {
                parts.Add($"Need <sprite name=\"agent\">[{agentSkillRequired}] (have {character.GetAgent()})");
            }
            if (emissarySkillRequired > 0 && character.GetEmmissary() < emissarySkillRequired)
            {
                parts.Add($"Need <sprite name=\"emmissary\">[{emissarySkillRequired}] (have {character.GetEmmissary()})");
            }
            if (mageSkillRequired > 0 && character.GetMage() < mageSkillRequired)
            {
                parts.Add($"Need <sprite name=\"mage\">[{mageSkillRequired}] (have {character.GetMage()})");
            }
        }

        Leader owner = character.GetOwner();
        if (owner != null)
        {
            if (isBuyCaravans || isSellCaravans)
            {
                ActionCostSnapshot snapshot = CalculateCostSnapshot();
                if (!snapshot.hasRequiredStock)
                {
                    parts.Add("Market lacks required stock.");
                }

                if (snapshot.goldDelta < 0 && owner.goldAmount < -snapshot.goldDelta)
                {
                    parts.Add($"Need <sprite name=\"gold\"/>[{Mathf.Abs(snapshot.goldDelta)}] (have {owner.goldAmount})");
                }

                if (isSellCaravans)
                {
                    foreach (var cost in snapshot.resourceCosts)
                    {
                        int available = cost.produce switch
                        {
                            ProducesEnum.leather => owner.leatherAmount,
                            ProducesEnum.timber => owner.timberAmount,
                            ProducesEnum.mounts => owner.mountsAmount,
                            ProducesEnum.iron => owner.ironAmount,
                            ProducesEnum.steel => owner.steelAmount,
                            ProducesEnum.mithril => owner.mithrilAmount,
                            _ => 0
                        };
                        if (available < cost.amount)
                        {
                            parts.Add($"Need <sprite name=\"{cost.produce}\">[{cost.amount}] (have {available})");
                        }
                    }
                }
            }
            else
            {
                if (leatherCost > 0 && owner.leatherAmount < leatherCost)
                    parts.Add($"Need <sprite name=\"leather\"/>[{leatherCost}] (have {owner.leatherAmount})");
                if (timberCost > 0 && owner.timberAmount < timberCost)
                    parts.Add($"Need <sprite name=\"timber\"/>[{timberCost}] (have {owner.timberAmount})");
                if (mountsCost > 0 && owner.mountsAmount < mountsCost)
                    parts.Add($"Need <sprite name=\"mounts\"/>[{mountsCost}] (have {owner.mountsAmount})");
                if (ironCost > 0 && owner.ironAmount < ironCost)
                    parts.Add($"Need <sprite name=\"iron\"/>[{ironCost}] (have {owner.ironAmount})");
                if (steelCost > 0 && owner.steelAmount < steelCost)
                    parts.Add($"Need <sprite name=\"steel\"/>[{steelCost}] (have {owner.steelAmount})");
                if (mithrilCost > 0 && owner.mithrilAmount < mithrilCost)
                    parts.Add($"Need <sprite name=\"mithril\"/>[{mithrilCost}] (have {owner.mithrilAmount})");
                if (goldCost > 0 && owner.goldAmount < goldCost)
                    parts.Add($"Need <sprite name=\"gold\"/>[{goldCost}] (have {owner.goldAmount})");
            }
        }

        if (parts.Count == 0 && !FulfillsConditions())
        {
            parts.Add("No valid targets or requirements not met.");
        }

        return string.Join("<br>", parts);
    }

    private string BuildCaravanDetailText()
    {
        ActionCostSnapshot snapshot = CalculateCostSnapshot();
        if (!snapshot.isCaravan) return string.Empty;

        List<string> parts = new();
        if (isBuyCaravans)
        {
            if (snapshot.goldDelta < 0)
            {
                parts.Add($"<color=red>-<sprite name=\"gold\"/>[{Mathf.Abs(snapshot.goldDelta)}]</color>");
            }
        }
        else if (isSellCaravans)
        {
            foreach (var cost in snapshot.resourceCosts)
            {
                parts.Add($"<color=red>-<sprite name=\"{cost.produce}\">[{cost.amount}]</color>");
            }

            if (snapshot.goldDelta > 0)
            {
                parts.Add($"<color=green>+<sprite name=\"gold\"/>[{snapshot.goldDelta}]</color>");
            }
            else if (snapshot.goldDelta < 0)
            {
                parts.Add($"<color=red>-<sprite name=\"gold\"/>[{Mathf.Abs(snapshot.goldDelta)}]</color>");
            }
        }

        return string.Join(" ", parts);
    }

    private string BuildStandardDetailText()
    {
        List<string> parts = new();

        foreach (var cost in GetResourceCosts())
        {
            parts.Add($"<color=red>-<sprite name=\"{cost.sprite}\"/>[{cost.amount}]</color>");
        }

        if (goldCost > 0)
        {
            parts.Add($"<color=red>-<sprite name=\"gold\"/>[{goldCost}]</color>");
        }

        return string.Join(" ", parts);
    }

    private void ApplyCaravanPayments(ActionCostSnapshot snapshot)
    {
        if (!snapshot.isCaravan) return;
        if (character == null || character.GetOwner() == null) return;

        Leader owner = character.GetOwner();

        if (isSellCaravans)
        {
            foreach (var cost in snapshot.resourceCosts)
            {
                switch (cost.produce)
                {
                    case ProducesEnum.leather:
                        owner.RemoveLeather(cost.amount);
                        break;
                    case ProducesEnum.timber:
                        owner.RemoveTimber(cost.amount);
                        break;
                    case ProducesEnum.mounts:
                        owner.RemoveMounts(cost.amount);
                        break;
                    case ProducesEnum.iron:
                        owner.RemoveIron(cost.amount);
                        break;
                    case ProducesEnum.steel:
                        owner.RemoveSteel(cost.amount);
                        break;
                    case ProducesEnum.mithril:
                        owner.RemoveMithril(cost.amount);
                        break;
                }
            }
        }

        if (snapshot.goldDelta < 0)
        {
            owner.RemoveGold(-snapshot.goldDelta);
        }
        else if (snapshot.goldDelta > 0)
        {
            owner.AddGold(snapshot.goldDelta);
        }
    }

    private void ApplySpellFailurePenalty(bool isAI)
    {
        if (character == null || character.killed) return;
        int damage = UnityEngine.Random.Range(1, 6);
        character.health = Mathf.Max(0, character.health - damage);
        Sounds.Instance?.PlayVoicePain(character);
        if (!isAI)
        {
            MessageDisplayNoUI.ShowMessage(character.hex, character, $"-{damage} <sprite name=\"health\">", Color.red);
        }
        if (character.health < 1)
        {
            character.Killed(null);
        }
        else
        {
            character.RefreshSelectedCharacterIconIfSelected();
            CharacterIcons.RefreshForHumanPlayerCharacter(character);
        }
    }

    private StoresManager GetStoresManager()
    {
        if (cachedStoresManager != null) return cachedStoresManager;
        cachedStoresManager = FindFirstObjectByType<StoresManager>();
        return cachedStoresManager;
    }

    private bool PlayerCanSeeHex(Hex hex)
    {
        if (hex == null) return false;
        Game g = game != null ? game : FindFirstObjectByType<Game>();
        if (g == null || g.player == null) return false;
        return g.player.visibleHexes.Contains(hex) && hex.IsHexSeen();
    }
}
