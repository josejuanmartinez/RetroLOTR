using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using UnityEngine.EventSystems;

public class EventIconsManager : MonoBehaviour
{
    public GameObject eventIcon;
    public GridLayoutGroup gridLayout;
    private readonly List<EventIcon> activeIcons = new();

    private void Awake()
    {
        if (gridLayout == null)
        {
            gridLayout = GetComponentInChildren<GridLayoutGroup>(true);
        }
    }

    public static EventIconsManager FindManager()
    {
        return FindFirstObjectByType<EventIconsManager>();
    }

    public EventIcon AddEventIcon(EventIconType type, bool discardable, Action onOpen)
    {
        if (eventIcon == null)
        {
            Debug.LogWarning("EventIconsManager has no eventIcon prefab assigned.");
            return null;
        }

        EnsureVisible();
        Transform parent = gridLayout != null ? gridLayout.transform : transform;
        GameObject iconInstance = Instantiate(eventIcon, parent);
        iconInstance.transform.SetAsLastSibling();
        iconInstance.transform.localScale = Vector3.one;
        EventIcon icon = iconInstance.GetComponent<EventIcon>();
        if (icon == null)
        {
            icon = iconInstance.AddComponent<EventIcon>();
        }

        icon.Configure(type, discardable, onOpen, () => RemoveIcon(icon), ResolveCharacterPortraitSprite());
        activeIcons.Add(icon);
        RefreshLayout(parent as RectTransform);
        return icon;
    }

    private void RemoveIcon(EventIcon icon)
    {
        activeIcons.Remove(icon);
        Transform parent = gridLayout != null ? gridLayout.transform : transform;
        RefreshLayout(parent as RectTransform);
    }

    private void EnsureVisible()
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        if (gridLayout != null && !gridLayout.gameObject.activeSelf) gridLayout.gameObject.SetActive(true);

        ScrollRect scrollRect = GetComponentInParent<ScrollRect>(true);
        if (scrollRect != null && !scrollRect.gameObject.activeSelf)
        {
            scrollRect.gameObject.SetActive(true);
        }

        Canvas canvas = GetComponentInParent<Canvas>(true);
        if (canvas != null && !canvas.gameObject.activeSelf)
        {
            canvas.gameObject.SetActive(true);
        }
    }

    private static void RefreshLayout(RectTransform layoutRect)
    {
        Canvas.ForceUpdateCanvases();
        if (layoutRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(layoutRect);
            RectTransform parentRect = layoutRect.parent as RectTransform;
            if (parentRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
            }
        }

        EventSystem current = EventSystem.current;
        if (current != null)
        {
            current.UpdateModules();
        }
    }

    private Sprite ResolveCharacterPortraitSprite()
    {
        SelectedCharacterIcon selectedIcon = FindFirstObjectByType<SelectedCharacterIcon>();
        if (selectedIcon != null)
        {
            if (selectedIcon.icon != null && selectedIcon.icon.sprite != null)
            {
                return selectedIcon.icon.sprite;
            }

            if (selectedIcon.rawImage != null && selectedIcon.rawImage.texture is Texture2D texture)
            {
                return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            }
        }

        Game game = FindFirstObjectByType<Game>();
        Leader leader = null;
        if (game != null)
        {
            leader = game.currentlyPlaying != null ? game.currentlyPlaying : game.player;
        }
        if (leader == null || string.IsNullOrWhiteSpace(leader.characterName))
        {
            return null;
        }

        Illustrations illustrations = FindFirstObjectByType<Illustrations>();
        return illustrations != null ? illustrations.GetIllustrationByName(leader.characterName, false) ?? illustrations.GetIllustrationByName(leader.characterName) : null;
    }
}
