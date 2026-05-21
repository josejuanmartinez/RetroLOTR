using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class CardShineEffect : MonoBehaviour
{
    private RawImage shineImage;
    private RectTransform shineRect;
    private RectTransform parentRect;

    private const float SweepInterval = 7f;
    private const float SweepDuration = 0.5f;
    private const float PeakAlpha = 0.20f;
    private const float BeamWidth = 30f;
    private const float BeamAngle = 22f;

    private void Awake()
    {
        parentRect = GetComponent<RectTransform>();

        var maskGo = new GameObject("ShineMask");
        maskGo.transform.SetParent(transform, false);
        var maskRect = maskGo.AddComponent<RectTransform>();
        maskRect.anchorMin = Vector2.zero;
        maskRect.anchorMax = Vector2.one;
        maskRect.sizeDelta = Vector2.zero;
        maskRect.anchoredPosition = Vector2.zero;
        maskGo.AddComponent<RectMask2D>();

        var shineGo = new GameObject("Beam");
        shineGo.transform.SetParent(maskGo.transform, false);
        shineImage = shineGo.AddComponent<RawImage>();
        shineImage.texture = BuildBeamTexture();
        shineImage.color = new Color(1f, 1f, 1f, 0f);
        shineImage.raycastTarget = false;

        shineRect = shineGo.GetComponent<RectTransform>();
        shineRect.anchorMin = new Vector2(0.5f, 0f);
        shineRect.anchorMax = new Vector2(0.5f, 1f);
        shineRect.pivot = new Vector2(0.5f, 0.5f);
        shineRect.sizeDelta = new Vector2(BeamWidth, 60f);
        shineRect.localEulerAngles = new Vector3(0f, 0f, BeamAngle);

        StartCoroutine(SweepLoop());
    }

    private IEnumerator SweepLoop()
    {
        yield return new WaitForSeconds(Random.Range(0f, SweepInterval));
        while (true)
        {
            yield return Sweep();
            yield return new WaitForSeconds(SweepInterval + Random.Range(-1f, 1f));
        }
    }

    private IEnumerator Sweep()
    {
        float halfTravel = (parentRect != null ? parentRect.rect.width : 120f) * 0.65f;
        if (halfTravel <= 0f)
        {
            yield return null;
            halfTravel = (parentRect != null ? parentRect.rect.width : 120f) * 0.65f;
        }

        float t = 0f;
        while (t < SweepDuration)
        {
            float p = t / SweepDuration;
            float alpha = Mathf.Sin(p * Mathf.PI) * PeakAlpha;
            float x = Mathf.Lerp(-halfTravel, halfTravel, p);

            shineImage.color = new Color(1f, 1f, 1f, alpha);
            shineRect.anchoredPosition = new Vector2(x, 0f);

            t += Time.deltaTime;
            yield return null;
        }
        shineImage.color = new Color(1f, 1f, 1f, 0f);
    }

    private static Texture2D BuildBeamTexture()
    {
        const int w = 32, h = 4;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)(w - 1);
                float a = Mathf.SmoothStep(0f, 1f, u) * Mathf.SmoothStep(0f, 1f, 1f - u);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return tex;
    }
}
