using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class BearerOfNenya : CharacterAction
{
    private static bool IsAllied(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return true;
        return source.GetAlignment() != AlignmentEnum.neutral
            && target.GetAlignment() == source.GetAlignment()
            && target.GetAlignment() != AlignmentEnum.neutral;
    }

    private static bool IsAlliedPc(Character source, PC pc)
    {
        if (source == null || pc == null || pc.owner == null) return false;
        if (pc.owner == source.GetOwner()) return true;
        return source.GetAlignment() != AlignmentEnum.neutral
            && pc.owner.GetAlignment() == source.GetAlignment()
            && pc.owner.GetAlignment() != AlignmentEnum.neutral;
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            return character.hex.GetHexesInRadius(2)
                .Any(h => h != null && IsAlliedPc(character, h.GetPCData()));
        };

        async Task<bool> nenyaAsync(Character character)
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            if (character == null || character.hex == null) return false;

            List<PC> alliedPcs = character.hex.GetHexesInRadius(2)
                .Where(h => h != null)
                .Select(h => h.GetPCData())
                .Where(pc => IsAlliedPc(character, pc))
                .Distinct()
                .ToList();

            if (alliedPcs.Count == 0) return false;

            bool isAI = !character.isPlayerControlled;
            PC targetPc = null;

            if (!isAI)
            {
                List<string> options = alliedPcs.Select(pc => pc.pcName).Distinct().ToList();
                string selected = await SelectionDialog.Ask(
                    "Select allied PC",
                    "Ok",
                    "Cancel",
                    options,
                    false,
                    SelectionDialog.Instance != null ? SelectionDialog.Instance.GetCharacterIllustration(character) : null);

                if (string.IsNullOrWhiteSpace(selected)) return false;
                targetPc = alliedPcs.FirstOrDefault(pc => pc.pcName == selected);
            }
            else
            {
                targetPc = alliedPcs.FirstOrDefault();
            }

            if (targetPc == null || targetPc.hex == null) return false;

            targetPc.SetTemporaryHidden(2);
            targetPc.hex.RedrawPC();

            List<Character> nearbyAllies = targetPc.hex.GetHexesInRadius(1)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && IsAllied(character, ch))
                .Distinct()
                .ToList();

            foreach (Character ally in nearbyAllies)
            {
                ally.ApplyStatusEffect(StatusEffectEnum.Encouraged, 1);
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Nenya veils {targetPc.pcName} (2 turns) and inspires {nearbyAllies.Count} allied unit(s).", Color.cyan);
            return true;
        }

        base.Initialize(c, condition, effect, nenyaAsync);
    }
}
