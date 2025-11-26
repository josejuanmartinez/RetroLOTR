using System.Collections;
using Google.Protobuf.WellKnownTypes;
using UnityEngine;

public class BoardNavigator : MonoBehaviour
{
    [Header("Move")]
    public float moveSpeed = 5.0f;

    [Header("Zoom")]
    public float zoomSpeed = 10.0f;
    public float smoothTime = 10f;

    [Tooltip("Minimum orthographic size (prevents camera from going flat)")]
    public float minZoom = 0.5f;

    [Tooltip("Maximum orthographic size (prevents excessive zoom-out)")]
    public float maxZoom = 20f;

    private bool isMouseWheelHeld = false;
    private Camera boardCamera;
    private Coroutine lookAtCoroutine;
    private float targetZoom;

    private void Awake()
    {
        boardCamera = GetComponent<Camera>();

        if (boardCamera != null && boardCamera.orthographic)
        {
            // Initialize the target zoom so it matches the current camera size
            targetZoom = Mathf.Clamp(boardCamera.orthographicSize, minZoom, maxZoom);
        }
    }

    void Update()
    {
        // Middle mouse button (wheel) pans the board
        if (Input.GetMouseButtonDown(2))
        {
            isMouseWheelHeld = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else if (Input.GetMouseButtonUp(2))
        {
            isMouseWheelHeld = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (isMouseWheelHeld)
            HandleMovement();
        else
            HandleZoom();
    }

    void HandleMovement()
    {
        float moveX = Input.GetAxis("Mouse X");
        float moveY = Input.GetAxis("Mouse Y");

        Vector3 move = new Vector3(moveX, moveY, 0);
        transform.position += move * moveSpeed * Time.deltaTime;
    }

    void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");

        if (boardCamera != null && boardCamera.orthographic)
        {
            if (Mathf.Abs(scroll) > 0.001f)
            {
                targetZoom -= scroll * zoomSpeed;
                targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
            }

            boardCamera.orthographicSize = Mathf.Lerp(
                boardCamera.orthographicSize,
                targetZoom,
                Time.deltaTime * smoothTime
            );

            // Absolute safeguard
            boardCamera.orthographicSize = Mathf.Clamp(boardCamera.orthographicSize, minZoom, maxZoom);
        }
    }

    public void LookAt(Vector3 targetPosition, float duration = 1.0f, float delay = 0.0f)
    {
        if (lookAtCoroutine != null)
            StopCoroutine(lookAtCoroutine);

        lookAtCoroutine = StartCoroutine(SmoothLookAt(targetPosition, duration, delay));
    }

    private IEnumerator SmoothLookAt(Vector3 targetPosition, float duration = 1.0f, float delay = 0.0f)
    {
        Vector3 startPosition = transform.position;
        targetPosition.z = startPosition.z;
        float journeyLength = Vector3.Distance(startPosition, targetPosition);

        if (journeyLength < 0.001f)
        {
            lookAtCoroutine = null;
            yield break;
        }

        yield return new WaitForSeconds(delay);

        float startTime = Time.time;
        while (Time.time - startTime < duration)
        {
            float timeElapsed = Time.time - startTime;
            float t = Mathf.SmoothStep(0, 1, timeElapsed / duration);

            Vector3 newPosition = Vector3.Lerp(startPosition, targetPosition, t);
            if (!float.IsNaN(newPosition.x) && !float.IsNaN(newPosition.y) && !float.IsNaN(newPosition.z))
                transform.position = newPosition;

            yield return null;
        }

        transform.position = targetPosition;
        lookAtCoroutine = null;
    }

    public void LookAtSelected()
    {
        Board board = FindAnyObjectByType<Board>();
        if (board != null && board.selectedCharacter != null && board.selectedCharacter.hex != null)
        {
            LookAt(board.selectedCharacter.hex.transform.position, 1.0f, 0.0f);
        }
    }

    public void ClampToLastValidPosition(Vector3 lastValidPosition, Vector2Int lastHitHexCoords)
    {
        var desiredPosition = transform.position;
        var attemptedDelta = desiredPosition - lastValidPosition;
        const float epsilon = 0.0001f;
        bool clamped = false;

        if (attemptedDelta.x > epsilon)
        {
            desiredPosition.x = lastValidPosition.x;
            clamped = true;
        }
        else if (attemptedDelta.x < -epsilon)
        {
            desiredPosition.x = lastValidPosition.x;
            clamped = true;
        }

        if (attemptedDelta.y > epsilon)
        {
            desiredPosition.y = lastValidPosition.y;
            clamped = true;
        }
        else if (attemptedDelta.y < -epsilon)
        {
            desiredPosition.y = lastValidPosition.y;
            clamped = true;
        }

        if (!clamped) return;

        transform.position = desiredPosition;

        /*if (lastHitHexCoords.x >= 0 && lastHitHexCoords.y >= 0)
        {
            Debug.Log($"Cannot move past hex {lastHitHexCoords.x},{lastHitHexCoords.y}; movement clamped to board bounds.");
        }
        else
        {
            Debug.Log("Cannot move further in that direction; movement clamped to board bounds.");
        }
        */
    }
}
