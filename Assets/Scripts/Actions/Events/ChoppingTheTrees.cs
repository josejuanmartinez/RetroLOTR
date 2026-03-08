using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ChoppingTheTrees : EventAction
{
    private const int TimberGain = 3;
    private const int ModerateWound = 15;

    private static bool IsTreeAffectedRace(RacesEnum race)
    {
        return race == RacesEnum.Ent
            || race == RacesEnum.Troll
            || race == RacesEnum.Goblin
            || race == RacesEnum.Spider
            || race == RacesEnum.Dragon;
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null || character.hex == null || character.hex.terrainType != TerrainEnum.forest) return false;

            Leader owner = character.GetOwner();
            if (owner == null) return false;

            owner.AddTimber(TimberGain);

            List<Character> targets = character.hex.characters
                .Where(ch => ch != null && !ch.killed && IsTreeAffectedRace(ch.race))
                .Distinct()
                .ToList();

            for (int i = 0; i < targets.Count; i++)
            {
                targets[i].Wounded(character.GetOwner(), ModerateWound);
            }

            string woundText = targets.Count > 0
                ? $" {targets.Count} beast/Ent unit(s) here take {ModerateWound} damage."
                : string.Empty;
            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Chopping the Trees grants +{TimberGain} timber.{woundText}", Color.yellow);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            return character != null && character.hex != null && character.hex.terrainType == TerrainEnum.forest && character.GetOwner() != null;
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
