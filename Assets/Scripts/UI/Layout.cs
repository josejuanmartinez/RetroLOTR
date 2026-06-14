using UnityEngine;

public class Layout : MonoBehaviour
{
    [SerializeField]
    private SelectedCharacterIcon selectedCharacterIcon;
    [SerializeField]
    private ActionsManager actionsManager;
    [SerializeField]
    private HexNumberManager hexNumberManager;
    [SerializeField]
    private Card environmentalCard;

    private void Awake()
    {
        if (environmentalCard != null) environmentalCard.gameObject.SetActive(false);
    }

    public void SetEnvironmentalCard(CardData card)
    {
        if (environmentalCard == null) return;
        if (card == null) { environmentalCard.SetEnvironmentalPulse(false); environmentalCard.gameObject.SetActive(false); return; }
        environmentalCard.gameObject.SetActive(true);
        environmentalCard.Initialize(card);
        environmentalCard.ShowEnvironmentalSprite();
        environmentalCard.SetEnvironmentalPulse(true);
    }

    public SelectedCharacterIcon GetSelectedCharacterIcon()
    {
        selectedCharacterIcon.gameObject.SetActive(true);
        return selectedCharacterIcon;
    }

    public ActionsManager GetActionsManager()
    {
        actionsManager.gameObject.SetActive(true);
        return actionsManager;
    }

    public HexNumberManager GetHexNumberManager()
    {
        return hexNumberManager;
    }
}
