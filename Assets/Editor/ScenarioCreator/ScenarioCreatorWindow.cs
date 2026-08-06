using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RetroLOTR.Scenarios.EditorTools
{
    /// <summary>
    /// Unity editor tool for authoring scenarios: paint terrain + regions, then use the
    /// magnifier to place leader starts, PCs, characters and armies on individual hexes.
    /// Saves to Assets/Resources/Scenarios/{name}.json, which the runtime loads via
    /// <see cref="ScenarioLoader"/>.
    /// </summary>
    public class ScenarioCreatorWindow : EditorWindow
    {
        private enum Tool { Paint, Region, Magnifier }

        private const string SaveFolder = "Assets/Resources/Scenarios";
        private const string ResourceFolder = "Scenarios";

        // ---- Map state (index = row * width + col) -------------------------------------------
        private string scenarioName = "New Scenario";
        private string scenarioDescription = "";
        private string scenarioDisplayTitle = "";
        private string representativeCardName = "";
        private int width = 40;
        private int height = 40;
        private TerrainEnum[] terrain;
        private string[] regions;
        private string[] spriteNames; // per-hex chosen tile variation ("" = terrain default)
        private readonly Dictionary<int, ScenarioPC> pcs = new();
        private readonly Dictionary<int, List<ScenarioCharacter>> characters = new();
        private readonly Dictionary<int, List<ScenarioObject>> objects = new();

        // ---- Tool state ----------------------------------------------------------------------
        private Tool tool = Tool.Magnifier;
        private TerrainEnum paintTerrain = TerrainEnum.plains;
        private int brushSize = 1;
        private string paintRegion = "";      // empty = leave region unchanged while terrain-painting
        private string paintSpriteName = "";  // chosen tile variation for the terrain brush ("" = default)
        private string regionBrushRegion = ""; // region applied by the region-only brush
        private int selectedIndex = -1;
        private Vector2 paintScroll;

        // ---- New-map inputs ------------------------------------------------------------------
        private int newWidth = 40;
        private int newHeight = 40;

        // ---- View (rendering only) -----------------------------------------------------------
        private float zoom = 1f;
        private float cellW = 26f;
        private float cellH = 34f;
        private Vector2 gridScroll;
        private Vector2 inspectorScroll;
        private const float ZoomWheelSensitivity = 0.05f;

        // Spacing as a fraction of the drawn tile size, so the opaque hex art of pointy-top tiles
        // (odd rows offset in X) interlocks edge-to-edge. Footprint ~773px wide on a 974x1314 canvas
        // → ~0.79 horizontally; pointy-top rows advance ~3/4 of the tile height → ~0.51 vertically.
        private const float PackX = 0.79f;
        private const float PackY = 0.51f;

        // Tiles are drawn this much larger than their grid cell (expanded about the cell center),
        // so opaque hex art overlaps the neighbors' anti-aliased edges and transparent canvas
        // margins instead of letting the window background show through as thin seam lines.
        private const float TileOverdraw = 1.10f;

        // ---- Hex preview shaders ---------------------------------------------------------------
        private enum HexPreviewStyle { None, SeamlessBlend }
        private HexPreviewStyle previewStyle = HexPreviewStyle.SeamlessBlend;
        private bool neonGrid = true;
        private readonly Dictionary<HexPreviewStyle, Material> previewMaterials = new();

        private const string PreviewShaderMaterialFolder = "Assets/Editor/ScenarioCreator/Shaders";

        private Material GetPreviewMaterial(HexPreviewStyle style)
        {
            if (style == HexPreviewStyle.None) return null;
            if (previewMaterials.TryGetValue(style, out Material cached) && cached != null) return cached;

            Material mat = AssetDatabase.LoadAssetAtPath<Material>($"{PreviewShaderMaterialFolder}/HexSeamlessBlend.mat");
            previewMaterials[style] = mat;
            return mat;
        }

        // Odd-r offset hex neighbor lookup matching TileRect's packing (odd rows shifted +0.5*stepX).
        // Direction order: 0=E, 1=NE, 2=NW, 3=W, 4=SW, 5=SE.
        private bool TryGetNeighborIndex(int row, int col, int direction, out int neighborIndex)
        {
            bool oddRow = (row & 1) == 1;
            int nRow = row;
            int nCol = col;

            switch (direction)
            {
                case 0: nRow = row;     nCol = col + 1; break; // E
                case 1: nRow = row - 1; nCol = oddRow ? col + 1 : col; break; // NE
                case 2: nRow = row - 1; nCol = oddRow ? col : col - 1; break; // NW
                case 3: nRow = row;     nCol = col - 1; break; // W
                case 4: nRow = row + 1; nCol = oddRow ? col : col - 1; break; // SW
                case 5: nRow = row + 1; nCol = oddRow ? col + 1 : col; break; // SE
            }

            if (!InBounds(nRow, nCol)) { neighborIndex = -1; return false; }
            neighborIndex = Index(nRow, nCol);
            return true;
        }

        [MenuItem("Window/RetroLOTR/Scenario Creator")]
        public static void Open()
        {
            ScenarioCreatorWindow window = GetWindow<ScenarioCreatorWindow>("Scenario Creator");
            window.minSize = new Vector2(900, 600);
            if (window.terrain == null) window.NewMap(window.newWidth, window.newHeight);
        }

        // -------------------------------------------------------------------------------------
        // Map lifecycle
        // -------------------------------------------------------------------------------------
        private int Index(int row, int col) => row * width + col;
        private bool InBounds(int row, int col) => row >= 0 && row < height && col >= 0 && col < width;

        private void NewMap(int w, int h)
        {
            scenarioDescription = "";
            scenarioDisplayTitle = "";
            representativeCardName = "";
            width = Mathf.Clamp(w, 1, 200);
            height = Mathf.Clamp(h, 1, 200);
            terrain = new TerrainEnum[width * height];
            regions = new string[width * height];
            spriteNames = new string[width * height];
            for (int i = 0; i < terrain.Length; i++) terrain[i] = TerrainEnum.deepWater;
            pcs.Clear();
            characters.Clear();
            objects.Clear();
            selectedIndex = -1;
        }

        // Renders full card previews (PC / character / army) in the magnifier inspector.
        private ScenarioCardPreviewRenderer cardRenderer;

        // Card data is cached statically in ScenarioCardCatalog and never auto-refreshes within a
        // Unity session. Invalidate on focus so edits to deck JSON (new PCs, characters, etc.)
        // show up when the author tabs back into the window, not only after a script recompile.
        private void OnFocus()
        {
            ScenarioCardCatalog.Invalidate();
            cardRenderer?.ClearCache();
        }

        private void OnDisable()
        {
            cardRenderer?.Dispose();
            cardRenderer = null;
        }

        // -------------------------------------------------------------------------------------
        // GUI
        // -------------------------------------------------------------------------------------
        private void OnGUI()
        {
            if (terrain == null) NewMap(newWidth, newHeight);

            DrawToolbar();
            EditorGUILayout.BeginHorizontal();
            DrawLeftPanel();        // tools + new/save
            DrawGrid();             // the map
            DrawInspector();        // magnifier inspector
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            scenarioName = EditorGUILayout.TextField(scenarioName, EditorStyles.toolbarTextField, GUILayout.Width(220));
            if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(60))) Save();
            if (GUILayout.Button("Load", EditorStyles.toolbarButton, GUILayout.Width(60))) Load();
            if (GUILayout.Button("Refresh Cards", EditorStyles.toolbarButton, GUILayout.Width(90)))
            {
                AssetDatabase.Refresh();
                ScenarioCardCatalog.Invalidate();
                cardRenderer?.ClearCache();
                ClearPreviewTextureCache(); // tile art may have been reimported
            }
            if (GUILayout.Button("Recalculate textures", EditorStyles.toolbarButton, GUILayout.Width(125)))
            {
                AssetDatabase.Refresh(); // pick up externally edited tile art
                ClearPreviewTextureCache();
                Repaint();
            }
            GUILayout.FlexibleSpace();
            GUILayout.Label("Shader", GUILayout.Width(44));
            previewStyle = (HexPreviewStyle)EditorGUILayout.EnumPopup(previewStyle, EditorStyles.toolbarPopup, GUILayout.Width(120));
            neonGrid = GUILayout.Toggle(neonGrid, "Grid", EditorStyles.toolbarButton, GUILayout.Width(40));
            GUILayout.Label($"{width} x {height}", EditorStyles.toolbarButton);
            GUILayout.Label("Zoom", GUILayout.Width(34));
            zoom = GUILayout.HorizontalSlider(zoom, 0.4f, 5f, GUILayout.Width(90));
            GUILayout.Label("Cell W", GUILayout.Width(40));
            cellW = GUILayout.HorizontalSlider(cellW, 8f, 48f, GUILayout.Width(80));
            GUILayout.Label("Cell H", GUILayout.Width(40));
            cellH = GUILayout.HorizontalSlider(cellH, 8f, 48f, GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawLeftPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(210));

            // Saved with the scenario; drives the campaign-selection screen's button:
            // Title (falls back to the file name), Description as the subtitle, and Card
            // whose token art represents the scenario.
            EditorGUILayout.LabelField("Title (campaign screen)", EditorStyles.boldLabel);
            scenarioDisplayTitle = EditorGUILayout.TextField(scenarioDisplayTitle ?? string.Empty);

            EditorGUILayout.LabelField("Description", EditorStyles.boldLabel);
            scenarioDescription = EditorGUILayout.TextArea(scenarioDescription ?? string.Empty,
                EditorStyles.textArea, GUILayout.MinHeight(52));

            EditorGUILayout.LabelField("Card (campaign screen token)", EditorStyles.boldLabel);
            SearchableField(string.Empty, representativeCardName, ScenarioCardCatalog.AllCardNames,
                chosen => representativeCardName = chosen, ScenarioCardCatalog.GetCard);
            if (!string.IsNullOrEmpty(representativeCardName) && GUILayout.Button("Clear card", GUILayout.Width(90)))
                representativeCardName = "";

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("New Map", EditorStyles.boldLabel);
            newWidth = EditorGUILayout.IntField("Width", newWidth);
            newHeight = EditorGUILayout.IntField("Height", newHeight);
            if (GUILayout.Button("Create (deep water)"))
            {
                if (EditorUtility.DisplayDialog("New Map", "Discard the current map and start a new one?", "Create", "Cancel"))
                    NewMap(newWidth, newHeight);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Tool", EditorStyles.boldLabel);
            tool = (Tool)GUILayout.Toolbar((int)tool, new[] { "Terrain", "Region", "Magnifier" });

            if (tool == Tool.Paint)
            {
                DrawTerrainBrushPanel();
            }
            else if (tool == Tool.Region)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Region Brush", EditorStyles.boldLabel);
                brushSize = EditorGUILayout.IntSlider("Size", brushSize, 1, 6);
                SearchableField("Region", regionBrushRegion, ScenarioCardCatalog.Regions, v => regionBrushRegion = v, ScenarioCardCatalog.GetCard);
                EditorGUILayout.HelpBox(
                    string.IsNullOrEmpty(regionBrushRegion)
                        ? "Pick a region, then paint over any hexes to set ONLY their region (terrain unchanged)."
                        : $"Paints region '{regionBrushRegion}' onto existing hexes; terrain is left untouched.",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox("Click a hex to edit its leader start, PC, characters and armies.", MessageType.Info);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawTerrainBrushPanel()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Terrain Brush", EditorStyles.boldLabel);

            TerrainEnum newTerrain = (TerrainEnum)EditorGUILayout.EnumPopup("Terrain", paintTerrain);
            if (newTerrain != paintTerrain)
            {
                paintTerrain = newTerrain;
                paintSpriteName = ""; // variations differ per terrain; reset to default
            }
            brushSize = EditorGUILayout.IntSlider("Size", brushSize, 1, 6);

            SearchableField("Region", paintRegion, ScenarioCardCatalog.Regions, v => paintRegion = v, ScenarioCardCatalog.GetCard);

            // Variation picker — a chasm tile makes the hex an Underground entrance at load.
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Tile variation", EditorStyles.boldLabel);
            DrawVariationPicker();

            EditorGUILayout.HelpBox(
                string.IsNullOrEmpty(paintSpriteName)
                    ? "Default: a variation is chosen at load."
                    : $"Tile: {paintSpriteName}",
                MessageType.None);
        }

        private void DrawVariationPicker()
        {
            List<Sprite> variations = ScenarioCardCatalog.GetTerrainVariations(paintTerrain);

            // Selected-tile preview.
            Rect preview = GUILayoutUtility.GetRect(256, 256, GUILayout.Width(256), GUILayout.Height(256));
            Sprite selected = string.IsNullOrEmpty(paintSpriteName)
                ? ScenarioCardCatalog.GetTerrainSprite(paintTerrain)
                : ScenarioCardCatalog.GetTerrainSpriteByName(paintSpriteName);
            DrawSprite(preview, selected);

            // Features depicted by the selected tile (what this hex will gain at load).
            DrawTileFeatures(paintSpriteName, isDefault: string.IsNullOrEmpty(paintSpriteName));

            if (variations == null || variations.Count == 0)
            {
                EditorGUILayout.HelpBox("No tile variations found for this terrain.", MessageType.None);
                return;
            }

            // "Default (any)" + each variation as a clickable thumbnail grid.
            const float thumb = 80f;
            int perRow = Mathf.Max(1, Mathf.FloorToInt(196f / (thumb + 4f)));
            paintScroll = EditorGUILayout.BeginScrollView(paintScroll, GUILayout.Height(260));

            int shown = 0;
            EditorGUILayout.BeginHorizontal();
            if (DrawVariationButton(null, "Any", thumb)) paintSpriteName = "";
            shown++;
            HashSet<string> seenNames = new(StringComparer.OrdinalIgnoreCase);
            foreach (Sprite s in variations)
            {
                if (s == null) continue;
                if (!seenNames.Add(s.name)) continue;
                if (shown % perRow == 0)
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                }
                if (DrawVariationButton(s, null, thumb)) paintSpriteName = s.name;
                shown++;
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
        }

        // Draws a clickable thumbnail; highlights the current selection. Returns true on click.
        private bool DrawVariationButton(Sprite sprite, string label, float size)
        {
            Rect r = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
            bool isSelected = sprite == null ? string.IsNullOrEmpty(paintSpriteName)
                                             : string.Equals(paintSpriteName, sprite.name, StringComparison.OrdinalIgnoreCase);
            if (isSelected) EditorGUI.DrawRect(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), new Color(1f, 0.8f, 0.1f, 0.8f));

            if (sprite != null) DrawSprite(r, sprite);
            else { EditorGUI.DrawRect(r, new Color(0.2f, 0.2f, 0.2f)); GUI.Label(r, label, EditorStyles.centeredGreyMiniLabel); }

            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        // Shows whether the tile depicts a chasm (read from the sprite name via ChasmTiles) —
        // a chasm hex becomes an Underground entrance at load, so the author sees it before painting.
        private static void DrawTileFeatures(string spriteName, bool isDefault)
        {
            EditorGUILayout.LabelField("Features", EditorStyles.boldLabel);

            if (isDefault)
            {
                EditorGUILayout.LabelField("A variation is chosen at load — features vary.", EditorStyles.wordWrappedMiniLabel);
                return;
            }

            if (!ChasmTiles.Contains(spriteName))
            {
                EditorGUILayout.LabelField("None", EditorStyles.miniLabel);
                return;
            }

            EditorGUILayout.LabelField("• Chasm", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(ChasmTiles.Description, EditorStyles.wordWrappedMiniLabel);
        }

        // Downscaled, mipmapped copies of the tile textures for grid rendering. The source art is
        // ~1000px per tile but drawn at ~30px, and sprite textures import without mipmaps — the
        // GPU has to gather from huge textures at extreme minification for every screen pixel
        // (times up to 31 taps in the blend shader), which is what froze the window. A 512px
        // trilinear copy with mips makes those fetches cheap and kills the shimmer too.
        private static readonly Dictionary<Texture, Texture2D> previewTextureCache = new();
        private const int PreviewTextureMaxSize = 512;

        private static Texture GetPreviewTexture(Texture tex)
        {
            if (tex == null) return null;
            if (previewTextureCache.TryGetValue(tex, out Texture2D cached) && cached != null) return cached;

            float scale = Mathf.Min(1f, (float)PreviewTextureMaxSize / Mathf.Max(tex.width, tex.height));
            int pw = Mathf.Max(1, Mathf.RoundToInt(tex.width * scale));
            int ph = Mathf.Max(1, Mathf.RoundToInt(tex.height * scale));

            RenderTexture rt = RenderTexture.GetTemporary(pw, ph, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture prevActive = RenderTexture.active;
            Graphics.Blit(tex, rt);
            RenderTexture.active = rt;
            Texture2D copy = new Texture2D(pw, ph, TextureFormat.RGBA32, true, false)
            {
                filterMode = FilterMode.Trilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            copy.ReadPixels(new Rect(0, 0, pw, ph), 0, 0);
            copy.Apply(true, true); // build the mip chain, then free the CPU-side copy
            RenderTexture.active = prevActive;
            RenderTexture.ReleaseTemporary(rt);

            previewTextureCache[tex] = copy;
            return copy;
        }

        private static void ClearPreviewTextureCache()
        {
            foreach (Texture2D copy in previewTextureCache.Values)
                if (copy != null) DestroyImmediate(copy);
            previewTextureCache.Clear();
        }

        // Normalized atlas rect of a sprite.
        private static Rect SpriteTexCoords(Sprite sprite)
        {
            Texture tex = sprite.texture;
            return new Rect(sprite.rect.x / tex.width, sprite.rect.y / tex.height,
                            sprite.rect.width / tex.width, sprite.rect.height / tex.height);
        }

        private static void DrawSprite(Rect r, Sprite sprite, Material previewMaterial = null)
            => DrawSpriteClipped(r, r, sprite, previewMaterial);

        // Draws 'sprite' into 'r', cropped to 'clip'. This exists because Graphics.DrawTexture
        // (the path used whenever a preview shader material is set) ignores Unity's GUIClip stack
        // — unlike plain GUI.DrawTexture(WithTexCoords) calls, it is NOT cut off by the enclosing
        // scroll view. An overdrawn grid tile (see TileOverdraw) that pokes outside the viewport
        // therefore painted straight over whatever UI sits above/beside the grid — e.g. the
        // toolbar's "Refresh Cards"/"Recalculate textures" buttons, whenever the top row was in
        // view — instead of stopping at the viewport edge. Cropping the rect (and the atlas UV
        // rect by the same fraction, so the crop shows the correct slice of art instead of
        // squashing the whole sprite into the smaller rect) fixes this without relying on
        // GUIClip at all. When clip == r this is a no-op and behaves exactly as before.
        private static void DrawSpriteClipped(Rect r, Rect clip, Sprite sprite, Material previewMaterial)
        {
            if (sprite == null || sprite.texture == null) { EditorGUI.DrawRect(r, new Color(0.15f, 0.15f, 0.15f)); return; }

            Rect visible = RectIntersect(r, clip);
            if (visible.width <= 0f || visible.height <= 0f) return;

            // Texcoords are normalized, so they address the downscaled preview copy identically.
            Texture tex = GetPreviewTexture(sprite.texture);
            Rect tc = SpriteTexCoords(sprite);

            Rect drawTc = tc;
            if (visible != r)
            {
                float uMin = (visible.xMin - r.xMin) / r.width;
                float uMax = (visible.xMax - r.xMin) / r.width;
                float vMin = (visible.yMin - r.yMin) / r.height;
                float vMax = (visible.yMax - r.yMin) / r.height;
                drawTc = new Rect(tc.x + uMin * tc.width, tc.y + vMin * tc.height,
                                   (uMax - uMin) * tc.width, (vMax - vMin) * tc.height);
            }

            if (previewMaterial == null)
            {
                GUI.DrawTextureWithTexCoords(visible, tex, drawTc, true);
                return;
            }

            // _SpriteUV must stay the FULL (uncropped) tile rect — the shader derives its
            // tile-local frame (hex mask, feather, neighbor-blend geometry) from it, and cropping
            // it along with the visible slice would shrink the hex geometry itself rather than
            // just windowing which part of it is drawn.
            previewMaterial.SetVector(SpriteUVId, new Vector4(tc.x, tc.y, tc.width, tc.height));

            // In a Linear color-space project, custom-material Graphics.DrawTexture samples sRGB
            // textures into linear values while the GUI target stores whatever we write, raw. The
            // HexPreview shaders therefore convert back to gamma themselves (LinearToGammaSpace at
            // the end of each), and we keep sRGB conversion on write explicitly off so the
            // brightness is right regardless of what state the editor left GL in.
            bool prevSRGB = GL.sRGBWrite;
            GL.sRGBWrite = false;
            Graphics.DrawTexture(visible, tex, drawTc, 0, 0, 0, 0, Color.white, previewMaterial);
            GL.sRGBWrite = prevSRGB;
        }

        private static Rect RectIntersect(Rect a, Rect b)
        {
            float xMin = Mathf.Max(a.xMin, b.xMin);
            float yMin = Mathf.Max(a.yMin, b.yMin);
            float xMax = Mathf.Min(a.xMax, b.xMax);
            float yMax = Mathf.Min(a.yMax, b.yMax);
            return new Rect(xMin, yMin, Mathf.Max(0f, xMax - xMin), Mathf.Max(0f, yMax - yMin));
        }

        private static readonly int SpriteUVId = Shader.PropertyToID("_SpriteUV");
        private static readonly int AspectYId = Shader.PropertyToID("_AspectY");
        private static readonly int GridOnId = Shader.PropertyToID("_GridOn");
        private static readonly int CellCenterId = Shader.PropertyToID("_CellCenter");

        // Cell spacing in the shader's tile-local units, cached by ApplySeamlessBlendGeometry so
        // ApplyNeighborBlendProperties can hand each cell its map-space center (for the grid hue).
        private float blendColX, blendRowY;
        private static readonly int[] NeighborTexIds = Enumerable.Range(0, 6).Select(d => Shader.PropertyToID($"_NeighborTex{d}")).ToArray();
        private static readonly int[] NeighborUVIds = Enumerable.Range(0, 6).Select(d => Shader.PropertyToID($"_NeighborUV{d}")).ToArray();
        private static readonly int[] NeighborOffsetIds = Enumerable.Range(0, 6).Select(d => Shader.PropertyToID($"_NeighborOffset{d}")).ToArray();
        private static readonly int[] NeighborValidIds = Enumerable.Range(0, 6).Select(d => Shader.PropertyToID($"_NeighborValid{d}")).ToArray();

        // -------------------------------------------------------------------------------------
        // Grid rendering + interaction
        // -------------------------------------------------------------------------------------
        private void DrawGrid()
        {
            float baseW = cellW * zoom;
            float baseH = cellH * zoom;
            float stepX = baseW * PackX;   // spacing < tile size, so adjacent opaque hexes touch
            float stepY = baseH * PackY;
            float drawW = baseW * TileOverdraw; // drawn size exceeds the cell, see TileOverdraw
            float drawH = baseH * TileOverdraw;

            float contentW = width * stepX + stepX * 0.5f + (drawW - stepX) + 8;
            float contentH = height * stepY + (drawH - stepY) + 8;

            // Reserve the viewport explicitly (GUI.BeginScrollView instead of the EditorGUILayout
            // wrapper) so its rect is known this same event — a Ctrl+wheel check needs it before
            // the scroll view's own wheel handling treats the event as a plain scroll, and
            // GUILayoutUtility.GetLastRect() right after BeginScrollView isn't legal (new group).
            Rect viewport = GUILayoutUtility.GetRect(0, 0, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            if (Event.current.type == EventType.ScrollWheel && Event.current.control &&
                viewport.Contains(Event.current.mousePosition))
            {
                zoom = Mathf.Clamp(zoom - Event.current.delta.y * ZoomWheelSensitivity, 0.4f, 5f);
                Event.current.Use();
                Repaint();
            }

            Rect content = new Rect(0, 0, contentW, contentH);
            gridScroll = GUI.BeginScrollView(viewport, gridScroll, content);

            HandleGridMouse(content, stepX, stepY);

            if (Event.current.type == EventType.Repaint)
            {
                ApplySeamlessBlendGeometry(drawW, drawH, stepX, stepY);

                // Only the cells inside the scroll viewport (plus a one-tile margin) are drawn —
                // repainting the whole map every frame is what froze large maps, especially in
                // shader mode where every cell is its own material draw.
                int rowFirst = Mathf.Max(0, Mathf.FloorToInt((gridScroll.y - drawH) / stepY));
                int rowLast = Mathf.Min(height - 1, Mathf.CeilToInt((gridScroll.y + viewport.height) / stepY));
                int colFirst = Mathf.Max(0, Mathf.FloorToInt((gridScroll.x - drawW) / stepX) - 1);
                int colLast = Mathf.Min(width - 1, Mathf.CeilToInt((gridScroll.x + viewport.width) / stepX));

                // Content-space window actually visible through the scroll viewport, used to crop
                // overdrawn tiles (see DrawSpriteClipped) so they can't bleed into the toolbar or
                // other UI above/beside the grid.
                Rect visibleClip = new Rect(gridScroll.x, gridScroll.y, viewport.width, viewport.height);

                // Pass 1: terrain tiles (drawn larger than the spacing so they interlock with no gaps).
                for (int row = rowFirst; row <= rowLast; row++)
                    for (int col = colFirst; col <= colLast; col++)
                        DrawCellSprite(TileRect(content, row, col, drawW, drawH, stepX, stepY), visibleClip, row, col, stepX, stepY);

                // Pass 2: character tokens, region tints, markers and selection on top.
                for (int row = rowFirst; row <= rowLast; row++)
                    for (int col = colFirst; col <= colLast; col++)
                    {
                        Rect tileRect = TileRect(content, row, col, drawW, drawH, stepX, stepY);
                        DrawCellCharacters(tileRect, stepX, stepY, row, col);
                        DrawCellOverlay(tileRect, stepX, stepY, row, col);
                    }
            }

            GUI.EndScrollView();
        }

        private Rect TileRect(Rect content, int row, int col, float drawW, float drawH, float stepX, float stepY)
        {
            // The overdraw expansion is centered on the cell, so the anchor shifts back by half of it.
            float expandX = drawW * (1f - 1f / TileOverdraw) * 0.5f;
            float expandY = drawH * (1f - 1f / TileOverdraw) * 0.5f;
            float x = content.x + col * stepX + ((row & 1) == 1 ? stepX * 0.5f : 0f) - expandX;
            float y = content.y + row * stepY - expandY;
            return new Rect(x, y, drawW, drawH);
        }

        private Sprite GetCellTerrainSprite(int idx)
        {
            return !string.IsNullOrEmpty(spriteNames[idx])
                ? ScenarioCardCatalog.GetTerrainSpriteByName(spriteNames[idx])
                : ScenarioCardCatalog.GetTerrainSprite(terrain[idx]);
        }

        private void DrawCellSprite(Rect draw, Rect clip, int row, int col, float stepX, float stepY)
        {
            int idx = Index(row, col);
            TerrainEnum t = terrain[idx];

            // While placing regions, swap the detailed terrain art for a flat grass/water
            // placeholder so the region tint + letter drawn on top stay legible. The old
            // tint was painted over this same overdrawn art rect, which bled into neighboring
            // hexes and made it hard to see what was actually being set.
            if (tool == Tool.Region)
            {
                DrawRegionHex(draw, stepX, stepY, t, regions[idx]);
                return;
            }

            Sprite sprite = GetCellTerrainSprite(idx);

            Material previewMaterial = GetPreviewMaterial(previewStyle);
            if (previewMaterial != null && previewStyle == HexPreviewStyle.SeamlessBlend)
                ApplyNeighborBlendProperties(previewMaterial, row, col);

            if (sprite != null && sprite.texture != null) DrawSpriteClipped(draw, clip, sprite, previewMaterial);
            else EditorGUI.DrawRect(RectIntersect(draw, clip), TerrainFallbackColor(t));

            // Overlay the PC's hex artwork (Assets/Art/Hexes/PCs) when a named PC sits on this hex.
            if (pcs.TryGetValue(idx, out ScenarioPC pc) && !string.IsNullOrEmpty(pc.pcName))
            {
                Sprite pcSprite = ScenarioCardCatalog.GetPcHexSprite(pc.pcName);
                if (pcSprite != null && pcSprite.texture != null) DrawSpriteClipped(draw, clip, pcSprite, null);
            }
        }

        // HexSeamlessBlend.shader works in a tile-local frame: origin at the hex center, +y up on
        // screen, 1 unit = drawn tile width. The neighbor-center offsets and the cell aspect only
        // depend on the view, so they are pushed once per repaint instead of per hex.
        private void ApplySeamlessBlendGeometry(float drawW, float drawH, float stepX, float stepY)
        {
            if (previewStyle != HexPreviewStyle.SeamlessBlend) return;
            Material mat = GetPreviewMaterial(previewStyle);
            if (mat == null) return;

            mat.SetFloat(AspectYId, drawH / drawW);
            mat.SetFloat(GridOnId, neonGrid ? 1f : 0f);

            float colX = blendColX = stepX / drawW; // one column of horizontal spacing, in drawn-tile-width units
            float rowY = blendRowY = stepY / drawW; // one row of vertical spacing, same units (y-up)
            mat.SetVector(NeighborOffsetIds[0], new Vector4(colX, 0f));           // E
            mat.SetVector(NeighborOffsetIds[1], new Vector4(colX * 0.5f, rowY));  // NE
            mat.SetVector(NeighborOffsetIds[2], new Vector4(-colX * 0.5f, rowY)); // NW
            mat.SetVector(NeighborOffsetIds[3], new Vector4(-colX, 0f));          // W
            mat.SetVector(NeighborOffsetIds[4], new Vector4(-colX * 0.5f, -rowY)); // SW
            mat.SetVector(NeighborOffsetIds[5], new Vector4(colX * 0.5f, -rowY));  // SE
        }

        // Feeds each of the up to 6 neighboring tiles' art into the shared blend material so
        // HexSeamlessBlend.shader can fade this hex's rim toward them. Direction order (0..5 =
        // E, NE, NW, W, SW, SE) must match the shader's _NeighborTexN/_NeighborUVN/_NeighborValidN.
        private void ApplyNeighborBlendProperties(Material mat, int row, int col)
        {
            // This cell's center in map space (tile-local units, y-up), for the grid rainbow hue.
            float centerX = (col + (((row & 1) == 1) ? 0.5f : 0f)) * blendColX;
            mat.SetVector(CellCenterId, new Vector4(centerX, -row * blendRowY));

            for (int direction = 0; direction < 6; direction++)
            {
                bool valid = TryGetNeighborIndex(row, col, direction, out int neighborIdx);
                Sprite neighborSprite = valid ? GetCellTerrainSprite(neighborIdx) : null;
                valid = valid && neighborSprite != null && neighborSprite.texture != null;

                mat.SetFloat(NeighborValidIds[direction], valid ? 1f : 0f);
                if (!valid) continue;

                Rect uv = SpriteTexCoords(neighborSprite);
                mat.SetTexture(NeighborTexIds[direction], GetPreviewTexture(neighborSprite.texture));
                mat.SetVector(NeighborUVIds[direction], new Vector4(uv.x, uv.y, uv.width, uv.height));
            }
        }

        // The true hex footprint, centered on 'center' — same shape DrawRegionHex has always used
        // for its placeholder fill, in pointy-top-row-packing terms (halfH = stepY * 2/3). This is
        // noticeably smaller than the overdrawn per-tile art rect (drawW/drawH, see TileOverdraw)
        // that terrain art is drawn into: that rect exists purely so neighboring tiles' art shows
        // through the hex mask at the edges, and is roughly 2x too tall to anchor UI against —
        // anything positioned against it (corner badges, the selection outline) reads as offset by
        // most of a row. Everything hex-shaped (selection outline, corner badges, character
        // tokens) should size/anchor against THIS box instead.
        private static Vector3[] BuildHexPoints(Vector2 center, float stepX, float stepY)
        {
            float halfW = stepX * 0.5f;
            float halfH = stepY * (2f / 3f);
            return new[]
            {
                new Vector3(center.x,         center.y - halfH),
                new Vector3(center.x + halfW, center.y - halfH * 0.5f),
                new Vector3(center.x + halfW, center.y + halfH * 0.5f),
                new Vector3(center.x,         center.y + halfH),
                new Vector3(center.x - halfW, center.y + halfH * 0.5f),
                new Vector3(center.x - halfW, center.y - halfH * 0.5f),
            };
        }

        // Draws each spawned character's baked idle sprite (standing idle, Forward-facing, frame
        // 0 — same art the game shows, no animation here) as a token centered on the hex. Several
        // characters on one hex are spread left/right of center so none fully occludes another; a
        // character with no baked spritesheet (nothing under AnimationSpritesheets for its name or
        // race) is silently skipped rather than drawn as a blank/placeholder square. Drawn straight
        // off the sprite's own full-resolution texture (DrawCharacterToken), NOT through
        // DrawSprite/GetPreviewTexture: that cache downsamples to fit a whole texture within 512px,
        // which is fine for a single ~1000px hex tile but turns a baked character atlas (every
        // animation frame packed into one sheet, easily several thousand pixels per side) into a
        // blurry handful-of-pixels-per-frame mess once cropped back down to one frame.
        private void DrawCellCharacters(Rect r, float stepX, float stepY, int row, int col)
        {
            int idx = Index(row, col);
            if (!characters.TryGetValue(idx, out List<ScenarioCharacter> list) || list.Count == 0) return;

            // Sized against the true hex box (see BuildHexPoints), not stepX/stepY directly —
            // stepY alone is much smaller than the hex actually is (rows interlock at ~51% of a
            // tile's height), which was making tokens read as tiny. Allowed to run a little taller
            // than the hex box itself (characters read fine standing "on" a hex while overhanging
            // its edges slightly — same as how the game itself draws them).
            float hexW = stepX;
            float hexH = stepY * (4f / 3f);
            float tokenSize = Mathf.Min(hexW, hexH) * (list.Count > 1 ? 0.85f : 1.15f);
            float spacing = tokenSize * 0.75f;
            float totalWidth = spacing * (list.Count - 1);
            float startX = r.center.x - totalWidth * 0.5f;

            for (int i = 0; i < list.Count; i++)
            {
                ScenarioCharacter c = list[i];
                if (c == null || string.IsNullOrWhiteSpace(c.characterName)) continue;

                Sprite sprite = ScenarioCardCatalog.GetCharacterIdleSprite(c.characterName);
                if (sprite == null) continue;

                float cx = list.Count == 1 ? r.center.x : startX + i * spacing;
                Rect tokenRect = new Rect(cx - tokenSize * 0.5f, r.center.y - tokenSize * 0.5f, tokenSize, tokenSize);
                DrawCharacterToken(tokenRect, sprite);
            }
        }

        // Full-resolution sprite draw, bypassing the GetPreviewTexture downsample cache — see
        // DrawCellCharacters for why that cache is wrong for baked character atlases.
        private static void DrawCharacterToken(Rect r, Sprite sprite)
        {
            if (sprite == null || sprite.texture == null) return;
            GUI.DrawTextureWithTexCoords(r, sprite.texture, SpriteTexCoords(sprite), true);
        }

        private void DrawCellOverlay(Rect r, float stepX, float stepY, int row, int col)
        {
            int idx = Index(row, col);

            bool hasLeader = characters.TryGetValue(idx, out var overlayList) && overlayList.Any(IsLeaderCard);
            bool hasPc = pcs.ContainsKey(idx);
            int charCount = characters.TryGetValue(idx, out var list) ? list.Count : 0;
            int artifactCount = objects.TryGetValue(idx, out var artifactList) ? artifactList.Count : 0;

            // Badges anchor at explicit points inset from center, NOT at the corners of a
            // bounding rect over the hex (as a naive TextAnchor.UpperLeft/etc against a rect
            // sized to the true hex box would do) — a pointy-top hex tapers to a single point at
            // its top/bottom, so a rect's actual corners sit in the gap between hexes, not inside
            // this one. halfW/halfH*0.5 is the hex's flat-sided band (see BuildHexPoints); insets
            // stay well within it so badges always land visibly on this hex's own art.
            float halfW = stepX * 0.5f;
            float halfH = stepY * (2f / 3f);
            float badgeX = halfW * 0.55f;
            float badgeY = halfH * 0.42f;

            if (hasLeader) DrawBadge(new Vector2(r.center.x - badgeX, r.center.y - badgeY), "★", new Color(1f, 0.85f, 0.1f));
            if (hasPc) DrawBadge(new Vector2(r.center.x + badgeX, r.center.y - badgeY), "⌂", Color.white);
            if (charCount > 0) DrawBadge(new Vector2(r.center.x + badgeX, r.center.y + badgeY), charCount.ToString(), Color.cyan);
            if (artifactCount > 0) DrawBadge(new Vector2(r.center.x - badgeX, r.center.y + badgeY), "✦", new Color(0.85f, 0.4f, 1f));

            // PC name label, centred just below the hex's middle band (not at the very bottom
            // point, for the same reason the corner badges above moved off the bounding rect).
            if (hasPc && pcs.TryGetValue(idx, out ScenarioPC pc) && !string.IsNullOrEmpty(pc.pcName))
            {
                Rect captionRect = new Rect(r.center.x - halfW * 0.8f, r.center.y + halfH * 0.35f, halfW * 1.6f, halfH * 0.5f);
                DrawHexCaption(captionRect, pc.pcName);
            }

            if (idx == selectedIndex)
            {
                Vector3[] hex = BuildHexPoints(r.center, stepX, stepY);
                Handles.BeginGUI();
                Handles.color = Color.red;
                Handles.DrawAAPolyLine(3f, hex[0], hex[1], hex[2], hex[3], hex[4], hex[5], hex[0]);
                Handles.EndGUI();
            }
        }

        // Small fixed-size, center-anchored marker (leader star / PC glyph / character count) —
        // deliberately NOT corner-anchored against a bounding rect, see DrawCellOverlay.
        private static void DrawBadge(Vector2 center, string text, Color color)
        {
            const float size = 16f;
            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                normal = { textColor = color }
            };
            GUI.Label(new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size), text, style);
        }

        // Region-tool-only hex placeholder: a plain per-terrain fallback color (grass/water/etc,
        // see TerrainFallbackColor) sized to the packed cell spacing (stepX/stepY) rather than the
        // overdrawn art rect, so it stays inside its own hex instead of bleeding into neighbors.
        // The region's color and initial letter are drawn on top when one is assigned.
        private static void DrawRegionHex(Rect draw, float stepX, float stepY, TerrainEnum terrainType, string region)
        {
            Vector2 center = draw.center;
            float halfW = stepX * 0.5f;
            float halfH = stepY * (2f / 3f);
            Vector3[] hex = BuildHexPoints(center, stepX, stepY);

            Handles.BeginGUI();
            Handles.color = TerrainFallbackColor(terrainType);
            Handles.DrawAAConvexPolygon(hex);

            if (!string.IsNullOrEmpty(region))
            {
                Color tint = RegionColor(region);
                tint.a = 0.6f;
                Handles.color = tint;
                Handles.DrawAAConvexPolygon(hex);
            }

            Handles.color = new Color(0f, 0f, 0f, 0.35f);
            Handles.DrawPolyLine(hex[0], hex[1], hex[2], hex[3], hex[4], hex[5], hex[0]);
            Handles.EndGUI();

            if (!string.IsNullOrEmpty(region))
            {
                string letters = RegionInitials(region);
                var style = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = Mathf.Clamp(Mathf.RoundToInt(halfH * 0.7f / Mathf.Max(1f, letters.Length * 0.55f)), 8, 22),
                    normal = { textColor = Color.black }
                };
                var labelRect = new Rect(center.x - halfW, center.y - halfH, halfW * 2f, halfH * 2f);

                var shadowStyle = new GUIStyle(style) { normal = { textColor = new Color(1f, 1f, 1f, 0.85f) } };
                GUI.Label(new Rect(labelRect.x + 1, labelRect.y + 1, labelRect.width, labelRect.height), letters, shadowStyle);
                GUI.Label(labelRect, letters, style);
            }
        }

        // Takes one letter per capitalized word in a PascalCase region name (e.g. "TheNorthDowns"
        // -> "TND") instead of just the first letter, since many region names share the same
        // leading word ("The...") and would otherwise all show the same disambiguating letter.
        private static string RegionInitials(string region)
        {
            string initials = "";
            for (int i = 0; i < region.Length; i++)
                if (i == 0 || char.IsUpper(region[i])) initials += char.ToUpperInvariant(region[i]);
            return initials;
        }

        // Centred caption (PC name) drawn near the bottom of a hex, with a dark backing strip
        // and a 1px text shadow so it stays legible over any tile artwork.
        private static void DrawHexCaption(Rect r, string text)
        {
            var style = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 9,
                wordWrap = false,
                clipping = TextClipping.Clip
            };

            float h = 13f;
            var band = new Rect(r.x + 1, r.yMax - h - 2, r.width - 2, h);
            EditorGUI.DrawRect(band, new Color(0f, 0f, 0f, 0.55f));

            var shadow = new Rect(band.x + 1, band.y + 1, band.width, band.height);
            style.normal.textColor = new Color(0f, 0f, 0f, 0.9f);
            GUI.Label(shadow, text, style);
            style.normal.textColor = Color.white;
            GUI.Label(band, text, style);
        }


        private void HandleGridMouse(Rect content, float cellW, float cellH)
        {
            Event e = Event.current;

            // Right mouse button drags to pan the view — it never edits or erases hexes.
            if (e.button == 1 && content.Contains(e.mousePosition) &&
                (e.type == EventType.MouseDown || e.type == EventType.MouseDrag))
            {
                if (e.type == EventType.MouseDrag)
                {
                    gridScroll -= e.delta;
                    Repaint();
                }
                e.Use();
                return;
            }

            if (e.type != EventType.MouseDown && e.type != EventType.MouseDrag) return;
            if (e.button != 0 || !content.Contains(e.mousePosition)) return;

            if (!PickHex(content, e.mousePosition, cellW, cellH, out int row, out int col)) return;

            if (tool == Tool.Paint || tool == Tool.Region)
            {
                ApplyBrush(row, col);
                e.Use();
                Repaint();
            }
            else if (e.type == EventType.MouseDown)
            {
                selectedIndex = Index(row, col);
                e.Use();
                Repaint();
            }
        }

        private bool PickHex(Rect content, Vector2 mouse, float cellW, float cellH, out int row, out int col)
        {
            row = Mathf.FloorToInt((mouse.y - content.y) / cellH);
            float rowOffset = (row & 1) == 1 ? cellW * 0.5f : 0f;
            col = Mathf.FloorToInt((mouse.x - content.x - rowOffset) / cellW);
            return InBounds(row, col);
        }

        private void ApplyBrush(int centerRow, int centerCol)
        {
            Vector3Int center = OffsetToCube(centerRow, centerCol);
            int radius = brushSize - 1;
            string blockedByRegion = null;

            for (int row = 0; row < height; row++)
            {
                for (int col = 0; col < width; col++)
                {
                    if (CubeDistance(OffsetToCube(row, col), center) > radius) continue;
                    int idx = Index(row, col);

                    if (tool == Tool.Region)
                    {
                        // Region-only brush: change the region of existing hexes, never the terrain.
                        string targetRegion = regionBrushRegion ?? "";
                        if (TryGetPcLockedRegion(idx, out string locked) && locked != targetRegion)
                        {
                            blockedByRegion ??= locked;
                            continue;
                        }
                        regions[idx] = targetRegion;
                        continue;
                    }

                    // Terrain brush: terrain + chosen tile variation, and region if one is selected.
                    terrain[idx] = paintTerrain;
                    spriteNames[idx] = paintSpriteName ?? "";
                    if (!string.IsNullOrEmpty(paintRegion))
                    {
                        if (TryGetPcLockedRegion(idx, out string locked) && locked != paintRegion)
                            blockedByRegion ??= locked;
                        else
                            regions[idx] = paintRegion;
                    }
                }
            }

            if (blockedByRegion != null)
                ShowNotification(new GUIContent(
                    $"The PC card has a region set to {blockedByRegion}. You can't set another region unless you change the card region."));
        }

        // A hex whose PC card carries a region is locked to that region — the card is the source
        // of truth once a PC sits there, so brushes can't paint it away from underneath the card.
        private bool TryGetPcLockedRegion(int idx, out string region)
        {
            region = null;
            if (!pcs.TryGetValue(idx, out ScenarioPC pc) || string.IsNullOrEmpty(pc.pcName)) return false;
            CardData card = ScenarioCardCatalog.GetCard(pc.pcName);
            if (card == null || string.IsNullOrEmpty(card.region)) return false;
            region = card.region;
            return true;
        }

        // -------------------------------------------------------------------------------------
        // Magnifier inspector
        // -------------------------------------------------------------------------------------
        private void DrawInspector()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(330));
            inspectorScroll = EditorGUILayout.BeginScrollView(inspectorScroll);

            if (selectedIndex < 0 || tool != Tool.Magnifier)
            {
                EditorGUILayout.HelpBox("Select the Magnifier tool and click a hex to edit it.", MessageType.Info);
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                return;
            }

            int row = selectedIndex / width;
            int col = selectedIndex % width;
            EditorGUILayout.LabelField($"Hex ({row}, {col})", EditorStyles.boldLabel);

            // Tile preview for the selected hex.
            EditorGUILayout.BeginHorizontal();
            Rect tile = GUILayoutUtility.GetRect(56, 56, GUILayout.Width(56), GUILayout.Height(56));
            Sprite hexSprite = !string.IsNullOrEmpty(spriteNames[selectedIndex])
                ? ScenarioCardCatalog.GetTerrainSpriteByName(spriteNames[selectedIndex])
                : ScenarioCardCatalog.GetTerrainSprite(terrain[selectedIndex]);
            DrawSprite(tile, hexSprite);
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("Terrain", terrain[selectedIndex].ToString());
            EditorGUILayout.LabelField("Tile", string.IsNullOrEmpty(spriteNames[selectedIndex]) ? "(default)" : spriteNames[selectedIndex]);
            EditorGUILayout.LabelField("Region", string.IsNullOrEmpty(regions[selectedIndex]) ? "(none)" : regions[selectedIndex]);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            DrawPcSection(selectedIndex, row, col);
            EditorGUILayout.Space();
            DrawCharactersSection(selectedIndex, row, col);
            EditorGUILayout.Space();
            DrawObjectsSection(selectedIndex, row, col);

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        // A character entry is a leader's own card — not a companion — when it is self-owned
        // (ownerLeaderName == characterName). Its mere presence at a hex is that leader's starting
        // position; there is no separate "leader start" record. Blank/blank never counts, so a
        // freshly-added character defaults to a normal companion.
        private static bool IsLeaderCard(ScenarioCharacter c) =>
            c != null && !string.IsNullOrWhiteSpace(c.characterName) &&
            string.Equals(c.characterName, c.ownerLeaderName, StringComparison.OrdinalIgnoreCase);

        // Every characterName already placed on the map, anywhere, other than 'excluding' itself —
        // used to star already-spawned names in the Name picker so the author can spot accidental
        // duplicates (e.g. re-placing the same unique leader/companion at a second hex).
        private HashSet<string> SpawnedCharacterNames(ScenarioCharacter excluding)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (List<ScenarioCharacter> list in characters.Values)
            {
                foreach (ScenarioCharacter other in list)
                {
                    if (other == excluding || other == null || string.IsNullOrWhiteSpace(other.characterName)) continue;
                    names.Add(other.characterName);
                }
            }
            return names;
        }

        private static bool IsKnownLeaderName(string name) =>
            !string.IsNullOrWhiteSpace(name) &&
            (ScenarioCardCatalog.PlayableLeaders.Contains(name, StringComparer.OrdinalIgnoreCase) ||
             ScenarioCardCatalog.NonPlayableLeaders.Contains(name, StringComparer.OrdinalIgnoreCase));

        // The Name picker offers both ordinary Character cards and every leader name in one list,
        // so picking "Thranduil" needs no separate mode — it's just a name that happens to match a
        // known leader.
        private static List<string> NameAndLeaderPool() =>
            ScenarioCardCatalog.CharacterCards
                .Concat(ScenarioCardCatalog.AllLeaders())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n)
                .ToList();

        // Lets the author pick which variant this self-owned leader card represents. Each card is
        // its own carousel entry (see NationSpawner/LeaderSelector) — "Base (no variant)" just
        // means this hex shows as the plain, unflavored entry, not "every variant is offered here."
        private void DrawLeaderVariantPicker(ScenarioCharacter c)
        {
            IReadOnlyList<LeaderVariantConfig> variants = ScenarioCardCatalog.GetPlayableLeaderVariants(c.characterName);
            if (variants == null || variants.Count == 0)
            {
                c.variantId = "";
                EditorGUILayout.HelpBox("This leader has no variants.", MessageType.None);
                return;
            }

            string[] labels = new string[variants.Count + 1];
            labels[0] = "Base (no variant)";
            int selected = 0;
            for (int i = 0; i < variants.Count; i++)
            {
                LeaderVariantConfig v = variants[i];
                string display = string.IsNullOrWhiteSpace(v.displayName) ? v.variantId : v.displayName;
                labels[i + 1] = $"{display}  ({v.variantId})";
                if (!string.IsNullOrEmpty(c.variantId) &&
                    string.Equals(v.variantId, c.variantId, StringComparison.OrdinalIgnoreCase))
                    selected = i + 1;
            }

            int chosen = EditorGUILayout.Popup("Variant", selected, labels);
            c.variantId = chosen <= 0 ? "" : variants[chosen - 1].variantId;

            EditorGUILayout.HelpBox(
                chosen <= 0
                    ? $"This hex shows as the base (unflavored) {c.characterName} entry in the selection carousel."
                    : $"This hex shows as the '{labels[chosen]}' entry for {c.characterName} in the selection carousel.",
                MessageType.Info);

            ScenarioCharacter duplicate = FindDuplicateLeaderVariant(c);
            if (duplicate != null)
            {
                string variantLabel = string.IsNullOrEmpty(c.variantId) ? "Base (no variant)" : c.variantId;
                EditorGUILayout.HelpBox(
                    $"Another {c.characterName} card at ({duplicate.row},{duplicate.col}) already uses '{variantLabel}' — " +
                    "the carousel can't tell these two hexes apart. Give one of them a different variant.",
                    MessageType.Warning);
            }
        }

        // Scans every hex's characters for another self-owned card of the same leader claiming the
        // same variant (including two cards both left at Base) — a real authoring collision, since
        // the carousel would show two indistinguishable entries pointing at different hexes.
        private ScenarioCharacter FindDuplicateLeaderVariant(ScenarioCharacter c)
        {
            foreach (List<ScenarioCharacter> list in characters.Values)
            {
                foreach (ScenarioCharacter other in list)
                {
                    if (other == c || !IsLeaderCard(other)) continue;
                    if (!string.Equals(other.characterName, c.characterName, StringComparison.OrdinalIgnoreCase)) continue;
                    if (string.Equals(other.variantId ?? "", c.variantId ?? "", StringComparison.OrdinalIgnoreCase)) return other;
                }
            }
            return null;
        }

        // Shown next to the Owner field for PCs/Characters owned by a playable leader: lets the
        // author lock ownership to a single variant, or leave it at "Base" so the entity stays
        // owned regardless of which variant (or the base leader) ends up chosen at the
        // leader-selection screen. See NationSpawner.ReconcileScenarioVariantOwnership for the
        // runtime rule this drives.
        private void DrawOwnerVariantPicker(string ownerLeaderName, Func<string> getVariantId, Action<string> setVariantId,
            Func<string> getFallbackOwnerName, Action<string> setFallbackOwnerName)
        {
            if (string.IsNullOrWhiteSpace(ownerLeaderName) ||
                !ScenarioCardCatalog.PlayableLeaders.Contains(ownerLeaderName, StringComparer.OrdinalIgnoreCase))
            {
                setVariantId("");
                setFallbackOwnerName("");
                return;
            }

            IReadOnlyList<LeaderVariantConfig> variants = ScenarioCardCatalog.GetPlayableLeaderVariants(ownerLeaderName);
            if (variants == null || variants.Count == 0)
            {
                setVariantId("");
                setFallbackOwnerName("");
                return;
            }

            string currentVariantId = getVariantId();
            string[] labels = new string[variants.Count + 1];
            labels[0] = "Base (owner regardless of variant)";
            int selected = 0;
            for (int i = 0; i < variants.Count; i++)
            {
                LeaderVariantConfig v = variants[i];
                string display = string.IsNullOrWhiteSpace(v.displayName) ? v.variantId : v.displayName;
                labels[i + 1] = $"{display}  ({v.variantId})";
                if (!string.IsNullOrEmpty(currentVariantId) &&
                    string.Equals(v.variantId, currentVariantId, StringComparison.OrdinalIgnoreCase))
                    selected = i + 1;
            }

            int chosen = EditorGUILayout.Popup("Owner Variant", selected, labels);
            setVariantId(chosen <= 0 ? "" : variants[chosen - 1].variantId);

            if (chosen <= 0)
            {
                setFallbackOwnerName("");
                EditorGUILayout.HelpBox(
                    $"Stays owned by {ownerLeaderName} whichever variant (or the base leader) is chosen.",
                    MessageType.Info);
                return;
            }

            // "(none)" (SearchableField/ScenarioSearchPopup's built-in clear option) means
            // fallbackOwnerName stays empty, i.e. destroy on mismatch — no separate mode toggle
            // needed, so there's nothing for a stray repaint to reset before a name is picked.
            string currentFallback = getFallbackOwnerName();
            SearchableField("If not selected", currentFallback, ScenarioCardCatalog.NonPlayableLeaders, setFallbackOwnerName);
            EditorGUILayout.HelpBox(
                string.IsNullOrWhiteSpace(currentFallback)
                    ? $"Only owned by {ownerLeaderName} if '{labels[chosen]}' is the variant actually chosen — otherwise this placement is destroyed when the game starts. Pick a Non-Playable Leader above to reassign it instead."
                    : $"Only owned by {ownerLeaderName} if '{labels[chosen]}' is chosen — otherwise reassigned to {currentFallback} instead of being destroyed.",
                MessageType.Info);
        }

        private static readonly string[] SpawnConditionModeLabels = { "Requires", "Excludes" };

        // Independent spawn gate shared by companion characters and armies: only created if the
        // named playable leader (any leader in the scenario, not necessarily this entity's owner)
        // is (Requires) or isn't (Excludes) playing with the given variant. Empty leader name =
        // always spawn. See NationSpawner.ReconcileScenarioSpawnConditions for the runtime rule.
        private void DrawSpawnConditionPicker(string title, Func<string> getLeaderName, Action<string> setLeaderName,
            Func<string> getVariantId, Action<string> setVariantId, Func<bool> getExclude, Action<bool> setExclude)
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            SearchableField("Required leader", getLeaderName(), ScenarioCardCatalog.PlayableLeaders, v =>
            {
                setLeaderName(v);
                setVariantId("");
            });

            string leaderName = getLeaderName();
            if (string.IsNullOrWhiteSpace(leaderName))
            {
                setVariantId("");
                EditorGUILayout.HelpBox("Always spawns.", MessageType.None);
                return;
            }

            bool exclude = EditorGUILayout.Popup("Mode", getExclude() ? 1 : 0, SpawnConditionModeLabels) == 1;
            setExclude(exclude);

            IReadOnlyList<LeaderVariantConfig> variants = ScenarioCardCatalog.GetPlayableLeaderVariants(leaderName);
            if (variants == null || variants.Count == 0)
            {
                setVariantId("");
                EditorGUILayout.HelpBox(
                    exclude
                        ? $"Only spawns if {leaderName} is NOT in play (that leader has no variants)."
                        : $"Only spawns if {leaderName} is in play (that leader has no variants).",
                    MessageType.Info);
                return;
            }

            string currentVariantId = getVariantId();
            string[] labels = new string[variants.Count + 1];
            labels[0] = "Base (no variant)";
            int selected = 0;
            for (int i = 0; i < variants.Count; i++)
            {
                LeaderVariantConfig v = variants[i];
                string display = string.IsNullOrWhiteSpace(v.displayName) ? v.variantId : v.displayName;
                labels[i + 1] = $"{display}  ({v.variantId})";
                if (!string.IsNullOrEmpty(currentVariantId) &&
                    string.Equals(v.variantId, currentVariantId, StringComparison.OrdinalIgnoreCase))
                    selected = i + 1;
            }

            int chosen = EditorGUILayout.Popup("Required variant", selected, labels);
            setVariantId(chosen <= 0 ? "" : variants[chosen - 1].variantId);

            EditorGUILayout.HelpBox(
                exclude
                    ? $"Only spawns if {leaderName} does NOT end up as '{labels[chosen]}' (absent entirely also satisfies this). No fallback — otherwise this is simply never created."
                    : $"Only spawns if {leaderName} ends up as '{labels[chosen]}'. No fallback — otherwise this is simply never created.",
                MessageType.Info);
        }

        private void DrawPcSection(int idx, int row, int col)
        {
            EditorGUILayout.LabelField("PC (City)", EditorStyles.boldLabel);
            bool has = pcs.TryGetValue(idx, out ScenarioPC pc);
            bool newHas = EditorGUILayout.Toggle("Has a PC", has);

            if (newHas && !has)
            {
                pc = new ScenarioPC { row = row, col = col, region = regions[idx] ?? "" };
                pcs[idx] = pc;
                has = true;
            }
            else if (!newHas && has)
            {
                pcs.Remove(idx);
                return;
            }
            if (!has) return;

            SearchableField("Name", pc.pcName, ScenarioCardCatalog.PcCards, v =>
            {
                pc.pcName = v;
                // Default the underground flag from the PC card so authored placements match the card.
                CardData pcCard = ScenarioCardCatalog.GetCard(v);
                if (pcCard != null) pc.isUnderground = pcCard.isUnderground;

                // The PC card's region is authoritative once a PC sits on a hex — lock the hex to
                // it so scenario data can't drift from the card the way past placements did.
                if (pcCard != null && !string.IsNullOrEmpty(pcCard.region))
                {
                    regions[idx] = pcCard.region;
                    pc.region = pcCard.region;
                }
            }, ScenarioCardCatalog.GetCard);
            SearchableField("Owner", pc.ownerLeaderName, ScenarioCardCatalog.AllLeaders(), v => pc.ownerLeaderName = v);
            DrawOwnerVariantPicker(pc.ownerLeaderName, () => pc.ownerVariantId, v => pc.ownerVariantId = v,
                () => pc.fallbackOwnerName, v => pc.fallbackOwnerName = v);
            pc.citySize = (int)(PCSizeEnum)EditorGUILayout.EnumPopup("Size", (PCSizeEnum)pc.citySize);
            pc.fortSize = (int)(FortSizeEnum)EditorGUILayout.EnumPopup("Fort", (FortSizeEnum)pc.fortSize);
            pc.hasPort = EditorGUILayout.Toggle("Has port", pc.hasPort);
            pc.isHidden = EditorGUILayout.Toggle("Hidden", pc.isHidden);
            pc.isCapital = EditorGUILayout.Toggle("Capital", pc.isCapital);
            pc.isUnderground = EditorGUILayout.Toggle("Underground", pc.isUnderground);
            pc.isIsland = EditorGUILayout.Toggle("Island", pc.isIsland);
            pc.loyalty = EditorGUILayout.IntSlider("Loyalty", pc.loyalty, 0, 100);

            DrawCardWithDecks(pc.pcName);
        }

        // Full card render (real Card prefab, offscreen camera) plus the list of decks that
        // contain the card, shown under the magnifier's PC / character / army pickers.
        private void DrawCardWithDecks(string cardName)
        {
            if (string.IsNullOrWhiteSpace(cardName)) return;

            CardData card = ScenarioCardCatalog.GetCard(cardName);
            if (card == null)
            {
                EditorGUILayout.HelpBox($"No card found for '{cardName}'.", MessageType.None);
                return;
            }

            EditorGUILayout.Space(4);
            cardRenderer ??= new ScenarioCardPreviewRenderer();
            Texture2D tex = cardRenderer.Render(card);

            const float previewW = 290f;
            float previewH = previewW * ScenarioCardPreviewRenderer.CanvasH / ScenarioCardPreviewRenderer.CanvasW;
            Rect r = GUILayoutUtility.GetRect(previewW, previewH, GUILayout.Width(previewW), GUILayout.Height(previewH));
            if (tex != null)
            {
                GUI.DrawTexture(r, tex, ScaleMode.ScaleToFit, false);
            }
            else
            {
                EditorGUI.DrawRect(r, new Color(0.15f, 0.15f, 0.15f));
                GUI.Label(r, "(card preview unavailable)", EditorStyles.centeredGreyMiniLabel);
            }

            IReadOnlyList<string> decks = ScenarioCardCatalog.GetDecksContaining(cardName);
            EditorGUILayout.LabelField(
                decks.Count == 0 ? "Decks: (none)" : "Decks: " + string.Join(", ", decks),
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(4);
        }

        private void DrawCharactersSection(int idx, int row, int col)
        {
            EditorGUILayout.LabelField("Characters", EditorStyles.boldLabel);
            if (!characters.TryGetValue(idx, out List<ScenarioCharacter> list))
            {
                list = new List<ScenarioCharacter>();
            }

            int removeAt = -1;
            for (int i = 0; i < list.Count; i++)
            {
                ScenarioCharacter c = list[i];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Character {i + 1}", EditorStyles.boldLabel);
                if (GUILayout.Button("Remove", GUILayout.Width(70))) removeAt = i;
                EditorGUILayout.EndHorizontal();

                // Naming a card after a known leader (playable or non-playable) makes it that
                // leader's own card by default — no separate flag to set. Change Owner below to a
                // different leader afterward only if you specifically want a subordinate that
                // happens to share the name.
                SearchableField("Name", c.characterName, NameAndLeaderPool(), v =>
                {
                    c.characterName = v;
                    if (IsKnownLeaderName(v)) c.ownerLeaderName = v;
                    c.variantId = "";
                }, ScenarioCardCatalog.GetCard, SpawnedCharacterNames(c));
                SearchableField("Owner", c.ownerLeaderName, ScenarioCardCatalog.AllLeaders(), v => c.ownerLeaderName = v);

                bool isLeaderCard = IsLeaderCard(c);
                if (isLeaderCard)
                {
                    // Playable vs non-playable is never authored — it's just which JSON the name
                    // came from — so show it as a read-only badge (gold/silver), not a toggle.
                    bool isPlayable = ScenarioCardCatalog.PlayableLeaders.Contains(c.characterName, StringComparer.OrdinalIgnoreCase);
                    Color prevColor = GUI.color;
                    GUI.color = isPlayable ? new Color(0.85f, 0.7f, 0.2f) : new Color(0.75f, 0.78f, 0.82f);
                    EditorGUILayout.LabelField(isPlayable ? "★ Playable Leader" : "★ Non-Playable Leader", EditorStyles.boldLabel);
                    GUI.color = prevColor;

                    // Only playable leaders have variants to restrict the selection carousel to.
                    if (isPlayable)
                    {
                        DrawLeaderVariantPicker(c);
                        // A playable leader/variant card's presence is governed by the selection
                        // carousel + PruneUnselectedLeaderVariants — gating it on an independent
                        // spawnCondition too would race that lifecycle (it could vanish AFTER
                        // already being offered/picked in the carousel), so it's deliberately not
                        // offered here.
                    }
                    else
                    {
                        c.variantId = "";
                        // Non-playable self-owned leader cards (e.g. Faramir) have no carousel or
                        // pruning to race — safe to gate the whole identity on a spawnCondition.
                        DrawSpawnConditionPicker("Spawn Condition", () => c.spawnConditionLeaderName, v => c.spawnConditionLeaderName = v,
                            () => c.spawnConditionVariantId, v => c.spawnConditionVariantId = v,
                            () => c.spawnConditionExclude, v => c.spawnConditionExclude = v);
                    }
                }
                else
                {
                    DrawOwnerVariantPicker(c.ownerLeaderName, () => c.ownerVariantId, v => c.ownerVariantId = v,
                        () => c.fallbackOwnerName, v => c.fallbackOwnerName = v);
                    DrawSpawnConditionPicker("Spawn Condition", () => c.spawnConditionLeaderName, v => c.spawnConditionLeaderName = v,
                        () => c.spawnConditionVariantId, v => c.spawnConditionVariantId = v,
                        () => c.spawnConditionExclude, v => c.spawnConditionExclude = v);
                }
                DrawCardWithDecks(c.characterName);

                DrawArmyEditor(c);
                DrawStartingObjectsEditor(c);

                EditorGUILayout.EndVertical();
            }

            if (removeAt >= 0)
            {
                list.RemoveAt(removeAt);
                SyncCharacterList(idx, list);
            }

            if (GUILayout.Button("Add Character"))
            {
                list.Add(new ScenarioCharacter { row = row, col = col });
                SyncCharacterList(idx, list);
            }
        }

        private void DrawArmyEditor(ScenarioCharacter c)
        {
            bool bearsArmy = c.army != null;
            bool newBears = EditorGUILayout.Toggle("Bears an army", bearsArmy);
            if (newBears && !bearsArmy)
            {
                c.army = new ScenarioArmy();
                bearsArmy = true;
            }
            else if (!newBears && bearsArmy)
            {
                c.army = null;
                return;
            }
            if (!bearsArmy) return;

            c.army.xp = EditorGUILayout.IntSlider("XP", c.army.xp, 0, 100);
            DrawSpawnConditionPicker("Army Spawn Condition", () => c.army.spawnConditionLeaderName, v => c.army.spawnConditionLeaderName = v,
                () => c.army.spawnConditionVariantId, v => c.army.spawnConditionVariantId = v,
                () => c.army.spawnConditionExclude, v => c.army.spawnConditionExclude = v);

            int removeStackAt = -1;
            for (int i = 0; i < c.army.stacks.Count; i++)
            {
                ScenarioArmyStack stack = c.army.stacks[i];
                EditorGUILayout.BeginHorizontal();
                ScenarioArmyStack capturedStack = stack;
                SearchableField("", capturedStack.armyCardName, ScenarioCardCatalog.ArmyCards, v => capturedStack.armyCardName = v, ScenarioCardCatalog.GetCard);
                stack.amount = EditorGUILayout.IntField(stack.amount, GUILayout.Width(60));
                if (GUILayout.Button("x", GUILayout.Width(22))) removeStackAt = i;
                EditorGUILayout.EndHorizontal();
            }
            if (removeStackAt >= 0) c.army.stacks.RemoveAt(removeStackAt);

            if (GUILayout.Button("Add Army Card"))
                c.army.stacks.Add(new ScenarioArmyStack { amount = 100 });

            // One rendered card per distinct army card in the stacks.
            foreach (string armyCardName in c.army.stacks
                         .Select(s => s.armyCardName)
                         .Where(n => !string.IsNullOrWhiteSpace(n))
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                DrawCardWithDecks(armyCardName);
            }
        }

        // Object cards this character starts holding — every scenario character (leader,
        // NPL, or companion) can carry objects; there's no automatic leader starting kit
        // anymore (see ScenarioData.CurrentVersion v12 note).
        private void DrawStartingObjectsEditor(ScenarioCharacter c)
        {
            c.startingObjects ??= new List<string>();
            EditorGUILayout.LabelField("Starting Objects", EditorStyles.boldLabel);

            int removeAt = -1;
            for (int i = 0; i < c.startingObjects.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                int captured = i;
                SearchableField("", c.startingObjects[i], ScenarioCardCatalog.ObjectCardNames,
                    v => c.startingObjects[captured] = v, ScenarioCardCatalog.GetCard);
                if (GUILayout.Button("x", GUILayout.Width(22))) removeAt = i;
                EditorGUILayout.EndHorizontal();
            }
            if (removeAt >= 0) c.startingObjects.RemoveAt(removeAt);

            if (GUILayout.Button("Add Starting Object"))
                c.startingObjects.Add(string.Empty);
        }

        private void SyncCharacterList(int idx, List<ScenarioCharacter> list)
        {
            if (list.Count == 0) characters.Remove(idx);
            else characters[idx] = list;
        }

        // Pins named hidden objects (the Object-card catalog's random-placement pool) to this
        // hex — see Board.PlaceScenarioObjects. Any hidden object left unpinned still lands on a
        // random hex exactly as before, so this section is purely additive/optional per scenario.
        private void DrawObjectsSection(int idx, int row, int col)
        {
            EditorGUILayout.LabelField("Objects", EditorStyles.boldLabel);
            if (!objects.TryGetValue(idx, out List<ScenarioObject> list))
            {
                list = new List<ScenarioObject>();
            }

            int removeAt = -1;
            for (int i = 0; i < list.Count; i++)
            {
                ScenarioObject a = list[i];
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                SearchableField("", a.objectName, ScenarioCardCatalog.ObjectCardNames, v => a.objectName = v,
                    ScenarioCardCatalog.GetCard);
                if (GUILayout.Button("x", GUILayout.Width(22))) removeAt = i;
                EditorGUILayout.EndHorizontal();
            }

            if (removeAt >= 0)
            {
                list.RemoveAt(removeAt);
                SyncObjectList(idx, list);
            }

            if (GUILayout.Button("Add Object"))
            {
                list.Add(new ScenarioObject { row = row, col = col });
                SyncObjectList(idx, list);
            }
        }

        private void SyncObjectList(int idx, List<ScenarioObject> list)
        {
            if (list.Count == 0) objects.Remove(idx);
            else objects[idx] = list;
        }

        // Search-as-you-type picker. Shows the current value on a dropdown button; clicking opens
        // ScenarioSearchPopup (with a search field + card preview); the choice is applied via the
        // callback, because the popup resolves after the click rather than inline.
        private void SearchableField(string label, string current, IReadOnlyList<string> pool, Action<string> onSelected,
            Func<string, CardData> cardResolver = null, IReadOnlyCollection<string> markedItems = null)
        {
            EditorGUILayout.BeginHorizontal();
            if (!string.IsNullOrEmpty(label)) EditorGUILayout.PrefixLabel(label);

            bool missing = !string.IsNullOrEmpty(current) && !pool.Contains(current);
            string text = string.IsNullOrEmpty(current) ? "(none)" : (missing ? current + "  (missing)" : current);
            var content = new GUIContent(text);

            Rect r = GUILayoutUtility.GetRect(content, EditorStyles.popup, GUILayout.MinWidth(120), GUILayout.ExpandWidth(true));
            if (EditorGUI.DropdownButton(r, content, FocusType.Keyboard))
            {
                var popup = new ScenarioSearchPopup(pool, chosen =>
                {
                    onSelected?.Invoke(chosen);
                    Repaint();
                }, cardResolver, markedItems);
                PopupWindow.Show(r, popup);
            }

            EditorGUILayout.EndHorizontal();
        }

        // -------------------------------------------------------------------------------------
        // Save / Load
        // -------------------------------------------------------------------------------------
        private ScenarioData ToScenarioData()
        {
            var data = new ScenarioData
            {
                scenarioName = scenarioName,
                description = scenarioDescription ?? string.Empty,
                displayTitle = scenarioDisplayTitle ?? string.Empty,
                representativeCardName = representativeCardName ?? string.Empty,
                width = width,
                height = height,
                terrain = terrain.Select(t => (int)t).ToArray(),
                pcs = pcs.Values.ToList(),
                characters = characters.Values.SelectMany(list => list).ToList(),
                objects = objects.Values.SelectMany(list => list).ToList()
            };

            for (int i = 0; i < regions.Length; i++)
            {
                if (string.IsNullOrEmpty(regions[i])) continue;
                data.regions.Add(new ScenarioRegionCell { row = i / width, col = i % width, region = regions[i] });
            }

            for (int i = 0; i < spriteNames.Length; i++)
            {
                if (string.IsNullOrEmpty(spriteNames[i])) continue;
                data.terrainSprites.Add(new ScenarioSpriteCell { row = i / width, col = i % width, spriteName = spriteNames[i] });
            }
            return data;
        }

        private void FromScenarioData(ScenarioData data)
        {
            scenarioName = data.scenarioName;
            scenarioDescription = data.description ?? string.Empty;
            scenarioDisplayTitle = data.displayTitle ?? string.Empty;
            representativeCardName = data.representativeCardName ?? string.Empty;
            width = data.width;
            height = data.height;
            terrain = new TerrainEnum[width * height];
            regions = new string[width * height];
            spriteNames = new string[width * height];
            for (int i = 0; i < terrain.Length; i++)
                terrain[i] = (i < data.terrain.Length) ? (TerrainEnum)data.terrain[i] : TerrainEnum.deepWater;

            pcs.Clear();
            characters.Clear();
            objects.Clear();
            selectedIndex = -1;

            foreach (ScenarioRegionCell cell in data.regions ?? new List<ScenarioRegionCell>())
                if (InBounds(cell.row, cell.col)) regions[Index(cell.row, cell.col)] = cell.region;

            foreach (ScenarioSpriteCell cell in data.terrainSprites ?? new List<ScenarioSpriteCell>())
                if (InBounds(cell.row, cell.col)) spriteNames[Index(cell.row, cell.col)] = cell.spriteName;

            foreach (ScenarioPC p in data.pcs ?? new List<ScenarioPC>())
                if (InBounds(p.row, p.col)) pcs[Index(p.row, p.col)] = p;

            foreach (ScenarioCharacter c in data.characters ?? new List<ScenarioCharacter>())
            {
                if (!InBounds(c.row, c.col)) continue;
                int idx = Index(c.row, c.col);
                if (!characters.TryGetValue(idx, out var list)) characters[idx] = list = new List<ScenarioCharacter>();
                list.Add(c);
            }

            foreach (ScenarioObject a in data.objects ?? new List<ScenarioObject>())
            {
                if (!InBounds(a.row, a.col)) continue;
                int idx = Index(a.row, a.col);
                if (!objects.TryGetValue(idx, out var list)) objects[idx] = list = new List<ScenarioObject>();
                list.Add(a);
            }
        }

        private void Save()
        {
            if (string.IsNullOrWhiteSpace(scenarioName))
            {
                EditorUtility.DisplayDialog("Save Scenario", "Please enter a scenario name.", "OK");
                return;
            }

            Directory.CreateDirectory(SaveFolder);
            string fileName = SanitizeFileName(scenarioName);
            string path = $"{SaveFolder}/{fileName}.json";
            File.WriteAllText(path, JsonUtility.ToJson(ToScenarioData(), true));
            UpdateIndex(fileName);
            AssetDatabase.Refresh();
            Debug.Log($"Scenario saved to {path}");
            EditorUtility.DisplayDialog("Save Scenario", $"Saved to {path}\n\nLoad it at runtime with GameConfig.ScenarioToLoad = \"{ResourceFolder}/{fileName}\".", "OK");
        }

        private void Load()
        {
            string path = EditorUtility.OpenFilePanel("Load Scenario", SaveFolder, "json");
            if (string.IsNullOrEmpty(path)) return;

            ScenarioData data = JsonUtility.FromJson<ScenarioData>(File.ReadAllText(path));
            if (data == null || data.width <= 0 || data.height <= 0)
            {
                EditorUtility.DisplayDialog("Load Scenario", "That file is not a valid scenario.", "OK");
                return;
            }
            FromScenarioData(data);
            Repaint();
        }

        // Maintains Resources/Scenarios/ScenariosIndex.json so a menu can enumerate scenarios.
        // Also prunes entries whose file no longer exists (renamed/deleted scenarios), so
        // the in-game scenario selection never offers an unloadable map.
        private void UpdateIndex(string fileName)
        {
            string indexPath = $"{SaveFolder}/ScenariosIndex.json";
            var names = new List<string>();
            if (File.Exists(indexPath))
            {
                var existing = JsonUtility.FromJson<ScenarioIndexFile>(File.ReadAllText(indexPath));
                if (existing?.scenarioNames != null) names = existing.scenarioNames;
            }
            if (!names.Contains(fileName)) names.Add(fileName);
            names.RemoveAll(n => string.IsNullOrWhiteSpace(n) || !File.Exists($"{SaveFolder}/{n}.json"));
            File.WriteAllText(indexPath, JsonUtility.ToJson(new ScenarioIndexFile { scenarioNames = names }, true));
        }

        [System.Serializable]
        private class ScenarioIndexFile
        {
            public List<string> scenarioNames = new();
        }

        private static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name.Trim();
        }

        // -------------------------------------------------------------------------------------
        // Hex math + colors
        // -------------------------------------------------------------------------------------
        private static Vector3Int OffsetToCube(int row, int col)
        {
            int x = row;
            int z = col - (row - (row & 1)) / 2;
            int y = -x - z;
            return new Vector3Int(x, y, z);
        }

        private static int CubeDistance(Vector3Int a, Vector3Int b)
        {
            return (Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) + Mathf.Abs(a.z - b.z)) / 2;
        }

        private static Color TerrainFallbackColor(TerrainEnum t) => t switch
        {
            TerrainEnum.deepWater => new Color(0.05f, 0.15f, 0.45f),
            TerrainEnum.shallowWater => new Color(0.15f, 0.45f, 0.7f),
            TerrainEnum.shore => new Color(0.85f, 0.8f, 0.5f),
            TerrainEnum.plains => new Color(0.5f, 0.7f, 0.35f),
            TerrainEnum.grasslands => new Color(0.4f, 0.65f, 0.25f),
            TerrainEnum.forest => new Color(0.15f, 0.4f, 0.18f),
            TerrainEnum.hills => new Color(0.55f, 0.5f, 0.3f),
            TerrainEnum.mountains => new Color(0.45f, 0.42f, 0.4f),
            TerrainEnum.swamp => new Color(0.3f, 0.4f, 0.3f),
            TerrainEnum.desert => new Color(0.85f, 0.75f, 0.45f),
            TerrainEnum.wastelands => new Color(0.45f, 0.3f, 0.25f),
            TerrainEnum.snow => new Color(0.92f, 0.95f, 0.98f),
            _ => Color.magenta
        };

        private static Color RegionColor(string region)
        {
            int hash = region.GetHashCode();
            UnityEngine.Random.State prev = UnityEngine.Random.state;
            UnityEngine.Random.InitState(hash);
            Color c = Color.HSVToRGB(UnityEngine.Random.value, 0.6f, 0.95f);
            UnityEngine.Random.state = prev;
            return c;
        }
    }
}
