using System;
using System.Collections.Generic;
using UnityEngine;

public class FindArtifact: MageAction
{
    private static List<Riddle> cachedRiddles;
    private static bool riddlesLoaded;

    private static List<Riddle> GetRiddles()
    {
        if (riddlesLoaded) return cachedRiddles;
        riddlesLoaded = true;

        TextAsset json = Resources.Load<TextAsset>("Riddles");
        if (json == null)
        {
            Debug.LogWarning("Riddles.json not found in Resources.");
            cachedRiddles = new();
            return cachedRiddles;
        }

        RiddleCollection collection = JsonUtility.FromJson<RiddleCollection>(json.text);
        cachedRiddles = collection?.riddles ?? new();
        return cachedRiddles;
    }

    private static Riddle GetRandomRiddle()
    {
        List<Riddle> riddles = GetRiddles();
        if (riddles == null || riddles.Count < 1) return null;
        return riddles[UnityEngine.Random.Range(0, riddles.Count)];
    }

    override public void Initialize(Character c, Func<Character, bool> condition = null, Func<Character, bool> effect = null, Func<Character, System.Threading.Tasks.Task<bool>> asyncEffect = null)
    {
        var originalEffect = effect;
        var originalCondition = condition;
        var originalAsyncEffect = asyncEffect;
        effect = (c) => {
            if (originalEffect != null && !originalEffect(c)) return false;
            if (c.hex.hiddenArtifacts.Count > 0) {
                c.hex.RevealArtifact();
                if (c.artifacts.Count >= Character.MAX_ARTIFACTS)
                {
                    _ = ConfirmationDialog.AskOk($"{c.characterName} can't hold more artifacts");
                    return false;
                }
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
            if (c.hex.hiddenArtifacts.Count < 1) return true;
            if (c.artifacts.Count >= Character.MAX_ARTIFACTS) return false;

            Artifact artifact = c.hex.hiddenArtifacts[0];
            bool isAI = !c.isPlayerControlled;
            Riddle riddle = GetRandomRiddle();
            async System.Threading.Tasks.Task ApplyAlignmentPenaltyIfNeeded()
            {
                if (!artifact.ShouldApplyAlignmentPenalty(c.GetAlignment())) return;
                if (!isAI)
                {
                    await ConfirmationDialog.AskOk("Artifacts of opposite alignment have health penalties for their bearers");
                }
                c.ApplyOppositeAlignmentArtifactPenalty(artifact);
            }

            if (riddle == null || riddle.options == null || riddle.options.Count < 1)
            {
                c.artifacts.Add(artifact);
                c.hex.hiddenArtifacts.Remove(artifact);
                c.hex.UpdateArtifactVisibility();
                await ApplyAlignmentPenaltyIfNeeded();
                MessageDisplayNoUI.ShowMessage(c.hex, c, $"<sprite name=\"artifact\"> {artifact.GetHoverText()} found", Color.green);
                Sounds.Instance?.PlayArtifactFound();
                return true;
            }

            string answer = await SelectionDialog.Ask(riddle.prompt, "Speak", "Leave", riddle.options, isAI, SelectionDialog.Instance != null ? SelectionDialog.Instance.GetCharacterIllustration(c) : null);
            if (string.Equals(answer, riddle.answer, StringComparison.OrdinalIgnoreCase))
            {
                c.artifacts.Add(artifact);
                c.hex.hiddenArtifacts.Remove(artifact);
                c.hex.UpdateArtifactVisibility();
                await ApplyAlignmentPenaltyIfNeeded();
                MessageDisplayNoUI.ShowMessage(c.hex, c, $"<sprite name=\"artifact\"> {artifact.GetHoverText()} claimed", Color.green);
                Sounds.Instance?.PlayArtifactFound();
            }
            else
            {
                int damage = UnityEngine.Random.Range(10, 26);
                MessageDisplayNoUI.ShowMessage(c.hex, c, "The warding word fails.", Color.red);
                c.Wounded(null, damage);
            }
            return true;
        };
        base.Initialize(c, condition, effect, asyncEffect);
    }
}

