using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class PopupManager : MonoBehaviour
{
    public static PopupManager Instance { get; private set; }

    [Header("References")]
    public GameObject container;
    public Image actor1;
    public Image actor2;
    public Sprite actorReplacement;
    public RawImage actor1RawImage;
    public RawImage actor2RawImage;
    public VideoPlayer actor1Video;
    public VideoPlayer actor2Video;
    public GameObject leftArrow;
    public GameObject rightArrow;
    public TextMeshProUGUI textWidget;
    public TextMeshProUGUI titleWidget;
    public TypewriterEffect typeWriterEffect;
    public int referenceHeight = 600;

    private readonly List<PopupData> queue = new();
    private int currentIndex = -1;
    private RectTransform rectTransform;
    private Vector2 initialSize;
    public static bool IsShowing { get; private set; }
    private Videos videos;
    private Coroutine waitForMessagesRoutine;

    private struct PopupData
    {
        public string title;
        public Sprite spriteActor1;
        public Sprite spriteActor2;
        public string text;
        public bool typeWrite;
        public int restrictHeight;
        public Action onClose;
    }

    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject); // optional: persists across scenes

        rectTransform = container.GetComponent<RectTransform>();
        initialSize = rectTransform != null ? rectTransform.sizeDelta : Vector2.zero;
        IsShowing = false;
    }

    public void Initialize(string title, Sprite spriteActor1, Sprite spriteActor2, string text, bool typeWrite, int restrictHeight = 0, Action onClose = null)
    {
        queue.Add(new PopupData
        {
            title = title,
            spriteActor1 = spriteActor1,
            spriteActor2 = spriteActor2,
            text = text,
            typeWrite = typeWrite,
            restrictHeight = restrictHeight,
            onClose = onClose
        });

        if (currentIndex == -1)
        {
            if (ShouldDelayPopup())
            {
                StartWaitForMessages();
            }
            else
            {
                ShowEntry(0);
            }
        }
        else
        {
            UpdateArrows(); // refresh navigation availability when adding while already showing
        }
    }

    public void Hide()
    {
        Action onClose = null;
        if (currentIndex >= 0 && currentIndex < queue.Count)
        {
            onClose = queue[currentIndex].onClose;
        }

        FindFirstObjectByType<Sounds>().StopAllSounds();
        Music.Instance?.StopEventMusic();
        container.SetActive(false);
        queue.Clear();
        currentIndex = -1;
        IsShowing = false;

        if (rectTransform != null)
        {
            rectTransform.sizeDelta = initialSize;
        }

        if (typeWriterEffect != null)
        {
            typeWriterEffect.enabled = false;
            typeWriterEffect.fullText = "";
        }

        SetActorVisuals(actor1, actor1RawImage, actor1Video, null, null);
        SetActorVisuals(actor2, actor2RawImage, actor2Video, null, null);

        ActionsManager actionsManager = FindFirstObjectByType<ActionsManager>();
        if (actionsManager != null)
        {
            actionsManager.RefreshInteractableState();
        }

        onClose?.Invoke();
    }

    public static void Show(string title, Sprite spriteActor1, Sprite spriteActor2, string text, bool typeWrite, int restrictHeight = 0, Action onClose = null)
    => Instance.Initialize(title, spriteActor1, spriteActor2, text, typeWrite, restrictHeight, onClose);

    public static void HidePopup()
        => Instance.Hide();

    public static void CloseAll()
    {
        if (Instance == null) return;
        Instance.Hide();
    }

    public void ShowPrevious()
    {
        if (queue.Count == 0 || currentIndex <= 0)
            return;

        ShowEntry(currentIndex - 1);
    }

    public void ShowNext()
    {
        if (queue.Count == 0 || currentIndex >= queue.Count - 1)
            return;

        ShowEntry(currentIndex + 1);
    }

    public static void ShowPreviousPopup()
        => Instance.ShowPrevious();

    public static void ShowNextPopup()
        => Instance.ShowNext();

    private void ShowEntry(int index)
    {
        if (index < 0 || index >= queue.Count)
            return;

        currentIndex = index;
        PopupData data = queue[currentIndex];
        Sounds.Instance?.PlayMessage();
        Music.Instance?.PlayEventMusic();
        container.SetActive(true);
        IsShowing = true;

        if (typeWriterEffect != null)
        {
            if (data.typeWrite)
            {
                typeWriterEffect.enabled = true;
                typeWriterEffect.fullText = data.text;
                textWidget.text = "";
                typeWriterEffect.StartWriting();
            }
            else
            {
                typeWriterEffect.enabled = false;
                typeWriterEffect.fullText = "";
                textWidget.text = data.text;
            }
        }
        else
        {
            textWidget.text = data.text;
        }

        titleWidget.text = data.title;
        VideoClip actor1Clip = GetVideoByName(data.spriteActor1 != null ? data.spriteActor1.name : null);
        SetActorVisuals(
            actor1,
            actor1RawImage,
            actor1Video,
            actor1Clip,
            data.spriteActor1
        );

        VideoClip actor2Clip = GetVideoByName(data.spriteActor2 != null ? data.spriteActor2.name : null);
        bool hasActor2 = data.spriteActor2 != null || actor2Clip != null;
        SetActor2Active(hasActor2);
        SetActorVisuals(
            actor2,
            actor2RawImage,
            actor2Video,
            actor2Clip,
            data.spriteActor2
        );

        if (rectTransform != null)
        {
            Vector2 size = initialSize;

            if (data.restrictHeight > 0)
            {
                size.y = referenceHeight - data.restrictHeight;
            }

            rectTransform.sizeDelta = size;
        }

        UpdateArrows();
    }

    private bool ShouldDelayPopup()
    {
        bool focusPending = BoardNavigator.Instance != null && BoardNavigator.Instance.HasPendingFocus();
        return MessageDisplay.IsBusy() || MessageDisplayNoUI.IsBusy() || focusPending;
    }

    private void StartWaitForMessages()
    {
        if (waitForMessagesRoutine != null) return;
        waitForMessagesRoutine = StartCoroutine(WaitForMessages());
    }

    private IEnumerator WaitForMessages()
    {
        while (ShouldDelayPopup())
        {
            yield return null;
        }
        waitForMessagesRoutine = null;
        if (currentIndex == -1 && queue.Count > 0)
        {
            ShowEntry(0);
        }
        else
        {
            UpdateArrows();
        }
    }

    private void UpdateArrows()
    {
        bool hasQueue = queue.Count > 1;

        if (leftArrow != null) leftArrow.SetActive(hasQueue && currentIndex > 0);
        if (rightArrow != null) rightArrow.SetActive(hasQueue && currentIndex < queue.Count - 1);
    }

    private VideoClip GetVideoByName(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (videos == null) videos = FindFirstObjectByType<Videos>();
        return videos != null ? videos.GetVideoByName(name) : null;
    }

    private void SetActor2Active(bool isActive)
    {
        if(!isActive) actor2.sprite = actorReplacement; 
        actor2.gameObject.SetActive(true);
        actor2RawImage.gameObject.SetActive(true);
        actor2Video.gameObject.SetActive(true);
    }

    private static void SetActorVisuals(Image image, RawImage rawImage, VideoPlayer video, VideoClip clip, Sprite fallbackSprite)
    {
        bool hasClip = clip != null && video != null;

        if (video != null)
        {
            if (hasClip)
            {
                video.enabled = true;
                video.clip = clip;
                video.Play();
            }
            else
            {
                video.Stop();
                video.enabled = false;
            }
        }

        if (rawImage != null) rawImage.enabled = hasClip;

        if (image != null)
        {
            image.enabled = !hasClip;
            if (!hasClip)
            {                
                image.sprite = fallbackSprite? fallbackSprite: Instance.actorReplacement;
            }
        }
    }
}
