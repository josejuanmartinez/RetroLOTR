using UnityEngine.EventSystems;
using TMPro;

public class CharacterIconWithText: CharacterIcon, IPointerEnterHandler, IPointerExitHandler
{
    public TextMeshProUGUI characterText;
    

    public override void Initialize(Character character)
    {
        base.Initialize(character);
        if (characterText != null && character != null)
            characterText.text = character.characterName;
    }

    public void Initialize(Character character, string text)
    {
        base.Initialize(character);
        SetCharacterWithText(character, text);
    }

    new public void OnClick()
    {
        if (character == null || character.killed) return;

        if (board == null) board = FindFirstObjectByType<Board>();
        if (board != null)
        {
            Sounds.Instance?.PlayUiClick();
            board.SelectCharacter(character);
        }
    }

    new public void OnPointerEnter(PointerEventData eventData)
    {
        if (character == null || character.killed) return;
        board ??= FindFirstObjectByType<Board>();
        if (board != null && board.selectedCharacter == character) return;
        Sounds.Instance?.PlayUiHover();

        if (selectedCharacterIcon == null)
        {
            Layout layout = FindFirstObjectByType<Layout>();
            selectedCharacterIcon = layout != null ? layout.GetSelectedCharacterIcon() : null;
        }
        if (selectedCharacterIcon == null) return;

        selectedCharacterIcon.RefreshForHover(character);
    }

    new public void OnPointerExit(PointerEventData eventData)
    {
        if (selectedCharacterIcon == null)
        {
            Layout layout = FindFirstObjectByType<Layout>();
            selectedCharacterIcon = layout != null ? layout.GetSelectedCharacterIcon() : null;
        }
        if (selectedCharacterIcon == null) return;

        board ??= FindFirstObjectByType<Board>();
        if (board != null && board.selectedCharacter != null)
        {
            selectedCharacterIcon.Refresh(board.selectedCharacter);
        }
        else
        {
            selectedCharacterIcon.Hide();
        }
    }

    public void Refresh(Character updatedCharacter, string text = null)
    {
        string label = text ?? (updatedCharacter != null ? updatedCharacter.characterName : string.Empty);
        SetCharacterWithText(updatedCharacter, label);
    }

    private void SetCharacterWithText(Character newCharacter, string text)
    {
        SetCharacter(newCharacter);
        characterText.text = text;
    }
}
