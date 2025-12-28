using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class Music : MonoBehaviour
{
    [Serializable]
    private class MusicCollection
    {
        public List<MusicEntry> music = new();
    }

    [Serializable]
    private class MusicEntry
    {
        public string path;
        public string suggestedUse;
        public bool used;
        public float durationSeconds;
    }

    private enum MusicTag
    {
        Title,
        World,
        Town,
        City,
        Overworld,
        Battle,
        Boss,
        FinalBoss,
        Dungeon,
        Desert,
        Jungle,
        Forest,
        Sea,
        Shrine,
        Chase,
        Credits,
        Hero,
        Evil,
        Bazaar,
        Parting,
        Downtime,
        Love,
        Victory,
        Generic
    }

    private enum AmbientTag
    {
        Forest,
        Water,
        Wind,
        Rain,
        Cave,
        Fire,
        Night,
        River,
        Waterfall,
        Generic
    }

    public static Music Instance { get; private set; }

    [Header("Audio Sources")]
    public AudioSource musicAudioSource;
    public AudioSource ambientAudioSource;

    [Header("Playback")]
    public bool playOnStart = true;
    public float musicVolume = 0.5f;
    public float ambientVolume = 0.4f;
    public float maxVolume = 0.5f;
    public float crossfadeDuration = 1.5f;
    public float ambientFadeDuration = 1.0f;
    public float minSwitchSeconds = 6f;

    private readonly Dictionary<string, AudioClip> clipByPath = new();
    private readonly Dictionary<MusicTag, List<AudioClip>> tracksByTag = new();
    private readonly Dictionary<AmbientTag, List<AudioClip>> ambientByTag = new();

    private MusicTag currentMusicTag = MusicTag.Generic;
    private AmbientTag currentAmbientTag = AmbientTag.Generic;
    private float lastSwitchTime = -999f;
    private Coroutine musicFadeRoutine;
    private Coroutine ambientFadeRoutine;
    private bool eventActive;
    private AudioClip previousMusicClip;
    private float previousMusicTime;
    private bool previousMusicLoop;
    private Vector2Int lastContextHex = Vector2Int.one * -1;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadUsedMusic();
    }

    private void Start()
    {
        if (playOnStart)
        {
            SetContext(MusicTag.Title, AmbientTag.Generic, force: true, playAmbient: false);
        }
        if (ambientAudioSource != null)
        {
            ambientAudioSource.Stop();
        }
    }

    public void UpdateForHex(Hex hex)
    {
        if (hex == null) return;
        Game g = FindFirstObjectByType<Game>();
        if (g == null || !g.started) return;
        if (eventActive) return;
        if (hex.v2 == lastContextHex) return;
        lastContextHex = hex.v2;
        var targetAmbient = DetermineAmbientTagForHex(hex);
        SetContext(currentMusicTag, targetAmbient, playAmbient: true);
    }

    public void PlayEventMusic()
    {
        if (eventActive || musicAudioSource == null) return;
        var clip = PickEventClip();
        if (clip == null) return;

        previousMusicClip = musicAudioSource.clip;
        previousMusicTime = musicAudioSource.time;
        previousMusicLoop = musicAudioSource.loop;

        eventActive = true;
        CrossfadeMusic(clip, 0f, true);
    }

    public void StopEventMusic()
    {
        if (!eventActive || musicAudioSource == null) return;
        eventActive = false;

        if (previousMusicClip == null)
        {
            musicAudioSource.Stop();
            return;
        }

        CrossfadeMusic(previousMusicClip, previousMusicTime, previousMusicLoop);
        previousMusicClip = null;
        previousMusicTime = 0f;
    }

    private void SetContext(MusicTag musicTag, AmbientTag ambientTag, bool force = false, bool playAmbient = true)
    {
        if (!force && Time.time - lastSwitchTime < minSwitchSeconds) return;

        if (musicTag != currentMusicTag || force)
        {
            var clip = PickRandom(tracksByTag, musicTag, MusicTag.Generic, musicAudioSource != null ? musicAudioSource.clip : null);
            if (clip != null) CrossfadeMusic(clip);
            currentMusicTag = musicTag;
        }

        if (playAmbient && (ambientTag != currentAmbientTag || force))
        {
            var clip = PickRandom(ambientByTag, ambientTag, AmbientTag.Generic, ambientAudioSource != null ? ambientAudioSource.clip : null);
            if (clip != null) CrossfadeAmbient(clip);
            currentAmbientTag = ambientTag;
        }

        lastSwitchTime = Time.time;
    }

    public void StopAmbient()
    {
        if (ambientAudioSource == null) return;
        ambientAudioSource.Stop();
        currentAmbientTag = AmbientTag.Generic;
    }

    private void LoadUsedMusic()
    {
        TextAsset jsonFile = Resources.Load<TextAsset>("music");
        if (jsonFile == null)
        {
            Debug.LogWarning("Music.json not found in Resources.");
            return;
        }

        MusicCollection collection = JsonUtility.FromJson<MusicCollection>(jsonFile.text);
        if (collection == null || collection.music == null) return;

        foreach (var entry in collection.music)
        {
            if (entry == null || !entry.used || string.IsNullOrWhiteSpace(entry.path)) continue;
            var clip = LoadClipByPath(entry.path);
            if (!clip) continue;

            clipByPath[entry.path] = clip;
            if (IsAmbient(entry.path))
            {
                var tag = ClassifyAmbient(entry.path);
                AddToMap(ambientByTag, tag, clip);
            }
            else
            {
                var tag = ClassifyMusic(entry.path);
                AddToMap(tracksByTag, tag, clip);
            }
        }
    }

    private static bool IsAmbient(string path)
    {
        return path.ToLowerInvariant().Contains("/nature sounds/");
    }

    private static MusicTag ClassifyMusic(string path)
    {
        string low = path.ToLowerInvariant();
        if (low.Contains("title")) return MusicTag.Title;
        if (low.Contains("credits")) return MusicTag.Credits;
        if (low.Contains("victory") || low.Contains("fanfare")) return MusicTag.Victory;
        if (low.Contains("final boss")) return MusicTag.FinalBoss;
        if (low.Contains("boss")) return MusicTag.Boss;
        if (low.Contains("battle")) return MusicTag.Battle;
        if (low.Contains("town")) return MusicTag.Town;
        if (low.Contains("city")) return MusicTag.City;
        if (low.Contains("castle")) return MusicTag.City;
        if (low.Contains("bazaar") || low.Contains("store")) return MusicTag.Bazaar;
        if (low.Contains("world map")) return MusicTag.World;
        if (low.Contains("overworld") || low.Contains("adventure") || low.Contains("journey")) return MusicTag.Overworld;
        if (low.Contains("dungeon") || low.Contains("cave")) return MusicTag.Dungeon;
        if (low.Contains("desert")) return MusicTag.Desert;
        if (low.Contains("jungle")) return MusicTag.Jungle;
        if (low.Contains("forest")) return MusicTag.Forest;
        if (low.Contains("sail") || low.Contains("sea") || low.Contains("island")) return MusicTag.Sea;
        if (low.Contains("shrine")) return MusicTag.Shrine;
        if (low.Contains("chase")) return MusicTag.Chase;
        if (low.Contains("hero")) return MusicTag.Hero;
        if (low.Contains("evil")) return MusicTag.Evil;
        if (low.Contains("parting")) return MusicTag.Parting;
        if (low.Contains("downtime")) return MusicTag.Downtime;
        if (low.Contains("friendship") || low.Contains("love")) return MusicTag.Love;
        return MusicTag.Generic;
    }

    private static AmbientTag ClassifyAmbient(string path)
    {
        string low = path.ToLowerInvariant();
        if (low.Contains("forest")) return AmbientTag.Forest;
        if (low.Contains("sea")) return AmbientTag.Water;
        if (low.Contains("river") || low.Contains("stream")) return AmbientTag.River;
        if (low.Contains("waterfall")) return AmbientTag.Waterfall;
        if (low.Contains("wind")) return AmbientTag.Wind;
        if (low.Contains("rain")) return AmbientTag.Rain;
        if (low.Contains("cave") || low.Contains("cavern")) return AmbientTag.Cave;
        if (low.Contains("fire")) return AmbientTag.Fire;
        if (low.Contains("night")) return AmbientTag.Night;
        return AmbientTag.Generic;
    }

    private static void AddToMap<T>(Dictionary<T, List<AudioClip>> map, T tag, AudioClip clip)
    {
        if (!map.TryGetValue(tag, out var list))
        {
            list = new List<AudioClip>();
            map[tag] = list;
        }
        list.Add(clip);
    }

    private static AudioClip PickRandom<T>(Dictionary<T, List<AudioClip>> map, T tag, T fallbackTag, AudioClip avoid)
    {
        if (!map.TryGetValue(tag, out var list) || list.Count == 0)
        {
            if (map.TryGetValue(fallbackTag, out var fallback) && fallback.Count > 0) return fallback[0];
            return null;
        }

        if (list.Count == 1) return list[0];
        for (int i = 0; i < 4; i++)
        {
            var candidate = list[UnityEngine.Random.Range(0, list.Count)];
            if (candidate != avoid) return candidate;
        }
        return list[0];
    }

    private void CrossfadeMusic(AudioClip clip)
    {
        CrossfadeMusic(clip, 0f, true);
    }

    private void CrossfadeAmbient(AudioClip clip)
    {
        if (ambientAudioSource == null) return;
        if (ambientFadeRoutine != null) StopCoroutine(ambientFadeRoutine);
        float volume = Mathf.Min(ambientVolume, maxVolume);
        ambientFadeRoutine = StartCoroutine(CrossfadeRoutine(ambientAudioSource, clip, ambientFadeDuration, true, 0f, volume));
    }

    private void CrossfadeMusic(AudioClip clip, float startTime, bool loop)
    {
        if (musicAudioSource == null) return;
        if (musicFadeRoutine != null) StopCoroutine(musicFadeRoutine);
        float volume = Mathf.Min(musicVolume, maxVolume);
        musicFadeRoutine = StartCoroutine(CrossfadeRoutine(musicAudioSource, clip, crossfadeDuration, loop, startTime, volume));
    }

    private IEnumerator CrossfadeRoutine(AudioSource source, AudioClip nextClip, float duration, bool loop, float startTime, float targetVolume)
    {
        if (source.clip == nextClip && source.isPlaying) yield break;

        float startVolume = source.volume;
        if (source.isPlaying)
        {
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                source.volume = Mathf.Lerp(startVolume, 0f, t / duration);
                yield return null;
            }
        }

        source.Stop();
        source.clip = nextClip;
        source.loop = loop;
        source.volume = 0f;
        if (nextClip != null)
        {
            float clampedTime = Mathf.Clamp(startTime, 0f, Mathf.Max(0f, nextClip.length - 0.01f));
            source.time = clampedTime;
        }
        source.Play();

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            source.volume = Mathf.Lerp(0f, targetVolume, t / duration);
            yield return null;
        }
        source.volume = targetVolume;
    }

    private static AudioClip LoadClipByPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(path);
#else
        string resourcePath = ToResourcesPath(path);
        if (string.IsNullOrWhiteSpace(resourcePath)) return null;
        return Resources.Load<AudioClip>(resourcePath);
#endif
    }

    private static string ToResourcesPath(string assetPath)
    {
        string normalized = assetPath.Replace("\\", "/");
        int resourcesIndex = normalized.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase);
        if (resourcesIndex < 0) return null;

        string relative = normalized[(resourcesIndex + "/Resources/".Length)..];
        return Path.ChangeExtension(relative, null);
    }

    private static MusicTag DetermineMusicTagForHex(Hex hex)
    {
        if (hex == null) return MusicTag.Generic;

        var pc = hex.GetPC();
        if (pc != null)
        {
            if (pc.citySize >= PCSizeEnum.city) return MusicTag.City;
            if (pc.citySize >= PCSizeEnum.town) return MusicTag.Town;
        }

        if (hex.IsWaterTerrain()) return MusicTag.Sea;
        return hex.terrainType switch
        {
            TerrainEnum.forest => MusicTag.Forest,
            TerrainEnum.desert => MusicTag.Desert,
            TerrainEnum.wastelands => MusicTag.Evil,
            TerrainEnum.mountains => MusicTag.Shrine,
            TerrainEnum.hills => MusicTag.Shrine,
            TerrainEnum.swamp => MusicTag.Dungeon,
            TerrainEnum.shore => MusicTag.Sea,
            TerrainEnum.deepWater => MusicTag.Sea,
            TerrainEnum.shallowWater => MusicTag.Sea,
            _ => MusicTag.Overworld
        };
    }

    private static AmbientTag DetermineAmbientTagForHex(Hex hex)
    {
        if (hex == null) return AmbientTag.Generic;

        if (hex.IsWaterTerrain()) return AmbientTag.Water;
        return hex.terrainType switch
        {
            TerrainEnum.forest => AmbientTag.Forest,
            TerrainEnum.desert => AmbientTag.Wind,
            TerrainEnum.wastelands => AmbientTag.Wind,
            TerrainEnum.mountains => AmbientTag.Wind,
            TerrainEnum.hills => AmbientTag.Wind,
            TerrainEnum.swamp => AmbientTag.Night,
            TerrainEnum.shore => AmbientTag.Water,
            _ => AmbientTag.Generic
        };
    }

    private AudioClip PickEventClip()
    {
        var priorities = new[]
        {
            MusicTag.Downtime,
            MusicTag.Love,
            MusicTag.Parting,
            MusicTag.World,
            MusicTag.Generic
        };

        foreach (var tag in priorities)
        {
            if (tracksByTag.TryGetValue(tag, out var list) && list.Count > 0)
            {
                return list[UnityEngine.Random.Range(0, list.Count)];
            }
        }

        return null;
    }
}
