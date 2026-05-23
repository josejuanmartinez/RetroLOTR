using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class FullMoon : EventAction
{
    private const int TeleportRadius = 5;

    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        List<Character> allChars = board.GetHexes()
            .Where(h => h != null && h.characters != null)
            .SelectMany(h => h.characters)
            .Where(ch => ch != null && !ch.killed)
            .Distinct().ToList();

        int nazgulHasted = 0, revealed = 0, agentHasted = 0;
        foreach (Character ch in allChars)
        {
            if (ch.race == RacesEnum.Nazgul)
            {
                ch.ClearStatusEffect(StatusEffectEnum.Halted);
                ch.ApplyStatusEffect(StatusEffectEnum.Haste, 1);
                nazgulHasted++;
            }
            else if (ch.GetAlignment() == AlignmentEnum.darkServants && ch.GetAgent() > 0)
            {
                // Dark agents work best under the full moon
                ch.ApplyStatusEffect(StatusEffectEnum.Haste, 1);
                agentHasted++;
            }
            else if (ch.GetAlignment() == AlignmentEnum.freePeople)
            {
                // Free People near any Nazgul are revealed and suffer Fear
                bool nearNazgul = allChars.Any(n => n.race == RacesEnum.Nazgul && n.hex != null
                    && ch.hex != null && n.hex.GetHexesInRadius(2).Contains(ch.hex));
                if (nearNazgul)
                {
                    ch.ClearStatusEffect(StatusEffectEnum.Hidden);
                    ch.ApplyStatusEffect(StatusEffectEnum.Fear, 1);
                    revealed++;
                }
            }
        }
        MessageDisplayNoUI.ShowMessage(null, null,
            $"Full Moon (ongoing): {nazgulHasted} Nazgul hasted; {agentHasted} dark agents hasted; {revealed} Free People near Nazgul revealed and feared.",
            Color.magenta);
    }

    private static bool IsFreePeople(Character ch) =>
        ch != null && ch.GetAlignment() == AlignmentEnum.freePeople;

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

            List<Character> nazguls = board.GetHexes()
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.race == RacesEnum.Nazgul && ch.hex != null)
                .Distinct()
                .ToList();

            if (nazguls.Count == 0) return false;

            int teleportedCount = 0;
            int movedCount = 0;

            foreach (Character nazgul in nazguls)
            {
                if (nazgul.hex == null) continue;

                Hex targetHex = nazgul.hex.GetHexesInRadius(TeleportRadius)
                    .FirstOrDefault(h => h != null && h.characters != null
                        && h.characters.Any(ch => ch != null && !ch.killed && IsFreePeople(ch)));

                if (targetHex != null)
                {
                    foreach (Character fp in targetHex.characters.Where(ch => ch != null && !ch.killed && IsFreePeople(ch)).ToList())
                        fp.ClearStatusEffect(StatusEffectEnum.Hidden);

                    MoveCharacterToHex(nazgul, targetHex);
                    nazgul.ApplyStatusEffect(StatusEffectEnum.Haste, 1);
                    teleportedCount++;
                }
                else
                {
                    nazgul.moved = Math.Max(0, nazgul.moved - 2);
                    movedCount++;
                }
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"Full Moon: {teleportedCount} Nazgul descend on Free People; {movedCount} advance toward the enemy.",
                Color.magenta);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;
            return board.GetHexes().Any(h => h != null && h.characters != null
                && h.characters.Any(ch => ch != null && !ch.killed && ch.race == RacesEnum.Nazgul));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
