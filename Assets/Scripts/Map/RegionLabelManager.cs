using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;

/// <summary>
/// Automatically creates region labels for every unique land region on the board.
/// Place this on the Board or an empty manager object and call Generate() after hex generation.
/// </summary>
public class RegionLabelManager : MonoBehaviour
{
    [Tooltip("Parent transform for spawned region label objects.")]
    public Transform labelsParent;

    [Tooltip("World-space TextMeshPro prefab (3D Object -> Text - TextMeshPro).")]
    public TMP_Text labelPrefab;

    [Header("Appearance")]
    public Vector3 labelOffset = new Vector3(0, 0, -1f);
    public string sortingLayerName = "UI";
    public int sortingOrder = 100;
    [Tooltip("Unity layer for label objects. Set to RegionLabels so cameras can include/exclude them independently.")]
    public string labelsLayerName = "RegionLabels";

    [Header("Orientation")]
    public bool autoOrient = true;
    public float aspectThreshold = 1.4f;

    [Header("Curve")]
    public bool useCurvedText = false;
    public float curveRadius = 10f;
    public float arcAngle = 20f;

    [Header("Readability")]
    public bool billboard = false;
    public bool autoSize = true;
    public float minFontSize = 1f;
    public float maxFontSize = 5f;
    public bool scaleFontToRegionSize = true;
    public float regionPadding = 0.15f;

    [Header("Center")]
    public bool useDeepestHex = true;

    [Header("Filtering")]
    [Tooltip("Regions with fewer hexes than this won't get a label.")]
    public int minHexCountForLabel = 4;

    [Header("Overlap Resolution")]
    [Tooltip("If true, nudge overlapping labels apart after generation.")]
    public bool resolveOverlaps = true;

    [Tooltip("How many iterations to run overlap resolution.")]
    public int overlapIterations = 5;

    [Tooltip("How far labels can drift from their computed center.")]
    public float maxDrift = 3f;

    private readonly Dictionary<string, RegionLabel> spawnedLabels = new();

    public void Generate(IEnumerable<Hex> boardHexes)
    {
        if (labelPrefab == null)
        {
            Debug.LogWarning("RegionLabelManager: No label prefab assigned.", this);
            return;
        }

        var grouped = boardHexes
            .Where(h => !string.IsNullOrWhiteSpace(h.GetLandRegion()))
            .GroupBy(h => h.GetLandRegion().Trim())
            .ToList();

        foreach (var group in grouped)
        {
            string regionName = group.Key;
            List<Transform> regionHexes = group.Select(h => h.transform).ToList();

            if (regionHexes.Count < minHexCountForLabel)
                continue;

            if (!spawnedLabels.TryGetValue(regionName, out RegionLabel label))
            {
                GameObject go = new GameObject($"Region_{regionName}");
                go.transform.SetParent(labelsParent != null ? labelsParent : transform, false);

                label = go.AddComponent<RegionLabel>();
                spawnedLabels[regionName] = label;
            }

            label.regionName = regionName;
            label.hexes = regionHexes;
            label.labelPrefab = labelPrefab;
            label.labelOffset = labelOffset;
            label.sortingLayerName = sortingLayerName;
            label.sortingOrder = sortingOrder;
            label.useDeepestHex = useDeepestHex;
            label.autoOrient = autoOrient;
            label.aspectThreshold = aspectThreshold;
            label.useCurvedText = useCurvedText;
            label.curveRadius = curveRadius;
            label.arcAngle = arcAngle;
            label.billboard = billboard;
            label.autoSize = autoSize;
            label.minFontSize = minFontSize;
            label.maxFontSize = maxFontSize;
            label.scaleFontToRegionSize = scaleFontToRegionSize;
            label.regionPadding = regionPadding;
            label.labelsLayerName = labelsLayerName;

            label.CreateLabel();
        }

        if (resolveOverlaps)
            ResolveOverlaps();
    }

    private void ResolveOverlaps()
    {
        var activeLabels = spawnedLabels.Values
            .Where(l => l != null && l.SpawnedLabel != null)
            .ToList();

        if (activeLabels.Count == 0) return;

        Dictionary<RegionLabel, Vector3> originalCenters = new();
        foreach (var label in activeLabels)
            originalCenters[label] = label.ComputedCenter;

        for (int iter = 0; iter < overlapIterations; iter++)
        {
            // Repulsion
            for (int i = 0; i < activeLabels.Count; i++)
            {
                for (int j = i + 1; j < activeLabels.Count; j++)
                {
                    var a = activeLabels[i];
                    var b = activeLabels[j];

                    Bounds boundsA = a.SpawnedLabel.GetComponent<MeshRenderer>().bounds;
                    Bounds boundsB = b.SpawnedLabel.GetComponent<MeshRenderer>().bounds;

                    if (!boundsA.Intersects(boundsB))
                        continue;

                    Vector3 delta = b.SpawnedLabel.transform.position - a.SpawnedLabel.transform.position;
                    delta.z = 0f;

                    float distance = delta.magnitude;
                    if (distance < 0.01f)
                    {
                        delta = Random.insideUnitCircle;
                        delta.z = 0f;
                        distance = 0.1f;
                    }

                    Vector3 dir = delta / distance;
                    float overlap = (boundsA.extents.magnitude + boundsB.extents.magnitude) - distance;
                    if (overlap <= 0) continue;

                    Vector3 push = dir * overlap * 0.6f;
                    a.SpawnedLabel.transform.position -= push;
                    b.SpawnedLabel.transform.position += push;
                }
            }

            // Spring back to original center
            foreach (var label in activeLabels)
            {
                Vector3 toCenter = originalCenters[label] - label.SpawnedLabel.transform.position;
                toCenter.z = 0f;
                float drift = toCenter.magnitude;
                if (drift > maxDrift)
                {
                    toCenter = toCenter.normalized * maxDrift;
                    label.SpawnedLabel.transform.position = originalCenters[label] - toCenter;
                }
                else
                {
                    label.SpawnedLabel.transform.position += toCenter * 0.3f;
                }
            }
        }
    }

    public void Clear()
    {
        foreach (var kvp in spawnedLabels)
        {
            if (kvp.Value != null && kvp.Value.gameObject != null)
            {
                if (Application.isPlaying)
                    Destroy(kvp.Value.gameObject);
                else
                    DestroyImmediate(kvp.Value.gameObject);
            }
        }
        spawnedLabels.Clear();
    }
}
