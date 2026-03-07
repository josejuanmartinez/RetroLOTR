using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RageOfUlmo : EventAction
{
    private const int Damage = 18;

    private static bool IsSeaAdjacentHex(Hex hex)
    {
        return hex != null && (hex.terrainType == TerrainEnum.shore || hex.IsWaterTerrain());
    }

    private static bool IsNavyOrEmbarked(Character ch)
    {
        if (ch == null || ch.killed) return false;
        if (ch.isEmbarked) return true;

        if (ch.IsArmyCommander())
        {
            Army army = ch.GetArmy();
            if (army != null && army.ws > 0) return true;
        }

        return false;
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

            List<Character> targets = board.GetHexes()
                .Where(h => h != null && IsSeaAdjacentHex(h) && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => IsNavyOrEmbarked(ch) && !ch.IsImmuneToNegativeEnvironmentalCards())
                .Distinct()
                .ToList();

            if (targets.Count == 0) return false;

            int affected = 0;
            int burningCleared = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                Character target = targets[i];
                if (target.HasStatusEffect(StatusEffectEnum.Burning))
                {
                    target.ClearStatusEffect(StatusEffectEnum.Burning);
                    burningCleared++;
                }
                target.Wounded(character.GetOwner(), Damage);
                if (!target.killed)
                {
                    target.Halt(1);
                }
                affected++;
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Rage of Ulmo batters {affected} naval or embarked unit(s): {Damage} damage, Halted (1), and Burning removed from {burningCleared}.", Color.cyan);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;

            return board.GetHexes().Any(h => h != null && IsSeaAdjacentHex(h) && h.characters != null && h.characters.Any(ch => IsNavyOrEmbarked(ch) && !ch.IsImmuneToNegativeEnvironmentalCards()));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
