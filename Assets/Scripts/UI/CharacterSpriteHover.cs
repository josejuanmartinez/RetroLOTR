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
        if (hex.characterSpriteRenderer.sprite == null) return;
        if (!hex.TryGetKnownCharacterForIcon(out Character character)) return;

        // Every known character loses the unhovered dim tint while hovered, regardless of
        // nation. Keep the clickable cursor restricted to selectable (player-controlled)
        // characters so previewing another nation does not imply it can be selected.
        hex.SetCharacterHovered(true);
        if (character.isPlayerControlled)
        {
            hex.GetCharacterAnimationController()?.SetHoverCursor(true);
        }
        hex.Hover();

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
        CardCenterPreview.Instance?.ShowPreviewForCharacter(character, includeArmyCards: isScouted);
    }

    private void Update()
    {
        if (!isPreviewing)
        {
            return;
        }

        ValidatePreviewStillValid();
    }

    // Character selection only happens by clicking directly on a character's own
    // sprite (this collider) — clicking elsewhere on the hex does nothing (the hex
    // tile itself has no click handler). Only selectable (yours) characters respond.
    private void OnMouseDown()
    {
        if (!Input.GetMouseButtonDown(0)) return;
        if (BoardNavigator.IsNavigationInputLocked()) return;
        if (PopupManager.IsShowing) return;
        if (BoardNavigator.IsPointerOverVisibleUIElement()) return;
        if (hex == null || !hex.TryGetKnownCharacterForIcon(out Character character)) return;
        if (!character.isPlayerControlled) return;

        board ??= FindFirstObjectByType<Board>();
        if (board == null) return;

        Sounds.Instance?.PlayUiClick();
        board.SelectHex(hex.v2, characterToSelect: character);
    }

    private void OnMouseExit()
    {
        if (hex != null)
        {
            hex.SetCharacterHovered(false);
            hex.GetCharacterAnimationController()?.SetHoverCursor(false);
        }
        ClearPreview();
    }

    private void OnDisable()
    {
        if (hex != null)
        {
            hex.SetCharacterHovered(false);
            hex.GetCharacterAnimationController()?.SetHoverCursor(false);
        }
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
            !previewedHex.TryGetKnownCharacterForIcon(out Character currentCharacter) ||
            currentCharacter != previewedCharacter)
        {
            ClearPreview();
        }
    }

    private void ClearPreview()
    {
        // Only tear down/restore when THIS component actually put up a preview — otherwise
        // every mouse-exit (including ones where OnMouseEnter no-opped, e.g. hovering the
        // already-selected character's own sprite) redundantly re-touches the shared
        // SelectedCharacterIcon. (selectedIcon is populated lazily and almost never null,
        // so the old `selectedIcon == null` half of this guard essentially never fired.)
        if (!isPreviewing)
        {
            return;
        }

        isPreviewing = false;
        previewedCharacter = null;
        previewedHex = null;
        CardCenterPreview.Instance?.HidePreview();

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
