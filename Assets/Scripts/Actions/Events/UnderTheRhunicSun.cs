using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class UnderTheRhunicSun : EventAction
{
    private static bool IsEasterling(Character ch) =>
        ch != null && (ch.race == RacesEnum.Easterling || ch.race == RacesEnum.Southron);

    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        int encouraged = 0, hasted = 0;
        foreach (Hex hex in board.GetHexes().Where(h => h != null && h.characters != null))
        {
            foreach (Character ch in hex.characters.Where(ch => ch != null && !ch.killed && IsEasterling(ch)).ToList())
            {
                ch.Encourage(1);
                encouraged++;
                if (ch.IsArmyCommander())
                {
                    hex.RevealArea(1, true, ch.GetOwner());
                    Army army = ch.GetArmy();
                    if (army != null && (army.lc > 0 || army.hc > 0))
                    {
                        ch.ApplyStatusEffect(StatusEffectEnum.Haste, 1);
                        hasted++;
                    }
                }
            }
        }
        MessageDisplayNoUI.ShowMessage(null, null,
            $"Red Sun (ongoing): {encouraged} Easterlings/Southrons encouraged; {hasted} cavalry hasted; commanders reveal hexes.",
            Color.yellow);
    }

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
            if (character == null) return false;

            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;

            List<Character> easterlings = board.GetHexes()
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.race == RacesEnum.Easterling && IsAllied(character, ch))
                .Distinct()
                .ToList();

            if (easterlings.Count == 0) return false;

            // Army commanders reveal their area
            int revealedCount = 0;
            foreach (Character easterling in easterlings.Where(e => e.IsArmyCommander() && e.hex != null))
            {
                easterling.hex.RevealArea(2, false, easterling.GetOwner());
                revealedCount++;
            }

            // First allied Easterling army gains +1 Light Cavalry
            Character firstCommander = easterlings.FirstOrDefault(e => e.IsArmyCommander() && e.GetArmy() != null);
            if (firstCommander != null)
                firstCommander.GetArmy().lc++;

            // Easterlings in the field (not in PC) gain Courage
            int encouragedCount = 0;
            foreach (Character easterling in easterlings.Where(e => e.hex != null && e.hex.GetPC() == null))
            {
                easterling.ApplyStatusEffect(StatusEffectEnum.Encouraged, 1);
                encouragedCount++;
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"Red Sun: {revealedCount} Easterling commander(s) reveal area; +1 LC for first army; {encouragedCount} unit(s) on patrol gain Courage.",
                Color.yellow);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;
            return board.GetHexes().Any(h => h != null && h.characters != null
                && h.characters.Any(ch => ch != null && !ch.killed && ch.race == RacesEnum.Easterling && IsAllied(character, ch)));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
