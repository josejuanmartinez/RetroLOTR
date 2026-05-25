using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class JiIndurUnleashed : EventAction
{
    private static bool IsDarkServant(Character ch) =>
        ch != null && ch.GetAlignment() == AlignmentEnum.darkServants;

    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        int hidden = 0;

        foreach (Hex hex in board.GetHexes().Where(h => h != null && h.characters != null))
        {
            foreach (Character ch in hex.characters.Where(ch => ch != null && !ch.killed && ch.GetAgent() > 0).ToList())
            {
                if (UnityEngine.Random.Range(0, 100) < 25)
                {
                    ch.ApplyStatusEffect(StatusEffectEnum.Hidden, 1);
                    hidden++;
                }
            }
        }

        MessageDisplayNoUI.ShowMessage(null, null,
            $"Ji Indur Unleashed (ongoing): {hidden} agent(s) become Hidden.",
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
            if (character == null) return false;

            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;

            List<Character> agents = board.GetHexes()
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed && IsDarkServant(ch) && ch.GetAgent() > 0)
                .Distinct()
                .ToList();

            int boosted = 0;
            foreach (Character ch in agents)
            {
                ch.AddCommander(1);
                boosted++;
            }

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"Ji Indur Unleashed: {boosted} dark servant agent(s) gain +1 Commander.",
                Color.red);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;
            return board.GetHexes().Any(h => h != null && h.characters != null
                && h.characters.Any(ch => ch != null && !ch.killed && IsDarkServant(ch) && ch.GetAgent() > 0));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
