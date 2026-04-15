using UnityEngine;

public class CharacterSpriteHover : MonoBehaviour
{
    public Hex hex;
    private SelectedCharacterIcon selectedIcon;
    private Board board;
    private bool isPreviewing;
    private Character previewedCharacter;
    private Hex previewedHex;

    private void Awake()
    {
        board = FindFirstObjectByType<Board>();
        selectedIcon = FindFirstObjectByType<SelectedCharacterIcon>();
    }

    private void OnMouseEnter()
    {
        if (hex == null || hex.characterSpriteRenderer == null) return;
        if (hex.characterSpriteRenderer.sprite == null || hex.characterSpriteRenderer.sprite == hex.defaultCharacterSprite) return;
        if (!hex.TryGetKnownCharacterForIcon(out Character character)) return;
        board ??= FindFirstObjectByType<Board>();
        if (board != null && board.selectedCharacter == character) return;
        if (!hex.TryGetPreviewTextForCharacter(character, out string hoverText)) return;
        if (selectedIcon == null)
        {
            Layout layout = FindFirstObjectByType<Layout>();
            selectedIcon = layout != null ? layout.GetSelectedCharacterIcon() : null;
        }
        if (selectedIcon == null) return;

        isPreviewing = true;
        previewedCharacter = character;
        previewedHex = hex;
        bool isScouted = hex.IsScouted();
        selectedIcon.RefreshHoverPreview(character, hoverText, isScouted, isScouted);
    }

    private void Update()
    {
        if (!isPreviewing)
        {
            return;
        }

        ValidatePreviewStillValid();
    }

    private void OnMouseExit()
    {
        ClearPreview();
    }

    private void OnDisable()
    {
        ClearPreview();
    }

    private void ValidatePreviewStillValid()
    {
        if (previewedHex == null || previewedHex.characterSpriteRenderer == null)
        {
            ClearPreview();
            return;
        }

        if (previewedCharacter == null || previewedCharacter.hex != previewedHex)
        {
            ClearPreview();
            return;
        }

        if (previewedHex.characterSpriteRenderer.sprite == null ||
            previewedHex.characterSpriteRenderer.sprite == previewedHex.defaultCharacterSprite ||
            !previewedHex.TryGetKnownCharacterForIcon(out Character currentCharacter) ||
            currentCharacter != previewedCharacter)
        {
            ClearPreview();
        }
    }

    private void ClearPreview()
    {
        if (!isPreviewing && selectedIcon == null)
        {
            return;
        }

        isPreviewing = false;
        previewedCharacter = null;
        previewedHex = null;

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
