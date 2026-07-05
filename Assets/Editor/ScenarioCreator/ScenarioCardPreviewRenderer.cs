using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RetroLOTR.Scenarios.EditorTools
{
    /// <summary>
    /// Renders full card previews for the scenario creator's magnifier inspector: the real
    /// Card prefab is laid out on a hidden world-space canvas and captured through an
    /// offscreen camera, mirroring DeckExplorerWindow's live preview. Unlike DeckExplorer
    /// (one live card), this caches one Texture2D per card name so the inspector can show
    /// the PC, character and army cards of a hex together without re-rendering every repaint.
    /// </summary>
    public class ScenarioCardPreviewRenderer : IDisposable
    {
        private const float CardW = 275f;
        private const float CardH = 325f;
        private const float Pad = 15f;
        public const float CanvasW = CardW + Pad * 2f;
        public const float CanvasH = CardH + Pad * 2f;

        private GameObject cardPrefab;
        private GameObject root;
        private GameObject canvasRoot;
        private GameObject cardObject;
        private Card cardComponent;
        private Camera previewCamera;
        private RenderTexture renderTexture;
        private readonly Dictionary<string, Texture2D> cache = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Rendered preview for a card, cached by card name. May be null when rendering fails.</summary>
        public Texture2D Render(CardData card)
        {
            if (card == null || string.IsNullOrWhiteSpace(card.name)) return null;
            if (cache.TryGetValue(card.name, out Texture2D cached) && cached != null) return cached;

            try
            {
                EnsureObjects();
                if (cardComponent == null || previewCamera == null || renderTexture == null) return null;

                cardObject.SetActive(true);
                canvasRoot.SetActive(true);
                ApplyData(card);
                cardComponent.ShowRealCard();

                // Force TMP to compute mesh bounds immediately so ContentSizeFitter gets
                // correct preferred heights before the layout rebuild runs (same editor
                // gotcha DeckExplorer works around).
                foreach (TextMeshProUGUI tmp in cardObject.GetComponentsInChildren<TextMeshProUGUI>(true))
                    tmp.ForceMeshUpdate();

                Canvas.ForceUpdateCanvases();
                RectTransform cardRect = cardObject.GetComponent<RectTransform>();
                if (cardRect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(cardRect);

                previewCamera.targetTexture = renderTexture;
                previewCamera.Render();
                previewCamera.targetTexture = null;

                Texture2D tex = CaptureTexture();
                cache[card.name] = tex;
                return tex;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"ScenarioCardPreviewRenderer: failed to render '{card.name}': {ex.Message}");
                return null;
            }
        }

        public void ClearCache()
        {
            foreach (Texture2D tex in cache.Values)
                if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
            cache.Clear();
        }

        public void Dispose()
        {
            ClearCache();

            if (cardObject != null) UnityEngine.Object.DestroyImmediate(cardObject);
            if (canvasRoot != null) UnityEngine.Object.DestroyImmediate(canvasRoot);
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            if (renderTexture != null)
            {
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }

            cardObject = null;
            canvasRoot = null;
            root = null;
            renderTexture = null;
            cardComponent = null;
            previewCamera = null;
            cardPrefab = null;
        }

        // -------------------------------------------------------------------------------------
        // Offscreen rig (camera + world-space canvas + Card prefab instance)
        // -------------------------------------------------------------------------------------
        private void EnsureObjects()
        {
            if (cardPrefab == null)
                cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameObjects/Reusable/Card.prefab");
            if (cardPrefab == null) return;

            if (root == null)
            {
                root = new GameObject("ScenarioCardPreviewRoot") { hideFlags = HideFlags.HideAndDontSave };
                root.layer = 5;
                previewCamera = root.AddComponent<Camera>();
                previewCamera.hideFlags = HideFlags.HideAndDontSave;
                previewCamera.orthographic = true;
                previewCamera.orthographicSize = 6f;
                previewCamera.clearFlags = CameraClearFlags.SolidColor;
                previewCamera.backgroundColor = Color.black;
                previewCamera.cullingMask = 1 << 5;
                previewCamera.nearClipPlane = 0.01f;
                previewCamera.farClipPlane = 100f;
                previewCamera.aspect = CanvasW / CanvasH;
                previewCamera.transform.position = new Vector3(0f, 0f, -10f);
                previewCamera.transform.rotation = Quaternion.identity;
            }

            if (canvasRoot == null)
            {
                canvasRoot = new GameObject("ScenarioCardPreviewCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
                canvasRoot.hideFlags = HideFlags.HideAndDontSave;
                canvasRoot.layer = 5;
                canvasRoot.transform.SetParent(root.transform, false);

                Canvas canvas = canvasRoot.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;

                // Scale canvas so its full height fills the camera's ortho view (orthoSize 6 → 12 units).
                float worldScale = previewCamera.orthographicSize * 2f / CanvasH;
                RectTransform canvasRect = canvasRoot.GetComponent<RectTransform>();
                canvasRect.sizeDelta = new Vector2(CanvasW, CanvasH);
                canvasRect.localPosition = new Vector3(0f, 0f, 10f);
                canvasRect.localRotation = Quaternion.identity;
                canvasRect.localScale = Vector3.one * worldScale;
            }

            if (cardObject == null)
            {
                cardObject = (GameObject)PrefabUtility.InstantiatePrefab(cardPrefab);
                if (cardObject == null) return;

                cardObject.hideFlags = HideFlags.HideAndDontSave;
                cardObject.transform.SetParent(canvasRoot.transform, false);
                cardObject.SetActive(true);

                RectTransform cardRect = cardObject.GetComponent<RectTransform>();
                if (cardRect != null)
                {
                    cardRect.anchorMin = new Vector2(0.5f, 0.5f);
                    cardRect.anchorMax = new Vector2(0.5f, 0.5f);
                    cardRect.sizeDelta = new Vector2(CardW, CardH);
                    cardRect.anchoredPosition = Vector2.zero;
                    cardRect.localScale = Vector3.one;
                }

                SetHideFlagsAndLayerRecursive(cardObject.transform);
                cardComponent = cardObject.GetComponent<Card>();
            }

            ConfigureTextFields();
            EnsureRenderTexture();
        }

        private void EnsureRenderTexture()
        {
            int width = Mathf.RoundToInt(CanvasW);
            int height = Mathf.RoundToInt(CanvasH);
            if (renderTexture != null && renderTexture.width == width && renderTexture.height == height) return;

            if (renderTexture != null)
            {
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }

            renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        private Texture2D CaptureTexture()
        {
            int width = renderTexture.width;
            int height = renderTexture.height;
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            Texture2D tex = new(width, height, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            return tex;
        }

        private static void SetHideFlagsAndLayerRecursive(Transform t)
        {
            if (t == null) return;
            t.gameObject.hideFlags = HideFlags.HideAndDontSave;
            t.gameObject.layer = 5;
            for (int i = 0; i < t.childCount; i++)
                SetHideFlagsAndLayerRecursive(t.GetChild(i));
        }

        // -------------------------------------------------------------------------------------
        // Card content
        // -------------------------------------------------------------------------------------
        private void ApplyData(CardData card)
        {
            SetText("titleText", FormatCardTitle(card.name));
            SetText("descriptionText", BuildDescription(card));
            SetText("requirementsText", BuildRequirementsText(card));
            SetText("requirementsMessage", string.Empty);

            Image cardArtImage = GetField<Image>("cardArtImage");
            if (cardArtImage != null)
            {
                Sprite sprite = ScenarioCardCatalog.GetCardArtwork(card);
                cardArtImage.sprite = sprite;
                cardArtImage.enabled = sprite != null;
            }

            Hover hover = GetField<Hover>("hover");
            if (hover != null) hover.Initialize(card.GetCardType().ToString());
        }

        private void ConfigureTextFields()
        {
            if (cardComponent == null) return;
            TMP_FontAsset defaultFont = TMP_Settings.defaultFontAsset;
            TMP_SpriteAsset defaultSpriteAsset = TMP_Settings.defaultSpriteAsset;

            foreach (string fieldName in new[] { "titleText", "descriptionText", "requirementsText", "requirementsMessage" })
            {
                TextMeshProUGUI tmp = GetField<TextMeshProUGUI>(fieldName);
                if (tmp == null) continue;
                if (tmp.font == null && defaultFont != null) tmp.font = defaultFont;
                if (tmp.spriteAsset == null && defaultSpriteAsset != null) tmp.spriteAsset = defaultSpriteAsset;
                tmp.richText = true;
            }
        }

        private void SetText(string fieldName, string text)
        {
            TextMeshProUGUI tmp = GetField<TextMeshProUGUI>(fieldName);
            if (tmp == null) return;
            tmp.text = text ?? string.Empty;
            tmp.color = Color.white;
        }

        private T GetField<T>(string fieldName) where T : class
        {
            if (cardComponent == null || string.IsNullOrWhiteSpace(fieldName)) return null;
            FieldInfo field = typeof(Card).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return field != null ? field.GetValue(cardComponent) as T : null;
        }

        private static string BuildDescription(CardData card)
        {
            string body = card.GetCardType() == CardTypeEnum.Encounter
                ? card.description?.Trim() ?? string.Empty
                : card.GetRenderedDescription(true);
            if (string.IsNullOrWhiteSpace(body)) body = card.description ?? string.Empty;

            string typeLabel = card.GetCardType().ToString();
            return string.IsNullOrWhiteSpace(body) ? typeLabel : $"{typeLabel}. {body}";
        }

        private static string BuildRequirementsText(CardData card)
        {
            List<string> reqs = new();
            AppendRequirement(reqs, "commander", card.commanderSkillRequired);
            AppendRequirement(reqs, "agent", card.agentSkillRequired);
            AppendRequirement(reqs, "emmissary", card.emissarySkillRequired);
            AppendRequirement(reqs, "mage", card.mageSkillRequired);
            AppendRequirement(reqs, "gold", card.GetTotalGoldCost());
            AppendRequirement(reqs, "leather", card.leatherRequired);
            AppendRequirement(reqs, "timber", card.timberRequired);
            AppendRequirement(reqs, "mounts", card.mountsRequired);
            AppendRequirement(reqs, "iron", card.ironRequired);
            AppendRequirement(reqs, "steel", card.steelRequired);
            AppendRequirement(reqs, "mithril", card.mithrilRequired);
            return reqs.Count == 0 ? string.Empty : string.Join(" ", reqs);
        }

        private static void AppendRequirement(List<string> requirements, string spriteName, int count)
        {
            if (count <= 0) return;
            requirements.Add($"{count}<sprite name=\"{spriteName}\">");
        }

        // Inserts spaces into CamelCase card names, same as DeckExplorer's title formatting.
        private static string FormatCardTitle(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            List<char> chars = new(value.Length + 4);
            for (int i = 0; i < value.Length; i++)
            {
                if (ShouldInsertWordSpace(value, i)) chars.Add(' ');
                chars.Add(value[i]);
            }

            string formatted = new string(chars.ToArray()).Trim().ToLowerInvariant();
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(formatted);
        }

        private static bool ShouldInsertWordSpace(string value, int index)
        {
            if (index <= 0 || index >= value.Length) return false;
            char current = value[index];
            if (!char.IsUpper(current)) return false;

            char previous = value[index - 1];
            if (char.IsWhiteSpace(previous)) return false;
            if (char.IsLower(previous) || char.IsDigit(previous)) return true;
            if (!char.IsUpper(previous)) return false;
            return index + 1 < value.Length && char.IsLower(value[index + 1]);
        }
    }
}
