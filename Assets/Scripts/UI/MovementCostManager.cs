using System;
using TMPro;
using UnityEngine;

public class MovementCostManager : MonoBehaviour
{
    public TextMeshPro movementText;
    public SpriteRenderer dot;

    private void Awake()
    {
        if (movementText == null)
        {
            movementText = GetComponentInChildren<TextMeshPro>(true);
        }

        if (dot == null)
        {
            SpriteRenderer[] spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                if (spriteRenderers[i] != null && string.Equals(spriteRenderers[i].gameObject.name, "dot", StringComparison.OrdinalIgnoreCase))
                {
                    dot = spriteRenderers[i];
                    break;
                }
            }
        }
    }

    public void ShowMovementLeft(int movementLeft, Character character)
    {
        //string spr = "movement";
        // if(character.IsArmyCommander()) spr = character.GetAlignment().ToString();
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        if (movementText != null && !movementText.gameObject.activeSelf) movementText.gameObject.SetActive(true);
        if (dot != null && !dot.gameObject.activeSelf) dot.gameObject.SetActive(true);
        movementText.text = movementLeft.ToString();
        UpdateDotColor(movementLeft, character);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void UpdateDotColor(int movementLeft, Character character)
    {
        if (dot == null) return;

        int maxMovement = character != null ? character.GetMaxMovement() : 0;
        float ratio = maxMovement > 0 ? Mathf.Clamp01(movementLeft / (float)maxMovement) : 0f;

        Color low = new(0.95f, 0.35f, 0.28f, 1f);
        Color high = new(0.25f, 0.9f, 0.45f, 1f);
        Color color = Color.Lerp(low, high, ratio);
        color.a = Mathf.Lerp(0.35f, 1f, ratio);
        dot.color = color;
    }
}
