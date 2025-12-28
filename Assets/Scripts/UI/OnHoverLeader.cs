using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class OnHoverLeader : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public GameObject alignment;
    public GameObject checkmark;

    public void Start()
    {
        alignment.SetActive(false);
        checkmark.GetComponent<Image>().sprite = alignment.GetComponent<Image>().sprite;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (PopupManager.IsShowing) return;
        Sounds.Instance?.PlayUiHover();
        alignment.SetActive(true);
    }

    // Called when the pointer exits the UI element
    public void OnPointerExit(PointerEventData eventData)
    {
        Sounds.Instance?.PlayUiExit();
        alignment.SetActive(false);
    }
}
