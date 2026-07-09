using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

public class HexTextureMapping : SearcherByName
{
    private const string AddressablesLabel = "default";
    public static string PcArtAddressRoot = "Assets/Art/Hexes/PCs";
    [SerializeField] private string pcArtAddressRoot = "Assets/Art/Hexes/PCs";

    private void Awake()
    {
        PcArtAddressRoot = pcArtAddressRoot;
        pcArtLocationsByKey.Clear();
        byPcNameLookup.Clear();
        isPcArtLookupLoaded = false;
        pcArtLookupLoadFailed = false;
    }

    public Sprite defaultTerrainSprite;
    public List<Sprite> deepWaterVariations;
    public List<Sprite> desertVariations;
    public List<Sprite> forestVariations;    
    public List<Sprite> grassVariations;
    public List<Sprite> hillsVariations;
    public List<Sprite> plainsVariations;
    public List<Sprite> shallowWaterVariations;
    public List<Sprite> shoreVariations;
    public List<Sprite> swampVariations;
    public List<Sprite> wastelandsVariations;
    public List<Sprite> mountainsVariations;
    public List<Sprite> snowVariations;

    public Sprite islandSprite;

    private static readonly Dictionary<string, Sprite> byPcNameLookup = new();
    private static readonly Dictionary<string, IResourceLocation> pcArtLocationsByKey = new();
    private static bool isPcArtLookupLoaded;
    private static bool pcArtLookupLoadFailed;
    private bool loggedNotReadyWarning;

    private static void EnsurePcArtLocationsLoaded()
    {
        if (isPcArtLookupLoaded || pcArtLookupLoadFailed) return;

        AsyncOperationHandle<IList<IResourceLocation>> handle = Addressables.LoadResourceLocationsAsync(AddressablesLabel, typeof(Sprite));
        IList<IResourceLocation> locations = handle.WaitForCompletion();
        if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
        {
            pcArtLookupLoadFailed = true;
            Debug.LogError($"HexTextureMapping: failed to load Addressables label '{AddressablesLabel}'.");
            return;
        }

        pcArtLocationsByKey.Clear();
        foreach (IResourceLocation location in locations)
        {
            if (location == null || string.IsNullOrWhiteSpace(location.PrimaryKey)) continue;
            if (!location.PrimaryKey.StartsWith(PcArtAddressRoot)) continue;

            RegisterLocationLookupKey(Path.GetFileNameWithoutExtension(location.PrimaryKey), location);
        }

        isPcArtLookupLoaded = true;
    }

    private static void RegisterLocationLookupKey(string rawKey, IResourceLocation location)
    {
        string key = NormalizeKey(rawKey);
        if (string.IsNullOrWhiteSpace(key) || pcArtLocationsByKey.ContainsKey(key)) return;
        pcArtLocationsByKey[key] = location;
    }

    private static void RegisterSpriteLookupKeys(Sprite sprite)
    {
        if (sprite == null) return;

        RegisterLookupKey(sprite.name, sprite);

        if (sprite.texture != null)
        {
            RegisterLookupKey(sprite.texture.name, sprite);
        }
    }

    public static string NormalizeKey(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;

        string normalized = name.Normalize(System.Text.NormalizationForm.FormD);
        System.Text.StringBuilder sb = new();
        foreach (char c in normalized)
        {
            var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        string withoutDiacritics = sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
        string sanitized = System.Text.RegularExpressions.Regex.Replace(withoutDiacritics, "[^A-Za-z0-9]", string.Empty);
        return sanitized.ToLowerInvariant();
    }

    private static void RegisterLookupKey(string rawKey, Sprite sprite)
    {
        string key = NormalizeKey(rawKey);
        if (string.IsNullOrWhiteSpace(key) || byPcNameLookup.ContainsKey(key)) return;
        byPcNameLookup[key] = sprite;
    }

    private Sprite GetPcSpriteByName(string pcName)
    {
        if (string.IsNullOrWhiteSpace(pcName)) return null;
        string normalizedPcName = NormalizeKey(pcName);

        if (byPcNameLookup.TryGetValue(normalizedPcName, out Sprite cachedSprite))
        {
            return cachedSprite;
        }

        EnsurePcArtLocationsLoaded();
        if (!isPcArtLookupLoaded)
        {
            if (!loggedNotReadyWarning)
            {
                Debug.LogWarning("HexTextureMapping PC art lookup is unavailable.");
                loggedNotReadyWarning = true;
            }
            return null;
        }

        if (!pcArtLocationsByKey.TryGetValue(normalizedPcName, out IResourceLocation location))
        {
            return null;
        }

        AsyncOperationHandle<Sprite> spriteHandle = Addressables.LoadAssetAsync<Sprite>(location);
        Sprite sprite = spriteHandle.WaitForCompletion();
        if (sprite == null || spriteHandle.Status != AsyncOperationStatus.Succeeded)
        {
            return null;
        }

        RegisterSpriteLookupKeys(sprite);
        return byPcNameLookup.TryGetValue(normalizedPcName, out Sprite loadedSprite) ? loadedSprite : sprite;
    }


    // Terrain art for the hex's base texture layer. Always returned regardless of whether
    // the hex also has a PC — the PC art is drawn separately on its own sprite renderer.
    public Sprite GetTerrainSprite(Hex hex)
    {
        if (hex == null) return defaultTerrainSprite;

        Sprite baseTerrainSprite = hex.GetBaseTerrainSprite();
        if (baseTerrainSprite != null) return baseTerrainSprite;

        return GetTerrainBaseSprite(hex.terrainType);
    }

    // PC/settlement art for the hex's PC texture layer, or null when the hex has no PC to show
    // (callers should hide that layer in that case).
    public Sprite GetPcSprite(Hex hex)
    {
        if (hex == null || !hex.HasAnyPC() || !hex.ShouldShowPcVisual()) return null;

        PC pc = hex.GetPCData();
        if (pc == null) return null;

        Sprite result = GetPcSpriteByName(pc.pcName);
        if (result == null)
        {
            Debug.LogWarning($"HexTextureMapping: no PC illustration resolvable by name for '{pc.pcName}'.");
        }
        return result;
    }

    // All terrain variation lists, in TerrainEnum order, for name lookups.
    private IEnumerable<List<Sprite>> AllTerrainVariationLists()
    {
        yield return mountainsVariations;
        yield return hillsVariations;
        yield return plainsVariations;
        yield return grassVariations;
        yield return shoreVariations;
        yield return forestVariations;
        yield return shallowWaterVariations;
        yield return deepWaterVariations;
        yield return swampVariations;
        yield return desertVariations;
        yield return wastelandsVariations;
        yield return snowVariations;
    }

    public List<Sprite> GetTerrainVariations(TerrainEnum terrainType)
    {
        return terrainType switch
        {
            TerrainEnum.deepWater => deepWaterVariations,
            TerrainEnum.desert => desertVariations,
            TerrainEnum.forest => forestVariations,
            TerrainEnum.grasslands => grassVariations,
            TerrainEnum.hills => hillsVariations,
            TerrainEnum.plains => plainsVariations,
            TerrainEnum.shallowWater => shallowWaterVariations,
            TerrainEnum.shore => shoreVariations,
            TerrainEnum.swamp => swampVariations,
            TerrainEnum.wastelands => wastelandsVariations,
            TerrainEnum.mountains => mountainsVariations,
            TerrainEnum.snow => snowVariations,
            _ => null
        };
    }

    // Resolves a specific terrain tile variation by sprite name (used to apply authored
    // scenario tiles, which also determine the hex's Underground-entrance state via ChasmTiles).
    public Sprite GetTerrainSpriteByName(string spriteName)
    {
        if (string.IsNullOrWhiteSpace(spriteName)) return null;
        foreach (List<Sprite> list in AllTerrainVariationLists())
        {
            if (list == null) continue;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && string.Equals(list[i].name, spriteName, System.StringComparison.OrdinalIgnoreCase))
                    return list[i];
            }
        }
        return null;
    }

    public Sprite GetTerrainBaseSprite(TerrainEnum terrainType)
    {
        if (terrainType == TerrainEnum.MAX) return defaultTerrainSprite;

        return terrainType switch
        {
            TerrainEnum.deepWater => deepWaterVariations[Random.Range(0, deepWaterVariations.Count)],
            TerrainEnum.desert => desertVariations[Random.Range(0, desertVariations.Count)],
            TerrainEnum.forest => forestVariations[Random.Range(0, forestVariations.Count)],
            TerrainEnum.grasslands => grassVariations[Random.Range(0, grassVariations.Count)],
            TerrainEnum.hills => hillsVariations[Random.Range(0, hillsVariations.Count)],
            TerrainEnum.plains => plainsVariations[Random.Range(0, plainsVariations.Count)],
            TerrainEnum.shallowWater => shallowWaterVariations[Random.Range(0, shallowWaterVariations.Count)],
            TerrainEnum.shore => shoreVariations[Random.Range(0, shoreVariations.Count)],
            TerrainEnum.swamp => swampVariations[Random.Range(0, swampVariations.Count)],
            TerrainEnum.wastelands => wastelandsVariations[Random.Range(0, wastelandsVariations.Count)],
            TerrainEnum.mountains => mountainsVariations[Random.Range(0, mountainsVariations.Count)],
            TerrainEnum.snow => (snowVariations != null && snowVariations.Count > 0) ? snowVariations[Random.Range(0, snowVariations.Count)] : defaultTerrainSprite,
            _ => defaultTerrainSprite
        };
    }

    public Sprite GetIslandSprite()
    {
        return islandSprite != null ? islandSprite : defaultTerrainSprite;
    }
}
