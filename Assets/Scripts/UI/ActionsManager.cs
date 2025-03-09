using System.Reflection;
using System;
using UnityEngine;

public class ActionsManager : MonoBehaviour
{
    [HideInInspector]
    public TrainMetAtArms trainMenAtArms;
    [HideInInspector]
    public TrainArchers trainArchers;
    [HideInInspector]
    public TrainCatapults trainCatapults;
    [HideInInspector]
    public TrainLightCavalry trainLightCavalry;
    [HideInInspector]
    public TrainHeavyCavalry trainHeavyCavalry;
    [HideInInspector]
    public TrainLightInfantry TrainLightInfantry;
    [HideInInspector]
    public TrainHeavyInfantry trainHeavyInfantry;
    [HideInInspector]
    public TrainWarships trainWarships;
    [HideInInspector]
    public FoundPC foundPC;

    public void Start()
    {
        trainMenAtArms = GetComponentInChildren<TrainMetAtArms>();
        trainArchers = GetComponentInChildren<TrainArchers>();
        trainCatapults = GetComponentInChildren<TrainCatapults>();
        trainLightCavalry = GetComponentInChildren<TrainLightCavalry>();
        trainHeavyCavalry = GetComponentInChildren<TrainHeavyCavalry>();
        TrainLightInfantry = GetComponentInChildren<TrainLightInfantry>();
        trainHeavyInfantry = GetComponentInChildren<TrainHeavyInfantry>();
        trainWarships = GetComponentInChildren<TrainWarships>();
        foundPC = GetComponentInChildren<FoundPC>();
    }

    // Define a common base type or interface that both classes share
    // This could be MonoBehaviour if both inherit from it and both have the methods

    public void Refresh(Character character)
    {
        foreach (var field in GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            object actionObject = field.GetValue(this);
            if (actionObject == null) continue;

            // Get the type of the field value
            Type type = actionObject.GetType();

            // Find the Initialize method on that type
            MethodInfo initMethod = type.GetMethod("Initialize");
            initMethod.Invoke(actionObject, new object[] { character, null, null });
        }
    }

    public void Hide()
    {
        foreach (var field in GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            object actionObject = field.GetValue(this);
            if (actionObject == null) continue;

            // Get the type of the field value
            Type type = actionObject.GetType();

            // Find the Reset method on that type
            MethodInfo resetMethod = type.GetMethod("Reset");
            resetMethod.Invoke(actionObject, new object[] { });
        }
    }
}