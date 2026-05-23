using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TheShadowDeepens : EventAction
{
    private static bool IsShadowCreature(RacesEnum r) =>
        r == RacesEnum.Spider || r == RacesEnum.Goblin || r == RacesEnum.Orc;

    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        List<(Character ch, TerrainEnum terrain)> forestChars = board.GetHexes()
            .Where(h => h != null && h.terrainType == TerrainEnum.forest && h.characters != null)
            .SelectMany(h => h.characters.Select(ch => (ch, h.terrainType)))
            .Where(t => t.ch != null && !t.ch.killed)
            .Distinct().ToList();

        int shadowHasted = 0, shadowHidden = 0, enemiesFeared = 0;
        foreach (var (ch, _) in forestChars)
        {
            if (IsShadowCreature(ch.race) && ch.GetAlignment() == AlignmentEnum.darkServants)
            {
                ch.ApplyStatusEffect(StatusEffectEnum.Haste, 1);
                ch.Hide(1);
                shadowHasted++;
                shadowHidden++;
            }
            else if (ch.GetAlignment() == AlignmentEnum.freePeople && !ch.IsImmuneToNegativeEnvironmentalCards())
            {
                ch.ApplyStatusEffect(StatusEffectEnum.Fear, 1);
                ch.ClearStatusEffect(StatusEffectEnum.Hidden);
                enemiesFeared++;
            }
        }
        // Dark agents in forest: extra Hidden
        List<Character> darkAgents = board.GetHexes()
            .Where(h => h != null && h.terrainType == TerrainEnum.forest && h.characters != null)
            .SelectMany(h => h.characters)
            .Where(ch => ch != null && !ch.killed && ch.GetAlignment() == AlignmentEnum.darkServants && ch.GetAgent() > 0)
            .Distinct().ToList();
        foreach (Character ch in darkAgents) ch.Hide(1);

        MessageDisplayNoUI.ShowMessage(null, null,
            $"Shadow Deepens (ongoing): {shadowHasted} Spiders/Orcs in forest hasted and hidden; {enemiesFeared} Free People feared and revealed.",
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

            List<Character> shadows = character.hex.GetHexesInRadius(3)
                .Where(h => h != null && h.terrainType == TerrainEnum.forest && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && IsShadowCreature(ch.race)
                    && ch.GetAlignment() == AlignmentEnum.darkServants)
                .Distinct().ToList();

            List<Character> enemies = character.hex.GetHexesInRadius(3)
                .Where(h => h != null && h.terrainType == TerrainEnum.forest && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.GetAlignment() == AlignmentEnum.freePeople)
                .Distinct().ToList();

            foreach (Character sh in shadows) { sh.ApplyStatusEffect(StatusEffectEnum.Haste, 2); }
            foreach (Character enemy in enemies) { enemy.ClearStatusEffect(StatusEffectEnum.Hidden); enemy.ApplyStatusEffect(StatusEffectEnum.Fear, 1); }

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"The Shadow Deepens: {shadows.Count} Spiders/Orcs in forest gain Haste; {enemies.Count} Free People revealed and feared.",
                Color.gray);
            return shadows.Count > 0 || enemies.Count > 0;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;
            return board.GetHexes().Any(h => h != null && h.terrainType == TerrainEnum.forest
                && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed
                    && (IsShadowCreature(ch.race) || ch.GetAlignment() == AlignmentEnum.freePeople)));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
