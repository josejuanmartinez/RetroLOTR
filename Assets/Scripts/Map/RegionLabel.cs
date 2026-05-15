using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;

/// <summary>
/// Spawns a world-space TextMeshPro label for a group of hexes (a region).
/// Place this on an empty GameObject parented under the board, assign hexes,
/// and call CreateLabel() after generation.
/// </summary>
public class RegionLabel : MonoBehaviour
{
    [Tooltip("Display name shown on the label.")]
    public string regionName;

    [Tooltip("Hex transforms that belong to this region. Used to compute center.")]
    public List<Transform> hexes = new();

    [Header("Prefabs")]
    [Tooltip("World-space TextMeshPro prefab (3D Object -> Text - TextMeshPro).")]
    public TMP_Text labelPrefab;

    [Header("Visual")]
    [Tooltip("Offset from computed center. Z is especially important for 2D boards.")]
    public Vector3 labelOffset = new Vector3(0, 0, -1f);

    [Tooltip("Sorting layer name for the label. Must exist in Project Settings -> Tags and Layers.")]
    public string sortingLayerName = "UI";

    [Tooltip("Sorting order within the layer. Higher = on top.")]
    public int sortingOrder = 100;

    [Header("Center")]
    [Tooltip("If true, place label on the hex deepest inside the region (furthest from edges).")]
    public bool useDeepestHex = true;

    [Header("Orientation")]
    [Tooltip("If true, rotate label to match region shape: tall = vertical, wide = horizontal.")]
    public bool autoOrient = true;

    [Tooltip("If region aspect ratio exceeds this, text flips to the other orientation.")]
    public float aspectThreshold = 1.4f;

    [Header("Curve")]
    [Tooltip("If true, add CurvedText to the spawned label.")]
    public bool useCurvedText = false;

    [Tooltip("Arc radius for CurvedText. Positive = upward smile, negative = downward frown.")]
    public float curveRadius = 10f;

    [Tooltip("Total arc angle in degrees.")]
    public float arcAngle = 20f;

    [Header("Readability")]
    [Tooltip("If true, label always faces the camera.")]
    public bool billboard = false;

    [Tooltip("If true, scale font size so the text fits inside the region.")]
    public bool scaleFontToRegionSize = true;

    [Tooltip("If true, enable auto-sizing on the TMP component.")]
    public bool autoSize = true;

    [Tooltip("Min font size for auto-size.")]
    public float minFontSize = 1f;

    [Tooltip("Max font size for auto-size.")]
    public float maxFontSize = 5f;

    [Tooltip("Padding multiplier: text must fit inside region-size * (1 - this).")]
    [Range(0f, 0.5f)]
    public float regionPadding = 0.15f;

    [Header("Layer")]
    [Tooltip("Unity layer assigned to the spawned label. Cameras can include/exclude this layer independently.")]
    public string labelsLayerName = "RegionLabels";

    public TMP_Text SpawnedLabel => spawnedLabel;
    public Vector3 ComputedCenter { get; private set; }
    public float ComputedRotation { get; private set; }

    private TMP_Text spawnedLabel;

    public void CreateLabel()
    {
        if (hexes.Count == 0 || labelPrefab == null)
            return;

        // Remove old label if present
        if (spawnedLabel != null)
        {
            if (Application.isPlaying)
                Destroy(spawnedLabel.gameObject);
            else
                DestroyImmediate(spawnedLabel.gameObject);
        }

        ComputedCenter = useDeepestHex ? CalculateDeepestHex() : CalculateBoundsCenter();
        ComputedRotation = autoOrient ? ComputeOrientation() : 0f;

        spawnedLabel = Instantiate(labelPrefab, transform);
        spawnedLabel.name = $"Label_{regionName}";
        spawnedLabel.text = regionName;

        // Explicit world position
        spawnedLabel.transform.position = ComputedCenter + labelOffset;
        spawnedLabel.transform.rotation = Quaternion.Euler(0, 0, ComputedRotation);

        // Center alignment & pivot
        spawnedLabel.alignment = TextAlignmentOptions.Center;
        if (spawnedLabel.rectTransform != null)
            spawnedLabel.rectTransform.pivot = new Vector2(0.5f, 0.5f);

        // Strip accidental CurvedText from prefab and reset vertices it may have deformed
        if (!useCurvedText)
        {
            CurvedText existing = spawnedLabel.GetComponent<CurvedText>();
            if (existing != null)
            {
                DestroyImmediate(existing);
                spawnedLabel.ForceMeshUpdate(); // regenerate mesh to undo vertex deformation
            }
        }

        // Auto-size
        if (autoSize)
        {
            spawnedLabel.enableAutoSizing = true;
            spawnedLabel.fontSizeMin = minFontSize;
            spawnedLabel.fontSizeMax = maxFontSize;
        }

        // Scale font to fit region
        if (scaleFontToRegionSize)
        {
            FitTextToRegion();
        }

        // Layer — allows cameras to include/exclude region labels independently
        int labelLayer = LayerMask.NameToLayer(labelsLayerName);
        if (labelLayer >= 0)
            spawnedLabel.gameObject.layer = labelLayer;

        // Sorting
        MeshRenderer renderer = spawnedLabel.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sortingLayerName = sortingLayerName;
            renderer.sortingOrder = sortingOrder;
        }

        // Curve (only if explicitly enabled)
        if (useCurvedText)
        {
            CurvedText curved = spawnedLabel.gameObject.GetComponent<CurvedText>();
            if (curved == null)
                curved = spawnedLabel.gameObject.AddComponent<CurvedText>();

            curved.curveRadius = curveRadius;
            curved.arcAngle = arcAngle;
            curved.updateEveryFrame = false;
            curved.ForceUpdate();
        }

        // Billboard
        if (billboard)
        {
            Billboard bb = spawnedLabel.gameObject.GetComponent<Billboard>();
            if (bb == null)
                bb = spawnedLabel.gameObject.AddComponent<Billboard>();
        }
    }

    // Neighbor offsets match Board.cs evenRowNeighbors / oddRowNeighbors (v2.x = row).
    private static readonly Vector2Int[] EvenRowOffsets = {
        new(1, 0), new(0, 1), new(-1, 0), new(-1, -1), new(0, -1), new(1, -1)
    };
    private static readonly Vector2Int[] OddRowOffsets = {
        new(1, 1), new(0, 1), new(-1, 1), new(-1, 0), new(0, -1), new(1, 0)
    };

    /// <summary>
    /// Returns the interior hex furthest from any region border — the "pole of inaccessibility."
    /// Uses grid coordinates (Hex.v2) so the result is independent of hexSize aspect ratio.
    /// </summary>
    private Vector3 CalculateDeepestHex()
    {
        if (hexes.Count == 0) return transform.position;
        if (hexes.Count == 1) return hexes[0].position;

        var hexComps = hexes.Select(t => t.GetComponent<Hex>()).Where(h => h != null).ToList();
        if (hexComps.Count != hexes.Count)
            return CalculateBoundsCenter(); // fallback: Hex component missing

        var regionSet = new HashSet<Vector2Int>(hexComps.Select(h => h.v2));
        var posMap = hexComps.ToDictionary(
            h => h.v2,
            h => new Vector2(h.transform.position.x, h.transform.position.y));

        // Border hexes: any grid-neighbor is outside the region
        var borderSet = new HashSet<Vector2Int>();
        foreach (var hex in hexComps)
        {
            var offsets = (hex.v2.x % 2 == 0) ? EvenRowOffsets : OddRowOffsets;
            foreach (var offset in offsets)
            {
                if (!regionSet.Contains(hex.v2 + offset))
                {
                    borderSet.Add(hex.v2);
                    break;
                }
            }
        }

        // Tiny region (all border): pick the hex with the most region-neighbors
        if (borderSet.Count == hexComps.Count)
        {
            Hex fallback = hexComps[0];
            int maxN = 0;
            foreach (var hex in hexComps)
            {
                var offsets = (hex.v2.x % 2 == 0) ? EvenRowOffsets : OddRowOffsets;
                int n = offsets.Count(o => regionSet.Contains(hex.v2 + o));
                if (n > maxN) { maxN = n; fallback = hex; }
            }
            return fallback.transform.position;
        }

        // Centroid of interior hexes — used as tie-breaker
        Vector2 interiorCentroid = Vector2.zero;
        int interiorCount = 0;
        foreach (var hex in hexComps)
        {
            if (!borderSet.Contains(hex.v2))
            {
                interiorCentroid += posMap[hex.v2];
                interiorCount++;
            }
        }
        if (interiorCount > 0) interiorCentroid /= interiorCount;

        // Score: primary = min world-distance to border, secondary = closeness to interior centroid
        Hex best = hexComps.First(h => !borderSet.Contains(h.v2));
        float bestScore = float.MinValue;

        foreach (var hex in hexComps)
        {
            if (borderSet.Contains(hex.v2)) continue;

            Vector2 p = posMap[hex.v2];
            float minDistToBorder = float.MaxValue;
            foreach (var bv2 in borderSet)
            {
                if (posMap.TryGetValue(bv2, out Vector2 bp))
                {
                    float d = Vector2.Distance(p, bp);
                    if (d < minDistToBorder) minDistToBorder = d;
                }
            }

            float score = minDistToBorder * 1000f - Vector2.Distance(p, interiorCentroid);
            if (score > bestScore) { bestScore = score; best = hex; }
        }

        return best.transform.position;
    }

    private float ComputeOrientation()
    {
        if (hexes.Count < 2) return 0f;

        // PCA: find the major axis of the hex point cloud
        Vector2 centroid = Vector2.zero;
        foreach (var h in hexes)
            centroid += HexXY(h);
        centroid /= hexes.Count;

        float xx = 0f, xy = 0f, yy = 0f;
        foreach (var h in hexes)
        {
            Vector2 d = HexXY(h) - centroid;
            xx += d.x * d.x;
            xy += d.x * d.y;
            yy += d.y * d.y;
        }

        // Eigenvalues of the 2×2 covariance matrix
        float trace = xx + yy;
        float det = xx * yy - xy * xy;
        float discriminant = Mathf.Sqrt(Mathf.Max(0f, trace * trace * 0.25f - det));
        float lambda1 = trace * 0.5f + discriminant;
        float lambda2 = trace * 0.5f - discriminant;

        // If the region is roughly circular keep text horizontal
        if (lambda2 > 0f && lambda1 / lambda2 < aspectThreshold)
            return 0f;

        // Angle of the major eigenvector, in [-90, 90] degrees
        return 0.5f * Mathf.Atan2(2f * xy, xx - yy) * Mathf.Rad2Deg;
    }

    private void FitTextToRegion()
    {
        if (hexes.Count == 0) return;

        // Project all hex positions onto the text direction to get available span
        float angleRad = ComputedRotation * Mathf.Deg2Rad;
        Vector2 textDir = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));

        float minProj = float.MaxValue, maxProj = float.MinValue;
        foreach (var h in hexes)
        {
            float proj = Vector2.Dot(HexXY(h), textDir);
            if (proj < minProj) minProj = proj;
            if (proj > maxProj) maxProj = proj;
        }
        float availableSpace = (maxProj - minProj) * (1f - regionPadding);

        spawnedLabel.fontSize = maxFontSize;
        spawnedLabel.ForceMeshUpdate();
        float textWidth = spawnedLabel.textBounds.size.x;

        if (textWidth > 0 && textWidth > availableSpace)
        {
            float scale = availableSpace / textWidth;
            spawnedLabel.fontSize = Mathf.Max(minFontSize, spawnedLabel.fontSize * scale);
        }
    }

    private static Vector2 HexXY(Transform t) => new(t.position.x, t.position.y);

    public void CollectHexesFromChildren()
    {
        hexes.Clear();
        foreach (Transform child in transform)
        {
            if (child.GetComponent<Hex>() != null)
                hexes.Add(child);
        }
    }

    public void FindHexesByRegionName(IEnumerable<Hex> boardHexes)
    {
        if (string.IsNullOrWhiteSpace(regionName))
            return;

        hexes = boardHexes
            .Where(h => h.GetLandRegion() != null &&
                        h.GetLandRegion().Equals(regionName, System.StringComparison.OrdinalIgnoreCase))
            .Select(h => h.transform)
            .ToList();
    }

    private Vector3 CalculateAverageCenter()
    {
        Vector3 sum = Vector3.zero;
        foreach (var hex in hexes)
            sum += hex.position;
        return sum / hexes.Count;
    }

    private Vector3 CalculateBoundsCenter()
    {
        if (hexes.Count == 0) return transform.position;

        Bounds bounds = new Bounds(hexes[0].position, Vector3.zero);
        foreach (var hex in hexes)
            bounds.Encapsulate(hex.position);
        return bounds.center;
    }

    void OnDrawGizmosSelected()
    {
        if (hexes == null || hexes.Count == 0) return;

        Gizmos.color = Color.cyan;
        Vector3 center = useDeepestHex ? CalculateDeepestHex() : CalculateBoundsCenter();
        Gizmos.DrawWireSphere(center, 0.3f);
        Gizmos.DrawLine(center, center + labelOffset);
    }
}
