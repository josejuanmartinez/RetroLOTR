using UnityEditor;
using UnityEngine;

// The 51 Object-card images landed with spriteMode: 2 (Multiple) instead of Single, so their
// sprite name ended up "<Name>_0" instead of "<Name>" — ScenarioCardCatalog.FindSprite and every
// other exact-name sprite lookup silently missed them (t:Sprite search still succeeds, but the
// name check fails). This postprocessor forces Sprite/Single for anything under
// Assets/Art/Cards so future new_image-skill generations can't regress into the same bug.
public class CardArtImportFixer : AssetPostprocessor
{
    private const string CardArtRoot = "Assets/Art/Cards/";

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(CardArtRoot)) return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
    }
}
