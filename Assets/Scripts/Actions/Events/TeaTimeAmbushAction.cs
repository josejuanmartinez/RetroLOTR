using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TeaTimeAmbushAction : EventAction
{
    private const int Radius = 2;

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

            Leader sourceOwner = character.GetOwner();
            if (sourceOwner == null) return false;

            var nearby = character.hex.GetHexesInRadius(Radius)
                .Where(h => h != null && h.characters != null)
                .SelectMany(h => h.characters)
                .Where(ch => ch != null && !ch.killed)
                .Distinct()
                .ToList();

            if (nearby.Count == 0) return false;

            int alliesHidden = 0;
            int tributeTaken = 0;

            for (int i = 0; i < nearby.Count; i++)
            {
                Character target = nearby[i];
                if (target.GetAlignment() == character.GetAlignment() &&
                    (target.race == RacesEnum.Hobbit || target.race == RacesEnum.Dwarf))
                {
                    target.Hide(1);
                    alliesHidden++;
                }

                if (target.GetAlignment() != character.GetAlignment())
                {
                    Leader owner = target.GetOwner();
                    if (owner == null) continue;

                    int goldTaken = Mathf.Min(1, Mathf.Max(0, owner.goldAmount));
                    if (goldTaken > 0)
                    {
                        owner.RemoveGold(goldTaken, owner == FindFirstObjectByType<Game>()?.player);
                        sourceOwner.AddGold(goldTaken);
                        tributeTaken += goldTaken;
                        continue;
                    }

                    ProducesEnum? lost = PickResourceToLose(owner);
                    if (lost.HasValue)
                    {
                        owner.RemoveResource(lost.Value, 1, owner == FindFirstObjectByType<Game>()?.player);
                        sourceOwner.AddGold(1);
                        tributeTaken++;
                    }
                }
            }

            if (alliesHidden == 0 && tributeTaken == 0) return false;

            MessageDisplayNoUI.ShowMessage(
                character.hex,
                character,
                $"Tea Time Ambush: {alliesHidden} allied Hobbit/Dwarf unit(s) slip into Hidden (1), and the tea table extracts {tributeTaken} tribute from nearby enemies.",
                new Color(0.72f, 0.61f, 0.41f));

            return true;
        };

        condition = (character) =>
        {
            if (originalCondition != null && !originalCondition(character)) return false;
            if (character == null || character.hex == null) return false;

            return character.hex.GetHexesInRadius(Radius)
                .Any(h => h != null && h.characters != null && h.characters.Any(ch => ch != null && !ch.killed));
        };

        asyncEffect = async (character) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(character)) return false;
            return true;
        };

        base.Initialize(c, condition, effect, asyncEffect);
    }
}
