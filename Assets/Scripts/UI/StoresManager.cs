using TMPro;
using UnityEngine;

public class StoresManager : MonoBehaviour
{
    // Gold gained per unit when selling resources
    public const int LeatherSellValue = 5;
    public const int TimberSellValue = 5;
    public const int IronSellValue = 10;
    public const int MountsSellValue = 10;
    public const int MithrilSellValue = 25;

    public TextMeshProUGUI leatherAmount;
    public TextMeshProUGUI mountsAmount;
    public TextMeshProUGUI timberAmount;
    public TextMeshProUGUI ironAmount;
    public TextMeshProUGUI mithrilAmount;
    public TextMeshProUGUI goldAmount;

    public void RefreshStores()
    {
        PlayableLeader playableLeader = FindFirstObjectByType<Game>().player;
        int leatherProduction = playableLeader.GetLeatherPerTurn();
        int mountsProduction = playableLeader.GetMountsPerTurn();
        int timberProduction = playableLeader.GetTimberPerTurn();
        int ironProduction = playableLeader.GetIronPerTurn();
        int mithrilProduction = playableLeader.GetMithrilPerTurn();
        int goldProduction = playableLeader.GetGoldPerTurn();

        leatherAmount.text = $"{playableLeader.leatherAmount}<br>{(leatherProduction >= 0 ? "+" : "")}{leatherProduction}";
        mountsAmount.text = $"{playableLeader.mountsAmount}<br>{(mountsProduction >= 0 ? "+" : "")}{mountsProduction}";
        timberAmount.text = $"{playableLeader.timberAmount}<br>{(timberProduction >= 0 ? "+" : "")}{timberProduction}";
        ironAmount.text = $"{playableLeader.ironAmount}<br>{(ironProduction >= 0 ? "+" : "")}{ironProduction}";
        mithrilAmount.text = $"{playableLeader.mithrilAmount}<br>{(mithrilProduction >= 0 ? "+" : "")}{mithrilProduction}";
        goldAmount.text =$"{playableLeader.goldAmount} <br> {(goldProduction >= 0 ? "+" : "")}{goldProduction}";
    }
}
