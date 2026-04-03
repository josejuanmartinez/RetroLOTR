using UnityEngine;
using TMPro;
using System.Collections.Generic;
using UnityEngine.Video;
using System.Linq;
using System;
using System.Globalization;
using System.Collections;
using UnityEngine.UI;

public class LeaderSelector : SearcherByName
{
    private class LeaderSelectionEntry
    {
        public string baseLeaderName;
        public string displayName;
        public string variantName;
        public string assetName;
        public string description;
        public string deckIdentity;
        public string subdeckId;
    }

    public CircularCardCarousel leaderCarousel;
    public GameObject carouselItemPrefab;
    public VideoPlayer introVideo;
    // public VideoPlayer leaderVideo;
    public TypewriterEffect typewriterEffect;
    public TextMeshProUGUI textUI;
    public GameObject progress;
    public GameObject progressText;
    public GameObject leaderSelectionFullScreen;

    readonly List<LeaderSelectionEntry> selectionEntries = new();
    readonly List<GameObject> carouselItems = new();

    List<string> loadedLeaders = new();
    bool loadedFirst = false;
    bool introFinished = false;
    bool selectionScreenShown = false;
    bool selectionScreenQueued = false;
    Coroutine deferredRefreshRoutine;
    void Awake()
    {
        if (introVideo != null)
        {
            introVideo.loopPointReached += OnIntroVideoFinished;
        }

        if (leaderCarousel != null)
        {
            leaderCarousel.RegisterOnSelectionChanged(SelectLeader);
        }
    }

    void OnDestroy()
    {
        if (introVideo != null)
        {
            introVideo.loopPointReached -= OnIntroVideoFinished;
        }

        if (leaderCarousel != null)
        {
            leaderCarousel.UnregisterOnSelectionChanged(SelectLeader);
        }
    }

    void Update()
    {
        List<PlayableLeader> playableLeaders = FindObjectsByType<PlayableLeader>(FindObjectsSortMode.None).ToList();
        if (loadedLeaders.Count < playableLeaders.Count)
        {
            for(int i=0; i<playableLeaders.Count; i++)
            {
                PlayableLeader playableLeader = playableLeaders[i];
                string leaderName = playableLeader.characterName;
                if (loadedLeaders.Contains(leaderName)) continue;

                AddLeaderOptions(playableLeader);
                loadedLeaders.Add(leaderName);

                if (!loadedFirst)
                {
                    loadedFirst = true;
                    if (leaderCarousel != null)
                    {
                        leaderCarousel.SetIndex(0);
                    }
                    SelectLeader(0);
                    ShowLeaderSelectionIfReady();
                }

                RequestLeaderSelectionRefresh();
            }
        }

        if (loadedFirst)
        {
            ShowLeaderSelectionIfReady();
        }
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            RequestLeaderSelectionRefresh();
        }
    }

    void OnIntroVideoFinished(VideoPlayer player)
    {
        introFinished = true;
        ShowLeaderSelectionIfReady();
    }

    void ShowLeaderSelectionIfReady()
    {
        if (!loadedFirst)
        {
            return;
        }

        bool introIsBlocking = introVideo != null
            && introVideo.gameObject.activeInHierarchy
            && introVideo.isPlaying
            && !introFinished;
        if (introIsBlocking)
        {
            return;
        }

        if (selectionScreenShown || selectionScreenQueued)
        {
            return;
        }

        selectionScreenQueued = true;
        StartCoroutine(ShowLeaderSelectionDeferred());
    }

    void ForceLeaderSelectionRefresh()
    {
        if (leaderCarousel != null)
        {
            leaderCarousel.Refresh();
        }

        if (textUI != null)
        {
            textUI.ForceMeshUpdate();
        }

        RectTransform fullScreenRect = leaderSelectionFullScreen != null
            ? leaderSelectionFullScreen.transform as RectTransform
            : null;
        if (fullScreenRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(fullScreenRect);
        }

        RectTransform carouselRect = leaderCarousel != null
            ? leaderCarousel.transform as RectTransform
            : null;
        if (carouselRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(carouselRect);
        }

        for (int i = 0; i < carouselItems.Count; i++)
        {
            if (carouselItems[i] == null) continue;
            RectTransform itemRect = carouselItems[i].transform as RectTransform;
            if (itemRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(itemRect);
            }
        }

        Canvas.ForceUpdateCanvases();
    }

    void RequestLeaderSelectionRefresh()
    {
        if (!isActiveAndEnabled) return;
        if (deferredRefreshRoutine != null) return;
        deferredRefreshRoutine = StartCoroutine(DeferredLeaderSelectionRefresh());
    }

    IEnumerator DeferredLeaderSelectionRefresh()
    {
        for (int i = 0; i < 4; i++)
        {
            yield return null;
            ForceLeaderSelectionRefresh();
            yield return new WaitForEndOfFrame();
            ForceLeaderSelectionRefresh();
        }

        deferredRefreshRoutine = null;
    }

    IEnumerator ShowLeaderSelectionDeferred()
    {
        yield return null;
        yield return new WaitForEndOfFrame();

        if (introVideo != null)
        {
            introVideo.gameObject.SetActive(false);
        }

        if (progress != null)
        {
            progress.SetActive(false);
        }

        if (progressText != null)
        {
            progressText.SetActive(false);
        }

        if (leaderSelectionFullScreen != null)
        {
            leaderSelectionFullScreen.SetActive(false);
            leaderSelectionFullScreen.SetActive(true);
        }

        selectionScreenShown = true;
        selectionScreenQueued = false;
        ForceLeaderSelectionRefresh();
        RequestLeaderSelectionRefresh();
    }

    void AddLeaderOptions(PlayableLeader playableLeader)
    {
        if (playableLeader == null) return;

        LeaderBiomeConfig biome = FindAnyObjectByType<PlayableLeaders>()?.playableLeaders?.biomes?
            .Find(x => x.characterName.ToLower() == playableLeader.characterName.ToLower());
        if (biome == null) return;

        AddSelectionEntry(playableLeader, BuildLeaderDisplayName(playableLeader), string.Concat(biome.description.Replace("<br>","")), biome.deckIdentity, biome.subdeckId);

        foreach (LeaderVariantConfig variant in biome.variants)
        {
            string variantName = GetVariantName(playableLeader.characterName, variant.displayName, variant.variantId);
            string displayName = BuildLeaderDisplayName(playableLeader, variantName);
            string assetName = ResolveVariantAssetName(playableLeader.characterName, variant.displayName, variant.variantId);
            string description = string.IsNullOrWhiteSpace(variant.description) ? string.Concat(biome.description.Replace("<br>","")) : string.Concat(biome.description.Replace("<br>",""), "<br><br>", variant.description.Replace("<br>",""));
            string subdeckId = string.IsNullOrWhiteSpace(variant.subdeckId) ? biome.subdeckId : variant.subdeckId;
            AddSelectionEntry(playableLeader, displayName, description, variant.deckIdentity, subdeckId, variantName, assetName);
        }
    }

    string BuildLeaderDisplayName(PlayableLeader playableLeader, string variantName = null)
    {
        if (playableLeader == null) return string.Empty;

        string alignmentSprite = GetAlignmentSpriteTag(playableLeader.GetAlignment());
        if (string.IsNullOrWhiteSpace(variantName))
        {
            return $"{alignmentSprite} {playableLeader.characterName}";
        }

        return $"{alignmentSprite} {playableLeader.characterName}<br>({variantName})";
    }

    string GetAlignmentSpriteTag(AlignmentEnum alignment)
    {
        string spriteName = alignment switch
        {
            AlignmentEnum.freePeople => "freePeople",
            AlignmentEnum.darkServants => "darkServants",
            _ => "neutral"
        };

        return $"<sprite name=\"{spriteName}\">";
    }

    string GetVariantName(string baseLeaderName, string displayName, string variantId)
    {
        string suffix = displayName ?? string.Empty;

        suffix = suffix.Replace("_", " ");
        List<char> chars = new();
        for (int i = 0; i < suffix.Length; i++)
        {
            char c = suffix[i];
            if (i > 0 && char.IsUpper(c) && !char.IsWhiteSpace(suffix[i - 1]) && !char.IsUpper(suffix[i - 1]))
            {
                chars.Add(' ');
            }
            chars.Add(c);
        }

        string formatted = new string(chars.ToArray()).Trim().ToLowerInvariant();
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(formatted);
    }

    string ResolveVariantAssetName(string baseLeaderName, string displayName, string variantId)
    {
        if (string.IsNullOrWhiteSpace(baseLeaderName))
        {
            return displayName;
        }

        Illustrations illustrations = FindFirstObjectByType<Illustrations>();
        string[] candidates =
        {
            $"{baseLeaderName} {displayName}",
            $"{baseLeaderName} {variantId}",
            displayName,
            variantId
        };

        HashSet<string> seen = new();
        foreach (string candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate) || !seen.Add(Normalize(candidate)))
            {
                continue;
            }

            if (illustrations == null || illustrations.TryGetIllustrationByName(candidate, out _))
            {
                return candidate;
            }
        }

        return $"{baseLeaderName} {displayName}";
    }

    void AddSelectionEntry(PlayableLeader playableLeader, string displayName, string description, string deckIdentity, string subdeckId, string variantName = null, string assetName = null)
    {
        Illustrations illustrations = FindFirstObjectByType<Illustrations>();
        Sprite carouselSprite = illustrations != null ? illustrations.GetIllustrationByName(assetName) ?? illustrations.GetIllustrationByName(playableLeader.characterName) : null;

        LeaderSelectionEntry selectionEntry = new()
        {
            baseLeaderName = playableLeader.characterName,
            displayName = displayName,
            variantName = variantName,
            assetName = assetName,
            description = description,
            deckIdentity = deckIdentity,
            subdeckId = subdeckId
        };
        selectionEntries.Add(selectionEntry);
        CreateCarouselItem(selectionEntry, carouselSprite);
        RequestLeaderSelectionRefresh();
    }

    string BuildLeaderText(LeaderSelectionEntry selection)
    {
        /*if (selection == null) return string.Empty;
        if (string.IsNullOrWhiteSpace(selection.variantName))
        {
            return selection.description;
        }

        return $"<b>{selection.baseLeaderName}</b><br><i>{selection.variantName}</i><br><br>{selection.description}";
        */
        if (selection == null) return string.Empty;
        if (string.IsNullOrWhiteSpace(selection.deckIdentity))
        {
            return selection.description;
        }

        return $"{selection.description}<br><br>{selection.deckIdentity}";
    }

    void CreateCarouselItem(LeaderSelectionEntry selection, Sprite sprite)
    {
        if (leaderCarousel == null || carouselItemPrefab == null || selection == null)
        {
            return;
        }

        GameObject item = Instantiate(carouselItemPrefab, leaderCarousel.transform);
        item.name = $"{selection.displayName} Carousel Item";
        carouselItems.Add(item);
        PopulateCarouselItem(item, selection, sprite);
        leaderCarousel.AddItem(item);
    }

    void PopulateCarouselItem(GameObject item, LeaderSelectionEntry selection, Sprite sprite)
    {
        if (item == null || selection == null)
        {
            return;
        }

        CarouselItem carouselItem = item.GetComponent<CarouselItem>();
        if (carouselItem != null)
        {
            carouselItem.SetSprite(sprite);
            carouselItem.SetLabel(BuildCarouselLabel(selection));
        }
    }

    string BuildCarouselLabel(LeaderSelectionEntry selection)
    {
        if (selection == null) return string.Empty;

        string alignmentSprite = GetAlignmentSpriteTag(null, selection.baseLeaderName);
        if (string.IsNullOrWhiteSpace(selection.variantName))
        {
            return $"{alignmentSprite} {selection.baseLeaderName}";
        }

        return $"{alignmentSprite} {selection.baseLeaderName}<br>({selection.variantName})";
    }

    string GetAlignmentSpriteTag(PlayableLeader playableLeader, string fallbackLeaderName = null)
    {
        if (playableLeader != null)
        {
            return GetAlignmentSpriteTag(playableLeader.GetAlignment());
        }

        AlignmentEnum inferredAlignment = fallbackLeaderName switch
        {
            "Gandalf" => AlignmentEnum.freePeople,
            "Sauron" => AlignmentEnum.darkServants,
            "Saruman" => AlignmentEnum.darkServants,
            _ => AlignmentEnum.neutral
        };
        return GetAlignmentSpriteTag(inferredAlignment);
    }

    public void SelectLeader(int value)
    {
        if (selectionEntries.Count > value)
        {
            LeaderSelectionEntry selection = selectionEntries[value];
            string leaderText = BuildLeaderText(selection);
            if (typewriterEffect) typewriterEffect.StartWriting(leaderText); else textUI.text = leaderText;

            PlayableLeader player = FindObjectsByType<PlayableLeader>(FindObjectsSortMode.None).ToList()
                .Find(x => x.characterName.ToLower() == selection.baseLeaderName.ToLower());
            if (player != null)
            {
                player.SetDeckSelection(selection.subdeckId, selection.deckIdentity, leaderText, selection.variantName);
            }

            FindFirstObjectByType<Game>().SelectPlayer(player);
        }
    }

    public void ApplyCurrentSelection()
    {
        if (leaderCarousel == null)
        {
            SelectLeader(0);
            return;
        }

        SelectLeader(leaderCarousel.GetCurrentIndex());
    }
}
