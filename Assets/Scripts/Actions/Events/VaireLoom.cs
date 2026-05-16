using System;
using System.Threading.Tasks;
using UnityEngine;

public class VaireLoom : EventAction
{
    public override void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null) return false;
            if (character.GetOwner() is not PlayableLeader leader) return false;
            DeckManager deckManager = UnityEngine.Object.FindFirstObjectByType<DeckManager>();
            return deckManager != null && deckManager.HasDeckFor(leader);
        };

        async Task<bool> loomAsync(Character character)
        {
            if (originalEffect != null && !originalEffect(character)) return false;
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            if (character == null) return false;
            if (character.GetOwner() is not PlayableLeader leader) return false;

            DeckManager deckManager = UnityEngine.Object.FindFirstObjectByType<DeckManager>();
            if (deckManager == null || !deckManager.HasDeckFor(leader)) return false;

            // Card was already consumed from hand — remaining count is originalHandSize - 1.
            int remaining = deckManager.GetHand(leader).Count;
            int targetHandSize = remaining + 1;

            deckManager.TryReshuffleHandIntoDeckAndRedraw(leader, targetHandSize);

            MessageDisplayNoUI.ShowMessage(character.hex, character,
                $"Vairë's Loom: the thread of fate is rewoven. Drew {targetHandSize} new card(s).",
                Color.cyan);
            return true;
        }

        base.Initialize(c, condition, effect, loomAsync);
    }
}
