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
        private int width = 40;
        private int height = 40;
        private TerrainEnum[] terrain;
        private string[] regions;
        private string[] spriteNames; // per-hex chosen tile variation ("" = terrain default)
        private readonly Dictionary<int, ScenarioLeaderStart> leaderStarts = new();
        private readonly Dictionary<int, ScenarioPC> pcs = new();
        private readonly Dictionary<int, List<ScenarioCharacter>> characters = new();

        // ---- Tool state ----------------------------------------------------------------------
        private Tool tool = Tool.Paint;
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

        // Spacing as a fraction of the drawn tile size, so the opaque hex art of pointy-top tiles
        // (odd rows offset in X) interlocks edge-to-edge. Footprint ~773px wide on a 974x1314 canvas
        // → ~0.79 horizontally; pointy-top rows advance ~3/4 of the tile height → ~0.51 vertically.
        private const float PackX = 0.79f;
        private const float PackY = 0.51f;

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
            width = Mathf.Clamp(w, 1, 200);
            height = Mathf.Clamp(h, 1, 200);
            terrain = new TerrainEnum[width * height];
            regions = new string[width * height];
            spriteNames = new string[width * height];
            for (int i = 0; i < terrain.Length; i++) terrain[i] = TerrainEnum.deepWater;
            leaderStarts.Clear();
            pcs.Clear();
            characters.Clear();
            selectedIndex = -1;
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
            GUILayout.FlexibleSpace();
            GUILayout.Label($"{width} x {height}", EditorStyles.toolbarButton);
            GUILayout.Label("Zoom", GUILayout.Width(34));
            zoom = GUILayout.HorizontalSlider(zoom, 0.4f, 2.5f, GUILayout.Width(90));
            GUILayout.Label("Cell W", GUILayout.Width(40));
            cellW = GUILayout.HorizontalSlider(cellW, 8f, 48f, GUILayout.Width(80));
            GUILayout.Label("Cell H", GUILayout.Width(40));
            cellH = GUILayout.HorizontalSlider(cellH, 8f, 48f, GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawLeftPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(210));

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

            // Variation picker — the chosen tile drives the hex's landmark features at load.
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
            Rect preview = GUILayoutUtility.GetRect(128, 128, GUILayout.Width(128), GUILayout.Height(128));
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
            foreach (Sprite s in variations)
            {
                if (s == null) continue;
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

        // Lists the landmark features a tile depicts (read from the sprite name via HexFeatureData),
        // with each feature's gameplay description — so the author sees what a tile grants before painting.
        private static void DrawTileFeatures(string spriteName, bool isDefault)
        {
            EditorGUILayout.LabelField("Features", EditorStyles.boldLabel);

            if (isDefault)
            {
                EditorGUILayout.LabelField("A variation is chosen at load — features vary.", EditorStyles.wordWrappedMiniLabel);
                return;
            }

            HexFeatureEnum features = HexFeatureData.GetFeatures(spriteName);
            if (features == HexFeatureEnum.None)
            {
                EditorGUILayout.LabelField("None", EditorStyles.miniLabel);
                return;
            }

            foreach ((HexFeatureEnum flag, string label) in HexFeatureData.GetPresentFeatures(features))
            {
                EditorGUILayout.LabelField($"• {label}", EditorStyles.boldLabel);
                string desc = HexFeatureData.GetFeatureDescription(flag);
                if (!string.IsNullOrEmpty(desc)) EditorGUILayout.LabelField(desc, EditorStyles.wordWrappedMiniLabel);
            }
        }

        private static void DrawSprite(Rect r, Sprite sprite)
        {
            if (sprite == null || sprite.texture == null) { EditorGUI.DrawRect(r, new Color(0.15f, 0.15f, 0.15f)); return; }
            Texture tex = sprite.texture;
            Rect tc = new(sprite.rect.x / tex.width, sprite.rect.y / tex.height,
                          sprite.rect.width / tex.width, sprite.rect.height / tex.height);
            GUI.DrawTextureWithTexCoords(r, tex, tc, true);
        }

        // -------------------------------------------------------------------------------------
        // Grid rendering + interaction
        // -------------------------------------------------------------------------------------
        private void DrawGrid()
        {
            gridScroll = EditorGUILayout.BeginScrollView(gridScroll, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            float drawW = cellW * zoom;
            float drawH = cellH * zoom;
            float stepX = drawW * PackX;   // spacing < tile size, so adjacent opaque hexes touch
            float stepY = drawH * PackY;

            float contentW = width * stepX + stepX * 0.5f + (drawW - stepX) + 8;
            float contentH = height * stepY + (drawH - stepY) + 8;
            Rect content = GUILayoutUtility.GetRect(contentW, contentH);

            HandleGridMouse(content, stepX, stepY);

            if (Event.current.type == EventType.Repaint)
            {
                // Pass 1: terrain tiles (drawn larger than the spacing so they interlock with no gaps).
                for (int row = 0; row < height; row++)
                    for (int col = 0; col < width; col++)
                        DrawCellSprite(TileRect(content, row, col, drawW, drawH, stepX, stepY), Index(row, col));

                // Pass 2: region tints, markers and selection on top.
                for (int row = 0; row < height; row++)
                    for (int col = 0; col < width; col++)
                        DrawCellOverlay(TileRect(content, row, col, drawW, drawH, stepX, stepY), row, col);
            }

            EditorGUILayout.EndScrollView();
        }

        private Rect TileRect(Rect content, int row, int col, float drawW, float drawH, float stepX, float stepY)
        {
            float x = content.x + col * stepX + ((row & 1) == 1 ? stepX * 0.5f : 0f);
            float y = content.y + row * stepY;
            return new Rect(x, y, drawW, drawH);
        }

        private void DrawCellSprite(Rect draw, int idx)
        {
            TerrainEnum t = terrain[idx];
            Sprite sprite = !string.IsNullOrEmpty(spriteNames[idx])
                ? ScenarioCardCatalog.GetTerrainSpriteByName(spriteNames[idx])
                : ScenarioCardCatalog.GetTerrainSprite(t);

            if (sprite != null && sprite.texture != null) DrawSprite(draw, sprite);
            else EditorGUI.DrawRect(draw, TerrainFallbackColor(t));
        }

        private void DrawCellOverlay(Rect r, int row, int col)
        {
            int idx = Index(row, col);

            if (!string.IsNullOrEmpty(regions[idx]))
            {
                Color tint = RegionColor(regions[idx]);
                tint.a = 0.22f;
                EditorGUI.DrawRect(r, tint);
            }

            bool hasLeader = leaderStarts.ContainsKey(idx);
            bool hasPc = pcs.ContainsKey(idx);
            int charCount = characters.TryGetValue(idx, out var list) ? list.Count : 0;

            if (hasLeader) DrawCorner(r, "★", new Color(1f, 0.85f, 0.1f), TextAnchor.UpperLeft);
            if (hasPc) DrawCorner(r, "⌂", Color.white, TextAnchor.UpperRight);
            if (charCount > 0) DrawCorner(r, charCount.ToString(), Color.cyan, TextAnchor.LowerRight);

            if (idx == selectedIndex)
            {
                Handles.color = Color.red;
                Handles.DrawSolidRectangleWithOutline(r, new Color(0, 0, 0, 0), Color.red);
            }
        }

        private static void DrawCorner(Rect r, string text, Color color, TextAnchor anchor)
        {
            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = anchor,
                fontSize = 10,
                normal = { textColor = color }
            };
            GUI.Label(new Rect(r.x + 1, r.y, r.width - 2, r.height), text, style);
        }

        private void HandleGridMouse(Rect content, float cellW, float cellH)
        {
            Event e = Event.current;
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

            for (int row = 0; row < height; row++)
            {
                for (int col = 0; col < width; col++)
                {
                    if (CubeDistance(OffsetToCube(row, col), center) > radius) continue;
                    int idx = Index(row, col);

                    if (tool == Tool.Region)
                    {
                        // Region-only brush: change the region of existing hexes, never the terrain.
                        regions[idx] = regionBrushRegion ?? "";
                        continue;
                    }

                    // Terrain brush: terrain + chosen tile variation, and region if one is selected.
                    terrain[idx] = paintTerrain;
                    spriteNames[idx] = paintSpriteName ?? "";
                    if (!string.IsNullOrEmpty(paintRegion)) regions[idx] = paintRegion;
                }
            }
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

            DrawLeaderStartSection(selectedIndex, row, col);
            EditorGUILayout.Space();
            DrawPcSection(selectedIndex, row, col);
            EditorGUILayout.Space();
            DrawCharactersSection(selectedIndex, row, col);

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawLeaderStartSection(int idx, int row, int col)
        {
            EditorGUILayout.LabelField("Leader Start", EditorStyles.boldLabel);
            bool has = leaderStarts.TryGetValue(idx, out ScenarioLeaderStart start);
            bool newHas = EditorGUILayout.Toggle("Is a leader's start", has);

            if (newHas && !has)
            {
                start = new ScenarioLeaderStart { row = row, col = col, isPlayable = true };
                leaderStarts[idx] = start;
                has = true;
            }
            else if (!newHas && has)
            {
                leaderStarts.Remove(idx);
                return;
            }
            if (!has) return;

            start.isPlayable = EditorGUILayout.Toggle("Playable leader", start.isPlayable);
            IReadOnlyList<string> pool = start.isPlayable ? ScenarioCardCatalog.PlayableLeaders : ScenarioCardCatalog.NonPlayableLeaders;
            SearchableField("Leader", start.leaderName, pool, v => start.leaderName = v);
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

            SearchableField("Name", pc.pcName, ScenarioCardCatalog.PcCards, v => pc.pcName = v, ScenarioCardCatalog.GetCard);
            SearchableField("Owner", pc.ownerLeaderName, ScenarioCardCatalog.AllLeaders(), v => pc.ownerLeaderName = v);
            pc.citySize = (int)(PCSizeEnum)EditorGUILayout.EnumPopup("Size", (PCSizeEnum)pc.citySize);
            pc.fortSize = (int)(FortSizeEnum)EditorGUILayout.EnumPopup("Fort", (FortSizeEnum)pc.fortSize);
            pc.hasPort = EditorGUILayout.Toggle("Has port", pc.hasPort);
            pc.isHidden = EditorGUILayout.Toggle("Hidden", pc.isHidden);
            pc.isCapital = EditorGUILayout.Toggle("Capital", pc.isCapital);
            pc.isIsland = EditorGUILayout.Toggle("Island", pc.isIsland);
            pc.loyalty = EditorGUILayout.IntSlider("Loyalty", pc.loyalty, 0, 100);
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

                SearchableField("Name", c.characterName, ScenarioCardCatalog.CharacterCards, v => c.characterName = v, ScenarioCardCatalog.GetCard);
                SearchableField("Owner", c.ownerLeaderName, ScenarioCardCatalog.AllLeaders(), v => c.ownerLeaderName = v);

                DrawArmyEditor(c);

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
        }

        private void SyncCharacterList(int idx, List<ScenarioCharacter> list)
        {
            if (list.Count == 0) characters.Remove(idx);
            else characters[idx] = list;
        }

        // Search-as-you-type picker. Shows the current value on a dropdown button; clicking opens
        // ScenarioSearchPopup (with a search field + card preview); the choice is applied via the
        // callback, because the popup resolves after the click rather than inline.
        private void SearchableField(string label, string current, IReadOnlyList<string> pool, Action<string> onSelected,
            Func<string, CardData> cardResolver = null)
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
                }, cardResolver);
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
                width = width,
                height = height,
                terrain = terrain.Select(t => (int)t).ToArray(),
                leaderStarts = leaderStarts.Values.ToList(),
                pcs = pcs.Values.ToList(),
                characters = characters.Values.SelectMany(list => list).ToList()
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
            width = data.width;
            height = data.height;
            terrain = new TerrainEnum[width * height];
            regions = new string[width * height];
            spriteNames = new string[width * height];
            for (int i = 0; i < terrain.Length; i++)
                terrain[i] = (i < data.terrain.Length) ? (TerrainEnum)data.terrain[i] : TerrainEnum.deepWater;

            leaderStarts.Clear();
            pcs.Clear();
            characters.Clear();
            selectedIndex = -1;

            foreach (ScenarioRegionCell cell in data.regions ?? new List<ScenarioRegionCell>())
                if (InBounds(cell.row, cell.col)) regions[Index(cell.row, cell.col)] = cell.region;

            foreach (ScenarioSpriteCell cell in data.terrainSprites ?? new List<ScenarioSpriteCell>())
                if (InBounds(cell.row, cell.col)) spriteNames[Index(cell.row, cell.col)] = cell.spriteName;

            foreach (ScenarioLeaderStart s in data.leaderStarts ?? new List<ScenarioLeaderStart>())
                if (InBounds(s.row, s.col)) leaderStarts[Index(s.row, s.col)] = s;

            foreach (ScenarioPC p in data.pcs ?? new List<ScenarioPC>())
                if (InBounds(p.row, p.col)) pcs[Index(p.row, p.col)] = p;

            foreach (ScenarioCharacter c in data.characters ?? new List<ScenarioCharacter>())
            {
                if (!InBounds(c.row, c.col)) continue;
                int idx = Index(c.row, c.col);
                if (!characters.TryGetValue(idx, out var list)) characters[idx] = list = new List<ScenarioCharacter>();
                list.Add(c);
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
