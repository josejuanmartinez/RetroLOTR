using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;

public class MinimapManager : MonoBehaviour
{
    private static MinimapManager instance;

    public Color overlayColor;
    public TMP_FontAsset escFontAsset;
    public Camera minimapCamera;
    public Sprite mapBackgroundSprite;
    public MinimapOverlayView overlayPrefab;
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
    private MinimapOverlayView overlayView;
    private GameObject legendContainer;
    private int savedRtWidth;
    private int savedRtHeight;
    private float savedCameraSize;
    private int savedCullingMask;

    // Board the camera was last fitted to (and its hex count, so a regenerated board of the
    // same instance refits). Board sizes vary per scenario/settings, so the scene-authored
    // camera transform can't be trusted to frame the map.
    private Board fittedBoard;
    private int fittedHexCount;

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
        if (refreshing) StartCoroutine(UpdateCoroutine());

        if (isExpanded && (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(0)))
            Close();
    }

    private IEnumerator UpdateCoroutine()
    {
        yield return new WaitForEndOfFrame();
        FitCameraToBoard();
        minimapCamera.enabled = true;
        yield return new WaitForEndOfFrame();
        minimapCamera.enabled = false;
        refreshing = false;
    }

    // Centers the minimap camera on the generated board and sizes it to frame every hex.
    private void FitCameraToBoard()
    {
        if (minimapCamera == null) return;
        Board board = FindFirstObjectByType<Board>();
        if (board == null || board.hexes == null || board.hexes.Count == 0) return;
        if (board == fittedBoard && board.hexes.Count == fittedHexCount) return;

        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (Hex hex in board.hexes.Values)
        {
            if (hex == null) continue;
            Vector3 p = hex.transform.position;
            if (p.x < minX) minX = p.x;
            if (p.x > maxX) maxX = p.x;
            if (p.y < minY) minY = p.y;
            if (p.y > maxY) maxY = p.y;
        }
        if (minX > maxX) return;

        Vector3 pos = minimapCamera.transform.position;
        minimapCamera.transform.position = new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, pos.z);
        if (minimapCamera.orthographic)
        {
            // Half a hex of margin on each side so border tiles aren't clipped.
            float halfHeight = (maxY - minY) * 0.5f + 1f;
            float halfWidthAsHeight = ((maxX - minX) * 0.5f + 1f) / Mathf.Max(0.01f, minimapCamera.aspect);
            minimapCamera.orthographicSize = Mathf.Max(halfHeight, halfWidthAsHeight);
        }

        fittedBoard = board;
        fittedHexCount = board.hexes.Count;
    }

    public static void RefreshMinimap()
    {
        if (instance != null) instance.refreshing = true;
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

        RefreshLegend();

        FitCameraToBoard();
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
        if (overlayPrefab == null) return;

        //overlayView = Instantiate(overlayPrefab);
        overlayView = overlayPrefab;
        minimapOverlay = overlayView.gameObject;

        overlayView.background.color = overlayColor;

        float scale = Mathf.Clamp(overlayMapScale, 0.1f, 2f);
        float minAnchor = (1f - scale) * 0.5f;
        float maxAnchor = 1f - minAnchor;

        // Optional map background — shown behind the minimap display.
        overlayView.mapBackground.rectTransform.anchorMin = new Vector2(minAnchor, minAnchor);
        overlayView.mapBackground.rectTransform.anchorMax = new Vector2(maxAnchor, maxAnchor);
        overlayView.mapBackground.sprite = mapBackgroundSprite;
        overlayView.mapBackground.gameObject.SetActive(mapBackgroundSprite != null);

        // Minimap display — centered, size driven by overlayMapScale.
        overlayView.mapDisplay.rectTransform.anchorMin = new Vector2(minAnchor, minAnchor);
        overlayView.mapDisplay.rectTransform.anchorMax = new Vector2(maxAnchor, maxAnchor);
        overlayView.mapDisplay.texture = minimapCamera.targetTexture;

        overlayView.hintText.font = escFontAsset;
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
}
