using UnityEngine;

public class SpriteRendererGridLayout : MonoBehaviour
{
    public Vector2 cellSize = new(1f, 1f);
    public Vector2 spacing = new(0.1f, 0.1f);
    public int constraintCount = 3; // items per row
    public bool centerAlignment = true;
    public bool scaleToCellSize = true;
    public bool autoArrangeOnStart = true;

    void Start()
    {
        if (autoArrangeOnStart) Arrange();
    }

    [ContextMenu("Arrange")]
    public void Arrange()
    {
        if (constraintCount <= 0) constraintCount = 1;

        int childCount = transform.childCount;
        int numRows = Mathf.CeilToInt((float)childCount / constraintCount);
        int maxItemsInRow = Mathf.Min(constraintCount, childCount);

        float xOffset = 0f;
        float yOffset = 0f;
        if (centerAlignment && childCount > 0)
        {
            float maxRowWidth = (maxItemsInRow - 1) * (cellSize.x + spacing.x);
            float gridHeight = (numRows - 1) * (cellSize.y + spacing.y);
            xOffset = -maxRowWidth * 0.5f;
            yOffset = gridHeight * 0.5f;
        }

        for (int i = 0; i < childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child == null) continue;
            int row = i / constraintCount;
            int col = i % constraintCount;

            float x = col * (cellSize.x + spacing.x) + xOffset;
            float y = -row * (cellSize.y + spacing.y) + yOffset;
            child.localPosition = new Vector3(x, y, 0);

            if (scaleToCellSize)
            {
                SpriteRenderer sr = child.GetComponentInChildren<SpriteRenderer>();
                if (sr != null && sr.sprite != null)
                {
                    Vector2 spriteSize = sr.sprite.bounds.size;
                    if (spriteSize.x > 0 && spriteSize.y > 0)
                    {
                        float scaleX = cellSize.x / spriteSize.x;
                        float scaleY = cellSize.y / spriteSize.y;
                        float uniformScale = Mathf.Min(scaleX, scaleY);
                        child.localScale = new Vector3(uniformScale, uniformScale, 1f);
                    }
                }
            }
        }
    }
}
