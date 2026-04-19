using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MuddyLaneDetourAction : EventAction
{
    private const int TotalDrainValue = 10;

    private static int GetResourceValue(ProducesEnum resourceType)
    {
        return resourceType switch
        {
            ProducesEnum.leather => 1,
            ProducesEnum.timber => 2,
            ProducesEnum.iron => 3,
            ProducesEnum.mounts => 2,
            ProducesEnum.steel => 4,
            ProducesEnum.mithril => 5,
            ProducesEnum.gold => 1,
            _ => 0
        };
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

            List<Character> enemyTargets = character.hex.characters
                .Where(ch => ch != null && !ch.killed && ch.GetAlignment() != character.GetAlignment())
                .Distinct()
                .ToList();

            if (enemyTargets.Count == 0) return false;

            Character target = enemyTargets
                .OrderByDescending(ch => ch.GetCommander() + ch.GetAgent() + ch.GetEmmissary() + ch.GetMage())
                .ThenByDescending(ch => ch.health)
                .FirstOrDefault();

            if (target == null) return false;

            Leader owner = target.GetOwner();
            if (owner == null) return false;

            int remainingValue = TotalDrainValue;
            int goldLoss = Mathf.Min(owner.goldAmount, remainingValue);
            if (goldLoss > 0)
            {
                owner.RemoveGold(goldLoss, owner == FindFirstObjectByType<Game>()?.player);
                remainingValue -= goldLoss;
            }

            List<(ProducesEnum resource, int value, Func<int> getter, Action<int, bool> remover)> resources = new()
            {
                (ProducesEnum.mithril, GetResourceValue(ProducesEnum.mithril), () => owner.mithrilAmount, (amount, showMessage) => owner.RemoveMithril(amount, showMessage)),
                (ProducesEnum.steel, GetResourceValue(ProducesEnum.steel), () => owner.steelAmount, (amount, showMessage) => owner.RemoveSteel(amount, showMessage)),
                (ProducesEnum.iron, GetResourceValue(ProducesEnum.iron), () => owner.ironAmount, (amount, showMessage) => owner.RemoveIron(amount, showMessage)),
                (ProducesEnum.mounts, GetResourceValue(ProducesEnum.mounts), () => owner.mountsAmount, (amount, showMessage) => owner.RemoveMounts(amount, showMessage)),
                (ProducesEnum.timber, GetResourceValue(ProducesEnum.timber), () => owner.timberAmount, (amount, showMessage) => owner.RemoveTimber(amount, showMessage)),
                (ProducesEnum.leather, GetResourceValue(ProducesEnum.leather), () => owner.leatherAmount, (amount, showMessage) => owner.RemoveLeather(amount, showMessage)),
            };

            List<string> drainedParts = new();
            for (int i = 0; i < resources.Count && remainingValue > 0; i++)
            {
                var entry = resources[i];
                int available = entry.getter();
                if (available <= 0 || entry.value <= 0) continue;

                int maxUnits = Mathf.Min(available, remainingValue / entry.value);
                if (maxUnits <= 0) continue;

                entry.remover(maxUnits, owner == FindFirstObjectByType<Game>()?.player);
                drainedParts.Add($"{maxUnits}<sprite name=\"{entry.resource.ToString().ToLowerInvariant()}\">");
                remainingValue -= maxUnits * entry.value;
            }

            if (remainingValue > 0 && owner.goldAmount > 0)
            {
                int extraGold = Mathf.Min(owner.goldAmount, remainingValue);
                owner.RemoveGold(extraGold, owner == FindFirstObjectByType<Game>()?.player);
                drainedParts.Add($"{extraGold}<sprite name=\"gold\">");
                remainingValue -= extraGold;
            }

            if (drainedParts.Count == 0) return false;

            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"Muddy Lane: {target.characterName}'s side loses {string.Join(" ", drainedParts)}.",
                new Color(0.55f, 0.34f, 0.16f));

            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            return character.hex.characters.Any(ch => ch != null && !ch.killed && ch.GetAlignment() != character.GetAlignment() && ch.GetOwner() != null &&
                (ch.GetOwner().goldAmount > 0
                || ch.GetOwner().leatherAmount > 0
                || ch.GetOwner().timberAmount > 0
                || ch.GetOwner().mountsAmount > 0
                || ch.GetOwner().ironAmount > 0
                || ch.GetOwner().steelAmount > 0
                || ch.GetOwner().mithrilAmount > 0));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
