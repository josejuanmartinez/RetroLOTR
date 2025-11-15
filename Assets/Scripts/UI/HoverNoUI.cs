using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class HoverNoUI : MonoBehaviour
{
    [Header("Tooltip (World-space GameObject)")]
    public GameObject tooltipPanel;      // a normal GameObject, not UI
    public TextMeshPro textWidget;       // 3D TextMeshPro, not UGUI
    public Vector3 offset = new(0, 1.5f, 0);

    [Header("Raycast")]
    public Camera rayCamera;             // defaults to Camera.main
    public LayerMask hoverMask = ~0;

    private bool wasHovering;

    void Awake()
    {
        if (rayCamera == null) rayCamera = Camera.main;
        tooltipPanel.SetActive(false);
    }

    void Update()
    {
        if (rayCamera == null) rayCamera = Camera.main;

        bool isHovering = IsMouseOverThis();
        // if (isHovering) Debug.Log($"Hovering over {gameObject.transform.parent.name}->{transform.name}");

        // Handle show/hide transitions
        if (isHovering && !wasHovering)
            tooltipPanel.SetActive(true);
        else if (!isHovering && wasHovering)
            tooltipPanel.SetActive(false);

        // Update tooltip position/orientation
        if (isHovering && tooltipPanel.activeSelf)
        {
            tooltipPanel.transform.position = transform.position + offset;
            tooltipPanel.transform.rotation = Quaternion.LookRotation(
                tooltipPanel.transform.position - rayCamera.transform.position
            );
        }

        wasHovering = isHovering;
    }

    public void Initialize(string text, float fontSize = 0.25f)
    {
        textWidget.text = text;
        textWidget.fontSize = fontSize;
    }

    private bool IsMouseOverThis()
    {
        if (rayCamera == null) return false;
        
        try
        {
            var sp = Input.mousePosition;

            if (!rayCamera.pixelRect.Contains(new Vector2(sp.x, sp.y)))
                return false;

            // Get the mouse position in world space (XY for 2D)
            Vector3 mouseWorld = rayCamera.ScreenToWorldPoint(Input.mousePosition);
            Vector2 point = (Vector2)mouseWorld;

            // 2D colliders: get ALL colliders at this point
            if (TryGetComponent(out Collider2D col2d))
            {
                var hits = Physics2D.OverlapPointAll(point, hoverMask);
                for (int i = 0; i < hits.Length; i++)
                    if (hits[i] == col2d) return true;
            }
        } catch (Exception) { }

        return false;
    }
}