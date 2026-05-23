using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class FrozenPasses : EventAction
{
    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        List<Character> mountainChars = board.GetHexes()
            .Where(h => h != null && h.terrainType == TerrainEnum.mountains && h.characters != null)
            .SelectMany(h => h.characters)
            .Where(ch => ch != null && !ch.killed && ch.race != RacesEnum.Dwarf
                && !ch.IsImmuneToNegativeEnvironmentalCards())
            .Distinct().ToList();

        int frozen = 0, slowed = 0;
        foreach (Character ch in mountainChars)
        {
            // All mountain units slowed; 25% chance to be Frozen
            ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement());
            slowed++;
            if (UnityEngine.Random.value < 0.25f)
            {
                ch.ApplyStatusEffect(StatusEffectEnum.Frozen, 1);
                frozen++;
            }
            // Cavalry are especially devastated — 25% chance to lose a unit
            if (ch.IsArmyCommander())
            {
                Army army = ch.GetArmy();
                if (army != null && (army.lc > 0 || army.hc > 0) && UnityEngine.Random.value < 0.25f)
                {
                    if (army.lc > 0) army.lc = Mathf.Max(0, army.lc - 1);
                    else army.hc = Mathf.Max(0, army.hc - 1);
                }
            }
        }
        MessageDisplayNoUI.ShowMessage(null, null,
            $"Frozen Passes (ongoing): {slowed} mountain units slowed; {frozen} frozen by ice. Dwarves navigate freely.",
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

            List<Character> enemies = board.GetHexes()
                .Where(h => h != null && h.terrainType == TerrainEnum.mountains && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.GetAlignment() != character.GetAlignment()
                    && ch.race != RacesEnum.Dwarf && !ch.IsImmuneToNegativeEnvironmentalCards())
                .Distinct().ToList();

            foreach (Character enemy in enemies)
            {
                enemy.ApplyStatusEffect(StatusEffectEnum.Frozen, 1);
                enemy.Halt(1);
            }
            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"Frozen Passes: {enemies.Count} enemy mountain units frozen and halted.",
                Color.cyan);
            return enemies.Count > 0;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;
            return board.GetHexes().Any(h => h != null && h.terrainType == TerrainEnum.mountains
                && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed
                    && ch.GetAlignment() != character?.GetAlignment() && ch.race != RacesEnum.Dwarf));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
