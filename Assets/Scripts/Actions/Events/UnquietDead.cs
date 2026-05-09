using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class UnquietDead : EventAction
{
    private const int ReviveHealth = 50;

    private static bool IsEnemy(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return false;
        return target.GetAlignment() != source.GetAlignment() || source.GetAlignment() == AlignmentEnum.neutral;
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null) return false;
            Leader owner = character.GetOwner();
            if (owner == null) return false;
            return UnityEngine.Object.FindObjectsByType<Character>(FindObjectsSortMode.None)
                .Any(ch => ch != null && ch.killed && ch.GetOwner() == owner);
        };

        async Task<bool> unquietAsync(Character character)
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            if (character == null || character.hex == null) return false;

            Leader owner = character.GetOwner();
            if (owner == null) return false;

            List<Character> deadAllies = UnityEngine.Object.FindObjectsByType<Character>(FindObjectsSortMode.None)
                .Where(ch => ch != null && ch.killed && ch.GetOwner() == owner)
                .ToList();

            if (deadAllies.Count == 0) return false;

            bool isAI = !character.isPlayerControlled;
            Character target = null;

            if (!isAI)
            {
                string selected = await SelectionDialog.Ask(
                    "Raise from the dead",
                    "Ok",
                    "Cancel",
                    deadAllies.Select(x => x.characterName).ToList(),
                    false,
                    SelectionDialog.Instance != null ? SelectionDialog.Instance.GetCharacterIllustration(character) : null);
                if (string.IsNullOrWhiteSpace(selected)) return false;
                target = deadAllies.FirstOrDefault(x => x.characterName == selected);
            }
            else
            {
                target = deadAllies.OrderByDescending(x => x.GetCommander() + x.GetMage()).FirstOrDefault();
            }

            if (target == null) return false;

            target.Revive(owner, character.hex, ReviveHealth);

            // Enemies in radius 1 of caster are Halted
            int haltedCount = 0;
            if (character.hex != null)
            {
                foreach (Hex h in character.hex.GetHexesInRadius(1))
                {
                    if (h?.characters == null) continue;
                    foreach (Character enemy in h.characters.Where(e => e != null && !e.killed && IsEnemy(character, e)).ToList())
                    {
                        enemy.Halt(1);
                        haltedCount++;
                    }
                }
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"Unquiet Dead: {target.characterName} rises with {ReviveHealth}% HP. {haltedCount} enemy(ies) in radius 1 cannot act.",
                new Color(0.5f, 0.8f, 0.5f));
            return true;
        }

        base.Initialize(c, condition, effect, unquietAsync);
    }
}
