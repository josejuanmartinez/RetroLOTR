using UnityEngine;
using UnityEngine.UI;

public class AutoScroll : MonoBehaviour
{
    public ScrollRect scrollRect;

    // Update is called once per frame
    public void Refresh()
    {

        scrollRect.verticalNormalizedPosition = 0f; // bottom
    }
}
