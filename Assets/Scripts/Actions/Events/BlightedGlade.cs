using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BlightedGlade : EventAction
{
    private const int Radius = 2;
    private const int BleedTurns = 2;
    private const int SpiderHideTurns = 2;
    private const int SpiderHasteTurns = 1;

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

            List<Hex> forestHexes = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.terrainType == TerrainEnum.forest)
                .ToList();

            int enemiesRevealed = 0;
            int enemiesBled = 0;
            int spidersBuffed = 0;

            foreach (Hex h in forestHexes)
            {
                if (h.characters == null) continue;
                foreach (Character target in h.characters)
                {
                    if (target == null || target.killed) continue;
                    if (IsAllied(character, target))
                    {
                        if (target.race == RacesEnum.Spider)
                        {
                            target.Hide(SpiderHideTurns);
                            target.ApplyStatusEffect(StatusEffectEnum.Haste, SpiderHasteTurns);
                            spidersBuffed++;
                        }
                    }
                    else
                    {
                        if (target.IsHidden())
                        {
                            target.ClearStatusEffect(StatusEffectEnum.Hidden);
                            enemiesRevealed++;
                        }
                        target.ApplyStatusEffect(StatusEffectEnum.Bleeding, BleedTurns);
                        enemiesBled++;
                    }
                }
            }

            if (enemiesBled == 0 && spidersBuffed == 0) return false;

            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Blighted Glade bleeds {enemiesBled} enemy unit(s) in the forest (revealing {enemiesRevealed}) and empowers {spidersBuffed} allied Spider(s).", Color.red);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            return character.hex.GetHexesInRadius(Radius)
                .Any(h => h != null && h.terrainType == TerrainEnum.forest && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
