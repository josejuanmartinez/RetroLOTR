using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class DwarUnleashed : EventAction
{
    private static bool IsCavalry(Character ch)
    {
        if (ch == null || !ch.IsArmyCommander()) return false;
        Army a = ch.GetArmy();
        return a != null && (a.lc > 0 || a.hc > 0);
    }

    private static bool IsBeastRace(RacesEnum race) =>
        race == RacesEnum.Troll || race == RacesEnum.Goblin || race == RacesEnum.Spider
        || race == RacesEnum.Dragon || race == RacesEnum.Undead || race == RacesEnum.Beast;

    private static bool IsDarkServantBeast(Character ch) =>
        ch != null && ch.GetAlignment() == AlignmentEnum.darkServants && IsBeastRace(ch.race);

    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        int cavalryBoosted = 0;
        int beastsStrengthened = 0;

        foreach (Hex hex in board.GetHexes().Where(h => h != null && h.characters != null))
        {
            foreach (Character ch in hex.characters.Where(ch => ch != null && !ch.killed).ToList())
            {
                if (IsCavalry(ch))
                {
                    ch.moved = Mathf.Max(0, ch.moved - 2);
                    cavalryBoosted++;
                }

                if (IsDarkServantBeast(ch))
                {
                    ch.ApplyStatusEffect(StatusEffectEnum.Strengthened, 1);
                    beastsStrengthened++;
                }
            }
        }

        MessageDisplayNoUI.ShowMessage(null, null,
            $"Dwar Unleashed (ongoing): {cavalryBoosted} cavalry unit(s) gain +2 movement; {beastsStrengthened} dark servant beast(s) strengthened.",
            Color.red);
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
