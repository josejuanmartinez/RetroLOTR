using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MirkwoodMiasma : EventAction
{
    private const int Radius = 2;
    private const int PoisonTurns = 3;
    private const int HaltTurns = 1;

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

            int enemiesPoisoned = 0;

            foreach (Hex h in forestHexes)
            {
                if (h.characters == null) continue;
                foreach (Character target in h.characters)
                {
                    if (target == null || target.killed) continue;
                    if (!IsAllied(character, target))
                    {
                        target.ApplyStatusEffect(StatusEffectEnum.Poisoned, PoisonTurns);
                        target.Halt(HaltTurns);
                        enemiesPoisoned++;
                    }
                }
            }

            if (enemiesPoisoned == 0) return false;

            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Mirkwood Miasma poisons {enemiesPoisoned} enemy unit(s) in the tainted forest (radius {Radius}).", Color.green);
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
