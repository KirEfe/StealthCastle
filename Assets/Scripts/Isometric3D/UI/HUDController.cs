using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace StealthCastle.Isometric3D.UI
{
    /// <summary>
    /// Контроллер HUD для 3D изометрической версии.
    /// Управляет отображением информации об уровне, здоровье, маскировке и т.д.
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        [Header("Level Info")]
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private string levelFormat = "Level: {0}";

        [Header("Loot")]
        [SerializeField] private TextMeshProUGUI lootText;
        [SerializeField] private string lootFormat = "Loot: {0}";
        [SerializeField] private Color lootFlashColor = new Color(1f, 0.82f, 0.25f);
        [SerializeField] private float lootFlashDuration = 1.2f;

        [Header("Disguise Info")]
        [SerializeField] private GameObject disguisePanel;
        [SerializeField] private TextMeshProUGUI disguiseTimerText;
        [SerializeField] private Image disguiseFillImage;
        [SerializeField] private string timerFormat = "{0:0.0}s";

        [Header("Game Over")]
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private TextMeshProUGUI gameOverText;

        [Header("Victory")]
        [SerializeField] private GameObject victoryPanel;
        [SerializeField] private TextMeshProUGUI victoryText;

        [Header("Pause Menu")]
        [SerializeField] private GameObject pausePanel;

        [Header("Level Intro")]
        [SerializeField] private GameObject introHintPanel;
        [SerializeField] private TextMeshProUGUI introHintText;
        [SerializeField] private string introHintFormat = "Plan your route   |   SPACE to start ({0:0})";

        [Header("Artifact Selection")]
        [SerializeField] private GameObject artifactPanel;
        [SerializeField] private TextMeshProUGUI artifactNameText;
        [SerializeField] private TextMeshProUGUI artifactDescriptionText;

        private Player.PlayerController3D playerController;
        private Mechanics.DisguiseSystem3D disguiseSystem;
        private Core.GameManager3D gameManager;
        private float lootFlashTimer;

        /// <summary>Текст уровня</summary>
        public TextMeshProUGUI LevelText => levelText;

        /// <summary>Панель маскировки</summary>
        public GameObject DisguisePanel => disguisePanel;

        /// <summary>Панель проигрыша</summary>
        public GameObject GameOverPanel => gameOverPanel;

        // Привязка UI-элементов, созданных из кода (UISetup3D)

        public void BindLevelText(TextMeshProUGUI text) => levelText = text;

        public void BindLootText(TextMeshProUGUI text) => lootText = text;

        public void BindDisguisePanel(GameObject panel, TextMeshProUGUI timerText, Image fillImage)
        {
            disguisePanel = panel;
            disguiseTimerText = timerText;
            disguiseFillImage = fillImage;
        }

        public void BindGameOverPanel(GameObject panel, TextMeshProUGUI text)
        {
            gameOverPanel = panel;
            gameOverText = text;
        }

        public void BindVictoryPanel(GameObject panel, TextMeshProUGUI text)
        {
            victoryPanel = panel;
            victoryText = text;
        }

        public void BindPausePanel(GameObject panel) => pausePanel = panel;

        public void BindIntroHint(GameObject panel, TextMeshProUGUI text)
        {
            introHintPanel = panel;
            introHintText = text;
        }

        /// <summary>Подсказка вступления уровня: изучить уровень, Пробел — начать</summary>
        public void ShowIntroHint(bool show)
        {
            if (introHintPanel != null)
            {
                introHintPanel.SetActive(show);
            }
        }

        /// <summary>Обратный отсчёт до автоматического старта</summary>
        public void SetIntroCountdown(float secondsLeft)
        {
            if (introHintText != null)
            {
                introHintText.text = string.Format(introHintFormat, Mathf.Ceil(secondsLeft));
            }
        }

        public void BindArtifactPanel(GameObject panel, TextMeshProUGUI nameText, TextMeshProUGUI descriptionText)
        {
            artifactPanel = panel;
            artifactNameText = nameText;
            artifactDescriptionText = descriptionText;
        }

        private void Awake()
        {
            // Поиск необходимых компонентов
            playerController = FindObjectOfType<Player.PlayerController3D>();
            disguiseSystem = playerController != null ? 
                playerController.GetComponent<Mechanics.DisguiseSystem3D>() : null;
            gameManager = Core.GameManager3D.Instance;
        }

        private void Start()
        {
            // Начальная инициализация
            if (gameManager != null)
            {
                UpdateLevelInfo(gameManager.CurrentLevel);
            }
        }

        private void Update()
        {
            // Панель дыхания видна только в маскировке
            bool disguised = disguiseSystem != null && disguiseSystem.IsDisguised;
            if (disguisePanel != null && disguisePanel.activeSelf != disguised)
            {
                ShowDisguisePanel(disguised);
            }

            if (disguised)
            {
                UpdateDisguiseTimer();
            }

            // Свежая добыча подсвечивается и плавно гаснет
            if (lootFlashTimer > 0f && lootText != null)
            {
                lootFlashTimer -= Time.unscaledDeltaTime;
                lootText.color = Color.Lerp(Color.white, lootFlashColor, Mathf.Clamp01(lootFlashTimer / lootFlashDuration));
            }
        }

        /// <summary>
        /// Добыча при себе; gained > 0 — только что подобрано (вспышка и «+N»)
        /// </summary>
        public void UpdateLoot(int carried, int gained)
        {
            if (lootText == null) return;
            lootText.transform.parent.gameObject.SetActive(true);
            lootText.text = string.Format(lootFormat, carried) + (gained > 0 ? $"  <size=80%>+{gained}</size>" : "");
            lootFlashTimer = gained > 0 ? lootFlashDuration : 0f;
            lootText.color = gained > 0 ? lootFlashColor : Color.white;
        }

        /// <summary>
        /// Обновление информации об уровне
        /// </summary>
        public void UpdateLevelInfo(int levelNumber)
        {
            if (levelText != null)
            {
                levelText.text = string.Format(levelFormat, levelNumber);
            }
        }

        /// <summary>
        /// Обновление таймера маскировки
        /// </summary>
        private void UpdateDisguiseTimer()
        {
            if (disguiseTimerText != null)
            {
                float remaining = disguiseSystem.RemainingTime;
                disguiseTimerText.text = string.Format(timerFormat, remaining);
            }

            if (disguiseFillImage != null)
            {
                float fillAmount = disguiseSystem.IsDisguised ? 
                    disguiseSystem.RemainingTime / disguiseSystem.TotalTime : 0f;
                disguiseFillImage.fillAmount = Mathf.Clamp01(fillAmount);
                // Image без спрайта игнорирует fillAmount — сжимаем полосу масштабом (pivot слева)
                disguiseFillImage.rectTransform.localScale = new Vector3(Mathf.Clamp01(fillAmount), 1f, 1f);
            }
        }

        /// <summary>
        /// Показать панель маскировки
        /// </summary>
        public void ShowDisguisePanel(bool show)
        {
            if (disguisePanel != null)
            {
                disguisePanel.SetActive(show);
            }
        }

        /// <summary>
        /// Показать экран Game Over
        /// </summary>
        public void ShowGameOverScreen(string message = null)
        {
            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(true);
                if (gameOverText != null)
                {
                    gameOverText.text = message ?? "Game Over!\n\nPress Space to restart";
                }
            }
        }

        /// <summary>
        /// Скрыть экран Game Over
        /// </summary>
        public void HideGameOverScreen()
        {
            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(false);
            }
        }

        /// <summary>
        /// Показать экран победы
        /// </summary>
        public void ShowVictoryScreen(string message = null)
        {
            if (victoryPanel != null)
            {
                victoryPanel.SetActive(true);
                if (victoryText != null)
                {
                    victoryText.text = message ?? "Level Complete!\n\nPress Space to continue";
                }
            }
        }

        /// <summary>
        /// Скрыть экран победы
        /// </summary>
        public void HideVictoryScreen()
        {
            if (victoryPanel != null)
            {
                victoryPanel.SetActive(false);
            }
        }

        /// <summary>
        /// Показать меню паузы
        /// </summary>
        public void ShowPauseMenu()
        {
            if (pausePanel != null)
            {
                pausePanel.SetActive(true);
            }
        }

        /// <summary>
        /// Скрыть меню паузы
        /// </summary>
        public void HidePauseMenu()
        {
            if (pausePanel != null)
            {
                pausePanel.SetActive(false);
            }
        }

        /// <summary>
        /// Показать информацию об артефакте
        /// </summary>
        public void ShowArtifactInfo(string name, string description)
        {
            if (artifactPanel != null)
            {
                artifactPanel.SetActive(true);
            }
            
            if (artifactNameText != null)
            {
                artifactNameText.text = name;
            }
            
            if (artifactDescriptionText != null)
            {
                artifactDescriptionText.text = description;
            }
        }

        /// <summary>
        /// Скрыть информацию об артефакте
        /// </summary>
        public void HideArtifactInfo()
        {
            if (artifactPanel != null)
            {
                artifactPanel.SetActive(false);
            }
        }

        /// <summary>
        /// Сброс всех панелей
        /// </summary>
        public void ResetAllPanels()
        {
            HideGameOverScreen();
            HideVictoryScreen();
            ShowIntroHint(false);
            HidePauseMenu();
            HideArtifactInfo();
            ShowDisguisePanel(false);
        }

        /// <summary>
        /// Установка видимости HUD
        /// </summary>
        public void SetHUDVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }
    }
}