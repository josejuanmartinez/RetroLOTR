using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class DoorsOfNight : EventAction
{
    private const int ObscureRadius = 4;

    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        int darkBoosted = 0, fpDebuffed = 0, hiddenAgents = 0;
        foreach (Hex hex in board.GetHexes().Where(h => h != null && h.characters != null))
        {
            bool isForest = hex.terrainType == TerrainEnum.forest;
            foreach (Character ch in hex.characters.Where(ch => ch != null && !ch.killed).ToList())
            {
                if (ch.GetAlignment() == AlignmentEnum.darkServants)
                {
                    ch.Encourage(1);
                    ch.ClearStatusEffect(StatusEffectEnum.Fear);
                    if (ch.GetAgent() > 0 || isForest)
                    {
                        ch.Hide(1);
                        hiddenAgents++;
                    }
                    darkBoosted++;
                }
                else if (ch.GetAlignment() == AlignmentEnum.freePeople)
                {
                    ch.ApplyStatusEffect(StatusEffectEnum.Despair, 1);
                    ch.moved = Mathf.Min(ch.moved + 1, ch.GetMaxMovement());
                    fpDebuffed++;
                }
            }
        }
        foreach (Hex hex in board.GetHexes().Where(h => h != null))
            hex.Obscure();

        MessageDisplayNoUI.ShowMessage(null, null,
            $"Doors of Night (ongoing): {darkBoosted} dark servants encouraged; {hiddenAgents} hidden in shadow; {fpDebuffed} Free People despaired and slowed.",
            Color.red);
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

            // Obscure all hexes in radius 4
            character.hex.ObscureArea(ObscureRadius, false);

            // Allied dark-aligned characters in radius 4 become Hidden
            List<Character> darkAllies = character.hex.GetHexesInRadius(ObscureRadius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.GetAlignment() == AlignmentEnum.darkServants)
                .Distinct()
                .ToList();

            foreach (Character darkAlly in darkAllies)
                darkAlly.Hide(1);

            // Dispel Dawn: clear Encouraged from Free People
            List<Character> freePeople = board.GetHexes()
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && ch.GetAlignment() == AlignmentEnum.freePeople)
                .Distinct()
                .ToList();

            foreach (Character fp in freePeople)
                fp.ClearEncouraged();

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"Doors of Night: radius {ObscureRadius} shrouded; {darkAllies.Count} dark servant(s) become Hidden; Dawn dispelled.",
                Color.red);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.GetAlignment() == AlignmentEnum.freePeople) return false;
            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;
            return board.GetHexes().Any(h => h != null && h.characters != null
                && h.characters.Any(ch => ch != null && !ch.killed && ch.GetAlignment() == AlignmentEnum.darkServants));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
