using UnityEngine;

public class Layout : MonoBehaviour
{
    [SerializeField]
    private SelectedCharacterIcon selectedCharacterIcon;
    [SerializeField]
    private ActionsManager actionsManager;
    [SerializeField]
    private HexNumberManager hexNumberManager;

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
