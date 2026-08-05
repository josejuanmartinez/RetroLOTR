using System;
using System.Linq;
using UnityEngine;

public class StealArtifact : AgentAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (c) =>
        {
            return originalEffect == null || originalEffect(c);
        };
        condition = (c) =>
        {
            if (originalCondition != null && !originalCondition(c)) return false;
            if (c.objects.Count >= Character.MAX_OBJECTS) return false;

            return c.hex.characters.Any(ch =>
                ch != null &&
                !ch.killed &&
                ch.GetOwner() != c.GetOwner() &&
                ch.objects.Any(a => a != null && a.transferable));
        };
        asyncEffect = async (c) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            if (c.objects.Count >= Character.MAX_OBJECTS) return false;

            var candidates = c.hex.characters
                .Where(ch => ch != null && !ch.killed && ch.GetOwner() != c.GetOwner())
                .Select(ch => new { character = ch, objects = ch.objects.Where(a => a != null && a.transferable).ToList() })
                .Where(x => x.objects.Count > 0)
                .ToList();

            if (candidates.Count < 1) return false;

            var target = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            CardData stolen = target.objects[UnityEngine.Random.Range(0, target.objects.Count)];
            if (!target.character.objects.Remove(stolen)) return false;

            c.objects.Add(stolen);
            Character.RefreshArtifactPcVisibilityForHex(c.hex);
            MessageDisplayNoUI.ShowMessage(c.hex, c, $"Stole {stolen.name}!", Color.red);
            return true;
        };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
