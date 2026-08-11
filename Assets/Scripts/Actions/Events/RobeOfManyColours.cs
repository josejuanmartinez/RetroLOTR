using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RobeOfManyColours : EventAction
{
    private const int HasteTurns = 1;
    private const int HiddenTurns = 2;

    private static readonly StatusEffectEnum[] PositiveEffects =
    {
        StatusEffectEnum.Hope,
        StatusEffectEnum.Encouraged,
        StatusEffectEnum.Haste,
        StatusEffectEnum.ArcaneInsight,
        StatusEffectEnum.Strengthened,
        StatusEffectEnum.Fortified,
        StatusEffectEnum.DuelSupremacy,
    };

    private static bool IsEnemy(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return false;
        return target.GetAlignment() != source.GetAlignment() || source.GetAlignment() == AlignmentEnum.neutral;
    }

    // Nearest enemy character on the board — the card gives no radius, so target
    // whoever the caster could actually reach, same "closest enemy" tie-break
    // UtilityAIContextDataBuilder.CacheEnemyTargets uses.
    private static Character FindNearestEnemy(Character character, Board board)
    {
        if (character?.hex == null || board?.hexes == null) return null;
        Character best = null;
        float bestDistance = float.MaxValue;
        foreach (Hex hex in board.hexes.Values)
        {
            if (hex?.characters == null) continue;
            foreach (Character candidate in hex.characters.Where(ch => ch != null && !ch.killed && IsEnemy(character, ch)))
            {
                float distance = Vector2.Distance(character.hex.v2, hex.v2);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }
        }
        return best;
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null) return false;

            Board board = Board.Instance;
            Character target = FindNearestEnemy(character, board);
            if (target?.hex?.characters == null) return false;

            int stripped = 0;
            foreach (Character victim in target.hex.characters.Where(ch => ch != null && !ch.killed && IsEnemy(character, ch)).ToList())
            {
                foreach (StatusEffectEnum buff in PositiveEffects)
                {
                    if (!victim.HasStatusEffect(buff)) continue;
                    victim.ClearStatusEffect(buff);
                    stripped++;
                }
            }

            character.ApplyStatusEffect(StatusEffectEnum.Haste, HasteTurns);
            character.ApplyStatusEffect(StatusEffectEnum.Hidden, HiddenTurns);

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"Robe of Many Colours: {stripped} buff(s) stripped from {target.characterName}'s hex; {character.characterName} gains Haste and Hidden.",
                Color.magenta);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null) return false;
            Board board = Board.Instance;
            return FindNearestEnemy(character, board) != null;
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
