using System;
using UnityEngine;
using System.Linq;

public class FindArtifact: MageAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            if (c.hex.hiddenArtifacts.Count > 0) {
                Artifact artifact = c.hex.hiddenArtifacts[0];
                c.artifacts.Add(artifact);
                c.hex.hiddenArtifacts.Remove(artifact);
                if(c.hex.hiddenArtifacts.Count < 1 && c.hex.encounters.Contains(EncountersEnum.Artifact)) c.hex.encounters.Remove(EncountersEnum.Artifact);
            }

            FindObjectsByType<NonPlayableLeader>(FindObjectsSortMode.None).ToList().ForEach(x =>
            {
                x.CheckArtifactConditions(c.GetOwner());
            });

            return originalEffect == null || originalEffect(c);
        };
        condition = (c) => {
            return (originalCondition == null || originalCondition(c)); 
        };
        base.Initialize(c, condition, effect);
    }
}
