using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RavensOverTheFordAction : EventAction
{
    private const int Radius = 2;

    private static bool IsAllied(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return true;
        return source.GetAlignment() != AlignmentEnum.neutral
            && target.GetAlignment() == source.GetAlignment()
            && target.GetAlignment() != AlignmentEnum.neutral;
    }

    private static bool IsHumanLike(Character ch) =>
        ch != null && (ch.race == RacesEnum.Common || ch.race == RacesEnum.Dunedain);

    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        List<Character> allChars = board.GetHexes()
            .Where(h => h != null && h.characters != null)
            .SelectMany(h => h.characters)
            .Where(ch => ch != null && !ch.killed)
            .Distinct().ToList();

        // Allied Humans/Dunedain get Haste; enemy Humans/Dunedain get Fear+Halted
        // Ravens bring intelligence: all agents gain ArcaneInsight
        int hasted = 0, feared = 0, insight = 0;
        foreach (Character ch in allChars)
        {
            if (ch.GetAgent() > 0) { ch.ApplyStatusEffect(StatusEffectEnum.ArcaneInsight, 1); insight++; }
            if (!IsHumanLike(ch)) continue;
            if (ch.GetAlignment() == AlignmentEnum.freePeople)
            {
                ch.ApplyStatusEffect(StatusEffectEnum.Haste, 1);
                hasted++;
            }
            else if (ch.GetAlignment() == AlignmentEnum.darkServants)
            {
                ch.ApplyStatusEffect(StatusEffectEnum.Fear, 1);
                ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement());
                feared++;
            }
        }
        MessageDisplayNoUI.ShowMessage(null, null,
            $"Ravens (ongoing): {hasted} allied Men/Dunedain hasted; {feared} enemy Men feared and halted; {insight} agents gain insight.",
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
            if (character == null || character.hex == null) return false;

            List<Character> nearby = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && IsHumanLike(ch))
                .Distinct()
                .ToList();

            if (nearby.Count == 0) return false;

            int swiftAllies = 0;
            int shakenEnemies = 0;

            for (int i = 0; i < nearby.Count; i++)
            {
                Character target = nearby[i];
                if (IsAllied(character, target))
                {
                    target.ApplyStatusEffect(StatusEffectEnum.Haste, 1);
                    swiftAllies++;
                }
                else
                {
                    target.ApplyStatusEffect(StatusEffectEnum.Fear, 1);
                    target.ApplyStatusEffect(StatusEffectEnum.Halted, 1);
                    shakenEnemies++;
                }
            }

            if (swiftAllies == 0 && shakenEnemies == 0) return false;

            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"Ravens: {swiftAllies} allied Human/Dunedain unit(s) gain Haste (1); {shakenEnemies} enemy Human/Dunedain unit(s) gain Fear and Halted (1).",
                Color.black);

            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            return character.hex.GetHexesInRadius(Radius)
                .Any(h => h != null
                    && h.characters != null
                    && h.characters.Any(ch => ch != null && !ch.killed && IsHumanLike(ch)));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
