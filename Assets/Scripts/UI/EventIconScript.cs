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
    HexMessage,
    Discovery
}

public class EventIcon : MonoBehaviour, IPointerClickHandler
{
    public Sprite storySprite;
    public Sprite tutorialSprite;
    public Sprite yesnoPopupSprite;
    public Sprite multichoiceSprite;
    public Sprite encounterSprite;
    public Sprite hexMessageSprite;
    public Sprite discoverySprite;

    private Action onOpenAction;
    private Action onRemoveAction;
    private bool discardable;

    private Image eventImage;

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

    public void Configure(EventIconType type, bool isDiscardable, Action onOpen, Action onRemove = null, Sprite characterPortrait = null)
    {
        onOpenAction = onOpen;
        onRemoveAction = onRemove;
        discardable = isDiscardable;

        if (eventImage != null)
        {
            eventImage.sprite = characterPortrait != null ? characterPortrait : GetSprite(type);
            eventImage.enabled = eventImage.sprite != null;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null) return;

        if (eventData.button == PointerEventData.InputButton.Left)
        {
            onOpenAction?.Invoke();
        }

        if (eventData.button == PointerEventData.InputButton.Right && discardable)
        {
            ConsumeAndDestroy();
        }
    }

    public void ConsumeAndDestroy()
    {
        onRemoveAction?.Invoke();
        onRemoveAction = null;
        Destroy(gameObject);
    }

    private Sprite GetSprite(EventIconType type)
    {
        return type switch
        {
            EventIconType.Story => storySprite,
            EventIconType.Tutorial => tutorialSprite,
            EventIconType.YesNo => yesnoPopupSprite,
            EventIconType.MultiChoice => multichoiceSprite,
            EventIconType.Encounter => encounterSprite,
            EventIconType.HexMessage => hexMessageSprite,
            EventIconType.Discovery => discoverySprite,
            _ => null
        };
    }
}
