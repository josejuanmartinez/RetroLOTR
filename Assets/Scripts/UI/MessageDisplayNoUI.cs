using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class MessageDisplayNoUI : MonoBehaviour
{
    private static MessageDisplayNoUI instance;
    private static bool displayPaused;

    [Header("References")]
    [SerializeField] private TextMeshPro textMesh;   // 3D TextMeshPro component

    [Header("Timing")]
    [SerializeField] private float displayDuration = 1f;
    [SerializeField] private float fadeDuration = 0.5f;

    [Header("Layout")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.5f, 0f);
    [SerializeField] private bool faceCamera = true;
    [SerializeField] private float fontScale = 0.5f;

    private readonly Queue<MessageData> messageQueue = new Queue<MessageData>();
    private readonly Dictionary<Vector2Int, Queue<MessageData>> pendingByHex = new Dictionary<Vector2Int, Queue<MessageData>>();
    private readonly List<Vector2Int> pendingKeysToRemove = new List<Vector2Int>();
    private readonly Dictionary<Vector2Int, List<System.Action>> pendingFocusRequests = new Dictionary<Vector2Int, List<System.Action>>();
    private bool isDisplayingMessage = false;
    private int focusHoldCount = 0;
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
            if (fontScale > 0f)
            {
                textMesh.fontSize *= fontScale;
            }
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

        bool playerCanSeeHex = game.player != null
            && game.player.visibleHexes.Contains(hex)
            && hex.IsHexSeen();
        bool publicRumour = character != null && (character.GetOwner() == game.player || playerCanSeeHex);

        Color resolved = color ?? Color.white;
        string displayMessage = message;
        if (character != null && character.GetOwner() != null && character.GetOwner() != game.player)
        {
            bool knownEnemy = playerCanSeeHex && (hex.IsScouted(game.player) || character.IsArmyCommander());
            bool spotted = false;
            if (knownEnemy)
            {
                displayMessage = $"{character.characterName}: {message}";
            }
            else
            {
                int totalLevel = character.GetCommander() + character.GetAgent() + character.GetEmmissary() + character.GetMage();
                int threshold = Mathf.Max(totalLevel, character.GetAgent() * 10);
                int roll = UnityEngine.Random.Range(0, 101);
                spotted = roll < threshold;
                string prefix = spotted ? $"{character.characterName}:" : "unspotted enemy:";
                displayMessage = $"{prefix} {message}";
            }
            if (playerCanSeeHex && (knownEnemy || spotted) && character.GetOwner() is NonPlayableLeader npl && game.player != null)
            {
                if (!npl.IsRevealedToLeader(game.player))
                {
                    npl.RevealToLeader(game.player, game.IsPlayerCurrentlyPlaying());
                }
            }
        }

        // Only show floating text when the human player can see the hex (prevents enemy leakage)
        if (playerCanSeeHex)
        {
            Vector3 worldPos = hex.gameObject.transform.position;
            if (instance != null)
            {
                if (IsNegativeColor(resolved))
                {
                    Sounds.Instance?.PlayNegative();
                }
                else if (IsPositiveColor(resolved))
                {
                    Sounds.Instance?.PlayPositive();
                }
                else
                {
                    Sounds.Instance?.PlayMessage();
                }
                if (instance.CanDisplayNow(hex))
                    instance.EnqueueMessage(hex, displayMessage, worldPos, resolved);
                else
                    instance.EnqueueDeferred(hex, displayMessage, worldPos, resolved);
            }
        }

        Rumour rumour = new Rumour {leader = character.GetOwner(), character = character, characterName = character?.characterName, rumour = message, v2 = hex.v2};
        RumoursManager.AddRumour(rumour, publicRumour);
    }

    public static bool IsBusy()
    {
        if (instance == null) return false;
        return instance.isDisplayingMessage || instance.messageQueue.Count > 0 || instance.pendingByHex.Count > 0;
    }

    public static bool IsDisplaying
    {
        get
        {
            if (instance == null) return false;
            return instance.isDisplayingMessage;
        }
    }

    public static bool IsHoldingFocus
    {
        get
        {
            if (instance == null) return false;
            return instance.focusHoldCount > 0;
        }
    }

    // -------------------------------------------------------------------------
    // Queue / Display Logic
    // -------------------------------------------------------------------------

    private void EnqueueMessage(Hex hex, string message, Vector3 worldPos, Color textColor)
    {
        EnqueueWithFocus(hex, message, worldPos, textColor);
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
        queue.Enqueue(new MessageData(hex, message, worldPos, textColor, true));
        if (!displayPaused)
            RequestFocusForMessage(hex, () => PromoteDeferredForHex(hex));
    }

    private void EnqueueDeferred(MessageData data)
    {
        if (data == null || data.Hex == null) return;
        EnqueueDeferred(data.Hex, data.Message, data.WorldPos, data.TextColor);
    }

    private void ProcessNextMessage()
    {
        if (displayPaused) return;
        while (messageQueue.Count > 0)
        {
            var next = messageQueue.Dequeue();
            if (ShouldSkipMessageHex(next.Hex))
            {
                if (next.RequiresFocus)
                {
                    focusHoldCount = Mathf.Max(0, focusHoldCount - 1);
                }
                continue;
            }
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
        float holdElapsed = 0f;
        while (holdElapsed < hold)
        {
            if (ShouldPauseDisplay())
            {
                yield return null;
                continue;
            }
            holdElapsed += Time.deltaTime;
            yield return null;
        }

        // Fade out
        yield return Fade(1f, 0f, fadeDuration, data.TextColor);

        textMesh.enabled = false;
        if (data.RequiresFocus)
        {
            focusHoldCount = Mathf.Max(0, focusHoldCount - 1);
        }
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
            if (ShouldPauseDisplay())
            {
                yield return null;
                continue;
            }
            float a = Mathf.Lerp(from, to, t / duration);
            SetTextAlpha(a, baseColor);
            t += Time.deltaTime;
            yield return null;
        }
        SetTextAlpha(to, baseColor);
    }

    private static bool IsNegativeColor(Color color)
    {
        return color.r >= 0.7f && color.g <= 0.4f;
    }

    private static bool IsPositiveColor(Color color)
    {
        return color.g >= 0.6f && color.b <= 0.6f;
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
            if (ShouldSkipMessageHex(next.Hex))
            {
                pendingKeysToRemove.Add(entry.Key);
                pendingFocusRequests.Remove(entry.Key);
                continue;
            }

            if (CanDisplayNow(next.Hex))
            {
                while (queue.Count > 0)
                    messageQueue.Enqueue(queue.Dequeue());

                pendingKeysToRemove.Add(entry.Key);
            }
            else
            {
                RequestFocusForMessage(next.Hex, () => PromoteDeferredForHex(next.Hex));
            }
        }

        for (int i = 0; i < pendingKeysToRemove.Count; i++)
            pendingByHex.Remove(pendingKeysToRemove[i]);

        if (!isDisplayingMessage && messageQueue.Count > 0)
            ProcessNextMessage();
    }

    private void RequestFocusForMessage(Hex hex)
    {
        if (hex == null) return;
        RequestFocusForMessage(hex, null);
    }

    private void RequestFocusForMessage(Hex hex, System.Action onArrive)
    {
        if (hex == null) return;
        if (displayPaused) return;
        Vector2Int key = hex.v2;
        if (!pendingFocusRequests.TryGetValue(key, out var callbacks))
        {
            callbacks = new List<System.Action>();
            pendingFocusRequests.Add(key, callbacks);
            if (BoardNavigator.Instance != null)
            {
                BoardNavigator.Instance.EnqueueMessageFocus(hex, () =>
                {
                    if (pendingFocusRequests.TryGetValue(key, out var list))
                    {
                        pendingFocusRequests.Remove(key);
                        for (int i = 0; i < list.Count; i++)
                        {
                            list[i]?.Invoke();
                        }
                    }
                });
            }
            else
            {
                pendingFocusRequests.Remove(key);
            }
        }
        if (onArrive != null) callbacks.Add(onArrive);
    }

    private void PromoteDeferredForHex(Hex hex)
    {
        if (hex == null) return;
        var key = hex.v2;
        if (!pendingByHex.TryGetValue(key, out var queue) || queue.Count == 0) return;
        if (ShouldSkipMessageHex(hex))
        {
            pendingByHex.Remove(key);
            return;
        }
        while (queue.Count > 0)
        {
            var data = queue.Dequeue();
            if (data != null && data.RequiresFocus) focusHoldCount++;
            messageQueue.Enqueue(data);
        }
        pendingByHex.Remove(key);
        if (!isDisplayingMessage)
            ProcessNextMessage();
    }

    private void EnqueueWithFocus(Hex hex, string message, Vector3 worldPos, Color textColor)
    {
        if (hex == null) return;
        if (displayPaused)
        {
            messageQueue.Enqueue(new MessageData(hex, message, worldPos, textColor, true));
            return;
        }
        if (BoardNavigator.Instance == null)
        {
            messageQueue.Enqueue(new MessageData(hex, message, worldPos, textColor));
            if (!isDisplayingMessage)
                ProcessNextMessage();
            return;
        }

        RequestFocusForMessage(hex, () =>
        {
            if (ShouldSkipMessageHex(hex)) return;
            focusHoldCount++;
            messageQueue.Enqueue(new MessageData(hex, message, worldPos, textColor, true));
            if (!isDisplayingMessage)
                ProcessNextMessage();
        });
    }

    private static bool ShouldPauseDisplay()
    {
        return displayPaused || PopupManager.IsShowing || ConfirmationDialog.IsShowing || SelectionDialog.IsShowing;
    }

    private static bool ShouldSkipMessageHex(Hex hex)
    {
        return hex == null || !hex.IsHexSeen();
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
        public bool RequiresFocus { get; }

        public MessageData(Hex hex, string message, Vector3 worldPos, Color textColor, bool requiresFocus = false)
        {
            Hex = hex;
            Message = message;
            WorldPos = worldPos;
            TextColor = textColor;
            RequiresFocus = requiresFocus;
        }
    }

    public static void SetPaused(bool paused)
    {
        displayPaused = paused;
        if (!displayPaused && instance != null)
        {
            instance.TryPromotePendingMessages();
            if (!instance.isDisplayingMessage)
            {
                instance.ProcessNextMessage();
            }
        }
    }
}
