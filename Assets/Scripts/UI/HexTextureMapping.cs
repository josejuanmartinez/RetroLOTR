using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

public class HexTextureMapping : SearcherByName
{
    private const string AddressablesLabel = "default";
    public static string HexTileAddressRoot = "Assets/Art/Hexes/Tiles/";
    [SerializeField] private string hexTileAddressRoot = "Assets/Art/Hexes/Tiles/";

    private void Awake()
    {
        HexTileAddressRoot = hexTileAddressRoot;
        tileLocationsByKey.Clear();
        byPcNameLookup.Clear();
        isTileLookupLoaded = false;
        tileLookupLoadFailed = false;
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

    public List<Sprite> defaultFreePeoplePC;
    public List<Sprite> defaultDarkServantsPC;
    public List<Sprite> defaultNeutralPC;
    public Sprite islandSprite;

    private static readonly Dictionary<string, Sprite> byPcNameLookup = new();
    private static readonly Dictionary<string, IResourceLocation> tileLocationsByKey = new();
    private static bool isTileLookupLoaded;
    private static bool tileLookupLoadFailed;
    private bool loggedNotReadyWarning;

    private static void EnsureTileLocationsLoaded()
    {
        if (isTileLookupLoaded || tileLookupLoadFailed) return;

        AsyncOperationHandle<IList<IResourceLocation>> handle = Addressables.LoadResourceLocationsAsync(AddressablesLabel, typeof(Sprite));
        IList<IResourceLocation> locations = handle.WaitForCompletion();
        if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
        {
            tileLookupLoadFailed = true;
            Debug.LogError($"HexTextureMapping: failed to load Addressables label '{AddressablesLabel}'.");
            return;
        }

        tileLocationsByKey.Clear();
        foreach (IResourceLocation location in locations)
        {
            if (location == null || string.IsNullOrWhiteSpace(location.PrimaryKey)) continue;
            if (!location.PrimaryKey.StartsWith(HexTileAddressRoot)) continue;

            RegisterLocationLookupKey(Path.GetFileNameWithoutExtension(location.PrimaryKey), location);
        }

        isTileLookupLoaded = true;
    }

    private static void RegisterLocationLookupKey(string rawKey, IResourceLocation location)
    {
        string key = NormalizeKey(rawKey);
        if (string.IsNullOrWhiteSpace(key) || tileLocationsByKey.ContainsKey(key)) return;
        tileLocationsByKey[key] = location;
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

    private static string NormalizeKey(string name)
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

        EnsureTileLocationsLoaded();
        if (!isTileLookupLoaded)
        {
            if (!loggedNotReadyWarning)
            {
                Debug.LogWarning("HexTextureMapping tile lookup is unavailable.");
                loggedNotReadyWarning = true;
            }
            return null;
        }

        if (!tileLocationsByKey.TryGetValue(normalizedPcName, out IResourceLocation location))
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


    public Sprite GetSprite(Hex hex)
    {
        if (hex == null) return defaultTerrainSprite;

        if (hex.HasAnyPC() && hex.ShouldShowPcVisual())
        {
            PC pc = hex.GetPCData();
            if (pc == null) return GetTerrainSprite(hex);

            Sprite result = GetPcSpriteByName(pc.pcName);
            if(result)
            {
              return result;  
            } 
            else
            {
                int seed = Mathf.Abs(hex.GetHashCode());
                switch(pc.owner.GetAlignment())
                {
                    case AlignmentEnum.darkServants: return defaultDarkServantsPC[seed % defaultDarkServantsPC.Count];
                    case AlignmentEnum.freePeople: return defaultFreePeoplePC[seed % defaultFreePeoplePC.Count];
                    case AlignmentEnum.neutral: return defaultNeutralPC[seed % defaultNeutralPC.Count];
                }
            }
        }
        
        return GetTerrainSprite(hex);
    }

    private Sprite GetTerrainSprite(Hex hex)
    {
        Sprite baseTerrainSprite = hex.GetBaseTerrainSprite();
        if (baseTerrainSprite != null) return baseTerrainSprite;

        return GetTerrainBaseSprite(hex.terrainType);
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
    // scenario tiles, which also determine the hex's landmark features via HexFeatureData).
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
