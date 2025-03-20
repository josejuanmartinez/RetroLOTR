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

    [Header("Artifacts")]
    public GridLayoutGroup artifactsGrid;
    public GameObject artifactPrefab;

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
        commander.text = c.GetCommander().ToString();
        agent.text = c.GetAgent().ToString();
        emmissary.text = c.GetEmmissary().ToString();
        mage.text = c.GetMage().ToString();
        actionedIcon.SetActive(c.hasActionedThisTurn);
        unactionedIcon.SetActive(!actionedIcon.activeSelf);
        health.gameObject.SetActive(true);
        health.fillAmount = c.health / 100;

        foreach (Transform child in artifactsGrid.transform)
        {
            Destroy(child.gameObject);
        }

        c.artifacts.ForEach(x =>
        {
            GameObject artifact = Instantiate(artifactPrefab, artifactsGrid.transform);
            artifact.name = x.artifactName;
            artifact.GetComponentInChildren<Hover>().Initialize($"{x.artifactName}( {x.artifactDescription} )", Vector2.one * -25, 35, TextAlignmentOptions.Left);
            //artifact.GetComponent<Image>().sprite = FindFirstObjectByType<IllustrationsSmall>().GetIllustrationByName(x.artifactName);
        });

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
