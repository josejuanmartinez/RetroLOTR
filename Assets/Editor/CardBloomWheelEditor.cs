using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CardBloomWheel))]
public class CardBloomWheelEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

        CardBloomWheel wheel = (CardBloomWheel)target;

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Bloom", GUILayout.Height(28)))
            {
                Undo.RecordObject(wheel.gameObject, "Preview Bloom");
                wheel.EditorPreviewBloom();
            }

            if (GUILayout.Button("Reset", GUILayout.Height(28)))
            {
                Undo.RecordObject(wheel.gameObject, "Reset Bloom");
                wheel.EditorResetBloom();
            }
        }
    }
}
