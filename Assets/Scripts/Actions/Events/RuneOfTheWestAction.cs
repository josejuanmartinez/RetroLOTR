using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RuneOfTheWestAction : EventAction
{
    public override void ApplyOngoingEffect()
    {
        Board board = FindFirstObjectByType<Board>();
        if (board == null) return;

        List<Character> allChars = board.GetHexes()
            .Where(h => h != null && h.characters != null)
            .SelectMany(h => h.characters)
            .Where(ch => ch != null && !ch.killed)
            .Distinct().ToList();

        int magesHidden = 0, emissariesHoped = 0, enemyMagesRevealed = 0;
        foreach (Character ch in allChars)
        {
            if (ch.GetAlignment() == AlignmentEnum.freePeople)
            {
                if (ch.GetMage() > 0)
                {
                    ch.ApplyStatusEffect(StatusEffectEnum.ArcaneInsight, 1);
                    ch.Hide(1);
                    magesHidden++;
                }
                if (ch.GetEmmissary() > 0)
                {
                    ch.ApplyStatusEffect(StatusEffectEnum.Hope, 1);
                    emissariesHoped++;
                }
            }
            else if (ch.GetAlignment() == AlignmentEnum.darkServants && ch.GetMage() > 0)
            {
                // Western runes disrupt enemy spellwork
                ch.ClearStatusEffect(StatusEffectEnum.Hidden);
                enemyMagesRevealed++;
            }
        }
        MessageDisplayNoUI.ShowMessage(null, null,
            $"Rune of the West (ongoing): {magesHidden} allied mages hidden+insightful; {emissariesHoped} emissaries gain hope; {enemyMagesRevealed} enemy mages revealed.",
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
            if (character == null) return false;
            character.ApplyStatusEffect(StatusEffectEnum.ArcaneInsight, 1);
            character.Hide(1);
            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"{character.characterName} inscribed with a Rune of the West: ArcaneInsight and Hidden (1 turn).",
                Color.cyan);
            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            return character != null && character.GetAlignment() == AlignmentEnum.freePeople;
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
