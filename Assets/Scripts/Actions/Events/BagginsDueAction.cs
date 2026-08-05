using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BagginsDueAction : EventAction
{
    private static bool IsEnemy(Character source, Character target)
    {
        if (source == null || target == null || target.killed) return false;
        return target.GetAlignment() != source.GetAlignment();
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

            var candidates = character.hex.characters
                .Where(ch => ch != null && !ch.killed && IsEnemy(character, ch))
                .Select(ch => new { character = ch, objects = ch.objects.Where(a => a != null && a.transferable).ToList() })
                .Where(x => x.objects.Count > 0)
                .ToList();

            if (candidates.Count == 0) return false;

            var target = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            CardData stolen = target.objects[UnityEngine.Random.Range(0, target.objects.Count)];
            if (!target.character.objects.Remove(stolen)) return false;

            character.objects.Add(stolen);
            Character.RefreshArtifactPcVisibilityForHex(character.hex);

            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"The Sack of Bag End: {character.characterName} ransacks the place and makes off with {stolen.name}!",
                new Color(0.84f, 0.72f, 0.42f));

            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;
            if (character.objects.Count >= Character.MAX_OBJECTS) return false;

            return character.hex.characters.Any(ch =>
                ch != null &&
                !ch.killed &&
                IsEnemy(character, ch) &&
                ch.objects.Any(a => a != null && a.transferable));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
