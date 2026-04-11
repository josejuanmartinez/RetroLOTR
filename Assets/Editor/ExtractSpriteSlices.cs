using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed class ExtractSpriteSlices : EditorWindow
{
    private const string DefaultOutputFolder = "Assets/Art/UI/ExtractedSprites";
    private const string MenuPath = "Tools/Sprites/Extract Sliced Sprites";
    private const string BatchMethodName = "ExtractSpriteSlices.ExtractFromCommandLine";

    [SerializeField] private DefaultAsset outputFolderAsset;
    [SerializeField] private string spriteNamePrefix = "";
    [SerializeField] private bool overwriteExisting = true;
    [SerializeField] private bool selectionOnly = true;

    [MenuItem(MenuPath)]
    public static void ShowWindow()
    {
        GetWindow<ExtractSpriteSlices>("Extract Sprite Slices");
    }

    public static void ExtractFromCommandLine()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        string outputFolder = GetArgValue(args, "-spriteSliceOutput");
        string prefix = GetArgValue(args, "-spriteSlicePrefix") ?? string.Empty;
        string sourcesArg = GetArgValue(args, "-spriteSliceSources");

        List<string> sourceTexturePaths = (sourcesArg ?? string.Empty)
            .Split(new[] { ';' }, System.StringSplitOptions.RemoveEmptyEntries)
            .Select(path => path.Trim())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToList();

        if (sourceTexturePaths.Count == 0)
        {
            Debug.LogError($"{BatchMethodName}: no source textures provided. Pass -spriteSliceSources \"Assets/path1.png;Assets/path2.png\".");
            EditorApplication.Exit(1);
            return;
        }

        if (string.IsNullOrWhiteSpace(outputFolder))
        {
            outputFolder = DefaultOutputFolder;
        }

        if (!AssetDatabase.IsValidFolder(outputFolder))
        {
            Directory.CreateDirectory(outputFolder);
            AssetDatabase.Refresh();
        }

        bool success = ExtractInternal(sourceTexturePaths, outputFolder, prefix, overwriteExisting: true, warnings: out List<string> warnings, exported: out int exported);
        for (int i = 0; i < warnings.Count; i++)
        {
            Debug.LogWarning(warnings[i]);
        }

        Debug.Log($"{BatchMethodName}: exported {exported} sprites to {outputFolder} with prefix filter '{prefix}'.");
        AssetDatabase.SaveAssets();
        EditorApplication.Exit(success ? 0 : 1);
    }

    private void OnEnable()
    {
        if (outputFolderAsset == null)
        {
            outputFolderAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(DefaultOutputFolder);
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Extract sliced Sprite sub-assets into standalone PNG files.", EditorStyles.wordWrappedLabel);
        EditorGUILayout.Space();

        outputFolderAsset = (DefaultAsset)EditorGUILayout.ObjectField(
            "Output Folder",
            outputFolderAsset,
            typeof(DefaultAsset),
            false);

        spriteNamePrefix = EditorGUILayout.TextField("Name Prefix Filter", spriteNamePrefix);
        selectionOnly = EditorGUILayout.Toggle("Use Current Selection", selectionOnly);
        overwriteExisting = EditorGUILayout.Toggle("Overwrite Existing", overwriteExisting);

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Select one or more textures imported as Sprite Mode = Multiple, then click Extract. "
            + "Only sprite sub-assets whose names match the optional prefix filter will be exported.",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(!CanExtract()))
        {
            if (GUILayout.Button("Extract"))
            {
                Extract();
            }
        }
    }

    private bool CanExtract()
    {
        return ResolveOutputFolderPath() != null && GetSourceTexturePaths().Count > 0;
    }

    private void Extract()
    {
        string outputFolder = ResolveOutputFolderPath();
        if (string.IsNullOrWhiteSpace(outputFolder))
        {
            Debug.LogError("Sprite slice extraction: output folder must be a valid folder under Assets/.");
            return;
        }

        List<string> sourceTexturePaths = GetSourceTexturePaths();
        if (sourceTexturePaths.Count == 0)
        {
            Debug.LogWarning("Sprite slice extraction: no source textures selected.");
            return;
        }

        bool success = ExtractInternal(sourceTexturePaths, outputFolder, spriteNamePrefix, overwriteExisting, out List<string> warnings, out int exported);
        for (int i = 0; i < warnings.Count; i++)
        {
            Debug.LogWarning(warnings[i]);
        }

        Debug.Log(success
            ? $"Sprite slice extraction complete. Exported {exported} sprites to {outputFolder}."
            : $"Sprite slice extraction completed with issues. Exported {exported} sprites to {outputFolder}.");
    }

    private string ResolveOutputFolderPath()
    {
        if (outputFolderAsset == null)
        {
            return DefaultOutputFolder;
        }

        string path = AssetDatabase.GetAssetPath(outputFolderAsset);
        if (string.IsNullOrWhiteSpace(path) || !AssetDatabase.IsValidFolder(path))
        {
            return null;
        }

        return path;
    }

    private List<string> GetSourceTexturePaths()
    {
        IEnumerable<Object> sourceObjects = selectionOnly
            ? Selection.objects
            : Resources.FindObjectsOfTypeAll<Texture2D>().Cast<Object>();

        return sourceObjects
            .Select(AssetDatabase.GetAssetPath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct()
            .Where(IsMultiSpriteTexturePath)
            .ToList();
    }

    private bool ShouldIncludeSprite(string spriteName)
    {
        if (string.IsNullOrWhiteSpace(spriteName))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(spriteNamePrefix))
        {
            return true;
        }

        return spriteName.StartsWith(spriteNamePrefix);
    }

    private static bool ShouldIncludeSprite(string spriteName, string prefix)
    {
        if (string.IsNullOrWhiteSpace(spriteName))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(prefix))
        {
            return true;
        }

        return spriteName.StartsWith(prefix);
    }

    private static bool IsMultiSpriteTexturePath(string assetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        return importer != null
            && importer.textureType == TextureImporterType.Sprite
            && importer.spriteImportMode == SpriteImportMode.Multiple;
    }

    private static bool ExtractInternal(
        List<string> sourceTexturePaths,
        string outputFolder,
        string prefix,
        bool overwriteExisting,
        out List<string> warnings,
        out int exported)
    {
        warnings = new List<string>();
        exported = 0;

        Directory.CreateDirectory(outputFolder);

        try
        {
            AssetDatabase.StartAssetEditing();

            for (int i = 0; i < sourceTexturePaths.Count; i++)
            {
                string atlasPath = sourceTexturePaths[i];
                if (!File.Exists(atlasPath))
                {
                    warnings.Add($"Missing source texture: {atlasPath}");
                    continue;
                }

                TextureImporter importer = AssetImporter.GetAtPath(atlasPath) as TextureImporter;
                if (importer == null)
                {
                    warnings.Add($"Texture importer not found for: {atlasPath}");
                    continue;
                }

                bool restoreReadable = false;
                if (!importer.isReadable)
                {
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                    restoreReadable = true;
                }

                Texture2D atlasTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);
                if (atlasTexture == null)
                {
                    warnings.Add($"Could not load source texture: {atlasPath}");
                    RestoreReadable(importer, restoreReadable);
                    continue;
                }

                Sprite[] sprites = AssetDatabase.LoadAllAssetRepresentationsAtPath(atlasPath)
                    .OfType<Sprite>()
                    .Where(sprite => sprite != null && ShouldIncludeSprite(sprite.name, prefix))
                    .ToArray();

                if (sprites.Length == 0)
                {
                    warnings.Add($"No matching sliced sprites found in: {atlasPath}");
                    RestoreReadable(importer, restoreReadable);
                    continue;
                }

                for (int j = 0; j < sprites.Length; j++)
                {
                    Sprite sprite = sprites[j];
                    string outputPath = $"{outputFolder}/{sprite.name}.png";
                    if (!overwriteExisting && File.Exists(outputPath))
                    {
                        continue;
                    }

                    Texture2D extracted = ExtractSpriteTexture(atlasTexture, sprite);
                    if (extracted == null)
                    {
                        warnings.Add($"Failed to extract sprite '{sprite.name}' from {atlasPath}");
                        continue;
                    }

                    File.WriteAllBytes(outputPath, extracted.EncodeToPNG());
                    Object.DestroyImmediate(extracted);
                    exported++;
                }

                RestoreReadable(importer, restoreReadable);
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }

        ConfigureExtractedSprites(outputFolder);
        return warnings.Count == 0;
    }

    private static string GetArgValue(string[] args, string key)
    {
        if (args == null || string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], key))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static void RestoreReadable(TextureImporter importer, bool restoreReadable)
    {
        if (restoreReadable && importer != null)
        {
            importer.isReadable = false;
            importer.SaveAndReimport();
        }
    }

    private static Texture2D ExtractSpriteTexture(Texture2D atlasTexture, Sprite sprite)
    {
        if (atlasTexture == null || sprite == null)
        {
            return null;
        }

        Rect rect = sprite.rect;
        int width = Mathf.RoundToInt(rect.width);
        int height = Mathf.RoundToInt(rect.height);
        if (width <= 0 || height <= 0)
        {
            return null;
        }

        Texture2D texture = new(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels(atlasTexture.GetPixels(
            Mathf.RoundToInt(rect.x),
            Mathf.RoundToInt(rect.y),
            width,
            height));
        texture.Apply();
        return texture;
    }

    private static void ConfigureExtractedSprites(string outputFolder)
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { outputFolder });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                continue;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
    }
}
