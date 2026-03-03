using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class ActionsManager : MonoBehaviour
{
    [Header("References")]
    public CanvasGroup canvasGroup;

    [HideInInspector]
    public CharacterAction DEFAULT;
    public CharacterAction[] characterActions;
    public static readonly char[] ActionHotkeyLetters = "BCEFGHIJKLMOQRTUVWYZ".ToCharArray();

    private readonly Dictionary<Type, CharacterAction> actionComponents = new();
    private readonly List<CharacterAction> availableActions = new();
    private Character currentCharacter;

    public void Start()
    {
        characterActions = LoadActionsFromJson();
        if (characterActions == null || characterActions.Length == 0)
        {
            characterActions = GetComponentsInChildren<CharacterAction>(true);
        }

        DEFAULT = characterActions.FirstOrDefault(a => string.Equals(a.GetType().Name, "Pass", StringComparison.OrdinalIgnoreCase));
        if (DEFAULT == null)
        {
            Debug.LogWarning("DEFAULT action not found. Expected action named 'Pass'.");
            DEFAULT = characterActions.FirstOrDefault(a => NormalizeActionName(a.actionName) == "pass") ?? characterActions.FirstOrDefault();
        }

        actionComponents.Clear();
        foreach (CharacterAction component in characterActions)
        {
            if (component == null) continue;
            actionComponents[component.GetType()] = component;
        }

        Hide();
    }

    public T GetAction<T>() where T : CharacterAction
    {
        if (actionComponents.TryGetValue(typeof(T), out CharacterAction component)) return component as T;
        Debug.LogWarning($"Action of type {typeof(T).Name} not found!");
        return null;
    }

    public void Refresh(Character character)
    {
        if (character == null)
        {
            Hide();
            return;
        }

        currentCharacter = character;

        foreach (CharacterAction component in actionComponents.Values)
        {
            component.Initialize(character, null, null);
        }

        BuildAvailableActions();
        UpdateInteractableState();
    }

    public void Hide()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        foreach (CharacterAction component in actionComponents.Values)
        {
            component.Reset();
        }

        availableActions.Clear();
        currentCharacter = null;
    }

    public int GetDefault()
    {
        return DEFAULT != null ? DEFAULT.actionId : 0;
    }

    public void RefreshInteractableState()
    {
        UpdateInteractableState();
    }

    public void GoToPreviousPage()
    {
    }

    public void GoToNextPage()
    {
    }

    public void PreviousPage()
    {
    }

    public void NextPage()
    {
    }

    public void ExecuteActionAtPageIndex(int index)
    {
        if (index < 0 || index >= ActionHotkeyLetters.Length) return;
        if (availableActions.Count == 0) return;
        if (index >= availableActions.Count) return;

        CharacterAction action = availableActions[index];
        if (action == null || !action.FulfillsConditions()) return;
        action.ExecuteFromButton();
    }

    public void ExecuteActionByHotkey(char letter)
    {
        int index = GetHotkeyIndex(letter);
        if (index < 0) return;
        ExecuteActionAtPageIndex(index);
    }

    private void BuildAvailableActions()
    {
        availableActions.Clear();
        if (characterActions == null || currentCharacter == null) return;

        foreach (CharacterAction action in characterActions)
        {
            if (action == null) continue;
            if (!action.IsRoleEligible(currentCharacter)) continue;
            if (!action.FulfillsConditions()) continue;
            availableActions.Add(action);
        }

        availableActions.Sort((a, b) => string.Compare(a?.actionName, b?.actionName, StringComparison.OrdinalIgnoreCase));
    }

    private CharacterAction[] LoadActionsFromJson()
    {
        TextAsset json = Resources.Load<TextAsset>("Actions");
        List<CharacterAction> prefabActions = GetComponentsInChildren<CharacterAction>(true).ToList();
        if (json == null)
        {
            Debug.LogWarning("Actions.json not found in Resources. Falling back to prefab actions.");
            return prefabActions.ToArray();
        }

        ActionDefinitionCollection definitionCollection = JsonUtility.FromJson<ActionDefinitionCollection>(json.text);
        if (definitionCollection == null || definitionCollection.actions == null || definitionCollection.actions.Count == 0)
        {
            Debug.LogWarning("Actions.json is empty or malformed. Falling back to prefab actions.");
            return prefabActions.ToArray();
        }

        if (prefabActions.Count == 0)
        {
            Debug.LogWarning("ActionsManager: no CharacterAction components found in scene/prefab. Attempting to create actions from Actions.json.");
        }

        return UpdateExistingActions(prefabActions, definitionCollection);
    }

    private CharacterAction[] UpdateExistingActions(List<CharacterAction> prefabActions, ActionDefinitionCollection definitionCollection)
    {
        Dictionary<string, CharacterAction> actionsByType = prefabActions
            .GroupBy(a => a.GetType().Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First());

        Dictionary<string, CharacterAction> actionsByName = prefabActions
            .GroupBy(a => NormalizeActionName(a.actionName))
            .ToDictionary(g => g.Key, g => g.First());

        List<CharacterAction> ordered = new();
        foreach (ActionDefinition definition in definitionCollection.actions)
        {
            CharacterAction action = null;
            if (!string.IsNullOrWhiteSpace(definition.className))
            {
                actionsByType.TryGetValue(definition.className, out action);
            }
            if (action == null)
            {
                string normalizedName = NormalizeActionName(definition.actionName);
                actionsByName.TryGetValue(normalizedName, out action);
            }
            if (action == null)
            {
                Type resolvedType = ResolveActionType(definition.className);
                if (resolvedType != null && typeof(CharacterAction).IsAssignableFrom(resolvedType))
                {
                    action = gameObject.AddComponent(resolvedType) as CharacterAction;
                    if (action != null)
                    {
                        if (!string.IsNullOrWhiteSpace(definition.className))
                        {
                            actionsByType[definition.className] = action;
                        }

                        string normalizedName = NormalizeActionName(definition.actionName);
                        if (!string.IsNullOrWhiteSpace(normalizedName))
                        {
                            actionsByName[normalizedName] = action;
                        }
                    }
                }

                if (action == null)
                {
                    Debug.LogWarning($"ActionsManager: action '{definition.className}'/'{definition.actionName}' is not present as a CharacterAction component.");
                    continue;
                }
            }

            ApplyDefinition(action, definition);
            ordered.Add(action);
        }

        foreach (CharacterAction leftover in prefabActions.Where(a => !ordered.Contains(a)))
        {
            ordered.Add(leftover);
        }

        if (ordered.Count == 0)
        {
            Debug.LogWarning("No actions matched the entries in Actions.json. Falling back to prefab ordering.");
            return prefabActions.ToArray();
        }

        return ordered.ToArray();
    }

    private void ApplyDefinition(CharacterAction action, ActionDefinition definition)
    {
        if (action == null || definition == null) return;

        action.actionName = definition.actionName;
        action.description = definition.description;
        action.gameObject.name = definition.actionName;
        action.actionId = definition.actionId;
        action.isBuyCaravans = definition.isBuyCaravans;
        action.isSellCaravans = definition.isSellCaravans;
        action.commanderXP = definition.commanderXP;
        action.agentXP = definition.agentXP;
        action.emmissaryXP = definition.emmissaryXP;
        action.mageXP = definition.mageXP;
        action.reward = definition.reward;
        action.advisorType = definition.advisorType;
    }

    private void UpdateInteractableState()
    {
        if (canvasGroup == null) return;

        Game game = FindFirstObjectByType<Game>();
        bool isPlayerTurn = game != null && game.IsPlayerCurrentlyPlaying();
        bool popupBlocking = PopupManager.IsShowing;

        bool enabled = isPlayerTurn && !popupBlocking;
        canvasGroup.alpha = enabled ? 1f : 0f;
        canvasGroup.interactable = enabled;
        canvasGroup.blocksRaycasts = enabled;
    }

    private int GetHotkeyIndex(char letter)
    {
        char normalized = char.ToUpperInvariant(letter);
        for (int i = 0; i < ActionHotkeyLetters.Length; i++)
        {
            if (ActionHotkeyLetters[i] == normalized) return i;
        }

        return -1;
    }

    private string NormalizeActionName(string value)
    {
        string stripped = ActionNameUtils.StripShortcut(value);
        return string.IsNullOrWhiteSpace(stripped) ? string.Empty : stripped.Trim().ToLowerInvariant();
    }

    private static Type ResolveActionType(string className)
    {
        if (string.IsNullOrWhiteSpace(className)) return null;

        Type direct = Type.GetType(className, false, true);
        if (direct != null) return direct;

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type candidate = assembly.GetType(className, false, true);
            if (candidate != null) return candidate;

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t != null).ToArray();
            }

            candidate = types.FirstOrDefault(t =>
                string.Equals(t.Name, className, StringComparison.OrdinalIgnoreCase));
            if (candidate != null) return candidate;
        }

        return null;
    }
}
