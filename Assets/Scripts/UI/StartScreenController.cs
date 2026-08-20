using RetroLOTR.Scenarios;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>First screen on a fresh scene. "Select campaign" toggles the campaign selection
/// screen on top of it without ever closing this one — it only closes once a scenario is
/// actually chosen (Board calls Hide() at that point).</summary>
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
        campaignSelection.gameObject.SetActive(!campaignSelection.gameObject.activeSelf);
    }

    // Called once a scenario has been chosen; campaignSelection has already hidden itself by then.
    public void Hide()
    {
        root.SetActive(false);
    }

    public void ToggleSkin()
    {
        SkinManager manager = FindFirstObjectByType<SkinManager>();
        if (manager == null) return;
        ApplySkin(manager.GetNextSkin());
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
