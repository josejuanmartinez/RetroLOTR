using UnityEngine;

public class CharacterSpriteHover : MonoBehaviour
{
    public Hex hex;
    private SelectedCharacterIcon selectedIcon;
    private Board board;
    private bool isPreviewing;

    private void Awake()
    {
        board = FindFirstObjectByType<Board>();
        selectedIcon = FindFirstObjectByType<SelectedCharacterIcon>();
    }

    private void OnMouseEnter()
    {
        if (hex == null || hex.characterIcon == null) return;
        if (hex.characterIcon.sprite == null || hex.characterIcon.sprite == hex.defaultCharacterSprite) return;
        if (!hex.TryGetKnownCharacterForIcon(out Character character)) return;
        board ??= FindFirstObjectByType<Board>();
        if (board != null && board.selectedCharacter == character) return;
        if (!hex.TryGetPreviewTextForCharacter(character, out string hoverText)) return;

        isPreviewing = true;
        bool isScouted = hex.IsScouted();
        selectedIcon.RefreshHoverPreview(character, hoverText, isScouted, isScouted);
    }

    private void OnMouseExit()
    {
        if (!isPreviewing) return;
        isPreviewing = false;

        if (selectedIcon == null)
        {
            Layout layout = FindFirstObjectByType<Layout>();
            selectedIcon = layout != null ? layout.GetSelectedCharacterIcon() : null;
        }
        if (selectedIcon == null) return;

        board ??= FindFirstObjectByType<Board>();
        if (board != null && board.selectedCharacter != null)
        {
            selectedIcon.Refresh(board.selectedCharacter);
        }
        else
        {
            selectedIcon.Hide();
        }
    }
}
