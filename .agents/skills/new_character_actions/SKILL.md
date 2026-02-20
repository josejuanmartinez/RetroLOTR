---
name: new-character-action
description: Create or update RetroLOTR character actions by implementing C# action classes under Assets/Scripts/Actions and wiring them through Assets/Resources/Actions.json. Use this when adding gameplay actions, spells, or action variants that must appear in the Actions UI and execute through CharacterAction.
---

# New Character Action

Implement actions in this project through the existing `CharacterAction` pipeline.

## Source Of Truth
- Treat `Assets/Scripts/CharacterAction.cs` as the execution contract (costs, failure, XP, unlock checks, UI refresh, messages).
- Treat `Assets/Scripts/UI/ActionsManager.cs` as the registration and UI contract (load order, class resolution, button wiring).
- Treat `Assets/Resources/Actions.json` as action metadata and ordering.
- Treat `Assets/Resources/SkillTree.json` as unlock gating for new actions.

## Action Class Hierarchy
Choose the closest base class first, then add specific behavior in `Initialize(...)`.

- Root: `CharacterAction`
- Role bases:
  - `CommanderAction`
  - `AgentAction`
  - `EmmissaryAction`
  - `MageAction`
  - `Spell` (special: usable by mage or by artifact provider)
- Commander specializations:
  - `CommanderArmyAction`
  - `CommanderPCAction`
  - `CommanderEnemyArmyAction`
  - `CommanderEnemyPCAction`
- Agent specializations:
  - `AgentCharacterAction`
  - `AgentPCAction`
- Emmissary specializations:
  - `EmmissaryPCAction`
  - `EmmissaryEnemyPCAction`
- Spell alignment specializations:
  - `FreeSpell`
  - `DarkSpell`
  - `DarkNeutralSpell`
  - `FreeNeutralSpell` (class name), defined in `FreeNeutralSpelll.cs` (file typo is legacy)

## Implementation Workflow
1. Pick the base class that already encodes role/target/alignment constraints.
2. Create or update the action class in `Assets/Scripts/Actions` (or `Assets/Scripts/Actions/Spells` for spells).
3. Override `Initialize(...)` and wrap delegates in the same pattern used across existing actions.
4. Call `base.Initialize(c, condition, effect, asyncEffect)` so base gating still applies.
5. Add or update the corresponding entry in `Assets/Resources/Actions.json`.
6. If the new action is a spell, explicitly ask the user whether they also want one or more artifacts that grant this spell.
7. If the user says yes, ask for artifact details and update `Assets/Resources/Artifacts.json` accordingly.
8. Verify runtime resolution and visibility in the Actions UI.

## Spell Artifact Rule
When creating or updating a spell action:
- Always ask: `Do you want to add artifact(s) that grant this spell?`
- If yes, collect artifact fields before editing:
  - `artifactName`
  - `artifactDescription`
  - `hidden`
  - `providesSpell` (match spell action name as used by artifact spell matching)
  - `alignment`
  - `transferable`
  - `oneShot`
  - `spriteString`
  - any stat bonuses (`commanderBonus`, `agentBonus`, `emmissaryBonus`, `mageBonus`, `bonusAttack`, `bonusDefense`)
- Use existing entries in `Assets/Resources/Artifacts.json` as the schema and style reference.

## Required Initialize Pattern
Use this structure so composition with base classes remains intact:

```csharp
using System;

public class MyAction : CommanderPCAction
{
    public override void Initialize(
        Character c,
        Func<Character, bool> condition = null,
        Func<Character, bool> effect = null,
        Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (c) =>
        {
            if (originalEffect != null && !originalEffect(c)) return false;
            // Perform action logic.
            return true;
        };

        condition = (c) =>
        {
            if (originalCondition != null && !originalCondition(c)) return false;
            // Add extra guards beyond base class.
            return true;
        };

        asyncEffect = async (c) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
```

## Actions.json Contract
Each action requires an entry in `Assets/Resources/Actions.json` with at least:
- `className`: must match the C# class intended for instantiation.
- `actionName`: UI label and action history label.
- `actionId`: unique id.
- Gameplay metadata: difficulty, role requirements, costs, XP, reward, advisor type.
- Visual metadata: `iconName`, `description`, `tutorialInfo`.

`ActionsManager` loads and orders actions from this file. If no prefab action matches, it instantiates a button and attaches the class dynamically.

## Important Project-Specific Pitfalls
- Keep project spelling as-is: `Emmissary` (double `s`) is intentional across code and JSON.
- Legacy class names are canonical for unlocks and JSON:
  - `TrainMetAtArms` (not `TrainMenAtArms`)
  - `WizardLaugh` (file is `WizardsLaugh.cs`)
  - `TrainWarships` (file is `TrainWarShips.cs`)
- File name does not have to match class name, but class name must match `Actions.json`.
- Prefer exact `className` matches in JSON even though `ActionsManager.ResolveActionType` has normalized fallback logic.
- Do not bypass `base.Initialize(...)`; skipping it drops core checks and UI behavior.

## Validation Checklist
- Action class compiles and derives from the intended base class.
- `Actions.json` entry exists and uses the intended `className`.
- Action appears for eligible characters and is hidden/disabled correctly when unavailable.
- Action executes once per turn and consumes/awards resources and XP as expected.
