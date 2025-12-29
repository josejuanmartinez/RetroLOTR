using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Sounds))]
public class SoundsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        if (target == null) return;
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script");
        serializedObject.ApplyModifiedProperties();
    }
}
