using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

[Serializable]
public class TutorialDefinition
{
    public int version;
    public List<TutorialFlow> tutorials = new();
}

[Serializable]
public class TutorialFlow
{
    public string leaderName;
    public string flowId;
    public List<TutorialStep> steps = new();
}

[Serializable]
public class TutorialStep
{
    public string stepId;
    public string title;
    public string type;
    public string targetLocation;
    public bool required = true;
    public string description;
    public string narration;
    public string actor1;
    public string actor2;
    public int grantSkillPoints = 0;
    public TutorialRequirements requirements;
    public TutorialRewards rewards;
}

[Serializable]
public class TutorialRequirements
{
    public string actionClass;
    public string targetLeader;
    public string targetCharacter;
    public string actorCharacter;
}

[Serializable]
public class TutorialRewards
{
    public List<string> unlockSkillNodes = new();
    public List<string> unlockRecruitmentTags = new();
    public List<string> grantArtifacts = new();
    public List<TutorialCharacterReward> grantCharacters = new();
}

[Serializable]
public class TutorialCharacterReward
{
    public string characterName;
    public int commander;
    public int agent;
    public int emmissary;
    public int mage;
    public int race = -1;
    public int sex = -1;
}

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    private TutorialDefinition definition;
    private TutorialFlow activeFlow;
    private int requiredStepIndex;
    private readonly HashSet<string> completedOptionalSteps = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, Artifact> artifactCatalog;
    private Dictionary<string, ActionDefinition> actionDefinitionsByClass;
    private Dictionary<string, ActionDefinition> actionDefinitionsByName;
    private PlayableLeader leader;
    private Coroutine pendingSkillPrompt;
    private readonly Dictionary<string, AiTutorialProgress> aiProgressByLeader = new(StringComparer.OrdinalIgnoreCase);

    private class AiTutorialProgress
    {
        public TutorialFlow flow;
        public int requiredIndex;
        public int attempts;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        LoadTutorialDefinition();
    }

    public void InitializeForLeader(PlayableLeader playableLeader)
    {
        if (playableLeader == null) return;
        leader = playableLeader;
        activeFlow = definition?.tutorials?.FirstOrDefault(t => string.Equals(t.leaderName, playableLeader.characterName, StringComparison.OrdinalIgnoreCase));
        requiredStepIndex = 0;
        completedOptionalSteps.Clear();
        BuildArtifactCatalog();
        UITutorialObjectivesManager.Instance?.ClearObjectives();
        ActivateCurrentRequiredStep();
    }

    public bool IsActiveFor(Character queryLeader)
    {
        return activeFlow != null && leader == queryLeader && !IsCompleted();
    }

    public void HandleActionExecuted(Character actor, string actionClassName, Hex actionHex)
    {
        if (actor == null || actionHex == null) return;
        if (!IsActiveFor(actor.GetOwner())) return;

        TutorialStep step = GetCurrentRequiredStep();
        if (step != null && StepMatchesAction(step, actor, actionClassName, actionHex))
        {
            CompleteStep(step, actor);
            AdvanceRequiredStep();
            return;
        }

        if (activeFlow?.steps == null) return;
        foreach (TutorialStep optional in activeFlow.steps.Where(s => s != null && !s.required && !completedOptionalSteps.Contains(s.stepId)))
        {
            if (StepMatchesAction(optional, actor, actionClassName, actionHex))
            {
                CompleteStep(optional, actor);
                completedOptionalSteps.Add(optional.stepId);
                break;
            }
        }
    }

    public void HandleCharacterArrived(Character actor, Hex newHex)
    {
        if (actor == null || newHex == null) return;
        if (!IsActiveFor(actor.GetOwner())) return;

        TutorialStep step = GetCurrentRequiredStep();
        if (step != null && StepMatchesTravel(step, newHex))
        {
            CompleteStep(step, actor);
            AdvanceRequiredStep();
            return;
        }

        if (activeFlow?.steps == null) return;
        foreach (TutorialStep optional in activeFlow.steps.Where(s => s != null && !s.required && !completedOptionalSteps.Contains(s.stepId)))
        {
            if (StepMatchesTravel(optional, newHex))
            {
                CompleteStep(optional, actor);
                completedOptionalSteps.Add(optional.stepId);
                break;
            }
        }
    }

    private bool IsCompleted()
    {
        return activeFlow == null || GetRequiredSteps().Count <= requiredStepIndex;
    }

    private TutorialStep GetCurrentRequiredStep()
    {
        List<TutorialStep> requiredSteps = GetRequiredSteps();
        return requiredStepIndex >= 0 && requiredStepIndex < requiredSteps.Count ? requiredSteps[requiredStepIndex] : null;
    }

    private List<TutorialStep> GetRequiredSteps()
    {
        return activeFlow?.steps?.Where(s => s != null && s.required).ToList() ?? new List<TutorialStep>();
    }

    private static List<TutorialStep> GetRequiredSteps(TutorialFlow flow)
    {
        return flow?.steps?.Where(s => s != null && s.required).ToList() ?? new List<TutorialStep>();
    }

    private void AdvanceRequiredStep()
    {
        requiredStepIndex++;
        ActivateCurrentRequiredStep();
    }

    private void ActivateCurrentRequiredStep()
    {
        TutorialStep step = GetCurrentRequiredStep();
        if (step == null || leader == null) return;
        ShowStepPopup(step);
        UpdateObjectiveDisplay(step);
    }

    public void RefreshObjectiveUI()
    {
        UITutorialObjectivesManager.Instance?.ClearObjectives();
        TutorialStep step = GetCurrentRequiredStep();
        if (step != null)
        {
            UpdateObjectiveDisplay(step);
        }
    }

    private void ShowStepPopup(TutorialStep step)
    {
        if (step == null || leader == null) return;
        Illustrations illustrations = FindFirstObjectByType<Illustrations>();
        Sprite actor1 = illustrations != null ? illustrations.GetIllustrationByName(step.actor1) : null;
        Sprite actor2 = illustrations != null ? illustrations.GetIllustrationByName(step.actor2) : null;
        string baseTitle = string.IsNullOrWhiteSpace(step.title) ? "Tutorial" : step.title;
        List<TutorialStep> requiredSteps = GetRequiredSteps();
        int stepNumber = requiredStepIndex + 1;
        int totalSteps = requiredSteps.Count;
        //string title = totalSteps > 0 ? $"{baseTitle} ({stepNumber}/{totalSteps})" : baseTitle;
        string title = baseTitle;
        string text = string.IsNullOrWhiteSpace(step.narration) ? step.description : step.narration;
        PopupManager.Show(title, actor1, actor2, text, true);
    }

    private void ScheduleSkillUnlockPrompt(TutorialStep step)
    {
        if (pendingSkillPrompt != null)
        {
            StopCoroutine(pendingSkillPrompt);
            pendingSkillPrompt = null;
        }

        if (step == null || leader == null) return;
        if (leader.GetSkillPoints() <= 0) return;

        pendingSkillPrompt = StartCoroutine(WaitForPopupThenPrompt(step));
    }

    private IEnumerator WaitForPopupThenPrompt(TutorialStep step)
    {
        while (PopupManager.IsShowing)
        {
            yield return null;
        }

        if (step == null || leader == null) yield break;
        if (!IsActiveFor(leader)) yield break;
        if (leader.GetSkillPoints() <= 0) yield break;

        List<string> options = BuildUnlockOptionsFromAllNodes();
        if (options.Count == 0) yield break;

        string message = $"You gained a skill point. Choose a skill to unlock ({leader.GetSkillPoints()} available).";
        var selectionTask = SelectionDialog.Ask(message, "Unlock", string.Empty, options, false, SelectionDialog.Instance != null ? SelectionDialog.Instance.GetCharacterIllustration(leader) : null);
        while (!selectionTask.IsCompleted)
        {
            yield return null;
        }

        string selection = selectionTask.Result;
        if (string.IsNullOrWhiteSpace(selection)) yield break;
        if (!TryResolveNodeId(selection, options, out string nodeId)) yield break;

        if (leader.UnlockSkillNode(nodeId))
        {
            string nodeName = GetNodeDisplayName(nodeId);
            MessageDisplay.ShowMessage($"{leader.characterName} unlocked {nodeName}.", Color.green);
            ShowSkillUnlockDialog(nodeId);
        }
    }

    private List<string> BuildUnlockOptionsFromAllNodes()
    {
        List<string> options = new();
        if (leader == null) return options;
        SkillTreeDefinition tree = SkillTreeService.GetDefinition();
        if (tree?.nodes == null) return options;

        foreach (SkillTreeNode node in tree.nodes)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.id)) continue;
            if (node.cost <= 0) continue;
            if (leader.IsSkillNodeUnlocked(node.id)) continue;
            if (!SkillTreeService.CanUnlockNode(leader, node.id)) continue;
            string displayName = !string.IsNullOrWhiteSpace(node.name) ? node.name : node.id;
            options.Add($"{displayName} ({node.id})");
        }

        return options;
    }

    private bool TryResolveNodeId(string selection, List<string> options, out string nodeId)
    {
        nodeId = null;
        if (string.IsNullOrWhiteSpace(selection)) return false;
        int start = selection.LastIndexOf('(');
        int end = selection.LastIndexOf(')');
        if (start >= 0 && end > start)
        {
            nodeId = selection.Substring(start + 1, end - start - 1);
            return !string.IsNullOrWhiteSpace(nodeId);
        }
        if (options != null && options.Contains(selection)) return false;
        return false;
    }

    private static string GetNodeDisplayName(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId)) return string.Empty;
        SkillTreeDefinition tree = SkillTreeService.GetDefinition();
        if (tree?.nodes == null) return nodeId;
        SkillTreeNode node = tree.nodes.FirstOrDefault(x => x != null && string.Equals(x.id, nodeId, StringComparison.OrdinalIgnoreCase));
        return node != null && !string.IsNullOrWhiteSpace(node.name) ? node.name : nodeId;
    }

    private void ShowSkillUnlockDialog(string nodeId)
    {
        if (leader == null || string.IsNullOrWhiteSpace(nodeId)) return;
        Game game = FindFirstObjectByType<Game>();
        if (game == null || game.player != leader) return;

        string message = BuildSkillUnlockHelpMessage(nodeId);
        if (string.IsNullOrWhiteSpace(message)) return;
        ConfirmationDialog.AskOk(message);
    }

    private string BuildSkillUnlockHelpMessage(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId)) return string.Empty;

        SkillTreeDefinition tree = SkillTreeService.GetDefinition();
        SkillTreeNode node = tree?.nodes?.FirstOrDefault(x => x != null && string.Equals(x.id, nodeId, StringComparison.OrdinalIgnoreCase));
        string nodeName = GetNodeDisplayName(nodeId);
        List<string> lines = new() { $"{nodeName} learned." };

        if (node?.unlocksActions == null || node.unlocksActions.Count == 0)
        {
            return string.Join("\n", lines);
        }

        EnsureActionDefinitionsLoaded();

        foreach (string actionClass in node.unlocksActions)
        {
            if (string.IsNullOrWhiteSpace(actionClass)) continue;
            ActionDefinition definition = GetActionDefinition(actionClass);
            string actionName = definition != null && !string.IsNullOrWhiteSpace(definition.actionName) ? definition.actionName : actionClass;
            string info = definition != null && !string.IsNullOrWhiteSpace(definition.tutorialInfo)
                ? definition.tutorialInfo
                : definition?.description;

            if (string.IsNullOrWhiteSpace(info))
            {
                lines.Add($"- {actionName}");
            }
            else
            {
                lines.Add($"- {actionName}: {info}");
            }
        }

        return string.Join("\n", lines);
    }

    private void EnsureActionDefinitionsLoaded()
    {
        if (actionDefinitionsByClass != null && actionDefinitionsByName != null) return;

        actionDefinitionsByClass = new Dictionary<string, ActionDefinition>(StringComparer.OrdinalIgnoreCase);
        actionDefinitionsByName = new Dictionary<string, ActionDefinition>(StringComparer.OrdinalIgnoreCase);

        TextAsset json = Resources.Load<TextAsset>("Actions");
        if (json == null) return;

        ActionDefinitionCollection collection = JsonUtility.FromJson<ActionDefinitionCollection>(json.text);
        if (collection?.actions == null) return;

        foreach (ActionDefinition action in collection.actions)
        {
            if (action == null) continue;
            if (!string.IsNullOrWhiteSpace(action.className) && !actionDefinitionsByClass.ContainsKey(action.className))
            {
                actionDefinitionsByClass[action.className] = action;
            }
            if (!string.IsNullOrWhiteSpace(action.actionName))
            {
                string key = NormalizeActionName(action.actionName);
                if (!actionDefinitionsByName.ContainsKey(key))
                {
                    actionDefinitionsByName[key] = action;
                }
            }
        }
    }

    private ActionDefinition GetActionDefinition(string actionClassOrName)
    {
        if (string.IsNullOrWhiteSpace(actionClassOrName)) return null;
        if (actionDefinitionsByClass != null && actionDefinitionsByClass.TryGetValue(actionClassOrName, out ActionDefinition byClass))
        {
            return byClass;
        }
        if (actionDefinitionsByName != null)
        {
            string key = NormalizeActionName(actionClassOrName);
            return actionDefinitionsByName.TryGetValue(key, out ActionDefinition byName) ? byName : null;
        }
        return null;
    }

    private static string NormalizeActionName(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        string stripped = ActionNameUtils.StripShortcut(value);
        return string.IsNullOrWhiteSpace(stripped) ? string.Empty : stripped.Trim().ToLowerInvariant();
    }

    private bool StepMatchesAction(TutorialStep step, Character actor, string actionClassName, Hex actionHex)
    {
        if (step == null || string.IsNullOrWhiteSpace(step.type)) return false;
        if (!string.Equals(step.type, "performAction", StringComparison.OrdinalIgnoreCase)) return false;

        TutorialRequirements req = step.requirements;
        if (req != null && !string.IsNullOrWhiteSpace(req.actorCharacter))
        {
            if (actor == null || !string.Equals(actor.characterName, req.actorCharacter, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }
        if (req != null && !string.IsNullOrWhiteSpace(req.actionClass))
        {
            if (!string.Equals(req.actionClass, actionClassName, StringComparison.OrdinalIgnoreCase)) return false;
        }

        if (req != null && !string.IsNullOrWhiteSpace(req.targetLeader))
        {
            PC pc = actionHex.GetPCData();
            string ownerName = pc != null && pc.owner != null ? pc.owner.characterName : string.Empty;
            string pcName = pc != null ? pc.pcName : string.Empty;
            bool matchesOwner = !string.IsNullOrWhiteSpace(ownerName)
                && string.Equals(ownerName, req.targetLeader, StringComparison.OrdinalIgnoreCase);
            bool matchesPc = !string.IsNullOrWhiteSpace(pcName)
                && string.Equals(pcName, req.targetLeader, StringComparison.OrdinalIgnoreCase);
            bool matchesCharacter = actionHex.characters.Any(c => c != null
                && string.Equals(c.characterName, req.targetLeader, StringComparison.OrdinalIgnoreCase));
            if (!matchesOwner && !matchesPc && !matchesCharacter)
            {
                bool isAllegiance = string.Equals(actionClassName, "StateAllegiance", StringComparison.OrdinalIgnoreCase);
                bool matchesLocation = !string.IsNullOrWhiteSpace(step.targetLocation)
                    && !string.IsNullOrWhiteSpace(pcName)
                    && string.Equals(pcName, step.targetLocation, StringComparison.OrdinalIgnoreCase);
                if (!(isAllegiance && matchesLocation && pc != null && pc.owner == null))
                {
                    return false;
                }
            }
        }

        if (req != null && !string.IsNullOrWhiteSpace(req.targetCharacter))
        {
            bool found = actionHex.characters.Any(c => c != null && string.Equals(c.characterName, req.targetCharacter, StringComparison.OrdinalIgnoreCase));
            if (!found) return false;
        }

        if (!string.IsNullOrWhiteSpace(step.targetLocation))
        {
            PC pc = actionHex.GetPCData();
            if (pc == null || !string.Equals(pc.pcName, step.targetLocation, StringComparison.OrdinalIgnoreCase)) return false;
        }

        return true;
    }

    private bool StepMatchesTravel(TutorialStep step, Hex newHex)
    {
        if (step == null || string.IsNullOrWhiteSpace(step.type)) return false;
        if (!string.Equals(step.type, "travel", StringComparison.OrdinalIgnoreCase)) return false;
        if (string.IsNullOrWhiteSpace(step.targetLocation)) return false;
        PC pc = newHex.GetPCData();
        return pc != null && string.Equals(pc.pcName, step.targetLocation, StringComparison.OrdinalIgnoreCase);
    }

    private void CompleteStep(TutorialStep step, Character actor)
    {
        if (step == null) return;
        if (step.grantSkillPoints > 0 && leader != null)
        {
            leader.AddSkillPoints(step.grantSkillPoints);
        }
        ApplyRewards(step, actor);
        ScheduleSkillUnlockPrompt(step);
        RemoveObjectiveDisplay(step);
    }

    private void UpdateObjectiveDisplay(TutorialStep step)
    {
        if (step == null) return;
        string id = step.stepId;
        if (string.IsNullOrWhiteSpace(id)) return;
        string text = GetObjectiveText(step);
        if (string.IsNullOrWhiteSpace(text)) return;
        UITutorialObjectivesManager.Instance?.AddObjective(id, text);
    }

    private void RemoveObjectiveDisplay(TutorialStep step)
    {
        if (step == null) return;
        string id = step.stepId;
        if (string.IsNullOrWhiteSpace(id)) return;
        UITutorialObjectivesManager.Instance?.RemoveObjective(id);
    }

    private static string GetObjectiveText(TutorialStep step)
    {
        if (step == null) return string.Empty;
        if (!string.IsNullOrWhiteSpace(step.description)) return step.description;
        if (!string.IsNullOrWhiteSpace(step.narration)) return step.narration;
        if (!string.IsNullOrWhiteSpace(step.title)) return step.title;
        return string.Empty;
    }

    private void ApplyRewards(TutorialStep step, Character actor)
    {
        TutorialRewards rewards = step.rewards;
        if (rewards == null || actor == null) return;
        Character recipient = actor;

        if (rewards.unlockSkillNodes != null)
        {
            foreach (string nodeId in rewards.unlockSkillNodes)
            {
                recipient.UnlockSkillNode(nodeId, ignoreRequirements: true);
            }
        }

        if (rewards.grantArtifacts != null)
        {
            foreach (string artifactName in rewards.grantArtifacts)
            {
                Artifact artifact = GetArtifactByName(artifactName);
                if (artifact == null) continue;
                actor.artifacts.Add(CloneArtifact(artifact));
                MessageDisplayNoUI.ShowMessage(actor.hex, actor, $"{actor.characterName} received {artifact.artifactName}", Color.green);
            }
        }

        if (rewards.grantCharacters != null && rewards.grantCharacters.Count > 0)
        {
            GrantCharacters(rewards.grantCharacters, actor);
        }
    }

    private void GrantCharacters(List<TutorialCharacterReward> grants, Character actor)
    {
        if (grants == null || actor == null) return;
        Leader owner = actor.GetOwner();
        if (owner == null) return;

        CharacterInstantiator instantiator = FindFirstObjectByType<CharacterInstantiator>();
        if (instantiator == null) return;

        for (int i = 0; i < grants.Count; i++)
        {
            TutorialCharacterReward grant = grants[i];
            if (grant == null || string.IsNullOrWhiteSpace(grant.characterName)) continue;

            Character existing = FindCharacterByName(grant.characterName);
            if (existing != null && existing is not Leader)
            {
                TransferCharacter(existing, owner, actor.hex);
                continue;
            }

            BiomeConfig config = new()
            {
                characterName = grant.characterName,
                alignment = owner.GetAlignment(),
                race = grant.race >= 0 ? (RacesEnum)grant.race : owner.GetBiome().race,
                sex = grant.sex >= 0 ? (SexEnum)grant.sex : owner.GetBiome().sex,
                commander = grant.commander,
                agent = grant.agent,
                emmissary = grant.emmissary,
                mage = grant.mage
            };

            Character newCharacter = instantiator.InstantiateCharacter(owner, actor.hex, config);
            if (newCharacter == null) continue;
            newCharacter.startingCharacter = false;
            newCharacter.hasActionedThisTurn = true;
            MessageDisplayNoUI.ShowMessage(actor.hex, actor, $"{grant.characterName} joins your service.", Color.green);
        }
    }

    private static Character FindCharacterByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        Character[] characters = FindObjectsByType<Character>(FindObjectsSortMode.None);
        return characters.FirstOrDefault(c => c != null && string.Equals(c.characterName, name, StringComparison.OrdinalIgnoreCase));
    }

    private static void TransferCharacter(Character character, Leader newOwner, Hex destination)
    {
        if (character == null || newOwner == null || destination == null) return;
        Leader oldOwner = character.GetOwner();
        if (oldOwner != null && oldOwner.controlledCharacters.Contains(character))
        {
            oldOwner.controlledCharacters.Remove(character);
        }

        if (!newOwner.controlledCharacters.Contains(character))
        {
            newOwner.controlledCharacters.Add(character);
        }

        Hex oldHex = character.hex;
        if (oldHex != null && oldHex.characters.Contains(character))
        {
            oldHex.characters.Remove(character);
            oldHex.RedrawCharacters();
            oldHex.RedrawArmies();
        }

        character.owner = newOwner;
        character.alignment = newOwner.GetAlignment();
        character.startingCharacter = false;
        character.hasActionedThisTurn = true;
        character.hex = destination;
        destination.characters.Add(character);
        destination.RedrawCharacters();
        destination.RedrawArmies();
        MessageDisplayNoUI.ShowMessage(destination, character, $"{character.characterName} joins your service.", Color.green);
        CharacterIcons.RefreshForHumanPlayerOf(newOwner);
    }

    public bool HasTutorialForLeader(PlayableLeader playableLeader)
    {
        return GetAiProgress(playableLeader) != null;
    }

    public bool IsAiTutorialComplete(PlayableLeader playableLeader)
    {
        AiTutorialProgress progress = GetAiProgress(playableLeader);
        if (progress == null) return true;
        List<TutorialStep> steps = GetRequiredSteps(progress.flow);
        return progress.requiredIndex >= steps.Count;
    }

    public IEnumerator RunAiTutorialTurn(PlayableLeader playableLeader)
    {
        AiTutorialProgress progress = GetAiProgress(playableLeader);
        if (progress == null) yield break;

        List<TutorialStep> requiredSteps = GetRequiredSteps(progress.flow);
        if (progress.requiredIndex < 0 || progress.requiredIndex >= requiredSteps.Count) yield break;

        TutorialStep step = requiredSteps[progress.requiredIndex];
        if (step == null)
        {
            progress.requiredIndex++;
            progress.attempts = 0;
            yield break;
        }

        Character actor = ResolveStepActor(playableLeader, step) ?? playableLeader;
        bool completed = false;

        if (string.Equals(step.type, "travel", StringComparison.OrdinalIgnoreCase))
        {
            completed = TryHandleTravelStep(actor, step, progress.attempts >= 2);
        }
        else if (string.Equals(step.type, "performAction", StringComparison.OrdinalIgnoreCase))
        {
            IEnumerator runner = TryHandleActionStep(actor, step, progress.attempts >= 2, value => completed = value);
            while (runner.MoveNext()) yield return runner.Current;
        }
        else
        {
            completed = true;
        }

        if (completed)
        {
            ApplyAiStepRewards(step, actor);
            progress.requiredIndex++;
            progress.attempts = 0;
        }
        else
        {
            progress.attempts++;
            if (progress.attempts >= 3)
            {
                ForceCompleteStep(step, actor);
                progress.requiredIndex++;
                progress.attempts = 0;
            }
        }
    }

    private AiTutorialProgress GetAiProgress(PlayableLeader playableLeader)
    {
        if (playableLeader == null) return null;
        if (aiProgressByLeader.TryGetValue(playableLeader.characterName, out AiTutorialProgress existing))
        {
            return existing;
        }

        TutorialFlow flow = definition?.tutorials?.FirstOrDefault(t => string.Equals(t.leaderName, playableLeader.characterName, StringComparison.OrdinalIgnoreCase));
        if (flow == null) return null;

        AiTutorialProgress progress = new()
        {
            flow = flow,
            requiredIndex = 0,
            attempts = 0
        };
        aiProgressByLeader[playableLeader.characterName] = progress;
        return progress;
    }

    private static Character ResolveStepActor(PlayableLeader leader, TutorialStep step)
    {
        if (leader == null || step == null) return null;
        string actorName = step.requirements != null && !string.IsNullOrWhiteSpace(step.requirements.actorCharacter)
            ? step.requirements.actorCharacter
            : step.actor1;

        if (!string.IsNullOrWhiteSpace(actorName))
        {
            Character owned = leader.controlledCharacters.FirstOrDefault(c => c != null && string.Equals(c.characterName, actorName, StringComparison.OrdinalIgnoreCase));
            if (owned != null) return owned;

            Character existing = FindCharacterByName(actorName);
            if (existing != null)
            {
                TransferCharacter(existing, leader, leader.hex);
                return existing;
            }
        }

        return leader;
    }

    private static Hex ResolveTargetHex(TutorialStep step)
    {
        if (step == null) return null;
        Board board = FindFirstObjectByType<Board>();
        if (board?.hexes == null) return null;

        if (!string.IsNullOrWhiteSpace(step.targetLocation))
        {
            foreach (Hex hex in board.hexes.Values)
            {
                if (hex == null) continue;
                PC pc = hex.GetPCData();
                if (pc != null && string.Equals(pc.pcName, step.targetLocation, StringComparison.OrdinalIgnoreCase)) return hex;
            }
        }

        if (step.requirements != null && !string.IsNullOrWhiteSpace(step.requirements.targetCharacter))
        {
            Character target = FindCharacterByName(step.requirements.targetCharacter);
            if (target?.hex != null) return target.hex;
        }

        if (step.requirements != null && !string.IsNullOrWhiteSpace(step.requirements.targetLeader))
        {
            foreach (Hex hex in board.hexes.Values)
            {
                if (hex == null) continue;
                PC pc = hex.GetPCData();
                if (pc?.owner != null && string.Equals(pc.owner.characterName, step.requirements.targetLeader, StringComparison.OrdinalIgnoreCase))
                {
                    return hex;
                }
                if (pc?.owner == null && pc != null && string.Equals(pc.pcName, step.requirements.targetLeader, StringComparison.OrdinalIgnoreCase))
                {
                    return hex;
                }
            }
        }

        return null;
    }

    private static bool TryHandleTravelStep(Character actor, TutorialStep step, bool forceTeleport)
    {
        if (actor == null || step == null) return false;
        Hex targetHex = ResolveTargetHex(step);
        if (targetHex == null) return false;

        if (actor.hex == targetHex) return true;

        if (forceTeleport)
        {
            TeleportCharacterToHex(actor, targetHex);
            return true;
        }

        return MoveActorTowards(actor, targetHex);
    }

    private IEnumerator TryHandleActionStep(Character actor, TutorialStep step, bool forceComplete, System.Action<bool> onComplete)
    {
        bool completed = false;
        if (actor == null || step == null)
        {
            onComplete?.Invoke(false);
            yield break;
        }

        Hex targetHex = ResolveTargetHex(step);
        if (targetHex != null && actor.hex != targetHex)
        {
            if (forceComplete)
            {
                TeleportCharacterToHex(actor, targetHex);
            }
            else if (!MoveActorTowards(actor, targetHex))
            {
                onComplete?.Invoke(false);
                yield break;
            }
        }

        if (step.requirements != null && !string.IsNullOrWhiteSpace(step.requirements.targetCharacter))
        {
            Character targetCharacter = FindCharacterByName(step.requirements.targetCharacter);
            if (targetCharacter != null && targetCharacter.hex != actor.hex)
            {
                if (forceComplete)
                {
                    TeleportCharacterToHex(targetCharacter, actor.hex);
                }
                else
                {
                    onComplete?.Invoke(false);
                    yield break;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(step.requirements?.actionClass))
        {
            completed = true;
        }
        else
        {
            ActionsManager actionsManager = FindFirstObjectByType<ActionsManager>();
            if (actionsManager != null)
            {
                actionsManager.Refresh(actor);
                CharacterAction action = actionsManager.characterActions
                    .FirstOrDefault(a => a != null && string.Equals(a.GetType().Name, step.requirements.actionClass, StringComparison.OrdinalIgnoreCase));
                if (action != null)
                {
                    Task task = action.Execute();
                    while (!task.IsCompleted) yield return null;
                    if (task.IsFaulted && task.Exception != null) Debug.LogException(task.Exception);
                    completed = action.LastExecutionSucceeded;
                }
            }
        }

        onComplete?.Invoke(completed);
    }

    private void ApplyAiStepRewards(TutorialStep step, Character actor)
    {
        if (step == null || actor == null) return;
        if (step.grantSkillPoints > 0)
        {
            actor.AddSkillPoints(step.grantSkillPoints);
        }
        ApplyRewards(step, actor);
    }

    private void ForceCompleteStep(TutorialStep step, Character actor)
    {
        if (step == null || actor == null) return;

        Hex targetHex = ResolveTargetHex(step);
        if (targetHex != null && actor.hex != targetHex)
        {
            TeleportCharacterToHex(actor, targetHex);
        }

        if (step.requirements != null && !string.IsNullOrWhiteSpace(step.requirements.targetCharacter))
        {
            Character targetCharacter = FindCharacterByName(step.requirements.targetCharacter);
            if (targetCharacter != null && targetCharacter.hex != actor.hex)
            {
                TeleportCharacterToHex(targetCharacter, actor.hex);
            }
        }

        if (string.Equals(step.requirements?.actionClass, "FindArtifact", StringComparison.OrdinalIgnoreCase))
        {
            if (actor.hex != null && actor.hex.hiddenArtifacts.Count > 0 && actor.artifacts.Count < Character.MAX_ARTIFACTS)
            {
                Artifact artifact = actor.hex.hiddenArtifacts[0];
                actor.artifacts.Add(artifact);
                actor.hex.hiddenArtifacts.RemoveAt(0);
                actor.hex.UpdateArtifactVisibility();
            }
        }

        ApplyAiStepRewards(step, actor);
    }

    private static bool MoveActorTowards(Character actor, Hex targetHex)
    {
        if (actor == null || actor.hex == null || targetHex == null) return false;
        if (actor.hex == targetHex) return true;

        HexPathRenderer pathRenderer = FindFirstObjectByType<HexPathRenderer>();
        Board board = FindFirstObjectByType<Board>();
        if (pathRenderer == null || board == null) return false;

        List<Vector2Int> path = pathRenderer.FindPath(actor.hex.v2, targetHex.v2, actor);
        if (path == null || path.Count < 2) return false;

        int steps = Mathf.Min(path.Count - 1, actor.GetMaxMovement());
        Hex current = actor.hex;
        for (int i = 1; i <= steps; i++)
        {
            Hex next = board.GetHex(path[i]);
            if (next == null) break;
            board.MoveCharacterOneHex(actor, current, next, false, false);
            current = next;
        }

        return actor.hex == targetHex;
    }

    private static void TeleportCharacterToHex(Character actor, Hex targetHex)
    {
        if (actor == null || targetHex == null) return;
        Hex oldHex = actor.hex;
        if (oldHex != null)
        {
            oldHex.characters.Remove(actor);
            if (actor.IsArmyCommander())
            {
                oldHex.armies.Remove(actor.GetArmy());
            }
            oldHex.RedrawCharacters();
            oldHex.RedrawArmies();
        }

        actor.hex = targetHex;
        if (!targetHex.characters.Contains(actor)) targetHex.characters.Add(actor);
        if (actor.IsArmyCommander() && !targetHex.armies.Contains(actor.GetArmy()))
        {
            targetHex.armies.Add(actor.GetArmy());
        }

        targetHex.RedrawCharacters();
        targetHex.RedrawArmies();
    }

    private void LoadTutorialDefinition()
    {
        TextAsset json = Resources.Load<TextAsset>("Tutorial");
        definition = json != null ? JsonUtility.FromJson<TutorialDefinition>(json.text) : null;
    }

    private void BuildArtifactCatalog()
    {
        artifactCatalog = new Dictionary<string, Artifact>(StringComparer.OrdinalIgnoreCase);

        PlayableLeaders playable = FindFirstObjectByType<PlayableLeaders>();
        if (playable?.playableLeaders?.biomes != null)
        {
            foreach (LeaderBiomeConfig biome in playable.playableLeaders.biomes)
            {
                if (biome == null || biome.tutorialArtifacts == null) continue;
                foreach (Artifact artifact in biome.tutorialArtifacts)
                {
                    if (artifact == null || string.IsNullOrWhiteSpace(artifact.artifactName)) continue;
                    if (!artifactCatalog.ContainsKey(artifact.artifactName))
                    {
                        artifactCatalog[artifact.artifactName] = artifact;
                    }
                }
            }
        }
    }

    private Artifact GetArtifactByName(string artifactName)
    {
        if (string.IsNullOrWhiteSpace(artifactName) || artifactCatalog == null) return null;
        return artifactCatalog.TryGetValue(artifactName, out Artifact artifact) ? artifact : null;
    }

    private static Artifact CloneArtifact(Artifact source)
    {
        if (source == null) return null;
        return new Artifact
        {
            artifactName = source.artifactName,
            artifactDescription = source.artifactDescription,
            hidden = source.hidden,
            alignment = source.alignment,
            providesSpell = source.providesSpell,
            commanderBonus = source.commanderBonus,
            agentBonus = source.agentBonus,
            emmissaryBonus = source.emmissaryBonus,
            mageBonus = source.mageBonus,
            bonusAttack = source.bonusAttack,
            bonusDefense = source.bonusDefense,
            oneShot = source.oneShot,
            transferable = source.transferable,
            spriteString = source.spriteString
        };
    }
}
