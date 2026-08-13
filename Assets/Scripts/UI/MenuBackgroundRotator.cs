using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Crossfades the intro-video stills used behind the title and pause menus.</summary>
public sealed class MenuBackgroundRotator : MonoBehaviour
{
    [SerializeField] private Image front;
    [SerializeField] private Image back;
    private Sprite[] sprites;
    private int index;
    private Coroutine routine;

    public void Initialise(Transform parent)
    {
        transform.SetParent(parent, false);
        RectTransform rect = GetComponent<RectTransform>();
        if (rect == null) rect = gameObject.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;

        if (back == null) back = CreateLayer("Backdrop A", transform, 0.9f);
        if (front == null) front = CreateLayer("Backdrop B", transform, 0f);
    }

    public void Configure(Image backLayer, Image frontLayer)
    {
        back = backLayer;
        front = frontLayer;
    }

    public void SetSkin(Skins skin)
    {
        string folder = skin == Skins.Bakshi ? "Bakshi" : "Default";
        sprites = Resources.LoadAll<Sprite>($"MenuBackgrounds/{folder}");
        index = 0;
        if (sprites == null || sprites.Length == 0) return;

        back.sprite = sprites[0];
        back.color = new Color(1f, 1f, 1f, 0.9f);
        front.color = new Color(1f, 1f, 1f, 0f);
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Rotate());
    }

    private IEnumerator Rotate()
    {
        while (sprites != null && sprites.Length > 1)
        {
            yield return new WaitForSecondsRealtime(6f);
            index = (index + 1) % sprites.Length;
            front.sprite = sprites[index];
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 1.15f)
            {
                front.color = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 0.9f, t));
                yield return null;
            }
            back.sprite = front.sprite;
            front.color = new Color(1f, 1f, 1f, 0f);
        }
    }

    private static Image CreateLayer(string name, Transform parent, float alpha)
    {
        GameObject layer = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        layer.transform.SetParent(parent, false);
        RectTransform rect = layer.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        Image image = layer.GetComponent<Image>();
        image.preserveAspect = false;
        image.color = new Color(1f, 1f, 1f, alpha);
        image.raycastTarget = false;
        return image;
    }
}
