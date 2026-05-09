using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class Horses : CharacterAction
{
    private const int TeleportRadius = 2;

    private static bool IsAllied(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return true;
        return source.GetAlignment() != AlignmentEnum.neutral
            && target.GetAlignment() == source.GetAlignment()
            && target.GetAlignment() != AlignmentEnum.neutral;
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

        if (character.GetOwner() == UnityEngine.Object.FindFirstObjectByType<Game>()?.player)
            targetHex.RevealArea(1, true);
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
            return character.hex.GetHexesInRadius(TeleportRadius)
                .Where(h => h != null && h != character.hex && h.characters != null)
                .Any(h => h.characters.Any(ch => ch != null && !ch.killed && IsAllied(character, ch)));
        };

        async Task<bool> horsesAsync(Character character)
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            if (character == null || character.hex == null) return false;

            List<Character> alliedChars = character.hex.GetHexesInRadius(TeleportRadius)
                .Where(h => h != null && h != character.hex && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && IsAllied(character, ch))
                .Distinct()
                .ToList();

            if (alliedChars.Count == 0) return false;

            bool isAI = !character.isPlayerControlled;
            Character destination = null;

            if (!isAI)
            {
                string selected = await SelectionDialog.Ask(
                    "Ride to allied character",
                    "Ok",
                    "Cancel",
                    alliedChars.Select(x => x.characterName).ToList(),
                    false,
                    SelectionDialog.Instance != null ? SelectionDialog.Instance.GetCharacterIllustration(character) : null);
                if (string.IsNullOrWhiteSpace(selected)) return false;
                destination = alliedChars.FirstOrDefault(x => x.characterName == selected);
            }
            else
            {
                destination = alliedChars.FirstOrDefault(ch => ch.IsArmyCommander()) ?? alliedChars.FirstOrDefault();
            }

            if (destination?.hex == null) return false;

            Hex targetHex = destination.hex;
            MoveCharacterToHex(character, targetHex);

            MessageDisplayNoUI.ShowMessage(character.hex ?? targetHex, character,
                $"{character.characterName} rides swiftly to join {destination.characterName}.",
                Color.green);
            return true;
        }

        base.Initialize(c, condition, effect, horsesAsync);
    }
}
