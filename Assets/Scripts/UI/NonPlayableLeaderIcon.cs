using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.EventSystems;
using System.Linq;
using UnityEngine.InputSystem;

public class NonPlayableLeaderIcon : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Image image;
    public CanvasGroup deadCanvasGroup;
    public Image border;

    private NonPlayableLeader nonPlayableLeader;
    private AlignmentEnum alignment;
    private string text = string.Empty;

    public void Initialize(NonPlayableLeader leader)
    {
        nonPlayableLeader = leader;
        image.sprite = FindFirstObjectByType<Illustrations>().GetIllustrationByName(leader.characterName);
        alignment = leader.alignment;
        text = $"<mark=#ffffff>{leader.characterName}</mark>";
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        PlayableLeaderIcon leader = FindObjectsByType<PlayableLeaderIcon>(FindObjectsSortMode.None).First((x => x.alignment == alignment));
        if (leader) leader.HighlighNonPlayableLeader(image.sprite, text);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        PlayableLeaderIcon leader = FindObjectsByType<PlayableLeaderIcon>(FindObjectsSortMode.None).First((x => x.alignment == alignment));
        if (leader) leader.Restore(image.sprite);
    }

    public void SetDead()
    {
        deadCanvasGroup.alpha = 1;
    }
    public void SetHired()
    {
        border.color = Color.white;
    }
}