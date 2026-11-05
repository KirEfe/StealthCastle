using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StealthCastle.Isometric3D.Hub
{
    /// <summary>
    /// UI логова, собирается из кода: тайник и артефакт (слева сверху), подсказка у станции (снизу),
    /// окно станции (список слева, подробности справа). Тексты на английском: в шрифте TMP по умолчанию нет кириллицы.
    /// </summary>
    public class HubUI3D : MonoBehaviour
    {
        private static readonly Color PanelColor = new Color(0.05f, 0.04f, 0.04f, 0.88f);
        private static readonly Color AccentColor = new Color(1f, 0.82f, 0.29f);

        private TextMeshProUGUI stashText;
        private TextMeshProUGUI artifactText;
        private GameObject promptPanel;
        private TextMeshProUGUI promptText;
        private GameObject window;
        private TextMeshProUGUI titleText;
        private TextMeshProUGUI listText;
        private TextMeshProUGUI detailsText;
        private TextMeshProUGUI messageText;
        private TextMeshProUGUI footerText;

        public bool IsBuilt => window != null;

        public void Build()
        {
            if (IsBuilt) return;

            var canvasGo = new GameObject("HubCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var root = canvasGo.transform;

            // Тайник и артефакт
            var status = Panel(root, "StatusPanel", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(380f, 96f), new Vector2(20f, -20f));
            stashText = Text(status, "Stash", new Vector2(0f, 0.5f), Vector2.one, new Vector2(18f, 0f), new Vector2(-12f, -6f), 30, TextAlignmentOptions.Left);
            stashText.color = AccentColor;
            artifactText = Text(status, "Artifact", Vector2.zero, new Vector2(1f, 0.5f), new Vector2(18f, 8f), new Vector2(-12f, 0f), 21, TextAlignmentOptions.Left);

            // Подсказка у станции
            promptPanel = Panel(root, "PromptPanel", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(560f, 60f), new Vector2(0f, 40f)).gameObject;
            promptText = Text(promptPanel.transform, "Prompt", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 27, TextAlignmentOptions.Center);
            promptPanel.SetActive(false);

            // Окно станции
            window = Panel(root, "StationWindow", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(1000f, 600f), Vector2.zero).gameObject;
            window.GetComponent<Image>().color = new Color(0.05f, 0.04f, 0.04f, 0.97f); // Подписи станций не просвечивают
            var w = window.transform;
            titleText = Text(w, "Title", new Vector2(0f, 1f), Vector2.one, new Vector2(30f, -80f), new Vector2(-30f, -20f), 40, TextAlignmentOptions.Left);
            titleText.color = AccentColor;
            listText = Text(w, "List", new Vector2(0f, 0f), new Vector2(0.5f, 1f), new Vector2(30f, 110f), new Vector2(-10f, -100f), 26, TextAlignmentOptions.TopLeft);
            detailsText = Text(w, "Details", new Vector2(0.5f, 0f), new Vector2(1f, 1f), new Vector2(10f, 110f), new Vector2(-30f, -100f), 23, TextAlignmentOptions.TopLeft);
            messageText = Text(w, "Message", Vector2.zero, new Vector2(1f, 0f), new Vector2(30f, 60f), new Vector2(-30f, 100f), 24, TextAlignmentOptions.Center);
            messageText.color = AccentColor;
            footerText = Text(w, "Footer", Vector2.zero, new Vector2(1f, 0f), new Vector2(30f, 15f), new Vector2(-30f, 55f), 20, TextAlignmentOptions.Center);
            footerText.color = new Color(0.75f, 0.72f, 0.68f);
            window.SetActive(false);
        }

        public void SetStatus(int stash, string artifactName)
        {
            stashText.text = $"Stash: {stash} loot";
            artifactText.text = $"Artifact: {(string.IsNullOrEmpty(artifactName) ? "none" : artifactName)}";
        }

        /// <summary>Подсказка внизу экрана; null — скрыть</summary>
        public void SetPrompt(string text)
        {
            bool show = !string.IsNullOrEmpty(text);
            if (promptPanel.activeSelf != show) promptPanel.SetActive(show);
            if (show) promptText.text = text;
        }

        /// <summary>Окно станции: строки списка, выбранная подсвечена; справа — подробности выбранного</summary>
        public void ShowWindow(string title, IList<string> lines, int cursor, string details, string footer)
        {
            window.SetActive(true);
            titleText.text = title;

            var list = new StringBuilder();
            for (int i = 0; lines != null && i < lines.Count; i++)
            {
                list.Append(i == cursor ? $"<color=#FFD24A>> {lines[i]}</color>" : $"   {lines[i]}");
                list.Append('\n');
            }
            listText.text = list.ToString();
            detailsText.text = details ?? "";

            // Без списка (итог забега, трофеи) подробности занимают всё окно
            bool hasList = lines != null && lines.Count > 0;
            detailsText.rectTransform.anchorMin = new Vector2(hasList ? 0.5f : 0f, 0f);
            detailsText.rectTransform.offsetMin = new Vector2(hasList ? 10f : 30f, 110f);
            footerText.text = footer ?? "";
        }

        public void SetMessage(string text)
        {
            messageText.text = text ?? "";
        }

        public void HideWindow()
        {
            window.SetActive(false);
            messageText.text = "";
        }

        // --- Построение -------------------------------------------------------------------

        private static Transform Panel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 size, Vector2 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            go.AddComponent<Image>().color = PanelColor;
            return go.transform;
        }

        private static TextMeshProUGUI Text(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax, float size, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            var text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }
    }
}
