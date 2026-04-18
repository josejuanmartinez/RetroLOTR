using UnityEngine;
using UnityEngine.UI;

public class PlayedCard : MonoBehaviour
{
    public Image image;

    public void Initialize(Sprite cardSprite)
    {
        if (image == null)
        {
            image = GetComponent<Image>();
        }

        if (image == null) return;

        image.sprite = cardSprite;
        image.enabled = cardSprite != null;
    }
}
