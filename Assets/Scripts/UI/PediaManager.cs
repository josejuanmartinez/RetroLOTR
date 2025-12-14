using UnityEngine;
using UnityEngine.EventSystems;

public class PediaManager : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public GameObject pedia;
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (PopupManager.IsShowing) return;
        pedia.SetActive(true);
    }


    public void OnPointerExit(PointerEventData eventData)
    {
        pedia.SetActive(false);
    }

}
