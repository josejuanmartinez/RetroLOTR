using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Shows a card enlarged ("unfolded") in the center of the screen. Extracted out of
// CardBloomWheel so any token card (not just ones in the now-retired bloom wheel) can
// unfold into a full card on hover, per the same visual language (fly-in, backdrop fade).
// Also supports previewing several cards at once (e.g. a character card plus its army's
// cards), laid out in a horizontal row that scales down to stay within the screen width.
public class CardCenterPreview : MonoBehaviour
{
    public static CardCenterPreview Instance { get; private set; }

    [Header("Center Preview")]
    [Tooltip("Optional RectTransform the preview is parented under. If unassigned, the preview is centered on the parent canvas.")]
    [SerializeField] private RectTransform centerPreviewAnchor;
    [SerializeField] private float centerPreviewScale = 1.5f;

    [Header("Hover Safety Net")]
    [Tooltip("For hover-triggered previews only (see ShowPreview's hoverDriven param): once the " +
        "mouse has moved this many screen pixels away from where the preview appeared, it is " +
        "force-hidden even if the hovered source never fired its own hide (e.g. a destroyed/" +
        "deactivated hover target, or a missed OnPointerExit). Scripted (non-hover) previews — " +
        "PC/region grant reveals — are exempt so an incidental mouse move can't cut them short.")]
    [SerializeField] private float hoverAutoHideDistance = 160f;

    [Header("Center Preview - Multiple Cards")]
    [Tooltip("Horizontal gap (template-local units, before scaling) between cards when previewing more than one at once.")]
    [SerializeField] private float multiCardSpacing = 40f;
    [Tooltip("Fraction of the available canvas width the combined row of cards is allowed to occupy before being scaled down to fit.")]
    [Range(0.5f, 1f)][SerializeField] private float multiCardMaxWidthFraction = 0.92f;

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
    // [Tooltip("Seconds the outgoing card takes to fade/scale away.")]
    // [SerializeField] private float previewExitDuration = 0.16f;

    private Canvas parentCanvas;
    private readonly List<GameObject> centerPreviewInstances = new();
    private readonly List<RectTransform> centerPreviewRects = new();
    private readonly List<CanvasGroup> centerPreviewGroups = new();
    private readonly List<Vector2> centerPreviewTargetPositions = new();
    private float centerPreviewFinalScale;
    private float previewIntroT;
    private float backdropAlpha;
    private bool previewActive;
    private float previewSpeedMultiplier = 1f;
    private bool hoverDrivenActive;
    private Vector2 hoverAnchorMousePos;

    // Resolved at runtime so newly-added serialized fields left at 0 still animate instead
    // of leaving the preview stuck invisible at alpha 0.
    private float TransitionSpeed => (previewTransitionSpeed > 0.01f ? previewTransitionSpeed : 9f) * previewSpeedMultiplier;
    private float BackdropMax => backdropMaxAlpha > 0.001f ? Mathf.Clamp01(backdropMaxAlpha) : 0.85f;
    private float IntroStartScale => previewIntroStartScale > 0.001f ? previewIntroStartScale : 0.55f;

    public RectTransform CurrentPreviewRect => centerPreviewRects.Count > 0 ? centerPreviewRects[0] : null;

    // Above every CardBloomWheel this can be triggered from — including the SituationCardsUI
    // opportunity-card wheel (CardBloomOverlay), which lives inside SituationCardsUI's own
    // prefab and therefore inherits its canvas at sortingOrder 200 (a plain "above the base
    // gameplay canvas" order like 50 rendered behind it). Still below LevelChangeEffectUI's 210.
    private const int PreviewSortingOrder = 205;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        parentCanvas = GetComponentInParent<Canvas>();

        Canvas previewCanvas = GetComponent<Canvas>();
        if (previewCanvas == null) previewCanvas = gameObject.AddComponent<Canvas>();
        previewCanvas.overrideSorting = true;
        previewCanvas.sortingOrder = PreviewSortingOrder;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (previewActive && hoverDrivenActive
            && Vector2.Distance(Input.mousePosition, hoverAnchorMousePos) > hoverAutoHideDistance)
        {
            HidePreview();
        }
        AnimateCenterPreview();
    }

    public void ShowPreview(CardData data, float speedMultiplier = 1f, bool hoverDriven = false, Vector3? worldAnchor = null, Camera worldAnchorCamera = null, System.Action onClick = null)
    {
        if (data == null) return;
        ShowPreview(new List<CardData> { data }, speedMultiplier, hoverDriven, worldAnchor, worldAnchorCamera, onClick);
    }

    // Shared by every character-hover site (roster lists, hex map): the character's own
    // card, plus one card per distinct troop type if they command an army. includeArmyCards
    // lets callers hide troop composition for characters that aren't fully scouted, matching
    // SelectedCharacterIcon.RefreshHoverPreview's existing showArtifacts gating. Every caller
    // is a hover source, so this always opts into the mouse-move safety net (see ShowPreview).
    public void ShowPreviewForCharacter(Character character, bool includeArmyCards = true)
    {
        if (character == null) return;
        DeckManager deckManager = DeckManager.Instance != null ? DeckManager.Instance : DeckManager.Instance;
        if (deckManager == null) return;

        List<CardData> previewCards = new();
        CardData characterCard = deckManager.FindAnyCardByName(character.characterName);
        if (characterCard != null) previewCards.Add(characterCard);

        if (includeArmyCards && character.IsArmyCommander())
        {
            Army army = character.GetArmy();
            IEnumerable<string> troopNames = army?.GetTroopGroups()
                .Where(group => group != null && !string.IsNullOrWhiteSpace(group.troopName))
                .Select(group => group.troopName)
                .Distinct(System.StringComparer.OrdinalIgnoreCase);
            foreach (string troopName in troopNames ?? Enumerable.Empty<string>())
            {
                CardData troopCard = deckManager.FindArmyCardByName(troopName);
                if (troopCard != null) previewCards.Add(troopCard);
            }
        }

        if (previewCards.Count > 0) ShowPreview(previewCards, hoverDriven: true);
    }

    // Shows several cards side by side (e.g. a character plus its army's cards). A single
    // entry behaves identically to ShowPreview(CardData) — same centered position and scale.
    // hoverDriven marks this as a hover-triggered preview: once shown, if the mouse strays
    // hoverAutoHideDistance pixels from where it was at that moment, the preview force-hides
    // itself even if the hover source never calls HidePreview (see Update's safety net).
    // Leave false for scripted/timed reveals that aren't tied to the cursor sitting still.
    // worldAnchor: when supplied (e.g. by CardBloomWheel, whose own tokens fan out around a
    // world-anchored hex rather than this preview's usual fixed centerPreviewAnchor spot),
    // the preview is centered on that world point instead — otherwise the enlarged card and
    // the ring of tokens it "bloomed" from end up anchored to two unrelated screen positions.
    public void ShowPreview(IReadOnlyList<CardData> cardsData, float speedMultiplier = 1f, bool hoverDriven = false, Vector3? worldAnchor = null, Camera worldAnchorCamera = null, System.Action onClick = null)
    {
        List<CardData> validCards = cardsData?.Where(c => c != null).ToList();
        if (validCards == null || validCards.Count == 0) return;

        ClearPreview();
        previewSpeedMultiplier = Mathf.Max(0.1f, speedMultiplier);
        hoverDrivenActive = hoverDriven;
        hoverAnchorMousePos = Input.mousePosition;
        // Keep generated cards beneath this object's dedicated override-sorting Canvas.
        // Parenting them to parentCanvas.rootCanvas escaped that Canvas entirely, so its
        // sortingOrder could not put previews in front of the SituationCardsUI bloom.
        Transform parent = centerPreviewAnchor != null
            ? (Transform)centerPreviewAnchor
            : transform;

        Vector2 anchorOffset = Vector2.zero;
        if (worldAnchor.HasValue && parent is RectTransform parentRect)
        {
            Camera sceneCam = worldAnchorCamera != null ? worldAnchorCamera : Camera.main;
            Canvas uiCanvas = parentRect.GetComponentInParent<Canvas>();
            Camera uiCam = uiCanvas != null && uiCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? uiCanvas.worldCamera : null;
            if (sceneCam != null)
            {
                Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(sceneCam, worldAnchor.Value);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, uiCam, out anchorOffset);
            }
        }

        DeckManager deckManager = DeckManager.Instance != null ? DeckManager.Instance : DeckManager.Instance;
        GameObject template = deckManager != null ? deckManager.GetCardPrefabTemplate() : null;
        if (template == null) return;

        float cardWidth = template.TryGetComponent(out RectTransform templateRect) ? templateRect.rect.width : 0f;
        if (cardWidth <= 0f) cardWidth = 1f;

        int count = validCards.Count;
        float step = cardWidth + multiCardSpacing;
        float contentWidth = count > 1 ? (count - 1) * step + cardWidth : cardWidth;

        float availableWidth = ResolveAvailableWidth() * multiCardMaxWidthFraction;
        float scale = centerPreviewScale;
        if (availableWidth > 0f && contentWidth * scale > availableWidth)
            scale = availableWidth / contentWidth;
        centerPreviewFinalScale = scale;

        for (int i = 0; i < count; i++)
        {
            GameObject instance = Instantiate(template, parent, false);
            instance.name = $"CardCenterPreview_{validCards[i].name}";
            instance.SetActive(true);

            RectTransform rect = instance.GetComponent<RectTransform>();
            Vector2 targetPos = Vector2.zero;
            if (rect != null)
            {
                Vector2 center = new(0.5f, 0.5f);
                rect.anchorMin = center;
                rect.anchorMax = center;
                rect.pivot = center;

                float unscaledX = count > 1 ? (i * step) - contentWidth * 0.5f + cardWidth * 0.5f : 0f;
                targetPos = new Vector2(unscaledX * scale, 0f) + anchorOffset;
            }

            Card previewCard = instance.GetComponent<Card>();
            if (previewCard != null)
            {
                // The center preview must always show official card art, never a same-named
                // sprite from the UI/Animation/Characters/Decks folders ResolveCardArtwork also
                // searches by default (see Illustrations.IllustrationsAddressRoots).
                previewCard.UseCardArtFolderOnly = true;
                previewCard.InitializePreview(validCards[i]);
                previewCard.SuppressHoverEffects = true;
                previewCard.ShowRealCard();
            }

            CanvasGroup group = instance.GetComponent<CanvasGroup>();
            if (group == null) group = instance.AddComponent<CanvasGroup>();
            group.blocksRaycasts = onClick != null;
            group.interactable = onClick != null;
            if (onClick != null) AttachClickCatcher(instance, onClick);

            centerPreviewInstances.Add(instance);
            centerPreviewRects.Add(rect);
            centerPreviewGroups.Add(group);
            centerPreviewTargetPositions.Add(targetPos);
        }

        previewActive = true;
        previewIntroT = 0f;
        ApplyPreviewPose(0f);
        PlaceBackdropBehindPreview(parent);
        for (int i = 0; i < centerPreviewRects.Count; i++)
            centerPreviewRects[i]?.SetAsLastSibling();
    }

    public void HidePreview()
    {
        if (!previewActive) return;
        previewActive = false;
        ClearPreview();
    }

    // Hard teardown: kills all current preview instances instantly and snaps the backdrop off.
    private void ClearPreview()
    {
        for (int i = 0; i < centerPreviewInstances.Count; i++)
        {
            if (centerPreviewInstances[i] != null) Destroy(centerPreviewInstances[i]);
        }
        centerPreviewInstances.Clear();
        centerPreviewRects.Clear();
        centerPreviewGroups.Clear();
        centerPreviewTargetPositions.Clear();
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

        if (centerPreviewRects.Count == 0) return;
        previewIntroT = Mathf.MoveTowards(previewIntroT, 1f, Time.deltaTime * TransitionSpeed);
        ApplyPreviewPose(previewIntroT);
    }

    // Positions/scales/fades every active preview card at intro progress t (0 = fly-in
    // start, 1 = settled), each relative to its own resting position in the row.
    private void ApplyPreviewPose(float t)
    {
        if (centerPreviewRects.Count == 0) return;

        float eased = EaseOutBack(Mathf.Clamp01(t));
        float startScale = centerPreviewFinalScale * IntroStartScale;
        float scale = Mathf.LerpUnclamped(startScale, centerPreviewFinalScale, eased);
        float rotation = Mathf.LerpUnclamped(previewIntroTilt, 0f, eased);
        float alpha = Mathf.Clamp01(t * 1.6f);

        for (int i = 0; i < centerPreviewRects.Count; i++)
        {
            RectTransform rect = centerPreviewRects[i];
            if (rect == null) continue;

            Vector2 target = centerPreviewTargetPositions[i];
            Vector2 introPos = target + previewIntroOffset;
            rect.anchoredPosition = Vector2.LerpUnclamped(introPos, target, eased);
            rect.localScale = Vector3.one * scale;
            rect.localRotation = Quaternion.Euler(0f, 0f, rotation);

            CanvasGroup group = centerPreviewGroups[i];
            if (group != null) group.alpha = alpha;
        }
    }

    // Canvas-local width available for the preview row, before multiCardMaxWidthFraction is applied.
    private float ResolveAvailableWidth()
    {
        RectTransform reference = centerPreviewAnchor != null
            ? centerPreviewAnchor
            : transform as RectTransform;
        return reference != null ? reference.rect.width : 0f;
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

    // The clone's own Card component (SuppressHoverEffects=true) would otherwise handle a
    // click itself and re-evaluate playability against its own (stale/unset) LastKnownPlayable
    // - it isn't the bloom token that already computed that. A full-rect catcher on top,
    // forwarding to the caller's onClick, routes the click to the real token's already-known
    // state instead (see CardBloomWheel.PlayCardAtIndex).
    private static void AttachClickCatcher(GameObject instance, System.Action onClick)
    {
        GameObject catcher = new("ClickCatcher", typeof(RectTransform));
        catcher.transform.SetParent(instance.transform, false);
        RectTransform catcherRect = catcher.GetComponent<RectTransform>();
        catcherRect.anchorMin = Vector2.zero;
        catcherRect.anchorMax = Vector2.one;
        catcherRect.offsetMin = Vector2.zero;
        catcherRect.offsetMax = Vector2.zero;

        Image image = catcher.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = true;

        PreviewClickForwarder forwarder = catcher.AddComponent<PreviewClickForwarder>();
        forwarder.onClick = onClick;

        catcher.transform.SetAsLastSibling();
    }

    private sealed class PreviewClickForwarder : MonoBehaviour, IPointerClickHandler
    {
        public System.Action onClick;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left) return;
            onClick?.Invoke();
        }
    }
}
