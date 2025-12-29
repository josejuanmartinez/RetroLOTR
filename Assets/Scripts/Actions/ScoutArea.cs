using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using System.Linq;

public class ScoutArea : AgentAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (c) => {
            if (originalEffect != null && !originalEffect(c)) return false;
            var radiusHexes = c.hex.GetHexesInRadius(1);
            Leader owner = c.GetOwner();
            List<Hex> detectedHexes = new();
            if (owner != null)
            {
                for (int i = 0; i < radiusHexes.Count; i++)
                {
                    Hex hex = radiusHexes[i];
                    if (hex == null) continue;
                    if (!hex.IsScoutedBy(owner) && hex.characters != null && hex.characters.Find(x => x != null && !x.killed && x.GetAlignment() != c.GetAlignment()) != null)
                    {
                        detectedHexes.Add(hex);
                    }
                }
            }

            c.hex.RevealArea(1, true, owner);
            c.GetOwner()?.AddTemporarySeenHexes(radiusHexes);
            bool hasArtifactsNearby = false;
            for (int i = 0; i < radiusHexes.Count; i++)
            {
                Hex hex = radiusHexes[i];
                if (hex != null && hex.hiddenArtifacts != null && hex.hiddenArtifacts.Count > 0)
                {
                    hasArtifactsNearby = true;
                    break;
                }
            }
            if (hasArtifactsNearby)
            {
                float chance = Mathf.Min(0.4f, 0.05f * c.GetAgent());
                if (UnityEngine.Random.value < chance)
                {
                    MessageDisplayNoUI.ShowMessage(c.hex, c, "Something seems buried in the area", Color.yellow);
                    for (int i = 0; i < radiusHexes.Count; i++)
                    {
                        Hex hex = radiusHexes[i];
                        if (hex != null && hex.hiddenArtifacts != null && hex.hiddenArtifacts.Count > 0)
                        {
                            hex.RevealArtifact();
                        }
                    }
                }
            }
            for (int i = 0; i < radiusHexes.Count; i++)
            {
                Hex hex = radiusHexes[i];
                if (hex == null) continue;
                hex.RefreshVisibilityRendering();
            }
            bool revealedCharacters = detectedHexes.Count > 0;
            string scoutMessage = revealedCharacters ? "Presence detected in the surroundings" : "Area scouted";
            MessageDisplayNoUI.ShowMessage(c.hex, c, scoutMessage, Color.green);
            if (revealedCharacters)
            {
                for (int i = 0; i < detectedHexes.Count; i++)
                {
                    Hex hex = detectedHexes[i];
                    if (hex == null || hex.characters == null) continue;
                    var names = hex.characters
                        .Where(ch => ch != null && !ch.killed && ch.GetAlignment() != c.GetAlignment())
                        .Select(ch => ch.characterName)
                        .Distinct()
                        .ToList();
                    if (names.Count > 0)
                    {
                        MessageDisplayNoUI.ShowMessage(hex, c, string.Join(", ", names), Color.yellow);
                    }
                }
            }
            return true;
        };
        condition = (c) => {
            return originalCondition == null || originalCondition(c);
        };
        asyncEffect = async (c) => {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
