using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class LightThroughCloudAction : EventAction
{
    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        List<Character> allChars = board.GetHexes()
            .Where(h => h != null && h.characters != null)
            .SelectMany(h => h.characters)
            .Where(ch => ch != null && !ch.killed)
            .Distinct().ToList();

        int encouraged = 0, halted = 0, mageInsight = 0;
        foreach (Character ch in allChars)
        {
            if (ch.GetAlignment() == AlignmentEnum.freePeople)
            {
                ch.Encourage(1);
                ch.ClearStatusEffect(StatusEffectEnum.Fear);
                if (ch.GetMage() > 0) { ch.ApplyStatusEffect(StatusEffectEnum.ArcaneInsight, 1); mageInsight++; }
                encouraged++;
            }
            else if (ch.GetAlignment() == AlignmentEnum.darkServants && !ch.IsImmuneToNegativeEnvironmentalCards())
            {
                if (UnityEngine.Random.value < 0.30f)
                {
                    ch.Halt(1);
                    halted++;
                }
            }
        }
        MessageDisplayNoUI.ShowMessage(null, null,
            $"Light Through Cloud (ongoing): {encouraged} allied units encouraged; {halted} dark units halted; {mageInsight} mages gain insight.",
            Color.yellow);
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

            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;

            List<Character> allies = character.hex.characters
                .Where(ch => ch != null && !ch.killed && ch.GetAlignment() == character.GetAlignment()).ToList();
            List<Character> enemies = character.hex.characters
                .Where(ch => ch != null && !ch.killed && ch.GetAlignment() != character.GetAlignment()).ToList();

            foreach (Character ally in allies) { ally.Encourage(1); ally.ClearStatusEffect(StatusEffectEnum.Fear); }
            foreach (Character enemy in enemies) enemy.Halt(1);

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"Light Through Cloud: {allies.Count} allies encouraged; {enemies.Count} enemies halted.",
                Color.yellow);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            return character != null && character.hex != null;
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
