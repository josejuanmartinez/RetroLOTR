using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MinimapManager : MonoBehaviour
{
    private static MinimapManager instance;

    public Camera minimapCamera;

    private bool refreshing = false;
    private bool isExpanded = false;

    private GameObject minimapOverlay;
    private int savedRtWidth;
    private int savedRtHeight;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (instance.refreshing) StartCoroutine(UpdateCoroutine());

        if (isExpanded && (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(0)))
            Close();
    }

    private IEnumerator UpdateCoroutine()
    {
        yield return new WaitForEndOfFrame();
        minimapCamera.enabled = true;
        yield return new WaitForEndOfFrame();
        minimapCamera.enabled = false;
        instance.refreshing = false;
    }

    public static void RefreshMinimap()
    {
        instance.refreshing = true;
    }

    public void ToggleMinimapOverview()
    {
        if (!isExpanded)
            Open();
        else
            Close();
    }

    private void Open()
    {
        isExpanded = true;

        if (minimapOverlay == null)
            CreateOverlay();

        minimapOverlay.SetActive(true);
        minimapOverlay.transform.SetAsLastSibling();

        ResizeRenderTexture(
            Mathf.RoundToInt(Screen.width * 0.5f),
            Mathf.RoundToInt(Screen.height * 0.5f));
    }

    private void Close()
    {
        isExpanded = false;

        if (minimapOverlay != null)
            minimapOverlay.SetActive(false);

        if (savedRtWidth > 0)
            ResizeRenderTexture(savedRtWidth, savedRtHeight);
    }

    private void CreateOverlay()
    {
        Canvas rootCanvas = FindRootCanvas();
        if (rootCanvas == null) return;

        // Full-screen black backdrop.
        minimapOverlay = new GameObject("MinimapOverlay");
        minimapOverlay.transform.SetParent(rootCanvas.transform, false);

        // Own Canvas + GraphicRaycaster so it blocks all clicks from reaching the game world.
        Canvas overlayCanvas = minimapOverlay.AddComponent<Canvas>();
        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingOrder = 999;
        minimapOverlay.AddComponent<GraphicRaycaster>();

        RectTransform bgRt = minimapOverlay.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;

        Image bgImage = minimapOverlay.AddComponent<Image>();
        bgImage.color = Color.black;
        bgImage.raycastTarget = true;

        // Minimap display — centered 50% of screen, clicks pass through.
        GameObject mapDisplay = new GameObject("MinimapDisplay");
        mapDisplay.transform.SetParent(minimapOverlay.transform, false);

        RectTransform mapRt = mapDisplay.AddComponent<RectTransform>();
        mapRt.anchorMin = new Vector2(0.25f, 0.25f);
        mapRt.anchorMax = new Vector2(0.75f, 0.75f);
        mapRt.offsetMin = Vector2.zero;
        mapRt.offsetMax = Vector2.zero;

        RawImage mapImage = mapDisplay.AddComponent<RawImage>();
        mapImage.texture = minimapCamera.targetTexture;
        mapImage.raycastTarget = false;

        // "ESC to close" hint — top-left corner.
        GameObject hint = new GameObject("CloseHint");
        hint.transform.SetParent(minimapOverlay.transform, false);

        RectTransform hintRt = hint.AddComponent<RectTransform>();
        hintRt.anchorMin = new Vector2(0f, 1f);
        hintRt.anchorMax = new Vector2(0f, 1f);
        hintRt.pivot = new Vector2(0f, 1f);
        hintRt.anchoredPosition = new Vector2(20f, -20f);
        hintRt.sizeDelta = new Vector2(300f, 40f);

        TextMeshProUGUI hintText = hint.AddComponent<TextMeshProUGUI>();
        hintText.text = "ESC to close";
        hintText.fontSize = 18f;
        hintText.color = new Color(1f, 1f, 1f, 0.7f);
        hintText.raycastTarget = false;
    }

    private void ResizeRenderTexture(int width, int height)
    {
        RenderTexture rt = minimapCamera.targetTexture;
        if (rt == null) return;

        if (!isExpanded)
        {
            savedRtWidth = rt.width;
            savedRtHeight = rt.height;
        }

        rt.Release();
        rt.width = width;
        rt.height = height;
        rt.Create();
        RefreshMinimap();
    }

    private Canvas FindRootCanvas()
    {
        Canvas[] all = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        Canvas best = null;
        foreach (Canvas c in all)
            if (c.isRootCanvas && (best == null || c.sortingOrder > best.sortingOrder))
                best = c;
        return best;
    }
}
