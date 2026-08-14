using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CarouselItem : MonoBehaviour, IPointerClickHandler
{
    public Image image;
    public TextMeshProUGUI label;

    private Action clickHandler;

    public void SetClickHandler(Action handler)
    {
        clickHandler = handler;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            clickHandler?.Invoke();
        }
    }

    public void SetSprite(Sprite spr)
    {
        image.sprite = spr;
    }

    public void SetLabel(string str, AlignmentEnum? alignment = null)
    {
        if (label == null) return;
        FontManager.Instance?.ApplyCurrentFont(label);
        label.richText = true;
        label.extraPadding = true;
        label.overflowMode = TextOverflowModes.Overflow;
        label.text = EnsureAlignmentSpritePrefix(str, alignment);
        label.ForceMeshUpdate(true, true);
    }

    string EnsureAlignmentSpritePrefix(string value, AlignmentEnum? alignment)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Contains("<sprite"))
        {
            return value;
        }

        if (alignment == null) return value;

        string spriteName = alignment.Value.ToString();
        return $"<sprite name=\"{spriteName}\">{spriteName} {value.Trim()}";
    }
}
