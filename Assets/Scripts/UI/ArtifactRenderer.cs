using System.Text;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;

public class ArtifactRenderer : MonoBehaviour
{
    public TextMeshProUGUI artifactText;

    public Hover hover;

    public void Initialize(Artifact artifact)
    {
        Initialize(artifact.GetSpriteString(), artifact.GetHoverText());
    }

    public void Initialize(string spriteName, string hoverLabel)
    {
        artifactText.text = $"<sprite name=\"{spriteName}\">";
        hover.Initialize(hoverLabel, 8);
    }
}
