using TMPro;
using UnityEngine;

public class HexNumberManager : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI textWidget;
    public void Show(Vector2Int v2)
    {
        textWidget.text = $"<mark=#ffffff>{v2}</mark>";
    }
}
