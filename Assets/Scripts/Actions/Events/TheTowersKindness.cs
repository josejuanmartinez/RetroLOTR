using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TheTowersKindness : EventAction
{
    private const int InsightTurns = 3;
    private const int HaltedTurns = 1;

    private static bool IsAllied(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return true;
        return source.GetAlignment() != AlignmentEnum.neutral
            && target.GetAlignment() == source.GetAlignment()
            && target.GetAlignment() != AlignmentEnum.neutral;
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null || character.hex == null) return false;

            PC pc = character.hex.GetPC();
            if (pc == null || pc.pcName != "Orthanc") return false;

            List<Character> mages = character.hex.GetHexesInRadius(0)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.GetMage() > 0 && IsAllied(character, ch))
                .Distinct()
                .ToList();

            if (mages.Count == 0) return false;

            foreach (Character mage in mages)
            {
                mage.ApplyStatusEffect(StatusEffectEnum.ArcaneInsight, InsightTurns);
                mage.ApplyStatusEffect(StatusEffectEnum.Halted, HaltedTurns);
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character, $"The Tower's Kindness grants {mages.Count} mage(s) at Orthanc Arcane Insight for {InsightTurns} turns, but Halted for {HaltedTurns} turn.", Color.red);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            PC pc = character.hex.GetPC();
            if (pc == null || pc.pcName != "Orthanc") return false;

            return character.hex.GetHexesInRadius(0)
                .Any(h => h != null && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed && ch.GetMage() > 0 && IsAllied(character, ch)));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
