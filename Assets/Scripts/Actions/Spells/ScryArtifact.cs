using System;
using System.Collections.Generic;
using UnityEngine;

public class ScryArtifact: Spell
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        List<Hex> remainingArtifactsHexes = FindFirstObjectByType<Board>().GetHexes().FindAll(x => x.hiddenArtifacts.Count > 0);
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (c) => {
            if (originalEffect != null && !originalEffect(c)) return false;
            if(remainingArtifactsHexes.Count < 1) return false;
            Hex randomHex = remainingArtifactsHexes[UnityEngine.Random.Range(0, remainingArtifactsHexes.Count)];
            if(randomHex == null) return false;
            if(randomHex.hiddenArtifacts.Count < 1) return false;
            Artifact artifact = randomHex.hiddenArtifacts[0];
            randomHex.Reveal(c.GetOwner());
            randomHex.LookAt();            
            MessageDisplayNoUI.ShowMessage(c.hex, c, $"<sprite name=\"artifact\"> {artifact.GetHoverText()}", Color.green);
            return true;
        };
        condition = (c) => {
            if (originalCondition != null && !originalCondition(c)) return false;
            return c.artifacts.Find(x => x.providesSpell == actionName) != null && remainingArtifactsHexes.Count > 0; 
        };
        asyncEffect = async (c) => {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}

