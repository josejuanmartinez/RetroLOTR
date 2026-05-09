using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class DreadOfTheNoldor : EventAction
{
    private const int Radius = 2;

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

            List<Character> elves = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.race == RacesEnum.Elf && ch.hex != null)
                .Distinct()
                .ToList();

            if (elves.Count == 0) return false;

            int displacedCount = 0;
            foreach (Character elf in elves)
            {
                bool mageCheckFails = elf.GetMage() < 2 || UnityEngine.Random.Range(0, 100) < 50;
                if (mageCheckFails)
                {
                    // Move westward (lowest v2.y adjacent hex)
                    List<Hex> adjacent = elf.hex.GetHexesInRadius(1).Where(h => h != null && h != elf.hex).ToList();
                    Hex westHex = adjacent.OrderBy(h => h.v2.y).FirstOrDefault();
                    if (westHex != null)
                    {
                        elf.ClearStatusEffect(StatusEffectEnum.Hidden);
                        MoveCharacterToHex(elf, westHex);
                        displacedCount++;
                    }
                }
                elf.ApplyStatusEffect(StatusEffectEnum.Despair, 1);
            }

            // Caster hides if in forest or shore hex
            bool casterHid = false;
            if (character.hex.terrainType == TerrainEnum.forest || character.hex.terrainType == TerrainEnum.shore)
            {
                character.Hide(1);
                casterHid = true;
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"Dread of the Noldor: {elves.Count} Elf unit(s) gain Despair; {displacedCount} displaced westward.{(casterHid ? " Caster becomes Hidden." : "")}",
                Color.magenta);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            return character.hex.GetHexesInRadius(Radius)
                .Any(h => h != null && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed && ch.race == RacesEnum.Elf));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
