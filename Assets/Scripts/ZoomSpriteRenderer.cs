using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class ZoomSpriteRenderer : MonoBehaviour
{
    [Min(0.01f)]
    public float zoomFactor = 1.2f;
    public float verticalOffset = 0f;

    public bool autoAssignMaterial = true;

    private static readonly int ZoomId = Shader.PropertyToID("_Zoom");
    private static readonly int SpriteUvId = Shader.PropertyToID("_SpriteUV");
    private static readonly int OffsetId = Shader.PropertyToID("_Offset");

    private SpriteRenderer spriteRenderer;
    private Sprite lastSprite;
    private MaterialPropertyBlock block;
    private float lastZoom;
    private float lastOffset;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        block = new MaterialPropertyBlock();
        lastZoom = zoomFactor;
        lastOffset = verticalOffset;
        ApplyZoom();
    }

    private void OnEnable()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (block == null) block = new MaterialPropertyBlock();
        ApplyZoom();
    }

    private void Update()
    {
        if (spriteRenderer == null) return;
        if (spriteRenderer.sprite != lastSprite || !Mathf.Approximately(zoomFactor, lastZoom) || !Mathf.Approximately(verticalOffset, lastOffset))
        {
            lastZoom = zoomFactor;
            lastOffset = verticalOffset;
            ApplyZoom();
        }
    }

    private void OnValidate()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (block == null) block = new MaterialPropertyBlock();
        ApplyZoom();
    }

    private void ApplyZoom()
    {
        if (spriteRenderer == null) return;

        if (autoAssignMaterial && (spriteRenderer.sharedMaterial == null || spriteRenderer.sharedMaterial.shader == null
            || spriteRenderer.sharedMaterial.shader.name != "Sprites/Zoom"))
        {
            Shader shader = Shader.Find("Sprites/Zoom");
            if (shader != null)
            {
                spriteRenderer.sharedMaterial = new Material(shader);
            }
        }

        Sprite sprite = spriteRenderer.sprite;
        lastSprite = sprite;
        if (sprite == null || sprite.texture == null) return;

        Rect rect = sprite.textureRect;
        float texW = sprite.texture.width;
        float texH = sprite.texture.height;
        Vector4 uv = new(rect.x / texW, rect.y / texH, rect.width / texW, rect.height / texH);

        spriteRenderer.GetPropertyBlock(block);
        block.SetFloat(ZoomId, Mathf.Max(0.01f, zoomFactor));
        block.SetVector(SpriteUvId, uv);
        block.SetVector(OffsetId, new Vector4(0f, verticalOffset, 0f, 0f));
        spriteRenderer.SetPropertyBlock(block);
    }

    public void Refresh()
    {
        ApplyZoom();
    }
}
