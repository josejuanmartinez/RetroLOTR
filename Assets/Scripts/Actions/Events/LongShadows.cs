using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Decks: FP (gandalf_base), dark (sauron_base), neutral (saruman_base)
// 4-part FP:    own forest beasts: reset movement + Encouraged + Hidden | own open-terrain chars slowed 1 | enemy forest emissaries ArcaneInsight | enemy forest lose scouting + beasts fear
// 4-part dark:  own forest beasts: Haste + Hidden | own open-terrain chars slowed 1 | FP forest emissaries ArcaneInsight | FP forest chars feared + revealed
// neutral rule: all forest beasts Encouraged; all non-beast forest slowed 1
public class LongShadows : EventAction
{
    private static bool IsBeast(RacesEnum r) => r == RacesEnum.Troll || r == RacesEnum.Goblin || r == RacesEnum.Spider || r == RacesEnum.Dragon || r == RacesEnum.Undead || r == RacesEnum.Beast;

    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        AlignmentEnum caster = GetCasterAlignment();

        if (caster == AlignmentEnum.neutral)
        {
            int beastBuff = 0, nonBeastSlowed = 0;
            foreach (var hex in board.GetHexes().Where(h => h != null && h.terrainType == TerrainEnum.forest && h.characters != null))
                foreach (var ch in hex.characters.Where(ch => ch != null && !ch.killed).ToList())
                {
                    if (IsBeast(ch.race)) { ch.Encourage(1); beastBuff++; }
                    else { ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement()); nonBeastSlowed++; }
                }
            MessageDisplayNoUI.ShowMessage(null, null, $"Long Shadows (ongoing): {beastBuff} forest beasts encouraged; {nonBeastSlowed} non-beasts slowed.", Color.gray);
            return;
        }

        AlignmentEnum other = caster == AlignmentEnum.freePeople ? AlignmentEnum.darkServants : AlignmentEnum.freePeople;
        int ownBeastBuff = 0, ownOpenSlowed = 0, otherSmall = 0, otherDebuff = 0;

        foreach (var hex in board.GetHexes().Where(h => h != null && h.characters != null))
        {
            bool isForest = hex.terrainType == TerrainEnum.forest;
            bool isOpen = hex.terrainType == TerrainEnum.plains || hex.terrainType == TerrainEnum.grasslands;
            foreach (var ch in hex.characters.Where(ch => ch != null && !ch.killed).ToList())
            {
                if (ch.GetAlignment() == caster)
                {
                    if (isForest && IsBeast(ch.race))
                    {
                        ch.moved = 0;
                        if (caster == AlignmentEnum.freePeople) { ch.Encourage(1); ch.Hide(1); } else { ch.ApplyStatusEffect(StatusEffectEnum.Haste, 1); ch.Hide(1); }
                        ownBeastBuff++;                                                             // big bonus own
                    }
                    if (isOpen) { ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement()); ownOpenSlowed++; } // small debuff own
                }
                else if (ch.GetAlignment() == other)
                {
                    if (isForest && ch.GetEmmissary() > 0) { ch.ApplyStatusEffect(StatusEffectEnum.ArcaneInsight, 1); otherSmall++; } // small bonus other
                    if (isForest)
                    {
                        if (caster == AlignmentEnum.freePeople) { ch.hex?.Obscure(); }             // big debuff: FP deck obscures enemy forest pos
                        else { ch.ApplyStatusEffect(StatusEffectEnum.Fear, 1); ch.ClearStatusEffect(StatusEffectEnum.Hidden); otherDebuff++; } // big debuff: dark deck fears+reveals FP
                    }
                }
            }
        }
        MessageDisplayNoUI.ShowMessage(null, null,
            $"Long Shadows (ongoing): {ownBeastBuff} forest beasts buffed; {otherDebuff} enemies feared/revealed; {otherSmall} enemy emissaries guided; {ownOpenSlowed} own open units slowed.",
            Color.gray);
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
            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;
            var beasts = board.GetHexes().Where(h => h != null && h.characters != null).SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && IsBeast(ch.race) && ch.GetAlignment() == character.GetAlignment()).Distinct().ToList();
            if (beasts.Count == 0) return false;
            foreach (var b in beasts) { b.moved = 0; b.ApplyStatusEffect(StatusEffectEnum.Encouraged, 1); }
            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Long Shadows: {beasts.Count} allied beasts reset movement and gain Encouraged.", Color.gray);
            return true;
        };
        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            Board board = FindFirstObjectByType<Board>();
            return board != null && board.GetHexes().Any(h => h != null && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed && IsBeast(ch.race) && ch.GetAlignment() == character.GetAlignment()));
        };
        asyncEffect = async (character) => { if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false; return true; };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
