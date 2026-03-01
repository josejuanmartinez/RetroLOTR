using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Telepathy : Spell
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (c) =>
        {
            if (originalEffect != null && !originalEffect(c)) return false;
            if (c == null || c.GetOwner() == null) return false;

            Leader owner = c.GetOwner();
            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;

            List<PC> alliedNationPcs = board.GetHexes()
                .Select(h => h?.GetPC())
                .Where(pc =>
                    pc != null &&
                    pc.owner != null &&
                    pc.owner != owner &&
                    pc.owner.GetAlignment() == owner.GetAlignment() &&
                    owner.GetAlignment() != AlignmentEnum.neutral)
                .Distinct()
                .ToList();

            if (alliedNationPcs.Count == 0) return false;

            int baseCount = Math.Max(1, c.GetMage());
            int revealCount = Mathf.Clamp(ApplySpellEffectMultiplier(c, baseCount), 1, alliedNationPcs.Count);
            List<PC> chosen = alliedNationPcs.OrderBy(_ => UnityEngine.Random.value).Take(revealCount).ToList();
            if (chosen.Count == 0) return false;

            for (int i = 0; i < chosen.Count; i++)
            {
                PC pc = chosen[i];
                if (pc?.hex == null) continue;
                pc.Reveal();
                pc.hex.RevealArea(1, false, owner);
                pc.hex.RefreshVisibilityRendering();
            }

            chosen[0].hex?.LookAt();
            MessageDisplayNoUI.ShowMessage(chosen[0].hex, c, $"Telepathy reveals {chosen.Count} allied nation PC area(s).", Color.cyan);
            return true;
        };

        condition = (c) =>
        {
            if (originalCondition != null && !originalCondition(c)) return false;
            if (c == null || c.GetOwner() == null) return false;

            Leader owner = c.GetOwner();
            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;

            return board.GetHexes().Any(h =>
            {
                PC pc = h?.GetPC();
                return pc != null &&
                       pc.owner != null &&
                       pc.owner != owner &&
                       pc.owner.GetAlignment() == owner.GetAlignment() &&
                       owner.GetAlignment() != AlignmentEnum.neutral;
            });
        };

        asyncEffect = async (c) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
