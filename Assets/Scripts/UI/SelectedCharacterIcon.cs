using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

[RequireComponent(typeof(Image))]
public class SelectedCharacterIcon : MonoBehaviour
{
    [Header("Game Objects")]
    public GameObject levelsGameObject;
    public GameObject moved;
    public GameObject actioned;
    public GameObject unactionedIcon;
    public GameObject actionedIcon;
    public GameObject border;

    [Header("Leader")]
    public Image icon;
    public RawImage rawImage;
    public VideoPlayer video;
    public TextMeshProUGUI textWidget;
    public Image alignmentIcon;

    [Header("Health")]
    public Image health;

    [Header("Levels")]
    public TextMeshProUGUI commander;
    public TextMeshProUGUI agent;
    public TextMeshProUGUI emmissary;
    public TextMeshProUGUI mage;
    public TextMeshProUGUI movementLeft;

    [Header("Artifacts")]
    public GameObject artifactPrefab;
    public Transform artifactsGridLayoutTransform;

    private Videos videos;
    private Illustrations illustrations;

    // Update is called once per frame
    public void Refresh(Character c)
    {
        border.SetActive(true);
        SetCharacterVisuals(
            GetVideoByName(c.characterName),
            GetIllustrationByName(c.characterName)
        );
        alignmentIcon.enabled = true;
        alignmentIcon.sprite = GetIllustrationByName(c.GetAlignment().ToString());
        textWidget.text = $"{c.GetHoverText(true, false, false, true, false, false)}";
        levelsGameObject.SetActive(true);
        actioned.SetActive(true);
        moved.SetActive(true);
        commander.text = c.GetCommander().ToString();
        agent.text = c.GetAgent().ToString();
        emmissary.text = c.GetEmmissary().ToString();
        mage.text = c.GetMage().ToString();
        actionedIcon.SetActive(c.hasActionedThisTurn);
        unactionedIcon.SetActive(!actionedIcon.activeSelf);
        health.gameObject.SetActive(true);
        health.fillAmount = c.health / 100;

        foreach (Transform artifactChild in artifactsGridLayoutTransform)
        {
            Destroy(artifactChild.gameObject);
        }

        c.artifacts.ForEach(x =>
        {
            GameObject artifactGO = Instantiate(artifactPrefab, artifactsGridLayoutTransform);
            artifactGO.name = x.artifactName;
            artifactGO.GetComponent<ArtifactRenderer>().Initialize(x);
        });
        
        RefreshMovementLeft(c);
    }

    public void RefreshHoverPreview(Character c, string hoverText, bool showHealth, bool showArtifacts)
    {
        if (c == null)
        {
            Hide();
            return;
        }

        border.SetActive(true);
        SetCharacterVisuals(
            GetVideoByName(c.characterName),
            GetIllustrationByName(c.characterName)
        );
        alignmentIcon.enabled = true;
        alignmentIcon.sprite = GetIllustrationByName(c.GetAlignment().ToString());
        textWidget.text = hoverText ?? "";

        actioned.SetActive(false);
        moved.SetActive(false);
        actionedIcon.SetActive(false);
        unactionedIcon.SetActive(false);

        commander.text = c.GetCommander().ToString();
        agent.text = c.GetAgent().ToString();
        emmissary.text = c.GetEmmissary().ToString();
        mage.text = c.GetMage().ToString();
        movementLeft.text = "-";

        health.gameObject.SetActive(showHealth);
        if (showHealth) health.fillAmount = c.health / 100f;

        foreach (Transform artifactChild in artifactsGridLayoutTransform)
        {
            Destroy(artifactChild.gameObject);
        }

        if (showArtifacts)
        {
            c.artifacts.ForEach(x =>
            {
                GameObject artifactGO = Instantiate(artifactPrefab, artifactsGridLayoutTransform);
                artifactGO.name = x.artifactName;
                artifactGO.GetComponent<ArtifactRenderer>().Initialize(x);
            });
        }
    }


    // Update is called once per frame
    public void Hide()
    {
        border.SetActive(false);
        alignmentIcon.enabled = false;
        if (video != null)
        {
            video.Stop();
            video.enabled = false;
        }
        if (rawImage != null) rawImage.enabled = false;
        Image targetImage = GetImageTarget();
        if (targetImage != null) targetImage.enabled = false;
        textWidget.text = "";
        levelsGameObject.SetActive(false);
        actioned.SetActive(false);
        moved.SetActive(false);
        health.gameObject.SetActive(false);
    }

    public void RefreshMovementLeft(Character c)
    {
        movementLeft.text = c.GetMovementLeft().ToString();
    }

    private Sprite GetIllustrationByName(string name)
    {
        if (illustrations == null) illustrations = FindFirstObjectByType<Illustrations>();
        return illustrations != null ? illustrations.GetIllustrationByName(name) : null;
    }

    private VideoClip GetVideoByName(string name)
    {
        if (videos == null) videos = FindFirstObjectByType<Videos>();
        return videos != null ? videos.GetVideoByName(name) : null;
    }

    private Image GetImageTarget()
    {
        return icon;
    }

    private void SetCharacterVisuals(VideoClip clip, Sprite fallbackSprite)
    {
        bool hasClip = clip != null && video != null;

        if (video != null)
        {
            if (hasClip)
            {
                video.enabled = true;
                video.clip = clip;
                video.Play();
            }
            else
            {
                video.Stop();
                video.enabled = false;
            }
        }

        if (rawImage != null) rawImage.enabled = hasClip;

        Image targetImage = GetImageTarget();
        if (targetImage != null)
        {
            targetImage.enabled = !hasClip;
            if (!hasClip)
            {
                targetImage.sprite = fallbackSprite;
            }
        }
    }
}
