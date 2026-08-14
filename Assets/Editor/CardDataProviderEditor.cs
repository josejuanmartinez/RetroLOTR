using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using RetroLOTR.Scenarios.EditorTools;
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

            ApplyEditorArtwork(provider.Card);
            ApplyEditorDeckArtwork(provider.Card);

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

    private static void ApplyEditorDeckArtwork(Card card)
    {
        if (card == null || card.cardData == null) return;

        SerializedObject cardObject = new SerializedObject(card);
        Image deckImage = cardObject.FindProperty("deckTypeImage")?.objectReferenceValue as Image;
        if (deckImage == null) return;

        Sprite deckSprite = FindSpriteByName(card.cardData.deckSpriteName, "Assets/Art/UI/Alignment");
        deckImage.sprite = deckSprite;
        deckImage.enabled = deckSprite != null;
        EditorUtility.SetDirty(deckImage);
        PrefabUtility.RecordPrefabInstancePropertyModifications(deckImage);
    }

    private static Sprite FindSpriteByName(string spriteName, string folder)
    {
        if (string.IsNullOrWhiteSpace(spriteName)) return null;

        string[] guids = AssetDatabase.FindAssets($"{spriteName} t:Sprite", new[] { folder });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is Sprite sprite &&
                    string.Equals(sprite.name, spriteName, System.StringComparison.OrdinalIgnoreCase))
                    return sprite;
            }
        }

        return null;
    }

    private static void ApplyEditorArtwork(Card card)
    {
        if (card == null || card.cardData == null) return;

        Sprite artwork = ScenarioCardCatalog.GetCardArtwork(card.cardData);
        if (artwork == null) return;

        SerializedObject cardObject = new SerializedObject(card);
        AssignArtwork(cardObject.FindProperty("cardArtImage"), artwork);
        AssignArtwork(cardObject.FindProperty("tokenImage"), artwork);
    }

    private static void AssignArtwork(SerializedProperty imageProperty, Sprite artwork)
    {
        Image image = imageProperty?.objectReferenceValue as Image;
        if (image == null) return;
        image.sprite = artwork;
        image.enabled = true;
        EditorUtility.SetDirty(image);
        PrefabUtility.RecordPrefabInstancePropertyModifications(image);
    }
}
