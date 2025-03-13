using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class SelectedCharacterIcon : MonoBehaviour
{
    public GameObject actionsGameObject;
    public Image icon;
    public TextMeshProUGUI commander;
    public TextMeshProUGUI agent;
    public TextMeshProUGUI emmissary;
    public TextMeshProUGUI mage;
    public TextMeshProUGUI movementLeft;
    public GameObject moved;
    public GameObject actioned;
    public GameObject unactionedIcon;
    public GameObject actionedIcon;

    private Game game;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        game = FindFirstObjectByType<Game>();
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

        RefreshMovementLeft(c);
    }


    // Update is called once per frame
    public void Hide()
    {
        icon.enabled = false;
        actionsGameObject.SetActive(false);
        actioned.SetActive(false);
        moved.SetActive(false);
    }

    public void RefreshMovementLeft(Character c)
    {
        movementLeft.text = c.GetMovementLeft().ToString();
    }
}
