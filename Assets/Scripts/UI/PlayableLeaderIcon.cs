using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.Playables;
using UnityEngine.Video;
using System;

public class PlayableLeaderIcon : MonoBehaviour
{
    public Image image;
    public VideoPlayer video;
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

    private VideoClip leaderClip = null;
    private Sprite leaderSprite = null;
    private string text = string.Empty;
    private bool initialized = false;
    private Videos videos;
    private VideoClip highlightedClip;
    private Illustrations illustrations;
    private Sprite highlightedSprite;

    public void Initialize(PlayableLeader leader)
    {
        playableLeader = leader;
        alignment = leader.alignment;
        if (illustrations == null) illustrations = FindFirstObjectByType<Illustrations>();
        if (videos == null) videos = FindFirstObjectByType<Videos>();
        leaderClip = videos != null ? videos.GetVideoByName(leader.characterName) : null;
        leaderSprite = illustrations != null ? illustrations.GetIllustrationByName(leader.characterName) : null;
        text = leader.GetHoverText(true, false, false, false, false, false);
        SetLeaderVisuals(leaderClip, leaderSprite);
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
        nonPlayableLeaderIcons.Instantiate(nonPlayableLeader, playableLeader);
    }

    public void HighlighNonPlayableLeader(string leaderName, string leaderText)
    {
        if (videos == null) videos = FindFirstObjectByType<Videos>();
        if (illustrations == null) illustrations = FindFirstObjectByType<Illustrations>();
        highlightedClip = videos != null ? videos.GetVideoByName(leaderName) : null;
        highlightedSprite = illustrations != null ? illustrations.GetIllustrationByName(leaderName) : null;
        SetLeaderVisuals(highlightedClip, highlightedSprite);
        textWidget.text = leaderText;
    }

    public void Restore(string leaderName)
    {
        if (videos == null) videos = FindFirstObjectByType<Videos>();
        if (illustrations == null) illustrations = FindFirstObjectByType<Illustrations>();
        VideoClip expectedClip = videos != null ? videos.GetVideoByName(leaderName) : null;
        Sprite expectedSprite = illustrations != null ? illustrations.GetIllustrationByName(leaderName) : null;
        bool restoreFromVideo = expectedClip != null && video != null && video.clip == expectedClip;
        bool restoreFromImage = expectedClip == null && image != null && image.sprite == expectedSprite;
        if (!restoreFromVideo && !restoreFromImage) return;

        SetLeaderVisuals(leaderClip, leaderSprite);
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

    private void SetLeaderVisuals(VideoClip clip, Sprite fallbackSprite)
    {
        bool hasClip = clip != null && video != null;

        if (video != null)
        {
            video.enabled = hasClip;
            if (hasClip)
            {
                video.clip = clip;
                video.Play();
            }
        }

        if (image != null)
        {
            image.enabled = !hasClip;
            if (!hasClip)
            {
                image.sprite = fallbackSprite;
            }
        }
    }

    public void ShowRumours()
    {
        FindFirstObjectByType<RumoursManager>().Show();
    }
}
