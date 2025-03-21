using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Hover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public GameObject tooltipPanel;
    public TextMeshProUGUI textWidget;
    public Vector2 offset;
    public float exitCheckFrequency = 0.1f; // How often to check for exit

    private RectTransform tooltipRectTransform;
    private float lastExitCheckTime = 0f;
    private bool initialized = false;

    void Awake()
    {
        tooltipRectTransform = tooltipPanel.GetComponent<RectTransform>();
        tooltipPanel.SetActive(false);
    }

    void Update()
    {
        if (!initialized) return;

        if (tooltipPanel.activeSelf)
        {
            // Always update position when tooltip is active
            UpdateTooltipPosition();

            // Perform more frequent manual checks for mouse exit
            if (Time.time - lastExitCheckTime > exitCheckFrequency)
            {
                lastExitCheckTime = Time.time;

                // Force check if mouse is still over the element
                if (!IsPointOverUI()) tooltipPanel.SetActive(false);
            }
        }
    }

    private void UpdateTooltipPosition()
    {
        // Get mouse position in screen space
        Vector2 mousePosition = Input.mousePosition;

        // Get the tooltip dimensions in screen space
        Vector2 tooltipSize = tooltipRectTransform.rect.size;
        Canvas canvas = tooltipPanel.GetComponentInParent<Canvas>();
        float scaleFactor = canvas.scaleFactor;
        tooltipSize *= scaleFactor;

        // Default position (mouse position + offset)
        Vector2 tooltipPosition = mousePosition + offset;

        // Check if tooltip would go off-screen and adjust accordingly
        // Account for the tooltip's pivot point
        Vector2 pivotOffset = (tooltipRectTransform.pivot - new Vector2(0.5f, 0.5f)) * tooltipSize;

        // Left edge check
        if (tooltipPosition.x - (tooltipSize.x * 0.5f) + pivotOffset.x < 0) tooltipPosition.x = (tooltipSize.x * 0.5f) - pivotOffset.x;

        // Right edge check
        if (tooltipPosition.x + (tooltipSize.x * 0.5f) + pivotOffset.x > Screen.width) tooltipPosition.x = Screen.width - (tooltipSize.x * 0.5f) - pivotOffset.x;

        // Bottom edge check
        if (tooltipPosition.y - (tooltipSize.y * 0.5f) + pivotOffset.y < 0) tooltipPosition.y = (tooltipSize.y * 0.5f) - pivotOffset.y;

        // Top edge check
        if (tooltipPosition.y + (tooltipSize.y * 0.5f) + pivotOffset.y > Screen.height) tooltipPosition.y = Screen.height - (tooltipSize.y * 0.5f) - pivotOffset.y;

        // Set the final position
        tooltipRectTransform.position = tooltipPosition;
    }

    public void Initialize(string text, Vector2 offset, int fontSize, TextAlignmentOptions textAlignment)
    {
        this.offset = offset;
        textWidget.text = CreateTextWithBackground(text);
        textWidget.fontSize = fontSize;
        textWidget.alignment = textAlignment;
        
        // Force layout rebuild to ensure correct size calculation
        LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRectTransform);

        initialized = true;
    }

    public string CreateTextWithBackground(string text)
    {
        return $"<mark=#ffffff>{text}</mark>";
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        tooltipPanel.SetActive(true);

        // Force layout rebuild before positioning
        LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRectTransform);

        UpdateTooltipPosition();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        tooltipPanel.SetActive(false);
    }

    private bool IsPointOverUI()
    {
        // Raycast against all UI elements
        PointerEventData eventDataCurrentPosition = new(EventSystem.current);
        eventDataCurrentPosition.position = Input.mousePosition;
        System.Collections.Generic.List<RaycastResult> results = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(eventDataCurrentPosition, results);

        // Check if any of the hit objects is this one
        foreach (RaycastResult result in results)
            if (result.gameObject == gameObject)
                return true;

        return false;
    }
}