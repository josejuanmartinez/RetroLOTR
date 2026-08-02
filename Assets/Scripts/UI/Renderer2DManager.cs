using System.Drawing;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class Renderer2DManager : SearcherByName
{
    [SerializeField] Renderer2DData[] renderers;

    public Renderer2DData GetRendererByName(string name)
    {
        return renderers.First(f => Normalize(f.name) == Normalize(name));
    }

    public int GetRendererIndexByName(string name)
    {
        string normalizedName = Normalize(name);
        return System.Array.FindIndex(renderers, renderer => Normalize(renderer.name) == normalizedName);
    }
}
