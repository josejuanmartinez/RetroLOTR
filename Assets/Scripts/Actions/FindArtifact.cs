using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class FindArtifact: MageAction
{
    public const string ActionRef = "FindArtifact";

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

    // Removes up to discardCount wrong options (never the correct one) and shuffles the rest.
    private static List<string> BuildDisplayOptions(Riddle riddle, int discardCount)
    {
        List<string> wrongOptions = riddle.options
            .Where(option => !string.Equals(option, riddle.answer, StringComparison.OrdinalIgnoreCase))
            .OrderBy(_ => UnityEngine.Random.value)
            .ToList();

        int toRemove = Mathf.Clamp(discardCount, 0, wrongOptions.Count);
        List<string> remainingWrong = wrongOptions.Skip(toRemove).ToList();

        List<string> display = new() { riddle.answer };
        display.AddRange(remainingWrong);
        return display.OrderBy(_ => UnityEngine.Random.value).ToList();
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
                MessageDisplayNoUI.ShowMessage(c.hex, c, $"No <sprite name=\"artifact\">artifact found", Color.red);
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
                Character.RefreshArtifactPcVisibilityForHex(c.hex);
                await ApplyAlignmentPenaltyIfNeeded();
                MessageDisplayNoUI.ShowMessage(c.hex, c, $"<sprite name=\"artifact\">artifact {artifact.GetHoverText()} found", Color.green);
                Sounds.Instance?.PlayArtifactFound();
                return true;
            }

            List<string> displayOptions = BuildDisplayOptions(riddle, c.GetMage() / 2);
            string answer = await SelectionDialog.Ask(riddle.prompt, "Speak", "Leave", displayOptions, isAI, SelectionDialog.Instance != null ? SelectionDialog.Instance.GetCharacterIllustration(c) : null);
            if (string.Equals(answer, riddle.answer, StringComparison.OrdinalIgnoreCase))
            {
                c.artifacts.Add(artifact);
                c.hex.hiddenArtifacts.Remove(artifact);
                c.hex.UpdateArtifactVisibility();
                Character.RefreshArtifactPcVisibilityForHex(c.hex);
                await ApplyAlignmentPenaltyIfNeeded();
                MessageDisplayNoUI.ShowMessage(c.hex, c, $"<sprite name=\"artifact\">artifact {artifact.GetHoverText()} claimed", Color.green);
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

