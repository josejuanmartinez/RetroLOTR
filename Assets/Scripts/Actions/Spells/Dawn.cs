using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Dawn : EventAction
{
    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        List<Character> allUnits = board.GetHexes()
            .Where(h => h != null && h.characters != null)
            .SelectMany(h => h.characters)
            .Where(ch => ch != null && !ch.killed)
            .Distinct().ToList();

        int encouraged = 0, feared = 0, halted = 0;
        foreach (Character ch in allUnits)
        {
            AlignmentEnum al = ch.GetAlignment();
            if (al == AlignmentEnum.freePeople)
            {
                ch.ClearStatusEffect(StatusEffectEnum.Fear);
                ch.ClearStatusEffect(StatusEffectEnum.Despair);
                ch.Encourage(1);
                encouraged++;
            }
            else if (al == AlignmentEnum.darkServants)
            {
                ch.ApplyStatusEffect(StatusEffectEnum.Despair, 1);
                if (ch.race == RacesEnum.Troll || ch.race == RacesEnum.Undead)
                {
                    ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement());
                    halted++;
                }
                feared++;
            }
        }
        MessageDisplayNoUI.ShowMessage(null, null,
            $"Dawn (ongoing): {encouraged} Free People encouraged; {feared} dark servants despaired; {halted} sun-weakened creatures halted.",
            Color.yellow);
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (c) =>
        {
            if (originalEffect != null && !originalEffect(c)) return false;
            if (c == null) return false;

            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;

            List<Character> allUnits = board.GetHexes()
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed)
                .Distinct()
                .ToList();

            List<Character> freePeople = allUnits
                .Where(ch => ch.GetAlignment() == AlignmentEnum.freePeople)
                .ToList();
            List<Character> darkServants = allUnits
                .Where(ch => ch.GetAlignment() == AlignmentEnum.darkServants)
                .ToList();

            if (freePeople.Count == 0) return false;

            for (int i = 0; i < darkServants.Count; i++)
            {
                darkServants[i].ClearEncouraged();
            }

            int turns = 1;
            for (int i = 0; i < freePeople.Count; i++)
            {
                freePeople[i].ClearEncouraged();
                freePeople[i].Encourage(turns);
            }

            MessageDisplayNoUI.ShowMessage(c.hex, c, $"Dawn grants Courage to {freePeople.Count} free people unit(s) for {turns} turn(s), dispelling Doors of Night!", Color.green);
            return true;
        };

        condition = (c) =>
        {
            if (originalCondition != null && !originalCondition(c)) return false;
            if (c == null || c.GetAlignment() == AlignmentEnum.darkServants) return false;
            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;
            return board.GetHexes().Any(h => h != null && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed && ch.GetAlignment() == AlignmentEnum.freePeople));
        };

        asyncEffect = async (c) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
