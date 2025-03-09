using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class SelectedCharacterIcon : MonoBehaviour
{
    private Image icon;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        icon = GetComponent<Image>();
    }

    // Update is called once per frame
    public void Refresh(Character c)
    {
        icon.enabled = true;
        icon.sprite = FindFirstObjectByType<IllustrationsSmall>().GetIllustrationByName(c.characterName);
    }


    // Update is called once per frame
    public void Hide()
    {
        icon.enabled = false;
    }
}
