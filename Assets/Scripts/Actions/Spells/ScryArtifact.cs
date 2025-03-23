using System;
using System.Collections.Generic;
using UnityEngine;

public class ScryArtifact: Spell
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null)
    {
        List<Hex> remainingArtifactsHexes = FindFirstObjectByType<Board>().GetHexes().FindAll(x => x.hiddenArtifacts.Count > 0);
        var originalEffect = effect;
        var originalCondition = condition;
        effect = (c) => {
            if(remainingArtifactsHexes.Count < 1) return false;
            Hex randomHex = remainingArtifactsHexes[UnityEngine.Random.Range(0, remainingArtifactsHexes.Count)];
            if(randomHex == null) return false;
            if(randomHex.hiddenArtifacts.Count < 1) return false;
            Artifact artifact = randomHex.hiddenArtifacts[0];
            randomHex.encounters.Add(EncountersEnum.Artifact);
            randomHex.Reveal();
            randomHex.LookAt();            
            MessageDisplay.ShowMessage($"You sense the power of {artifact.GetText()}", Color.green);
            return originalEffect == null || originalEffect(c);
        };
        condition = (c) => {
            return c.GetOwner() == FindFirstObjectByType<Game>().player && remainingArtifactsHexes.Count > 0 && (originalCondition == null || originalCondition(c)); 
        };
        base.Initialize(c, condition, effect);
    }
}
