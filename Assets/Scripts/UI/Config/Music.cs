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
    public AudioClip startingMusic;
    public List<AudioClip> musicBattleClips = new();
    public List<AudioClip> musicBattleWonClips = new();
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
    private AudioClip musicBeforeVideo;
    private float musicTimeBeforeVideo;
    private bool musicLoopBeforeVideo;
    private bool musicSuspendedForVideo;
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
        PlayStartingMusic();
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
        Game g = Game.Instance;
        if (g == null || !g.started)
        {
            StopAmbient();
            return;
        }
        if (hex == null)
        {
            SetContext(null, force: true, playAmbient: false);
            StopAmbient();
            return;
        }
        if (eventActive) return;
        if (hex.v2 == lastContextHex) return;
        lastContextHex = hex.v2;
        var targetAmbient = (TerrainEnum?)hex.terrainType;
        PC pc = hex.GetPC();
        bool battleOverride = IsBattleMusicActive();
        AudioClip cityAmbientClip = PickCityAmbientClip(pc);
        string cityAmbientKey = GetCityAmbientKey(pc);
        if (battleOverride && lastBattleClip != null)
        {
            SetContext(targetAmbient, playAmbient: true, musicOverride: lastBattleClip, musicOverrideKey: lastBattleMusicKey, ambientOverride: cityAmbientClip, ambientOverrideKey: cityAmbientKey);
            return;
        }

        SetContext(targetAmbient, playAmbient: true, ambientOverride: cityAmbientClip, ambientOverrideKey: cityAmbientKey);
    }

    public void StopMusicForVideo()
    {
        if (!musicSuspendedForVideo && musicAudioSource != null)
        {
            musicBeforeVideo = musicAudioSource.clip;
            musicTimeBeforeVideo = musicAudioSource.time;
            musicLoopBeforeVideo = musicAudioSource.loop;
            musicSuspendedForVideo = true;
        }
        if (musicFadeRoutine != null)
        {
            StopCoroutine(musicFadeRoutine);
            musicFadeRoutine = null;
        }
        musicAudioSource?.Stop();
    }

    public void RestoreMusicAfterVideo()
    {
        if (!musicSuspendedForVideo) return;
        musicSuspendedForVideo = false;

        AudioClip clip = musicBeforeVideo != null ? musicBeforeVideo : startingMusic;
        float startTime = musicBeforeVideo != null ? musicTimeBeforeVideo : 0f;
        bool loop = musicBeforeVideo != null ? musicLoopBeforeVideo : true;
        musicBeforeVideo = null;
        musicTimeBeforeVideo = 0f;

        if (clip != null)
        {
            CrossfadeMusic(clip, startTime, loop);
        }
    }

    public void PlayStartingMusic()
    {
        eventActive = false;
        previousMusicClip = null;
        AudioClip clip = startingMusic != null
            ? startingMusic
            : PickStableClip(musicGenericClips, "music_starting_fallback");
        if (clip == null) return;

        currentMusicKey = "music_starting";
        lastSwitchTime = Time.time;
        CrossfadeMusic(clip);
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

    private void SetContext(TerrainEnum? ambientTerrain, bool force = false, bool playAmbient = true, AudioClip musicOverride = null, string musicOverrideKey = null, AudioClip ambientOverride = null, string ambientOverrideKey = null)
    {
        if (!force && Time.time - lastSwitchTime < minSwitchSeconds) return;

        string desiredMusicKey = musicOverrideKey ?? "music_generic";
        if (desiredMusicKey != currentMusicKey || force)
        {
            var clip = musicOverride ?? PickMusicClip();
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

    private static string GetCityAmbientKey(PC pc)
    {
        if (pc == null) return null;
        return (int)pc.citySize <= 2 ? "ambient_city_small" : "ambient_city_big";
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
    private AudioClip PickMusicClip()
    {
        return PickStableClip(musicGenericClips, "music_generic");
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
