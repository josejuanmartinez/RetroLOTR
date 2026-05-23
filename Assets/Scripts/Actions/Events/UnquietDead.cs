using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class UnquietDead : EventAction
{
    private const int ReviveHealth = 50;

    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        List<Hex> allHexes = board.GetHexes().Where(h => h != null).ToList();

        // Haste all Undead and collect their hexes for death-aura adjacency
        int undeadHasted = 0, enemiesHalted = 0;
        HashSet<Hex> undeadHexes = new HashSet<Hex>();
        foreach (Hex hex in allHexes)
        {
            if (hex.characters == null) continue;
            foreach (Character u in hex.characters.Where(ch => ch != null && !ch.killed && ch.race == RacesEnum.Undead).ToList())
            {
                u.ApplyStatusEffect(StatusEffectEnum.Haste, 1);
                undeadHasted++;
                undeadHexes.Add(hex);
            }
        }

        // Death aura: enemies on hexes adjacent to any Undead hex — 25% chance to be slowed
        HashSet<Hex> adjToUndead = new HashSet<Hex>();
        foreach (Hex uh in undeadHexes)
            foreach (Hex adj in uh.GetHexesInRadius(1))
                if (adj != null) adjToUndead.Add(adj);

        foreach (Hex adj in adjToUndead)
        {
            if (adj.characters == null) continue;
            foreach (Character enemy in adj.characters.Where(ch => ch != null && !ch.killed
                && ch.race != RacesEnum.Undead
                && !ch.IsImmuneToNegativeEnvironmentalCards()).ToList())
            {
                if (UnityEngine.Random.value < 0.25f)
                {
                    enemy.moved = Mathf.Min(enemy.moved + 1, enemy.GetMaxMovement());
                    enemiesHalted++;
                }
            }
        }
        MessageDisplayNoUI.ShowMessage(null, null,
            $"Unquiet Dead (ongoing): {undeadHasted} Undead hasted; {enemiesHalted} nearby enemies halted by death aura.",
            Color.magenta);
    }

    private static bool IsEnemy(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return false;
        return target.GetAlignment() != source.GetAlignment() || source.GetAlignment() == AlignmentEnum.neutral;
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null) return false;
            Leader owner = character.GetOwner();
            if (owner == null) return false;
            return UnityEngine.Object.FindObjectsByType<Character>(FindObjectsSortMode.None)
                .Any(ch => ch != null && ch.killed && ch.GetOwner() == owner);
        };

        async Task<bool> unquietAsync(Character character)
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            if (character == null || character.hex == null) return false;

            Leader owner = character.GetOwner();
            if (owner == null) return false;

            List<Character> deadAllies = UnityEngine.Object.FindObjectsByType<Character>(FindObjectsSortMode.None)
                .Where(ch => ch != null && ch.killed && ch.GetOwner() == owner)
                .ToList();

            if (deadAllies.Count == 0) return false;

            bool isAI = !character.isPlayerControlled;
            Character target = null;

            if (!isAI)
            {
                string selected = await SelectionDialog.Ask(
                    "Raise from the dead",
                    "Ok",
                    "Cancel",
                    deadAllies.Select(x => x.characterName).ToList(),
                    false,
                    SelectionDialog.Instance != null ? SelectionDialog.Instance.GetCharacterIllustration(character) : null);
                if (string.IsNullOrWhiteSpace(selected)) return false;
                target = deadAllies.FirstOrDefault(x => x.characterName == selected);
            }
            else
            {
                target = deadAllies.OrderByDescending(x => x.GetCommander() + x.GetMage()).FirstOrDefault();
            }

            if (target == null) return false;

            target.Revive(owner, character.hex, ReviveHealth);

            // Enemies in radius 1 of caster are Halted
            int haltedCount = 0;
            if (character.hex != null)
            {
                foreach (Hex h in character.hex.GetHexesInRadius(1))
                {
                    if (h?.characters == null) continue;
                    foreach (Character enemy in h.characters.Where(e => e != null && !e.killed && IsEnemy(character, e)).ToList())
                    {
                        enemy.Halt(1);
                        haltedCount++;
                    }
                }
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"Unquiet Dead: {target.characterName} rises with {ReviveHealth}% HP. {haltedCount} enemy(ies) in radius 1 cannot act.",
                new Color(0.5f, 0.8f, 0.5f));
            return true;
        }

        base.Initialize(c, condition, effect, unquietAsync);
    }
}
