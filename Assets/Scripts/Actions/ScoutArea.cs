using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using System.Linq;
using System.Threading.Tasks;

public class ScoutArea : AgentAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        List<(Hex hex, List<string> names)> revealedCharacterEntries = new();
        bool showPlayerResults = false;
        effect = (c) => {
            if (originalEffect != null && !originalEffect(c)) return false;
            var radiusHexes = c.hex.GetHexesInRadius(1);
            Leader owner = c.GetOwner();
            List<Hex> detectedHexes = new();
            Game game = FindFirstObjectByType<Game>();
            showPlayerResults = owner != null && game != null && owner == game.player;
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
            c.GetOwner()?.AddTemporaryScoutCenters(new[] { c.hex });
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
                    if (showPlayerResults)
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
            }
            for (int i = 0; i < radiusHexes.Count; i++)
            {
                Hex hex = radiusHexes[i];
                if (hex == null) continue;
                hex.RefreshVisibilityRendering();
            }
            bool revealedCharacters = detectedHexes.Count > 0;
            if (showPlayerResults)
            {
                string scoutMessage = revealedCharacters ? "Presence detected in the surroundings" : "Area scouted";
                MessageDisplayNoUI.ShowMessage(c.hex, c, scoutMessage, Color.green);
            }
            if (showPlayerResults && detectedHexes.Count > 0)
            {
                revealedCharacterEntries = detectedHexes
                    .Select(hex => (hex, names: hex.characters
                        .Where(ch => ch != null && !ch.killed && ch.GetAlignment() != c.GetAlignment())
                        .Select(ch => ch.characterName)
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .Distinct()
                        .ToList()))
                    .Where(entry => entry.names.Count > 0)
                    .ToList();
            }
            return true;
        };
        condition = (c) => {
            return originalCondition == null || originalCondition(c);
        };
        asyncEffect = async (c) => {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            if (!showPlayerResults || revealedCharacterEntries.Count == 0) return true;
            while (MessageDisplayNoUI.IsBusy())
            {
                await Task.Yield();
            }
            foreach (var entry in revealedCharacterEntries)
            {
                if (entry.hex == null) continue;
                if (BoardNavigator.Instance != null)
                {
                    var focusTcs = new TaskCompletionSource<bool>();
                    BoardNavigator.Instance.EnqueueFocus(entry.hex, 0.6f, 0.2f, true, () => focusTcs.TrySetResult(true));
                    await focusTcs.Task;
                }
                foreach (string name in entry.names)
                {
                    MessageDisplayNoUI.ShowMessage(entry.hex, c, name, Color.yellow);
                    while (MessageDisplayNoUI.IsBusy())
                    {
                        await Task.Yield();
                    }
                }
            }
            return true;
        };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
