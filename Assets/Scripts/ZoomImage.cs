using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[RequireComponent(typeof(Image))]
public class ZoomImage : MonoBehaviour
{
    [Min(0.01f)]
    public float zoomFactor = 1.2f;
    public float verticalOffset = 0f;

    public bool autoAssignMaterial = true;

    private static readonly int ZoomId = Shader.PropertyToID("_Zoom");
    private static readonly int SpriteUvId = Shader.PropertyToID("_SpriteUV");
    private static readonly int OffsetId = Shader.PropertyToID("_Offset");

    private Image image;
    private Sprite lastSprite;
    private Material materialInstance;

    private void Awake()
    {
        image = GetComponent<Image>();
        ApplyZoom();
    }

    private void OnEnable()
    {
        if (image == null) image = GetComponent<Image>();
        ApplyZoom();
    }

    private void Update()
    {
        if (image == null) return;
        if (image.sprite != lastSprite)
        {
            ApplyZoom();
        }
    }

    private void OnValidate()
    {
        if (image == null) image = GetComponent<Image>();
        ApplyZoom();
    }

    private void OnDestroy()
    {
        if (materialInstance != null)
        {
            if (Application.isPlaying)
            {
                Destroy(materialInstance);
            }
            else
            {
                DestroyImmediate(materialInstance);
            }
        }
    }

    private void ApplyZoom()
    {
        if (image == null) return;

        if (autoAssignMaterial)
        {
            Shader shader = Shader.Find("UI/Zoom");
            if (shader != null)
            {
                if (materialInstance == null || materialInstance.shader != shader)
                {
                    materialInstance = new Material(shader);
                }
                image.material = materialInstance;
            }
        }

        Sprite sprite = image.sprite;
        lastSprite = sprite;
        if (sprite == null || sprite.texture == null) return;

        Rect rect = sprite.textureRect;
        float texW = sprite.texture.width;
        float texH = sprite.texture.height;
        Vector4 uv = new(rect.x / texW, rect.y / texH, rect.width / texW, rect.height / texH);

        Material target = image.material;
        if (target == null) return;
        target.SetFloat(ZoomId, Mathf.Max(0.01f, zoomFactor));
        target.SetVector(SpriteUvId, uv);
        target.SetVector(OffsetId, new Vector4(0f, verticalOffset, 0f, 0f));
    }
}
