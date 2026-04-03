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

    [Header("Played cards")]
    [SerializeField] private CanvasGroup card1CanvasGroup;
    [SerializeField] private Image card1;
    
    [Header("Selection Change Effect")]
    [SerializeField] private float selectionFxDuration = 0.24f;
    [SerializeField] private float selectionFxDurationMultiplier = 3f;
    [SerializeField] private float selectionFxDarkenMin = 0.15f;

    // private Videos videos;
    private Illustrations illustrations;
    private ActionsManager actionsManager;
    private CanvasGroup canvasGroup;
    private Image rootImage;
    private Image borderImage;
    private Color rootDefaultColor = Color.white;
    private Color borderDefaultColor = Color.white;
    private Vector3 defaultScale = Vector3.one;
    private bool dropHintActive;
    private float dropHintProximity;
    private bool dropHintLocked;
    private bool detailsHiddenForDropPreview;
    private readonly Dictionary<GameObject, bool> cachedChildActiveStates = new();
    private bool cacheIconEnabled;
    private bool cacheRawImageEnabled;
    private bool cacheTextEnabled;
    private bool cacheAlignmentEnabled;
    private bool cacheHealthEnabled;
    private int lastRefreshedCharacterId = int.MinValue;
    private Coroutine selectionFxCoroutine;
    private Color iconDefaultColor = Color.white;
    private Color rawImageDefaultColor = Color.white;

    private void Awake()
    {
        rootImage = GetComponent<Image>();
        borderImage = border != null ? border.GetComponent<Image>() : null;
        if (rootImage != null) rootDefaultColor = rootImage.color;
        if (borderImage != null) borderDefaultColor = borderImage.color;
        if (icon != null) iconDefaultColor = icon.color;
        if (rawImage != null) rawImageDefaultColor = rawImage.color;
        defaultScale = transform.localScale;
    }

    private void OnDisable()
    {
        SetDropTargetHighlight(false);
        StopSelectionChangeFx();
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

        if (hidden)
        {
            cacheIconEnabled = icon != null && icon.enabled;
            cacheRawImageEnabled = rawImage != null && rawImage.enabled;
            cacheTextEnabled = textWidget != null && textWidget.enabled;
            cacheAlignmentEnabled = alignmentIcon != null && alignmentIcon.enabled;
            cacheHealthEnabled = health != null && health.enabled;

            cachedChildActiveStates.Clear();
            int childCount = transform.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child == null) continue;
                GameObject childGO = child.gameObject;
                if (childGO == null) continue;
                if (border != null && childGO == border) continue; // keep shell visible for magnet feedback

                cachedChildActiveStates[childGO] = childGO.activeSelf;
                childGO.SetActive(false);
            }

            if (icon != null) icon.enabled = false;
            if (rawImage != null) rawImage.enabled = false;
            if (textWidget != null) textWidget.enabled = false;
            if (alignmentIcon != null) alignmentIcon.enabled = false;
            if (health != null) health.enabled = false;
        }
        else
        {
            foreach (KeyValuePair<GameObject, bool> state in cachedChildActiveStates)
            {
                if (state.Key == null) continue;
                state.Key.SetActive(state.Value);
            }
            cachedChildActiveStates.Clear();

            if (icon != null) icon.enabled = cacheIconEnabled;
            if (rawImage != null) rawImage.enabled = cacheRawImageEnabled;
            if (textWidget != null) textWidget.enabled = cacheTextEnabled;
            if (alignmentIcon != null) alignmentIcon.enabled = cacheAlignmentEnabled;
            if (health != null) health.enabled = cacheHealthEnabled;
        }

        detailsHiddenForDropPreview = hidden;
    }

    // Update is called once per frame
    public void Refresh(Character c)
    {
        bool characterChanged = c != null && c.GetInstanceID() != lastRefreshedCharacterId;
        SetDropTargetHighlight(false);
        SetVisible(true);
        border.SetActive(true);
        SetCharacterVisuals(GetIllustrationByName(c.characterName));
        alignmentIcon.enabled = true;
        alignmentIcon.sprite = GetIllustrationByName(c.GetAlignment().ToString());
        string baseHoverText = c.GetHoverText(true, false, false, true, false, false);
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

        foreach (Transform artifactChild in artifactsGridLayoutTransform)
        {
            Destroy(artifactChild.gameObject);
        }

        c.artifacts.ForEach(x =>
        {
            GameObject artifactGO = Instantiate(artifactPrefab, artifactsGridLayoutTransform);
            artifactGO.name = x.artifactName;
            artifactGO.GetComponent<ArtifactRenderer>().Initialize(x);
        });
        
        RefreshMovementLeft(c);
        RefreshPlayedCard(c);

        if (c != null)
        {
            lastRefreshedCharacterId = c.GetInstanceID();
            if (characterChanged)
            {
                PlaySelectionChangeFx();
            }
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
        textWidget.text = hoverText ?? "";

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

        foreach (Transform artifactChild in artifactsGridLayoutTransform)
        {
            Destroy(artifactChild.gameObject);
        }

        if (showArtifacts)
        {
            c.artifacts.ForEach(x =>
            {
                GameObject artifactGO = Instantiate(artifactPrefab, artifactsGridLayoutTransform);
                artifactGO.name = x.artifactName;
                artifactGO.GetComponent<ArtifactRenderer>().Initialize(x);
            });
        }

        SetPlayedCardVisible(false);
    }


    // Update is called once per frame
    public void Hide()
    {
        SetDropTargetHighlight(false);
        StopSelectionChangeFx();
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
        SetPlayedCardVisible(false);
        lastRefreshedCharacterId = int.MinValue;
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

    private void RefreshPlayedCard(Character c)
    {
        if (c == null)
        {
            SetPlayedCardVisible(false);
            return;
        }

        Sprite playedSprite = null;

        if (!string.IsNullOrWhiteSpace(c.lastPlayedCardSpriteNameThisTurn))
        {
            playedSprite = GetIllustrationByName(c.lastPlayedCardSpriteNameThisTurn);
        }

        if (playedSprite == null && !string.IsNullOrWhiteSpace(c.lastPlayedActionClassNameThisTurn))
        {
            playedSprite = GetIllustrationByName(c.lastPlayedActionClassNameThisTurn);
            if (playedSprite == null)
            {
                playedSprite = ResolveActionSpriteByClassName(c.lastPlayedActionClassNameThisTurn);
            }
        }

        if (playedSprite == null && !string.IsNullOrWhiteSpace(c.lastPlayedActionNameThisTurn))
        {
            playedSprite = GetIllustrationByName(c.lastPlayedActionNameThisTurn);
        }

        if (playedSprite == null)
        {
            SetPlayedCardVisible(false);
            return;
        }

        if (card1 != null)
        {
            card1.sprite = playedSprite;
            card1.enabled = true;
        }
        SetPlayedCardVisible(true);
    }

    private Sprite ResolveActionSpriteByClassName(string className)
    {
        if (string.IsNullOrWhiteSpace(className)) return null;
        if (actionsManager == null) actionsManager = FindFirstObjectByType<ActionsManager>();
        if (actionsManager == null || actionsManager.characterActions == null) return null;

        for (int i = 0; i < actionsManager.characterActions.Length; i++)
        {
            CharacterAction action = actionsManager.characterActions[i];
            if (action == null) continue;
            if (!string.Equals(action.GetType().Name, className, System.StringComparison.OrdinalIgnoreCase)) continue;
            if (action.actionSprite != null) return action.actionSprite;
        }

        return null;
    }

    private void SetPlayedCardVisible(bool visible)
    {
        if (card1CanvasGroup != null)
        {
            card1CanvasGroup.alpha = visible ? 1f : 0f;
            card1CanvasGroup.interactable = visible;
            card1CanvasGroup.blocksRaycasts = visible;
        }
        if (card1 != null)
        {
            card1.enabled = visible;
        }
    }

    private void PlaySelectionChangeFx()
    {
        if (dropHintActive) return;
        StopSelectionChangeFx();
        selectionFxCoroutine = StartCoroutine(SelectionChangeFxCoroutine());
    }

    private void StopSelectionChangeFx()
    {
        if (selectionFxCoroutine != null)
        {
            StopCoroutine(selectionFxCoroutine);
            selectionFxCoroutine = null;
        }
        ResetSelectionFxVisuals();
    }

    private System.Collections.IEnumerator SelectionChangeFxCoroutine()
    {
        float duration = Mathf.Max(0.05f, selectionFxDuration * Mathf.Max(0.1f, selectionFxDurationMultiplier) * 5f);
        float halfDuration = duration * 0.5f;
        float elapsed = 0f;

        while (elapsed < halfDuration)
        {
            if (dropHintActive)
            {
                ResetSelectionFxVisuals();
                selectionFxCoroutine = null;
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);
            ApplySelectionFxTint(Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            if (dropHintActive)
            {
                ResetSelectionFxVisuals();
                selectionFxCoroutine = null;
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);
            ApplySelectionFxTint(Mathf.SmoothStep(1f, 0f, t));
            yield return null;
        }

        ResetSelectionFxVisuals();
        selectionFxCoroutine = null;
    }

    private void ApplySelectionFxTint(float darkenLerp)
    {
        float brightness = Mathf.Lerp(1f, Mathf.Clamp01(selectionFxDarkenMin), Mathf.Clamp01(darkenLerp));

        if (icon != null)
        {
            icon.color = new Color(
                iconDefaultColor.r * brightness,
                iconDefaultColor.g * brightness,
                iconDefaultColor.b * brightness,
                iconDefaultColor.a);
        }

        if (rawImage != null)
        {
            rawImage.color = new Color(
                rawImageDefaultColor.r * brightness,
                rawImageDefaultColor.g * brightness,
                rawImageDefaultColor.b * brightness,
                rawImageDefaultColor.a);
        }
    }

    private void ResetSelectionFxVisuals()
    {
        transform.localScale = defaultScale;
        if (rootImage != null) rootImage.color = rootDefaultColor;
        if (borderImage != null) borderImage.color = borderDefaultColor;
        if (icon != null) icon.color = iconDefaultColor;
        if (rawImage != null) rawImage.color = rawImageDefaultColor;
    }
}
