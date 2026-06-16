using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RetroLOTR.Scenarios.EditorTools
{
    /// <summary>
    /// Search-as-you-type picker over a list of names, with a live card preview on hover for
    /// card pools (PC / Character / Army). Modelled on DeckExplorer's card preview but kept
    /// lightweight — it shows the card's artwork plus key stats rather than a full render. For
    /// non-card pools (leaders, regions) the preview pane is omitted.
    /// </summary>
    public class ScenarioSearchPopup : PopupWindowContent
    {
        public const string NoneLabel = "(none)";

        private readonly List<string> items;
        private readonly Action<string> onSelected;
        private readonly Func<string, CardData> cardResolver;
        private readonly bool showPreview;

        private string search = string.Empty;
        private Vector2 scroll;
        private string hovered;
        private bool focusRequested;

        private const float RowHeight = 18f;
        private const float ListWidth = 240f;
        private const float PreviewWidth = 230f;

        public ScenarioSearchPopup(IReadOnlyList<string> items, Action<string> onSelected, Func<string, CardData> cardResolver = null)
        {
            this.items = new List<string> { NoneLabel };
            if (items != null) this.items.AddRange(items.Where(i => !string.IsNullOrWhiteSpace(i)));
            this.onSelected = onSelected;
            this.cardResolver = cardResolver;
            showPreview = cardResolver != null;
        }

        public override Vector2 GetWindowSize()
        {
            float w = showPreview ? ListWidth + PreviewWidth + 12 : ListWidth + 8;
            return new Vector2(w, 360f);
        }

        public override void OnOpen()
        {
            focusRequested = true;
            if (editorWindow != null) editorWindow.wantsMouseMove = true;
        }

        public override void OnGUI(Rect rect)
        {
            Event e = Event.current;

            // Search field (focused on open).
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUI.SetNextControlName("ScenarioSearchField");
            search = GUILayout.TextField(search, EditorStyles.toolbarSearchField, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            if (focusRequested && e.type == EventType.Repaint)
            {
                EditorGUI.FocusTextInControl("ScenarioSearchField");
                focusRequested = false;
            }

            List<string> filtered = string.IsNullOrWhiteSpace(search)
                ? items
                : items.Where(i => i == NoneLabel || i.IndexOf(search.Trim(), StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            GUILayout.BeginHorizontal();

            // List column.
            GUILayout.BeginVertical(GUILayout.Width(ListWidth));
            scroll = GUILayout.BeginScrollView(scroll);
            foreach (string item in filtered)
            {
                Rect row = GUILayoutUtility.GetRect(new GUIContent(item), EditorStyles.label, GUILayout.Height(RowHeight), GUILayout.ExpandWidth(true));

                if (row.Contains(e.mousePosition))
                {
                    if (!string.Equals(hovered, item, StringComparison.Ordinal))
                    {
                        hovered = item;
                        if (editorWindow != null) editorWindow.Repaint();
                    }
                    EditorGUI.DrawRect(row, new Color(0.3f, 0.4f, 0.6f, 0.35f));
                }

                GUI.Label(row, item);

                if (e.type == EventType.MouseDown && row.Contains(e.mousePosition))
                {
                    onSelected?.Invoke(item == NoneLabel ? string.Empty : item);
                    editorWindow?.Close();
                    e.Use();
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            // Preview column.
            if (showPreview)
            {
                GUILayout.BeginVertical(GUILayout.Width(PreviewWidth));
                DrawPreview(hovered);
                GUILayout.EndVertical();
            }

            GUILayout.EndHorizontal();

            if (e.type == EventType.MouseMove && editorWindow != null) editorWindow.Repaint();
        }

        private void DrawPreview(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name == NoneLabel)
            {
                EditorGUILayout.HelpBox("Hover an item to preview the card.", MessageType.None);
                return;
            }

            CardData card = cardResolver(name);
            if (card == null)
            {
                EditorGUILayout.LabelField(name, EditorStyles.boldLabel);
                return;
            }

            EditorGUILayout.LabelField(card.name, EditorStyles.boldLabel);

            Rect art = GUILayoutUtility.GetRect(PreviewWidth - 8, 150, GUILayout.ExpandWidth(false));
            Sprite sprite = ScenarioCardCatalog.GetCardArtwork(card);
            if (sprite != null && sprite.texture != null)
            {
                Texture tex = sprite.texture;
                Rect tc = new(sprite.rect.x / tex.width, sprite.rect.y / tex.height,
                              sprite.rect.width / tex.width, sprite.rect.height / tex.height);
                GUI.DrawTextureWithTexCoords(art, tex, tc, true);
            }
            else
            {
                EditorGUI.DrawRect(art, new Color(0.15f, 0.15f, 0.15f));
                GUI.Label(art, "(no art)", EditorStyles.centeredGreyMiniLabel);
            }

            EditorGUILayout.LabelField("Type", card.GetCardType().ToString());
            foreach (string line in BuildStatLines(card))
                EditorGUILayout.LabelField(line, EditorStyles.wordWrappedMiniLabel);
        }

        private static IEnumerable<string> BuildStatLines(CardData card)
        {
            switch (card.GetCardType())
            {
                case CardTypeEnum.Character:
                    yield return $"Cmd {card.commander}  Agt {card.agent}  Emm {card.emmissary}  Mag {card.mage}";
                    if (card.race != default) yield return $"Race: {card.race}";
                    break;
                case CardTypeEnum.Army:
                    yield return $"Troop: {card.troopType}";
                    if (card.specialAbilities != null && card.specialAbilities.Count > 0)
                        yield return "Abilities: " + string.Join(", ", card.specialAbilities);
                    break;
                case CardTypeEnum.PC:
                    if (!string.IsNullOrWhiteSpace(card.region)) yield return $"Region: {card.region}";
                    break;
            }
        }
    }
}
