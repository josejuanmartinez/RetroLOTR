using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class MessageDisplayNoUI : MonoBehaviour
{
    private static MessageDisplayNoUI instance;

    [Header("References")]
    [SerializeField] private TextMeshPro textMesh;   // 3D TextMeshPro component

    [Header("Timing")]
    [SerializeField] private float displayDuration = 1f;
    [SerializeField] private float fadeDuration = 0.5f;

    [Header("Layout")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.5f, 0f);
    [SerializeField] private bool faceCamera = true;

    private readonly Queue<MessageData> messageQueue = new Queue<MessageData>();
    private readonly Dictionary<Vector2Int, Queue<MessageData>> pendingByHex = new Dictionary<Vector2Int, Queue<MessageData>>();
    private readonly List<Vector2Int> pendingKeysToRemove = new List<Vector2Int>();
    private bool isDisplayingMessage = false;
    private Camera mainCam;
    private MapBorderDetector mapBorderDetector;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        mainCam = Camera.main;
        if (mapBorderDetector == null)
            mapBorderDetector = FindAnyObjectByType<MapBorderDetector>();

        if (textMesh != null)
        {
            textMesh.text = "";
            SetTextAlpha(0f);
            textMesh.enabled = false;
        }
    }

    private void LateUpdate()
    {
        EnsureCameraReferences();

        if (faceCamera && mainCam != null && textMesh != null)
        {
            // Billboard to camera
            transform.LookAt(transform.position + mainCam.transform.rotation * Vector3.forward,
                             mainCam.transform.rotation * Vector3.up);
        }

        TryPromotePendingMessages();
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    public static void ShowMessage(Hex hex, Character character, string message, Color? color = null)
    {
        if (hex == null || hex.gameObject == null) return;

        Game game = FindFirstObjectByType<Game>();
        if (game == null) return;

        if(!game || !game.currentlyPlaying || !game.started)
        {

            // Debug.Log(message);
            return;
        }

        string author = $"[{game.currentlyPlaying.characterName}]";
        string characterName = character != game.currentlyPlaying ? $"({character.characterName})" : "";
        string hexText = hex.GetText();
        string textMessage = $"{author}{characterName} {hexText}: \"{message}\"";

        bool playerCanSeeHex = game.player != null && game.player.visibleHexes.Contains(hex);
        bool publicRumour = character != null && character.GetOwner() == game.player; // only publish our own actions

        // Only show floating text when the human player can see the hex (prevents enemy leakage)
        if (playerCanSeeHex)
        {
            Vector3 worldPos = hex.gameObject.transform.position;
            if (instance != null)
            {
                if (instance.CanDisplayNow(hex))
                    instance.EnqueueMessage(hex, message, worldPos, color ?? Color.white);
                else
                    instance.EnqueueDeferred(hex, message, worldPos, color ?? Color.white);
            }
        }

        Rumour rumour = new Rumour {leader = character.GetOwner(), characterName = character?.characterName, rumour = textMessage, v2 = hex.v2};
        RumoursManager.AddRumour(rumour, publicRumour);
    }

    // -------------------------------------------------------------------------
    // Queue / Display Logic
    // -------------------------------------------------------------------------

    private void EnqueueMessage(Hex hex, string message, Vector3 worldPos, Color textColor)
    {
        messageQueue.Enqueue(new MessageData(hex, message, worldPos, textColor));

        if (!isDisplayingMessage)
            ProcessNextMessage();
    }

    private void EnqueueDeferred(Hex hex, string message, Vector3 worldPos, Color textColor)
    {
        if (hex == null) return;
        var key = hex.v2;
        if (!pendingByHex.TryGetValue(key, out var queue))
        {
            queue = new Queue<MessageData>();
            pendingByHex.Add(key, queue);
        }
        queue.Enqueue(new MessageData(hex, message, worldPos, textColor));
    }

    private void EnqueueDeferred(MessageData data)
    {
        if (data == null || data.Hex == null) return;
        EnqueueDeferred(data.Hex, data.Message, data.WorldPos, data.TextColor);
    }

    private void ProcessNextMessage()
    {
        while (messageQueue.Count > 0)
        {
            var next = messageQueue.Dequeue();
            if (CanDisplayNow(next.Hex))
            {
                StartCoroutine(DisplayCoroutine(next));
                return;
            }

            EnqueueDeferred(next);
        }

        isDisplayingMessage = false;
    }

    private IEnumerator DisplayCoroutine(MessageData data)
    {
        isDisplayingMessage = true;
        textMesh.enabled = true;

        // Set position & color
        transform.position = data.WorldPos + worldOffset;
        textMesh.text = data.Message;
        textMesh.color = new Color(data.TextColor.r, data.TextColor.g, data.TextColor.b, 0f);

        // Fade in
        yield return Fade(0f, 1f, fadeDuration, data.TextColor);

        // Wait
        float hold = Mathf.Max(0f, displayDuration - fadeDuration * 2f);
        if (hold > 0f) yield return new WaitForSeconds(hold);

        // Fade out
        yield return Fade(1f, 0f, fadeDuration, data.TextColor);

        textMesh.enabled = false;
        ProcessNextMessage();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private IEnumerator Fade(float from, float to, float duration, Color baseColor)
    {
        float t = 0f;
        while (t < duration)
        {
            float a = Mathf.Lerp(from, to, t / duration);
            SetTextAlpha(a, baseColor);
            t += Time.deltaTime;
            yield return null;
        }
        SetTextAlpha(to, baseColor);
    }

    private void SetTextAlpha(float a, Color? baseColor = null)
    {
        if (textMesh == null) return;
        Color c = baseColor ?? textMesh.color;
        textMesh.color = new Color(c.r, c.g, c.b, a);
    }

    private void EnsureCameraReferences()
    {
        if (mainCam == null)
            mainCam = Camera.main;

        if (mapBorderDetector == null)
            mapBorderDetector = mainCam != null ? mainCam.GetComponentInChildren<MapBorderDetector>() : FindAnyObjectByType<MapBorderDetector>();
    }

    private bool CanDisplayNow(Hex hex)
    {
        if (hex == null || hex.gameObject == null) return false;
        EnsureCameraReferences();

        if (mainCam != null)
        {
            Vector3 viewportPos = mainCam.WorldToViewportPoint(hex.transform.position);
            if (viewportPos.z <= 0f) return false;
            return viewportPos.x >= 0f && viewportPos.x <= 1f && viewportPos.y >= 0f && viewportPos.y <= 1f;
        }

        if (mapBorderDetector != null && mapBorderDetector.HasRegisteredHit)
            return mapBorderDetector.CurrentHexCoords == hex.v2;

        return true;
    }

    private void TryPromotePendingMessages()
    {
        if (pendingByHex.Count == 0) return;

        pendingKeysToRemove.Clear();
        foreach (var entry in pendingByHex)
        {
            var queue = entry.Value;
            if (queue.Count == 0)
            {
                pendingKeysToRemove.Add(entry.Key);
                continue;
            }

            var next = queue.Peek();
            if (next == null || next.Hex == null)
            {
                pendingKeysToRemove.Add(entry.Key);
                continue;
            }

            if (CanDisplayNow(next.Hex))
            {
                while (queue.Count > 0)
                    messageQueue.Enqueue(queue.Dequeue());

                pendingKeysToRemove.Add(entry.Key);
            }
        }

        for (int i = 0; i < pendingKeysToRemove.Count; i++)
            pendingByHex.Remove(pendingKeysToRemove[i]);

        if (!isDisplayingMessage && messageQueue.Count > 0)
            ProcessNextMessage();
    }

    // -------------------------------------------------------------------------
    // Data
    // -------------------------------------------------------------------------

    private class MessageData
    {
        public Hex Hex { get; }
        public string Message { get; }
        public Vector3 WorldPos { get; }
        public Color TextColor { get; }

        public MessageData(Hex hex, string message, Vector3 worldPos, Color textColor)
        {
            Hex = hex;
            Message = message;
            WorldPos = worldPos;
            TextColor = textColor;
        }
    }
}
