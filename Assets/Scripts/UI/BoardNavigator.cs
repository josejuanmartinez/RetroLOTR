using System.Collections;
using UnityEngine;

public class BoardNavigator : MonoBehaviour
{
    public float moveSpeed = 5.0f;
    public float zoomSpeed = 10.0f;
    private bool isMouseWheelHeld = false;

    private Camera boardCamera;

    private Coroutine lookAtCoroutine;

    private void Awake()
    {
        boardCamera = GetComponent<Camera>();
    }

    void Update()
    {
        // Check if the right mouse button is held
        if (Input.GetMouseButtonDown(2))
        {
            isMouseWheelHeld = true;
            Cursor.lockState = CursorLockMode.Locked; // Hide and lock cursor
            Cursor.visible = false;
        }
        else if (Input.GetMouseButtonUp(2))
        {
            isMouseWheelHeld = false;
            Cursor.lockState = CursorLockMode.None; // Unlock cursor
            Cursor.visible = true;
        }

        if (isMouseWheelHeld)
        {
            HandleMovement();
        }

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
            boardCamera.orthographicSize -= scroll * zoomSpeed;
            boardCamera.orthographicSize = Mathf.Clamp(boardCamera.orthographicSize, 1f, 20f);
        }
    }

    public void LookAt(Vector3 worldPosition)
    {
        // If there's already a LookAt coroutine running, stop it
        if (lookAtCoroutine != null)
        {
            StopCoroutine(lookAtCoroutine);
        }

        // Start a new coroutine to smoothly move to the target position
        lookAtCoroutine = StartCoroutine(SmoothLookAt(worldPosition));
    }

    private IEnumerator SmoothLookAt(Vector3 targetPosition)
    {
        Vector3 startPosition = transform.position;
        
        targetPosition.z = startPosition.z;

        float journeyLength = Vector3.Distance(startPosition, targetPosition);
        float startTime = Time.time;
        float duration = 1.0f; // 2 seconds for the transition

        while (Time.time - startTime < duration)
        {
            float distanceCovered = (Time.time - startTime) * journeyLength / duration;
            float fractionOfJourney = distanceCovered / journeyLength;

            // Use smoothstep interpolation for a more natural movement
            float t = Mathf.SmoothStep(0, 1, fractionOfJourney);
            transform.position = Vector3.Lerp(startPosition, targetPosition, t);

            yield return null;
        }

        // Ensure we end exactly at the target position
        transform.position = targetPosition;
        lookAtCoroutine = null;
    }
}
