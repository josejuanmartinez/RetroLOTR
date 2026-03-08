using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TheDrownOfTheStone : EventAction
{
    private static bool IsPalantir(Artifact artifact)
    {
        return artifact != null
            && !string.IsNullOrWhiteSpace(artifact.artifactName)
            && artifact.artifactName.IndexOf("Palantir", StringComparison.OrdinalIgnoreCase) >= 0;
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

            List<Hex> waterHexes = board.GetHexes()
                .Where(h => h != null && h.IsWaterTerrain())
                .ToList();
            if (waterHexes.Count == 0) return false;

            List<Character> freePeopleHolders = board.GetHexes()
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null
                    && !ch.killed
                    && ch.GetAlignment() == AlignmentEnum.freePeople
                    && ch.artifacts != null
                    && ch.artifacts.Any(IsPalantir))
                .Distinct()
                .ToList();

            if (freePeopleHolders.Count == 0) return false;

            int movedPalantirs = 0;
            foreach (Character holder in freePeopleHolders)
            {
                List<Artifact> palantirs = holder.artifacts.Where(IsPalantir).ToList();
                if (palantirs.Count == 0) continue;

                foreach (Artifact palantir in palantirs)
                {
                    holder.artifacts.Remove(palantir);
                    palantir.hidden = true;

                    Hex targetHex = waterHexes[UnityEngine.Random.Range(0, waterHexes.Count)];
                    targetHex.hiddenArtifacts.Add(palantir);
                    targetHex.UpdateArtifactVisibility();
                    Character.RefreshArtifactPcVisibilityForHex(targetHex);
                    movedPalantirs++;
                }

                Character.RefreshArtifactPcVisibilityForHex(holder.hex);
            }

            if (movedPalantirs == 0) return false;

            MessageDisplayNoUI.ShowMessage(character.hex, character, $"The Drown Of The Stone drags {movedPalantirs} Palantir artifact(s) from Free People hands into the sea.", Color.magenta);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null) return false;

            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;

            bool hasWaterHex = board.GetHexes().Any(h => h != null && h.IsWaterTerrain());
            if (!hasWaterHex) return false;

            return board.GetHexes()
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Any(ch => ch != null
                    && !ch.killed
                    && ch.GetAlignment() == AlignmentEnum.freePeople
                    && ch.artifacts != null
                    && ch.artifacts.Any(IsPalantir));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
