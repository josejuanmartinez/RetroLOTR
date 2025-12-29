using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class Music : MonoBehaviour
{
    public static Music Instance { get; private set; }

    [Header("Audio Sources")]
    public AudioSource musicAudioSource;
    public AudioSource ambientAudioSource;

    [Header("Music Clips")]
    public List<AudioClip> musicBattleClips = new();
    public List<AudioClip> musicBattleWonClips = new();
    public List<AudioClip> musicCitySmallClips = new();
    public List<AudioClip> musicCityBigClips = new();
    public List<AudioClip> musicForestClips = new();
    public List<AudioClip> musicGrasslandsClips = new();
    public List<AudioClip> musicPlainsClips = new();
    public List<AudioClip> musicHillsClips = new();
    public List<AudioClip> musicMountainsClips = new();
    public List<AudioClip> musicDesertClips = new();
    public List<AudioClip> musicSwampClips = new();
    public List<AudioClip> musicWastelandsClips = new();
    public List<AudioClip> musicShoreClips = new();
    public List<AudioClip> musicShallowWaterClips = new();
    public List<AudioClip> musicDeepWaterClips = new();
    public List<AudioClip> musicGenericClips = new();

    [Header("Ambient Clips")]
    public List<AudioClip> ambientForestClips = new();
    public List<AudioClip> ambientGrasslandsClips = new();
    public List<AudioClip> ambientPlainsClips = new();
    public List<AudioClip> ambientHillsClips = new();
    public List<AudioClip> ambientMountainsClips = new();
    public List<AudioClip> ambientDesertClips = new();
    public List<AudioClip> ambientSwampClips = new();
    public List<AudioClip> ambientWastelandsClips = new();
    public List<AudioClip> ambientShoreClips = new();
    public List<AudioClip> ambientShallowWaterClips = new();
    public List<AudioClip> ambientDeepWaterClips = new();
    public List<AudioClip> ambientCitySmallClips = new();
    public List<AudioClip> ambientCityBigClips = new();

    [Header("Playback")]
    public bool playOnStart = true;
    public float musicVolume = 0.5f;
    public float ambientVolume = 0.4f;
    public float maxVolume = 0.5f;
    public float crossfadeDuration = 1.5f;
    public float ambientFadeDuration = 1.0f;
    public float minSwitchSeconds = 6f;
    public float battleMusicHoldSeconds = 10f;

    private readonly Dictionary<string, AudioClip> stablePickByKey = new();
    private string currentMusicKey;
    private string currentAmbientKey;
    private TerrainEnum? currentAmbientTerrain;
    private float lastSwitchTime = -999f;
    private Coroutine musicFadeRoutine;
    private Coroutine ambientFadeRoutine;
    private bool eventActive;
    private AudioClip previousMusicClip;
    private float previousMusicTime;
    private bool previousMusicLoop;
    private Vector2Int lastContextHex = Vector2Int.one * -1;
    private float lastBattleMusicTime = -999f;
    private string lastBattleMusicKey;
    private AudioClip lastBattleClip;

    private void OnValidate()
    {
        EnsureAudioClipLists();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureAudioClipLists();
    }

    private void Start()
    {
        if (playOnStart)
        {
            SetContext(null, null, force: true, playAmbient: false);
        }
        if (ambientAudioSource != null)
        {
            ambientAudioSource.Stop();
        }
    }

    private void EnsureAudioClipLists()
    {
        var fields = GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        foreach (var field in fields)
        {
            if (field.FieldType == typeof(List<AudioClip>) && field.GetValue(this) == null)
            {
                field.SetValue(this, new List<AudioClip>());
            }
        }
    }

    public void UpdateForHex(Hex hex)
    {
        Game g = FindFirstObjectByType<Game>();
        if (g == null || !g.started)
        {
            SetContext(null, null, force: true, playAmbient: false);
            StopAmbient();
            return;
        }
        if (hex == null)
        {
            SetContext(null, null, force: true, playAmbient: false);
            StopAmbient();
            return;
        }
        if (eventActive) return;
        if (hex.v2 == lastContextHex) return;
        lastContextHex = hex.v2;
        var targetMusic = (TerrainEnum?)hex.terrainType;
        var targetAmbient = (TerrainEnum?)hex.terrainType;
        PC pc = hex.GetPC();
        bool battleOverride = IsBattleMusicActive();
        AudioClip cityMusicClip = PickCityMusicClip(pc);
        AudioClip cityAmbientClip = PickCityAmbientClip(pc);
        string cityAmbientKey = GetCityAmbientKey(pc);
        if (battleOverride && lastBattleClip != null)
        {
            SetContext(null, targetAmbient, playAmbient: true, musicOverride: lastBattleClip, musicOverrideKey: lastBattleMusicKey, ambientOverride: cityAmbientClip, ambientOverrideKey: cityAmbientKey);
            return;
        }

        if (cityMusicClip != null)
        {
            SetContext(null, targetAmbient, playAmbient: true, musicOverride: cityMusicClip, musicOverrideKey: GetCityMusicKey(pc), ambientOverride: cityAmbientClip, ambientOverrideKey: cityAmbientKey);
        }
        else
        {
            SetContext(targetMusic, targetAmbient, playAmbient: true, ambientOverride: cityAmbientClip, ambientOverrideKey: cityAmbientKey);
        }
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

    public void PlayBattleMusic()
    {
        var clip = PickStableClip(musicBattleClips, "music_battle");
        if (clip == null) clip = PickStableClip(musicGenericClips, "music_battle_generic");
        if (clip != null) CrossfadeMusic(clip);
        lastBattleClip = clip;
        lastBattleMusicKey = "music_battle";
        lastBattleMusicTime = Time.time;
    }

    public void PlayBattleWonMusic()
    {
        var clip = PickStableClip(musicBattleWonClips, "music_battle_won");
        if (clip == null) clip = PickStableClip(musicBattleClips, "music_battle");
        if (clip == null) clip = PickStableClip(musicGenericClips, "music_battle_generic");
        if (clip != null) CrossfadeMusic(clip);
        lastBattleClip = clip;
        lastBattleMusicKey = "music_battle_won";
        lastBattleMusicTime = Time.time;
    }

    private void SetContext(TerrainEnum? musicTerrain, TerrainEnum? ambientTerrain, bool force = false, bool playAmbient = true, AudioClip musicOverride = null, string musicOverrideKey = null, AudioClip ambientOverride = null, string ambientOverrideKey = null)
    {
        if (!force && Time.time - lastSwitchTime < minSwitchSeconds) return;

        string desiredMusicKey = musicOverrideKey ?? (musicTerrain.HasValue ? $"music_{musicTerrain.Value}" : "music_generic");
        if (desiredMusicKey != currentMusicKey || force)
        {
            var clip = musicOverride ?? PickMusicClip(musicTerrain);
            if (clip != null) CrossfadeMusic(clip);
            currentMusicKey = desiredMusicKey;
        }

        string desiredAmbientKey = ambientOverrideKey ?? (ambientTerrain.HasValue ? $"ambient_{ambientTerrain.Value}" : "ambient_none");
        if (playAmbient && (desiredAmbientKey != currentAmbientKey || force))
        {
            var clip = ambientOverride ?? PickStableClip(GetAmbientClips(ambientTerrain), $"ambient_{ambientTerrain}");
            if (clip != null) CrossfadeAmbient(clip);
            currentAmbientTerrain = ambientTerrain;
            currentAmbientKey = desiredAmbientKey;
        }

        lastSwitchTime = Time.time;
    }

    public void StopAmbient()
    {
        if (ambientAudioSource == null) return;
        ambientAudioSource.Stop();
        currentAmbientTerrain = null;
        currentAmbientKey = null;
    }

    private AudioClip PickStableClip(List<AudioClip> clips, string key)
    {
        if (clips == null || clips.Count == 0) return null;
        if (stablePickByKey.TryGetValue(key, out var cached) && cached != null) return cached;

        AudioClip chosen = null;
        for (int i = 0; i < clips.Count; i++)
        {
            var candidate = clips[UnityEngine.Random.Range(0, clips.Count)];
            if (candidate != null)
            {
                chosen = candidate;
                break;
            }
        }

        if (chosen == null)
        {
            foreach (var candidate in clips)
            {
                if (candidate != null)
                {
                    chosen = candidate;
                    break;
                }
            }
        }

        if (chosen != null)
        {
            stablePickByKey[key] = chosen;
        }
        return chosen;
    }

    private static string GetCityMusicKey(PC pc)
    {
        if (pc == null) return null;
        return (int)pc.citySize <= 2 ? "music_city_small" : "music_city_big";
    }

    private static string GetCityAmbientKey(PC pc)
    {
        if (pc == null) return null;
        return (int)pc.citySize <= 2 ? "ambient_city_small" : "ambient_city_big";
    }

    private AudioClip PickCityMusicClip(PC pc)
    {
        if (pc == null || pc.citySize == PCSizeEnum.NONE) return null;
        bool smallCity = (int)pc.citySize <= 2;
        var clips = smallCity ? musicCitySmallClips : musicCityBigClips;
        string key = smallCity ? "music_city_small" : "music_city_big";
        return PickStableClip(clips, key);
    }

    private AudioClip PickCityAmbientClip(PC pc)
    {
        if (pc == null || pc.citySize == PCSizeEnum.NONE) return null;
        bool smallCity = (int)pc.citySize <= 2;
        var clips = smallCity ? ambientCitySmallClips : ambientCityBigClips;
        string key = smallCity ? "ambient_city_small" : "ambient_city_big";
        return PickStableClip(clips, key);
    }

    private bool IsBattleMusicActive()
    {
        return Time.time - lastBattleMusicTime <= battleMusicHoldSeconds;
    }
    private AudioClip PickMusicClip(TerrainEnum? terrain)
    {
        if (terrain == null)
        {
            return PickStableClip(musicGenericClips, "music_generic");
        }

        var terrainClips = GetMusicClips(terrain.Value);
        if (terrainClips != null && terrainClips.Count > 0)
        {
            return PickStableClip(terrainClips, $"music_{terrain.Value}");
        }

        return PickStableClip(musicGenericClips, $"music_generic_{terrain.Value}");
    }

    private List<AudioClip> GetMusicClips(TerrainEnum terrain)
    {
        return terrain switch
        {
            TerrainEnum.forest => musicForestClips,
            TerrainEnum.grasslands => musicGrasslandsClips,
            TerrainEnum.plains => musicPlainsClips,
            TerrainEnum.hills => musicHillsClips,
            TerrainEnum.mountains => musicMountainsClips,
            TerrainEnum.desert => musicDesertClips,
            TerrainEnum.swamp => musicSwampClips,
            TerrainEnum.wastelands => musicWastelandsClips,
            TerrainEnum.shore => musicShoreClips,
            TerrainEnum.shallowWater => musicShallowWaterClips,
            TerrainEnum.deepWater => musicDeepWaterClips,
            _ => null
        };
    }

    private List<AudioClip> GetAmbientClips(TerrainEnum? terrain)
    {
        if (terrain == null) return null;
        return terrain.Value switch
        {
            TerrainEnum.forest => ambientForestClips,
            TerrainEnum.grasslands => ambientGrasslandsClips,
            TerrainEnum.plains => ambientPlainsClips,
            TerrainEnum.hills => ambientHillsClips,
            TerrainEnum.mountains => ambientMountainsClips,
            TerrainEnum.desert => ambientDesertClips,
            TerrainEnum.swamp => ambientSwampClips,
            TerrainEnum.wastelands => ambientWastelandsClips,
            TerrainEnum.shore => ambientShoreClips,
            TerrainEnum.shallowWater => ambientShallowWaterClips,
            TerrainEnum.deepWater => ambientDeepWaterClips,
            _ => null
        };
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

    private AudioClip PickEventClip()
    {
        return PickStableClip(musicGenericClips, "music_event");
    }
}
