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
                if (Application.isPlaying) wheel.DebugForceOpen();
                else wheel.EditorPreviewBloom();
            }

            if (GUILayout.Button("Reset", GUILayout.Height(28)))
            {
                if (Application.isPlaying) wheel.DebugForceClose();
                else wheel.EditorResetBloom();
            }
        }
    }
}
