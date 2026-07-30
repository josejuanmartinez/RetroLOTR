using UnityEngine;

// Shows a card enlarged ("unfolded") in the center of the screen. Extracted out of
// CardBloomWheel so any token card (not just ones in the now-retired bloom wheel) can
// unfold into a full card on hover, per the same visual language (fly-in, backdrop fade).
public class CardCenterPreview : MonoBehaviour
{
    public static CardCenterPreview Instance { get; private set; }

    [Header("Center Preview")]
    [Tooltip("Optional RectTransform the preview is parented under. If unassigned, the preview is centered on the parent canvas.")]
    [SerializeField] private RectTransform centerPreviewAnchor;
    [SerializeField] private float centerPreviewScale = 1.5f;

    [Header("Center Preview Transition")]
    [Tooltip("Full-screen black backdrop (with CanvasGroup) faded in while a card is previewed and faded out when hidden.")]
    [SerializeField] private CanvasGroup centerPreviewBackdrop;
    [Tooltip("Alpha the black backdrop reaches when a card preview is fully shown.")]
    [Range(0f, 1f)][SerializeField] private float backdropMaxAlpha = 0.85f;
    [Tooltip("Higher = snappier intro/backdrop transition.")]
    [SerializeField] private float previewTransitionSpeed = 9f;
    [Tooltip("Scale (relative to centerPreviewScale) the card starts at when flying in.")]
    [Range(0.1f, 1f)][SerializeField] private float previewIntroStartScale = 0.55f;
    [Tooltip("Local offset the card starts at before settling to center (epic fly-in).")]
    [SerializeField] private Vector2 previewIntroOffset = new(0f, -180f);
    [Tooltip("Z rotation (degrees) the card starts tilted at before settling upright.")]
    [SerializeField] private float previewIntroTilt = 14f;
    [Tooltip("Seconds the outgoing card takes to fade/scale away.")]
    [SerializeField] private float previewExitDuration = 0.16f;

    private Canvas parentCanvas;
    private GameObject centerPreviewInstance;
    private RectTransform centerPreviewRect;
    private CanvasGroup centerPreviewGroup;
    private float previewIntroT;
    private float backdropAlpha;
    private bool previewActive;

    // Resolved at runtime so newly-added serialized fields left at 0 still animate instead
    // of leaving the preview stuck invisible at alpha 0.
    private float TransitionSpeed => previewTransitionSpeed > 0.01f ? previewTransitionSpeed : 9f;
    private float BackdropMax => backdropMaxAlpha > 0.001f ? Mathf.Clamp01(backdropMaxAlpha) : 0.85f;
    private float IntroStartScale => previewIntroStartScale > 0.001f ? previewIntroStartScale : 0.55f;

    public RectTransform CurrentPreviewRect => centerPreviewRect;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        parentCanvas = GetComponentInParent<Canvas>();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        AnimateCenterPreview();
    }

    public void ShowPreview(CardData data)
    {
        if (data == null) return;

        ClearPreview();
        Transform parent = centerPreviewAnchor != null
            ? (Transform)centerPreviewAnchor
            : (parentCanvas != null ? parentCanvas.rootCanvas.transform : transform);

        DeckManager deckManager = DeckManager.Instance != null ? DeckManager.Instance : FindFirstObjectByType<DeckManager>();
        GameObject template = deckManager != null ? deckManager.GetCardPrefabTemplate() : null;
        if (template == null) return;

        centerPreviewInstance = Instantiate(template, parent, false);
        centerPreviewInstance.name = $"CardCenterPreview_{data.name}";
        centerPreviewInstance.SetActive(true);
        centerPreviewRect = centerPreviewInstance.GetComponent<RectTransform>();
        if (centerPreviewRect != null)
        {
            Vector2 center = new(0.5f, 0.5f);
            centerPreviewRect.anchorMin = center;
            centerPreviewRect.anchorMax = center;
            centerPreviewRect.pivot = center;
        }

        Card previewCard = centerPreviewInstance.GetComponent<Card>();
        if (previewCard != null)
        {
            previewCard.InitializePreview(data);
            previewCard.SuppressHoverEffects = true;
            previewCard.ShowRealCard();
        }

        centerPreviewGroup = centerPreviewInstance.GetComponent<CanvasGroup>();
        if (centerPreviewGroup == null) centerPreviewGroup = centerPreviewInstance.AddComponent<CanvasGroup>();
        centerPreviewGroup.blocksRaycasts = false;
        centerPreviewGroup.interactable = false;
        previewActive = true;
        previewIntroT = 0f;
        ApplyPreviewPose(0f);
        PlaceBackdropBehindPreview(parent);
        if (centerPreviewRect != null) centerPreviewRect.SetAsLastSibling();
    }

    public void HidePreview()
    {
        if (!previewActive) return;
        previewActive = false;
        ClearPreview();
    }

    // Hard teardown: kills the current preview instantly and snaps the backdrop off.
    private void ClearPreview()
    {
        if (centerPreviewInstance != null)
        {
            Destroy(centerPreviewInstance);
            centerPreviewInstance = null;
        }
        centerPreviewRect = null;
        centerPreviewGroup = null;
        previewIntroT = 0f;
        backdropAlpha = 0f;
        if (centerPreviewBackdrop != null)
        {
            centerPreviewBackdrop.alpha = 0f;
            centerPreviewBackdrop.blocksRaycasts = false;
            centerPreviewBackdrop.interactable = false;
            if (centerPreviewBackdrop.gameObject.activeSelf)
                centerPreviewBackdrop.gameObject.SetActive(false);
        }
    }

    // Drives the backdrop fade and the active preview's fly-in every frame.
    private void AnimateCenterPreview()
    {
        bool wantPreview = previewActive;

        float backdropTarget = wantPreview ? BackdropMax : 0f;
        backdropAlpha = Mathf.MoveTowards(backdropAlpha, backdropTarget, Time.deltaTime * TransitionSpeed * BackdropMax);
        if (centerPreviewBackdrop != null)
        {
            bool shouldBeActive = backdropAlpha > 0.001f;
            if (centerPreviewBackdrop.gameObject.activeSelf != shouldBeActive)
                centerPreviewBackdrop.gameObject.SetActive(shouldBeActive);
            centerPreviewBackdrop.alpha = backdropAlpha;
            centerPreviewBackdrop.blocksRaycasts = false;
            centerPreviewBackdrop.interactable = false;
        }

        if (centerPreviewRect == null) return;
        previewIntroT = Mathf.MoveTowards(previewIntroT, 1f, Time.deltaTime * TransitionSpeed);
        ApplyPreviewPose(previewIntroT);
    }

    // Positions/scales/fades the active preview at intro progress t (0 = fly-in start, 1 = settled).
    private void ApplyPreviewPose(float t)
    {
        if (centerPreviewRect == null) return;

        float eased = EaseOutBack(Mathf.Clamp01(t));
        float startScale = centerPreviewScale * IntroStartScale;
        float scale = Mathf.LerpUnclamped(startScale, centerPreviewScale, eased);

        centerPreviewRect.anchoredPosition = Vector2.LerpUnclamped(previewIntroOffset, Vector2.zero, eased);
        centerPreviewRect.localScale = Vector3.one * scale;
        centerPreviewRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(previewIntroTilt, 0f, eased));

        if (centerPreviewGroup != null)
            centerPreviewGroup.alpha = Mathf.Clamp01(t * 1.6f);
    }

    // Relocates the user-assigned backdrop to sit as a full-screen sibling immediately
    // beneath the preview, so it reliably renders behind the card and dims the screen,
    // regardless of where it originally lived in the hierarchy.
    private void PlaceBackdropBehindPreview(Transform parent)
    {
        if (centerPreviewBackdrop == null || parent == null) return;

        Transform bg = centerPreviewBackdrop.transform;
        if (bg.parent != parent) bg.SetParent(parent, false);

        if (bg is RectTransform bgRect)
        {
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            bgRect.localScale = Vector3.one;
        }

        bg.SetAsLastSibling();
    }

    private static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float xm1 = x - 1f;
        return 1f + c3 * xm1 * xm1 * xm1 + c1 * xm1 * xm1;
    }
}
