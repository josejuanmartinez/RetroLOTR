using TMPro;
using UnityEngine;

public class StoresManager : MonoBehaviour
{
    public TextMeshProUGUI leatherAmount;
    public TextMeshProUGUI mountsAmount;
    public TextMeshProUGUI timberAmount;
    public TextMeshProUGUI ironAmount;
    public TextMeshProUGUI mithrilAmount;
    public TextMeshProUGUI goldAmount;

    public void RefreshStores()
    {
        PlayableLeader playableLeader = FindFirstObjectByType<Game>().player;
        leatherAmount.text = playableLeader.leatherAmount.ToString();
        mountsAmount.text = playableLeader.mountsAmount.ToString();
        timberAmount.text = playableLeader.timberAmount.ToString();
        ironAmount.text = playableLeader.ironAmount.ToString();
        mithrilAmount.text = playableLeader.mithrilAmount.ToString();
        goldAmount.text = playableLeader.goldAmount.ToString();
    }
}
