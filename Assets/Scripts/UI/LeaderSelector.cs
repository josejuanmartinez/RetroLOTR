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
        public string baseDescription;
        public string variantDescription;
        public string deckIdentity;
        public string subdeckId;
    }

    public CircularCardCarousel leaderCarousel;
    public GameObject carouselItemPrefab;
    public VideoPlayer introVideo;
    // public VideoPlayer leaderVideo;
    public TypewriterEffect typewriterEffect;
    public TextMeshProUGUI textUI;
    public TextMeshProUGUI variantTextUI;
    public TextMeshProUGUI deckTextUI;
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
    Canvas rootCanvas;
    void Awake()
    {
        rootCanvas = GetComponentInParent<Canvas>(true);
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

                if (selectionScreenShown || selectionScreenQueued)
                {
                    RequestLeaderSelectionRefresh();
                }
            }
        }

        if (loadedFirst)
        {
            ShowLeaderSelectionIfReady();
        }
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && (selectionScreenShown || selectionScreenQueued))
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
        if (!selectionScreenShown && !selectionScreenQueued)
        {
            return;
        }

        if (rootCanvas == null)
        {
            rootCanvas = GetComponentInParent<Canvas>(true);
        }

        if (rootCanvas != null && rootCanvas.gameObject != null && !rootCanvas.gameObject.activeSelf)
        {
            rootCanvas.gameObject.SetActive(true);
        }

        if (rootCanvas != null)
        {
            rootCanvas.enabled = false;
            rootCanvas.enabled = true;
        }

        if (leaderSelectionFullScreen != null)
        {
            if (!leaderSelectionFullScreen.activeSelf)
            {
                leaderSelectionFullScreen.SetActive(true);
            }

            leaderSelectionFullScreen.transform.SetAsLastSibling();

            CanvasGroup group = leaderSelectionFullScreen.GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.alpha = 1f;
                group.interactable = true;
                group.blocksRaycasts = true;
            }

            Canvas fullScreenCanvas = leaderSelectionFullScreen.GetComponent<Canvas>();
            if (fullScreenCanvas != null)
            {
                fullScreenCanvas.enabled = false;
                fullScreenCanvas.enabled = true;
            }
        }

        if (leaderCarousel != null)
        {
            if (!leaderCarousel.gameObject.activeSelf)
            {
                leaderCarousel.gameObject.SetActive(true);
            }

            leaderCarousel.gameObject.SetActive(false);
            leaderCarousel.gameObject.SetActive(true);
            leaderCarousel.enabled = false;
            leaderCarousel.enabled = true;
            leaderCarousel.Refresh();
            leaderCarousel.SetIndex(leaderCarousel.GetCurrentIndex());
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
        for (int i = 0; i < 8; i++)
        {
            yield return null;
            ForceLeaderSelectionRefresh();
            yield return new WaitForEndOfFrame();
            ForceLeaderSelectionRefresh();
            yield return new WaitForSecondsRealtime(0.02f);
        }

        deferredRefreshRoutine = null;
    }

    IEnumerator ShowLeaderSelectionDeferred()
    {
        yield return null;
        yield return new WaitForEndOfFrame();

        FindFirstObjectByType<Board>()?.HideGenerationProgressUi();

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
            yield return null;
            leaderSelectionFullScreen.SetActive(true);
            leaderSelectionFullScreen.transform.SetAsLastSibling();
        }

        selectionScreenShown = true;
        selectionScreenQueued = false;
        SelectLeader(leaderCarousel != null ? leaderCarousel.GetCurrentIndex() : 0);
        FindFirstObjectByType<Board>()?.HideGenerationProgressUi();
        ForceLeaderSelectionRefresh();
        RequestLeaderSelectionRefresh();
        StartCoroutine(ForceLeaderSelectionVisibleAcrossFrames());
    }

    IEnumerator ForceLeaderSelectionVisibleAcrossFrames()
    {
        for (int i = 0; i < 12; i++)
        {
            FindFirstObjectByType<Board>()?.HideGenerationProgressUi();
            yield return null;
            ForceLeaderSelectionRefresh();
            yield return new WaitForEndOfFrame();
            ForceLeaderSelectionRefresh();
        }
    }

    void AddLeaderOptions(PlayableLeader playableLeader)
    {
        if (playableLeader == null) return;

        LeaderBiomeConfig biome = FindAnyObjectByType<PlayableLeaders>()?.playableLeaders?.biomes?
            .Find(x => x.characterName.ToLower() == playableLeader.characterName.ToLower());
        if (biome == null) return;

        string baseDescription = NormalizeLeaderDescription(biome.description);
        AddSelectionEntry(playableLeader, BuildLeaderDisplayName(playableLeader), baseDescription, string.Empty, biome.deckIdentity, biome.subdeckId);

        foreach (LeaderVariantConfig variant in biome.variants)
        {
            string variantName = GetVariantName(playableLeader.characterName, variant.displayName, variant.variantId);
            string displayName = BuildLeaderDisplayName(playableLeader, variantName);
            string assetName = ResolveVariantAssetName(playableLeader.characterName, variant.displayName, variant.variantId);
            string variantDescription = NormalizeLeaderDescription(variant.description);
            string subdeckId = string.IsNullOrWhiteSpace(variant.subdeckId) ? biome.subdeckId : variant.subdeckId;
            AddSelectionEntry(playableLeader, displayName, baseDescription, variantDescription, variant.deckIdentity, subdeckId, variantName, assetName);
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

    void AddSelectionEntry(PlayableLeader playableLeader, string displayName, string baseDescription, string variantDescription, string deckIdentity, string subdeckId, string variantName = null, string assetName = null)
    {
        Illustrations illustrations = FindFirstObjectByType<Illustrations>();
        Sprite carouselSprite = illustrations != null ? illustrations.GetIllustrationByName(assetName) ?? illustrations.GetIllustrationByName(playableLeader.characterName) : null;

        LeaderSelectionEntry selectionEntry = new()
        {
            baseLeaderName = playableLeader.characterName,
            displayName = displayName,
            variantName = variantName,
            assetName = assetName,
            baseDescription = baseDescription,
            variantDescription = variantDescription,
            deckIdentity = deckIdentity,
            subdeckId = subdeckId
        };
        selectionEntries.Add(selectionEntry);
        CreateCarouselItem(selectionEntry, carouselSprite);
        RequestLeaderSelectionRefresh();
    }

    string BuildLeaderText(LeaderSelectionEntry selection)
    {
        if (selection == null) return string.Empty;

        List<string> parts = new();
        if (!string.IsNullOrWhiteSpace(selection.baseDescription))
        {
            parts.Add(selection.baseDescription);
        }
        if (!string.IsNullOrWhiteSpace(selection.variantDescription))
        {
            parts.Add(selection.variantDescription);
        }
        if (!string.IsNullOrWhiteSpace(selection.deckIdentity))
        {
            parts.Add(selection.deckIdentity);
        }

        return string.Join("\n\n", parts);
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
            ApplyLeaderTexts(selection);

            PlayableLeader player = FindObjectsByType<PlayableLeader>(FindObjectsSortMode.None).ToList()
                .Find(x => x.characterName.ToLower() == selection.baseLeaderName.ToLower());
            if (player != null)
            {
                player.SetDeckSelection(selection.subdeckId, selection.deckIdentity, leaderText, selection.variantName);
            }

            FindFirstObjectByType<Game>().SelectPlayer(player);
        }
    }

    void ApplyLeaderTexts(LeaderSelectionEntry selection)
    {
        if (selection == null)
        {
            if (typewriterEffect) typewriterEffect.StartWriting(string.Empty);
            else if (textUI != null) textUI.text = string.Empty;
            if (variantTextUI != null) variantTextUI.text = string.Empty;
            if (deckTextUI != null) deckTextUI.text = string.Empty;
            return;
        }

        string mainText = selection.baseDescription ?? string.Empty;
        string variantText = selection.variantDescription ?? string.Empty;

        if (string.IsNullOrWhiteSpace(selection.variantName) && string.IsNullOrWhiteSpace(variantText))
        {
            SplitBaseDescription(mainText, out string topHalf, out string bottomHalf);
            mainText = topHalf;
            variantText = bottomHalf;
        }

        if (typewriterEffect) typewriterEffect.StartWriting(mainText);
        else if (textUI != null) textUI.text = mainText;

        if (variantTextUI != null)
        {
            variantTextUI.text = variantText;
        }

        if (deckTextUI != null)
        {
            deckTextUI.text = selection.deckIdentity ?? string.Empty;
        }
    }

    string NormalizeLeaderDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description)) return string.Empty;
        return description.Replace("<br><br>", "\n\n").Replace("<br>", "\n").Trim();
    }

    void SplitBaseDescription(string description, out string firstHalf, out string secondHalf)
    {
        firstHalf = description ?? string.Empty;
        secondHalf = string.Empty;

        if (string.IsNullOrWhiteSpace(description))
        {
            return;
        }

        string[] paragraphs = description
            .Split(new[] { "\n\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Trim())
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();

        if (paragraphs.Length >= 2)
        {
            int splitIndex = Mathf.CeilToInt(paragraphs.Length / 2f);
            firstHalf = string.Join("\n\n", paragraphs.Take(splitIndex));
            secondHalf = string.Join("\n\n", paragraphs.Skip(splitIndex));
            return;
        }

        string[] sentences = description
            .Split(new[] { ". ", "! ", "? " }, StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Trim())
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();

        if (sentences.Length >= 2)
        {
            int splitIndex = Mathf.CeilToInt(sentences.Length / 2f);
            firstHalf = string.Join(". ", sentences.Take(splitIndex)).Trim();
            secondHalf = string.Join(". ", sentences.Skip(splitIndex)).Trim();
            if (!string.IsNullOrWhiteSpace(firstHalf) && !firstHalf.EndsWith(".") && !firstHalf.EndsWith("!") && !firstHalf.EndsWith("?")) firstHalf += ".";
            if (!string.IsNullOrWhiteSpace(secondHalf) && !secondHalf.EndsWith(".") && !secondHalf.EndsWith("!") && !secondHalf.EndsWith("?")) secondHalf += ".";
            return;
        }

        string[] words = description.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 2)
        {
            secondHalf = string.Empty;
            return;
        }

        int wordSplitIndex = Mathf.CeilToInt(words.Length / 2f);
        firstHalf = string.Join(" ", words.Take(wordSplitIndex));
        secondHalf = string.Join(" ", words.Skip(wordSplitIndex));
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
