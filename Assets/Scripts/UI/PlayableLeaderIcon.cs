using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.Playables;

public class PlayableLeaderIcon : MonoBehaviour
{
    public Image image;
    public NonPlayableLeaderIcons nonPlayableLeaderIcons;
    public CanvasGroup deadCanvasGroup;
    // public TextMeshProUGUI joinedText;
    public TextMeshProUGUI textWidget;
    public Image alignmentImage;
    public Image border;
    public TextMeshProUGUI victoryPoints;

    [HideInInspector]
    public AlignmentEnum alignment;
    [HideInInspector]
    public PlayableLeader playableLeader;

    private Sprite sprite = null;
    private string text = string.Empty;
    private bool initialized = false;

    public void Initialize(PlayableLeader leader)
    {
        playableLeader = leader;
        alignment = leader.alignment;
        Illustrations illustrations = FindFirstObjectByType<Illustrations>();
        sprite = illustrations.GetIllustrationByName(leader.characterName);
        text = leader.GetHoverText(true, false, false, false, false, false);
        image.sprite = sprite;
        textWidget.text = text;
        // joinedText.text = $"<mark=#ffffff>{leader.GetBiome().joinedText}</mark>";

        alignmentImage.sprite = illustrations.GetIllustrationByName(leader.alignment.ToString());
        RefreshVictoryPoints(leader.victoryPoints != null ? leader.victoryPoints.RelativeScore : 0);

        // Start the coroutine to hide the text after 6 seconds
        // StartCoroutine(HideJoinedTextAfterDelay(6f));
        
        initialized = true;
    }

    public bool IsInitialized() => initialized;

    /*private IEnumerator HideJoinedTextAfterDelay(float delay)
    {
        // Wait for the specified delay
        yield return new WaitForSeconds(delay);

        // Hide the text
        // joinedText.gameObject.SetActive(false);
    }*/

    public void SetDead()
    {
        deadCanvasGroup.alpha = 1;
    }

    public void AddNonPlayableLeader(NonPlayableLeader nonPlayableLeader)
    {
        nonPlayableLeaderIcons.Instantiate(nonPlayableLeader, playableLeader);
    }

    public void HighlighNonPlayableLeader(Sprite leader, string leaderText)
    {
        image.sprite = leader;
        textWidget.text = leaderText;
    }

    public void Restore(Sprite leader)
    {
        if (image.sprite == leader)
        {
            image.sprite = sprite;
            textWidget.text = text;
        }
    }

    public void SetCurrentlyPlayingEffect()
    {
        border.color = Color.white;
    }
    public void RemoveCurrentlyPlayingEffect()
    {
        border.color = Color.black;
    }

    public void RefreshVictoryPoints(int points)
    {
        if (victoryPoints != null) victoryPoints.text = points.ToString();
    }
}
