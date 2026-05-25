using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class CardBloomLinesGraphic : Graphic
{
    [SerializeField] private float lineWidth = 2f;

    private CardBloomWheel wheel;

    public void Init(CardBloomWheel bloomWheel)
    {
        wheel = bloomWheel;
        raycastTarget = false;
        color = Color.white;
    }

    private void Update()
    {
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (wheel == null) return;

        float alpha = wheel.LinesAlpha;
        if (alpha <= 0.005f) return;

        var cards = wheel.CardRects;
        if (cards == null || cards.Count == 0) return;

        // Resolve start point: center of hoverTriggerRect in this Graphic's local space.
        RectTransform trigger = wheel.HoverTriggerRect;
        Vector2 startLocal = trigger != null
            ? (Vector2)rectTransform.InverseTransformPoint(trigger.TransformPoint(trigger.rect.center))
            : Vector2.zero;

        Color lineColor = new Color(1f, 1f, 1f, alpha);

        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] == null || !cards[i].gameObject.activeSelf) continue;
            Vector2 cardLocal = rectTransform.InverseTransformPoint(
                cards[i].TransformPoint(Vector3.zero));
            AddLine(vh, startLocal, cardLocal, lineWidth, lineColor);
        }
    }

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
