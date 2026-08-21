using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

[CustomEditor(typeof(CardDataProvider))]
[CanEditMultipleObjects]
public sealed class CardDataProviderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("cardName"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("startAsToken"));
        DrawDeckSelector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Runtime", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("initializeOnStart"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Presentation", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("suppressHoverEffects"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("useCardArtFolderOnly"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("showRequirementWarnings"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("showCloseIcon"));

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button("Apply", GUILayout.Height(30f))) ApplyPreview();
        }

        if (Application.isPlaying)
            EditorGUILayout.HelpBox("Use Apply() or Initialize(string, bool) from runtime code.", MessageType.Info);
        else
            EditorGUILayout.HelpBox("Apply resolves the named card and refreshes the Card preview.", MessageType.None);
    }

    private void DrawDeckSelector()
    {
        SerializedProperty deckProperty = serializedObject.FindProperty("deckId");
        List<string> ids = new() { string.Empty };
        List<string> labels = new() { "Card's own deck" };

        TextAsset manifestAsset = Resources.Load<TextAsset>("Cards");
        CardsManifest manifest = manifestAsset != null
            ? JsonUtility.FromJson<CardsManifest>(manifestAsset.text)
            : null;

        if (manifest?.decks != null)
        {
            foreach (DeckManifestEntry deck in manifest.decks)
            {
                if (deck == null || string.IsNullOrWhiteSpace(deck.deckId)) continue;
                ids.Add(deck.deckId);
                labels.Add(string.IsNullOrWhiteSpace(deck.nation)
                    ? deck.deckId
                    : $"{deck.deckId} ({deck.nation})");
            }
        }

        int selectedIndex = System.Math.Max(0, ids.FindIndex(id =>
            string.Equals(id, deckProperty.stringValue, System.StringComparison.OrdinalIgnoreCase)));

        EditorGUI.showMixedValue = deckProperty.hasMultipleDifferentValues;
        EditorGUI.BeginChangeCheck();
        int nextIndex = EditorGUILayout.Popup(new GUIContent("Deck Icon"), selectedIndex, labels.ToArray());
        if (EditorGUI.EndChangeCheck()) deckProperty.stringValue = ids[nextIndex];
        EditorGUI.showMixedValue = false;
    }

    private void ApplyPreview()
    {
        serializedObject.ApplyModifiedProperties();

        foreach (Object selected in targets)
        {
            CardDataProvider provider = selected as CardDataProvider;
            if (provider == null) continue;

            Component[] hierarchy = provider.GetComponentsInChildren<Component>(true);
            Undo.RecordObjects(hierarchy, "Apply Card Data");

            if (!provider.Apply()) continue;

            CardEditorArtworkBaker.ApplyArtwork(provider.Card);
            CardEditorArtworkBaker.ApplyDeckArtwork(provider.Card);

            foreach (Component component in hierarchy)
            {
                if (component == null) continue;
                EditorUtility.SetDirty(component);
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }

            EditorUtility.SetDirty(provider.gameObject);
        }

        serializedObject.Update();
    }
}
