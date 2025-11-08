using TMPro;
using UnityEngine;

public class MovementCostManager : MonoBehaviour
{
    public TextMeshPro movementText;

    public void ShowMovementLeft(int movementLeft, Character character)
    {
        string spr = "movement";
        if(character.IsArmyCommander()) spr = character.GetAlignment().ToString();
        movementText.text = $"<sprite name=\"{spr}\"/>{movementLeft}";
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

}
