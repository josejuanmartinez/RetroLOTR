using System;
using System.Collections;
using UnityEngine;

// Decorative "card played" flight: the card compacts into its round token form, then
// spirals down to the hex its effect lands on, shrinking and fading as it arrives.
// Launched from Card.TryPlayCard right before the played card is destroyed; the flight
// object lives on the root canvas so it survives the hand refresh that follows a play.
public class CardPlayFlight : MonoBehaviour
{
    private const float CompactDuration = 0.20f;
    private const float FlightDuration = 0.95f;
    private const float ArrivalFadeDuration = 0.18f;
    private const float SpiralTurns = 2f;
    private const float ArrivalScale = 0.16f;

    private RectTransform canvasRect;
    private Camera uiCamera;
    private RectTransform rect;
    private CanvasGroup group;
    private Hex targetHex;
    private Vector2 startLocal;
    private float compactStartScale;
    private Action onArrived;
    private float durationScale = 1f;

    // sourceWorldCenter/sourceWorldWidth let a caller snapshot the enlarged center-preview
    // card's on-screen footprint at click time and hand it in later, once the card has
    // actually finished resolving (after awaited action/consume calls). Querying
    // CardCenterPreview.Instance live at that later point doesn't work: consuming the card
    // from hand rebuilds the bloom (CardBloomWheel.SetCards -> ClearCenterPreview) well
    // before this runs, destroying the preview clone the flight is supposed to fly out of —
    // silently downgrading the "spin out of the zoomed card" flight into one from the tiny
    // token instead. See Card.TryPlayCard/PlayEnvironmentalFromBloom for the snapshot.
    public static void Launch(Card card, Hex targetHex, Action onArrived = null, float durationScale = 1f, Vector3? sourceWorldCenter = null, float? sourceWorldWidth = null)
    {
        if (card == null) { Debug.LogWarning("[CardPlayFlight] Launch aborted: card is null."); onArrived?.Invoke(); return; }

        Canvas parentCanvas = card.GetComponentInParent<Canvas>();
        Canvas rootCanvas = parentCanvas != null ? parentCanvas.rootCanvas : null;
        if (rootCanvas == null) { Debug.LogWarning($"[CardPlayFlight] Launch aborted: '{card.name}' has no parent Canvas."); onArrived?.Invoke(); return; }

        Vector3 sourceCenterWorld;
        float sourceWidthWorld;
        if (sourceWorldCenter.HasValue && sourceWorldWidth.HasValue)
        {
            sourceCenterWorld = sourceWorldCenter.Value;
            sourceWidthWorld = sourceWorldWidth.Value;
        }
        else
        {
            // Fallback: the enlarged center preview (when one is still showing) is what the
            // player is actually looking at as they click — start the flight from it, else
            // from the hand card itself.
            CardCenterPreview preview = CardCenterPreview.Instance;
            RectTransform sourceRect = preview != null && preview.CurrentPreviewRect != null
                ? preview.CurrentPreviewRect
                : card.transform as RectTransform;
            Debug.Log($"[CardPlayFlight] '{card.name}': no source snapshot passed in — falling back to {(preview != null && preview.CurrentPreviewRect != null ? "live CardCenterPreview" : "card.transform")}.");
            if (sourceRect == null) { Debug.LogWarning($"[CardPlayFlight] Launch aborted: '{card.name}' has no fallback sourceRect (card.transform is not a RectTransform)."); onArrived?.Invoke(); return; }
            sourceCenterWorld = sourceRect.TransformPoint(sourceRect.rect.center);
            sourceWidthWorld = sourceRect.rect.width * Mathf.Abs(sourceRect.lossyScale.x);
        }

        GameObject flightGo = new("CardPlayFlight", typeof(RectTransform));
        RectTransform flightRect = flightGo.GetComponent<RectTransform>();
        flightRect.SetParent(rootCanvas.transform, false);
        flightRect.anchorMin = flightRect.anchorMax = new Vector2(0.5f, 0.5f);
        flightRect.pivot = new Vector2(0.5f, 0.5f);
        flightRect.SetAsLastSibling();

        GameObject token = card.CreateTokenVisualClone(flightRect, out Vector2 tokenSize);
        if (token == null)
        {
            Debug.LogWarning($"[CardPlayFlight] Launch aborted: '{card.name}'.CreateTokenVisualClone returned null (no tokenCanvasGroup on this Card instance).");
            Destroy(flightGo);
            onArrived?.Invoke();
            return;
        }
        Debug.Log($"[CardPlayFlight] '{card.name}': flight launched successfully, targetHex={(targetHex != null ? targetHex.name : "none")}.");
        flightRect.sizeDelta = tokenSize;
        RectTransform tokenRect = token.transform as RectTransform;
        if (tokenRect != null)
        {
            tokenRect.anchorMin = tokenRect.anchorMax = new Vector2(0.5f, 0.5f);
            tokenRect.pivot = new Vector2(0.5f, 0.5f);
            tokenRect.sizeDelta = tokenSize;
            tokenRect.anchoredPosition = Vector2.zero;
            tokenRect.localScale = Vector3.one;
        }

        CardPlayFlight flight = flightGo.AddComponent<CardPlayFlight>();
        flight.rect = flightRect;
        flight.canvasRect = rootCanvas.transform as RectTransform;
        flight.uiCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
        flight.targetHex = targetHex;
        flight.onArrived = onArrived;
        flight.durationScale = Mathf.Max(0.05f, durationScale);
        flight.group = flightGo.AddComponent<CanvasGroup>();
        flight.group.blocksRaycasts = false;
        flight.group.interactable = false;

        // Start at the source card's on-screen center, scaled up so the token covers
        // the card's footprint — the compact phase then reads as the card collapsing
        // into its token.
        Camera sourceCamera = flight.uiCamera;
        Vector2 sourceScreen = RectTransformUtility.WorldToScreenPoint(sourceCamera, sourceCenterWorld);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(flight.canvasRect, sourceScreen, flight.uiCamera, out Vector2 sourceLocal);
        flight.startLocal = sourceLocal;
        flightRect.localPosition = sourceLocal;

        float tokenWidth = tokenSize.x * Mathf.Abs(flight.canvasRect.lossyScale.x);
        flight.compactStartScale = tokenWidth > 1f ? Mathf.Clamp(sourceWidthWorld / tokenWidth, 1f, 4f) : 2.5f;
        flightRect.localScale = Vector3.one * flight.compactStartScale;

        flight.StartCoroutine(flight.Run());
    }

    // CardData-driven variant for auto-played effects (PC entry/turn-start resource grants)
    // that have no live hand Card instance to fly from — spins up a throwaway TokenCard just
    // to borrow its token visual, exactly like CampaignSelectionManager's button tokens.
    // onArrived (optional) fires once the token reaches the hex, before it sinks/fades —
    // callers use it to hold off granting resources until the token visually lands.
    public static void LaunchFromData(CardData data, Hex targetHex, Action onArrived = null, float durationScale = 1f)
    {
        if (data == null || targetHex == null) { onArrived?.Invoke(); return; }

        GameObject template = DeckManager.Instance?.GetTokenCardPrefabTemplate();
        if (template == null) { onArrived?.Invoke(); return; }

        Transform parent = CardCenterPreview.Instance != null ? CardCenterPreview.Instance.transform : null;
        GameObject temp = Instantiate(template, parent);
        temp.SetActive(true);
        Card tempCard = temp.GetComponent<Card>();
        if (tempCard == null) { Destroy(temp); onArrived?.Invoke(); return; }

        tempCard.Initialize(data);
        Launch(tempCard, targetHex, onArrived, durationScale);
        Destroy(temp, 0.01f);
    }

    private IEnumerator Run()
    {
        // Phase 1 — compact: the card-sized token collapses to its natural size.
        float t = 0f;
        float compactDuration = CompactDuration * durationScale;
        float flightDuration = FlightDuration * durationScale;
        float arrivalFadeDuration = ArrivalFadeDuration * durationScale;
        while (t < compactDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / compactDuration);
            rect.localScale = Vector3.one * Mathf.Lerp(compactStartScale, 1f, p * p);
            yield return null;
        }
        rect.localScale = Vector3.one;

        // Phase 2 — spiral flight down to the target hex (skipped without a target,
        // e.g. cards with a global effect: those just compact and fade in place).
        if (targetHex != null && Camera.main != null)
        {
            Vector2 toTarget = CurrentTargetLocal() - startLocal;
            float spiralRadius = Mathf.Clamp(toTarget.magnitude * 0.22f, 50f, 200f);
            float basePhase = Mathf.Atan2(toTarget.y, toTarget.x) + Mathf.PI * 0.5f;

            t = 0f;
            while (t < flightDuration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / flightDuration);
                float move = p * p * (3f - 2f * p); // smoothstep: gentle launch, decisive arrival

                // Recomputed per frame — effects often pan the camera to the hex while
                // the token is in the air, and the flight must land on the hex anyway.
                Vector2 basePos = Vector2.Lerp(startLocal, CurrentTargetLocal(), move);

                // Sideways offset winding around the flight line; the sine envelope is
                // zero at both ends so the corkscrew starts at the card and dies out
                // exactly on the hex.
                float envelope = Mathf.Sin(p * Mathf.PI) * (1f - p * 0.35f);
                float angle = basePhase + SpiralTurns * 2f * Mathf.PI * p;
                Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (spiralRadius * envelope);

                rect.localPosition = basePos + offset;
                rect.localScale = Vector3.one * Mathf.Lerp(1f, ArrivalScale, p * p);
                yield return null;
            }
            rect.localPosition = CurrentTargetLocal();
        }

        // The token has visually landed — callers waiting to grant resources/effects
        // until the token "arrives" resume here, before the sink/fade plays out.
        onArrived?.Invoke();
        onArrived = null;

        // Phase 3 — arrival: quick fade while sinking into the hex.
        float fadeStartAlpha = group.alpha;
        Vector3 fadeStartScale = rect.localScale;
        t = 0f;
        while (t < arrivalFadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / arrivalFadeDuration);
            group.alpha = Mathf.Lerp(fadeStartAlpha, 0f, p);
            rect.localScale = fadeStartScale * Mathf.Lerp(1f, 0.4f, p);
            if (targetHex != null && Camera.main != null) rect.localPosition = CurrentTargetLocal();
            yield return null;
        }

        Destroy(gameObject);
    }

    private Vector2 CurrentTargetLocal()
    {
        Camera boardCamera = Camera.main;
        if (targetHex == null || boardCamera == null || canvasRect == null)
            return rect != null ? (Vector2)rect.localPosition : Vector2.zero;

        Vector2 screen = boardCamera.WorldToScreenPoint(targetHex.transform.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, uiCamera, out Vector2 local);
        return local;
    }
}
