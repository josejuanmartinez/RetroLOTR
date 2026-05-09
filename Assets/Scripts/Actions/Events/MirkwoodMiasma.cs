using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MirkwoodMiasma : EventAction
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

    private static void RemoveWeakestTroop(Army army)
    {
        if (army == null) return;
        int min = int.MaxValue;
        int slot = -1;
        if (army.ma > 0 && army.ma < min) { min = army.ma; slot = 0; }
        if (army.li > 0 && army.li < min) { min = army.li; slot = 1; }
        if (army.hi > 0 && army.hi < min) { min = army.hi; slot = 2; }
        if (army.lc > 0 && army.lc < min) { min = army.lc; slot = 3; }
        if (army.hc > 0 && army.hc < min) { slot = 4; }

        switch (slot)
        {
            case 0: army.ma = Math.Max(0, army.ma - 1); break;
            case 1: army.li = Math.Max(0, army.li - 1); break;
            case 2: army.hi = Math.Max(0, army.hi - 1); break;
            case 3: army.lc = Math.Max(0, army.lc - 1); break;
            case 4: army.hc = Math.Max(0, army.hc - 1); break;
        }
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

            List<Hex> forestHexes = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.terrainType == TerrainEnum.forest)
                .ToList();

            int enemiesStopped = 0;
            int spidersEmpowered = 0;

            foreach (Hex h in forestHexes)
            {
                if (h.characters == null) continue;
                foreach (Character target in h.characters.ToList())
                {
                    if (target == null || target.killed) continue;

                    if (!IsAllied(character, target))
                    {
                        target.Halt(1);
                        if (target.IsArmyCommander() && target.GetArmy() != null)
                            RemoveWeakestTroop(target.GetArmy());
                        enemiesStopped++;
                    }
                    else if (target.race == RacesEnum.Spider)
                    {
                        target.ApplyStatusEffect(StatusEffectEnum.Haste, 1);
                        target.Hide(1);
                        spidersEmpowered++;
                    }
                }
            }

            if (enemiesStopped == 0 && spidersEmpowered == 0) return false;

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"Mirkwood Miasma: {enemiesStopped} enemy unit(s) halted and lose a troop; {spidersEmpowered} allied Spider(s) gain Haste and Hidden.",
                Color.green);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            return character.hex.GetHexesInRadius(Radius)
                .Any(h => h != null && h.terrainType == TerrainEnum.forest && h.characters != null
                    && h.characters.Any(ch => ch != null && !ch.killed));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
