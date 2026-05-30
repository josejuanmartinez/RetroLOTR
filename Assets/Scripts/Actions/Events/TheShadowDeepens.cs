using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Deck: dark only (the_necromancer)
// 4-part: DS forest Spiders/Orcs/Goblins: Haste + Hidden + DS agents Hidden | DS non-forest slowed 1 | FP forest emissaries ArcaneInsight | FP forest feared + revealed + DS +8% atk
public class TheShadowDeepens : EventAction
{
    private static bool IsShadowCreature(RacesEnum r) => r == RacesEnum.Spider || r == RacesEnum.Goblin || r == RacesEnum.Orc;

    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        EnvironmentalCardManager env = EnvironmentalCardManager.Instance;
        if (env != null) env.DarkServantsArmyAttackFactor = 1.08f;

        int shadowBuff = 0, dsSlowed = 0, fpSmall = 0, fpFeared = 0;
        foreach (var hex in board.GetHexes().Where(h => h != null && h.characters != null))
        {
            bool isForest = hex.terrainType == TerrainEnum.forest;
            foreach (var ch in hex.characters.Where(ch => ch != null && !ch.killed).ToList())
            {
                if (ch.GetAlignment() == AlignmentEnum.darkServants)
                {
                    if (isForest && (IsShadowCreature(ch.race) || ch.GetAgent() > 0)) { ch.ApplyStatusEffect(StatusEffectEnum.Haste, 1); ch.Hide(1); shadowBuff++; } // big bonus
                    if (!isForest) { ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement()); dsSlowed++; } // small debuff own
                }
                else if (ch.GetAlignment() == AlignmentEnum.freePeople)
                {
                    if (isForest && ch.GetEmmissary() > 0) { ch.ApplyStatusEffect(StatusEffectEnum.ArcaneInsight, 1); fpSmall++; } // small bonus other
                    if (isForest && !ch.IsImmuneToNegativeEnvironmentalCards()) { ch.ApplyStatusEffect(StatusEffectEnum.Fear, 1); ch.ClearStatusEffect(StatusEffectEnum.Hidden); fpFeared++; } // big debuff
                }
            }
        }
        MessageDisplayNoUI.ShowMessage(null, null,
            $"Shadow Deepens (ongoing): {shadowBuff} dark forest creatures hasted and hidden; {fpFeared} FP in forest feared and revealed; {fpSmall} FP emissaries sense the shadow.",
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
            var shadows = character.hex.GetHexesInRadius(3).Where(h => h != null && h.terrainType == TerrainEnum.forest && h.characters != null)
                .SelectMany(h => h.characters).Where(ch => ch != null && !ch.killed && IsShadowCreature(ch.race) && ch.GetAlignment() == AlignmentEnum.darkServants).Distinct().ToList();
            var enemies = character.hex.GetHexesInRadius(3).Where(h => h != null && h.terrainType == TerrainEnum.forest && h.characters != null)
                .SelectMany(h => h.characters).Where(ch => ch != null && !ch.killed && ch.GetAlignment() == AlignmentEnum.freePeople).Distinct().ToList();
            foreach (var s in shadows) s.ApplyStatusEffect(StatusEffectEnum.Haste, 2);
            foreach (var e in enemies) { e.ClearStatusEffect(StatusEffectEnum.Hidden); e.ApplyStatusEffect(StatusEffectEnum.Fear, 1); }
            MessageDisplayNoUI.ShowMessage(character.hex, character, $"Shadow Deepens: {shadows.Count} dark forest creatures hasted; {enemies.Count} FP revealed and feared.", Color.gray);
            return shadows.Count > 0 || enemies.Count > 0;
        };
        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            Board board = FindFirstObjectByType<Board>();
            return board != null && board.GetHexes().Any(h => h != null && h.terrainType == TerrainEnum.forest && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed && (IsShadowCreature(ch.race) || ch.GetAlignment() == AlignmentEnum.freePeople)));
        };
        asyncEffect = async (character) => { if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false; return true; };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
