using UnityEngine;
using TMPro;

/// <summary>
/// Bends a TextMeshPro mesh along an arc.
/// Attach this to a world-space TextMeshPro object after the text is set.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(TMP_Text))]
public class CurvedText : MonoBehaviour
{
    [Tooltip("Radius of the arc the text bends along. Positive = upward curve, negative = downward curve.")]
    public float curveRadius = 8f;

    [Tooltip("Total arc angle in degrees. Spread across all characters.")]
    public float arcAngle = 25f;

    [Tooltip("If true, recalculate every frame (needed for animated/variable text).")]
    public bool updateEveryFrame = true;

    private TMP_Text textMesh;
    private string lastText;
    private float lastCurveRadius;
    private float lastArcAngle;

    void Awake()
    {
        textMesh = GetComponent<TMP_Text>();
    }

    void OnEnable()
    {
        ForceUpdate();
    }

    void LateUpdate()
    {
        if (!updateEveryFrame)
        {
            // Still update if parameters changed in editor
            if (textMesh != null &&
                (textMesh.text != lastText ||
                 !Mathf.Approximately(curveRadius, lastCurveRadius) ||
                 !Mathf.Approximately(arcAngle, lastArcAngle)))
            {
                ForceUpdate();
            }
            return;
        }

        ForceUpdate();
    }

    public void ForceUpdate()
    {
        if (textMesh == null)
            return;

        textMesh.ForceMeshUpdate();

        TMP_TextInfo textInfo = textMesh.textInfo;
        int characterCount = textInfo.characterCount;

        if (characterCount == 0)
            return;

        float angleStep = arcAngle / Mathf.Max(characterCount - 1, 1);
        float startAngle = -arcAngle * 0.5f;

        for (int i = 0; i < characterCount; i++)
        {
            TMP_CharacterInfo charInfo = textInfo.characterInfo[i];

            if (!charInfo.isVisible)
                continue;

            int materialIndex = charInfo.materialReferenceIndex;
            int vertexIndex = charInfo.vertexIndex;

            Vector3[] vertices = textInfo.meshInfo[materialIndex].vertices;

            Vector3 charMidBaseline = (vertices[vertexIndex + 0] + vertices[vertexIndex + 2]) * 0.5f;

            float angle = startAngle + angleStep * i;
            float angleRad = Mathf.Deg2Rad * angle;

            Quaternion rotation = Quaternion.Euler(0, 0, -angle);

            // Offset places the character on the arc
            Vector3 offset = new Vector3(
                Mathf.Sin(angleRad) * curveRadius,
                Mathf.Cos(angleRad) * curveRadius - curveRadius,
                0);

            for (int j = 0; j < 4; j++)
            {
                vertices[vertexIndex + j] -= charMidBaseline;
                vertices[vertexIndex + j] = rotation * vertices[vertexIndex + j];
                vertices[vertexIndex + j] += charMidBaseline + offset;
            }
        }

        for (int i = 0; i < textInfo.meshInfo.Length; i++)
        {
            textInfo.meshInfo[i].mesh.vertices = textInfo.meshInfo[i].vertices;
            textMesh.UpdateGeometry(textInfo.meshInfo[i].mesh, i);
        }

        lastText = textMesh.text;
        lastCurveRadius = curveRadius;
        lastArcAngle = arcAngle;
    }
}
