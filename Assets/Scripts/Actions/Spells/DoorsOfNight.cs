using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class DoorsOfNight : DarkNeutralSpell
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
            Game game = FindFirstObjectByType<Game>();
            Board board = FindFirstObjectByType<Board>();
            bool applyGlobalEffects = owner != null && game != null && owner == game.player;
            if (board == null) return false;

            List<Hex> allHexes = board.GetHexes().Where(h => h != null).ToList();
            if (allHexes.Count == 0) return false;

            int affectedCount = Mathf.Max(1, Mathf.RoundToInt(allHexes.Count * 0.5f));
            List<Hex> affectedHexes = allHexes
                .OrderBy(_ => UnityEngine.Random.value)
                .Take(affectedCount)
                .ToList();

            if (applyGlobalEffects)
            {
                for (int i = 0; i < affectedHexes.Count; i++)
                {
                    Hex targetHex = affectedHexes[i];
                    if (targetHex == null) continue;

                    targetHex.MarkDarknessByPlayer();
                    targetHex.ClearScoutingAll();

                    PC pc = targetHex.GetPC();
                    AlignmentEnum ownerAlignment = owner != null ? owner.GetAlignment() : AlignmentEnum.neutral;
                    AlignmentEnum pcAlignment = pc != null && pc.owner != null ? pc.owner.GetAlignment() : AlignmentEnum.neutral;
                    bool sameAlignment = pcAlignment == ownerAlignment && pcAlignment != AlignmentEnum.neutral;
                    if (pc != null && (pc.owner == owner || sameAlignment))
                    {
                        pc.SetTemporaryHidden(2);
                        targetHex.RedrawPC();
                    }
                }

                for (int i = 0; i < affectedHexes.Count; i++)
                {
                    Hex targetHex = affectedHexes[i];
                    if (targetHex == null) continue;
                    targetHex.RefreshVisibilityRendering();
                }

                MessageDisplayNoUI.ShowMessage(c.hex, c, $"Doors of Night shroud {affectedHexes.Count} hexes!", Color.red);
            }

            return true;
        };

        condition = (c) =>
        {
            if (originalCondition != null && !originalCondition(c)) return false;
            return true;
        };

        asyncEffect = async (c) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
