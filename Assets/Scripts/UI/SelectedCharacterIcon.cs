using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class SelectedCharacterIcon : MonoBehaviour
{
    [Header("Game Obejcts")]
    public GameObject actionsGameObject;
    public GameObject moved;
    public GameObject actioned;
    public GameObject unactionedIcon;
    public GameObject actionedIcon;

    [Header("Leader")]
    public Image icon;

    [Header("Health")]
    public Image health;

    [Header("Levels")]
    public TextMeshProUGUI commander;
    public TextMeshProUGUI agent;
    public TextMeshProUGUI emmissary;
    public TextMeshProUGUI mage;
    public TextMeshProUGUI movementLeft;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        icon = GetComponent<Image>();
        Hide();
    }

    // Update is called once per frame
    public void Refresh(Character c)
    {
        icon.enabled = true;
        actionsGameObject.SetActive(true);
        actioned.SetActive(true);
        moved.SetActive(true);
        icon.sprite = FindFirstObjectByType<IllustrationsSmall>().GetIllustrationByName(c.characterName);
        commander.text = c.commander.ToString();
        agent.text = c.agent.ToString();
        emmissary.text = c.emmissary.ToString();
        mage.text = c.mage.ToString();
        actionedIcon.SetActive(c.hasActionedThisTurn);
        unactionedIcon.SetActive(!actionedIcon.activeSelf);
        health.gameObject.SetActive(true);
        health.fillAmount = c.health / 100;

        RefreshMovementLeft(c);
    }


    // Update is called once per frame
    public void Hide()
    {
        icon.enabled = false;
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
