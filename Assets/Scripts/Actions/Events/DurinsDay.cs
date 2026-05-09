using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class DurinsDay : EventAction
{
    private const int ArtifactRadius = 3;
    private const int DwarfRadius = 3;

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

            List<Character> dwarves = board.GetHexes()
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.race == RacesEnum.Dwarf)
                .Distinct()
                .ToList();

            if (dwarves.Count == 0) return false;

            // Reveal a random artifact site in Dwarf territory (radius from caster)
            int artifactsRevealed = 0;
            if (character.hex != null)
            {
                List<Hex> nearbyHexes = character.hex.GetHexesInRadius(ArtifactRadius);
                foreach (Hex h in nearbyHexes)
                {
                    if (h != null && h.hiddenArtifacts != null && h.hiddenArtifacts.Count > 0)
                    {
                        h.RevealArtifact();
                        artifactsRevealed++;
                        break;
                    }
                }
            }

            // All Dwarves in radius 3 may act again and gain Hope
            int activatedCount = 0;
            if (character.hex != null)
            {
                List<Hex> dwarfHexes = character.hex.GetHexesInRadius(DwarfRadius);
                foreach (Character dwarf in dwarves.Where(d => d.hex != null && dwarfHexes.Contains(d.hex)))
                {
                    dwarf.hasActionedThisTurn = false;
                    dwarf.ApplyStatusEffect(StatusEffectEnum.Hope, 1);
                    activatedCount++;
                }
            }
            else
            {
                foreach (Character dwarf in dwarves)
                {
                    dwarf.hasActionedThisTurn = false;
                    dwarf.ApplyStatusEffect(StatusEffectEnum.Hope, 1);
                    activatedCount++;
                }
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"Durin's Day: {artifactsRevealed} artifact site(s) revealed; {activatedCount} Dwarf(s) may act again and gain Hope.",
                Color.yellow);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;
            return board.GetHexes().Any(h => h != null && h.characters != null
                && h.characters.Any(ch => ch != null && !ch.killed && ch.race == RacesEnum.Dwarf));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
