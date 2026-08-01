using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class LoadingUIIcon : MonoBehaviour
{
    [SerializeField] private Sprite sprite;
    [SerializeField] private float rotationSpeed = 180f;

    private Image iconImage;
    private bool isRotating = true;

    private void Awake()
    {
        iconImage = GetComponent<Image>();
        ApplySprite();
    }

    private void Update()
    {
        if (!isRotating) return;

        transform.Rotate(0f, 0f, -rotationSpeed * Time.unscaledDeltaTime);
    }

    public void Initialize(Sprite iconSprite)
    {
        sprite = iconSprite;
        isRotating = true;
        ApplySprite();
    }

    public void Stop()
    {
        isRotating = false;
    }

    private void ApplySprite()
    {
        if (iconImage == null)
            iconImage = GetComponent<Image>();

        iconImage.sprite = sprite;
        iconImage.enabled = sprite != null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ApplySprite();
    }
#endif
}
