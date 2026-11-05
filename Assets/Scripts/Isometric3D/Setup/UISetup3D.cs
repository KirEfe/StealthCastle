using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace StealthCastle.Isometric3D.Setup
{
    /// <summary>
    /// Настройка UI для 3D изометрической версии.
    /// Создает все необходимые UI элементы автоматически.
    /// </summary>
    public class UISetup3D : MonoBehaviour
    {
        [Header("UI Settings")]
        [SerializeField] private bool autoCreateUI = true;
        [SerializeField] private Font defaultFont;
        [SerializeField] private Material defaultMaterial;

        [Header("References")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private Core.GameManager3D gameManager;
        [SerializeField] private Player.PlayerController3D playerController;

        private UI.HUDController hudController;

        private void Awake()
        {
            if (!autoCreateUI) return;
            
            Debug.Log("[UISetup3D] Starting UI setup...");
            
            // Создаем Canvas, если его нет
            CreateCanvas();
            
            // Создаем HUD Controller
            CreateHUDController();
            
            // Создаем все UI элементы
            CreateHUDElements();
            
            Debug.Log("[UISetup3D] UI setup complete!");
        }

        /// <summary>
        /// Создание Canvas
        /// </summary>
        private void CreateCanvas()
        {
            if (canvas == null)
            {
                var canvasGo = new GameObject("MainCanvas");
                canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
                
                var canvasScaler = canvasGo.AddComponent<CanvasScaler>();
                canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasScaler.referenceResolution = new Vector2(1920, 1080);
                canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                
                var graphicRaycaster = canvasGo.AddComponent<GraphicRaycaster>();
            }
            
            if (canvas != null)
            {
                // Создаем EventSystem, если его нет
                var eventSystem = FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
                if (eventSystem == null)
                {
                    var eventSystemGo = new GameObject("EventSystem");
                    eventSystem = eventSystemGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
                    eventSystemGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                }
            }
        }

        /// <summary>
        /// Создание HUD Controller
        /// </summary>
        private void CreateHUDController()
        {
            if (hudController != null) return;
            
            var hudGo = new GameObject("HUDController");
            hudGo.transform.SetParent(canvas.transform, false);
            
            hudController = hudGo.AddComponent<UI.HUDController>();
        }

        /// <summary>
        /// Создание всех UI элементов
        /// </summary>
        private void CreateHUDElements()
        {
            // Создаем основные панели
            CreateLevelInfoPanel();
            CreateLootPanel();
            CreateDisguisePanel();
            CreateGameOverPanel();
            CreateVictoryPanel();
            CreatePauseMenu();
            CreateArtifactPanel();
            CreateIntroHint();

            // Настраиваем ссылки в HUDController
            SetupHUDReferences();
        }

        /// <summary>
        /// Создание панели информации об уровне
        /// </summary>
        /// <summary>
        /// Подсказка вступления уровня (внизу по центру): изучить маршруты, Пробел — начать
        /// </summary>
        private void CreateIntroHint()
        {
            var panelGo = new GameObject("IntroHintPanel");
            panelGo.transform.SetParent(canvas.transform, false);

            var rectTransform = panelGo.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0f);
            rectTransform.anchorMax = new Vector2(0.5f, 0f);
            rectTransform.pivot = new Vector2(0.5f, 0f);
            rectTransform.sizeDelta = new Vector2(620f, 60f);
            rectTransform.anchoredPosition = new Vector2(0f, 40f);

            var image = panelGo.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.7f);

            var textGo = new GameObject("IntroHintText");
            textGo.transform.SetParent(panelGo.transform, false);

            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.fontSize = 26;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;

            panelGo.SetActive(false);
            hudController.BindIntroHint(panelGo, text);
        }

        private void CreateLevelInfoPanel()
        {
            var panelGo = new GameObject("LevelInfoPanel");
            panelGo.transform.SetParent(canvas.transform, false);
            
            var rectTransform = panelGo.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.sizeDelta = new Vector2(250f, 50f);
            rectTransform.anchoredPosition = new Vector2(20f, -20f);
            
            var image = panelGo.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.7f);
            
            var textGo = new GameObject("LevelText");
            textGo.transform.SetParent(panelGo.transform, false);
            
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            textRect.anchoredPosition = Vector2.zero;
            
            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.text = "Level: 1";
            text.fontSize = 24;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            
            if (defaultMaterial != null)
            {
                text.fontSharedMaterial = defaultMaterial;
            }
            
            // Сохраняем ссылку
            hudController.BindLevelText(text);
        }

        /// <summary>
        /// Добыча при себе — под панелью уровня. Скрыта, пока нет забега (тестовые сцены)
        /// </summary>
        private void CreateLootPanel()
        {
            var panelGo = new GameObject("LootPanel");
            panelGo.transform.SetParent(canvas.transform, false);

            var rectTransform = panelGo.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.sizeDelta = new Vector2(250f, 44f);
            rectTransform.anchoredPosition = new Vector2(20f, -78f);

            var image = panelGo.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.7f);

            var textGo = new GameObject("LootText");
            textGo.transform.SetParent(panelGo.transform, false);

            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.text = "Loot: 0";
            text.fontSize = 22;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;

            panelGo.SetActive(false);
            hudController.BindLootText(text);
        }

        /// <summary>
        /// Создание панели маскировки
        /// </summary>
        private void CreateDisguisePanel()
        {
            var panelGo = new GameObject("DisguisePanel");
            panelGo.transform.SetParent(canvas.transform, false);
            panelGo.SetActive(false);
            
            var rectTransform = panelGo.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0f);
            rectTransform.anchorMax = new Vector2(0.5f, 0f);
            rectTransform.pivot = new Vector2(0.5f, 0f);
            rectTransform.sizeDelta = new Vector2(300f, 80f);
            rectTransform.anchoredPosition = new Vector2(0f, 20f);
            
            var image = panelGo.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.8f);
            
            // Текст таймера
            var timerGo = new GameObject("TimerText");
            timerGo.transform.SetParent(panelGo.transform, false);
            
            var timerRect = timerGo.AddComponent<RectTransform>();
            timerRect.anchorMin = Vector2.zero;
            timerRect.anchorMax = Vector2.one;
            timerRect.sizeDelta = new Vector2(-20f, -20f);
            timerRect.anchoredPosition = new Vector2(0f, 0f);
            
            var timerText = timerGo.AddComponent<TextMeshProUGUI>();
            timerText.text = "0.0s";
            timerText.fontSize = 28;
            timerText.color = Color.white;
            timerText.alignment = TextAlignmentOptions.Center;
            
            // Full image
            var fillGo = new GameObject("FillImage");
            fillGo.transform.SetParent(panelGo.transform, false);
            
            var fillRect = fillGo.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(1f, 0f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.sizeDelta = new Vector2(0f, 10f);
            fillRect.anchoredPosition = new Vector2(10f, 0f);
            
            var fillImage = fillGo.AddComponent<Image>();
            fillImage.color = Color.green;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.type = Image.Type.Filled;
            fillImage.fillOrigin = 0;
            
            // Сохраняем ссылки
            hudController.BindDisguisePanel(panelGo, timerText, fillImage);
        }

        /// <summary>
        /// Создание панели Game Over
        /// </summary>
        private void CreateGameOverPanel()
        {
            var panelGo = new GameObject("GameOverPanel");
            panelGo.transform.SetParent(canvas.transform, false);
            panelGo.SetActive(false);
            
            var rectTransform = panelGo.AddComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.sizeDelta = Vector2.zero;
            
            var image = panelGo.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.9f);
            
            var textGo = new GameObject("GameOverText");
            textGo.transform.SetParent(panelGo.transform, false);
            
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = new Vector2(-100f, -100f);
            textRect.anchoredPosition = Vector2.zero;
            
            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.text = "Game Over!\n\nPress Space to restart";
            text.fontSize = 36;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            
            if (defaultMaterial != null)
            {
                text.fontSharedMaterial = defaultMaterial;
            }
            
            // Сохраняем ссылки
            hudController.BindGameOverPanel(panelGo, text);
        }

        /// <summary>
        /// Создание панели победы
        /// </summary>
        private void CreateVictoryPanel()
        {
            var panelGo = new GameObject("VictoryPanel");
            panelGo.transform.SetParent(canvas.transform, false);
            panelGo.SetActive(false);
            
            var rectTransform = panelGo.AddComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.sizeDelta = Vector2.zero;
            
            var image = panelGo.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.85f);
            
            var textGo = new GameObject("VictoryText");
            textGo.transform.SetParent(panelGo.transform, false);
            
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = new Vector2(-100f, -100f);
            textRect.anchoredPosition = Vector2.zero;
            
            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.text = "Level Complete!\n\nPress Space to continue";
            text.fontSize = 36;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            
            if (defaultMaterial != null)
            {
                text.fontSharedMaterial = defaultMaterial;
            }
            
            // Сохраняем ссылки
            hudController.BindVictoryPanel(panelGo, text);
        }

        /// <summary>
        /// Создание меню паузы
        /// </summary>
        private void CreatePauseMenu()
        {
            var panelGo = new GameObject("PausePanel");
            panelGo.transform.SetParent(canvas.transform, false);
            panelGo.SetActive(false);
            
            var rectTransform = panelGo.AddComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.sizeDelta = Vector2.zero;
            
            var image = panelGo.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.8f);
            
            var textGo = new GameObject("PauseText");
            textGo.transform.SetParent(panelGo.transform, false);
            
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            textRect.anchoredPosition = Vector2.zero;
            
            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.text = "PAUSED\n\nPress ESC to continue";
            text.fontSize = 36;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            
            if (defaultMaterial != null)
            {
                text.fontSharedMaterial = defaultMaterial;
            }
            
            // Сохраняем ссылки
            hudController.BindPausePanel(panelGo);
        }

        /// <summary>
        /// Создание панели информации об артефакте
        /// </summary>
        private void CreateArtifactPanel()
        {
            var panelGo = new GameObject("ArtifactPanel");
            panelGo.transform.SetParent(canvas.transform, false);
            panelGo.SetActive(false);
            
            var rectTransform = panelGo.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(1f, 1f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(1f, 1f);
            rectTransform.sizeDelta = new Vector2(320f, 100f);
            rectTransform.anchoredPosition = new Vector2(-20f, -20f);
            
            var image = panelGo.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.85f);
            
            // Заголовок
            var nameGo = new GameObject("ArtifactName");
            nameGo.transform.SetParent(panelGo.transform, false);
            
            var nameRect = nameGo.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0.5f, 1f);
            nameRect.anchorMax = new Vector2(0.5f, 1f);
            nameRect.pivot = new Vector2(0.5f, 1f);
            nameRect.sizeDelta = new Vector2(280f, 40f);
            nameRect.anchoredPosition = new Vector2(0f, -8f);
            
            var nameText = nameGo.AddComponent<TextMeshProUGUI>();
            nameText.text = "Artifact Name";
            nameText.fontSize = 20;
            nameText.color = new Color(1f, 0.8f, 0f); // Золотой
            nameText.alignment = TextAlignmentOptions.Center;
            
            // Описание
            var descGo = new GameObject("ArtifactDescription");
            descGo.transform.SetParent(panelGo.transform, false);
            
            var descRect = descGo.AddComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0.5f, 0f);
            descRect.anchorMax = new Vector2(0.5f, 0f);
            descRect.pivot = new Vector2(0.5f, 0f);
            descRect.sizeDelta = new Vector2(300f, 56f);
            descRect.anchoredPosition = new Vector2(0f, 8f);
            
            var descText = descGo.AddComponent<TextMeshProUGUI>();
            descText.text = "Artifact description goes here...";
            descText.fontSize = 16;
            descText.color = Color.white;
            descText.alignment = TextAlignmentOptions.Top;
            descText.enableWordWrapping = true;
            
            if (defaultMaterial != null)
            {
                nameText.fontSharedMaterial = defaultMaterial;
                descText.fontSharedMaterial = defaultMaterial;
            }
            
            // Сохраняем ссылки
            hudController.BindArtifactPanel(panelGo, nameText, descText);
        }

        /// <summary>
        /// Настройка ссылок в HUDController
        /// </summary>
        private void SetupHUDReferences()
        {
            // Находим GameManager
            if (gameManager == null)
            {
                gameManager = Core.GameManager3D.Instance;
            }
            
            // Находим PlayerController
            if (playerController == null)
            {
                playerController = FindObjectOfType<Player.PlayerController3D>();
            }
            
            // Назначаем ссылки
            if (hudController != null)
            {
                // HUDController уже автоматически находит компоненты через Awake
                // Но можем назначить явно, если нужно
            }
        }

        /// <summary>
        /// Создание префаба UI
        /// </summary>
        [ContextMenu("Create UI Prefab")]
        public void CreateUIPrefab()
        {
            autoCreateUI = true;
            
            // Создаем новый Canvas
            CreateCanvas();
            CreateHUDController();
            CreateHUDElements();
            
            Debug.Log("[UISetup3D] UI prefab created!");
        }

        /// <summary>
        /// Проверка всех UI элементов
        /// </summary>
        public bool ValidateUI()
        {
            if (canvas == null)
            {
                Debug.LogError("[UISetup3D] Canvas is null!");
                return false;
            }
            
            if (hudController == null)
            {
                Debug.LogError("[UISetup3D] HUDController is null!");
                return false;
            }
            
            // Проверяем основные элементы
            if (hudController.LevelText == null)
            {
                Debug.LogError("[UISetup3D] LevelText is null!");
                return false;
            }
            
            if (hudController.DisguisePanel == null)
            {
                Debug.LogError("[UISetup3D] DisguisePanel is null!");
                return false;
            }
            
            if (hudController.GameOverPanel == null)
            {
                Debug.LogError("[UISetup3D] GameOverPanel is null!");
                return false;
            }
            
            Debug.Log("[UISetup3D] All UI elements are valid!");
            return true;
        }
    }
}