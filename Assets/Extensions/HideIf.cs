// You'll need to add this custom attribute and drawer in your project
#if UNITY_EDITOR
// Attribute that specifies when to hide a field
using System;
using UnityEditor;
using UnityEngine;

public class HideIfAttribute : PropertyAttribute
{
    public readonly string ConditionalSourceField;

    public HideIfAttribute(string conditionalSourceField)
    {
        ConditionalSourceField = conditionalSourceField;
    }
}

// Custom property drawer that handles the HideIf attribute
[CustomPropertyDrawer(typeof(HideIfAttribute))]
public class HideIfPropertyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        HideIfAttribute hideIf = attribute as HideIfAttribute;
        SerializedProperty sourceProperty = property.serializedObject.FindProperty(hideIf.ConditionalSourceField);

        // Hide the field if the condition is true
        if (sourceProperty != null && !sourceProperty.boolValue)
        {
            EditorGUI.PropertyField(position, property, label, true);
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        HideIfAttribute hideIf = attribute as HideIfAttribute;
        SerializedProperty sourceProperty = property.serializedObject.FindProperty(hideIf.ConditionalSourceField);

        // If the condition is true, don't allocate vertical space for this property
        if (sourceProperty != null && sourceProperty.boolValue)
        {
            return 0;
        }

        return EditorGUI.GetPropertyHeight(property, label, true);
    }
}
#endif