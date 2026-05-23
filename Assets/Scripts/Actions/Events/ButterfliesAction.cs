using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ButterfliesAction : EventAction
{
    private const int Radius = 3;

    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        // Reveal all forest and swamp hexes globally
        foreach (Hex hex in board.GetHexes().Where(h => h != null
            && (h.terrainType == TerrainEnum.forest || h.terrainType == TerrainEnum.swamp)))
        {
            hex.RevealArea(0, false);
        }

        // Allied characters in forest gain ArcaneInsight (nature attunement)
        List<Character> forestAllies = board.GetHexes()
            .Where(h => h != null && h.terrainType == TerrainEnum.forest && h.characters != null)
            .SelectMany(h => h.characters)
            .Where(ch => ch != null && !ch.killed && ch.GetAlignment() == AlignmentEnum.freePeople)
            .Distinct().ToList();

        // Hidden enemies in forest revealed
        List<Character> hiddenEnemies = board.GetHexes()
            .Where(h => h != null && h.terrainType == TerrainEnum.forest && h.characters != null)
            .SelectMany(h => h.characters)
            .Where(ch => ch != null && !ch.killed && ch.GetAlignment() == AlignmentEnum.darkServants
                && ch.HasStatusEffect(StatusEffectEnum.Hidden))
            .Distinct().ToList();

        foreach (Character ch in forestAllies) ch.ApplyStatusEffect(StatusEffectEnum.ArcaneInsight, 1);
        foreach (Character ch in hiddenEnemies) ch.ClearStatusEffect(StatusEffectEnum.Hidden);

        MessageDisplayNoUI.ShowMessage(null, null,
            $"Butterflies (ongoing): forest/swamp revealed; {forestAllies.Count} allied forest units gain insight; {hiddenEnemies.Count} hidden enemies exposed.",
            Color.green);
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

            Leader owner = character.GetOwner();
            if (owner == null) return false;

            List<Hex> terrainHexes = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && (h.terrainType == TerrainEnum.forest || h.terrainType == TerrainEnum.swamp))
                .ToList();

            if (terrainHexes.Count == 0) return false;

            owner.AddTemporarySeenHexes(terrainHexes);
            if (owner == FindFirstObjectByType<Game>()?.player)
            {
                owner.RefreshVisibleHexesImmediate();
            }

            for (int i = 0; i < terrainHexes.Count; i++)
            {
                terrainHexes[i]?.RefreshVisibilityRendering();
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"Butterflies: forest and swamp hexes in radius {Radius} are seen for 1 turn.",
                new Color(0.76f, 0.74f, 0.5f));

            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            return character.hex.GetHexesInRadius(Radius)
                .Any(h => h != null && (h.terrainType == TerrainEnum.forest || h.terrainType == TerrainEnum.swamp));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
