using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class StarlightDispatch : EventAction
{
    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        List<Character> allChars = board.GetHexes()
            .Where(h => h != null && h.characters != null)
            .SelectMany(h => h.characters)
            .Where(ch => ch != null && !ch.killed && ch.GetAlignment() == AlignmentEnum.freePeople)
            .Distinct().ToList();

        int agentsHidden = 0, magesInsightful = 0;
        foreach (Character ch in allChars)
        {
            if (ch.GetAgent() > 0)
            {
                ch.Hide(1);
                ch.ApplyStatusEffect(StatusEffectEnum.ArcaneInsight, 1);
                agentsHidden++;
            }
            if (ch.GetMage() > 0)
            {
                ch.ApplyStatusEffect(StatusEffectEnum.ArcaneInsight, 2);
                magesInsightful++;
            }
        }
        MessageDisplayNoUI.ShowMessage(null, null,
            $"Starlight Dispatch (ongoing): {agentsHidden} allied agents hidden+insightful; {magesInsightful} mages gain deep arcane insight.",
            Color.cyan);
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null || character.IsArmyCommander()) return false;

            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;

            Hex capitalHex = board.GetHexes().Find(h => h?.GetPC() != null
                && h.GetPC().owner == character.GetOwner() && h.GetPC().isCapital);
            if (capitalHex == null || capitalHex == character.hex) return false;

            board.MoveCharacterOneHex(character, character.hex, capitalHex, true);
            MessageDisplayNoUI.ShowMessage(capitalHex, character,
                $"Starlight Dispatch: {character.characterName} returns to capital on a beam of starlight.",
                Color.cyan);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.IsArmyCommander()) return false;
            Board board = FindFirstObjectByType<Board>();
            if (board == null) return false;
            return board.GetHexes().Any(h => h?.GetPC() != null
                && h.GetPC().owner == character.GetOwner() && h.GetPC().isCapital
                && h != character.hex);
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
