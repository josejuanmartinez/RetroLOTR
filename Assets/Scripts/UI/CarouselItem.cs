using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CarouselItem : MonoBehaviour
{
    public Image image;
    public TextMeshProUGUI label;

    public void SetSprite(Sprite spr)
    {
        image.sprite = spr;
    }

    public void SetLabel(string str, AlignmentEnum? alignment = null)
    {
        if (label == null) return;
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
