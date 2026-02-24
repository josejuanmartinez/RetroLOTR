using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;


public class Illustrations : SearcherByName
{
    private const string IllustrationsLabel = "default";

    private Dictionary<string, Sprite> illustrationsByName = new();
    private AsyncOperationHandle<IList<Sprite>> loadHandle;
    private bool isLoaded;
    private bool loggedNotReadyWarning;

    private void Awake()
    {
        loadHandle = Addressables.LoadAssetsAsync<Sprite>(IllustrationsLabel, null);
        loadHandle.Completed += OnIllustrationsLoaded;
    }

    private void OnDestroy()
    {
        if (loadHandle.IsValid())
        {
            Addressables.Release(loadHandle);
        }
    }

    private void OnIllustrationsLoaded(AsyncOperationHandle<IList<Sprite>> handle)
    {
        if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
        {
            illustrationsByName = new Dictionary<string, Sprite>();
            isLoaded = false;
            Debug.LogError($"Illustrations: failed to load Addressables label '{IllustrationsLabel}'.");
            return;
        }

        illustrationsByName = new Dictionary<string, Sprite>();
        int loadedCount = 0;
        foreach (Sprite sprite in handle.Result)
        {
            if (sprite == null) continue;
            string key = Normalize(sprite.name);
            if (!illustrationsByName.ContainsKey(key))
            {
                illustrationsByName[key] = sprite;
                loadedCount++;
            }
        }

        isLoaded = true;
        Debug.Log($"Illustrations: loaded {loadedCount} sprites from Addressables label '{IllustrationsLabel}'.");
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
