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

    public void Start()
    {
        // Get all components that might be actions (all MonoBehaviours in children)
        characterActions = GetComponentsInChildren<CharacterAction>();

        // Store each component by its type for easy access
        foreach (CharacterAction component in characterActions)
        {
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
    }

    public void Hide()
    {
        actionComponents.Values.ToList().ForEach(component => component.Reset());
    }

    public int GetDefault()
    {
        return DEFAULT.actionId;
    }
}