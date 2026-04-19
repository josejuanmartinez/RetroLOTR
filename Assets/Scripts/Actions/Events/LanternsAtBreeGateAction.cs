using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class LanternsAtBreeGateAction : EventAction
{
    private const int RevealRadius = 3;

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

            List<Hex> revealedArea = character.hex.GetHexesInRadius(RevealRadius);
            owner.AddTemporarySeenHexes(revealedArea);
            owner.AddTemporaryScoutCenters(new[] { character.hex });
            character.hex.RevealArea(RevealRadius, true, owner);

            for (int i = 0; i < revealedArea.Count; i++)
            {
                revealedArea[i]?.RefreshVisibilityRendering();
            }

            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"Lanterns at Bree Gate: the road is seen clearly in radius {RevealRadius} for 1 turn.",
                new Color(1f, 0.82f, 0.45f));

            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;
            return character.hex.GetHexesInRadius(RevealRadius)
                .Any(h => h != null);
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
