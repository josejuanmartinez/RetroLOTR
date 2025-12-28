using UnityEngine.EventSystems;
using TMPro;

public class CharacterIconWithText: CharacterIcon, IPointerEnterHandler, IPointerExitHandler
{
    public TextMeshProUGUI characterText;
    

    public void Initialize(Character character, string text)
    {
        board = FindFirstObjectByType<Board>();
        selectedCharacterIcon = FindFirstObjectByType<SelectedCharacterIcon>();
        SetCharacterWithText(character, text);
    }

    new public void OnClick()
    {
        if (character == null || character.killed) return;

        if (board == null) board = FindFirstObjectByType<Board>();
        if (board != null)
        {
            Sounds.Instance?.PlayUiClick();
            if (character.hex != null)
            {
                character.hex.LookAt();
            }
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

        selectedCharacterIcon.Refresh(character);
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

    public void Refresh(Character updatedCharacter, string text)
    {
        SetCharacterWithText(updatedCharacter, text);
    }

    private void SetCharacterWithText(Character newCharacter, string text)
    {
        SetCharacter(newCharacter);
        characterText.text = text;
    }
}
