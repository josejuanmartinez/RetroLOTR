using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class MaterialManager : SearcherByName
{
    [SerializeField] private Material[] materials;

    private readonly Dictionary<Material, float> effectIntensities = new();
    private readonly Dictionary<Graphic, Material> disabledGraphicMaterials = new();

    private void Awake()
    {
        foreach (Material material in materials)
        {
            if (material != null && material.HasFloat("_EffectIntensity"))
                effectIntensities[material] = material.GetFloat("_EffectIntensity");
        }
    }

    public Material GetMaterialByName(string name)
    {
        return materials.FirstOrDefault(material =>
            material != null && Normalize(material.name) == Normalize(name));
    }

    public void SetEffectEnabled(Material material, bool enabled)
    {
        if (material == null || !material.HasFloat("_EffectIntensity")) return;

        float authoredIntensity = effectIntensities.TryGetValue(material, out float intensity)
            ? intensity
            : material.GetFloat("_EffectIntensity");
        material.SetFloat("_EffectIntensity", enabled ? authoredIntensity : 0f);

        if (enabled)
        {
            foreach (KeyValuePair<Graphic, Material> entry in disabledGraphicMaterials.ToArray())
            {
                if (entry.Value != material) continue;
                if (entry.Key != null) entry.Key.material = material;
                disabledGraphicMaterials.Remove(entry.Key);
            }
            return;
        }

        // UI masks create derived stencil materials, so changing only the source material's
        // shader property can leave an already-rendering clone unchanged. Remove the custom
        // material from every matching Graphic as well; enabling the skin restores it.
        foreach (Graphic graphic in FindObjectsByType<Graphic>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (graphic.material != material) continue;
            disabledGraphicMaterials[graphic] = material;
            graphic.material = null;
        }
    }
}
