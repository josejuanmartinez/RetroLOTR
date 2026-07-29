using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

// Resolves the baked per-facing spritesheets/atlases produced by the Animation Spritesheet
// Baker (see Assets/Editor/AnimationSpritesheetBaker.cs) for a given character, walking the
// same name -> race -> fallback tiers CharacterAnimatorControllers used for the Animator-driven
// system this replaces. Layout on disk (see AnimationSpritesheetBaker._outFolder / SaveAtlas):
//   Assets/Art/Characters/AnimationSpritesheets/[RaceOrName]/[RaceOrName]_[Back|Left|Forward|Right].png
//   Assets/Art/Characters/AnimationSpritesheets/[RaceOrName]/[RaceOrName]_[Back|Left|Forward|Right].atlas.json
// The atlas manifest TextAssets must be marked Addressable with the "default" label — that's
// what the initial scan finds them by (AnimationSpritesheetBaker does this automatically for
// anything it bakes from now on). The sliced frame Sprites don't need that scan at all: each
// manifest's own "texture" field gives the exact address of its PNG, which is loaded directly
// via Addressables.LoadAssetAsync<IList<Sprite>> — it just needs to BE Addressable (any label).
public static class CharacterSpritesheets
{
    private const string SpritesheetsLabel = "default";
    public const string AddressRoot = "Assets/Art/Characters/AnimationSpritesheets/";
    private static readonly string[] Facings = { "Forward", "Back", "Left", "Right" };

    [System.Serializable]
    public class AtlasFrame
    {
        public int index;
        public int x, y, width, height;
    }

    [System.Serializable]
    public class AtlasState
    {
        public string name;
        public string spriteNamePrefix;
        public int row;
        public int frameCount;
        public bool loop;
        public float clipLength;
        public float fps;
        public List<AtlasFrame> frames;
    }

    [System.Serializable]
    public class AtlasManifest
    {
        public string texture;
        public int textureWidth, textureHeight;
        public int cellWidth, cellHeight;
        public int framesPerState;
        public List<AtlasState> states;
    }

    // Keyed by normalized "{raceOrName}_{facing}", e.g. "gandalf_forward".
    private static readonly Dictionary<string, AtlasManifest> manifestsByKey = new();
    // Every baked frame Sprite, keyed by its exact sub-asset name (e.g. "Gandalf_Forward_Action_00").
    private static readonly Dictionary<string, Sprite> spritesByName = new();
    // Every raceOrName folder with at least one baked facing, lowercased.
    private static readonly HashSet<string> availableRaceOrNames = new();

    private static bool loadStarted;
    private static bool manifestLocationsScanned;
    private static int pendingManifestLoads;
    private static int pendingSpriteLoads;

    // manifestLocationsScanned guards against a false "loaded" reading in the gap between the
    // initial location scan completing and the first manifest's OnManifestLoaded callback
    // actually running (which is what increments pendingSpriteLoads) — without it, IsLoaded
    // could read true for one frame while pendingManifestLoads/pendingSpriteLoads are still both
    // sitting at their pre-scan value of 0.
    public static bool IsLoaded => manifestLocationsScanned && pendingManifestLoads == 0 && pendingSpriteLoads == 0;

    public static void EnsureLoading()
    {
        if (loadStarted) return;
        loadStarted = true;
        Addressables.LoadResourceLocationsAsync(SpritesheetsLabel, typeof(TextAsset)).Completed += OnManifestLocationsLoaded;
    }

    private static void OnManifestLocationsLoaded(AsyncOperationHandle<IList<IResourceLocation>> handle)
    {
        if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
        {
            manifestLocationsScanned = true;
            Debug.LogError($"CharacterSpritesheets: failed to load Addressables label '{SpritesheetsLabel}' for atlas manifests.");
            return;
        }

        pendingManifestLoads = 0;
        foreach (IResourceLocation location in handle.Result)
        {
            if (location == null || string.IsNullOrWhiteSpace(location.PrimaryKey)) continue;
            if (!location.PrimaryKey.StartsWith(AddressRoot) || !location.PrimaryKey.EndsWith(".atlas.json")) continue;

            pendingManifestLoads++;
            Addressables.LoadAssetAsync<TextAsset>(location).Completed += OnManifestLoaded;
        }
        manifestLocationsScanned = true;
    }

    private static void OnManifestLoaded(AsyncOperationHandle<TextAsset> handle)
    {
        if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
        {
            RegisterManifest(handle.Result);
        }

        pendingManifestLoads = Mathf.Max(0, pendingManifestLoads - 1);
        if (pendingManifestLoads == 0)
        {
            Debug.Log($"CharacterSpritesheets: loaded {manifestsByKey.Count} atlas manifest(s), {pendingSpriteLoads} spritesheet(s) still loading.");
        }
    }

    private static void RegisterManifest(TextAsset asset)
    {
        AtlasManifest manifest = JsonUtility.FromJson<AtlasManifest>(asset.text);
        if (manifest?.states == null) return;

        // Unity's importer strips only the final ".json", so a source file named
        // "Gandalf_Forward.atlas.json" becomes a TextAsset named "Gandalf_Forward.atlas" — strip
        // the remaining ".atlas" too before splitting off the facing suffix.
        string stem = asset.name.EndsWith(".atlas") ? asset.name.Substring(0, asset.name.Length - ".atlas".Length) : asset.name;
        if (!TrySplitFacing(stem, out string raceOrName, out string facing)) return;

        manifestsByKey[NormalizeKey(raceOrName, facing)] = manifest;
        // availableRaceOrNames is populated once the actual sprite FRAMES load (see
        // OnSpriteSheetLoaded below), not here — the manifest alone tells TryResolveRaceOrName
        // "this character resolves," but GetSprite(...) would still return null until the frame
        // texture itself finishes its own, separate (and typically much slower) Addressables
        // load, leaving the Hex-drawn card-illustration fallback stuck on screen for that gap.

        // Addressables.LoadResourceLocationsAsync(label, typeof(Sprite)) does NOT reliably
        // enumerate the individual sliced sub-sprites of a multi-sprite texture as separate
        // locations — in practice it only ever surfaced one location per PNG here (confirmed:
        // exactly as many "Sprite" locations as baked sheets, never the ~192 frames each sheet
        // actually contains). Loading IList<Sprite> against the TEXTURE's own address is the
        // documented way to pull every sub-sprite of one multi-sprite texture at once, and since
        // manifest.texture already tells us that texture's filename, no separate scan is needed.
        if (string.IsNullOrEmpty(manifest.texture)) return;
        string pngAddress = $"{AddressRoot}{raceOrName}/{manifest.texture}";
        pendingSpriteLoads++;
        Addressables.LoadAssetAsync<IList<Sprite>>(pngAddress).Completed += handle => OnSpriteSheetLoaded(handle, raceOrName);
    }

    private static void OnSpriteSheetLoaded(AsyncOperationHandle<IList<Sprite>> handle, string raceOrName)
    {
        if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
        {
            foreach (Sprite sprite in handle.Result)
                if (sprite != null) spritesByName[sprite.name] = sprite;
            // Only NOW is this raceOrName actually safe to resolve to — GetSprite(...) can
            // genuinely return a frame for it from this point on.
            availableRaceOrNames.Add(raceOrName.ToLowerInvariant());
        }
        else
        {
            Debug.LogError($"CharacterSpritesheets: failed to load sprite frames for '{handle.DebugName}'.");
        }

        pendingSpriteLoads = Mathf.Max(0, pendingSpriteLoads - 1);
        if (pendingSpriteLoads == 0 && pendingManifestLoads == 0)
        {
            Debug.Log($"CharacterSpritesheets: loaded {spritesByName.Count} sprite frame(s) total.");
        }
    }

    private static bool TrySplitFacing(string stem, out string raceOrName, out string facing)
    {
        foreach (string candidate in Facings)
        {
            string suffix = "_" + candidate;
            if (stem.EndsWith(suffix))
            {
                raceOrName = stem.Substring(0, stem.Length - suffix.Length);
                facing = candidate;
                return true;
            }
        }
        raceOrName = null;
        facing = null;
        return false;
    }

    private static string NormalizeKey(string raceOrName, string facing) =>
        $"{raceOrName.Trim().ToLowerInvariant()}_{facing.Trim().ToLowerInvariant()}";

    // Tries characterName, then (if this is a leader variant) the base leader name it was
    // transformed from, then race, then fallback (in that order), returning the first one with
    // at least one baked facing. The specific facing requested later via GetManifest can still
    // come back null if only some of that raceOrName's facings were baked.
    public static bool TryResolveRaceOrName(string characterName, string variantBaseName, RacesEnum race, string fallback, out string raceOrName)
    {
        EnsureLoading();
        if (HasAny(characterName)) { raceOrName = characterName; return true; }
        if (HasAny(variantBaseName)) { raceOrName = variantBaseName; return true; }
        if (HasAny(race.ToString())) { raceOrName = race.ToString(); return true; }
        // Manifests register incrementally as each one finishes loading, not all at once — so
        // mid-load, the fallback's sheet can already be registered while this character's own
        // name/race sheet simply hasn't arrived yet. Committing to fallback in that window would
        // visibly show the wrong character (e.g. every character briefly rendering as "Gandalf"
        // if that's what fallback happens to be set to) instead of waiting the extra moment for
        // its own match. Only trust fallback once loading is fully done and nothing better exists.
        if (IsLoaded && HasAny(fallback)) { raceOrName = fallback; return true; }
        raceOrName = null;
        return false;
    }

    private static bool HasAny(string raceOrName) =>
        !string.IsNullOrWhiteSpace(raceOrName) && availableRaceOrNames.Contains(raceOrName.Trim().ToLowerInvariant());

    public static AtlasManifest GetManifest(string raceOrName, string facing) =>
        manifestsByKey.TryGetValue(NormalizeKey(raceOrName, facing), out AtlasManifest manifest) ? manifest : null;

    public static Sprite GetSprite(string exactName) =>
        spritesByName.TryGetValue(exactName, out Sprite sprite) ? sprite : null;
}
