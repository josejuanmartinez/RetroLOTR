using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;


public class Illustrations : SearcherByName
{
    private const string IllustrationsLabel = "default";
    private const string CardArtAddressRoot = "Assets/Art/Cards/";
    private const string DeckArtAddressRoot = "Assets/Art/Decks/";
    private const string CharacterArtAddressRoot = "Assets/Art/Cards/Characters/";
    private static readonly string[] IllustrationsAddressRoots =
    {
        "Assets/Art/Cards/",
        "Assets/Art/UI/",
        "Assets/Art/UI/",
        "Assets/Art/Animation/",
        "Assets/Art/Characters/",
        DeckArtAddressRoot
    };

    private Dictionary<string, Sprite> illustrationsByName = new();
    private Dictionary<string, Sprite> cardArtByName = new();
    private Dictionary<string, Sprite> characterArtByName = new();
    // Deck-back art (Assets/Art/Decks) is keyed separately from the general illustrations
    // dictionary: nation/variant names like "Gandalf" or "Sharkey" are already claimed by
    // character portrait cards there, so a shared dictionary would silently hand back the
    // wrong sprite depending on load order instead of the deck-back art callers actually want.
    private Dictionary<string, Sprite> deckArtByName = new();
    private AsyncOperationHandle<IList<IResourceLocation>> locationsHandle;
    private readonly List<AsyncOperationHandle<Sprite>> spriteHandles = new();
    private int pendingLocationLoads;
    private int totalLocationLoads;
    private bool locationScanFinished;
    private bool isLoaded;
    private bool loggedNotReadyWarning;

    public bool IsLoaded => isLoaded;
    public float LoadProgress => isLoaded
        ? 1f
        : !locationScanFinished || totalLocationLoads <= 0
            ? 0.05f
            : 1f - (float)pendingLocationLoads / totalLocationLoads;

    private void Awake()
    {
        StartCoroutine(BeginLoadingAfterFirstFrame());
    }

    private IEnumerator BeginLoadingAfterFirstFrame()
    {
        // Give StartupLoadingScreen one rendered frame before queuing 1,000+ Addressables.
        yield return null;
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
            locationScanFinished = true;
            illustrationsByName = new Dictionary<string, Sprite>();
            cardArtByName = new Dictionary<string, Sprite>();
            characterArtByName = new Dictionary<string, Sprite>();
            deckArtByName = new Dictionary<string, Sprite>();
            // Complete in a degraded state so a failed Addressables label cannot leave the
            // full-screen startup blocker up forever. The error remains explicit in the log.
            isLoaded = true;
            Debug.LogError($"Illustrations: failed to load Addressables label '{IllustrationsLabel}'.");
            return;
        }

        illustrationsByName = new Dictionary<string, Sprite>();
        cardArtByName = new Dictionary<string, Sprite>();
        characterArtByName = new Dictionary<string, Sprite>();
        deckArtByName = new Dictionary<string, Sprite>();
        pendingLocationLoads = 0;
        locationScanFinished = true;
        int queuedCount = 0;
        foreach (IResourceLocation location in handle.Result)
        {
            if (location == null || string.IsNullOrWhiteSpace(location.PrimaryKey)) continue;
            if (!IsIllustrationAddress(location.PrimaryKey)) continue;

            bool isDeckArt = location.PrimaryKey.StartsWith(DeckArtAddressRoot);
            bool isCardArt = location.PrimaryKey.StartsWith(CardArtAddressRoot);
            bool isCharacterArt = location.PrimaryKey.StartsWith(CharacterArtAddressRoot);
            queuedCount++;
            pendingLocationLoads++;
            AsyncOperationHandle<Sprite> spriteHandle = Addressables.LoadAssetAsync<Sprite>(location);
            spriteHandles.Add(spriteHandle);
            spriteHandle.Completed += completedHandle => OnIllustrationSpriteLoaded(completedHandle, isDeckArt, isCardArt, isCharacterArt);
        }

        isLoaded = pendingLocationLoads == 0;
        totalLocationLoads = queuedCount;
        Debug.Log($"Illustrations: queued {queuedCount} sprites from Addressables label '{IllustrationsLabel}'.");
    }

    private void OnIllustrationSpriteLoaded(AsyncOperationHandle<Sprite> handle, bool isDeckArt, bool isCardArt, bool isCharacterArt)
    {
        if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
        {
            RegisterSpriteLookupKeys(handle.Result, isDeckArt ? deckArtByName : illustrationsByName);
            if (isCardArt)
            {
                RegisterSpriteLookupKeys(handle.Result, cardArtByName);
            }
            if (isCharacterArt)
            {
                RegisterSpriteLookupKeys(handle.Result, characterArtByName);
            }
        }

        pendingLocationLoads = Mathf.Max(0, pendingLocationLoads - 1);
        if (pendingLocationLoads == 0)
        {
            isLoaded = true;
            Debug.Log($"Illustrations: loaded {illustrationsByName.Count} sprite lookup keys, {characterArtByName.Count} character-art keys, {deckArtByName.Count} deck-art keys.");
        }
    }

    private int RegisterSpriteLookupKeys(Sprite sprite, Dictionary<string, Sprite> target)
    {
        if (sprite == null) return 0;

        int added = 0;
        foreach (string key in EnumerateLookupKeys(sprite.name))
        {
            if (TryRegisterKey(key, sprite, target))
            {
                added++;
            }
        }

        foreach (string key in EnumerateLookupKeys(StripSubSpriteSuffix(sprite.name)))
        {
            if (TryRegisterKey(key, sprite, target))
            {
                added++;
            }
        }

        // Fallback key: source texture asset name (usually filename).
        // This covers cases where Sprite.name was not updated after image rename.
        string textureName = sprite.texture != null ? sprite.texture.name : null;
        foreach (string key in EnumerateLookupKeys(textureName))
        {
            if (TryRegisterKey(key, sprite, target))
            {
                added++;
            }
        }

        foreach (string key in EnumerateLookupKeys(StripSubSpriteSuffix(textureName)))
        {
            if (TryRegisterKey(key, sprite, target))
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

    public bool TryGetCardArtByName(string name, out Sprite sprite)
    {
        sprite = null;
        if (string.IsNullOrWhiteSpace(name) || !isLoaded) return false;

        foreach (string key in EnumerateLookupKeys(name))
        {
            if (cardArtByName.TryGetValue(key, out sprite)) return true;
        }
        return false;
    }

    public Sprite GetCharacterArtByName(string name, bool logMissing = true)
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

        foreach (string key in EnumerateLookupKeys(name))
        {
            if (characterArtByName.TryGetValue(key, out Sprite sprite))
            {
                return sprite;
            }
        }

        if (logMissing)
        {
            Debug.LogWarning($"Character art for {name} is not registered under '{CharacterArtAddressRoot}'.");
        }
        return null;
    }

    // Deck-back fanned-card art (Assets/Art/Decks), looked up by deckId/subdeckId
    // (e.g. "mithrandir", "the_necromancer") rather than character/portrait name — see
    // deckArtByName's declaration for why this can't share the general lookup.
    public Sprite GetDeckArtByName(string name, bool logMissing = true)
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

        if (TryGetDeckArtByName(name, out Sprite sprite))
        {
            return sprite;
        }

        if (logMissing)
        {
            Debug.LogWarning($"Deck art for {name} is not registered. Typo? Missing Addressables label '{IllustrationsLabel}'?");
        }
        return null;
    }

    public bool TryGetDeckArtByName(string name, out Sprite sprite)
    {
        sprite = null;
        if (string.IsNullOrWhiteSpace(name) || !isLoaded)
        {
            return false;
        }

        foreach (string key in EnumerateLookupKeys(name))
        {
            if (deckArtByName.TryGetValue(key, out sprite))
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

        // Character display names and their shipped art names are not always identical.
        // Variants such as Sharkey use "Saruman-Sharkey" as the card-art key, stored in
        // illustrationName when the character card is applied.
        if (!string.IsNullOrWhiteSpace(character.illustrationName))
        {
            Sprite illustration = GetIllustrationByName(character.illustrationName, false);
            if (illustration != null) return illustration;
        }

        Sprite namedIllustration = GetIllustrationByName(character.characterName, false);
        if (namedIllustration != null) return namedIllustration;

        if (!string.IsNullOrWhiteSpace(character.SpriteVariantBaseName))
        {
            Sprite baseIllustration = GetIllustrationByName(character.SpriteVariantBaseName, false);
            if (baseIllustration != null) return baseIllustration;
        }

        return GetIllustrationByName(character.characterName);
    }

    private bool TryRegisterKey(string normalizedKey, Sprite sprite, Dictionary<string, Sprite> target)
    {
        if (string.IsNullOrWhiteSpace(normalizedKey) || sprite == null || target.ContainsKey(normalizedKey))
        {
            return false;
        }

        target[normalizedKey] = sprite;
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

    private string StripSubSpriteSuffix(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName)) return rawName;

        int underscoreIndex = rawName.LastIndexOf('_');
        if (underscoreIndex < 0 || underscoreIndex == rawName.Length - 1) return rawName;

        bool suffixIsNumeric = true;
        for (int i = underscoreIndex + 1; i < rawName.Length; i++)
        {
            if (!char.IsDigit(rawName[i]))
            {
                suffixIsNumeric = false;
                break;
            }
        }

        return suffixIsNumeric ? rawName.Substring(0, underscoreIndex) : rawName;
    }
}
