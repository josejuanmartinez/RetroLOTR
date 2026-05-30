using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Decks: FP (gandalf_base), neutral (saruman_the_white), dark (the_deceiver)
// 4-part FP:    own naval get Strengthened | own naval take 5 damage (fury is indiscriminate) | enemy emissaries ArcaneInsight (sea knowledge) | enemy naval 18 damage + halted + 20% warship loss + DS -10% atk
// 4-part dark:  own naval get Strengthened | own naval take 8 damage | FP emissaries ArcaneInsight | FP naval 18 damage + halted + 25% warship loss + FP -10% atk
// neutral rule: all naval 12 damage + halted; 20% warship loss all
public class RageOfUlmo : EventAction
{
    private static bool IsSeaHex(Hex hex) => hex != null && (hex.terrainType == TerrainEnum.shore || hex.IsWaterTerrain());
    private static bool IsNaval(Character ch)
    {
        if (ch == null || ch.killed) return false;
        if (ch.isEmbarked) return true;
        if (ch.IsArmyCommander()) { Army a = ch.GetArmy(); if (a != null && a.ws > 0) return true; }
        return false;
    }

    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        AlignmentEnum caster = GetCasterAlignment();
        EnvironmentalCardManager env = EnvironmentalCardManager.Instance;

        if (caster == AlignmentEnum.neutral)
        {
            // bonus: shore/coastal chars gain Haste (storm surge propels those on land)
            // debuff: all naval 12 damage + halted; 20% warship loss
            int shoreHasted = 0, affected = 0;
            foreach (var hex in board.GetHexes().Where(h => h != null && h.characters != null))
            {
                bool isShore = hex.terrainType == TerrainEnum.shore;
                foreach (var ch in hex.characters.Where(ch => ch != null && !ch.killed).ToList())
                {
                    if (isShore && !IsNaval(ch)) { ch.ApplyStatusEffect(StatusEffectEnum.Haste, 1); shoreHasted++; }
                    if (IsSeaHex(hex) && IsNaval(ch) && !ch.IsImmuneToNegativeEnvironmentalCards())
                    { ch.health = Mathf.Max(1, ch.health - 12); if (!ch.killed) ch.Halt(1); if (ch.IsArmyCommander()) { Army a = ch.GetArmy(); if (a != null && a.ws > 0 && UnityEngine.Random.value < 0.20f) a.ws = Mathf.Max(0, a.ws - 1); } affected++; }
                }
            }
            MessageDisplayNoUI.ShowMessage(null, null, $"Rage of Ulmo (ongoing): {shoreHasted} shore chars gain Haste; {affected} naval battered 12 HP and halted.", Color.cyan);
            return;
        }

        AlignmentEnum other = caster == AlignmentEnum.freePeople ? AlignmentEnum.darkServants : AlignmentEnum.freePeople;
        if (env != null)
        {
            if (other == AlignmentEnum.darkServants) env.DarkServantsArmyAttackFactor = 0.90f;
            else env.FreePeopleArmyAttackFactor = 0.90f;
        }

        int ownStrengthened = 0, ownDamaged = 0, otherSmall = 0, otherBattered = 0;
        foreach (var hex in board.GetHexes().Where(h => h != null && IsSeaHex(h) && h.characters != null))
        {
            foreach (var ch in hex.characters.Where(ch => ch != null && !ch.killed).ToList())
            {
                if (ch.GetAlignment() == caster && IsNaval(ch))
                {
                    ch.ApplyStatusEffect(StatusEffectEnum.Strengthened, 1); ownStrengthened++;     // big bonus own
                    ch.health = Mathf.Max(1, ch.health - (caster == AlignmentEnum.freePeople ? 5 : 8)); ownDamaged++; // small debuff own
                }
                else if (ch.GetAlignment() == other)
                {
                    if (ch.GetEmmissary() > 0) { ch.ApplyStatusEffect(StatusEffectEnum.ArcaneInsight, 1); otherSmall++; } // small bonus other
                    if (IsNaval(ch) && !ch.IsImmuneToNegativeEnvironmentalCards())
                    {
                        ch.health = Mathf.Max(1, ch.health - 18); if (!ch.killed) ch.Halt(1);
                        Army a = ch.IsArmyCommander() ? ch.GetArmy() : null;
                        float shipLoss = caster == AlignmentEnum.freePeople ? 0.20f : 0.25f;
                        if (a != null && a.ws > 0 && UnityEngine.Random.value < shipLoss) a.ws = Mathf.Max(0, a.ws - 1);
                        otherBattered++;                                                           // big debuff other
                    }
                }
            }
        }
        MessageDisplayNoUI.ShowMessage(null, null,
            $"Rage of Ulmo (ongoing): {ownStrengthened} own naval strengthened; {otherBattered} enemy naval battered 18 HP; {otherSmall} enemy emissaries gain sea knowledge.",
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
            if (character == null) return false;
            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;
            var targets = board.GetHexes().Where(h => h != null && IsSeaHex(h) && h.characters != null)
                .SelectMany(h => h.characters).Where(ch => IsNaval(ch) && !ch.IsImmuneToNegativeEnvironmentalCards()).Distinct().ToList();
            if (targets.Count == 0) return false;
            int affected = 0;
            foreach (var t in targets) { if (t.HasStatusEffect(StatusEffectEnum.Burning)) t.ClearStatusEffect(StatusEffectEnum.Burning); t.Wounded(character.GetOwner(), 18); if (!t.killed) t.Halt(1); affected++; }
            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Rage of Ulmo batters {affected} naval units: 18 damage, Halted.", Color.cyan);
            return true;
        };
        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            Board board = FindFirstObjectByType<Board>();
            return board != null && board.GetHexes().Any(h => h != null && IsSeaHex(h) && h.characters != null && h.characters.Any(ch => IsNaval(ch) && !ch.IsImmuneToNegativeEnvironmentalCards()));
        };
        asyncEffect = async (character) => { if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false; return true; };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
