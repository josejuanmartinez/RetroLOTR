using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RevealHexes : Spell
{
    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (c) =>
        {
            if (originalEffect != null && !originalEffect(c)) return false;
            if (c == null) return false;

            Leader owner = c.GetOwner();
            Board board = FindFirstObjectByType<Board>();
            if (owner == null || board == null) return false;

            List<Hex> eligibleHexes = board.GetHexes()
                .Where(hex => hex != null && !hex.IsScoutedBy(owner))
                .ToList();
            if (eligibleHexes.Count == 0) return false;

            int baseCount = Math.Max(1, c.GetMage());
            int revealCount = Mathf.Clamp(ApplySpellEffectMultiplier(c, baseCount), 1, 7);

            List<Hex> chosen = eligibleHexes
                .OrderBy(_ => UnityEngine.Random.value)
                .Take(revealCount)
                .ToList();
            if (chosen.Count == 0) return false;

            owner.AddTemporarySeenHexes(chosen);
            for (int i = 0; i < chosen.Count; i++)
            {
                Hex hex = chosen[i];
                if (hex == null) continue;
                hex.Reveal(owner);
                hex.RefreshVisibilityRendering();
            }

            chosen[0].LookAt();
            MessageDisplayNoUI.ShowMessage(chosen[0], c, $"Revealed {chosen.Count} hex(es)", Color.green);
            return true;
        };

        condition = (c) =>
        {
            if (originalCondition != null && !originalCondition(c)) return false;
            if (c == null) return false;
            if (c.GetOwner() != FindFirstObjectByType<Game>().player) return false;

            Leader owner = c.GetOwner();
            Board board = FindFirstObjectByType<Board>();
            if (owner == null || board == null) return false;
            return board.GetHexes().Any(hex => hex != null && !hex.IsScoutedBy(owner));
        };

        asyncEffect = async (c) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
