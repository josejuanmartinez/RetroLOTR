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
    private bool isDisplayingMessage = false;
    private Camera mainCam;

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

        if (textMesh != null)
        {
            textMesh.text = "";
            SetTextAlpha(0f);
            textMesh.enabled = false;
        }
    }

    private void LateUpdate()
    {
        if (faceCamera && mainCam != null && textMesh != null)
        {
            // Billboard to camera
            transform.LookAt(transform.position + mainCam.transform.rotation * Vector3.forward,
                             mainCam.transform.rotation * Vector3.up);
        }
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

            Debug.Log(message);
            return;
        }

        string author = $"[{game.currentlyPlaying.characterName}]";
        string characterName = character != game.currentlyPlaying ? $"->[{character.characterName}]" : "";
        string hexText = hex.GetText();
        string textMessage = $"{author}{characterName} {hexText}: {message}";

        bool publicRumour = game.currentlyPlaying.visibleHexes.Contains(hex);
        if (publicRumour)
        {
            Vector3 worldPos = hex.gameObject.transform.position;
            instance.EnqueueMessage(message, worldPos, color ?? Color.white);
        }

        Rumour rumour = new Rumour {leader = character.GetOwner(), rumour = textMessage, v2 = hex.v2};
        RumoursManager.AddRumour(rumour, publicRumour);
    }

    // -------------------------------------------------------------------------
    // Queue / Display Logic
    // -------------------------------------------------------------------------

    private void EnqueueMessage(string message, Vector3 worldPos, Color textColor)
    {
        messageQueue.Enqueue(new MessageData(message, worldPos, textColor));

        if (!isDisplayingMessage)
            ProcessNextMessage();
    }

    private void ProcessNextMessage()
    {
        if (messageQueue.Count > 0)
        {
            var next = messageQueue.Dequeue();
            StartCoroutine(DisplayCoroutine(next));
        }
        else
        {
            isDisplayingMessage = false;
        }
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

    // -------------------------------------------------------------------------
    // Data
    // -------------------------------------------------------------------------

    private class MessageData
    {
        public string Message { get; }
        public Vector3 WorldPos { get; }
        public Color TextColor { get; }

        public MessageData(string message, Vector3 worldPos, Color textColor)
        {
            Message = message;
            WorldPos = worldPos;
            TextColor = textColor;
        }
    }
}
