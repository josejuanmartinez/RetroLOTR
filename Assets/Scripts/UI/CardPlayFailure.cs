using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Decorative "card fizzled" flutter: the played card shakes in place, drains toward red,
// then dissolves to nothing. Launched from Card.TryPlayCard when the card was spent but its
// action's difficulty roll failed — the effect never landed, so unlike CardPlayFlight this
// never travels to a hex. Lives on the root canvas, like CardPlayFlight, so it survives the
// hand refresh that follows a play.
public class CardPlayFailure : MonoBehaviour
{
    private const float ShakeDuration = 0.45f;
    private const float ShakeAmplitude = 14f;
    private const float ShakeFrequency = 28f;
    private const float FadeDuration = 0.5f;

    private static readonly Color FailColor = new Color(0.85f, 0.15f, 0.1f);

    public static void Launch(Card card)
    {
        if (card == null) return;

        Canvas parentCanvas = card.GetComponentInParent<Canvas>();
        Canvas rootCanvas = parentCanvas != null ? parentCanvas.rootCanvas : null;
        if (rootCanvas == null) return;

        // Same source-point logic as CardPlayFlight: prefer the enlarged center preview
        // (what the player was actually looking at when they clicked), else the hand card.
        CardCenterPreview preview = CardCenterPreview.Instance;
        RectTransform sourceRect = preview != null && preview.CurrentPreviewRect != null
            ? preview.CurrentPreviewRect
            : card.transform as RectTransform;
        if (sourceRect == null) return;

        GameObject flutterGo = new("CardPlayFailure", typeof(RectTransform));
        RectTransform flutterRect = flutterGo.GetComponent<RectTransform>();
        flutterRect.SetParent(rootCanvas.transform, false);
        flutterRect.anchorMin = flutterRect.anchorMax = new Vector2(0.5f, 0.5f);
        flutterRect.pivot = new Vector2(0.5f, 0.5f);
        flutterRect.SetAsLastSibling();

        GameObject clone = card.CreateRealCardVisualClone(flutterRect, out Vector2 cardSize);
        if (clone == null)
        {
            Destroy(flutterGo);
            return;
        }
        flutterRect.sizeDelta = cardSize;
        if (clone.transform is RectTransform cloneRect)
        {
            cloneRect.anchorMin = cloneRect.anchorMax = new Vector2(0.5f, 0.5f);
            cloneRect.pivot = new Vector2(0.5f, 0.5f);
            cloneRect.sizeDelta = cardSize;
            cloneRect.anchoredPosition = Vector2.zero;
            cloneRect.localScale = Vector3.one;
        }

        Camera uiCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
        RectTransform canvasRect = rootCanvas.transform as RectTransform;
        Vector2 sourceScreen = RectTransformUtility.WorldToScreenPoint(uiCamera, sourceRect.TransformPoint(sourceRect.rect.center));
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, sourceScreen, uiCamera, out Vector2 sourceLocal);
        flutterRect.localPosition = sourceLocal;

        CardPlayFailure flutter = flutterGo.AddComponent<CardPlayFailure>();
        flutter.StartCoroutine(flutter.Run(flutterRect, clone));
    }

    private IEnumerator Run(RectTransform rect, GameObject clone)
    {
        CanvasGroup group = clone.GetComponent<CanvasGroup>();
        Graphic[] graphics = clone.GetComponentsInChildren<Graphic>(true);
        Color[] baseColors = new Color[graphics.Length];
        for (int i = 0; i < graphics.Length; i++)
            baseColors[i] = graphics[i] != null ? graphics[i].color : Color.white;

        Vector2 basePos = rect.localPosition;

        // Phase 1 — shake while draining toward red; the shake settles as it goes.
        float t = 0f;
        while (t < ShakeDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / ShakeDuration);
            float damp = 1f - p;
            float offsetX = Mathf.Sin(t * ShakeFrequency) * ShakeAmplitude * damp;
            rect.localPosition = basePos + new Vector2(offsetX, 0f);

            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] == null) continue;
                Color target = new Color(FailColor.r, FailColor.g, FailColor.b, baseColors[i].a);
                graphics[i].color = Color.Lerp(baseColors[i], target, p);
            }

            yield return null;
        }
        rect.localPosition = basePos;

        // Phase 2 — the fully-reddened card dissolves to nothing.
        t = 0f;
        float startAlpha = group != null ? group.alpha : 1f;
        while (t < FadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / FadeDuration);
            if (group != null) group.alpha = Mathf.Lerp(startAlpha, 0f, p);
            yield return null;
        }

        Destroy(gameObject);
    }
}
