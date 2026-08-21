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
    private bool hasAppliedSkin;

    // ApplySkin() only sweeps whatever Image/SpriteRenderer components exist at the moment a skin
    // is chosen. Anything that ships with a skin material baked into its own prefab (e.g. a leader
    // portrait authored with the Bakshi material already assigned) and is instantiated or enabled
    // *after* that sweep never gets corrected — it's stuck on its spawn-time material regardless of
    // the active skin. This periodic re-sweep catches those without needing every such prefab to
    // call back in individually.
    private const float ResweepInterval = 1f;
    private float resweepTimer;

    private void Update()
    {
        if (!hasAppliedSkin) return;
        resweepTimer += Time.deltaTime;
        if (resweepTimer < ResweepInterval) return;
        resweepTimer = 0f;
        ApplyToAllRenderers(FindObjectsByType<Hex>(FindObjectsInactive.Include, FindObjectsSortMode.None));
    }

    public void ApplySkin(Skins skin)
    {
        SkinMaterial entry = System.Array.Find(skinMaterials, s => s.skin == skin);
        activeMaterial = entry?.material;
        activeAnimatedMaterial = entry?.animatedMaterial;
        useSkinMaterials = activeMaterial != null;
        hasAppliedSkin = true;

        Hex[] hexes = FindObjectsByType<Hex>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        ApplyToAllRenderers(hexes);
        RestoreHexTerrainMaterials(hexes);
    }

    private void ApplyToAllRenderers(Hex[] hexes)
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

        // HexSeamlessTerrain exclusively owns each hex's terrain SpriteRenderer (runtime-generated
        // material + per-hex MaterialPropertyBlock data, rebuilt via MarkDirty/RestoreHexTerrainMaterials
        // below). Sweeping them here too would fight that ownership: this loop would cache whatever
        // material it first saw as the renderer's "original" and keep forcing it back, even after
        // HexSeamlessTerrain destroys and replaces its runtime material on a skin change — reassigning
        // a stale/destroyed material with none of the blend property-block data applied.
        HashSet<SpriteRenderer> hexTerrainRenderers = new();
        foreach (Hex hex in hexes)
            if (hex.terrainTexture != null) hexTerrainRenderers.Add(hex.terrainTexture);

        foreach (SpriteRenderer spriteRenderer in FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (hexTerrainRenderers.Contains(spriteRenderer)) continue;

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

    private static void RestoreHexTerrainMaterials(Hex[] hexes)
    {
        // HexSeamlessTerrain owns the terrain material and its grid/blending property blocks.
        // Marking each existing hex dirty rebuilds those renderers at the end of this frame,
        // repairing any runtime assignment made by an older MaterialManager implementation.
        foreach (Hex hex in hexes)
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
