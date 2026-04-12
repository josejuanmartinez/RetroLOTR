using System;
using UnityEngine;

public class MaterialRetrieval : CharacterAction
{
    protected override AdvisorType DefaultAdvisorType => AdvisorType.Economic;

    public override void Initialize(Character c, CardData card = null, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (c) =>
        {
            if (this.card != null)
            {
                Leader owner = c.GetOwner();
                if (owner != null)
                {
                    if (this.card.leatherGranted > 0) owner.AddLeather(this.card.leatherGranted, false);
                    if (this.card.mountsGranted > 0) owner.AddMounts(this.card.mountsGranted, false);
                    if (this.card.timberGranted > 0) owner.AddTimber(this.card.timberGranted, false);
                    if (this.card.ironGranted > 0) owner.AddIron(this.card.ironGranted, false);
                    if (this.card.steelGranted > 0) owner.AddSteel(this.card.steelGranted, false);
                    if (this.card.mithrilGranted > 0) owner.AddMithril(this.card.mithrilGranted, false);
                    if (this.card.goldGranted > 0) owner.AddGold(this.card.goldGranted, false);
                }
            }

            if (originalEffect != null && !originalEffect(c)) return false;
            return true;
        };

        asyncEffect = async (c) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };

        base.Initialize(c, card, condition, effect, asyncEffect);
    }
}
