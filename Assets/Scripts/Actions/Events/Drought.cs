using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Decks: neutral (of_many_colours) — Saruman's many-coloured desert pressure card.
// The heat-side mirror of the Frozen inflicters (Snow / Cruel Winter / Caradhras), built on Sunburnt.
// Where Sand Storm slows and freezes cavalry/siege, Drought bakes the unprepared: sunburn + thirst.
//
// neutral rule:  Southron/Easterling endure (Encouraged) | other races on desert/wasteland slowed 1 + 5% Sunburnt
// faction rule:  own desert races Encouraged | enemy desert/wasteland units slowed 1 + 10% Sunburnt,
//                and 20% of mounted enemy armies lose a mount to thirst (army modification, not a status)
public class Drought : EventAction
{
    private static bool IsParched(Hex h) => h != null && (h.terrainType == TerrainEnum.desert || h.terrainType == TerrainEnum.wastelands);
    private static bool IsDesertRace(Character ch) => ch != null && (ch.race == RacesEnum.Southron || ch.race == RacesEnum.Easterling);

    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        AlignmentEnum caster = GetCasterAlignment();
        AlignmentEnum other = caster == AlignmentEnum.darkServants ? AlignmentEnum.freePeople
            : caster == AlignmentEnum.freePeople ? AlignmentEnum.darkServants : AlignmentEnum.neutral;

        int endured = 0, slowed = 0, sunburnt = 0, mountsLost = 0;

        foreach (var (ch, hex) in board.GetHexes().Where(h => IsParched(h) && h.characters != null)
            .SelectMany(h => h.characters.Select(ch => (ch, h))).Where(t => t.ch != null && !t.ch.killed))
        {
            if (IsDesertRace(ch))
            {
                ch.Encourage(1); endured++;                                                       // big bonus own: desert-born thrive
                continue;
            }

            if (ch.IsImmuneToNegativeEnvironmentalCards()) continue;
            if (caster != AlignmentEnum.neutral && ch.GetAlignment() == caster) continue;          // a faction's Drought spares its own ranks
            if (caster != AlignmentEnum.neutral && ch.GetAlignment() != other) continue;

            ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement()); slowed++;                      // heat exhaustion saps the march

            float sunburnChance = caster == AlignmentEnum.neutral ? 0.05f : 0.10f;
            if (UnityEngine.Random.value < sunburnChance) { ch.ApplyStatusEffect(StatusEffectEnum.Sunburnt, 1); sunburnt++; }

            // Thirst rarely claims a pack animal — only on true desert, and only for these non-desert
            // races (already filtered above). Kept light: a 5% chance to lose a single mount.
            if (hex.terrainType == TerrainEnum.desert && ch.IsArmyCommander() && UnityEngine.Random.value < 0.05f)
            {
                Army army = ch.GetArmy();
                if (army != null && (army.lc > 0 || army.hc > 0))
                {
                    TroopsTypeEnum? lost = army.RemoveRandomTroopOfTypes(TroopsTypeEnum.lc, TroopsTypeEnum.hc);
                    if (lost.HasValue) { mountsLost++; MessageDisplayNoUI.ShowMessage(hex, ch, $"{ch.characterName}'s mount dies of thirst (-1 <sprite name=\"{lost.Value.ToString().ToLower()}\">).", Color.yellow); }
                }
            }
        }

        MessageDisplayNoUI.ShowMessage(null, null,
            $"Drought (ongoing): {endured} desert-born endure; {slowed} parched and slowed; {sunburnt} sunburnt; {mountsLost} mounts lost to thirst.",
            new Color(1f, 0.55f, 0.1f));
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (ch) =>
        {
            if (originalEffect != null && !originalEffect(ch)) return false;
            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;
            var targets = board.GetHexes().Where(h => IsParched(h) && h.characters != null)
                .SelectMany(h => h.characters).Where(x => x != null && !x.killed && !IsDesertRace(x) && !x.IsImmuneToNegativeEnvironmentalCards()).Distinct().ToList();
            if (targets.Count == 0) return false;
            foreach (var t in targets) t.ApplyStatusEffect(StatusEffectEnum.Sunburnt, 1);
            MessageDisplayNoUI.ShowMessage(ch?.hex, ch, $"Drought scorches {targets.Count} unit(s) caught on the parched land (Sunburnt).", new Color(1f, 0.55f, 0.1f));
            return true;
        };
        condition = (ch) =>
        {
            if (originalCondition != null && !originalCondition(ch)) return false;
            Board board = FindFirstObjectByType<Board>();
            return board != null && board.GetHexes().Any(h => IsParched(h) && h.characters != null && h.characters.Any(x => x != null && !x.killed && !IsDesertRace(x) && !x.IsImmuneToNegativeEnvironmentalCards()));
        };
        asyncEffect = async (ch) => { if (originalAsyncEffect != null && !await originalAsyncEffect(ch)) return false; return true; };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
