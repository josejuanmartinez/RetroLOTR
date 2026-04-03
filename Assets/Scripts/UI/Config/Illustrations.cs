using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;


public class Illustrations : SearcherByName
{
    private const string IllustrationsLabel = "default";
    private const string IllustrationsAddressRoot = "Assets/Art/Cards/";

    private Dictionary<string, Sprite> illustrationsByName = new();
    private AsyncOperationHandle<IList<IResourceLocation>> locationsHandle;
    private readonly List<AsyncOperationHandle<Sprite>> spriteHandles = new();
    private int pendingSpriteLoads;
    private bool isLoaded;
    private bool loggedNotReadyWarning;

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
        pendingSpriteLoads = 0;
        int queuedCount = 0;
        foreach (IResourceLocation location in handle.Result)
        {
            if (location == null || string.IsNullOrWhiteSpace(location.PrimaryKey)) continue;
            if (!location.PrimaryKey.StartsWith(IllustrationsAddressRoot)) continue;

            queuedCount++;
            pendingSpriteLoads++;
            AsyncOperationHandle<Sprite> spriteHandle = Addressables.LoadAssetAsync<Sprite>(location);
            spriteHandles.Add(spriteHandle);
            spriteHandle.Completed += OnIllustrationSpriteLoaded;
        }

        isLoaded = pendingSpriteLoads == 0;
        Debug.Log($"Illustrations: queued {queuedCount} card sprites from Addressables label '{IllustrationsLabel}'.");
    }

    private void OnIllustrationSpriteLoaded(AsyncOperationHandle<Sprite> handle)
    {
        if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
        {
            RegisterSpriteLookupKeys(handle.Result);
        }

        pendingSpriteLoads = Mathf.Max(0, pendingSpriteLoads - 1);
        if (pendingSpriteLoads == 0)
        {
            isLoaded = true;
        }
    }

    private int RegisterSpriteLookupKeys(Sprite sprite)
    {
        if (sprite == null) return 0;

        int added = 0;

        // Primary key: actual Sprite object name.
        string spriteKey = Normalize(sprite.name);
        if (!string.IsNullOrWhiteSpace(spriteKey) && !illustrationsByName.ContainsKey(spriteKey))
        {
            illustrationsByName[spriteKey] = sprite;
            added++;
        }

        // Fallback key: source texture asset name (usually filename).
        // This covers cases where Sprite.name was not updated after image rename.
        string textureName = sprite.texture != null ? sprite.texture.name : null;
        string textureKey = Normalize(textureName);
        if (!string.IsNullOrWhiteSpace(textureKey) && !illustrationsByName.ContainsKey(textureKey))
        {
            illustrationsByName[textureKey] = sprite;
            added++;
        }

        return added;
    }

    public Sprite GetIllustrationByName(string name)
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

        if (illustrationsByName.TryGetValue(Normalize(name), out Sprite sprite))
        {
            return sprite;
        }

        Debug.LogWarning($"Sprite for {name} is not registered. Typo? Missing Addressables label '{IllustrationsLabel}'?");
        return null;
    }

    public Sprite GetIllustrationByName(Character character)
    {
        if (character == null) return null;
        return GetIllustrationByName(character.characterName);
    }
}
