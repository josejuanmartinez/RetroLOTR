using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RetroLOTR.Scenarios
{
    /// <summary>
    /// First thing the player sees on a fresh game start: pick an authored scenario or
    /// "The Champions of Middle Earth" (the default, randomly generated campaign).
    /// Nothing else — intro video, board generation, leader selector — runs until a
    /// choice is made (they wait on <see cref="GameConfig.ScenarioChosen"/>).
    ///
    /// Built entirely from code (own canvas), so it needs no prefab or scene wiring:
    /// Board.Start calls <see cref="Show"/> when no choice has been made yet.
    /// </summary>
    public class ScenarioSelectionScreen : MonoBehaviour
    {
        private const string DefaultCampaignTitle = "The Champions of Middle Earth";
        private const string DefaultCampaignSubtitle = "A new random Middle-earth — the classic campaign";

        private static readonly Color BackgroundColor = new(0.055f, 0.05f, 0.08f, 1f);
        private static readonly Color PanelColor = new(0.12f, 0.11f, 0.16f, 1f);
        private static readonly Color ButtonColor = new(0.18f, 0.16f, 0.22f, 1f);
        private static readonly Color ButtonHoverColor = new(0.28f, 0.24f, 0.34f, 1f);
        private static readonly Color TitleColor = new(0.92f, 0.80f, 0.45f, 1f);
        private static readonly Color TextColor = new(0.92f, 0.90f, 0.86f, 1f);
        private static readonly Color SubtleTextColor = new(0.65f, 0.63f, 0.60f, 1f);

        public static void Show()
        {
            if (FindFirstObjectByType<ScenarioSelectionScreen>() != null) return;
            new GameObject("ScenarioSelectionScreen").AddComponent<ScenarioSelectionScreen>();
        }

        private void Start()
        {
            BuildUi();
        }

        private void Choose(string scenarioName)
        {
            GameConfig.ScenarioToLoad = scenarioName; // null = default random campaign
            GameConfig.ScenarioChosen = true;
            Destroy(gameObject);
        }

        // ------------------------------------------------------------------
        // UI construction
        // ------------------------------------------------------------------

        private void BuildUi()
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000; // above everything, including the video

            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            gameObject.AddComponent<GraphicRaycaster>();

            // Opaque backdrop so nothing behind it is visible.
            Image backdrop = CreateChild<Image>(transform, "Backdrop");
            Stretch(backdrop.rectTransform);
            backdrop.color = BackgroundColor;

            // Centered panel.
            Image panel = CreateChild<Image>(transform, "Panel");
            panel.color = PanelColor;
            RectTransform panelRt = panel.rectTransform;
            panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(720f, 760f);

            VerticalLayoutGroup layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(36, 36, 30, 30);
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            CreateLabel(panelRt, "Choose your campaign", 40f, TitleColor, FontStyles.Bold, 54f);
            CreateLabel(panelRt, "Nothing is decided until you choose.", 18f, SubtleTextColor, FontStyles.Italic, 30f);
            CreateSpacer(panelRt, 10f);

            // Default campaign — always first, always available.
            CreateChoiceButton(panelRt, DefaultCampaignTitle, DefaultCampaignSubtitle, () => Choose(null));

            List<string> scenarios = ScenarioLoader.GetAvailableScenarios();
            if (scenarios.Count > 0)
            {
                CreateSpacer(panelRt, 8f);
                CreateLabel(panelRt, "Scenarios", 22f, TitleColor, FontStyles.Bold, 32f);

                RectTransform listArea = CreateScrollableList(panelRt, 380f);
                foreach (string scenario in scenarios)
                {
                    string captured = scenario;
                    CreateChoiceButton(listArea, scenario, "Authored scenario", () => Choose(captured));
                }
            }
        }

        private static RectTransform CreateScrollableList(RectTransform parent, float height)
        {
            Image viewport = CreateChild<Image>(parent, "ScenarioList");
            viewport.color = new Color(0f, 0f, 0f, 0.18f);
            LayoutElement viewportElement = viewport.gameObject.AddComponent<LayoutElement>();
            viewportElement.preferredHeight = height;
            viewport.gameObject.AddComponent<RectMask2D>();

            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            RectTransform content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport.transform, false);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(0f, 0f);

            VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.content = content;
            scroll.viewport = viewport.rectTransform;
            return content;
        }

        private void CreateChoiceButton(RectTransform parent, string title, string subtitle, Action onClick)
        {
            Image background = CreateChild<Image>(parent, $"Choice_{title}");
            background.color = ButtonColor;
            LayoutElement element = background.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 84f;

            Button button = background.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(ButtonHoverColor.r / ButtonColor.r, ButtonHoverColor.g / ButtonColor.g, ButtonHoverColor.b / ButtonColor.b, 1f);
            colors.pressedColor = colors.highlightedColor * 1.1f;
            button.colors = colors;
            button.onClick.AddListener(() => onClick?.Invoke());

            RectTransform textArea = new GameObject("Texts", typeof(RectTransform)).GetComponent<RectTransform>();
            textArea.SetParent(background.transform, false);
            Stretch(textArea);

            TextMeshProUGUI titleText = CreateChild<TextMeshProUGUI>(textArea, "Title");
            titleText.text = title;
            titleText.fontSize = 26f;
            titleText.fontStyle = FontStyles.Bold;
            titleText.color = TextColor;
            titleText.alignment = TextAlignmentOptions.MidlineLeft;
            RectTransform titleRt = titleText.rectTransform;
            titleRt.anchorMin = new Vector2(0f, 0.42f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.offsetMin = new Vector2(24f, 0f);
            titleRt.offsetMax = new Vector2(-24f, -6f);

            TextMeshProUGUI subtitleText = CreateChild<TextMeshProUGUI>(textArea, "Subtitle");
            subtitleText.text = subtitle;
            subtitleText.fontSize = 16f;
            subtitleText.color = SubtleTextColor;
            subtitleText.alignment = TextAlignmentOptions.TopLeft;
            RectTransform subtitleRt = subtitleText.rectTransform;
            subtitleRt.anchorMin = new Vector2(0f, 0f);
            subtitleRt.anchorMax = new Vector2(1f, 0.42f);
            subtitleRt.offsetMin = new Vector2(24f, 8f);
            subtitleRt.offsetMax = new Vector2(-24f, 0f);
        }

        private static void CreateLabel(RectTransform parent, string text, float size, Color color, FontStyles style, float height)
        {
            TextMeshProUGUI label = CreateChild<TextMeshProUGUI>(parent, $"Label_{text}");
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            LayoutElement element = label.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = height;
        }

        private static void CreateSpacer(RectTransform parent, float height)
        {
            RectTransform spacer = new GameObject("Spacer", typeof(RectTransform)).GetComponent<RectTransform>();
            spacer.SetParent(parent, false);
            LayoutElement element = spacer.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = height;
        }

        private static T CreateChild<T>(Transform parent, string name) where T : Component
        {
            GameObject child = new(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return child.AddComponent<T>();
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
