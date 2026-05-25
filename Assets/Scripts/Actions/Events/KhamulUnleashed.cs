using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class KhamulUnleashed : EventAction
{
    private static bool IsDarkServant(Character ch) =>
        ch != null && ch.GetAlignment() == AlignmentEnum.darkServants;


    private static bool HasLightTroops(Army army) =>
        army != null && (army.ma > 0 || army.ar > 0 || army.li > 0);

    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        int fortified = 0;

        foreach (Hex hex in board.GetHexes().Where(h => h != null && (h.terrainType == TerrainEnum.forest || h.terrainType == TerrainEnum.mountains) && h.characters != null))
        {
            foreach (Character ch in hex.characters.Where(ch => ch != null && !ch.killed && ch.IsArmyCommander() && IsDarkServant(ch)).ToList())
            {
                Army army = ch.GetArmy();
                if (!HasLightTroops(army)) continue;
                ch.ApplyStatusEffect(StatusEffectEnum.Fortified, 1);
                fortified++;
            }
        }

        MessageDisplayNoUI.ShowMessage(null, null,
            $"Khamul Unleashed (ongoing): {fortified} dark servant commander(s) with light troops in forests or mountains gain Fortified.",
            Color.red);
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
