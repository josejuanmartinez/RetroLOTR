using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class LightningStorm : EventAction
{
    private static readonly HashSet<TerrainEnum> ExposedTerrain = new HashSet<TerrainEnum>
    {
        TerrainEnum.plains, TerrainEnum.wastelands, TerrainEnum.desert, TerrainEnum.shore
    };

    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        List<Character> exposed = board.GetHexes()
            .Where(h => h != null && ExposedTerrain.Contains(h.terrainType) && h.characters != null)
            .SelectMany(h => h.characters)
            .Where(ch => ch != null && !ch.killed)
            .Distinct().ToList();

        int wounded = 0, revealed = 0, cavalryLost = 0;

        foreach (Character ch in exposed)
        {
            if (ch.HasStatusEffect(StatusEffectEnum.Hidden))
            {
                ch.ClearStatusEffect(StatusEffectEnum.Hidden);
                revealed++;
            }

            if (UnityEngine.Random.value < 0.15f)
            {
                ch.Wounded(null, 15);
                wounded++;
            }

            if (ch.IsArmyCommander())
            {
                Army army = ch.GetArmy();
                if (army != null && (army.lc > 0 || army.hc > 0) && UnityEngine.Random.value < 0.15f)
                {
                    if (army.lc > 0) army.lc = Mathf.Max(0, army.lc - 1);
                    else army.hc = Mathf.Max(0, army.hc - 1);
                    cavalryLost++;
                }
            }
        }

        MessageDisplayNoUI.ShowMessage(null, null,
            $"Lightning Storm: {wounded} unit(s) struck for 15 HP; {revealed} hidden unit(s) revealed; {cavalryLost} cavalry lost to lightning.",
            Color.yellow);
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;

            List<Character> enemies = board.GetHexes()
                .Where(h => h != null && ExposedTerrain.Contains(h.terrainType) && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.GetAlignment() != character.GetAlignment())
                .Distinct().ToList();

            int struck = 0, revealed = 0;
            foreach (Character enemy in enemies)
            {
                enemy.Wounded(null, 15);
                struck++;
                if (enemy.HasStatusEffect(StatusEffectEnum.Hidden))
                {
                    enemy.ClearStatusEffect(StatusEffectEnum.Hidden);
                    revealed++;
                }
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"Lightning Storm: {struck} enemy unit(s) struck for 15 HP; {revealed} revealed.",
                Color.yellow);
            return struck > 0 || revealed > 0;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;
            return board.GetHexes().Any(h => h != null && ExposedTerrain.Contains(h.terrainType)
                && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed
                    && ch.GetAlignment() != character?.GetAlignment()));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
