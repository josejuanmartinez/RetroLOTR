using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Linq;

public class NonPlayableLeaderIcon : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public CanvasGroup canvasGroup;
    public Image image;
    public CanvasGroup deadCanvasGroup;
    public Image border;
    
    [HideInInspector]
    public NonPlayableLeader nonPlayableLeader;
    
    private AlignmentEnum alignment;
    private string text = string.Empty;
    private bool isUnrevealed = true;

    private Game game;

    private Sprite leaderSprite;

    public void Initialize(NonPlayableLeader leader)
    {
        game = FindFirstObjectByType<Game>();
        nonPlayableLeader = leader;
        leaderSprite = FindFirstObjectByType<Illustrations>().GetIllustrationByName(leader.characterName);
        alignment = leader.alignment;
        text = $"<mark=#ffffff>{leader.characterName}</mark>";
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isUnrevealed) return;
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
    public void RevealToPlayer()
    {
        if (!isUnrevealed || !game.IsPlayerCurrentlyPlaying()) return;
        PlayableLeader player = game.player;
        canvasGroup.alpha = 1;
        image.sprite = leaderSprite;
        image.color = Color.white;
        string alignment = nonPlayableLeader.alignment == AlignmentEnum.freePeople ? "a free people" : nonPlayableLeader.alignment == AlignmentEnum.darkServants ? "a dark servant" : "a neutral";
        PopupManager.Show(
            $"{nonPlayableLeader.characterName} reveals themselves!",
            FindFirstObjectByType<Illustrations>().GetIllustrationByName(player.characterName),
            FindFirstObjectByType<Illustrations>().GetIllustrationByName(nonPlayableLeader.characterName),
            $"You discovered {nonPlayableLeader.characterName}, {alignment} nation",
            true
        );
        isUnrevealed = false;
    }
}