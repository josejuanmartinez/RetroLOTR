using UnityEngine.EventSystems;
using TMPro;

public class CharacterIconWithTextAndRumour: CharacterIconWithText
{
    public TextMeshProUGUI rumourText;
    

    public new void Initialize(Character character, string text)
    {
        base.Initialize(character);
        rumourText.text = text;
    }

    
}
