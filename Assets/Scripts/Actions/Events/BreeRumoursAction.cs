using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BreeRumoursAction : EventAction
{
    private const int RevealCount = 5;

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

            List<Hex> eligibleHexes = board.GetHexes()
                .Where(hex => hex != null && !hex.IsHexRevealed())
                .ToList();
            if (eligibleHexes.Count == 0) return false;

            List<Hex> chosen = eligibleHexes
                .OrderBy(_ => UnityEngine.Random.value)
                .Take(Mathf.Min(RevealCount, eligibleHexes.Count))
                .ToList();

            if (chosen.Count == 0) return false;

            for (int i = 0; i < chosen.Count; i++)
            {
                Hex hex = chosen[i];
                if (hex == null) continue;
                hex.RevealMapOnlyArea(0, false, false);
            }

            if (character.hex != null && character.GetOwner() == FindFirstObjectByType<Game>()?.player)
            {
                MinimapManager.RefreshMinimap();
                chosen[0].LookAt();
            }

            MessageDisplayNoUI.ShowMessage(
                chosen[0],
                character,
                $"Bree Rumours: {chosen.Count} hidden hex(es) are marked in unseen detail.",
                new Color(0.86f, 0.74f, 0.45f));

            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null) return false;

            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;

            return board.GetHexes().Any(hex => hex != null && !hex.IsHexRevealed());
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
