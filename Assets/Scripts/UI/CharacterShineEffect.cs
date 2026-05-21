using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Adds a diagonal light-sweep (glass reflection) effect over a UI Image.
/// Attach to any Image GameObject — self-configures on Awake.
/// </summary>
public class CharacterShineEffect : MonoBehaviour
{
    private RawImage shineImage;
    private RectTransform shineRect;
    private RectTransform parentRect;

    private const float SweepInterval = 5f;
    private const float SweepDuration = 0.7f;
    private const float PeakAlpha = 0.38f;
    private const float BeamWidth = 55f;
    private const float BeamAngle = 22f;

    private void Awake()
    {
        parentRect = GetComponent<RectTransform>();

        // RectMask2D container clips the beam to exactly the image bounds
        var maskGo = new GameObject("ShineMask");
        maskGo.transform.SetParent(transform, false);
        var maskRect = maskGo.AddComponent<RectTransform>();
        maskRect.anchorMin = Vector2.zero;
        maskRect.anchorMax = Vector2.one;
        maskRect.sizeDelta = Vector2.zero;
        maskRect.anchoredPosition = Vector2.zero;
        maskGo.AddComponent<RectMask2D>();

        // The beam itself — tall strip that stretches full height, clipped by mask
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
        // Extra height headroom so the rotated corners don't clip inside the mask
        shineRect.sizeDelta = new Vector2(BeamWidth, 60f);
        shineRect.localEulerAngles = new Vector3(0f, 0f, BeamAngle);

        StartCoroutine(SweepLoop());
    }

    private IEnumerator SweepLoop()
    {
        // Stagger so multiple icons don't all flash at the same moment
        yield return new WaitForSeconds(Random.Range(0f, SweepInterval));
        while (true)
        {
            yield return Sweep();
            yield return new WaitForSeconds(SweepInterval + Random.Range(-0.5f, 0.5f));
        }
    }

    private IEnumerator Sweep()
    {
        float halfTravel = (parentRect != null ? parentRect.rect.width : 280f) * 0.65f;
        // If rect hasn't been laid out yet, wait a frame
        if (halfTravel <= 0f)
        {
            yield return null;
            halfTravel = (parentRect != null ? parentRect.rect.width : 280f) * 0.65f;
        }

        float t = 0f;
        while (t < SweepDuration)
        {
            float p = t / SweepDuration;
            // Sine bell: fades in, peaks mid-sweep, fades out
            float alpha = Mathf.Sin(p * Mathf.PI) * PeakAlpha;
            float x = Mathf.Lerp(-halfTravel, halfTravel, p);

            shineImage.color = new Color(1f, 1f, 1f, alpha);
            shineRect.anchoredPosition = new Vector2(x, 0f);

            t += Time.deltaTime;
            yield return null;
        }
        shineImage.color = new Color(1f, 1f, 1f, 0f);
    }

    // Horizontal bell-curve gradient: transparent → white → transparent
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
                // SmoothStep on both halves gives a soft bell
                float a = Mathf.SmoothStep(0f, 1f, u) * Mathf.SmoothStep(0f, 1f, 1f - u);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return tex;
    }
}
