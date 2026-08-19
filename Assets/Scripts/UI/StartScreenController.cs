using RetroLOTR.Scenarios;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>First screen on a fresh scene. Campaign selection is shown only after Start.</summary>
public sealed class StartScreenController : MonoBehaviour
{
    [Header("Authored Start Screen")]
    [SerializeField] private GameObject root;
    [SerializeField] public Button startButton;
    [SerializeField] public Button quitButton;
    [SerializeField] public Button skinButton;
    [SerializeField] private TextMeshProUGUI skinValue;

    private CampaignSelectionManager campaignSelection;
    private bool wired;

    public void Show(CampaignSelectionManager selector)
    {
        campaignSelection = selector;
        campaignSelection.gameObject.SetActive(false);
        WireAuthoredControls();
        root.SetActive(true);
        ApplySkin(FindFirstObjectByType<SkinManager>()?.CurrentSkin ?? Skins.Default);
    }

    private void WireAuthoredControls()
    {
        if (wired) return;
        if (root == null) root = gameObject;
        wired = true;
    }

    public void StartCampaign()
    {
        Sounds.Instance?.PlayUiClick();
        root.SetActive(false);
        campaignSelection.gameObject.SetActive(true);
    }

    public void ToggleSkin()
    {
        SkinManager manager = FindFirstObjectByType<SkinManager>();
        ApplySkin(manager != null && manager.CurrentSkin == Skins.Default ? Skins.Bakshi : Skins.Default);
        Sounds.Instance?.PlayUiClick();
    }

    private void ApplySkin(Skins skin)
    {
        FindFirstObjectByType<SkinManager>()?.ChangeSkin(skin);
        if (skinValue != null) skinValue.text = $"Skin: {skin}";
    }

    public static void Quit()
    {
        Sounds.Instance?.PlayUiExit();
        Application.Quit();
    }

}
