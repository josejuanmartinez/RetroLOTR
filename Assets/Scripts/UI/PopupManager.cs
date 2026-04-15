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
    private CanvasGroup containerCanvasGroup;
    private Vector2 initialSize;
    public static bool IsShowing { get; private set; }
    // private Videos videos;
    private Coroutine waitForMessagesRoutine;
    // private Coroutine actorPlaybackSequenceRoutine;
    // private Coroutine popupDisplayRoutine;
    // private int actorPlaybackToken;
    // private int popupDisplayToken;

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

        if (container != null && !container.activeSelf)
        {
            container.SetActive(true);
        }

        rectTransform = container.GetComponent<RectTransform>();
        containerCanvasGroup = container != null ? container.GetComponent<CanvasGroup>() : null;
        if (container != null && containerCanvasGroup == null)
        {
            containerCanvasGroup = container.AddComponent<CanvasGroup>();
        }
        initialSize = rectTransform != null ? rectTransform.sizeDelta : Vector2.zero;
        IsShowing = false;
        SetContainerVisible(false);
    }

    public void Initialize(string title, Sprite spriteActor1, Sprite spriteActor2, string text, bool typeWrite, int restrictHeight = 0, Action onClose = null)
        => InitializeInternal(title, spriteActor1, spriteActor2, text, typeWrite, restrictHeight, onClose, false);

    private void InitializeInternal(string title, Sprite spriteActor1, Sprite spriteActor2, string text, bool typeWrite, int restrictHeight = 0, Action onClose = null, bool immediate = false)
    {
        FindFirstObjectByType<Game>()?.NotifyStartupPopupShown();

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
            if (!immediate && ShouldDelayPopup())
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
        // Video popup flow disabled for now; static portraits only.
        // popupDisplayToken++;
        // StopPopupDisplayRoutine();
        // actorPlaybackToken++;
        // StopActorPlaybackSequence();
        Action onClose = null;
        if (currentIndex >= 0 && currentIndex < queue.Count)
        {
            onClose = queue[currentIndex].onClose;
        }

        FindFirstObjectByType<Sounds>().StopAllSounds();
        Music.Instance?.StopEventMusic();
        SetContainerVisible(false);
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

        SetActorVisuals(actor1, actor1RawImage, actor1Video, null);
        SetActorVisuals(actor2, actor2RawImage, actor2Video, null);

        ActionsManager actionsManager = FindFirstObjectByType<ActionsManager>();
        if (actionsManager != null)
        {
            actionsManager.RefreshInteractableState();
        }

        FindFirstObjectByType<Game>()?.NotifyStartupPopupClosed();
        onClose?.Invoke();
    }

    public static void Show(string title, Sprite spriteActor1, Sprite spriteActor2, string text, bool typeWrite, int restrictHeight = 0, Action onClose = null)
    {
        ShowWithIconType(EventIconType.Story, title, spriteActor1, spriteActor2, text, typeWrite, restrictHeight, onClose);
    }

    public static void ShowImmediate(string title, Sprite spriteActor1, Sprite spriteActor2, string text, bool typeWrite, int restrictHeight = 0, Action onClose = null)
    {
        if (Instance == null) return;
        Instance.InitializeInternal(title, spriteActor1, spriteActor2, text, typeWrite, restrictHeight, onClose, true);
    }

    public static void ShowWithIconType(EventIconType iconType, string title, Sprite spriteActor1, Sprite spriteActor2, string text, bool typeWrite, int restrictHeight = 0, Action onClose = null)
    {
        if (Instance == null) return;

        EventIconsManager iconsManager = EventIconsManager.FindManager();
        if (iconsManager == null)
        {
            Instance.Initialize(title, spriteActor1, spriteActor2, text, typeWrite, restrictHeight, onClose);
            return;
        }

        EventIcon icon = null;
        Action wrappedClose = () =>
        {
            onClose?.Invoke();
            icon?.ConsumeAndDestroy();
        };

        icon = iconsManager.AddEventIcon(
            iconType,
            true,
            () => Instance.Initialize(title, spriteActor1, spriteActor2, text, typeWrite, restrictHeight, wrappedClose));
    }

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

        // Video popup flow disabled for now; static portraits only.
        // popupDisplayToken++;
        // StopPopupDisplayRoutine();
        // actorPlaybackToken++;
        // StopActorPlaybackSequence();
        currentIndex = index;
        PopupData data = queue[currentIndex];
        Sounds.Instance?.PlayMessage();
        Music.Instance?.PlayEventMusic();

        // StopActorVideo(actor1Video);
        // StopActorVideo(actor2Video);
        // ClearActorOutput(actor1RawImage, actor1Video);
        // ClearActorOutput(actor2RawImage, actor2Video);

        bool hasActor2 = data.spriteActor2 != null;
        SetActor2Active(hasActor2);
        SetActorVisuals(actor1, actor1RawImage, actor1Video, data.spriteActor1);
        SetActorVisuals(actor2, actor2RawImage, actor2Video, data.spriteActor2);
        ShowContainer();
        ApplyPopupTextAndTitle(data);

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

    // private void StopPopupDisplayRoutine()
    // {
    //     if (popupDisplayRoutine == null) return;
    //     StopCoroutine(popupDisplayRoutine);
    //     popupDisplayRoutine = null;
    // }

    private bool ShouldDelayPopup()
    {
        bool focusPending = BoardNavigator.Instance != null && BoardNavigator.Instance.HasPendingFocus();
        return MessageDisplay.IsDisplaying() || MessageDisplayNoUI.IsBusy() || focusPending;
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

    // private VideoClip GetVideoByName(string name)
    // {
    //     if (string.IsNullOrEmpty(name)) return null;
    //     if (videos == null) videos = FindFirstObjectByType<Videos>();
    //     return videos != null ? videos.GetVideoByName(name) : null;
    // }

    private void SetActor2Active(bool isActive)
    {
        if(!isActive) actor2.sprite = actorReplacement; 
        actor2.gameObject.SetActive(true);
        if (actor2RawImage != null) actor2RawImage.gameObject.SetActive(false);
        if (actor2Video != null) actor2Video.gameObject.SetActive(false);
    }

    // private void StopActorPlaybackSequence()
    // {
    //     if (actorPlaybackSequenceRoutine == null) return;
    //     StopCoroutine(actorPlaybackSequenceRoutine);
    //     actorPlaybackSequenceRoutine = null;
    // }

    // private IEnumerator PlayRightActorAfterLeftCompletes(int tokenSnapshot, VideoClip leftClip, Sprite leftFallbackSprite, VideoClip rightClip, Sprite rightFallbackSprite)
    // {
    //     Video popup flow disabled for now.
    //     yield break;
    // }

    // private IEnumerator PrepareAndShowDualVideoEntry(int displayTokenSnapshot, VideoClip leftClip, Sprite leftFallbackSprite, VideoClip rightClip, Sprite rightFallbackSprite)
    // {
    //     Video popup flow disabled for now.
    //     yield break;
    // }

    // private IEnumerator PrepareAndShowSingleStateEntry(int displayTokenSnapshot, VideoClip leftClip, Sprite leftFallbackSprite, VideoClip rightClip, Sprite rightFallbackSprite)
    // {
    //     Video popup flow disabled for now.
    //     yield break;
    // }

    // private IEnumerator PrepareVideo(VideoPlayer video, RawImage rawImage, VideoClip clip)
    // {
    //     Video popup flow disabled for now.
    //     yield break;
    // }

    // private void ApplyPreparedActorState(Image image, RawImage rawImage, VideoPlayer video, VideoClip clip, Sprite fallbackSprite)
    // {
    // }

    // private void PlayPreparedVideo(Image image, RawImage rawImage, VideoPlayer video, VideoClip clip, Sprite fallbackSprite)
    // {
    // }

    private void ShowContainer()
    {
        SetContainerVisible(true);
        IsShowing = true;
    }

    private void SetContainerVisible(bool visible)
    {
        if (container == null) return;
        if (!container.activeSelf)
        {
            container.SetActive(true);
        }

        if (containerCanvasGroup != null)
        {
            containerCanvasGroup.alpha = visible ? 1f : 0f;
            containerCanvasGroup.interactable = visible;
            containerCanvasGroup.blocksRaycasts = visible;
        }
    }

    private void ApplyPopupTextAndTitle(PopupData data)
    {
        if (typeWriterEffect != null)
        {
            if (data.typeWrite)
            {
                typeWriterEffect.enabled = true;
                typeWriterEffect.fullText = data.text;
                textWidget.text = "";
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

        if (typeWriterEffect != null && data.typeWrite)
        {
            typeWriterEffect.StartWriting();
        }
    }

    // private static void StopActorVideo(VideoPlayer video)
    // {
    //     if (video == null) return;
    //     video.Stop();
    //     video.enabled = false;
    // }

    // private static void ClearActorOutput(RawImage rawImage, VideoPlayer video)
    // {
    //     if (rawImage != null)
    //     {
    //         rawImage.enabled = false;
    //     }
    //
    //     RenderTexture target = video != null ? video.targetTexture : null;
    //     if (target == null) return;
    //
    //     RenderTexture previous = RenderTexture.active;
    //     RenderTexture.active = target;
    //     GL.Clear(true, true, Color.clear);
    //     RenderTexture.active = previous;
    // }

    private static void SetActorVisuals(Image image, RawImage rawImage, VideoPlayer video, Sprite fallbackSprite)
    {
        // Video popup flow disabled for now; static portraits only.
        if (video != null)
        {
            video.Stop();
            video.enabled = false;
        }

        if (rawImage != null) rawImage.enabled = false;

        if (image != null)
        {
            image.enabled = true;
            image.sprite = fallbackSprite ? fallbackSprite : Instance.actorReplacement;
        }
    }
}
