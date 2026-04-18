using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BagginsDueAction : EventAction
{
    private const int Radius = 2;
    private const int GoldLoss = 2;

    private static bool IsEnemy(Character source, Character target)
    {
        if (source == null || target == null || target.killed) return false;
        return target.GetAlignment() != source.GetAlignment();
    }

    private static int GetPriority(Character target)
    {
        if (target == null) return 0;
        return target.GetCommander() + target.GetAgent() + target.GetEmmissary() + target.GetMage();
    }

    private static ProducesEnum? PickResourceToLose(Leader owner)
    {
        if (owner == null) return null;

        List<ProducesEnum> resources = new();
        if (owner.leatherAmount > 0) resources.Add(ProducesEnum.leather);
        if (owner.timberAmount > 0) resources.Add(ProducesEnum.timber);
        if (owner.mountsAmount > 0) resources.Add(ProducesEnum.mounts);
        if (owner.ironAmount > 0) resources.Add(ProducesEnum.iron);
        if (owner.steelAmount > 0) resources.Add(ProducesEnum.steel);
        if (owner.mithrilAmount > 0) resources.Add(ProducesEnum.mithril);
        return resources.Count == 0 ? null : resources[UnityEngine.Random.Range(0, resources.Count)];
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

            List<Character> enemies = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => IsEnemy(character, ch))
                .Distinct()
                .ToList();

            if (enemies.Count == 0) return false;

            Character target = enemies
                .OrderByDescending(GetPriority)
                .ThenByDescending(ch => ch.health)
                .FirstOrDefault();

            if (target == null) return false;

            Leader enemyOwner = target.GetOwner();
            Leader sourceOwner = character.GetOwner();
            if (enemyOwner == null || sourceOwner == null) return false;

            int goldTaken = Mathf.Min(GoldLoss, Mathf.Max(0, enemyOwner.goldAmount));
            int resourceTaken = 0;

            if (goldTaken > 0)
            {
                enemyOwner.RemoveGold(goldTaken, enemyOwner == FindFirstObjectByType<Game>()?.player);
            }
            else
            {
                ProducesEnum? resource = PickResourceToLose(enemyOwner);
                if (resource.HasValue)
                {
                    enemyOwner.RemoveResource(resource.Value, 1, enemyOwner == FindFirstObjectByType<Game>()?.player);
                    resourceTaken = 1;
                }
            }

            if (goldTaken == 0 && resourceTaken == 0) return false;

            sourceOwner.AddGold(Mathf.Max(1, goldTaken));

            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"The Sack of Bag End: the richest nearby enemy owner pays {Mathf.Max(1, goldTaken)} gold or a resource, and {sourceOwner.characterName} collects the due.",
                new Color(0.84f, 0.72f, 0.42f));

            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            return character.hex.GetHexesInRadius(Radius)
                .Any(h => h != null && h.characters != null && h.characters.Any(ch => IsEnemy(character, ch)));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
