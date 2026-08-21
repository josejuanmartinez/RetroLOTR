using RetroLOTR.Scenarios.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Bakes a CardDataProvider's resolved card/deck artwork directly onto its Card's Image
// components at edit time, so the sprite ends up as a normal serialized asset reference with no
// runtime Illustrations/Addressables lookup involved. Shared by CardDataProviderEditor's preview
// button and StartupLoadingScreenEditor's preload-card baking.
internal static class CardEditorArtworkBaker
{
    public static void ApplyArtwork(Card card)
    {
        if (card == null || card.cardData == null) return;

        Sprite artwork = ScenarioCardCatalog.GetCardArtwork(card.cardData);
        if (artwork == null) return;

        SerializedObject cardObject = new SerializedObject(card);
        AssignArtwork(cardObject.FindProperty("cardArtImage"), artwork);
        AssignArtwork(cardObject.FindProperty("tokenImage"), artwork);
    }

    public static void ApplyDeckArtwork(Card card)
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

    private static void AssignArtwork(SerializedProperty imageProperty, Sprite artwork)
    {
        Image image = imageProperty?.objectReferenceValue as Image;
        if (image == null) return;
        image.sprite = artwork;
        image.enabled = true;
        EditorUtility.SetDirty(image);
        PrefabUtility.RecordPrefabInstancePropertyModifications(image);
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
}
