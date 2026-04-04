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
    private readonly Dictionary<string, CharacterAction> actionComponentsByClassName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ActionDefinition> actionDefinitionsByClassName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ActionDefinition> actionDefinitionsByActionName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, ActionDefinition> actionDefinitionsById = new();
    private readonly List<CharacterAction> availableActions = new();
    private Character currentCharacter;
    private Game cachedGame;

    public void Start()
    {
        LoadActionDefinitions();
        characterActions = Array.Empty<CharacterAction>();
        DEFAULT = ResolveActionByRef("Pass");

        currentCharacter = null;
        availableActions.Clear();
        UpdateInteractableState();
    }

    public T GetAction<T>() where T : CharacterAction
    {
        CharacterAction component = GetOrCreateAction(typeof(T));
        if (component != null) return component as T;
        Debug.LogWarning($"Action of type {typeof(T).Name} not found!");
        return null;
    }

    public CharacterAction ResolveActionByRef(string actionRef)
    {
        string normalizedActionRef = NormalizeActionRef(actionRef);
        if (string.IsNullOrWhiteSpace(normalizedActionRef)) return null;

        if (actionComponentsByClassName.TryGetValue(normalizedActionRef, out CharacterAction loaded))
        {
            return loaded;
        }

        if (actionDefinitionsByClassName.TryGetValue(normalizedActionRef, out ActionDefinition byClassDefinition))
        {
            CharacterAction createdFromClass = GetOrCreateAction(ResolveActionType(byClassDefinition.className), byClassDefinition);
            if (createdFromClass != null) return createdFromClass;
        }

        if (actionDefinitionsByActionName.TryGetValue(normalizedActionRef, out ActionDefinition byNameDefinition))
        {
            CharacterAction createdFromName = GetOrCreateAction(ResolveActionType(byNameDefinition.className), byNameDefinition);
            if (createdFromName != null) return createdFromName;
        }

        Type resolvedType = ResolveActionType(normalizedActionRef);
        return GetOrCreateAction(resolvedType);
    }

    public CharacterAction ResolveActionById(int actionId)
    {
        if (!actionDefinitionsById.TryGetValue(actionId, out ActionDefinition definition))
        {
            return null;
        }

        return ResolveActionByRef(definition.className);
    }

    public IReadOnlyList<CharacterAction> GetLoadedActions()
    {
        return actionComponents.Values.ToArray();
    }

    public void Refresh(Character character)
    {
        if (character == null)
        {
            Hide();
            return;
        }

        currentCharacter = character;

        if (IsHumanPlayerCharacter(character))
        {
            availableActions.Clear();
            UpdateInteractableState();
            return;
        }

        foreach (CharacterAction component in GetLoadedActions())
        {
            component.Initialize(character, null, null);
        }

        BuildAvailableActions();
        UpdateInteractableState();
    }

    public void Hide()
    {
        foreach (CharacterAction component in actionComponents.Values)
        {
            component.Reset();
        }

        availableActions.Clear();
        currentCharacter = null;
        UpdateInteractableState();
    }

    public int GetDefault()
    {
        return DEFAULT != null ? DEFAULT.actionId : 0;
    }

    public void RefreshInteractableState()
    {
        UpdateInteractableState();
    }

    private void BuildAvailableActions()
    {
        availableActions.Clear();
        if (currentCharacter == null) return;

        foreach (CharacterAction action in GetLoadedActions())
        {
            if (action == null) continue;
            if (!action.IsRoleEligible(currentCharacter)) continue;
            if (!action.FulfillsConditions()) continue;
            availableActions.Add(action);
        }

        availableActions.Sort((a, b) => string.Compare(a?.actionName, b?.actionName, StringComparison.OrdinalIgnoreCase));
    }
    private void ApplyDefinition(CharacterAction action, ActionDefinition definition)
    {
        if (action == null || definition == null) return;

        action.actionName = definition.actionName;
        action.description = definition.description;
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

    private void LoadActionDefinitions()
    {
        actionDefinitionsByClassName.Clear();
        actionDefinitionsByActionName.Clear();
        actionDefinitionsById.Clear();

        TextAsset json = Resources.Load<TextAsset>("Actions");
        if (json == null)
        {
            Debug.LogWarning("Actions.json not found in Resources.");
            return;
        }

        ActionDefinitionCollection definitionCollection = JsonUtility.FromJson<ActionDefinitionCollection>(json.text);
        if (definitionCollection == null || definitionCollection.actions == null || definitionCollection.actions.Count == 0)
        {
            Debug.LogWarning("Actions.json is empty or malformed.");
            return;
        }

        foreach (ActionDefinition definition in definitionCollection.actions)
        {
            if (definition == null) continue;

            if (!string.IsNullOrWhiteSpace(definition.className))
            {
                actionDefinitionsByClassName[definition.className.Trim()] = definition;
            }

            string normalizedActionName = NormalizeActionName(definition.actionName);
            if (!string.IsNullOrWhiteSpace(normalizedActionName))
            {
                actionDefinitionsByActionName[normalizedActionName] = definition;
            }

            actionDefinitionsById[definition.actionId] = definition;
        }
    }

    private void UpdateInteractableState()
    {
        if (canvasGroup == null) return;

        Game game = GetGame();
        bool isPlayerTurn = game != null && game.IsPlayerCurrentlyPlaying();
        bool popupBlocking = PopupManager.IsShowing;
        bool playerUsesCardHandUi = IsHumanPlayerCharacter(currentCharacter) || (currentCharacter == null && isPlayerTurn);
        bool visible = isPlayerTurn && !popupBlocking;
        bool enabled = visible && !playerUsesCardHandUi;
        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }

    private Game GetGame()
    {
        if (cachedGame == null) cachedGame = FindFirstObjectByType<Game>();
        return cachedGame;
    }

    private bool IsHumanPlayerCharacter(Character character)
    {
        Game game = GetGame();
        return character != null && game != null && game.player != null && character.GetOwner() == game.player;
    }

    private string NormalizeActionName(string value)
    {
        string stripped = ActionNameUtils.StripShortcut(value);
        return string.IsNullOrWhiteSpace(stripped) ? string.Empty : stripped.Trim().ToLowerInvariant();
    }

    private static string NormalizeActionRef(string actionRef)
    {
        if (string.IsNullOrWhiteSpace(actionRef)) return string.Empty;

        string normalized = actionRef.Trim();
        if (normalized.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^3];
        }

        return normalized.Trim();
    }

    private CharacterAction RegisterActionComponent(CharacterAction action)
    {
        if (action == null) return null;

        actionComponents[action.GetType()] = action;
        actionComponentsByClassName[action.GetType().Name] = action;

        ActionDefinition definition = null;
        actionDefinitionsByClassName.TryGetValue(action.GetType().Name, out definition);
        if (definition == null)
        {
            string normalizedActionName = NormalizeActionName(action.actionName);
            actionDefinitionsByActionName.TryGetValue(normalizedActionName, out definition);
        }

        ApplyDefinition(action, definition);
        characterActions = actionComponents.Values.ToArray();
        return action;
    }

    private CharacterAction GetOrCreateAction(Type actionType, ActionDefinition definition = null)
    {
        if (actionType == null || !typeof(CharacterAction).IsAssignableFrom(actionType)) return null;
        if (actionComponents.TryGetValue(actionType, out CharacterAction loaded)) return loaded;
        CharacterAction created = Activator.CreateInstance(actionType) as CharacterAction;

        if (definition == null)
        {
            actionDefinitionsByClassName.TryGetValue(actionType.Name, out definition);
        }

        ApplyDefinition(created, definition);
        return RegisterActionComponent(created);
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
