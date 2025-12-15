using UnityEngine;
using UnityEngine.UI;

public class CharacterIcon : MonoBehaviour
{
    public CanvasGroup deadCanvasGroup;
    public Image image;
    public Image healthBar;
    private Character character;
    private Board board;
    private SelectedCharacterIcon selectedCharacterIcon;

    

    public void Initialize(Character character)
    {
        board = FindFirstObjectByType<Board>();
        selectedCharacterIcon = FindFirstObjectByType<SelectedCharacterIcon>();
        SetCharacter(character);
    }

    public void OnClick()
    {
        if (character == null || character.killed) return;

        if (board == null) board = FindFirstObjectByType<Board>();
        if (board != null)
        {
            board.SelectCharacter(character);
        }
        else
        {
            if (selectedCharacterIcon == null) selectedCharacterIcon = FindFirstObjectByType<SelectedCharacterIcon>();
            selectedCharacterIcon?.Refresh(character);
        }
    }

    public void Refresh(Character updatedCharacter)
    {
        SetCharacter(updatedCharacter);
    }

    public Character GetCharacter()
    {
        return character;
    }

    private void SetCharacter(Character newCharacter)
    {
        character = newCharacter;
        if (character != null)
        {
            if (!string.IsNullOrWhiteSpace(character.characterName))
            {
                gameObject.name = character.characterName;
            }

            var illustrations = FindFirstObjectByType<Illustrations>();
            if (illustrations != null) image.sprite = illustrations.GetIllustrationByName(character.characterName);
            if (healthBar != null) healthBar.fillAmount = character.killed ? 0f : Mathf.Clamp01(character.health / 100f);
        }

        RefreshDeathState();
    }

    private void RefreshDeathState()
    {
        if (deadCanvasGroup == null) return;
        float targetAlpha = character != null && character.killed ? 1f : 0f;
        if (!Mathf.Approximately(deadCanvasGroup.alpha, targetAlpha))
        {
            deadCanvasGroup.alpha = targetAlpha;
        }
    }
}
