using UnityEngine;
using UnityEngine.EventSystems;

public class PediaManager : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public GameObject pedia;
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (PopupManager.IsShowing) return;
        Sounds.Instance?.PlayUiHover();
        pedia.SetActive(true);
    }


    public void OnPointerExit(PointerEventData eventData)
    {
        Sounds.Instance?.PlayUiExit();
        pedia.SetActive(false);
    }

}
