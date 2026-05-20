using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MinimapManager : MonoBehaviour
{
    private static MinimapManager instance;

    public Color overlayColor;
    public TMP_FontAsset escFontAsset;
    public Camera minimapCamera;
    public Sprite mapBackgroundSprite;
    [Tooltip("Visual scale of the minimap display in the overlay. 0.5 = half screen, 1.0 = full screen.")]
    public float overlayMapScale = 0.5f;
    [Tooltip("Zoom level of the minimap camera. 1.0 = default, < 1 = zoom in (bigger), > 1 = zoom out (smaller).")]
    public float minimapCameraZoom = 1f;
    [Tooltip("Render texture resolution as a multiple of screen resolution. 1 = full res, 2 = 2× supersampled.")]
    [Range(0.25f, 4f)]
    public float renderTextureScale = 2f;
    [Tooltip("Layer name for region labels. Labels on this layer are added to the minimap camera only when the overlay is open.")]
    public string regionLabelsLayerName = "RegionLabels";

    private bool refreshing = false;
    private bool isExpanded = false;

    private GameObject minimapOverlay;
    private GameObject legendContainer;
    private int savedRtWidth;
    private int savedRtHeight;
    private float savedCameraSize;
    private int savedCullingMask;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        // Ensure region labels are excluded from the small always-visible minimap thumbnail.
        // AddLabelsLayerToCamera / RestoreLabelsLayerOnCamera handle adding them only for the overlay.
        if (minimapCamera != null)
        {
            int layer = LayerMask.NameToLayer(regionLabelsLayerName);
            if (layer >= 0)
                minimapCamera.cullingMask &= ~(1 << layer);
        }
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

        if (minimapOverlay != null)
            Destroy(minimapOverlay);

        CreateOverlay();

        minimapOverlay.SetActive(true);
        minimapOverlay.transform.SetAsLastSibling();

        RefreshLegend();

        ApplyCameraZoom();
        AddLabelsLayerToCamera();

        RenderTexture rtBeforeOpen = minimapCamera != null ? minimapCamera.targetTexture : null;
        if (rtBeforeOpen != null)
        {
            savedRtWidth = rtBeforeOpen.width;
            savedRtHeight = rtBeforeOpen.height;
        }

        ResizeRenderTexture(
            Mathf.RoundToInt(Screen.width * renderTextureScale),
            Mathf.RoundToInt(Screen.height * renderTextureScale));
    }

    private void Close()
    {
        isExpanded = false;

        if (minimapOverlay != null)
            minimapOverlay.SetActive(false);

        RestoreCameraZoom();
        RestoreLabelsLayerOnCamera();

        if (savedRtWidth > 0)
            ResizeRenderTexture(savedRtWidth, savedRtHeight);
        else
            RefreshMinimap();
    }

    private void AddLabelsLayerToCamera()
    {
        if (minimapCamera == null) return;
        int layer = LayerMask.NameToLayer(regionLabelsLayerName);
        if (layer < 0) return;
        savedCullingMask = minimapCamera.cullingMask;
        minimapCamera.cullingMask |= 1 << layer;
    }

    private void RestoreLabelsLayerOnCamera()
    {
        if (minimapCamera == null) return;
        minimapCamera.cullingMask = savedCullingMask;
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
        bgImage.color = overlayColor;
        bgImage.raycastTarget = true;

        float scale = Mathf.Clamp(overlayMapScale, 0.1f, 2f);
        float minAnchor = (1f - scale) * 0.5f;
        float maxAnchor = 1f - minAnchor;

        // Optional map background — shown behind the minimap display.
        if (mapBackgroundSprite != null)
        {
            GameObject mapBg = new GameObject("MapBackground");
            mapBg.transform.SetParent(minimapOverlay.transform, false);

            RectTransform mapBgRt = mapBg.AddComponent<RectTransform>();
            mapBgRt.anchorMin = new Vector2(minAnchor, minAnchor);
            mapBgRt.anchorMax = new Vector2(maxAnchor, maxAnchor);
            mapBgRt.offsetMin = Vector2.zero;
            mapBgRt.offsetMax = Vector2.zero;

            Image mapBgImage = mapBg.AddComponent<Image>();
            mapBgImage.sprite = mapBackgroundSprite;
            mapBgImage.color = Color.white;
            mapBgImage.raycastTarget = false;
        }

        // Minimap display — centered, size driven by overlayMapScale.
        GameObject mapDisplay = new GameObject("MinimapDisplay");
        mapDisplay.transform.SetParent(minimapOverlay.transform, false);

        RectTransform mapRt = mapDisplay.AddComponent<RectTransform>();
        mapRt.anchorMin = new Vector2(minAnchor, minAnchor);
        mapRt.anchorMax = new Vector2(maxAnchor, maxAnchor);
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
        hintText.font = escFontAsset;
        hintText.text = "ESC to close";
        hintText.fontSize = 18f;
        hintText.color = new Color(1f, 1f, 1f, 0.7f);
        hintText.raycastTarget = false;
    }

    private void RefreshLegend()
    {
        if (legendContainer != null)
            Destroy(legendContainer);

        Board board = FindFirstObjectByType<Board>();
        if (board == null || board.hexes == null) return;

        HashSet<string> discoveredRegions = new();
        foreach (var hex in board.hexes.Values)
        {
            if (hex != null && hex.IsHexRevealed())
            {
                string region = hex.GetLandRegion();
                if (!string.IsNullOrWhiteSpace(region))
                    discoveredRegions.Add(region);
            }
        }

        if (discoveredRegions.Count == 0) return;

        legendContainer = new GameObject("LegendContainer");
        legendContainer.transform.SetParent(minimapOverlay.transform, false);

        float itemHeight = 12f;
        int count = discoveredRegions.Count;
        float totalHeight = count * itemHeight;

        RectTransform containerRt = legendContainer.AddComponent<RectTransform>();
        containerRt.anchorMin = new Vector2(0.85f, 0.5f);
        containerRt.anchorMax = new Vector2(0.98f, 0.5f);
        containerRt.pivot = new Vector2(1f, 0.5f);
        containerRt.offsetMin = Vector2.zero;
        containerRt.offsetMax = Vector2.zero;
        containerRt.sizeDelta = new Vector2(0f, totalHeight);

        int index = 0;
        foreach (string region in discoveredRegions.OrderBy(r => r))
        {
            GameObject item = new GameObject("LegendItem_" + region);
            item.transform.SetParent(legendContainer.transform, false);

            RectTransform itemRt = item.AddComponent<RectTransform>();
            itemRt.anchorMin = new Vector2(0f, 0.5f);
            itemRt.anchorMax = new Vector2(1f, 0.5f);
            itemRt.pivot = new Vector2(0.5f, 0.5f);
            float yPos = ((count - 1) * itemHeight * 0.5f) - (index * itemHeight);
            itemRt.anchoredPosition = new Vector2(0f, yPos);
            itemRt.sizeDelta = new Vector2(0f, itemHeight);

            TextMeshProUGUI itemText = item.AddComponent<TextMeshProUGUI>();
            itemText.font = escFontAsset;
            itemText.text = region;
            itemText.fontSize = 7f;
            itemText.color = RegionColors.GetColor(region, alpha: 1f);
            itemText.raycastTarget = false;
            itemText.alignment = TextAlignmentOptions.Right;

            index++;
        }
    }

    private void ApplyCameraZoom()
    {
        if (minimapCamera == null) return;
        float zoom = Mathf.Clamp(minimapCameraZoom, 0.1f, 10f);
        if (minimapCamera.orthographic)
        {
            savedCameraSize = minimapCamera.orthographicSize;
            minimapCamera.orthographicSize = savedCameraSize * zoom;
        }
        else
        {
            savedCameraSize = minimapCamera.fieldOfView;
            minimapCamera.fieldOfView = savedCameraSize * zoom;
        }
    }

    private void RestoreCameraZoom()
    {
        if (minimapCamera == null) return;
        if (minimapCamera.orthographic)
            minimapCamera.orthographicSize = savedCameraSize;
        else
            minimapCamera.fieldOfView = savedCameraSize;
    }

    private void ResizeRenderTexture(int width, int height)
    {
        RenderTexture rt = minimapCamera.targetTexture;
        if (rt == null) return;

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
