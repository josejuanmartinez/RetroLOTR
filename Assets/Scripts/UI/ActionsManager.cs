using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ActionsManager : MonoBehaviour
{
    public CharacterAction DEFAULT;
    public CharacterAction[] characterActions;
    // Dictionary to store all the action components
    private Dictionary<Type, CharacterAction> actionComponents = new ();
    private CanvasGroup canvasGroup;

    public void Start()
    {
        canvasGroup = GetComponent<CanvasGroup>();

        // Get all components that might be actions (all MonoBehaviours in children)
        characterActions = GetComponentsInChildren<CharacterAction>();

        // Store each component by its type for easy access
        foreach (CharacterAction component in characterActions)
        {
            Debug.Log($"Registering action {component.actionName}");
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

        canvasGroup.alpha = isPlayerTurn ? 1f : 0f;
        canvasGroup.interactable = isPlayerTurn;
        canvasGroup.blocksRaycasts = isPlayerTurn;
    }
}
