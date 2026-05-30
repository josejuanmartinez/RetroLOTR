using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RuneOfTheWestAction : EventAction
{
    private const int FarFromHomeRadius = 6;

    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        List<Hex> allHexes = board.GetHexes().Where(h => h != null).ToList();

        // Build set of hex coordinates within radius 6 of any allied (free people) PC
        HashSet<Vector2Int> nearAlliedPc = new();
        foreach (Hex hex in allHexes)
        {
            PC pc = hex.GetPC();
            if (pc == null || pc.owner == null) continue;
            if (pc.owner.GetAlignment() != AlignmentEnum.freePeople) continue;
            foreach (Hex inRadius in hex.GetHexesInRadius(FarFromHomeRadius))
                if (inRadius != null) nearAlliedPc.Add(inRadius.v2);
        }

        // Allied army commanders whose hex is outside radius 6 of every allied PC
        int boosted = 0;
        foreach (Hex hex in allHexes)
        {
            if (hex.characters == null || nearAlliedPc.Contains(hex.v2)) continue;
            foreach (Character ch in hex.characters.Where(ch =>
                ch != null && !ch.killed &&
                ch.GetAlignment() == AlignmentEnum.freePeople &&
                ch.IsArmyCommander()).ToList())
            {
                Army army = ch.GetArmy();
                if (army == null) continue;
                IncrementLargestTroopType(army);
                boosted++;
            }
        }

        MessageDisplayNoUI.ShowMessage(null, null,
            $"Star of Earendil: {boosted} allied commander(s) deep in enemy territory — their largest troop type grows by 1.",
            Color.cyan);
    }

    private static void IncrementLargestTroopType(Army army)
    {
        int max = Mathf.Max(army.ma, army.ar, army.li, army.hi, army.lc, army.hc, army.ca, army.ws);
        if (max <= 0) return;
        if      (army.ma == max) army.ma++;
        else if (army.ar == max) army.ar++;
        else if (army.li == max) army.li++;
        else if (army.hi == max) army.hi++;
        else if (army.lc == max) army.lc++;
        else if (army.hc == max) army.hc++;
        else if (army.ca == max) army.ca++;
        else                     army.ws++;
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (_) => true;

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            return character != null && character.GetAlignment() == AlignmentEnum.freePeople;
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
