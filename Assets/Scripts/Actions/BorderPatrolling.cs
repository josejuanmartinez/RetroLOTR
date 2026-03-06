using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BorderPatrolling : AgentCommanderAction
{
    private static int GetPatrolRadius(Character character)
    {
        if (character == null) return 1;
        return Mathf.Clamp(Mathf.Max(character.GetAgent(), character.GetCommander()), 1, 4);
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null || character.hex == null) return false;

            int radius = GetPatrolRadius(character);
            List<Hex> radiusHexes = character.hex.GetHexesInRadius(radius);
            Leader owner = character.GetOwner();

            List<Character> detectedEnemies = radiusHexes
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null
                    && !ch.killed
                    && ch.GetOwner() != owner
                    && (ch.GetAlignment() != character.GetAlignment() || character.GetAlignment() == AlignmentEnum.neutral)
                    && !ch.IsHidden())
                .Distinct()
                .ToList();

            character.hex.RevealArea(radius, true, owner);
            owner?.AddTemporarySeenHexes(radiusHexes);
            owner?.AddTemporaryScoutCenters(new[] { character.hex });

            for (int i = 0; i < radiusHexes.Count; i++)
            {
                Hex hex = radiusHexes[i];
                if (hex == null) continue;
                hex.RefreshVisibilityRendering();
            }

            Character enemyCommander = character.hex.characters
                .Where(ch => ch != null
                    && !ch.killed
                    && ch.IsArmyCommander()
                    && ch.GetOwner() != owner
                    && (ch.GetAlignment() != character.GetAlignment() || character.GetAlignment() == AlignmentEnum.neutral))
                .FirstOrDefault();

            bool haltedArmy = false;
            if (enemyCommander != null)
            {
                enemyCommander.Halt(1);
                haltedArmy = true;
            }

            string presenceText = detectedEnemies.Count > 0 ? $"detected {detectedEnemies.Count} enemy unit(s)" : "found no visible enemies";
            string haltText = haltedArmy ? " and halted an enemy army commander in the hex" : string.Empty;
            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"Border patrol (radius {radius}) {presenceText}{haltText}.",
                Color.green);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            return character != null && character.hex != null;
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
