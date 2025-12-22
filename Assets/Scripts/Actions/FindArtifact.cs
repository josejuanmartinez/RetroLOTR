using System;
using UnityEngine;

public class FindArtifact: MageAction
{
    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (c) => {
            if (originalEffect != null && !originalEffect(c)) return false;
            if (c.hex.hiddenArtifacts.Count > 0) {
                if (c.artifacts.Count >= Character.MAX_ARTIFACTS)
                {
                    _ = ConfirmationDialog.AskOk($"{c.characterName} can't hold more artifacts");
                    return false;
                }
                Artifact artifact = c.hex.hiddenArtifacts[0];
                c.artifacts.Add(artifact);
                c.hex.hiddenArtifacts.Remove(artifact);
                MessageDisplayNoUI.ShowMessage(c.hex, c, $"<sprite name=\"artifact\"> {artifact.GetHoverText()} found", Color.green);
            } 
            else
            {
                MessageDisplayNoUI.ShowMessage(c.hex, c, $"No <sprite name=\"artifact\"> found", Color.red);
            }


            return true;
        };
        condition = (c) => { return originalCondition == null || originalCondition(c); };
        asyncEffect = async (c) => {
            if (originalAsyncEffect != null && !await originalAsyncEffect(c)) return false;
            return true;
        };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}

