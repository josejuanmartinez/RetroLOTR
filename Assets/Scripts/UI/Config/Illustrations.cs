using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;


public class Illustrations : SearcherByName
{
    private const string IllustrationsLabel = "default";
    private static readonly string[] IllustrationsAddressRoots =
    {
        "Assets/Art/Cards/",
        "Assets/Art/UI/"
    };

    private Dictionary<string, Sprite> illustrationsByName = new();
    private AsyncOperationHandle<IList<IResourceLocation>> locationsHandle;
    private readonly List<AsyncOperationHandle<Sprite>> spriteHandles = new();
    private int pendingLocationLoads;
    private bool isLoaded;
    private bool loggedNotReadyWarning;

    public bool IsLoaded => isLoaded;

    private void Awake()
    {
        locationsHandle = Addressables.LoadResourceLocationsAsync(IllustrationsLabel, typeof(Sprite));
        locationsHandle.Completed += OnIllustrationLocationsLoaded;
    }

    private void OnDestroy()
    {
        foreach (AsyncOperationHandle<Sprite> handle in spriteHandles)
        {
            if (handle.IsValid())
            {
                Addressables.Release(handle);
            }
        }

        spriteHandles.Clear();

        if (locationsHandle.IsValid())
        {
            Addressables.Release(locationsHandle);
        }
    }

    private void OnIllustrationLocationsLoaded(AsyncOperationHandle<IList<IResourceLocation>> handle)
    {
        if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
        {
            illustrationsByName = new Dictionary<string, Sprite>();
            isLoaded = false;
            Debug.LogError($"Illustrations: failed to load Addressables label '{IllustrationsLabel}'.");
            return;
        }

        illustrationsByName = new Dictionary<string, Sprite>();
        pendingLocationLoads = 0;
        int queuedCount = 0;
        foreach (IResourceLocation location in handle.Result)
        {
            if (location == null || string.IsNullOrWhiteSpace(location.PrimaryKey)) continue;
            if (!IsIllustrationAddress(location.PrimaryKey)) continue;

            queuedCount++;
            pendingLocationLoads++;
            AsyncOperationHandle<Sprite> spriteHandle = Addressables.LoadAssetAsync<Sprite>(location);
            spriteHandles.Add(spriteHandle);
            spriteHandle.Completed += OnIllustrationSpriteLoaded;
        }

        isLoaded = pendingLocationLoads == 0;
        Debug.Log($"Illustrations: queued {queuedCount} sprites from Addressables label '{IllustrationsLabel}'.");
    }

    private void OnIllustrationSpriteLoaded(AsyncOperationHandle<Sprite> handle)
    {
        if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
        {
            RegisterSpriteLookupKeys(handle.Result);
        }

        pendingLocationLoads = Mathf.Max(0, pendingLocationLoads - 1);
        if (pendingLocationLoads == 0)
        {
            isLoaded = true;
            Debug.Log($"Illustrations: loaded {illustrationsByName.Count} sprite lookup keys.");
        }
    }

    private int RegisterSpriteLookupKeys(Sprite sprite)
    {
        if (sprite == null) return 0;

        int added = 0;
        foreach (string key in EnumerateLookupKeys(sprite.name))
        {
            if (TryRegisterKey(key, sprite))
            {
                added++;
            }
        }

        // Fallback key: source texture asset name (usually filename).
        // This covers cases where Sprite.name was not updated after image rename.
        string textureName = sprite.texture != null ? sprite.texture.name : null;
        foreach (string key in EnumerateLookupKeys(textureName))
        {
            if (TryRegisterKey(key, sprite))
            {
                added++;
            }
        }

        return added;
    }

    public Sprite GetIllustrationByName(string name)
    {
        return GetIllustrationByName(name, true);
    }

    public Sprite GetIllustrationByName(string name, bool logMissing)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        if (!isLoaded)
        {
            if (!loggedNotReadyWarning)
            {
                Debug.LogWarning("Illustrations requested before Addressables load completed.");
                loggedNotReadyWarning = true;
            }
            return null;
        }

        if (TryGetIllustrationByName(name, out Sprite sprite))
        {
            return sprite;
        }

        if (logMissing)
        {
            Debug.LogWarning($"Sprite for {name} is not registered. Typo? Missing Addressables label '{IllustrationsLabel}'?");
        }
        return null;
    }

    public bool TryGetIllustrationByName(string name, out Sprite sprite)
    {
        sprite = null;
        if (string.IsNullOrWhiteSpace(name) || !isLoaded)
        {
            return false;
        }

        foreach (string key in EnumerateLookupKeys(name))
        {
            if (illustrationsByName.TryGetValue(key, out sprite))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsIllustrationAddress(string address)
    {
        if (string.IsNullOrWhiteSpace(address)) return false;
        for (int i = 0; i < IllustrationsAddressRoots.Length; i++)
        {
            if (address.StartsWith(IllustrationsAddressRoots[i]))
            {
                return true;
            }
        }
        return false;
    }

    public Sprite GetIllustrationByName(Character character)
    {
        if (character == null) return null;
        return GetIllustrationByName(character.characterName);
    }

    private bool TryRegisterKey(string normalizedKey, Sprite sprite)
    {
        if (string.IsNullOrWhiteSpace(normalizedKey) || sprite == null || illustrationsByName.ContainsKey(normalizedKey))
        {
            return false;
        }

        illustrationsByName[normalizedKey] = sprite;
        return true;
    }

    private IEnumerable<string> EnumerateLookupKeys(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
        {
            yield break;
        }

        HashSet<string> seen = new();

        foreach (string candidate in BuildNameCandidates(rawName))
        {
            string normalized = Normalize(candidate);
            if (!string.IsNullOrWhiteSpace(normalized) && seen.Add(normalized))
            {
                yield return normalized;
            }
        }
    }

    private IEnumerable<string> BuildNameCandidates(string rawName)
    {
        yield return rawName;
    }
}
