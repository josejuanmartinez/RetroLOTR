using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System.Linq;
using System.Text;

public class NonPlayableLeaderIcon : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("References")]
    public GraphicRaycaster raycaster;
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
    private bool tempRevealQueued = false;

    public void Initialize(NonPlayableLeader leader)
    {
        game = FindFirstObjectByType<Game>();
        nonPlayableLeader = leader;
        leaderSprite = FindFirstObjectByType<Illustrations>().GetIllustrationByName(leader.characterName);
        alignment = leader.alignment;
        text = $"<sprite name=\"{alignment}\">{leader.characterName}";
        raycaster.enabled = false;
        canvasGroup.alpha = 0;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isUnrevealed || PopupManager.IsShowing) return;
        Sounds.Instance?.PlayUiHover();
        PlayableLeaderIcon leader = FindObjectsByType<PlayableLeaderIcon>(FindObjectsSortMode.None).First(x => x.alignment == alignment);
        if (leader) leader.HighlighNonPlayableLeader(nonPlayableLeader.characterName, text);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Sounds.Instance?.PlayUiExit();
        PlayableLeaderIcon leader = FindObjectsByType<PlayableLeaderIcon>(FindObjectsSortMode.None).First(x => x.alignment == alignment);
        if (leader) leader.Restore(nonPlayableLeader.characterName);
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
        if (player != null)
        {
            Hex capitalHex = nonPlayableLeader.controlledPcs.FirstOrDefault(pc => pc != null && pc.isCapital)?.hex;
            if (capitalHex != null)
            {
                bool playerCanSee = player.visibleHexes.Contains(capitalHex) && capitalHex.IsHexSeen();
                if (!playerCanSee)
                {
                    player.AddTemporarySeenHexes(new[] { capitalHex });
                    if (!tempRevealQueued)
                    {
                        tempRevealQueued = true;
                        StartCoroutine(RefreshVisibleNextFrame(player));
                    }
                }
            }
        }
        canvasGroup.alpha = 1;
        raycaster.enabled = true;
        image.sprite = leaderSprite;
        image.color = Color.white;
        string alignment = nonPlayableLeader.alignment == AlignmentEnum.freePeople ? "a free people" : nonPlayableLeader.alignment == AlignmentEnum.darkServants ? "a dark servant" : "a neutral";
        StringBuilder sb = new($"You discovered {nonPlayableLeader.characterName}, {alignment} nation");
        sb.Append("<br><br>");
        bool hasHiddenCapital = nonPlayableLeader.controlledPcs.Any(pc => pc.isHidden && !pc.hiddenButRevealed);
        if (hasHiddenCapital)
        {
            sb.Append("We found their nation but cannot find a way into their capital. Issue `Reveal PC` to possibly reveal a path.<br><br>");
        }
        if (nonPlayableLeader.alignment == game.currentlyPlaying.alignment || nonPlayableLeader.alignment == AlignmentEnum.neutral)
        {
            sb.Append("They can join your side; neutral nations will work with any alignment, others require a match.<br><br>");
            sb.Append(nonPlayableLeader.GetJoiningConditionsText(game.currentlyPlaying));
            sb.Append("<br><br>");
        }
        if (nonPlayableLeader.alignment != game.currentlyPlaying.alignment && nonPlayableLeader.alignment != AlignmentEnum.neutral)
        {
            sb.Append("You can attack to weaken their forces.");
        }
        PopupManager.Show(
            $"{nonPlayableLeader.characterName} reveals themselves!",
            FindFirstObjectByType<Illustrations>().GetIllustrationByName(player.characterName),
            FindFirstObjectByType<Illustrations>().GetIllustrationByName(nonPlayableLeader.characterName),
            sb.ToString(),
            true
        );
        isUnrevealed = false;
    }

    private IEnumerator RefreshVisibleNextFrame(PlayableLeader player)
    {
        yield return null;
        if (player != null) player.RefreshVisibleHexesImmediate();
        tempRevealQueued = false;
    }
}
