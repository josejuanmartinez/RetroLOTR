using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SelectionDialog))]
public class SelectionDialogEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

        SelectionDialog dialog = (SelectionDialog)target;

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Render Example", GUILayout.Height(28)))
            {
                Undo.RecordObject(dialog.gameObject, "Preview SelectionDialog");
                dialog.EditorRenderExample();
            }

            if (GUILayout.Button("Hide", GUILayout.Height(28)))
            {
                Undo.RecordObject(dialog.gameObject, "Hide SelectionDialog");
                dialog.EditorHide();
            }
        }
    }
}
