using System;
using System.Collections.Generic;
using UnityEngine;

public class Twilight : EventAction
{
    private const int Radius = 2;

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
            Game game = FindFirstObjectByType<Game>();
            if (owner == null || game == null || owner != game.player) return false;

            List<Hex> radiusHexes = character.hex.GetHexesInRadius(Radius);
            int hiddenPcs = 0;

            for (int i = 0; i < radiusHexes.Count; i++)
            {
                Hex targetHex = radiusHexes[i];
                if (targetHex == null) continue;

                targetHex.MarkDarknessByPlayer();
                targetHex.ClearScoutingAll();

                PC pc = targetHex.GetPCData();
                if (pc == null || pc.owner == null) continue;

                AlignmentEnum ownerAlignment = owner.GetAlignment();
                AlignmentEnum pcAlignment = pc.owner.GetAlignment();
                bool sameAlignment = pcAlignment == ownerAlignment && pcAlignment != AlignmentEnum.neutral;
                if (pc.owner == owner || sameAlignment)
                {
                    pc.SetTemporaryHidden(2);
                    targetHex.RedrawPC();
                    hiddenPcs++;
                }
            }

            character.hex.ObscureArea(Radius, true, owner);
            for (int i = 0; i < radiusHexes.Count; i++)
            {
                radiusHexes[i]?.RefreshVisibilityRendering();
            }

            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"Twilight obscures the area in radius {Radius} and hides {hiddenPcs} allied PC(s).",
                Color.magenta);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            Leader owner = character.GetOwner();
            Game game = FindFirstObjectByType<Game>();
            return owner != null && game != null && owner == game.player;
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
