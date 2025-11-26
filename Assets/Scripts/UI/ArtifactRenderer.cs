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
        string spriteText = $"<sprite name=\"{artifact.GetSpriteString()}\">";
        artifactText.text = spriteText;
        hover.Initialize(artifact.GetHoverText(), 8);
    }
}
