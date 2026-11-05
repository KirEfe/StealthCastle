using UnityEngine;
using TMPro;

namespace StealthCastle.Isometric.Core
{
    /// <summary>
    /// Менеджер забега: замыкает цикл
    /// стартовый экран → выбор артефакта → сборка → уровень → выход/смерть → итоги.
    /// </summary>
    public class RunManager : MonoBehaviour
    {
        /// <summary>Глобальная ссылка на активный RunManager (для триггеров).</summary>
        public static RunManager Instance { get; private set; }

        [SerializeField] private World.LevelGenerator levelGenerator; // генератор уровня
        [SerializeField] private Player.IsoPlayerController playerController;
        [SerializeField] private GameObject startScreen;              // панель стартового экрана
        [SerializeField] private GameObject resultsScreen;            // панель итогов
        [SerializeField] private UI.ArtifactSelector artifactSelector; // выбор артефакта

        /// <summary>Текущая фаза цикла.</summary>
        public RunPhase Phase { get; private set; } = RunPhase.StartScreen;

        /// <summary>Чем закончился забег (для экрана итогов).</summary>
        public string LastRunResult { get; private set; } = "";

        private void Start()
        {
            Instance = this;
            ShowStartScreen();
        }

        // Стартовый экран: артефакт выбирается до начала забега
        private void ShowStartScreen()
        {
            Phase = RunPhase.StartScreen;
            if (startScreen != null) startScreen.SetActive(true);
            if (resultsScreen != null) resultsScreen.SetActive(false);
            if (playerController != null) playerController.SetInputEnabled(false);
            if (artifactSelector != null) artifactSelector.Show(rules => BeginRun());
        }

        // Кнопка «Начать забег»: применить артефакт и запустить сборку
        public void BeginRun()
        {
            Phase = RunPhase.Building;
            if (startScreen != null) startScreen.SetActive(false);
            if (artifactSelector != null) artifactSelector.ApplySelected();

            // Сброс состояния игрока к началу забега
            ResetPlayer();

            // Применить бонусы маскировки от выбранного артефакта (Шаг 4, ТЗ 2.2)
            ApplyArtifactPlayerBonuses();

            var rng = new System.Random(); // seed для отладки можно захардкодить
            levelGenerator.Generate(rng, playerController, OnPlayerCaught);
            Phase = RunPhase.Playing;
        }

        // Бонусы игрока от артефакта (таймер дыхания + радиус скана) — не через веса
        private void ApplyArtifactPlayerBonuses()
        {
            if (artifactSelector == null) return;

            var selected = GetSelectedArtifact(artifactSelector);
            if (selected == null) return;

            var disguise = playerController != null
                ? playerController.GetComponent<Mechanics.IsoDisguiseSystem>()
                : null;
            if (disguise == null) return;

            var bonus = selected.DisguiseDurationBonus;
            var scanBonus = selected.ScanRadiusBonus;

            if (bonus != 0f) SetField(disguise, "disguiseDuration", disguise.TotalTime + bonus);
            if (scanBonus != 0f) SetField(disguise, "scanRadius", 2f + scanBonus); // 2f — дефолт скана

            if (bonus != 0f || scanBonus != 0f)
                UnityEngine.Debug.Log($"[RunManager] Артефакт '{selected.DisplayName}': дыхание +{bonus:F0}с, скан +{scanBonus:F1}м");
        }

        // Прочитать выбранный артефакт из ArtifactSelector
        private static Core.ArtifactData GetSelectedArtifact(UI.ArtifactSelector selector)
            => selector?.GetSelected();

        // Runtime-хелпер для приватного [SerializeField] поля (без UnityEditor — чтобы компилировалось в игре)
        private static void SetField(object target, string fieldName, object value)
        {
            var type = target.GetType();
            var field = type.GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(target, value);
                return;
            }

            var prop = type.GetProperty(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(target, value);
                return;
            }

            UnityEngine.Debug.LogWarning($"[RunManager] Поле '{fieldName}' не найдено у {type.Name}");
        }

        // Сброс игрока в стартовую точку + снятие маскировки
        private void ResetPlayer()
        {
            if (playerController == null) return;

            // Телепорт в центр стартовой комнаты (StartPiece спавнится в (0,0))
            playerController.transform.position = Vector3.zero;

            // Снять зависшую маскировку — иначе новый забег начнётся «невидимым»
            var disguise = playerController.GetComponent<Mechanics.IsoDisguiseSystem>();
            disguise?.RemoveDisguise("Новый забег");
        }

        // Игрок дошёл до выхода — победа
        public void OnLevelCompleted()
        {
            LastRunResult = "Уровень пройден!";
            EndRun();
        }

        // Враг поймал игрока — смерть = конец забега
        public void OnPlayerCaught()
        {
            LastRunResult = "Вас заметили. Забег окончен.";
            EndRun();
        }

        private void EndRun()
        {
            Phase = RunPhase.Results;
            if (playerController != null) playerController.SetInputEnabled(false);
            levelGenerator?.Clear(); // уничтожить уровень и врагов — иначе живут после смерти
            if (resultsScreen != null)
            {
                resultsScreen.SetActive(true);
                var txt = resultsScreen.GetComponentInChildren<TextMeshProUGUI>();
                if (txt != null)
                    txt.text = LastRunResult + "\n\n[Пробел] — новый забег";
            }
        }

        private void Update()
        {
            // На экране итогов Пробел возвращает на стартовый экран
            if (Phase == RunPhase.Results &&
                UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame)
                ShowStartScreen();
        }
    }
}
