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

    public void SetLabel(string str)
    {
        if (label == null) return;
        label.richText = true;
        label.extraPadding = true;
        label.overflowMode = TextOverflowModes.Overflow;
        label.text = EnsureAlignmentSpritePrefix(str);
        label.ForceMeshUpdate(true, true);
    }

    string EnsureAlignmentSpritePrefix(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Contains("<sprite"))
        {
            return value;
        }

        string trimmed = value.Trim();
        string spriteName = trimmed switch
        {
            "Gandalf" => "freePeople",
            "Saruman" => "darkServants",
            "Sauron" => "darkServants",
            _ => null
        };

        if (string.IsNullOrWhiteSpace(spriteName))
        {
            return value;
        }

        return $"<sprite name=\"{spriteName}\"> {trimmed}";
    }
}
