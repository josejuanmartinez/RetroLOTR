using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class CaravansManager : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public StoresManager storesManager;
    public GameObject caravans;
    public TextMeshProUGUI textGUI;
   
    void Start()
    {
        if(!storesManager) storesManager = FindFirstObjectByType<StoresManager>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        textGUI.text = storesManager.GetMarketSummary();
        caravans.SetActive(true);
    }


    public void OnPointerExit(PointerEventData eventData)
    {
        caravans.SetActive(false);
    }

}
