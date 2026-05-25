using System;
using System.Reflection;
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
    Discovery,
    MapReveal,
    CaravanArrival,
    LeaderRevealed
}

public class EventIcon : MonoBehaviour, IPointerClickHandler
{
    public Image characterImage;
    public Image eventImage;
    public Sprite storySprite;
    public Sprite tutorialSprite;
    public Sprite yesnoPopupSprite;
    public Sprite multichoiceSprite;
    public Sprite encounterSprite;
    public Sprite hexMessageSprite;
    public Sprite discoverySprite;
    public Sprite mapRevealSprite;
    public Sprite caravanArrivalSprite;
    public Sprite leaderRevealedSprite;

    private Action onOpenAction;
    private Action onRemoveAction;
    private bool discardable;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        EnsureClickRouting();
    }

    private void EnsureClickRouting()
    {
        if (button != null)
        {
            button.interactable = true;
            if (button.image is not null)
                button.image.raycastTarget = true;
        }

        if (eventImage is not null) eventImage.raycastTarget = true;
        if (characterImage is not null) characterImage.raycastTarget = false;
    }

    public void Configure(EventIconType type, bool isDiscardable, Action onOpen, Action onRemove = null, Sprite characterPortrait = null)
    {
        onOpenAction = onOpen;
        onRemoveAction = onRemove;
        discardable = isDiscardable;

        if (eventImage != null)
        {
            eventImage.sprite = GetSprite(type);
            eventImage.enabled = eventImage.sprite != null;
        }

        if (characterImage != null)
        {
            characterImage.sprite = characterPortrait;
            characterImage.enabled = characterPortrait != null;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null) return;

        if (eventData.button == PointerEventData.InputButton.Left)
        {
            HandleOpenClicked();
            if (discardable)
            {
                ConsumeAndDestroy();
            }
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
        onOpenAction = null;
        Destroy(gameObject);
    }

    private void HandleOpenClicked()
    {
        onOpenAction?.Invoke();
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
            EventIconType.MapReveal => mapRevealSprite,
            EventIconType.CaravanArrival => caravanArrivalSprite,
            EventIconType.LeaderRevealed => leaderRevealedSprite,
            _ => null
        };
    }
}
