using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[RequireComponent(typeof(CanvasRenderer))]
public class CardBloomLinesGraphic : Graphic
{
    [SerializeField] private float lineWidth = 2f;

    private CardBloomWheel wheel;

    protected override void OnEnable()
    {
        base.OnEnable();
        if (wheel == null)
            wheel = GetComponentInParent<CardBloomWheel>();
        raycastTarget = false;
    }

    public void Init(CardBloomWheel bloomWheel)
    {
        wheel = bloomWheel;
        raycastTarget = false;
        color = Color.white;
    }

    private void Update()
    {
        if (!Application.isPlaying) return;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (wheel == null) return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            DrawEditorLines(vh);
            return;
        }
#endif

        float alpha = wheel.LinesAlpha;
        if (alpha <= 0.005f) return;

        var cards = wheel.CardRects;
        if (cards == null || cards.Count == 0) return;

        var cardColors = wheel.CardLineColors;

        RectTransform trigger = wheel.HoverTriggerRect;
        Vector2 startLocal = trigger != null
            ? (Vector2)rectTransform.InverseTransformPoint(trigger.TransformPoint(trigger.rect.center))
            : Vector2.zero;

        Vector2 off = wheel.LineEndOffset;

        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] == null || !cards[i].gameObject.activeSelf) continue;
            Color baseColor = (cardColors != null && i < cardColors.Count) ? cardColors[i] : Color.white;
            Color lineColor = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
            Vector2 cardLocal = rectTransform.InverseTransformPoint(
                cards[i].TransformPoint(new Vector3(off.x, off.y, 0f)));
            AddLine(vh, startLocal, cardLocal, lineWidth, lineColor);
        }
    }

#if UNITY_EDITOR
    private void DrawEditorLines(VertexHelper vh)
    {
        var previewRects = wheel.GetEditorPreviewRects();
        if (previewRects == null || previewRects.Count == 0) return;

        RectTransform trigger = wheel.HoverTriggerRect;
        Vector2 startLocal = trigger != null
            ? (Vector2)rectTransform.InverseTransformPoint(trigger.TransformPoint(trigger.rect.center))
            : Vector2.zero;

        Color lineColor = new Color(1f, 1f, 1f, 0.7f);
        Vector2 off = wheel.LineEndOffset;

        foreach (var rt in previewRects)
        {
            if (rt == null) continue;
            Vector2 cardLocal = rectTransform.InverseTransformPoint(
                rt.TransformPoint(new Vector3(off.x, off.y, 0f)));
            AddLine(vh, startLocal, cardLocal, lineWidth, lineColor);
        }
    }
#endif

    private void AddLine(VertexHelper vh, Vector2 from, Vector2 to, float width, Color col)
    {
        Vector2 dir = to - from;
        if (dir.sqrMagnitude < 0.001f) return;
        dir.Normalize();
        Vector2 perp = new Vector2(-dir.y, dir.x) * (width * 0.5f);

        int idx = vh.currentVertCount;
        UIVertex v = new UIVertex();
        v.color = col;
        v.uv0 = Vector2.zero;

        v.position = from + perp; vh.AddVert(v);
        v.position = from - perp; vh.AddVert(v);
        v.position = to - perp;   vh.AddVert(v);
        v.position = to + perp;   vh.AddVert(v);

        vh.AddTriangle(idx, idx + 1, idx + 2);
        vh.AddTriangle(idx, idx + 2, idx + 3);
    }
}
