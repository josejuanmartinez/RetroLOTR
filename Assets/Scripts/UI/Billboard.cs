using UnityEngine;

/// <summary>
/// Keeps a transform facing the camera.
/// Useful for world-space text labels that should remain readable.
/// </summary>
public class Billboard : MonoBehaviour
{
    [Tooltip("If true, only rotate around the Y axis (upright text).")]
    public bool uprightOnly = true;

    void LateUpdate()
    {
        if (Camera.main == null) return;

        if (uprightOnly)
        {
            Vector3 dir = Camera.main.transform.position - transform.position;
            dir.y = 0;
            if (dir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(-dir);
        }
        else
        {
            transform.forward = Camera.main.transform.forward;
        }
    }
}
