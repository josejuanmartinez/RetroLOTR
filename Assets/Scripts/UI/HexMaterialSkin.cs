using UnityEngine;

[System.Serializable]
public class SkinHexMaterial
{
    public Skins skin;
    public Material terrainMaterial;
    public Material gridMaterial;
}

// Per-skin choice of which material renders hex terrain (HexSeamlessBlendGame or another) and
// which asset drives the grid look (HexNeonGrid or another). HexSeamlessTerrain remains the sole
// owner of applying these to hex renderers (see MaterialManager.RestoreHexTerrainMaterials) —
// this manager only feeds it the per-skin source materials.
public class HexMaterialSkin : MonoBehaviour
{
    [SerializeField] private SkinHexMaterial[] skinHexMaterials;

    // Also read directly by ScenarioCreatorWindow (editor tooling) off the prefab asset, without
    // entering Play mode, so the scenario preview can match what a skin renders in-game.
    public SkinHexMaterial GetEntry(Skins skin)
    {
        return System.Array.Find(skinHexMaterials, s => s.skin == skin);
    }

    public void ApplySkin(Skins skin)
    {
        SkinHexMaterial entry = GetEntry(skin);
        HexSeamlessTerrain.SetSkinMaterials(entry?.terrainMaterial, entry?.gridMaterial);
    }
}
