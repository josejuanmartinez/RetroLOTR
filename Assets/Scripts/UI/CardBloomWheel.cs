using System.Collections.Generic;
using UnityEngine;

public class CardBloomWheel : MonoBehaviour
{
    [Header("Bloom Layout")]
    [SerializeField] private float bloomRadius = 280f;
    [SerializeField] private float startAngleDeg = 180f;
    [SerializeField] private float endAngleDeg = 0f;

    [Header("Animation")]
    [SerializeField] private float bloomSpeed = 14f;
    [SerializeField] private float collapseSpeed = 22f;
    [SerializeField] private float hoverDelay = 2f;

    [Header("Lines")]
    [Tooltip("If assigned, BloomLines is reparented as first sibling under this rect's parent at Start so it renders behind it.")]
    [SerializeField] private RectTransform linesBackgroundTarget;
    [SerializeField] private Vector2 lineEndOffset = Vector2.zero;

    [Header("Card Hover")]
    [SerializeField] private float lineHitTolerance = 20f;

    [Header("Trigger")]
    [Tooltip("RectTransform that opens the bloom on mouse-enter (assign SelectedCharacterIcon's rect).")]
    [SerializeField] private RectTransform hoverTriggerRect;
    [SerializeField] private SelectedCharacterIcon selectedCharacterIcon;

    private readonly List<RectTransform> cardRects = new();
    private readonly List<CanvasGroup> cardGroups = new();
    private readonly List<Card> cardComponents = new();
    private readonly List<Vector2> bloomTargets = new();
    private readonly List<Color> cardLineColors = new();

    private bool isOpen;
    private bool isVisible = true;
    private int hoveredCardIndex = -1;
    private RectTransform rectTransform;
    private Canvas parentCanvas;
    private Transform linesGraphicTransform;

    private float cachedRadius;
    private float cachedStartAngle;
    private float cachedEndAngle;
    private float hoverTimer;

    public bool IsOpen => isOpen;
    public float LinesAlpha { get; private set; }
    public Vector2 LineEndOffset => lineEndOffset;
    public IReadOnlyList<RectTransform> CardRects => cardRects;
    public IReadOnlyList<Color> CardLineColors => cardLineColors;
    public RectTransform HoverTriggerRect => hoverTriggerRect;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        parentCanvas = GetComponentInParent<Canvas>();

        var linesGo = new GameObject("BloomLines", typeof(RectTransform));
        linesGo.transform.SetParent(transform, false);
        var linesRect = linesGo.GetComponent<RectTransform>();
        linesRect.anchorMin = Vector2.zero;
        linesRect.anchorMax = Vector2.one;
        linesRect.offsetMin = Vector2.zero;
        linesRect.offsetMax = Vector2.zero;
        linesGo.AddComponent<CardBloomLinesGraphic>().Init(this);
        linesGo.transform.SetAsFirstSibling();
        linesGraphicTransform = linesGo.transform;
    }

    private void Start()
    {
        if (linesBackgroundTarget != null && linesGraphicTransform != null)
        {
            linesGraphicTransform.SetParent(linesBackgroundTarget.parent, false);
            var lr = linesGraphicTransform.GetComponent<RectTransform>();
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = Vector2.one;
            lr.offsetMin = Vector2.zero;
            lr.offsetMax = Vector2.zero;
            linesGraphicTransform.SetAsFirstSibling();
        }
    }

    private void OnDestroy()
    {
        if (linesGraphicTransform != null)
            Destroy(linesGraphicTransform.gameObject);
    }

    private void Update()
    {
        if (!isVisible) return;

#if UNITY_EDITOR
        if (debugForcedOpen)
        {
            if (!isOpen) SetOpenState(true);
            if (bloomRadius != cachedRadius || startAngleDeg != cachedStartAngle || endAngleDeg != cachedEndAngle)
                RecalculateBloomTargets();
            hoveredCardIndex = -1;
            AnimateCards();
            LinesAlpha = 1f;
            return;
        }
#endif

        Camera cam = CanvasCamera();
        Camera triggerCam = TriggerCamera();
        bool characterActed = selectedCharacterIcon != null
            && selectedCharacterIcon.CurrentCharacter != null
            && selectedCharacterIcon.CurrentCharacter.hasActionedThisTurn;
        bool mouseOnTrigger = !characterActed && hoverTriggerRect != null &&
            RectTransformUtility.RectangleContainsScreenPoint(hoverTriggerRect, Input.mousePosition, triggerCam);
        bool mouseInArea = !characterActed && isOpen && IsMouseInsideBloomArea(cam, triggerCam);

        if (mouseOnTrigger && !isOpen)
        {
            hoverTimer += Time.deltaTime;
        }
        else if (!mouseOnTrigger && !mouseInArea)
        {
            hoverTimer = 0f;
        }

        bool shouldBeOpen = (mouseOnTrigger && hoverTimer >= hoverDelay) || mouseInArea;

        if (shouldBeOpen != isOpen)
            SetOpenState(shouldBeOpen);

        if (bloomRadius != cachedRadius || startAngleDeg != cachedStartAngle || endAngleDeg != cachedEndAngle)
            RecalculateBloomTargets();

        hoveredCardIndex = isOpen ? FindHoveredCardIndex(cam) : -1;
        AnimateCards();
        LinesAlpha = 1f;
    }

    // Called by DeckManager after spawning / clearing cards.
    public void SetCards(List<GameObject> cards)
    {
        cardRects.Clear();
        cardGroups.Clear();
        cardComponents.Clear();
        bloomTargets.Clear();
        cardLineColors.Clear();

        Colors colors = FindFirstObjectByType<Colors>();

        if (cards != null)
        {
            foreach (GameObject go in cards)
            {
                if (go == null) continue;
                RectTransform rt = go.GetComponent<RectTransform>();
                if (rt == null) continue;
                cardRects.Add(rt);
                cardGroups.Add(go.GetComponent<CanvasGroup>());

                Card card = go.GetComponent<Card>();
                cardComponents.Add(card);
                if (card != null) card.SuppressHoverEffects = true;

                cardLineColors.Add(ResolveCardColor(card, colors));
            }
        }

        RecalculateBloomTargets();
        SnapAllToCollapsed();
    }

    private static Color ResolveCardColor(Card card, Colors colors)
    {
        if (card == null || card.cardData == null || colors == null) return Color.white;
        string name = card.cardData.GetCardType() switch
        {
            CardTypeEnum.PC => "pc",
            CardTypeEnum.Land => "land",
            CardTypeEnum.Character => "character",
            CardTypeEnum.Army => "army",
            CardTypeEnum.Event => "event",
            CardTypeEnum.Action => "action",
            CardTypeEnum.Spell => "spell",
            CardTypeEnum.Encounter => "encounter",
            CardTypeEnum.Environmental => "environmental",
            _ => null
        };
        if (name == null) return Color.white;
        try { return colors.GetColorByName(name); }
        catch { return Color.white; }
    }

    public void SetVisible(bool visible)
    {
        isVisible = visible;

        CanvasGroup cg = GetComponent<CanvasGroup>();
        if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
        cg.alpha = visible ? 1f : 0f;
        cg.interactable = visible;
        cg.blocksRaycasts = visible;

        if (!visible && isOpen)
            SetOpenState(false);
    }

    private void SetOpenState(bool open)
    {
        isOpen = open;
        if (!open) hoveredCardIndex = -1;

        for (int i = 0; i < cardGroups.Count; i++)
        {
            if (cardGroups[i] == null) continue;
            cardGroups[i].blocksRaycasts = open;
            cardGroups[i].interactable = open;
        }

    }

    private void SnapAllToCollapsed()
    {
        isOpen = false;
        hoveredCardIndex = -1;
        for (int i = 0; i < cardRects.Count; i++)
        {
            if (cardRects[i] != null)
            {
                cardRects[i].anchoredPosition = Vector2.zero;
                cardRects[i].localScale = Vector3.one;
            }

            if (i < cardGroups.Count && cardGroups[i] != null)
            {
                cardGroups[i].alpha = 0f;
                cardGroups[i].blocksRaycasts = false;
                cardGroups[i].interactable = false;
            }

        }
    }

    private void RecalculateBloomTargets()
    {
        cachedRadius = bloomRadius;
        cachedStartAngle = startAngleDeg;
        cachedEndAngle = endAngleDeg;

        bloomTargets.Clear();
        int n = cardRects.Count;
        if (n == 0) return;

        for (int i = 0; i < n; i++)
        {
            float t = n > 1 ? (float)i / (n - 1) : 0.5f;
            float angleDeg = Mathf.Lerp(startAngleDeg, endAngleDeg, t);
            float rad = angleDeg * Mathf.Deg2Rad;
            bloomTargets.Add(new Vector2(
                bloomRadius * Mathf.Cos(rad),
                bloomRadius * Mathf.Sin(rad)
            ));
        }
    }

    private void AnimateCards()
    {
        float speed = isOpen ? bloomSpeed : collapseSpeed;
        bool anyHovered = hoveredCardIndex >= 0;

        for (int i = 0; i < cardRects.Count; i++)
        {
            if (cardRects[i] == null) continue;

            // When closing, ensure all cards are active so they can animate back.
            if (!isOpen && !cardRects[i].gameObject.activeSelf)
                cardRects[i].gameObject.SetActive(true);

            // When a card is hovered, hide all other cards entirely.
            if (isOpen && anyHovered)
            {
                bool shouldShow = i == hoveredCardIndex;
                if (cardRects[i].gameObject.activeSelf != shouldShow)
                    cardRects[i].gameObject.SetActive(shouldShow);
            }
            else if (isOpen && !cardRects[i].gameObject.activeSelf)
            {
                cardRects[i].gameObject.SetActive(true);
            }

            // Position
            Vector2 posTarget = isOpen && i < bloomTargets.Count ? bloomTargets[i] : Vector2.zero;
            cardRects[i].anchoredPosition = Vector2.Lerp(
                cardRects[i].anchoredPosition, posTarget, Time.deltaTime * speed);

            // Scale + token/card flip
            // Token / real-card flip — no scaling, just visibility swap.
            if (i < cardComponents.Count && cardComponents[i] != null)
            {
                bool isThisHovered = isOpen && i == hoveredCardIndex;
                if (isThisHovered) cardComponents[i].ShowRealCard();
                else cardComponents[i].ShowToken();
            }

            // Alpha — only fade in/out; no per-card dimming.
            if (i < cardGroups.Count && cardGroups[i] != null)
            {
                float alphaTarget = isOpen ? 1f : 0f;
                cardGroups[i].alpha = Mathf.Lerp(cardGroups[i].alpha, alphaTarget, Time.deltaTime * speed * 1.5f);
            }
        }
    }

    private int FindHoveredCardIndex(Camera cam)
    {
        Vector2 mouse = Input.mousePosition;
        int best = -1;
        float bestDist = float.MaxValue;
        for (int i = 0; i < cardRects.Count; i++)
        {
            if (cardRects[i] == null || !cardRects[i].gameObject.activeSelf) continue;
            if (!RectTransformUtility.RectangleContainsScreenPoint(cardRects[i], mouse, cam)) continue;
            Vector2 center = RectTransformUtility.WorldToScreenPoint(cam, cardRects[i].position);
            float dist = (mouse - center).sqrMagnitude;
            if (dist < bestDist) { bestDist = dist; best = i; }
        }
        return best;
    }

    private Camera CanvasCamera()
    {
        if (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            return parentCanvas.worldCamera;
        return null;
    }

    private Camera TriggerCamera()
    {
        if (hoverTriggerRect == null) return null;
        Canvas c = hoverTriggerRect.GetComponentInParent<Canvas>();
        if (c == null || c.renderMode == RenderMode.ScreenSpaceOverlay) return null;
        return c.worldCamera;
    }

    private bool IsMouseInsideBloomArea(Camera cam, Camera triggerCam)
    {
        for (int i = 0; i < cardRects.Count; i++)
        {
            if (cardRects[i] == null) continue;
            if (RectTransformUtility.RectangleContainsScreenPoint(cardRects[i], Input.mousePosition, cam))
                return true;
        }

        if (hoverTriggerRect != null &&
            RectTransformUtility.RectangleContainsScreenPoint(hoverTriggerRect, Input.mousePosition, triggerCam))
            return true;

        if (IsMouseNearLines(cam, triggerCam)) return true;

        return rectTransform != null &&
               RectTransformUtility.RectangleContainsScreenPoint(rectTransform, Input.mousePosition, cam);
    }

    private bool IsMouseNearLines(Camera cam, Camera triggerCam)
    {
        if (hoverTriggerRect == null || cardRects.Count == 0) return false;
        Vector2 triggerScreen = RectTransformUtility.WorldToScreenPoint(triggerCam,
            hoverTriggerRect.TransformPoint(hoverTriggerRect.rect.center));
        Vector2 mouse = Input.mousePosition;
        for (int i = 0; i < cardRects.Count; i++)
        {
            if (cardRects[i] == null) continue;
            Vector2 cardScreen = RectTransformUtility.WorldToScreenPoint(cam, cardRects[i].position);
            if (DistanceToSegment(mouse, triggerScreen, cardScreen) <= lineHitTolerance)
                return true;
        }
        return false;
    }

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        if (ab.sqrMagnitude < 0.001f) return Vector2.Distance(p, a);
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + t * ab);
    }

#if UNITY_EDITOR
    private bool debugForcedOpen;

    public void DebugForceOpen()
    {
        debugForcedOpen = true;
        isVisible = true;
        hoverTimer = 0f;
        if (!isOpen) SetOpenState(true);
    }

    public void DebugForceClose()
    {
        debugForcedOpen = false;
        hoverTimer = 0f;
        if (isOpen) SetOpenState(false);
    }

    private const string PreviewPrefix = "BloomPreview_";
    [SerializeField] private int editorPreviewCardCount = 5;

    public void EditorPreviewBloom()
    {
        CanvasGroup rootCg = GetComponent<CanvasGroup>();
        if (rootCg != null) { rootCg.alpha = 1f; rootCg.interactable = true; rootCg.blocksRaycasts = true; }

        List<RectTransform> rects = CollectActiveChildRects(out List<CanvasGroup> groups);

        if (rects.Count == 0)
        {
            for (int i = 0; i < editorPreviewCardCount; i++)
            {
                var go = new GameObject($"{PreviewPrefix}{i}", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Preview Bloom");
                go.transform.SetParent(transform, false);
                var img = go.GetComponent<UnityEngine.UI.Image>();
                img.color = new Color(0.4f, 0.7f, 1f, 0.5f);
                var rt = go.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(80f, 110f);
                rects.Add(rt);
                groups.Add(null);
            }
        }

        int n = rects.Count;
        for (int i = 0; i < n; i++)
        {
            UnityEditor.Undo.RecordObject(rects[i], "Preview Bloom");
            float t = n > 1 ? (float)i / (n - 1) : 0.5f;
            float angleDeg = Mathf.Lerp(startAngleDeg, endAngleDeg, t);
            float rad = angleDeg * Mathf.Deg2Rad;
            rects[i].anchoredPosition = new Vector2(bloomRadius * Mathf.Cos(rad), bloomRadius * Mathf.Sin(rad));
            rects[i].localScale = Vector3.one;
            if (groups[i] != null) { UnityEditor.Undo.RecordObject(groups[i], "Preview Bloom"); groups[i].alpha = 1f; }
            UnityEditor.EditorUtility.SetDirty(rects[i].gameObject);
        }

        RefreshLinesGraphic();
        UnityEditor.EditorUtility.SetDirty(gameObject);
    }

    public void EditorResetBloom()
    {
        DestroyPreviewChildren();
        List<RectTransform> rects = CollectActiveChildRects(out List<CanvasGroup> groups);
        for (int i = 0; i < rects.Count; i++)
        {
            UnityEditor.Undo.RecordObject(rects[i], "Reset Bloom");
            rects[i].anchoredPosition = Vector2.zero;
            rects[i].localScale = Vector3.one;
            if (groups[i] != null) { UnityEditor.Undo.RecordObject(groups[i], "Reset Bloom"); groups[i].alpha = 0f; }
            UnityEditor.EditorUtility.SetDirty(rects[i].gameObject);
        }

        RefreshLinesGraphic();
        UnityEditor.EditorUtility.SetDirty(gameObject);
    }

    private void RefreshLinesGraphic()
    {
        CardBloomLinesGraphic graphic = GetComponentInChildren<CardBloomLinesGraphic>();
        if (graphic == null)
        {
            var linesGo = new GameObject("BloomLines", typeof(RectTransform));
            UnityEditor.Undo.RegisterCreatedObjectUndo(linesGo, "Preview Bloom");
            linesGo.transform.SetParent(transform, false);
            var linesRect = linesGo.GetComponent<RectTransform>();
            linesRect.anchorMin = Vector2.zero;
            linesRect.anchorMax = Vector2.one;
            linesRect.offsetMin = Vector2.zero;
            linesRect.offsetMax = Vector2.zero;
            graphic = linesGo.AddComponent<CardBloomLinesGraphic>();
            linesGo.transform.SetAsFirstSibling();
        }
        graphic.Init(this);
        graphic.SetAllDirty();
    }

    public List<RectTransform> GetEditorPreviewRects()
    {
        var rects = new List<RectTransform>();
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child.GetComponent<CardBloomLinesGraphic>() != null) continue;
            RectTransform rt = child.GetComponent<RectTransform>();
            if (rt != null) rects.Add(rt);
        }
        return rects;
    }

    private void DestroyPreviewChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name.StartsWith(PreviewPrefix))
                UnityEditor.Undo.DestroyObjectImmediate(child.gameObject);
        }
    }

    private List<RectTransform> CollectActiveChildRects(out List<CanvasGroup> groups)
    {
        var rects = new List<RectTransform>();
        groups = new List<CanvasGroup>();
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (!child.gameObject.activeSelf) continue;
            if (child.GetComponent<CardBloomLinesGraphic>() != null) continue;
            if (child.name.StartsWith(PreviewPrefix)) continue;
            RectTransform rt = child.GetComponent<RectTransform>();
            if (rt == null) continue;
            rects.Add(rt);
            groups.Add(child.GetComponent<CanvasGroup>());
        }
        return rects;
    }
#endif
}
