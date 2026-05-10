using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(SpriteRendererGridLayout))]
[CanEditMultipleObjects]
public class SpriteRendererGridLayoutEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        if (GUILayout.Button("Arrange Children", GUILayout.Height(30)))
        {
            foreach (Object t in targets)
            {
                SpriteRendererGridLayout layout = t as SpriteRendererGridLayout;
                if (layout == null) continue;

                Transform parent = layout.transform;
                int childCount = parent.childCount;
                Object[] undoObjects = new Object[childCount + 1];
                undoObjects[0] = parent;
                for (int i = 0; i < childCount; i++)
                {
                    undoObjects[i + 1] = parent.GetChild(i);
                }
                Undo.RecordObjects(undoObjects, "Arrange Sprite Grid");

                layout.Arrange();

                for (int i = 0; i < childCount; i++)
                {
                    EditorUtility.SetDirty(parent.GetChild(i));
                }
                EditorUtility.SetDirty(layout);
            }

            SceneView.RepaintAll();
        }
    }
}
