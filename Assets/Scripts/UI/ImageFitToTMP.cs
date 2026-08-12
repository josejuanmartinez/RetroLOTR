using TMPro;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[RequireComponent(typeof(Image))]
public class ImageFitToTMP : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private Vector2 padding = new(0.2f, 0.1f);
    [SerializeField] private bool growRight = true;

    // Image's initial anchoredPosition.x in parent space — the fixed left-edge anchor.
    [SerializeField] private bool anchorSet;
    [SerializeField] private float anchorInParent;

    private Image _image;
    private RectTransform _rt;

    private void Awake()
    {
        _image = GetComponent<Image>();
        _rt = _image.rectTransform;
    }

    private void OnEnable() => Fit();

#if UNITY_EDITOR
    private void Update() => Fit();

    [ContextMenu("Reset Anchor")]
    private void ResetAnchor() => anchorSet = false;
#endif

    public void Fit()
    {
        if (_image == null) _image = GetComponent<Image>();
        if (_rt == null) _rt = _image.rectTransform;
        if (label == null) return;

        if (string.IsNullOrWhiteSpace(label.text))
        {
            _image.enabled = false;
            return;
        }

        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;

        // preferredWidth is position-independent — avoids feedback loops from textBounds.
        float textWidth  = label.preferredWidth;
        float textHeight = label.preferredHeight;
        if (textWidth < 0.001f) { _image.enabled = false; return; }

        float newWidth  = textWidth  + padding.x;
        float newHeight = textHeight + padding.y;

        _image.type = Image.Type.Sliced;
        _rt.sizeDelta = new Vector2(newWidth, newHeight);
        _image.enabled = true;

        if (!growRight) return;

        // Anchor = image left edge in parent space, captured once.
        if (!anchorSet)
        {
            anchorInParent = _rt.anchoredPosition.x - newWidth * 0.5f;
            anchorSet = true;
        }

        // Image grows rightward; left edge stays fixed at anchorInParent.
        Vector2 pos = _rt.anchoredPosition;
        pos.x = anchorInParent + newWidth * 0.5f;
        _rt.anchoredPosition = pos;

        // Label pivot.x = 0 (left-center pivot), so label.anchoredPosition.x IS where text
        // starts in image-local space. Place it at image-left + pad/2.
        RectTransform labelRt = label.rectTransform;
        Vector2 labelPos = labelRt.anchoredPosition;
        labelPos.x = -newWidth * 0.5f + padding.x * 0.5f;
        labelRt.anchoredPosition = labelPos;
    }
}
