using System.Text.RegularExpressions;
using UnityEngine;

// Tracks which hex sits under the camera centre (exposed via CurrentHexCoords / HasRegisteredHit,
// used by MessageDisplayNoUI). The actual camera clamping to the discovered area now lives on
// BoardNavigator, which is guaranteed to be the live, moving camera.
public class MapBorderDetector : MonoBehaviour
{
    private static readonly Regex CoordRegex = new(@"(-?\d+)\s*,\s*(-?\d+)", RegexOptions.Compiled);

    public int maxDistance = 100;

    [SerializeField] private BoardNavigator boardNavigator;

    private Transform trackedTransform;
    private Vector3 lastValidNavigatorPosition;
    private Vector2Int lastHitHexCoords = new(-1, -1);
    private bool hasRegisteredHit;

    public Vector2Int CurrentHexCoords => lastHitHexCoords;
    public bool HasRegisteredHit => hasRegisteredHit;

    void Awake()
    {
        if (boardNavigator == null)
            boardNavigator = GetComponentInParent<BoardNavigator>();

        trackedTransform = boardNavigator != null ? boardNavigator.transform : transform.parent;
        if (trackedTransform != null)
            lastValidNavigatorPosition = trackedTransform.position;
    }

    void LateUpdate()
    {
        if (trackedTransform == null) return;

        var origin = transform.position;
        var dir = transform.forward;

        if (Physics.Raycast(origin, dir, out var hit, maxDistance))
        {
            hasRegisteredHit = true;
            lastValidNavigatorPosition = trackedTransform.position;
            UpdateLastHitCoords(hit.collider.transform);
        }
    }

    private void UpdateLastHitCoords(Transform target)
    {
        while (target != null)
        {
            if (TryParseCoords(target.name, out var coords))
            {
                lastHitHexCoords = coords;
                return;
            }

            target = target.parent;
        }

        lastHitHexCoords = new Vector2Int(-1, -1);
    }

    private static bool TryParseCoords(string candidate, out Vector2Int coords)
    {
        coords = new Vector2Int(-1, -1);
        if (string.IsNullOrWhiteSpace(candidate)) return false;

        var match = CoordRegex.Match(candidate);
        if (!match.Success) return false;

        if (int.TryParse(match.Groups[1].Value, out var x) && int.TryParse(match.Groups[2].Value, out var y))
        {
            coords = new Vector2Int(x, y);
            return true;
        }

        return false;
    }
}
