using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Video;
public enum Skins
{
    Bakshi,
    Default
}

public class SkinManager : MonoBehaviour
{
    [SerializeField] private Camera skinCamera;
    [SerializeField] private MaterialManager materialManagerPrefab;
    [SerializeField] private FontManager fontManagerPrefab;

    private Videos videos;
    private Renderer2DManager render2dManager;
    private MaterialManager materialManager;
    private FontManager fontManager;
    private Skins currentSkin = Skins.Default;

    public Skins CurrentSkin => currentSkin;

    void Awake()
    {
        videos = FindFirstObjectByType<Videos>();
        render2dManager = FindFirstObjectByType<Renderer2DManager>();
        materialManager = FindFirstObjectByType<MaterialManager>();
        if (materialManager == null && materialManagerPrefab != null)
            materialManager = Instantiate(materialManagerPrefab);

        fontManager = FindFirstObjectByType<FontManager>();
        if (fontManager == null && fontManagerPrefab != null)
            fontManager = Instantiate(fontManagerPrefab);
    }
    
    public void ChangeSkin(Skins skin)
    {
        currentSkin = skin;
        string rendererName = $"Renderer2D{GetSkinSuffix(skin)}";
        int rendererIndex = render2dManager.GetRendererIndexByName(rendererName);
        if (rendererIndex < 0)
        {
            Debug.LogError($"SkinManager: renderer '{rendererName}' is not registered.");
            return;
        }

        Camera targetCamera = skinCamera != null ? skinCamera : Camera.main;
        UniversalAdditionalCameraData cameraData = targetCamera.GetUniversalAdditionalCameraData();
        cameraData.SetRenderer(rendererIndex);

        materialManager.ApplySkin(GetSkinSuffix(skin));

        fontManager.ApplyFont(fontManager.GetFontByName(skin.ToString()));
    }

    public VideoClip GetIntroVideo()
    {
        switch(currentSkin) {
            case Skins.Bakshi:
                return videos.introBakshi;
            default:
                return videos.intro;
        }
    }

    private static string GetSkinSuffix(Skins skin)
    {
        return skin == Skins.Default ? string.Empty : skin.ToString();
    }

}
