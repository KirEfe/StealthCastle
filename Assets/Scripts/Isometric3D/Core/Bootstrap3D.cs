using UnityEngine;
using UnityEngine.InputSystem;

namespace StealthCastle.Isometric3D.Core
{
    /// <summary>
    /// Базовый класс для инициализации 3D изометрической версии игры.
    /// Создает и настраивает все необходимые системы автоматически.
    /// </summary>
    public class Bootstrap3D : MonoBehaviour
    {
        [Header("Core Systems")]
        [SerializeField] private GameManager3D gameManagerPrefab;
        [SerializeField] private CameraSystem3D cameraSystemPrefab;
        [SerializeField] private InputSystem3D inputSystemPrefab;

        [Header("Player Settings")]
        [SerializeField] private Player.PlayerController3D playerControllerPrefab;
        [SerializeField] private Transform playerSpawnPoint;

        [Header("World Settings")]
        [SerializeField] private World.LevelGenerator3D levelGeneratorPrefab;
        [SerializeField] private bool generateLevelOnStart = true;

        [Header("UI Settings")]
        [SerializeField] private UI.HUDController hudControllerPrefab;
        [SerializeField] private Canvas canvasPrefab;

        [Header("Input Settings")]
        [SerializeField] private InputActionAsset inputActions;

        private GameManager3D gameManagerInstance;
        private CameraSystem3D cameraSystemInstance;
        private InputSystem3D inputSystemInstance;
        private Player.PlayerController3D playerInstance;
        private World.LevelGenerator3D levelGeneratorInstance;
        private UI.HUDController hudControllerInstance;
        private Canvas canvasInstance;

        private void Awake()
        {
            // Проверяем, что это основной bootstrap
            if (FindObjectsOfType<Bootstrap3D>().Length > 1)
            {
                Destroy(gameObject);
                return;
            }

            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            InitializeAllSystems();
        }

        /// <summary>
        /// Инициализация всех систем
        /// </summary>
        private void InitializeAllSystems()
        {
            Debug.Log("[Bootstrap3D] Starting initialization...");

            // 1. Создаем Input System
            CreateInputSystem();

            // 2. Создаем Camera System
            CreateCameraSystem();

            // 3. Создаем Game Manager
            CreateGameManager();

            // 4. Создаем Canvas и UI
            CreateUI();

            // 5. Создаем Level Generator
            CreateLevelGenerator();

            // 6. Создаем Player
            CreatePlayer();

            // 7. Настраиваем связи между системами
            SetupSystemReferences();

            // 8. Начинаем игру
            StartGame();

            Debug.Log("[Bootstrap3D] Initialization complete!");
        }

        /// <summary>
        /// Создание системы ввода
        /// </summary>
        private void CreateInputSystem()
        {
            if (inputSystemPrefab != null)
            {
                inputSystemInstance = Instantiate(inputSystemPrefab);
                inputSystemInstance.name = "InputSystem3D";
                
                // Назначаем Input Actions (переопределяют ассет из префаба)
                if (inputActions != null)
                {
                    inputSystemInstance.SetInputActions(inputActions);
                }
            }
            else
            {
                Debug.LogWarning("[Bootstrap3D] InputSystem3D prefab not assigned, creating with defaults");
                var inputGo = new GameObject("InputSystem3D");
                inputSystemInstance = inputGo.AddComponent<InputSystem3D>();
                inputSystemInstance.SetInputActions(inputActions);
            }
            
            DontDestroyOnLoad(inputSystemInstance.gameObject);
        }

        /// <summary>
        /// Создание системы камеры
        /// </summary>
        private void CreateCameraSystem()
        {
            if (cameraSystemPrefab != null)
            {
                cameraSystemInstance = Instantiate(cameraSystemPrefab);
                cameraSystemInstance.name = "CameraSystem3D";
            }
            else
            {
                Debug.LogWarning("[Bootstrap3D] CameraSystem3D prefab not assigned, creating with defaults");
                var cameraGo = new GameObject("CameraSystem3D");
                var camera = cameraGo.AddComponent<Camera>();
                cameraSystemInstance = cameraGo.AddComponent<CameraSystem3D>();
                
                // Устанавливаем как основную камеру
                camera.tag = "MainCamera";
            }
            
            DontDestroyOnLoad(cameraSystemInstance.gameObject);
        }

        /// <summary>
        /// Создание менеджера игры
        /// </summary>
        private void CreateGameManager()
        {
            if (gameManagerPrefab != null)
            {
                gameManagerInstance = Instantiate(gameManagerPrefab);
                gameManagerInstance.name = "GameManager3D";
            }
            else
            {
                Debug.LogWarning("[Bootstrap3D] GameManager3D prefab not assigned, creating with defaults");
                var managerGo = new GameObject("GameManager3D");
                gameManagerInstance = managerGo.AddComponent<GameManager3D>();
            }
            
            DontDestroyOnLoad(gameManagerInstance.gameObject);
        }

        /// <summary>
        /// Создание UI системы
        /// </summary>
        private void CreateUI()
        {
            // Создаем Canvas
            if (canvasPrefab != null)
            {
                canvasInstance = Instantiate(canvasPrefab);
                canvasInstance.name = "MainCanvas";
            }
            else
            {
                var canvasGo = new GameObject("MainCanvas");
                canvasInstance = canvasGo.AddComponent<Canvas>();
                canvasInstance.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasInstance.sortingOrder = 100;
                
                canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
                canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }

            // Создаем HUD Controller
            if (hudControllerPrefab != null)
            {
                var hudGo = Instantiate(hudControllerPrefab, canvasInstance?.transform);
                hudControllerInstance = hudGo.GetComponent<UI.HUDController>();
                hudGo.name = "HUDController";
            }
            else
            {
                Debug.LogWarning("[Bootstrap3D] HUDController prefab not assigned, creating with defaults");
                var hudGo = new GameObject("HUDController");
                if (canvasInstance != null)
                {
                    hudGo.transform.SetParent(canvasInstance.transform, false);
                }
                hudControllerInstance = hudGo.AddComponent<UI.HUDController>();
            }
            
            if (canvasInstance != null)
            {
                DontDestroyOnLoad(canvasInstance.gameObject);
            }
        }

        /// <summary>
        /// Создание генератора уровней
        /// </summary>
        private void CreateLevelGenerator()
        {
            if (levelGeneratorPrefab != null)
            {
                levelGeneratorInstance = Instantiate(levelGeneratorPrefab);
                levelGeneratorInstance.name = "LevelGenerator3D";
            }
            else
            {
                Debug.LogWarning("[Bootstrap3D] LevelGenerator3D prefab not assigned, creating with defaults");
                var generatorGo = new GameObject("LevelGenerator3D");
                levelGeneratorInstance = generatorGo.AddComponent<World.LevelGenerator3D>();
            }
            
            DontDestroyOnLoad(levelGeneratorInstance.gameObject);
        }

        /// <summary>
        /// Создание игрока
        /// </summary>
        private void CreatePlayer()
        {
            if (playerControllerPrefab != null)
            {
                Vector3 spawnPosition = playerSpawnPoint != null ? playerSpawnPoint.position : Vector3.zero;
                playerInstance = Instantiate(playerControllerPrefab, spawnPosition, Quaternion.identity);
                playerInstance.name = "Player3D";
            }
            else
            {
                Debug.LogWarning("[Bootstrap3D] PlayerController3D prefab not assigned, creating with defaults");
                var playerGo = new GameObject("Player3D");
                playerInstance = playerGo.AddComponent<Player.PlayerController3D>();
                
                // Rigidbody и CapsuleCollider добавлены через RequireComponent
                var collider = playerGo.GetComponent<CapsuleCollider>();

                collider.height = 1.8f;
                collider.radius = 0.4f;

                // Создаем визуальный объект
                var visualGo = new GameObject("PlayerVisual");
                visualGo.transform.SetParent(playerGo.transform, false);
                playerInstance.SetVisualTransform(visualGo.transform);
            }
            
            DontDestroyOnLoad(playerInstance.gameObject);
        }

        /// <summary>
        /// Настройка связей между системами
        /// </summary>
        private void SetupSystemReferences()
        {
            if (gameManagerInstance != null)
            {
                // Назначаем ссылки в GameManager
                gameManagerInstance.SetReferences(playerInstance, levelGeneratorInstance, hudControllerInstance);
            }

            if (cameraSystemInstance != null && playerInstance != null)
            {
                // Камера следит за игроком
                cameraSystemInstance.SetTarget(playerInstance.transform);
            }

            if (playerInstance != null)
            {
                // П Layer Controller уже автоматически находит InputSystem через FindObjectOfType
                // Но можно явно назначить
                var inputSystem = FindObjectOfType<InputSystem3D>();
                if (inputSystem != null)
                {
                    // PlayerController3D уже ищет InputSystem3D через FindObjectOfType
                }
            }
        }

        /// <summary>
        /// Запуск игры
        /// </summary>
        private void StartGame()
        {
            if (gameManagerInstance != null)
            {
                // Устанавливаем начальное состояние
                gameManagerInstance.SetGameState(GameManager3D.GameState.Loading);
                
                if (generateLevelOnStart && levelGeneratorInstance != null)
                {
                    // Генерируем уровень
                    var rng = new System.Random(1); // Детерминированный сид для тестирования
                    levelGeneratorInstance.Generate(rng, playerInstance);
                }
                
                // Начинаем игру
                gameManagerInstance.SetGameState(GameManager3D.GameState.Playing);
                
                // Включаем управление игроком
                playerInstance?.SetInputEnabled(true);
            }
        }

        /// <summary>
        /// Получение текущего GameManager
        /// </summary>
        public GameManager3D GetGameManager()
        {
            return gameManagerInstance;
        }

        /// <summary>
        /// Получение текущего CameraSystem
        /// </summary>
        public CameraSystem3D GetCameraSystem()
        {
            return cameraSystemInstance;
        }

        /// <summary>
        /// Получение текущего InputSystem
        /// </summary>
        public InputSystem3D GetInputSystem()
        {
            return inputSystemInstance;
        }

        /// <summary>
        /// Получение текущего игрока
        /// </summary>
        public Player.PlayerController3D GetPlayer()
        {
            return playerInstance;
        }
    }
}