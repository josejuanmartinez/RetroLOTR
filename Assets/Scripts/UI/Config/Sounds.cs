using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class Sounds : SearcherByName
{
    [Serializable]
    private class SoundCollection
    {
        public List<SoundEntry> sounds = new();
    }

    [Serializable]
    private class SoundEntry
    {
        public string path;
        public string category;
        public string suggestedUse;
        public bool used;
        public float durationSeconds;
    }

    private class SoundRuntime
    {
        public SoundEntry entry;
        public AudioClip clip;
        public HashSet<string> tokens = new();
        public string key;
        public string category;
    }

    private class VoiceSet
    {
        public AudioClip expression;
        public AudioClip attack;
        public AudioClip effort;
        public AudioClip pain;
    }

    public static Sounds Instance { get; private set; }

    [Header("Audio")]
    public List<AudioClip> sounds = new();
    public AudioSource soundAudioSource;

    [Header("Queue")]
    public int maxQueueSize = 8;
    public float globalMinInterval = 0.05f;

    private readonly Dictionary<string, AudioClip> clipByPath = new();
    private readonly Dictionary<string, SoundRuntime> runtimeByKey = new();
    private readonly Dictionary<string, List<SoundRuntime>> runtimeByCategory = new();
    private readonly List<SoundRuntime> allRuntime = new();

    private readonly Queue<SfxRequest> queue = new();
    private readonly Dictionary<string, float> lastPlayByKey = new();
    private float lastPlayTime = -999f;
    private Coroutine queueRoutine;
    private bool speechActive;
    private float speechEndTime;
    private float lastFootstepTime = -999f;
    private readonly Dictionary<int, VoiceSet> voiceSetByCharacterId = new();

    [Header("Movement SFX")]
    public float footstepMinInterval = 0.12f;
    public float footstepVolume = 1.6f;
    public float voiceVolume = 2.0f;

    private const string CategoryUi = "10_ui_menu_sfx";
    private const string CategoryButtons = "button_clicks";
    private const string CategoryMenu = "menu sounds";
    private const string CategoryPops = "pops and jingles";
    private const string CategorySpecialPops = "special pops";
    private const string CategoryMovement = "12_player_movement_sfx";
    private const string CategoryFootsteps = "footsteps - essentials";
    private const string CategoryWeapons = "weapons";
    private const string CategoryBattle = "10_battle_sfx";
    private const string CategoryMagic = "8_atk_magic_sfx";
    private const string CategoryBuffs = "8_buffs_heals_sfx";
    private const string CategoryCountdown = "countdown";
    private const string CategoryClock = "ticking clock";
    private const string CategoryCoin = "coin";
    private const string CategoryKeys = "keys";
    private const string CategoryJingles = "pops and jingles";
    private const string CategorySplat = "splat_splash_squish";
    private const string CategoryVoices = "voices sfx";

    private struct SfxRequest
    {
        public AudioClip clip;
        public float volume;
        public float minInterval;
        public string key;
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
        if (soundAudioSource == null)
        {
            Game g = FindFirstObjectByType<Game>();
            if (g != null) soundAudioSource = g.soundPlayer;
        }
        LoadUsedSounds();
        if (soundAudioSource != null && soundAudioSource.volume < 1f)
        {
            soundAudioSource.volume = 1f;
            soundAudioSource.spatialBlend = 0f;
        }
    }

    public AudioClip GetSoundByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        string key = Normalize(name);
        if (runtimeByKey.TryGetValue(key, out var runtime)) return runtime.clip;

        AudioClip sound = sounds.Find(x => Normalize(x.name) == key);
        if (!sound) Debug.LogWarning($"Sound for {name} is not registered. Typo? Forgot to add it?");
        return sound;
    }

    public void StopAllSounds()
    {
        if (soundAudioSource != null) soundAudioSource.Stop();
        queue.Clear();
        speechActive = false;
        speechEndTime = 0f;
    }

    public void PlayUiHover()
    {
        EnqueueByKeywords(new[] { "hover", "over", "select", "focus" }, CategoryUi, 0.08f, 0.7f);
    }

    public void PlayUiClick()
    {
        EnqueueByKeywords(new[] { "click", "press", "select", "confirm" }, CategoryButtons, 0.06f, 0.8f);
    }

    public void PlayUiDenied()
    {
        EnqueueByKeywords(new[] { "denied", "error", "fail", "cancel" }, CategoryUi, 0.2f, 0.9f);
    }

    public void PlayPositive()
    {
        EnqueueByKeywords(new[] { "success", "win", "complete", "jingle", "reward", "gold" }, CategoryJingles, 0.2f, 0.85f);
    }

    public void PlayNegative()
    {
        EnqueueByKeywords(new[] { "fail", "denied", "error", "lose", "negative" }, CategoryUi, 0.25f, 0.9f);
    }

    public void PlaySpeechIntro(AlignmentEnum alignment)
    {
        if (speechActive && Time.time < speechEndTime) return;
        var clip = GetSoundByName($"{alignment}_intro");
        if (!clip || soundAudioSource == null) return;

        queue.Clear();
        if (queueRoutine != null) StopCoroutine(queueRoutine);
        soundAudioSource.Stop();

        speechActive = true;
        speechEndTime = Time.time + Mathf.Max(0f, clip.length);

        soundAudioSource.PlayOneShot(clip, 1.0f);
        string key = Normalize(clip.name);
        lastPlayByKey[key] = Time.time;
        lastPlayTime = Time.time;
    }

    public void PlayVoiceForRace(RacesEnum race)
    {
        string[] keywords = race switch
        {
            RacesEnum.Orc => new[] { "orc" },
            RacesEnum.Troll => new[] { "troll" },
            RacesEnum.Goblin => new[] { "goblin" },
            RacesEnum.Nazgul => new[] { "nazgul" },
            RacesEnum.Undead => new[] { "undead", "zombie" },
            RacesEnum.Dragon => new[] { "dragon", "dinosaur" },
            RacesEnum.Spider => new[] { "spider" },
            RacesEnum.Balrog => new[] { "demon", "balrog" },
            RacesEnum.Eagle => new[] { "beast", "creature", "dinosaur" },
            RacesEnum.Ent => new[] { "beast", "creature" },
            RacesEnum.Beorning => new[] { "beast", "creature" },
            RacesEnum.Wose => new[] { "beast", "creature" },
            RacesEnum.Maia => new[] { "humanoid", "voice" },
            _ => new[] { "humanoid", "voice", "male", "female" }
        };

        var clip = FindClipByKeywords(keywords, CategoryVoices);
        if (!clip) clip = FindAnyFromCategory(CategoryVoices);
        if (!clip) return;
        Enqueue(clip, voiceVolume, 0.3f);
    }

    public void PlayVoiceExpression(Character character)
    {
        if (character == null) return;
        if (speechActive && Time.time < speechEndTime) return;
        if (soundAudioSource == null) return;
        if (!IsHumanoidRace(character.race)) return;
        if (!PlayerCanSeeHex(character.hex)) return;

        if (character.health < 50)
        {
            PlayVoicePain(character);
            return;
        }

        var voiceSet = GetOrCreateVoiceSet(character);
        if (voiceSet == null || !voiceSet.expression) return;
        Enqueue(voiceSet.expression, voiceVolume, 0.5f);
    }

    public void PlayVoiceAttack(Character character)
    {
        if (character == null) return;
        if (speechActive && Time.time < speechEndTime) return;
        if (soundAudioSource == null) return;
        if (!IsHumanoidRace(character.race)) return;
        if (!PlayerCanSeeHex(character.hex)) return;

        var voiceSet = GetOrCreateVoiceSet(character);
        if (voiceSet == null || !voiceSet.attack) return;
        Enqueue(voiceSet.attack, voiceVolume, 0.4f);
    }

    public void PlayVoiceEffort(Character character)
    {
        if (character == null) return;
        if (speechActive && Time.time < speechEndTime) return;
        if (soundAudioSource == null) return;
        if (!IsHumanoidRace(character.race)) return;
        if (!PlayerCanSeeHex(character.hex)) return;

        var voiceSet = GetOrCreateVoiceSet(character);
        if (voiceSet == null || !voiceSet.effort) return;
        Enqueue(voiceSet.effort, voiceVolume, 0.4f);
    }

    public void PlayVoicePain(Character character)
    {
        if (character == null) return;
        if (speechActive && Time.time < speechEndTime) return;
        if (soundAudioSource == null) return;
        if (!IsHumanoidRace(character.race)) return;
        if (!PlayerCanSeeHex(character.hex)) return;

        var voiceSet = GetOrCreateVoiceSet(character);
        if (voiceSet == null || !voiceSet.pain) return;
        Enqueue(voiceSet.pain, voiceVolume, 0.4f);
    }

    public void PlayVoiceForAction(Character character, string actionName)
    {
        if (character == null || string.IsNullOrWhiteSpace(actionName)) return;
        if (!IsHumanoidRace(character.race)) return;

        string lower = actionName.ToLowerInvariant();
        if (lower.Contains("attack") || lower.Contains("assassinate") || lower.Contains("wound") || lower.Contains("siege"))
        {
            PlayVoiceAttack(character);
            return;
        }

        if (lower.Contains("train") || lower.Contains("fortify") || lower.Contains("fortification") || lower.Contains("destroy")
            || lower.Contains("sabotage") || lower.Contains("block") || lower.Contains("create camp")
            || lower.Contains("post camp") || lower.Contains("build") || lower.Contains("remove"))
        {
            PlayVoiceEffort(character);
        }
    }

    public void PlayUiExit()
    {
        EnqueueByKeywords(new[] { "back", "close", "exit", "cancel" }, CategoryMenu, 0.12f, 0.6f);
    }

    public void PlayActionExecute()
    {
        EnqueueByKeywords(new[] { "start", "action", "whoosh" }, CategoryPops, 0.1f, 0.7f);
    }

    public void PlayActionSuccess()
    {
        EnqueueByKeywords(new[] { "success", "win", "complete", "jingle" }, CategoryJingles, 0.3f, 0.9f);
    }

    public void PlayActionSuccess(string actionName)
    {
        if (string.IsNullOrWhiteSpace(actionName))
        {
            PlayActionSuccess();
            return;
        }

        string lower = actionName.ToLowerInvariant();

        if (lower.Contains("ice") || lower.Contains("frost"))
        {
            EnqueueByKeywords(new[] { "ice", "frost" }, CategoryMagic, 0.18f, 0.9f);
            return;
        }
        if (lower.Contains("fire") || lower.Contains("flame") || lower.Contains("burn"))
        {
            EnqueueByKeywords(new[] { "fire", "flame" }, CategoryMagic, 0.18f, 0.9f);
            return;
        }
        if (lower.Contains("lightning") || lower.Contains("storm"))
        {
            EnqueueByKeywords(new[] { "lightning", "storm" }, CategoryMagic, 0.18f, 0.9f);
            return;
        }
        if (lower.Contains("heal") || lower.Contains("cure") || lower.Contains("restore") || lower.Contains("buff") || lower.Contains("courage"))
        {
            EnqueueByKeywords(new[] { "heal", "buff", "restore" }, CategoryBuffs, 0.2f, 0.85f);
            return;
        }
        if (lower.Contains("curse") || lower.Contains("halt") || lower.Contains("darkness"))
        {
            EnqueueByKeywords(new[] { "dark", "curse", "whoosh" }, CategoryMagic, 0.2f, 0.85f);
            return;
        }
        if (lower.Contains("teleport") || lower.Contains("return"))
        {
            EnqueueByKeywords(new[] { "whoosh", "magic", "portal" }, CategoryMagic, 0.2f, 0.85f);
            return;
        }
        if (lower.Contains("scry") || lower.Contains("reveal") || lower.Contains("perceive"))
        {
            EnqueueByKeywords(new[] { "magic", "spark", "chime" }, CategorySpecialPops, 0.2f, 0.7f);
            return;
        }
        if (lower.Contains("find artifact") || lower.Contains("artifact"))
        {
            EnqueueByKeywords(new[] { "jingle", "reward", "special" }, CategorySpecialPops, 0.2f, 0.8f);
            return;
        }
        if (lower.Contains("buy") || lower.Contains("sell") || lower.Contains("steal") || lower.Contains("gold"))
        {
            EnqueueByKeywords(new[] { "coin", "gold", "pickup" }, CategoryCoin, 0.12f, 0.8f);
            return;
        }
        if (lower.Contains("train") || lower.Contains("fortify") || lower.Contains("create camp") || lower.Contains("post camp"))
        {
            EnqueueByKeywords(new[] { "powerup", "build", "forge" }, "powerup", 0.2f, 0.75f);
            return;
        }
        if (lower.Contains("attack") || lower.Contains("siege") || lower.Contains("assassinate") || lower.Contains("wound"))
        {
            EnqueueByKeywords(new[] { "hit", "impact", "slash" }, CategoryBattle, 0.12f, 0.85f);
            return;
        }
        if (lower.Contains("destroy") || lower.Contains("sabotage"))
        {
            EnqueueByKeywords(new[] { "impact", "hit", "break" }, CategoryBattle, 0.12f, 0.85f);
            return;
        }

        PlayActionSuccess();
    }

    public void PlayActionFail()
    {
        EnqueueByKeywords(new[] { "fail", "denied", "error" }, CategoryUi, 0.3f, 0.9f);
    }

    public void PlayMessage()
    {
        EnqueueByKeywords(new[] { "pop", "notify", "message" }, CategorySpecialPops, 0.15f, 0.6f);
    }

    public void PlayArtifactFound()
    {
        EnqueueByKeywords(new[] { "jingle", "reward", "special" }, CategorySpecialPops, 0.2f, 0.9f);
    }

    public void PlayMovement(Hex from, Hex to)
    {
        if (speechActive && Time.time < speechEndTime) return;
        if (soundAudioSource == null) return;

        string category = CategoryFootsteps;
        string[] keywords = to != null ? GetFootstepKeywordsForTerrain(to.terrainType) : new[] { "footsteps", "dirt", "ground" };

        if (to != null && (to.IsWaterTerrain() || to.terrainType == TerrainEnum.shore))
        {
            category = CategorySplat;
            keywords = new[] { "water", "splash" };
        }

        if (Time.time - lastFootstepTime < footstepMinInterval) return;
        var clip = FindClipByKeywords(keywords, category);
        if (!clip) clip = FindAnyFromCategory(category);
        if (!clip) return;

        soundAudioSource.PlayOneShot(clip, footstepVolume);
        lastFootstepTime = Time.time;
        lastPlayTime = Time.time;
    }

    public void PlayCombatImpact()
    {
        EnqueueByKeywords(new[] { "hit", "impact", "slash" }, CategoryBattle, 0.08f, 0.85f);
    }

    public void PlayMagic()
    {
        EnqueueByKeywords(new[] { "magic", "spell", "cast" }, CategoryMagic, 0.12f, 0.85f);
    }

    public void PlayBuff()
    {
        EnqueueByKeywords(new[] { "buff", "heal", "restore" }, CategoryBuffs, 0.12f, 0.8f);
    }

    public void PlayCountdown()
    {
        EnqueueByKeywords(new[] { "tick", "ticking", "clock", "count", "countdown" }, CategoryClock, 0.2f, 0.6f);
    }

    public void PlayCoin()
    {
        EnqueueByKeywords(new[] { "coin", "gold", "pickup" }, CategoryCoin, 0.08f, 0.75f);
    }

    public void PlayKey()
    {
        EnqueueByKeywords(new[] { "key", "unlock" }, CategoryKeys, 0.12f, 0.75f);
    }

    private void EnqueueByKeywords(string[] keywords, string category, float minInterval, float volume)
    {
        var clip = FindClipByKeywords(keywords, category);
        if (!clip) clip = FindClipByKeywords(keywords, null);
        if (!clip) clip = FindAnyFromCategory(category);
        if (!clip) return;

        Enqueue(clip, volume, minInterval);
    }

    private void Enqueue(AudioClip clip, float volume, float minInterval)
    {
        if (clip == null || soundAudioSource == null) return;
        if (speechActive && Time.time < speechEndTime) return;

        float now = Time.time;
        if (now - lastPlayTime < globalMinInterval) return;

        string key = Normalize(clip.name);
        if (lastPlayByKey.TryGetValue(key, out float lastTime) && now - lastTime < minInterval) return;

        if (queue.Count >= maxQueueSize) queue.Dequeue();
        queue.Enqueue(new SfxRequest
        {
            clip = clip,
            volume = volume,
            minInterval = minInterval,
            key = key
        });

        if (queueRoutine == null) queueRoutine = StartCoroutine(ProcessQueue());
    }

    private IEnumerator ProcessQueue()
    {
        while (queue.Count > 0)
        {
            var req = queue.Dequeue();
            soundAudioSource.PlayOneShot(req.clip, req.volume);
            lastPlayByKey[req.key] = Time.time;
            lastPlayTime = Time.time;
            float spacing = Mathf.Clamp(req.minInterval, 0.02f, 0.35f);
            yield return new WaitForSeconds(spacing);
        }
        queueRoutine = null;
    }

    private void Update()
    {
        if (speechActive && Time.time >= speechEndTime)
        {
            speechActive = false;
        }
    }

    private AudioClip FindClipByKeywords(IEnumerable<string> keywords, string category)
    {
        return FindClipByKeywords(keywords, category, null);
    }

    private AudioClip FindClipByKeywords(IEnumerable<string> keywords, string category, List<SoundRuntime> poolOverride)
    {
        var keywordList = new List<string>();
        foreach (var k in keywords)
        {
            if (string.IsNullOrWhiteSpace(k)) continue;
            keywordList.Add(k.ToLowerInvariant());
        }

        IEnumerable<SoundRuntime> pool = poolOverride ?? allRuntime;
        if (poolOverride == null && !string.IsNullOrWhiteSpace(category))
        {
            if (runtimeByCategory.TryGetValue(category.ToLowerInvariant(), out var categoryList))
            {
                pool = categoryList;
            }
        }

        var matches = new List<SoundRuntime>();
        foreach (var entry in pool)
        {
            foreach (var k in keywordList)
            {
                if (entry.tokens.Contains(k))
                {
                    matches.Add(entry);
                    break;
                }
            }
        }

        if (matches.Count == 0) return null;
        var choice = matches[UnityEngine.Random.Range(0, matches.Count)];
        return choice.clip;
    }

    private AudioClip FindClipByAllKeywords(IEnumerable<string> keywords, string category, List<SoundRuntime> poolOverride = null)
    {
        var keywordList = new List<string>();
        foreach (var k in keywords)
        {
            if (string.IsNullOrWhiteSpace(k)) continue;
            keywordList.Add(k.ToLowerInvariant());
        }

        IEnumerable<SoundRuntime> pool = poolOverride ?? allRuntime;
        if (poolOverride == null && !string.IsNullOrWhiteSpace(category))
        {
            if (runtimeByCategory.TryGetValue(category.ToLowerInvariant(), out var categoryList))
            {
                pool = categoryList;
            }
        }

        var matches = new List<SoundRuntime>();
        foreach (var entry in pool)
        {
            bool allMatch = true;
            foreach (var k in keywordList)
            {
                if (!entry.tokens.Contains(k))
                {
                    allMatch = false;
                    break;
                }
            }
            if (allMatch)
            {
                matches.Add(entry);
            }
        }

        if (matches.Count == 0) return null;
        var choice = matches[UnityEngine.Random.Range(0, matches.Count)];
        return choice.clip;
    }

    private AudioClip FindAnyFromCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category)) return null;
        if (!runtimeByCategory.TryGetValue(category.ToLowerInvariant(), out var list) || list.Count == 0) return null;
        return list[UnityEngine.Random.Range(0, list.Count)].clip;
    }

    private void LoadUsedSounds()
    {
        TextAsset jsonFile = Resources.Load<TextAsset>("sound");
        if (jsonFile == null)
        {
            Debug.LogWarning("sound.json not found in Resources.");
            return;
        }

        SoundCollection collection = JsonUtility.FromJson<SoundCollection>(jsonFile.text);
        if (collection == null || collection.sounds == null) return;

        sounds.Clear();
        foreach (var entry in collection.sounds)
        {
            if (entry == null || !entry.used || string.IsNullOrWhiteSpace(entry.path)) continue;
            var clip = LoadClipByPath(entry.path);
            if (!clip) continue;

            clipByPath[entry.path] = clip;
            sounds.Add(clip);

            var runtime = new SoundRuntime
            {
                entry = entry,
                clip = clip,
                key = Normalize(clip.name),
                category = entry.category != null ? entry.category.ToLowerInvariant() : string.Empty
            };
            runtime.tokens = Tokenize(entry.path);

            allRuntime.Add(runtime);
            runtimeByKey[runtime.key] = runtime;
            if (!string.IsNullOrWhiteSpace(runtime.category))
            {
                if (!runtimeByCategory.TryGetValue(runtime.category, out var list))
                {
                    list = new List<SoundRuntime>();
                    runtimeByCategory[runtime.category] = list;
                }
                list.Add(runtime);
            }
        }
    }

    private static HashSet<string> Tokenize(string value)
    {
        var tokens = new HashSet<string>();
        if (string.IsNullOrWhiteSpace(value)) return tokens;

        var buffer = new List<char>();
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch))
            {
                buffer.Add(char.ToLowerInvariant(ch));
            }
            else
            {
                FlushToken(tokens, buffer);
            }
        }
        FlushToken(tokens, buffer);
        return tokens;
    }

    private static void FlushToken(HashSet<string> tokens, List<char> buffer)
    {
        if (buffer.Count == 0) return;
        tokens.Add(new string(buffer.ToArray()));
        buffer.Clear();
    }

    private static string[] GetFootstepKeywordsForTerrain(TerrainEnum terrain)
    {
        return terrain switch
        {
            TerrainEnum.forest => new[] { "leaves", "grass", "forest" },
            TerrainEnum.grasslands => new[] { "grass", "dirt", "dirtyground" },
            TerrainEnum.plains => new[] { "grass", "dirt", "dirtyground", "ground" },
            TerrainEnum.hills => new[] { "gravel", "rock", "stone" },
            TerrainEnum.mountains => new[] { "rock", "gravel", "stone" },
            TerrainEnum.desert => new[] { "sand" },
            TerrainEnum.swamp => new[] { "mud" },
            TerrainEnum.wastelands => new[] { "dirt", "dirtyground", "rock" },
            TerrainEnum.shore => new[] { "sand", "water", "shore" },
            _ => new[] { "dirt", "dirtyground", "ground" }
        };
    }

    private AudioClip PickVoiceClip(VoiceType type, SexEnum sex)
    {
        string[] sexKeywords = sex == SexEnum.Female ? new[] { "female" } : new[] { "male" };
        string[] typeKeywords = type switch
        {
            VoiceType.Expression => new[] { "expressions" },
            VoiceType.Attack => new[] { "attack" },
            VoiceType.Effort => new[] { "effort" },
            VoiceType.Pain => new[] { "pain" },
            _ => new[] { "expressions" }
        };

        var clip = FindClipByAllKeywords(CombineKeywords(sexKeywords, typeKeywords), CategoryVoices);
        if (!clip) clip = FindClipByAllKeywords(CombineKeywords(new[] { "voice" }, sexKeywords, typeKeywords), CategoryVoices);
        return clip;
    }

    private static string[] CombineKeywords(params string[][] groups)
    {
        var list = new List<string>();
        foreach (var g in groups)
        {
            if (g == null) continue;
            list.AddRange(g);
        }
        return list.ToArray();
    }

    private static bool IsHumanoidRace(RacesEnum race)
    {
        return race is RacesEnum.Common or RacesEnum.Elf or RacesEnum.Dwarf or RacesEnum.Hobbit
            or RacesEnum.Maia or RacesEnum.Dunedain or RacesEnum.Beorning or RacesEnum.Wose or RacesEnum.Ent;
    }

    private static bool PlayerCanSeeHex(Hex hex)
    {
        if (hex == null) return false;
        Game g = FindFirstObjectByType<Game>();
        if (g == null || g.player == null) return false;
        return g.player.visibleHexes.Contains(hex) && hex.IsHexSeen();
    }

    private VoiceSet GetOrCreateVoiceSet(Character character)
    {
        int key = character.GetInstanceID();
        if (voiceSetByCharacterId.TryGetValue(key, out var set)) return set;

        var created = new VoiceSet
        {
            expression = PickVoiceClip(VoiceType.Expression, character.sex),
            attack = PickVoiceClip(VoiceType.Attack, character.sex),
            effort = PickVoiceClip(VoiceType.Effort, character.sex),
            pain = PickVoiceClip(VoiceType.Pain, character.sex)
        };
        voiceSetByCharacterId[key] = created;
        return created;
    }

    private enum VoiceType
    {
        Expression,
        Attack,
        Effort,
        Pain
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
}
