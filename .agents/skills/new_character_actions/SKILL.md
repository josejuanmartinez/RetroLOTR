---
name: new-character-action
description: Create or update RetroLOTR character actions by implementing C# action classes under Assets/Scripts/Actions and wiring them through the owning card JSON entry when the current deck schema still uses an action reference. Use this when adding gameplay actions, spells, or action variants that must execute through CharacterAction.
---

# New Character Action

Implement actions in this project through the existing `CharacterAction` pipeline.

## Source Of Truth
- Treat `Assets/Scripts/CharacterAction.cs` as the execution contract (costs, failure, XP, unlock checks, UI refresh, messages).
- Treat `Assets/Scripts/UI/ActionsManager.cs` as the registration and UI contract (load order, class resolution, button wiring).
- Treat the owning card JSON entry as the action registry for this branch; keep only the fields the current deck schema actually uses on the card record.

## Action Class Hierarchy
Choose the closest base class first, then add specific behavior in `Initialize(...)`.

- Root: `CharacterAction`
- Role bases:
  - `CommanderAction`
  - `AgentAction`
  - `EmmissaryAction`
  - `MageAction`
  - `Spell` (special: usable by mage)
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
5. Add or update the corresponding card JSON entry so it points at the action class using the fields the current deck schema actually uses.
6. Create a new card image for the action by using the `new-image` skill and save it in the correct `Assets/Art/Cards/...` folder (`Actions` or `Actions/Spells` for spells).
7. Verify runtime resolution and visibility in the Actions UI.

## Design Rule
- Every new action must express a unique, immersive mechanic; avoid building another thin wrapper around an existing effect unless the wrapper creates a clearly different play pattern.
- Do not keep repeating the same status effects over and over again across new actions.

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

## Card Linkage Contract
Each action must be referenced by a card JSON entry using the current deck schema fields for that deck. When a deck still uses an action reference, prefer the existing `action` field and only preserve `actionClassName` or `actionId` if the schema already contains them.

`ActionsManager` resolves action classes dynamically from code. In this repo there is no separate action registry file; the card JSON is the metadata source of truth for card-linked actions.

## Important Project-Specific Pitfalls
- Keep project spelling as-is: `Emmissary` (double `s`) is intentional across code and JSON.
- File name does not have to match class name, but class name must match the reference used by the card JSON when one is present.
- Prefer exact class-name matches in the card JSON even though `ActionsManager.ResolveActionType` has normalized fallback logic.
- Do not bypass `base.Initialize(...)`; skipping it drops core checks and UI behavior.

## Validation Checklist
- Action class compiles and derives from the intended base class.
- Card JSON linkage exists and uses the intended current-schema action reference fields.
- Action appears for eligible characters and is hidden/disabled correctly when unavailable.
- Action executes once per turn and consumes/awards resources and XP as expected.
