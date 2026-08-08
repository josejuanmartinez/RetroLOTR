using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ShadeOfTheBarrowKing : EventAction
{
    private const int Radius = 1;
    private const int FearTurns = 1;
    private const int DespairTurns = 1;

    private static bool IsEnemy(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return false;
        return target.GetAlignment() != source.GetAlignment() || source.GetAlignment() == AlignmentEnum.neutral;
    }

    private static List<Character> GetCandidates(Character character)
    {
        if (character == null || character.hex == null) return new List<Character>();
        return character.hex.GetHexesInRadius(Radius)
            .Where(h => h != null && h.characters != null)
            .SelectMany(h => h.characters)
            .Where(ch => ch != null && !ch.killed && IsEnemy(character, ch)
                && ch.objects != null && ch.objects.Any(o => o != null && o.transferable))
            .Distinct()
            .ToList();
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null || character.objects.Count >= Character.MAX_OBJECTS) return false;

            List<Character> candidates = GetCandidates(character);
            if (candidates.Count == 0) return false;

            Character target = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            List<CardData> stealable = target.objects.Where(o => o != null && o.transferable).ToList();
            CardData stolen = stealable[UnityEngine.Random.Range(0, stealable.Count)];
            if (!target.objects.Remove(stolen)) return false;

            character.objects.Add(stolen);
            Character.RefreshArtifactPcVisibilityForHex(character.hex);
            Character.RefreshArtifactPcVisibilityForHex(target.hex);

            target.ApplyStatusEffect(StatusEffectEnum.Fear, FearTurns);
            target.ApplyStatusEffect(StatusEffectEnum.Despair, DespairTurns);

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"Shade of the Barrow-king: stole {stolen.name} from {target.characterName}, who gains Fear and Despair.",
                Color.gray);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.objects.Count >= Character.MAX_OBJECTS) return false;
            return GetCandidates(character).Count > 0;
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
