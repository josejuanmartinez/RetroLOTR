using System.Collections;
using UnityEngine;

public class CharacterSpriteHover : MonoBehaviour
{
    public Hex hex;
    [Tooltip("Seconds the cursor must stay on a character's sprite, uninterrupted, before its card preview appears.")]
    [SerializeField] private float cardPreviewHoverDelay = 5f;
    private SelectedCharacterIcon selectedIcon;
    private Board board;
    private bool isPreviewing;
    private Character previewedCharacter;
    private Hex previewedHex;
    private Coroutine cardPreviewCoroutine;

    private void Awake()
    {
        board = Board.Instance;
        selectedIcon = FindFirstObjectByType<SelectedCharacterIcon>();
    }

    private void OnMouseEnter()
    {
        if (hex == null || hex.characterSpriteRenderer == null) return;
        if (hex.characterSpriteRenderer.sprite == null) return;
        if (!hex.TryGetKnownCharacterForIcon(out Character character)) return;
        Sounds.Instance?.PlayUiHover();
        if (BoardNavigator.IsNavigationInputLocked()) return;

        // Every known character loses the unhovered dim tint while hovered, regardless of
        // nation. Keep the clickable cursor restricted to selectable (player-controlled)
        // characters so previewing another nation does not imply it can be selected.
        hex.SetCharacterHovered(true);
        if (character.isPlayerControlled)
        {
            hex.GetCharacterAnimationController()?.SetHoverCursor(true);
        }
        hex.Hover();

        board ??= Board.Instance;
        bool isSelected = board != null && board.selectedCharacter == character;
        bool isScouted = hex.IsScouted();

        // The already-selected character's info sits permanently in SelectedCharacterIcon
        // already, so skip the transient hover-text overwrite for it — but the card preview
        // below is independent of that panel and should still show on hover either way.
        if (!isSelected)
        {
            if (!hex.TryGetPreviewTextForCharacter(character, out string hoverText)) return;
            if (selectedIcon == null)
            {
                Layout layout = FindFirstObjectByType<Layout>();
                selectedIcon = layout != null ? layout.GetSelectedCharacterIcon() : null;
            }
            if (selectedIcon == null) return;

            selectedIcon.RefreshHoverPreview(character, hoverText, isScouted, isScouted);
        }

        isPreviewing = true;
        previewedCharacter = character;
        previewedHex = hex;

        if (cardPreviewCoroutine != null) StopCoroutine(cardPreviewCoroutine);
        cardPreviewCoroutine = StartCoroutine(ShowCardPreviewAfterDelay(character, isScouted));
    }

    // Only pops the card preview after the cursor has sat on this character's sprite,
    // uninterrupted, for cardPreviewHoverDelay seconds — OnMouseExit/OnDisable (via
    // ClearPreview) cancel this if the cursor leaves first.
    private IEnumerator ShowCardPreviewAfterDelay(Character character, bool includeArmyCards)
    {
        yield return new WaitForSeconds(cardPreviewHoverDelay);
        cardPreviewCoroutine = null;
        CardCenterPreview.Instance?.ShowPreviewForCharacter(character, includeArmyCards: includeArmyCards);
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
        if (BoardNavigator.IsPointerOverVisibleUIElement()) return;
        if (hex == null || !hex.TryGetKnownCharacterForIcon(out Character character)) return;
        if (BoardNavigator.IsNavigationInputLocked() || PopupManager.IsShowing || !character.isPlayerControlled)
        {
            Sounds.Instance?.PlayNegative();
            return;
        }

        board ??= Board.Instance;
        if (board == null) return;

        Sounds.Instance?.PlayUiClick();
        board.SelectHex(hex.v2, characterToSelect: character);
        if (board.selectedCharacter == character)
        {
            SituationCardsUI.Instance?.TryRestoreBloom(character);
        }
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

        if (cardPreviewCoroutine != null) { StopCoroutine(cardPreviewCoroutine); cardPreviewCoroutine = null; }

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

        board ??= Board.Instance;
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
