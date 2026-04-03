using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum EventIconType
{
    Story,
    Tutorial,
    YesNo,
    MultiChoice,
    Encounter,
    HexMessage
}

public class EventIcon : MonoBehaviour, IPointerClickHandler
{
    public Sprite storySprite;
    public Sprite tutorialSprite;
    public Sprite yesnoPopupSprite;
    public Sprite multichoiceSprite;
    public Sprite encounterSprite;
    public Sprite hexMessageSprite;

    [SerializeField] private Image eventImage;

    private Action openAction;
    private Action removeAction;
    private bool discardable;

    private void Awake()
    {
        if (eventImage == null)
        {
            Image[] images = GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] != null && string.Equals(images[i].gameObject.name, "EventImage", StringComparison.OrdinalIgnoreCase))
                {
                    eventImage = images[i];
                    break;
                }
            }
        }
    }

    public void Configure(EventIconType type, bool isDiscardable, Action onOpen, Action onRemove = null)
    {
        discardable = isDiscardable;
        openAction = onOpen;
        removeAction = onRemove;

        if (eventImage != null)
        {
            eventImage.sprite = GetSprite(type);
            eventImage.enabled = eventImage.sprite != null;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null) return;

        if (eventData.button == PointerEventData.InputButton.Left)
        {
            openAction?.Invoke();
            return;
        }

        if (eventData.button == PointerEventData.InputButton.Right && discardable)
        {
            removeAction?.Invoke();
            Destroy(gameObject);
        }
    }

    public void ConsumeAndDestroy()
    {
        removeAction?.Invoke();
        Destroy(gameObject);
    }

    private Sprite GetSprite(EventIconType type)
    {
        return type switch
        {
            EventIconType.Tutorial => tutorialSprite,
            EventIconType.YesNo => yesnoPopupSprite,
            EventIconType.MultiChoice => multichoiceSprite,
            EventIconType.Encounter => encounterSprite,
            EventIconType.HexMessage => hexMessageSprite,
            _ => storySprite
        };
    }
}
