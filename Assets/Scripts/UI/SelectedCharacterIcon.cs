using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class SelectedCharacterIcon : MonoBehaviour
{
    [Header("Game Objects")]
    public GameObject actionsGameObject;
    public GameObject moved;
    public GameObject actioned;
    public GameObject unactionedIcon;
    public GameObject actionedIcon;
    public GameObject border;

    [Header("Leader")]
    public Image icon;
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
    public TextMeshProUGUI artifactsText;
    public TextMeshProUGUI artifactsHoverText;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        icon = GetComponent<Image>();
        Hide();
    }

    // Update is called once per frame
    public void Refresh(Character c)
    {
        border.SetActive(true);
        icon.enabled = true;
        alignmentIcon.enabled = true;
        alignmentIcon.sprite = FindFirstObjectByType<Illustrations>().GetIllustrationByName(c.GetAlignment().ToString());
        textWidget.text = $"<mark=#ffffff>{c.GetHoverText(false, false, false, true, false)}</mark>";
        actionsGameObject.SetActive(true);
        actioned.SetActive(true);
        moved.SetActive(true);
        icon.sprite = FindFirstObjectByType<Illustrations>().GetIllustrationByName(c);
        commander.text = c.GetCommander().ToString();
        agent.text = c.GetAgent().ToString();
        emmissary.text = c.GetEmmissary().ToString();
        mage.text = c.GetMage().ToString();
        actionedIcon.SetActive(c.hasActionedThisTurn);
        unactionedIcon.SetActive(!actionedIcon.activeSelf);
        health.gameObject.SetActive(true);
        health.fillAmount = c.health / 100;

        artifactsText.text = "";
        artifactsHoverText.text = "";

        c.artifacts.ForEach(x =>
        {
            string spriteText = $"<sprite name=\"{x.GetSpriteString()}\">";
            artifactsText.text += spriteText;
            artifactsHoverText.text += $"{spriteText}{x.artifactName}{x.artifactDescription}<br>";
        });
        
        RefreshMovementLeft(c);
    }


    // Update is called once per frame
    public void Hide()
    {
        border.SetActive(false);
        alignmentIcon.enabled = false;
        icon.enabled = false;
        textWidget.text = "";
        actionsGameObject.SetActive(false);
        actioned.SetActive(false);
        moved.SetActive(false);
        health.gameObject.SetActive(false);
    }

    public void RefreshMovementLeft(Character c)
    {
        movementLeft.text = c.GetMovementLeft().ToString();
    }
}
