using System;
using System.Threading.Tasks;
using UnityEngine;

public class VaireLoom : EventAction
{

    private static CardData GetPreviousCard(Leader leader)
    {
        // recentlyPlayedCards already has THIS card as its last entry (ApplyCardCosts records
        // it on consumption, before Execute runs) — the one before it is what gets undone.
        int count = leader.recentlyPlayedCards.Count;
        return count >= 2 ? leader.recentlyPlayedCards[count - 2] : null;
    }

    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.GetOwner() is not Leader leader) return false;
            return GetPreviousCard(leader) != null;
        };

        effect = (character) =>
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (character == null || character.GetOwner() is not Leader leader) return false;

            CardData previous = GetPreviousCard(leader);
            if (previous == null) return false;

            if (previous.leatherRequired > 0) leader.AddLeather(previous.leatherRequired, false);
            if (previous.timberRequired > 0) leader.AddTimber(previous.timberRequired, false);
            if (previous.mountsRequired > 0) leader.AddMounts(previous.mountsRequired, false);
            if (previous.ironRequired > 0) leader.AddIron(previous.ironRequired, false);
            if (previous.steelRequired > 0) leader.AddSteel(previous.steelRequired, false);
            if (previous.mithrilRequired > 0) leader.AddMithril(previous.mithrilRequired, false);
            int goldCost = previous.GetTotalGoldCost();
            if (goldCost > 0) leader.AddGold(goldCost, false);

            character.hasActionedThisTurn = false;
            character.RefreshActionsIfSelected();

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"Vairë's Loom: the thread of \"{previous.name}\" is unwoven — its cost is returned, and {character.characterName} may act again.",
                Color.cyan);
            return true;
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
