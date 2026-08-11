using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;

public class PlayableLeaderIcon : MonoBehaviour
{
    public Image image;
    public bool videoMode;
    public NonPlayableLeaderIcons nonPlayableLeaderIcons;
    public CanvasGroup deadCanvasGroup;
    // public TextMeshProUGUI joinedText;
    public TextMeshProUGUI textWidget;
    public Image alignmentImage;
    public Image border;
    public TextMeshProUGUI victoryPoints;
    public TextMeshProUGUI newRumoursText;

    [HideInInspector]
    public AlignmentEnum alignment;
    [HideInInspector]
    public PlayableLeader playableLeader;

    private Sprite leaderSprite = null;
    private string text = string.Empty;
    private bool initialized = false;
    private Illustrations illustrations;
    private Sprite highlightedSprite;

    public void Initialize(PlayableLeader leader)
    {
        playableLeader = leader;
        alignment = leader.alignment;
        if (illustrations == null) illustrations = FindFirstObjectByType<Illustrations>();
        leaderSprite = illustrations != null ? illustrations.GetCharacterArtByName(leader.characterName) : null;
        text = leader.GetHoverText(true, false, false, false, false, false);
        SetLeaderVisuals(leaderSprite);
        textWidget.text = text;
        // joinedText.text = $"<mark=#ffffff>{leader.GetBiome().joinedText}</mark>";

        alignmentImage.sprite = illustrations.GetIllustrationByName(leader.alignment.ToString());
        RefreshVictoryPoints(leader.victoryPoints != null ? leader.victoryPoints.RelativeScore : 0);
        RemoveCurrentlyPlayingEffect();
        RefreshNewRumoursCount();

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
        NonPlayableLeaderIcons icons = FindFirstObjectByType<NonPlayableLeaderIcons>();
        if (icons != null)
        {
            icons.Instantiate(nonPlayableLeader, playableLeader);
        }
    }

    public void HighlighNonPlayableLeader(string leaderName, string leaderText)
    {
        if (illustrations == null) illustrations = FindFirstObjectByType<Illustrations>();
        highlightedSprite = illustrations != null ? illustrations.GetCharacterArtByName(leaderName) : null;
        SetLeaderVisuals(highlightedSprite);
        textWidget.text = leaderText;
    }

    public void Restore(string leaderName)
    {
        if (illustrations == null) illustrations = FindFirstObjectByType<Illustrations>();
        Sprite expectedSprite = illustrations != null ? illustrations.GetCharacterArtByName(leaderName) : null;
        bool restoreFromImage = image != null && image.sprite == expectedSprite;
        if (!restoreFromImage) return;

        SetLeaderVisuals(leaderSprite);
        textWidget.text = text;
    }

    public void SetCurrentlyPlayingEffect()
    {
        image.color = new Color(image.color.r, image.color.g, image.color.b, 1.0f);
    }
    public void RemoveCurrentlyPlayingEffect()
    {
        image.color = new Color(image.color.r, image.color.g, image.color.b, 0.25f);
    }

    public void RefreshVictoryPoints(int points)
    {
        if (victoryPoints != null) victoryPoints.text = points.ToString();
        PlayableLeaderIcons icons = FindFirstObjectByType<PlayableLeaderIcons>();
        if (icons != null) icons.UpdateVictoryPointColors();
    }

    public void RefreshNewRumoursCount()
    {
        if (newRumoursText == null || playableLeader == null) return;
        int count = RumoursManager.GetUnseenRumoursCount(playableLeader);
        newRumoursText.text = Math.Max(count, 0).ToString();
    }

    private void SetLeaderVisuals(Sprite fallbackSprite)
    {        
        if (image != null)
        {
            image.enabled = true;
            image.sprite = fallbackSprite;
        }    
        
    }

    public void ShowRumours()
    {
        FindFirstObjectByType<RumoursManager>().Show(); 
    }
}
