using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class CardEnvironmentalPulseEffect : MonoBehaviour
{
    private Image pulseImage;
    private Coroutine pulseCoroutine;

    private static readonly Color GlowColor = new Color(0.4f, 0.85f, 1f, 1f);
    private const float PulseSpeed = 1.1f;
    private const float AlphaMin = 0.12f;
    private const float AlphaMax = 0.60f;
    private const float BorderExpand = 10f;

    private void Awake()
    {
        BuildBorderImage();
    }

    private void OnEnable()
    {
        if (pulseCoroutine == null)
            pulseCoroutine = StartCoroutine(PulseLoop());
    }

    private void OnDisable()
    {
        if (pulseCoroutine != null) { StopCoroutine(pulseCoroutine); pulseCoroutine = null; }
        if (pulseImage != null)
            pulseImage.color = new Color(GlowColor.r, GlowColor.g, GlowColor.b, 0f);
    }

    private void OnDestroy()
    {
        if (pulseImage != null) Destroy(pulseImage.gameObject);
    }

    private void BuildBorderImage()
    {
        var go = new GameObject("EnvPulseBorder", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        go.transform.SetAsFirstSibling();

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = new Vector2(BorderExpand, BorderExpand);
        rt.anchoredPosition = Vector2.zero;

        pulseImage = go.AddComponent<Image>();
        pulseImage.color = new Color(GlowColor.r, GlowColor.g, GlowColor.b, 0f);
        pulseImage.raycastTarget = false;
    }

    private IEnumerator PulseLoop()
    {
        while (true)
        {
            float alpha = Mathf.Lerp(AlphaMin, AlphaMax, (Mathf.Sin(Time.time * PulseSpeed) + 1f) * 0.5f);
            if (pulseImage != null)
                pulseImage.color = new Color(GlowColor.r, GlowColor.g, GlowColor.b, alpha);
            yield return null;
        }
    }
}
