using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class PlayableLeaderIcon : MonoBehaviour
{
    public Image image;
    public CanvasGroup deadCanvasGroup;
    public TextMeshProUGUI joinedText;

    public void Initialize(Leader leader)
    {
        image.sprite = FindFirstObjectByType<IllustrationsSmall>().GetIllustrationByName(leader.characterName);
        joinedText.text = $"<mark=#ffffff>{leader.GetBiome().joinedText}</mark>";

        // Start the coroutine to hide the text after 6 seconds
        StartCoroutine(HideJoinedTextAfterDelay(6f));
    }

    private IEnumerator HideJoinedTextAfterDelay(float delay)
    {
        // Wait for the specified delay
        yield return new WaitForSeconds(delay);

        // Hide the text
        joinedText.gameObject.SetActive(false);
    }

    public void SetDead()
    {
        deadCanvasGroup.alpha = 1;
    }
}