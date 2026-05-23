using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class ElvesGoingWest : CharacterAction
{
    private const int Radius = 3;

    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        List<Character> elves = board.GetHexes()
            .Where(h => h != null && h.characters != null)
            .SelectMany(h => h.characters)
            .Where(ch => ch != null && !ch.killed && ch.race == RacesEnum.Elf)
            .Distinct().ToList();

        if (elves.Count == 0) return;

        // One random elf per turn succumbs to sea-longing
        Character chosen = elves[UnityEngine.Random.Range(0, elves.Count)];
        chosen.hasActionedThisTurn = true;
        chosen.ApplyStatusEffect(StatusEffectEnum.Despair, 1);
        chosen.ClearStatusEffect(StatusEffectEnum.Hidden);

        // Other Elves feel the grief — 25% chance each loses Hope
        foreach (Character elf in elves.Where(e => e != chosen))
        {
            if (UnityEngine.Random.value < 0.25f)
                elf.ClearStatusEffect(StatusEffectEnum.Hope);
        }
        MessageDisplayNoUI.ShowMessage(null, null,
            $"Elves Going West (ongoing): {chosen.characterName} consumed by sea-longing — loses action and gains Despair.",
            Color.cyan);
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

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;
            return character.hex.GetHexesInRadius(Radius)
                .Any(h => h != null && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed && ch.race == RacesEnum.Elf));
        };

        async Task<bool> westAsync(Character character)
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            if (character == null || character.hex == null) return false;

            List<Character> elves = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.race == RacesEnum.Elf && ch.hex != null)
                .Distinct()
                .ToList();

            if (elves.Count == 0) return false;

            bool isAI = !character.isPlayerControlled;
            Character target = null;

            if (!isAI)
            {
                string selected = await SelectionDialog.Ask(
                    "Choose Elf to compel westward",
                    "Ok",
                    "Cancel",
                    elves.Select(x => x.characterName).ToList(),
                    false,
                    SelectionDialog.Instance != null ? SelectionDialog.Instance.GetCharacterIllustration(character) : null);
                if (string.IsNullOrWhiteSpace(selected)) return false;
                target = elves.FirstOrDefault(x => x.characterName == selected);
            }
            else
            {
                target = elves.OrderByDescending(e => e.GetMage() + e.GetCommander()).FirstOrDefault();
            }

            if (target?.hex == null) return false;

            // Move toward lowest column index (west)
            List<Hex> adjacent = target.hex.GetHexesInRadius(1).Where(h => h != null && h != target.hex).ToList();
            Hex westHex = adjacent.OrderBy(h => h.v2.y).FirstOrDefault();

            if (westHex != null)
                MoveCharacterToHex(target, westHex);

            target.ApplyStatusEffect(StatusEffectEnum.Despair, 1);
            target.ClearStatusEffect(StatusEffectEnum.Hidden);
            target.hasActionedThisTurn = true;

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"{target.characterName} is compelled westward by longing, gains Despair, and cannot act offensively this turn.",
                Color.magenta);
            return true;
        }

        base.Initialize(c, condition, effect, westAsync);
    }
}
