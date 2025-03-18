using System.Reflection;
using System;
using System.Collections.Generic;
using UnityEngine;

public class ActionsManager : MonoBehaviour
{
    // Dictionary to store all the action components
    private Dictionary<Type, MonoBehaviour> actionComponents = new Dictionary<Type, MonoBehaviour>();

    public void Start()
    {
        // Get all components that might be actions (all MonoBehaviours in children)
        MonoBehaviour[] allChildComponents = GetComponentsInChildren<MonoBehaviour>();

        // Store each component by its type for easy access
        foreach (MonoBehaviour component in allChildComponents)
        {
            // Skip this ActionsManager itself
            if (component == this) continue;

            Type componentType = component.GetType();
            actionComponents[componentType] = component;
        }
    }

    // Generic method to get a specific action component
    public T GetAction<T>() where T : MonoBehaviour
    {
        if (actionComponents.TryGetValue(typeof(T), out MonoBehaviour component))
        {
            return component as T;
        }

        Debug.LogWarning($"Action of type {typeof(T).Name} not found!");
        return null;
    }

    public void Refresh(Character character)
    {
        foreach (var component in actionComponents.Values)
        {
            if (component == null) continue;

            // Get the type of the component
            Type type = component.GetType();

            // Find the Initialize method on that type
            MethodInfo initMethod = type.GetMethod("Initialize");
            if (initMethod != null)
            {
                initMethod.Invoke(component, new object[] { character, null, null });
            }
        }
    }

    public void Hide()
    {
        foreach (var component in actionComponents.Values)
        {
            if (component == null) continue;

            // Get the type of the component
            Type type = component.GetType();

            // Find the Reset method on that type
            MethodInfo resetMethod = type.GetMethod("Reset");
            if (resetMethod != null)
            {
                resetMethod.Invoke(component, new object[] { });
            }
        }
    }
}