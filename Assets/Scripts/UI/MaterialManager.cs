using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class SkinMaterial
{
    public Skins skin;
    public Material material;
    public Material animatedMaterial;
}

public class MaterialManager : SearcherByName
{
    [SerializeField] private SkinMaterial[] skinMaterials;

    private readonly Dictionary<Image, Material> originalImageMaterials = new();
    private readonly Dictionary<SpriteRenderer, Material> originalSpriteMaterials = new();
    private readonly HashSet<Image> animatedImages = new();
    private readonly HashSet<SpriteRenderer> animatedSprites = new();

    private Material activeMaterial;
    private Material activeAnimatedMaterial;
    private bool useSkinMaterials;

    public void ApplySkin(Skins skin)
    {
        SkinMaterial entry = System.Array.Find(skinMaterials, s => s.skin == skin);
        activeMaterial = entry?.material;
        activeAnimatedMaterial = entry?.animatedMaterial;
        useSkinMaterials = activeMaterial != null;

        ApplyToAllRenderers();
        RestoreHexTerrainMaterials();
    }

    private void ApplyToAllRenderers()
    {
        foreach (Image image in FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!originalImageMaterials.ContainsKey(image))
            {
                Material original = image.material;
                if (IsAnimatedMaterial(original)) animatedImages.Add(image);
                originalImageMaterials[image] = IsManagedMaterial(original) ? null : original;
            }

            Material originalMaterial = originalImageMaterials[image];
            if (image.TryGetComponent<ImageUnaffectedBySkin>(out _))
            {
                if (image.material != originalMaterial) image.material = originalMaterial;
                continue;
            }

            if (!IsSkinCandidate(originalMaterial))
            {
                if (image.material != originalMaterial) image.material = originalMaterial;
                continue;
            }

            Material targetMaterial = useSkinMaterials
                ? (animatedImages.Contains(image) && activeAnimatedMaterial != null ? activeAnimatedMaterial : activeMaterial)
                : originalImageMaterials[image];
            if (image.material != targetMaterial) image.material = targetMaterial;
        }

        foreach (SpriteRenderer spriteRenderer in FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!originalSpriteMaterials.ContainsKey(spriteRenderer))
            {
                Material original = spriteRenderer.sharedMaterial;
                if (IsAnimatedMaterial(original)) animatedSprites.Add(spriteRenderer);
                originalSpriteMaterials[spriteRenderer] = IsManagedMaterial(original) ? null : original;
            }

            Material originalMaterial = originalSpriteMaterials[spriteRenderer];
            if (!IsSkinCandidate(originalMaterial))
            {
                if (spriteRenderer.sharedMaterial != originalMaterial)
                    spriteRenderer.sharedMaterial = originalMaterial; 
                continue;
            }

            Material targetMaterial = useSkinMaterials
                ? (animatedSprites.Contains(spriteRenderer) && activeAnimatedMaterial != null ? activeAnimatedMaterial : activeMaterial)
                : originalSpriteMaterials[spriteRenderer];
            if (spriteRenderer.sharedMaterial != targetMaterial) spriteRenderer.sharedMaterial = targetMaterial;
        }

        RemoveDestroyedRenderers();
    }

    private static void RestoreHexTerrainMaterials()
    {
        // HexSeamlessTerrain owns the terrain material and its grid/blending property blocks.
        // Marking each existing hex dirty rebuilds those renderers at the end of this frame,
        // repairing any runtime assignment made by an older MaterialManager implementation.
        foreach (Hex hex in FindObjectsByType<Hex>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            HexSeamlessTerrain.MarkDirty(hex);
    }

    private bool IsManagedMaterial(Material material)
    {
        return material != null && skinMaterials.Any(entry => entry.material == material || entry.animatedMaterial == material);
    }

    private bool IsAnimatedMaterial(Material material)
    {
        return material != null && skinMaterials.Any(entry => entry.animatedMaterial == material);
    }

    private bool IsSkinCandidate(Material material)
    {
        if (material == null || IsManagedMaterial(material)) return true;
        return Normalize(material.name).Contains("default");
    }

    private void RemoveDestroyedRenderers()
    {
        foreach (Image image in originalImageMaterials.Keys.Where(image => image == null).ToArray())
        {
            originalImageMaterials.Remove(image);
            animatedImages.Remove(image);
        }

        foreach (SpriteRenderer sprite in originalSpriteMaterials.Keys.Where(sprite => sprite == null).ToArray())
        {
            originalSpriteMaterials.Remove(sprite);
            animatedSprites.Remove(sprite);
        }
    }
}
