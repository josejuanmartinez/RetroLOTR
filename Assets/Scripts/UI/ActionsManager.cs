using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ActionsManager : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject actionButtonPrefab;
    public GameObject hoverPrefab;

    [HideInInspector]
    public CharacterAction DEFAULT;
    public CharacterAction[] characterActions;
    // Dictionary to store all the action components
    private Dictionary<Type, CharacterAction> actionComponents = new ();
    private CanvasGroup canvasGroup;

    public void Start()
    {
        canvasGroup = GetComponent<CanvasGroup>();

        // Load definitions from json and sync them with instantiated buttons
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

        // Store each component by its type for easy access
        foreach (CharacterAction component in characterActions)
        {
            // Debug.Log($"Registering action {component.actionName}");
            Type componentType = component.GetType();
            actionComponents[componentType] = component;
        }
        Hide();
    }

    // Generic method to get a specific action component
    public T GetAction<T>() where T : CharacterAction
    {
        if (actionComponents.TryGetValue(typeof(T), out CharacterAction component)) return component as T;

        Debug.LogWarning($"Action of type {typeof(T).Name} not found!");
        return null;
    }

    public void Refresh(Character character)
    {
        actionComponents.Values.ToList().ForEach(component => component.Initialize(character, null, null));
        UpdateInteractableState();
    }

    public void Hide()
    {
        actionComponents.Values.ToList().ForEach(component => component.Reset());
        UpdateInteractableState();
    }

    public int GetDefault()
    {
        return DEFAULT.actionId;
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

    private CharacterAction[] LoadActionsFromJson()
    {
        TextAsset json = Resources.Load<TextAsset>("Actions");
        // Include inactive children because action buttons may start disabled on the prefab
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

        // If we already have action children, map and update them; otherwise instantiate from definitions
        if (prefabActions.Count > 0)
        {
            return UpdateExistingActions(prefabActions, definitionCollection);
        }

        return CreateActionsFromDefinitions(definitionCollection);
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
                Debug.LogWarning($"Action '{definition.className}' in Actions.json does not exist in Actions.prefab");
                continue;
            }

            WireUiReferences(action.gameObject, action);
            ApplyDefinition(action, definition);
            ordered.Add(action);
        }

        // Keep any prefab actions that were not present in the json at the end of the array
        foreach (CharacterAction leftover in prefabActions.Where(a => !ordered.Contains(a)))
        {
            WireUiReferences(leftover.gameObject, leftover);
            ordered.Add(leftover);
        }

        if (ordered.Count == 0)
        {
            Debug.LogWarning("No actions matched the entries in Actions.json. Falling back to prefab ordering.");
            return prefabActions.ToArray();
        }

        return ordered.ToArray();
    }

    private CharacterAction[] CreateActionsFromDefinitions(ActionDefinitionCollection definitionCollection)
    {
        List<CharacterAction> created = new();

        foreach (ActionDefinition definition in definitionCollection.actions)
        {
            Type actionType = ResolveActionType(definition.className);
            if (actionType == null || !typeof(CharacterAction).IsAssignableFrom(actionType))
            {
                Debug.LogWarning($"Action type '{definition.className}' could not be found for action '{definition.actionName}'.");
                continue;
            }

            GameObject go = InstantiateActionButton(definition.actionName);
            if (go == null)
            {
                Debug.LogWarning($"Could not instantiate button for action '{definition.actionName}'. Ensure actionButtonPrefab is assigned.");
                continue;
            }

            // Add or reuse the specific CharacterAction component
            CharacterAction action = go.GetComponent(actionType) as CharacterAction ?? go.AddComponent(actionType) as CharacterAction;
            if (action == null)
            {
                Debug.LogWarning($"Failed to attach action component '{definition.className}' to button '{definition.actionName}'.");
                Destroy(go);
                continue;
            }

            WireUiReferences(go, action);
            ApplyDefinition(action, definition);
            created.Add(action);
        }

        return created.ToArray();
    }

    private static Type ResolveActionType(string className)
    {
        if (string.IsNullOrWhiteSpace(className)) return null;

        // Direct lookup
        Type direct = Type.GetType($"{className}, Assembly-CSharp");
        if (direct != null) return direct;

        // Fallback: find any CharacterAction-derived type whose name matches ignoring case and common punctuation differences
        string normalized = NormalizeActionName(className).Replace("-", string.Empty).Replace(" ", string.Empty);
        foreach (Type t in typeof(CharacterAction).Assembly.GetTypes())
        {
            if (!typeof(CharacterAction).IsAssignableFrom(t)) continue;
            string tn = NormalizeActionName(t.Name).Replace("-", string.Empty).Replace(" ", string.Empty);
            if (tn == normalized) return t;
        }

        return null;
    }

    private GameObject InstantiateActionButton(string actionName)
    {
        GameObject prefab = actionButtonPrefab;
        if (prefab == null)
        {
            prefab = Resources.Load<GameObject>("ActionButton");
        }
        if (prefab == null)
        {
            prefab = Resources.Load<GameObject>("GameObjects/ActionButton");
        }
        if (prefab == null)
        {
            Debug.LogWarning("ActionButton prefab is not assigned and could not be loaded from Resources.");
            return null;
        }

        GameObject instance = Instantiate(prefab, transform);
        instance.name = actionName;
        return instance;
    }

    private void WireUiReferences(GameObject go, CharacterAction action)
    {
        if (go == null || action == null) return;

        action.button = go.GetComponent<Button>() ?? go.GetComponentInChildren<Button>(true);
        action.textUI = go.GetComponentInChildren<TextMeshProUGUI>(true);

        if (hoverPrefab != null)
        {
            action.hoverPrefab = hoverPrefab;
        }

        if (action.button != null)
        {
            action.button.onClick.RemoveListener(action.ExecuteFromButton);
            action.button.onClick.AddListener(action.ExecuteFromButton);
        }
    }

    private static void ApplyDefinition(CharacterAction action, ActionDefinition definition)
    {
        if (action == null || definition == null) return;

        action.actionName = definition.actionName;
        action.description = definition.description;
        action.gameObject.name = definition.actionName;
        action.actionId = definition.actionId;
        action.difficulty = definition.difficulty;
        action.commanderSkillRequired = definition.commanderSkillRequired;
        action.agentSkillRequired = definition.agentSkillRequired;
        action.emissarySkillRequired = definition.emissarySkillRequired;
        action.mageSkillRequired = definition.mageSkillRequired;
        action.leatherCost = definition.leatherCost;
        action.mountsCost = definition.mountsCost;
        action.timberCost = definition.timberCost;
        action.ironCost = definition.ironCost;
        action.steelCost = definition.steelCost;
        action.mithrilCost = definition.mithrilCost;
        action.goldCost = definition.goldCost;
        action.isBuyCaravans = definition.isBuyCaravans;
        action.isSellCaravans = definition.isSellCaravans;
        action.commanderXP = definition.commanderXP;
        action.agentXP = definition.agentXP;
        action.emmissaryXP = definition.emmissaryXP;
        action.mageXP = definition.mageXP;
        action.reward = definition.reward;
        action.advisorType = definition.advisorType;
    }

    private static string NormalizeActionName(string value)
    {
        string stripped = ActionNameUtils.StripShortcut(value);
        return string.IsNullOrWhiteSpace(stripped) ? string.Empty : stripped.Trim().ToLowerInvariant();
    }
}
