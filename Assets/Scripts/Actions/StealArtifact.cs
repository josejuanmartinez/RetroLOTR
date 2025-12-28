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
            if (c.artifacts.Count >= Character.MAX_ARTIFACTS) return false;

            return c.hex.characters.Any(ch =>
                ch != null &&
                !ch.killed &&
                ch.GetOwner() != c.GetOwner() &&
                ch.artifacts.Any(a => a != null && a.transferable));
        };
        asyncEffect = async (c) =>
        {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            if (c.artifacts.Count >= Character.MAX_ARTIFACTS) return false;

            var candidates = c.hex.characters
                .Where(ch => ch != null && !ch.killed && ch.GetOwner() != c.GetOwner())
                .Select(ch => new { character = ch, artifacts = ch.artifacts.Where(a => a != null && a.transferable).ToList() })
                .Where(x => x.artifacts.Count > 0)
                .ToList();

            if (candidates.Count < 1) return false;

            var target = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            Artifact stolen = target.artifacts[UnityEngine.Random.Range(0, target.artifacts.Count)];
            if (!target.character.artifacts.Remove(stolen)) return false;

            bool isAI = !c.isPlayerControlled;
            if (stolen.ShouldApplyAlignmentPenalty(c.GetAlignment()) && !isAI)
            {
                await ConfirmationDialog.AskOk("Artifacts of opposite alignment have health penalties for their bearers");
            }

            c.artifacts.Add(stolen);
            c.ApplyOppositeAlignmentArtifactPenalty(stolen);
            MessageDisplayNoUI.ShowMessage(c.hex, c, $"Stole {stolen.artifactName}!", Color.red);
            return true;
        };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}
