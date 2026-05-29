using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class AdunaphelUnleashed : EventAction
{
    private static bool IsDarkServantEmissary(Character ch) =>
        ch != null && ch.GetAlignment() == AlignmentEnum.darkServants && ch.GetEmmissary() > 0;

    private static bool IsSouthron(Character ch) =>
        ch != null && ch.race == RacesEnum.Southron;

    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        int hidden = 0;

        foreach (Hex hex in board.GetHexes().Where(h => h != null && h.characters != null))
        {
            foreach (Character ch in hex.characters.Where(ch => ch != null && !ch.killed && IsDarkServantEmissary(ch)).ToList())
            {
                ch.ApplyStatusEffect(StatusEffectEnum.Hidden, 1);
                hidden++;
            }
        }

        MessageDisplayNoUI.ShowMessage(null, null,
            $"Adunaphel Unleashed (ongoing): {hidden} dark servant emissary(ies) become Hidden.",
            Color.magenta);
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

            List<Character> emissaries = board.GetHexes()
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && IsDarkServantEmissary(ch))
                .Distinct()
                .ToList();

            List<Character> southrons = board.GetHexes()
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && IsSouthron(ch))
                .Distinct()
                .ToList();

            int emmissaryBoosted = 0;
            foreach (Character ch in emissaries)
            {
                ch.AddEmmissary(1);
                emmissaryBoosted++;
            }

            int commanderBoosted = 0;
            foreach (Character ch in southrons)
            {
                ch.AddCommander(1);
                commanderBoosted++;
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"Adunaphel Unleashed: {emmissaryBoosted} dark servant emissary(ies) gain +1 Emissary. {commanderBoosted} Southron(s) gain +1 Commander.",
                Color.magenta);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;
            return board.GetHexes().Any(h => h != null && h.characters != null
                && h.characters.Any(ch => ch != null && !ch.killed && (IsDarkServantEmissary(ch) || IsSouthron(ch))));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
