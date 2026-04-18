using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(Image))]
[RequireComponent(typeof(CanvasGroup))]
public class SelectedCharacterIcon : MonoBehaviour
{
    [Header("Game Objects")]
    public GameObject levelsGameObject;
    public GameObject moved;
    public GameObject actioned;
    public GameObject unactionedIcon;
    public GameObject actionedIcon;
    public GameObject border;
    public GameObject otherCharacters;

    [Header("Leader")]
    public Image icon;
    public RawImage rawImage;
    public VideoPlayer video;
    public TextMeshProUGUI textWidget;
    public Image alignmentIcon;

    [Header("Health")]
    public Image health;

    [Header("Levels")]
    public TextMeshProUGUI commander;
    public TextMeshProUGUI agent;
    public TextMeshProUGUI emmissary;
    public TextMeshProUGUI mage;
    public TextMeshProUGUI movementLeft;

    [Header("Artifacts")]
    public GameObject artifactPrefab;
    public Transform artifactsGridLayoutTransform;

    [Header("Drop Target Hint")]
    [SerializeField] private Color dropHintColor = new Color(1f, 0.92f, 0.35f, 1f);
    [SerializeField] private Color dropReadyColor = new Color(0.55f, 1f, 0.72f, 1f);
    [SerializeField] private float dropHintPulseSpeed = 8f;
    [SerializeField] private float dropHintScaleMultiplier = 1.05f;
    [SerializeField] private float dropReadyScaleMultiplier = 1.13f;
    [SerializeField] private bool hideDetailsWhenDropReady = true;
    [SerializeField] private Image rootImage;
    [SerializeField] private Image borderImage;

    [Header("Played cards")]
    [SerializeField] private CanvasGroup cardCanvasGroup;
    [SerializeField] public GameObject playedCard;
    
    // private Videos videos;
    private Illustrations illustrations;
    private CanvasGroup canvasGroup;
    private Color rootDefaultColor = Color.white;
    private Color borderDefaultColor = Color.white;
    private Vector3 defaultScale = Vector3.one;
    private bool dropHintActive;
    private float dropHintProximity;
    private bool dropHintLocked;
    private bool detailsHiddenForDropPreview;
    private readonly Dictionary<GameObject, bool> cachedChildActiveStates = new();
    private int lastRefreshedCharacterId = int.MinValue;
    private Character pendingRefreshCharacter;
    public Character CurrentCharacter { get; private set; }
    private bool refreshScheduled;
    private readonly List<ArtifactRenderer> artifactRenderers = new();
    private readonly List<GameObject> playedCardInstances = new();

    private void Awake()
    {
        if (rootImage != null) rootDefaultColor = rootImage.color;
        if (borderImage != null) borderDefaultColor = borderImage.color;
        defaultScale = transform.localScale;
        BindPlayedCardTemplate();
        ClearPlayedCardInstances();
        SetPlayedCardVisible(false);
    }

    private void OnDisable()
    {
        SetDropTargetHighlight(false);
        refreshScheduled = false;
        pendingRefreshCharacter = null;
        StopAllCoroutines();
    }

    private void Update()
    {
        if (!dropHintActive) return;

        float pulse = (Mathf.Sin(Time.unscaledTime * dropHintPulseSpeed) + 1f) * 0.5f;
        float proximity = Mathf.Clamp01(dropHintProximity);
        float lockBoost = dropHintLocked ? 1f : 0f;
        float baseScale = Mathf.Lerp(dropHintScaleMultiplier, dropReadyScaleMultiplier, Mathf.Max(proximity, lockBoost));
        float scaleLerp = Mathf.Lerp(0.4f, 1f, pulse);
        transform.localScale = Vector3.Lerp(defaultScale, defaultScale * baseScale, scaleLerp);

        if (rootImage != null)
        {
            Color targetColor = Color.Lerp(dropHintColor, dropReadyColor, Mathf.Max(proximity, lockBoost));
            float tint = Mathf.Lerp(0.14f, 0.3f, proximity);
            if (dropHintLocked) tint = Mathf.Max(tint, 0.36f);
            rootImage.color = Color.Lerp(rootDefaultColor, targetColor, tint);
        }
        if (borderImage != null)
        {
            Color targetColor = Color.Lerp(dropHintColor, dropReadyColor, Mathf.Max(proximity, lockBoost));
            float tint = Mathf.Lerp(0.72f, 0.96f, proximity);
            if (dropHintLocked) tint = 1f;
            borderImage.color = Color.Lerp(borderDefaultColor, targetColor, tint);
        }

        SetDropPreviewDetailsHidden(dropHintLocked);
    }

    public void SetDropTargetHighlight(bool active)
    {
        dropHintActive = active;
        if (dropHintActive)
        {
            dropHintProximity = 0f;
            dropHintLocked = false;
            return;
        }

        dropHintProximity = 0f;
        dropHintLocked = false;
        SetDropPreviewDetailsHidden(false);
        transform.localScale = defaultScale;
        if (rootImage != null) rootImage.color = rootDefaultColor;
        if (borderImage != null) borderImage.color = borderDefaultColor;
    }

    public void SetDropTargetProximity(float proximity, bool locked)
    {
        if (!dropHintActive) return;
        dropHintProximity = Mathf.Clamp01(proximity);
        dropHintLocked = locked;
        if (!dropHintLocked)
        {
            SetDropPreviewDetailsHidden(false);
        }
    }

    private void SetDropPreviewDetailsHidden(bool hidden)
    {
        if (!hideDetailsWhenDropReady) return;
        if (detailsHiddenForDropPreview == hidden) return;

        GameObject artifactsWidget = artifactsGridLayoutTransform != null ? artifactsGridLayoutTransform.gameObject : null;

        if (hidden)
        {
            cachedChildActiveStates.Clear();
            if (artifactsWidget != null)
            {
                cachedChildActiveStates[artifactsWidget] = artifactsWidget.activeSelf;
                artifactsWidget.SetActive(false);
            }
            if (otherCharacters != null)
            {
                cachedChildActiveStates[otherCharacters] = otherCharacters.activeSelf;
                otherCharacters.SetActive(false);
            }
        }
        else
        {
            foreach (KeyValuePair<GameObject, bool> state in cachedChildActiveStates)
            {
                if (state.Key == null) continue;
                state.Key.SetActive(state.Value);
            }
            cachedChildActiveStates.Clear();
        }

        detailsHiddenForDropPreview = hidden;
    }

    // Update is called once per frame
    public void Refresh(Character c)
    {
        pendingRefreshCharacter = c;
        if (refreshScheduled) return;
        refreshScheduled = true;
        StartCoroutine(RefreshNextFrame());
    }

    private System.Collections.IEnumerator RefreshNextFrame()
    {
        yield return null;
        refreshScheduled = false;
        Character c = pendingRefreshCharacter;
        pendingRefreshCharacter = null;
        if (c == null)
        {
            Hide();
            yield break;
        }
        ApplyRefresh(c);
    }

    private void ApplyRefresh(Character c)
    {
        CurrentCharacter = c;
        SetDropTargetHighlight(false);
        SetVisible(true);
        border.SetActive(true);
        SetCharacterVisuals(GetIllustrationByName(c.characterName));
        alignmentIcon.enabled = true;
        alignmentIcon.sprite = GetIllustrationByName(c.GetAlignment().ToString());
        string baseHoverText = BuildSelectedCharacterTitle(c);
        string kidnappingText = BuildKidnappingStatusText(c);
        textWidget.text = string.IsNullOrWhiteSpace(kidnappingText)
            ? baseHoverText
            : $"{baseHoverText}\n{kidnappingText}";
        levelsGameObject.SetActive(true);
        actioned.SetActive(true);
        moved.SetActive(true);
        commander.text = c.GetCommander().ToString();
        agent.text = c.GetAgent().ToString();
        emmissary.text = c.GetEmmissary().ToString();
        mage.text = c.GetMage().ToString();
        actionedIcon.SetActive(c.hasActionedThisTurn);
        unactionedIcon.SetActive(!actionedIcon.activeSelf);
        health.gameObject.SetActive(true);
        health.fillAmount = c.health / 100f;

        RefreshArtifacts(c.artifacts);
        
        RefreshMovementLeft(c);
        RefreshPlayedCards(c);

        if (c != null)
        {
            lastRefreshedCharacterId = c.GetInstanceID();
        }
    }

    private string BuildKidnappingStatusText(Character c)
    {
        if (c == null) return string.Empty;

        List<string> parts = new();
        int captiveCount = c.kidnappedCharacters != null ? c.kidnappedCharacters.Count(x => x != null && x.character != null && !x.character.killed) : 0;
        if (captiveCount > 0)
        {
            parts.Add($"Captives: {captiveCount}");
        }

        if (c.IsKidnapped())
        {
            string kidnapperName = c.kidnappedBy != null ? c.kidnappedBy.characterName : "Unknown";
            parts.Add($"Captured by: {kidnapperName}");
        }

        return string.Join(" | ", parts);
    }

    public void RefreshHoverPreview(Character c, string hoverText, bool showHealth, bool showArtifacts)
    {
        if (c == null)
        {
            Hide();
            return;
        }

        SetDropTargetHighlight(false);
        SetVisible(true);
        border.SetActive(true);
        SetCharacterVisuals(GetIllustrationByName(c.characterName));
        alignmentIcon.enabled = true;
        alignmentIcon.sprite = GetIllustrationByName(c.GetAlignment().ToString());
        textWidget.text = string.IsNullOrWhiteSpace(hoverText) ? BuildSelectedCharacterTitle(c) : hoverText;

        actioned.SetActive(false);
        moved.SetActive(false);
        actionedIcon.SetActive(false);
        unactionedIcon.SetActive(false);

        commander.text = c.GetCommander().ToString();
        agent.text = c.GetAgent().ToString();
        emmissary.text = c.GetEmmissary().ToString();
        mage.text = c.GetMage().ToString();
        movementLeft.text = "-";

        health.gameObject.SetActive(showHealth);
        if (showHealth) health.fillAmount = c.health / 100f;

        RefreshArtifacts(showArtifacts ? c.artifacts : null);

        ClearPlayedCardInstances();
        SetPlayedCardVisible(false);
    }


    // Update is called once per frame
    public void Hide()
    {
        SetDropTargetHighlight(false);
        SetVisible(false);
        border.SetActive(false);
        alignmentIcon.enabled = false;
        // Video path disabled for now; static illustrations only.
        // if (video != null)
        // {
        //     video.Stop();
        //     video.enabled = false;
        // }
        if (video != null) video.enabled = false;
        if (rawImage != null) rawImage.enabled = false;
        Image targetImage = GetImageTarget();
        if (targetImage != null) targetImage.enabled = false;
        textWidget.text = "";
        levelsGameObject.SetActive(false);
        actioned.SetActive(false);
        moved.SetActive(false);
        health.gameObject.SetActive(false);
        ClearPlayedCardInstances();
        SetPlayedCardVisible(false);
        lastRefreshedCharacterId = int.MinValue;
        pendingRefreshCharacter = null;
        CurrentCharacter = null;
        refreshScheduled = false;
    }

    public void RefreshMovementLeft(Character c)
    {
        movementLeft.text = c.GetMovementLeft().ToString();
    }

    private Sprite GetIllustrationByName(string name)
    {
        if (illustrations == null) illustrations = FindFirstObjectByType<Illustrations>();
        return illustrations != null ? illustrations.GetIllustrationByName(name) : null;
    }

    private Sprite GetIllustrationByName(string name, bool logMissing)
    {
        if (illustrations == null) illustrations = FindFirstObjectByType<Illustrations>();
        return illustrations != null ? illustrations.GetIllustrationByName(name, logMissing) : null;
    }

    // private VideoClip GetVideoByName(string name)
    // {
    //     if (videos == null) videos = FindFirstObjectByType<Videos>();
    //     return videos != null ? videos.GetVideoByName(name) : null;
    // }

    private Image GetImageTarget()
    {
        return icon;
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        Image rootImage = GetComponent<Image>();
        if (rootImage != null) rootImage.enabled = visible;
    }

    private void SetCharacterVisuals(Sprite fallbackSprite)
    {
        // Video path disabled for now; static illustrations only.
        // bool hasClip = clip != null && video != null;
        //
        // if (video != null)
        // {
        //     if (hasClip)
        //     {
        //         video.enabled = true;
        //         video.clip = clip;
        //         video.Play();
        //     }
        //     else
        //     {
        //         video.Stop();
        //         video.enabled = false;
        //     }
        // }
        //
        // if (rawImage != null) rawImage.enabled = hasClip;
        if (video != null) video.enabled = false;
        if (rawImage != null) rawImage.enabled = false;

        Image targetImage = GetImageTarget();
        if (targetImage != null)
        {
            targetImage.enabled = true;
            targetImage.sprite = fallbackSprite;
        }
    }

    private void RefreshPlayedCards(Character c)
    {
        ClearPlayedCardInstances();

        if (c == null || c.playedCardSpritesThisTurn == null || c.playedCardSpritesThisTurn.Count == 0)
        {
            SetPlayedCardVisible(false);
            return;
        }

        BindPlayedCardTemplate();
        if (playedCard == null)
        {
            SetPlayedCardVisible(false);
            return;
        }

        Transform parent = cardCanvasGroup != null ? cardCanvasGroup.transform : playedCard.transform.parent;
        if (parent == null)
        {
            SetPlayedCardVisible(false);
            return;
        }

        playedCard.SetActive(false);
        for (int i = 0; i < c.playedCardSpritesThisTurn.Count; i++)
        {
            Sprite playedSprite = c.playedCardSpritesThisTurn[i];
            if (playedSprite == null) continue;

            GameObject instance = Instantiate(playedCard, parent);
            instance.name = $"PlayedCard_{i + 1}";
            instance.SetActive(true);

            PlayedCard playedCardComponent = instance.GetComponent<PlayedCard>();
            playedCardComponent?.Initialize(playedSprite);

            playedCardInstances.Add(instance);
        }

        SetPlayedCardVisible(playedCardInstances.Count > 0);
    }

    private void SetPlayedCardVisible(bool visible)
    {
        if (cardCanvasGroup != null)
        {
            cardCanvasGroup.alpha = visible ? 1f : 0f;
            cardCanvasGroup.interactable = visible;
            cardCanvasGroup.blocksRaycasts = visible;
        }
    }

    private void BindPlayedCardTemplate()
    {
        if (playedCard != null)
        {
            playedCard.SetActive(false);
            if (cardCanvasGroup == null)
            {
                Transform parent = playedCard.transform.parent;
                if (parent != null)
                {
                    cardCanvasGroup = parent.GetComponent<CanvasGroup>();
                    if (cardCanvasGroup == null)
                    {
                        cardCanvasGroup = parent.gameObject.AddComponent<CanvasGroup>();
                    }
                }
            }
            return;
        }

        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child != null && string.Equals(child.name, "playedCard", System.StringComparison.OrdinalIgnoreCase))
            {
                playedCard = child.gameObject;
                playedCard.SetActive(false);
                if (cardCanvasGroup == null)
                {
                    Transform parent = child.parent;
                    if (parent != null)
                    {
                        cardCanvasGroup = parent.GetComponent<CanvasGroup>();
                        if (cardCanvasGroup == null)
                        {
                            cardCanvasGroup = parent.gameObject.AddComponent<CanvasGroup>();
                        }
                    }
                }
                break;
            }
        }
    }

    private void ClearPlayedCardInstances()
    {
        for (int i = 0; i < playedCardInstances.Count; i++)
        {
            GameObject instance = playedCardInstances[i];
            if (instance != null)
            {
                Destroy(instance);
            }
        }
        playedCardInstances.Clear();
    }

    private void RefreshArtifacts(List<Artifact> artifacts)
    {
        int requiredCount = artifacts != null ? artifacts.Count : 0;

        for (int i = artifactRenderers.Count; i < requiredCount; i++)
        {
            GameObject artifactGO = Instantiate(artifactPrefab, artifactsGridLayoutTransform);
            ArtifactRenderer renderer = artifactGO.GetComponent<ArtifactRenderer>();
            artifactRenderers.Add(renderer);
        }

        for (int i = 0; i < artifactRenderers.Count; i++)
        {
            ArtifactRenderer renderer = artifactRenderers[i];
            if (renderer == null) continue;

            bool active = i < requiredCount;
            renderer.gameObject.SetActive(active);
            if (!active) continue;

            Artifact artifact = artifacts[i];
            renderer.gameObject.name = artifact != null ? artifact.artifactName : $"Artifact {i + 1}";
            renderer.Initialize(artifact);
        }
    }

    private string BuildSelectedCharacterTitle(Character c)
    {
        if (c == null) return string.Empty;

        Army army = c.GetArmy();
        if (army == null || army.GetSize() < 1)
        {
            return $"<u>{c.characterName}</u> (wandering)";
        }

        return $"<u>{c.characterName}</u>, leading an army of {BuildArmyTroopSummary(c, army)}";
    }

    private string BuildArmyTroopSummary(Character c, Army army)
    {
        List<string> troops = army != null
            ? army.GetTroopGroups()
                .Where(group => group != null && group.amount > 0)
                .Select(group => $"{group.amount}<sprite name=\"{group.troopType.ToString().ToLower()}\">{group.troopName}")
                .ToList()
            : new List<string>();

        return string.Join(", ", troops);
    }

}
