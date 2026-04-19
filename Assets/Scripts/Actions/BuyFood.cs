using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BuyFood : EmmissaryPCAction
{
    private const int GoldPerArmy = 2;

    protected override AdvisorType DefaultAdvisorType => AdvisorType.Militaristic;

    public override void Initialize(
        Character c,
        Func<Character, bool> condition = null,
        Func<Character, bool> effect = null,
        Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;

        effect = (c) =>
        {
            if (originalEffect != null && !originalEffect(c)) return false;
            if (c?.GetOwner() is not PlayableLeader owner) return false;

            List<Character> armyCommanders = owner.controlledCharacters
                .Where(ch => ch != null && !ch.killed && ch.IsArmyCommander() && ch.GetArmy() != null)
                .Distinct()
                .ToList();

            if (armyCommanders.Count == 0) return false;

            int maxAffordable = owner.goldAmount / GoldPerArmy;
            int affectedCount = Math.Min(armyCommanders.Count, maxAffordable);
            if (affectedCount <= 0) return false;

            int totalCost = affectedCount * GoldPerArmy;
            owner.RemoveGold(totalCost);

            for (int i = 0; i < affectedCount; i++)
            {
                armyCommanders[i].ApplyStatusEffect(StatusEffectEnum.Haste, 1);
            }

            MessageDisplayNoUI.ShowMessage(
                c.hex,
                c,
                $"Army Rations: {affectedCount} army(ies) gain Haste for 1 turn (-{totalCost} <sprite name=\"gold\">).",
                Color.green);

            return true;
        };

        condition = (c) =>
        {
            if (originalCondition != null && !originalCondition(c)) return false;
            if (c?.GetOwner() is not PlayableLeader owner) return false;
            if (owner.goldAmount < GoldPerArmy) return false;

            return owner.controlledCharacters.Any(ch => ch != null && !ch.killed && ch.IsArmyCommander() && ch.GetArmy() != null);
        };

        asyncEffect = async (c) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
