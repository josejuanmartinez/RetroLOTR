using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SmokeOfDolGuldur : EventAction
{
    private static bool IsNecromancerCharacter(Character ch) =>
        ch != null && ch.GetMage() > 0 && ch.GetAlignment() == AlignmentEnum.darkServants;

    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        List<Character> forestChars = board.GetHexes()
            .Where(h => h != null && h.terrainType == TerrainEnum.forest && h.characters != null)
            .SelectMany(h => h.characters)
            .Where(ch => ch != null && !ch.killed)
            .Distinct().ToList();

        int poisoned = 0, despaired = 0, insightful = 0;
        foreach (Character ch in forestChars)
        {
            if (IsNecromancerCharacter(ch))
            {
                // Necromancers read the dark smoke
                ch.ApplyStatusEffect(StatusEffectEnum.ArcaneInsight, 1);
                insightful++;
                continue;
            }
            if (ch.GetAlignment() == AlignmentEnum.darkServants) continue;
            if (!ch.IsImmuneToNegativeEnvironmentalCards())
            {
                ch.ApplyStatusEffect(StatusEffectEnum.Poisoned, 1);
                poisoned++;
                if (ch.HasStatusEffect(StatusEffectEnum.Poisoned))
                {
                    ch.ApplyStatusEffect(StatusEffectEnum.Despair, 1);
                    despaired++;
                }
            }
        }
        MessageDisplayNoUI.ShowMessage(null, null,
            $"Smoke of Dol Guldur (ongoing): {poisoned} forest units poisoned; {despaired} also despaired; {insightful} dark mages gain insight. Dark servants immune.",
            Color.gray);
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null) return false;

            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;

            List<Character> forestVictims = board.GetHexes()
                .Where(h => h != null && h.terrainType == TerrainEnum.forest && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && !IsNecromancerCharacter(ch)
                    && ch.GetAlignment() != AlignmentEnum.darkServants
                    && !ch.IsImmuneToNegativeEnvironmentalCards())
                .Distinct().ToList();

            foreach (Character victim in forestVictims) victim.ApplyStatusEffect(StatusEffectEnum.Poisoned, 1);
            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"Smoke of Dol Guldur: {forestVictims.Count} forest units poisoned. Dark servants and Necromancers are immune.",
                Color.gray);
            return forestVictims.Count > 0;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;
            return board.GetHexes().Any(h => h != null && h.terrainType == TerrainEnum.forest
                && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed
                    && !IsNecromancerCharacter(ch) && ch.GetAlignment() != AlignmentEnum.darkServants));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
