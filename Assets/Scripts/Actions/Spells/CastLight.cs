using System;
using System.Collections.Generic;
using UnityEngine;

public class CastLight: FreeNeutralSpell
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (c) => {
            if (originalEffect != null && !originalEffect(c)) return false;
            Hex hex = c.hex;
            int radius = 1;
            List<Hex> radiusHexes = hex.GetHexesInRadius(radius);
            Leader owner = c.GetOwner();
            Game game = FindFirstObjectByType<Game>();
            bool applyGlobalEffects = owner != null && game != null && owner == game.player;
            bool revealedCharacters = false;
            if (owner != null)
            {
                for (int i = 0; i < radiusHexes.Count; i++)
                {
                    Hex areaHex = radiusHexes[i];
                    if (areaHex == null) continue;
                    if (!areaHex.IsScoutedBy(owner) && areaHex.characters != null && areaHex.characters.Find(x => x != null && !x.killed && x.GetAlignment() != c.GetAlignment()) != null)
                    {
                        revealedCharacters = true;
                        break;
                    }
                }
            }

            hex.RevealArea(radius, true, owner);
            c.GetOwner()?.AddTemporarySeenHexes(radiusHexes);
            c.GetOwner()?.AddTemporaryScoutCenters(new[] { c.hex });
            if (applyGlobalEffects)
            {
                for (int i = 0; i < radiusHexes.Count; i++)
                {
                    Hex areaHex = radiusHexes[i];
                    if (areaHex == null) continue;
                    PC pc = areaHex.GetPC();
                    if (pc == null) continue;
                    if (pc.owner == owner) continue;
                    AlignmentEnum ownerAlignment = owner != null ? owner.GetAlignment() : AlignmentEnum.neutral;
                    AlignmentEnum pcAlignment = pc.owner != null ? pc.owner.GetAlignment() : AlignmentEnum.neutral;
                    bool isEnemy = pcAlignment == AlignmentEnum.neutral || pcAlignment != ownerAlignment;
                    if (!isEnemy) continue;
                    if (pc.isHidden || pc.IsTemporarilyHidden(owner))
                    {
                        pc.SetTemporaryReveal(2);
                        areaHex.RedrawPC();
                    }
                }
            }
            for (int i = 0; i < radiusHexes.Count; i++)
            {
                Hex areaHex = radiusHexes[i];
                if (areaHex == null) continue;
                areaHex.RefreshVisibilityRendering();
            }

            if (applyGlobalEffects)
            {
                string scoutMessage = revealedCharacters ? "Presence detected in the surroundings" : "Area scouted";
                MessageDisplayNoUI.ShowMessage(c.hex, c, scoutMessage, Color.green);
            }
            return true;
        };
        condition = (c) => {
            if (originalCondition != null && !originalCondition(c)) return false;
            return true; 
        };
        asyncEffect = async (c) => {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}

