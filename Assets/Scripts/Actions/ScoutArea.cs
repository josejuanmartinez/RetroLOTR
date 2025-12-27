using System;
using UnityEngine;

public class ScoutArea : AgentAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (c) => {
            if (originalEffect != null && !originalEffect(c)) return false;
            c.hex.RevealArea(1, true, c.GetOwner());
            var radiusHexes = c.hex.GetHexesInRadius(1);
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
                }
            }
            MessageDisplayNoUI.ShowMessage(c.hex, c, $"Area scouted", Color.green);
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

