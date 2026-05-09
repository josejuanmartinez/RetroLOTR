using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class DolGuldurRises : EventAction
{
    private const int RevealRadius = 4;
    private const int TeleportRadius = 3;

    private static bool IsAllied(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return true;
        return source.GetAlignment() != AlignmentEnum.neutral
            && target.GetAlignment() == source.GetAlignment()
            && target.GetAlignment() != AlignmentEnum.neutral;
    }

    private static bool IsEnemy(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return false;
        return target.GetAlignment() != source.GetAlignment() || source.GetAlignment() == AlignmentEnum.neutral;
    }

    private static void MoveCharacterToHex(Character character, Hex targetHex)
    {
        if (character == null || targetHex == null || character.hex == targetHex) return;

        Hex previousHex = character.hex;
        if (previousHex != null)
        {
            previousHex.characters.Remove(character);
            if (character.IsArmyCommander() && previousHex.armies != null && character.GetArmy() != null)
                previousHex.armies.Remove(character.GetArmy());
            previousHex.RedrawCharacters();
            previousHex.RedrawArmies();
            Character.RefreshArtifactPcVisibilityForHex(previousHex);
        }

        if (!targetHex.characters.Contains(character)) targetHex.characters.Add(character);
        if (character.IsArmyCommander() && targetHex.armies != null && character.GetArmy() != null
            && !targetHex.armies.Contains(character.GetArmy()))
            targetHex.armies.Add(character.GetArmy());

        character.hex = targetHex;
        character.RefreshKidnappedCharactersPosition();
        Character.RefreshArtifactPcVisibilityForHex(targetHex);
        targetHex.RedrawCharacters();
        targetHex.RedrawArmies();
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

            // Reveal all Hidden enemies in radius 4
            int revealedCount = 0;
            foreach (Hex h in character.hex.GetHexesInRadius(RevealRadius))
            {
                if (h?.characters == null) continue;
                foreach (Character enemy in h.characters.Where(ch => ch != null && !ch.killed
                    && IsEnemy(character, ch) && ch.HasStatusEffect(StatusEffectEnum.Hidden)).ToList())
                {
                    enemy.ClearStatusEffect(StatusEffectEnum.Hidden);
                    revealedCount++;
                }
            }

            // Allied dark characters in caster's hex may teleport to a forest hex in radius 3
            List<Hex> forestHexes = character.hex.GetHexesInRadius(TeleportRadius)
                .Where(h => h != null && h.terrainType == TerrainEnum.forest)
                .ToList();

            int teleportedCount = 0;
            if (forestHexes.Count > 0 && character.hex.characters != null)
            {
                foreach (Character ally in character.hex.characters.Where(ch => ch != null && !ch.killed
                    && IsAllied(character, ch) && ch.GetAlignment() == AlignmentEnum.darkServants && ch != character).ToList())
                {
                    Hex targetForest = forestHexes[teleportedCount % forestHexes.Count];
                    MoveCharacterToHex(ally, targetForest);
                    teleportedCount++;
                }
            }

            // All allies in hex gain protection (RefuseDuels as proxy for "cannot be targeted by card effects")
            int protectedCount = 0;
            if (character.hex.characters != null)
            {
                foreach (Character ally in character.hex.characters.Where(ch => ch != null && !ch.killed && IsAllied(character, ch)).ToList())
                {
                    ally.RefuseDuels(1);
                    protectedCount++;
                }
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"Dol Guldur Rises: {revealedCount} hidden enemy(ies) exposed; {teleportedCount} dark character(s) vanish into the forest; {protectedCount} ally(ies) join the shadows.",
                new Color(0.4f, 0.0f, 0.5f));
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;
            return character.GetAlignment() == AlignmentEnum.darkServants;
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
