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
    public Transform gridLayoutTransform;

    [Header("Pagination")]
    public Button previousPageButton;
    public Button nextPageButton;
    [SerializeField] private int actionsPerPage = 5;

    [HideInInspector]
    public CharacterAction DEFAULT;
    public CharacterAction[] characterActions;
    // Dictionary to store all the action components
    private Dictionary<Type, CharacterAction> actionComponents = new ();
    private CanvasGroup canvasGroup;
    private readonly List<CharacterAction> availableActions = new();
    private int currentPageIndex;
    private Character currentCharacter;
    public static readonly char[] ActionHotkeyLetters = "BCEFGHIJKLMOQRTUVWYZ".ToCharArray();


    private Illustrations illustrations;
    public void Start()
    {
        if(illustrations == null) illustrations = FindFirstObjectByType<Illustrations>();
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
        SetupPaginationButtons();
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
        if (currentCharacter != character)
        {
            currentCharacter = character;
            currentPageIndex = 0;
        }
        actionComponents.Values.ToList().ForEach(component => component.Initialize(character, null, null));
        BuildAvailableActions();
        ApplyPagination();
        UpdateInteractableState();
    }

    public void Hide()
    {
        actionComponents.Values.ToList().ForEach(component => component.Reset());
        availableActions.Clear();
        currentPageIndex = 0;
        UpdatePaginationButtons(0);
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

    private void SetupPaginationButtons()
    {
        if (previousPageButton != null)
        {
            previousPageButton.onClick.RemoveListener(GoToPreviousPage);
            previousPageButton.onClick.AddListener(GoToPreviousPage);
        }
        if (nextPageButton != null)
        {
            nextPageButton.onClick.RemoveListener(GoToNextPage);
            nextPageButton.onClick.AddListener(GoToNextPage);
        }
    }

    private void GoToPreviousPage()
    {
        if (currentPageIndex <= 0) return;
        currentPageIndex--;
        ApplyPagination();
    }

    private void GoToNextPage()
    {
        int totalPages = GetTotalPages();
        if (currentPageIndex >= totalPages - 1) return;
        currentPageIndex++;
        ApplyPagination();
    }

    public void PreviousPage()
    {
        GoToPreviousPage();
    }

    public void NextPage()
    {
        GoToNextPage();
    }

    public void ExecuteActionAtPageIndex(int index)
    {
        if (index < 0 || index >= actionsPerPage) return;
        if (availableActions.Count == 0) return;

        int startIndex = currentPageIndex * actionsPerPage;
        int targetIndex = startIndex + index;
        if (targetIndex < 0 || targetIndex >= availableActions.Count) return;

        CharacterAction action = availableActions[targetIndex];
        if (action == null || !action.FulfillsConditions()) return;
        action.ExecuteFromButton();
    }

    public void ExecuteActionByHotkey(char letter)
    {
        int index = GetHotkeyIndex(letter);
        if (index < 0) return;
        ExecuteActionAtPageIndex(index);
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

    private void BuildAvailableActions()
    {
        availableActions.Clear();
        if (characterActions == null) return;

        foreach (CharacterAction action in characterActions)
        {
            if (action == null) continue;
            if (action.FulfillsConditions())
            {
                availableActions.Add(action);
            }
        }

        availableActions.Sort((a, b) => string.Compare(a?.actionName, b?.actionName, StringComparison.OrdinalIgnoreCase));
    }

    private void ApplyPagination()
    {
        if (characterActions == null || characterActions.Length == 0)
        {
            UpdatePaginationButtons(0);
            return;
        }

        foreach (CharacterAction action in characterActions)
        {
            if (action?.button != null)
            {
                action.button.gameObject.SetActive(false);
            }
        }

        int totalActions = availableActions.Count;
        int totalPages = GetTotalPages();
        if (totalActions == 0 || totalPages == 0)
        {
            UpdatePaginationButtons(0);
            return;
        }

        currentPageIndex = Mathf.Clamp(currentPageIndex, 0, totalPages - 1);
        int startIndex = currentPageIndex * actionsPerPage;
        int endIndex = Mathf.Min(startIndex + actionsPerPage, totalActions);

        Transform targetParent = gridLayoutTransform != null ? gridLayoutTransform : transform;
        for (int i = startIndex; i < endIndex; i++)
        {
            CharacterAction action = availableActions[i];
            if (action == null) continue;
            if (action.button != null)
            {
                action.button.gameObject.SetActive(true);
            }
            if (action.transform.parent != targetParent)
            {
                action.transform.SetParent(targetParent, false);
            }
            action.transform.SetSiblingIndex(i - startIndex);
            if (action.textUI != null)
            {
                action.textUI.text = FormatActionLabel(i - startIndex, action.actionName);
            }
        }

        UpdatePaginationButtons(totalPages);
    }

    private int GetTotalPages()
    {
        if (actionsPerPage <= 0) return 0;
        return Mathf.CeilToInt(availableActions.Count / (float)actionsPerPage);
    }

    private void UpdatePaginationButtons(int totalPages)
    {
        bool hasPages = totalPages > 1;

        if (previousPageButton != null)
        {
            previousPageButton.gameObject.SetActive(hasPages);
            previousPageButton.interactable = hasPages && currentPageIndex > 0;
        }

        if (nextPageButton != null)
        {
            nextPageButton.gameObject.SetActive(hasPages);
            nextPageButton.interactable = hasPages && currentPageIndex < totalPages - 1;
        }
    }

    private string FormatActionLabel(int index, string actionName)
    {
        if (TryGetHotkeyLetter(index, out char letter))
        {
            return $"[{letter}] {actionName ?? string.Empty}";
        }

        return $"[{index}] {actionName ?? string.Empty}";
    }

    private bool TryGetHotkeyLetter(int index, out char letter)
    {
        if (index >= 0 && index < ActionHotkeyLetters.Length)
        {
            letter = ActionHotkeyLetters[index];
            return true;
        }

        letter = '?';
        return false;
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

    private Type ResolveActionType(string className)
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

        GameObject instance = Instantiate(prefab, gridLayoutTransform);
        instance.name = actionName;
        return instance;
    }

    private void WireUiReferences(GameObject go, CharacterAction action)
    {
        if (go == null || action == null) return;

        action.button = go.GetComponent<Button>() ?? go.GetComponentInChildren<Button>(true);
        action.textUI = go.GetComponentInChildren<TextMeshProUGUI>(true);
        if (action.spriteImage == null)
        {
            action.spriteImage = FindActionSpriteImage(go);
        }

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

    private void ApplyDefinition(CharacterAction action, ActionDefinition definition)
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
        ApplyIcon(action, definition.iconName);
    }

    private Image FindActionSpriteImage(GameObject go)
    {
        Transform imageTransform = go.transform.Find("Image");
        if (imageTransform != null)
        {
            return imageTransform.GetComponent<Image>();
        }

        return go.GetComponentInChildren<Image>(true);
    }

    private void ApplyIcon(CharacterAction action, string iconName)
    {
        if (action == null || action.spriteImage == null) return;
        if (string.IsNullOrWhiteSpace(iconName)) return;

        if (illustrations == null) return;

        Sprite icon = illustrations.GetIllustrationByName(iconName);
        action.spriteImage.sprite = icon;
        action.spriteImage.enabled = icon != null;
    }

    private string NormalizeActionName(string value)
    {
        string stripped = ActionNameUtils.StripShortcut(value);
        return string.IsNullOrWhiteSpace(stripped) ? string.Empty : stripped.Trim().ToLowerInvariant();
    }
}
