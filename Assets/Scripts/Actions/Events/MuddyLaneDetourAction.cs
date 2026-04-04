using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MuddyLaneDetourAction : EventAction
{
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

            List<ProducesEnum> availableResources = new();
            if (owner.leatherAmount > 0) availableResources.Add(ProducesEnum.leather);
            if (owner.timberAmount > 0) availableResources.Add(ProducesEnum.timber);
            if (owner.mountsAmount > 0) availableResources.Add(ProducesEnum.mounts);
            if (owner.ironAmount > 0) availableResources.Add(ProducesEnum.iron);
            if (owner.steelAmount > 0) availableResources.Add(ProducesEnum.steel);
            if (owner.mithrilAmount > 0) availableResources.Add(ProducesEnum.mithril);

            string lossText;
            if (availableResources.Count > 0)
            {
                ProducesEnum lost = availableResources[UnityEngine.Random.Range(0, availableResources.Count)];
                owner.RemoveResource(lost, 1, owner == FindFirstObjectByType<Game>()?.player);
                lossText = $"1 {lost}";
            }
            else if (owner.goldAmount > 0)
            {
                owner.RemoveGold(1, owner == FindFirstObjectByType<Game>()?.player);
                lossText = "1 gold";
            }
            else
            {
                return false;
            }

            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"Muddy Lane Detour: {target.characterName}'s side loses {lossText} in the mess.",
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
