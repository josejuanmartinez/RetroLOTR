using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RestlessEast : EventAction
{
    private static bool IsEasterlingOrSouthron(Character ch) =>
        ch != null && (ch.race == RacesEnum.Easterling || ch.race == RacesEnum.Southron);

    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        int fortified = 0, supremacy = 0;
        foreach (Hex hex in board.GetHexes().Where(h => h != null && h.characters != null))
        {
            bool isOpenTerrain = hex.terrainType == TerrainEnum.plains
                || hex.terrainType == TerrainEnum.grasslands
                || hex.terrainType == TerrainEnum.wastelands
                || hex.terrainType == TerrainEnum.desert;
            if (!isOpenTerrain) continue;
            foreach (Character ch in hex.characters.Where(ch => ch != null && !ch.killed && IsEasterlingOrSouthron(ch)).ToList())
            {
                ch.ApplyStatusEffect(StatusEffectEnum.Fortified, 1);
                fortified++;
                if (ch.GetCommander() > 0)
                {
                    ch.GainDuelSupremacy(1);
                    supremacy++;
                }
            }
        }
        MessageDisplayNoUI.ShowMessage(null, null,
            $"Restless East (ongoing): {fortified} Easterling/Southron units on open ground fortified; {supremacy} commanders gain duel supremacy.",
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
            if (character == null) return false;
            character.ApplyStatusEffect(StatusEffectEnum.Fortified, 1);
            character.ApplyStatusEffect(StatusEffectEnum.Haste, 1);
            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"{character.characterName} stands firm under the Restless East: Fortified and Haste (1 turn).",
                Color.yellow);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            return character != null && IsEasterlingOrSouthron(character);
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
