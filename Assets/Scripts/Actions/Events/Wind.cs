using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Decks: FP (gandalf_base), dark (sauron_base), neutral (saruman_base)
// 4-part FP:    own flyers +4 move + own plains chars +1 move | own cavalry slowed 1 | enemy flyers -2 move | FP armies +5% atk
// 4-part dark:  own flyers +4 move + own plains chars +1 move | own cavalry slowed 1 | enemy flyers -2 move | DS armies +5% atk
// neutral rule: all flyers +4 move | all cavalry slowed 1
public class Wind : EventAction
{
    private static bool IsFlying(Character ch) =>
        ch.race == RacesEnum.Eagle || ch.race == RacesEnum.Nazgul || ch.race == RacesEnum.Dragon
        || (ch.GetArmy() != null && ch.GetArmy().GetAbilityTroopCount(ArmySpecialAbilityEnum.Flying) > 0);

    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        AlignmentEnum caster = GetCasterAlignment();
        EnvironmentalCardManager env = EnvironmentalCardManager.Instance;

        List<Character> allChars = board.GetHexes()
            .Where(h => h != null && h.characters != null)
            .SelectMany(h => h.characters)
            .Where(ch => ch != null && !ch.killed).Distinct().ToList();

        if (caster == AlignmentEnum.neutral)
        {
            int flyers = 0, cavalrySlowed = 0;
            foreach (var ch in allChars)
            {
                if (IsFlying(ch)) { ch.moved = Mathf.Max(0, ch.moved - 4); flyers++; }
                else if (ch.IsArmyCommander()) { Army a = ch.GetArmy(); if (a != null && (a.lc > 0 || a.hc > 0)) { ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement()); cavalrySlowed++; } }
            }
            MessageDisplayNoUI.ShowMessage(null, null, $"Wind (ongoing): {flyers} flyers +4 movement; {cavalrySlowed} cavalry slowed.", Color.cyan);
            return;
        }

        AlignmentEnum other = caster == AlignmentEnum.freePeople ? AlignmentEnum.darkServants : AlignmentEnum.freePeople;
        if (env != null)
        {
            if (caster == AlignmentEnum.freePeople) env.FreePeopleArmyAttackFactor = 1.05f;
            else env.DarkServantsArmyAttackFactor = 1.05f;
        }

        int ownFlyers = 0, ownPlainsBoost = 0, ownCavSlowed = 0, enemyFlyersSlowed = 0;
        foreach (var ch in allChars)
        {
            bool isOwn = ch.GetAlignment() == caster;
            bool isOther = ch.GetAlignment() == other;

            if (isOwn)
            {
                if (IsFlying(ch)) { ch.moved = Mathf.Max(0, ch.moved - 4); ownFlyers++; }           // big bonus own: flyers
                else if (ch.hex != null && ch.hex.terrainType == TerrainEnum.plains)
                { ch.moved = Mathf.Max(0, ch.moved - 1); ownPlainsBoost++; }                         // big bonus own: plains riders
                Army a = ch.IsArmyCommander() ? ch.GetArmy() : null;
                if (a != null && (a.lc > 0 || a.hc > 0)) { ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement()); ownCavSlowed++; } // small debuff own
            }
            else if (isOther && IsFlying(ch))
            { ch.moved = Mathf.Min(ch.moved + 2, ch.GetMaxMovement()); enemyFlyersSlowed++; }         // big debuff other: enemy flyers penalised
        }
        MessageDisplayNoUI.ShowMessage(null, null,
            $"Wind (ongoing): {ownFlyers} own flyers soar; {ownPlainsBoost} plains riders advance; {enemyFlyersSlowed} enemy flyers hindered; {ownCavSlowed} own cavalry slowed.",
            Color.cyan);
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;
            var alliedFlyers = board.GetHexes().Where(h => h != null && h.characters != null).SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && IsFlying(ch) && ch.GetAlignment() == character.GetAlignment()).Distinct().ToList();
            foreach (var ch in alliedFlyers) ch.moved = Mathf.Max(0, ch.moved - 4);
            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Wind: {alliedFlyers.Count} allied flying creature(s) gain +4 movement.", Color.cyan);
            return alliedFlyers.Count > 0;
        };
        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            Board board = FindFirstObjectByType<Board>();
            return board != null && board.GetHexes().Any(h => h != null && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed && IsFlying(ch) && ch.GetAlignment() == character?.GetAlignment()));
        };
        asyncEffect = async (character) => { if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false; return true; };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
