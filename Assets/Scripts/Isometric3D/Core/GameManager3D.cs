using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace StealthCastle.Isometric3D.Core
{
    /// <summary>
    /// Основной менеджер игры для 3D изометрической версии.
    /// Управляет состоянием игры, загрузкой уровней и переходом между сценами.
    /// </summary>
    public class GameManager3D : MonoBehaviour
    {
        public static GameManager3D Instance { get; private set; }

        [Header("Game Settings")]
        [SerializeField] private string mainMenuScene = "MainMenu";
        [SerializeField] private string gameScene = "Game3D";
        [SerializeField] private string loadingScene = "Loading";
        [SerializeField] private string hubScene = "Isometric3D_Hub"; // Логово: забег начинается и заканчивается здесь

        [Header("References")]
        [SerializeField] private Player.PlayerController3D playerController;
        [SerializeField] private World.LevelGenerator3D levelGenerator;
        [SerializeField] private UI.HUDController hudController;

        [Header("Game Over")]
        [SerializeField] private float autoRestartDelay = 3f; // Перезапуск сцены после поимки, если игрок не нажал кнопку

        [Header("Level Intro")]
        [SerializeField] private float planningTime = 4f; // Общий план со стражами в движении — время продумать маршрут

        private float gameOverTimer;
        private CameraSystem3D cameraSystem;
        private Coroutine introRoutine;
        private int initializedSceneHandle = -1;
        private int runSeed; // Зерно забега: каждый забег — новые уровни
        private int layoutVariant; // Перезапуск уровня (F4) — другая раскладка того же уровня

        /// <summary>
        /// Назначение ссылок на системы, созданные из кода. null-аргументы игнорируются.
        /// </summary>
        public void SetReferences(Player.PlayerController3D player, World.LevelGenerator3D generator, UI.HUDController hud)
        {
            if (player != null) playerController = player;
            if (generator != null) levelGenerator = generator;
            if (hud != null) hudController = hud;
        }

        /// <summary>Текущее состояние игры</summary>
        // Intro — вступление уровня: общий план, стражи ходят, управления нет. Hub — игрок гуляет по логову
        public enum GameState { Menu, Loading, Playing, Paused, GameOver, Victory, Intro, Hub }
        public GameState CurrentState { get; private set; } = GameState.Menu;

        /// <summary>Текущий уровень</summary>
        public int CurrentLevel { get; private set; } = 1;

        /// <summary>Текущий забег (null вне забега и в тестовых сценах)</summary>
        public Meta.RunState3D Run { get; private set; }

        /// <summary>Есть ли куда вернуться: сцена хаба в Build Settings</summary>
        public bool CanReturnToHub => !string.IsNullOrEmpty(hubScene) && Application.CanStreamedLevelBeLoaded(hubScene);

        /// <summary>Максимальный разблокированный уровень</summary>
        public int MaxUnlockedLevel { get; private set; } = 1;

        private void Awake()
        {
            // Singleton pattern
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Отладочная панель F1 живёт вместе с менеджером — во всех сценах
            if (GetComponent<Testing.DebugPanel3D>() == null) gameObject.AddComponent<Testing.DebugPanel3D>();
#endif
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        /// <summary>
        /// Менеджер переживает перезагрузку сцены — заново находим ссылки и состояние
        /// </summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (Instance != this || scene.handle == initializedSceneHandle) return;
            InitializeGame();
        }

        private void Update()
        {
            // После поимки или победы — перезапуск по кнопке или по таймеру
            if (CurrentState != GameState.GameOver && CurrentState != GameState.Victory) return;

            gameOverTimer += Time.unscaledDeltaTime;
            bool restartPressed = StartPressed();

            // Забег из хаба: решение за игроком, без таймера
            if (Run != null && CanReturnToHub)
            {
                if (CurrentState == GameState.Victory && levelGenerator != null)
                {
                    if (restartPressed) LoadLevel(CurrentLevel + 1); // Глубже: больше добычи, но и рискуешь всей
                    else if (EscapePressed()) EndRun(escaped: true); // Домой со всей добычей
                }
                else if (restartPressed)
                {
                    EndRun(escaped: false);
                }
                return;
            }

            if (restartPressed || gameOverTimer >= autoRestartDelay)
            {
                if (CurrentState == GameState.Victory && levelGenerator != null)
                {
                    LoadLevel(CurrentLevel + 1); // Следующий уровень того же забега
                }
                else
                {
                    ReloadActiveScene(); // Поимка = конец забега
                }
            }
        }

        private void Start()
        {
            // Для сцены, с которой стартовала игра, sceneLoaded уже пришёл раньше Start — не инициализируем дважды
            if (initializedSceneHandle != SceneManager.GetActiveScene().handle)
            {
                InitializeGame();
            }
        }

        /// <summary>
        /// Инициализация игры при старте
        /// </summary>
        private void InitializeGame()
        {
            initializedSceneHandle = SceneManager.GetActiveScene().handle;
            Time.timeScale = 1f;
            ResolveReferences();

            // Загружаем сохраненные данные
            LoadGameData();
            
            // Проверяем, с какой сцены начать
            if (SceneManager.GetActiveScene().name == mainMenuScene)
            {
                SetGameState(GameState.Menu);
            }
            else if (SceneManager.GetActiveScene().name == hubScene)
            {
                // Логовом управляет HubController3D; менеджер только отдаёт управление игроку
                Run = null;
                SetGameState(GameState.Hub);
            }
            else if (SceneManager.GetActiveScene().name == gameScene)
            {
                StartNewGame();
            }
            else
            {
                // Тестовые сцены (уровень собран вручную) стартуют сразу в игре
                // Выходы ручного уровня; у сгенерированного их подключает LevelGenerator3D
                foreach (var exit in FindObjectsByType<World.LevelExit3D>(FindObjectsSortMode.None))
                {
                    exit.OnLevelCompleted += CompleteLevel;
                }
                SetGameState(GameState.Playing);
            }
        }

        /// <summary>
        /// Загрузка данных игры (уровни, настройки и т.д.)
        /// </summary>
        private void LoadGameData()
        {
            // В будущем: загрузка из PlayerPrefs или файла
            MaxUnlockedLevel = PlayerPrefs.GetInt("MaxUnlockedLevel", 1);
        }

        /// <summary>
        /// Сохранение данных игры
        /// </summary>
        public void SaveGameData()
        {
            PlayerPrefs.SetInt("MaxUnlockedLevel", MaxUnlockedLevel);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Установка состояния игры
        /// </summary>
        public void SetGameState(GameState newState)
        {
            CurrentState = newState;
            
            switch (newState)
            {
                case GameState.Menu:
                    OnMenuState();
                    break;
                case GameState.Loading:
                    OnLoadingState();
                    break;
                case GameState.Playing:
                    OnPlayingState();
                    break;
                case GameState.Paused:
                    OnPausedState();
                    break;
                case GameState.GameOver:
                    OnGameOverState();
                    break;
                case GameState.Victory:
                    OnVictoryState();
                    break;
                case GameState.Intro:
                    // Игрок смотрит на уровень, но ещё не управляет
                    playerController?.SetInputEnabled(false);
                    break;
                case GameState.Hub:
                    playerController?.SetInputEnabled(true);
                    break;
            }
            
            Debug.Log($"[GameManager3D] Game state changed to: {newState}");
        }

        private void OnMenuState()
        {
            // Отключаем управление игроком
            playerController?.SetInputEnabled(false);
        }

        private void OnLoadingState()
        {
            // Показываем экран загрузки
        }

        private void OnPlayingState()
        {
            // Включаем управление игроком
            playerController?.SetInputEnabled(true);
        }

        private void OnPausedState()
        {
            // Отключаем управление игроком
            playerController?.SetInputEnabled(false);
        }

        private void OnGameOverState()
        {
            // Отключаем управление игроком
            playerController?.SetInputEnabled(false);
        }

        private void OnVictoryState()
        {
            // Отключаем управление игроком
            playerController?.SetInputEnabled(false);
            
            // Разблокируем следующий уровень, если текущий был пройден
            if (CurrentLevel == MaxUnlockedLevel)
            {
                MaxUnlockedLevel++;
                SaveGameData();
            }
        }

        /// <summary>
        /// Начало новой игры
        /// </summary>
        public void StartNewGame()
        {
            CurrentLevel = 1;
            runSeed = System.Environment.TickCount;
            layoutVariant = 0;

            // Снаряжение из хаба: артефакт с верстака + улучшения скупщика
            var progress = Meta.MetaProgress3D.Current;
            var config = Meta.MetaConfig3D.Load();
            var artifact = config != null ? config.FindArtifact(progress.selectedArtifact) : null;
            if (artifact != null && !progress.IsUnlocked(artifact)) artifact = null;
            Run = new Meta.RunState3D(artifact);
            Meta.Loadout3D.Apply(artifact, progress, config, playerController, levelGenerator);

            LoadLevel(CurrentLevel);
        }

        /// <summary>
        /// Из хаба — в замок: новый забег в игровой сцене
        /// </summary>
        public void StartRunFromHub()
        {
            LoadScene(gameScene);
        }

        /// <summary>
        /// Сундук открыт: добыча забега растёт с глубиной. Возвращает, сколько добавилось
        /// </summary>
        public int CollectChest()
        {
            if (Run == null || CurrentState != GameState.Playing) return 0;

            var config = Meta.MetaConfig3D.Load();
            int amount = config != null ? config.ChestLoot(CurrentLevel) : 20;
            Run.CarriedLoot += amount;
            Run.ChestsOpened++;
            hudController?.UpdateLoot(Run.CarriedLoot, amount);
            return amount;
        }

        /// <summary>
        /// Конец забега: добыча (вся — если ушёл сам, часть — если поймали) уходит в тайник, статистика — в сохранение,
        /// итог показывается в хабе
        /// </summary>
        public void EndRun(bool escaped)
        {
            if (Run == null) return;

            var progress = Meta.MetaProgress3D.Current;
            var config = Meta.MetaConfig3D.Load();
            int kept = escaped ? Run.CarriedLoot : KeptIfCaught();

            progress.loot += kept;
            progress.totalLootBanked += kept;
            progress.runs++;
            if (escaped) progress.escapes++;
            progress.deepestLevel = Mathf.Max(progress.deepestLevel, CurrentLevel);
            progress.levelsCleared += Run.LevelsCleared;
            progress.chestsOpened += Run.ChestsOpened;
            Meta.MetaProgress3D.Save();

            Meta.RunReport3D.Pending = new Meta.RunReport3D
            {
                Escaped = escaped,
                Level = CurrentLevel,
                LevelsCleared = Run.LevelsCleared,
                Carried = Run.CarriedLoot,
                Kept = kept,
                Chests = Run.ChestsOpened,
            };
            Run = null;
            LoadScene(hubScene);
        }

        private int KeptIfCaught()
        {
            var config = Meta.MetaConfig3D.Load();
            float share = config != null ? config.KeptWhenCaught(Meta.MetaProgress3D.Current) : 0.25f;
            return Mathf.FloorToInt(Run.CarriedLoot * share);
        }

        private void LoadScene(string sceneName)
        {
            if (introRoutine != null)
            {
                StopCoroutine(introRoutine);
                introRoutine = null;
            }
            Time.timeScale = 1f;
            SetGameState(GameState.Loading);
#if UNITY_EDITOR
            // В редакторе сцене не обязательно быть в Build Settings
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                foreach (var guid in UnityEditor.AssetDatabase.FindAssets($"t:Scene {sceneName}"))
                {
                    var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                    if (System.IO.Path.GetFileNameWithoutExtension(path) == sceneName)
                    {
                        EditorSceneManager.LoadSceneInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
                        return;
                    }
                }
            }
#endif
            SceneManager.LoadScene(sceneName);
        }

        /// <summary>
        /// Загрузка уровня
        /// </summary>
        public void LoadLevel(int levelNumber)
        {
            CurrentLevel = levelNumber;
            Time.timeScale = 1f; // Разморозка после экрана победы
            SetGameState(GameState.Loading);

            hudController?.ResetAllPanels();
            hudController?.UpdateLevelInfo(levelNumber);
            if (Run != null)
            {
                hudController?.UpdateLoot(Run.CarriedLoot, 0);
                if (Run.Artifact != null) hudController?.ShowArtifactInfo(Run.Artifact.DisplayName, Run.Artifact.Description);
            }

            if (introRoutine != null)
            {
                StopCoroutine(introRoutine);
                introRoutine = null;
            }

            if (levelGenerator != null)
            {
                // Уровень собирается на глазах игрока на общем плане; затем вступление и только потом управление
                levelGenerator.Clear();
                var rng = new System.Random(unchecked(runSeed + levelNumber * 7919 + layoutVariant * 104729));
                levelGenerator.Generate(rng, playerController,
                    onBuilt: () => introRoutine = StartCoroutine(LevelIntro()),
                    onLayout: bounds => cameraSystem?.ShowOverview(bounds));
            }
            else
            {
                playerController?.SetInputEnabled(true);
                SetGameState(GameState.Playing);
            }
        }

        /// <summary>
        /// Завершение уровня (победа)
        /// </summary>
        public void CompleteLevel()
        {
            if (CurrentState != GameState.Playing) return;

            gameOverTimer = 0f;
            SetGameState(GameState.Victory);
            Time.timeScale = 0f; // Уровень пройден — мир замирает, стражи не реагируют под экраном победы

            if (Run != null && CanReturnToHub)
            {
                var config = Meta.MetaConfig3D.Load();
                Run.LevelsCleared = CurrentLevel;
                Run.CarriedLoot += config != null ? config.LevelClearLoot(CurrentLevel) : 0;
                hudController?.UpdateLoot(Run.CarriedLoot, 0);
                hudController?.ShowVictoryScreen(
                    $"Level {CurrentLevel} cleared!\n\nLoot carried: {Run.CarriedLoot}\n" +
                    $"<size=70%>If caught deeper, you keep only {KeptIfCaught()}</size>\n\n" +
                    "SPACE - go deeper\nH - escape to the hideout with all loot");
            }
            else
            {
                hudController?.ShowVictoryScreen();
            }
        }

        /// <summary>
        /// Проигрыш
        /// </summary>
        public void GameOver()
        {
            // Только во время игры: несколько стражей могут поймать в один кадр, а после победы ловить уже некого
            if (CurrentState != GameState.Playing) return;

            gameOverTimer = 0f;
            SetGameState(GameState.GameOver);
            Time.timeScale = 0f; // Мир замирает до перезапуска

            if (Run != null && CanReturnToHub)
            {
                hudController?.ShowGameOverScreen(
                    $"Caught on level {CurrentLevel}!\n\nLoot: you keep {KeptIfCaught()} of {Run.CarriedLoot}\n\n" +
                    "SPACE - back to the hideout");
            }
            else
            {
                hudController?.ShowGameOverScreen();
            }
        }

        /// <summary>
        /// Перезагрузка текущей сцены (смерть = начать заново)
        /// </summary>
        public void ReloadActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            Time.timeScale = 1f;
            SetGameState(GameState.Loading);
#if UNITY_EDITOR
            // В редакторе сцене не нужно быть в Build Settings
            EditorSceneManager.LoadSceneInPlayMode(scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(scene.name);
#endif
        }

        /// <summary>
        /// Поиск систем текущей сцены: ссылки из прошлой сцены уничтожены вместе с ней
        /// </summary>
        private void ResolveReferences()
        {
            if (playerController == null) playerController = FindFirstObjectByType<Player.PlayerController3D>();
            if (levelGenerator == null) levelGenerator = FindFirstObjectByType<World.LevelGenerator3D>();
            if (hudController == null) hudController = FindFirstObjectByType<UI.HUDController>();
            if (cameraSystem == null) cameraSystem = FindFirstObjectByType<CameraSystem3D>();
        }

        /// <summary>
        /// Вступление уровня после сборки: общий план, стражи уже ходят (но не замечают), игрок планирует.
        /// Пробел — пропустить. Затем камера возвращается к игроку и только после этого даётся управление
        /// </summary>
        private System.Collections.IEnumerator LevelIntro()
        {
            SetGameState(GameState.Intro);
            hudController?.ShowIntroHint(true);

            float timeLeft = planningTime;
            while (timeLeft > 0f && !StartPressed())
            {
                hudController?.SetIntroCountdown(timeLeft);
                timeLeft -= Time.unscaledDeltaTime;
                yield return null;
            }
            hudController?.ShowIntroHint(false);

            if (cameraSystem != null)
            {
                cameraSystem.HideOverview();
                yield return new WaitForSecondsRealtime(cameraSystem.OverviewTransitionTime);
            }

            introRoutine = null;
            SetGameState(GameState.Playing);
        }

        private static bool StartPressed()
        {
            return (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
        }

        private static bool EscapePressed()
        {
            return (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame)
                || (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);
        }

        /// <summary>
        /// Перезапуск уровня
        /// </summary>
        /// <summary>
        /// Отладка: добыча — при себе в забеге или сразу в тайник в логове
        /// </summary>
        public void AddDebugLoot(int amount)
        {
            if (Run != null)
            {
                Run.CarriedLoot += amount;
                hudController?.UpdateLoot(Run.CarriedLoot, amount);
                return;
            }
            Meta.MetaProgress3D.Current.loot += amount;
            Meta.MetaProgress3D.Save();
        }

        public void RestartLevel()
        {
            layoutVariant++;
            LoadLevel(CurrentLevel);
        }

        /// <summary>
        /// Переход на следующий уровень
        /// </summary>
        public void NextLevel()
        {
            if (CurrentLevel < MaxUnlockedLevel)
            {
                LoadLevel(CurrentLevel + 1);
            }
            else
            {
                // Все уровни пройдены
                Debug.Log("[GameManager3D] All levels completed!");
            }
        }

        /// <summary>
        /// Возврат в главное меню
        /// </summary>
        public void ReturnToMenu()
        {
            SetGameState(GameState.Loading);
            SceneManager.LoadScene(mainMenuScene);
        }

        /// <summary>
        /// Пауза игры
        /// </summary>
        public void PauseGame()
        {
            if (CurrentState == GameState.Playing)
            {
                SetGameState(GameState.Paused);
                Time.timeScale = 0f;
            }
        }

        /// <summary>
        /// Продолжение игры
        /// </summary>
        public void ResumeGame()
        {
            if (CurrentState == GameState.Paused)
            {
                SetGameState(GameState.Playing);
                Time.timeScale = 1f;
            }
        }

        /// <summary>
        /// Выход из игры
        /// </summary>
        public void QuitGame()
        {
            Debug.Log("[GameManager3D] Quitting game...");
            #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
            #else
                Application.Quit();
            #endif
        }
    }
}