using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace StealthCastle.Isometric3D.Setup
{
    /// <summary>
    /// Автоматическая настройкацены для 3D изометрической версии.
    /// Создает все необходимые объекты для тестирования.
    /// </summary>
    public class SceneSetup3D : MonoBehaviour
    {
        [Header("Setup Settings")]
        [SerializeField] private bool autoSetup = true;
        [SerializeField] private bool includeTestEnvironment = true;
        [SerializeField] private bool includeTestEnemies = true;

        [Header("References")]
        [SerializeField] private Transform playerSpawnPoint;
        [SerializeField] private GameObject floorPrefab;
        [SerializeField] private GameObject wallPrefab;
        [SerializeField] private GameObject testEnemyPrefab;

        [Header("Lighting")]
        [SerializeField] private Vector3 lightDirection = new Vector3(-0.5f, -1f, -0.5f);
        [SerializeField] private Color lightColor = Color.white;
        [SerializeField] private float lightIntensity = 1.5f;

        private void Awake()
        {
            if (!autoSetup) return;
            
            Debug.Log("[SceneSetup3D] Starting scene setup...");
            
            // Удаляем текущие настройки, если они уже есть
            CleanupExistingSetup();
            
            // Создаем основные системы
            CreateCoreSystems();
            
            // Создаем игрока
            CreatePlayer();
            
            // Настраиваем освещение
            SetupLighting();
            
            // Создаем камеру
            SetupCamera();
            
            // Создаем тестовую среду
            if (includeTestEnvironment)
            {
                CreateTestEnvironment();
            }
            
            // Создаем тестовых врагов
            if (includeTestEnemies)
            {
                CreateTestEnemies();
            }
            
            Debug.Log("[SceneSetup3D] Scene setup complete!");
        }

        /// <summary>
        /// Очистка существующих настроек
        /// </summary>
        private void CleanupExistingSetup()
        {
            var existingManagers = FindObjectsOfType<Core.GameManager3D>();
            foreach (var manager in existingManagers)
            {
                if (manager.gameObject != this.gameObject)
                {
                    DestroyImmediate(manager.gameObject);
                }
            }
            
            var existingCameras = FindObjectsOfType<Core.CameraSystem3D>();
            foreach (var camera in existingCameras)
            {
                if (camera.gameObject != this.gameObject)
                {
                    DestroyImmediate(camera.gameObject);
                }
            }
            
            var existingInputs = FindObjectsOfType<Core.InputSystem3D>();
            foreach (var input in existingInputs)
            {
                if (input.gameObject != this.gameObject)
                {
                    DestroyImmediate(input.gameObject);
                }
            }
            
            var existingPlayers = FindObjectsOfType<Player.PlayerController3D>();
            foreach (var player in existingPlayers)
            {
                DestroyImmediate(player.gameObject);
            }
            
            // Очищаем теги камеры
            var cameras = FindObjectsOfType<Camera>();
            foreach (var cam in cameras)
            {
                if (cam.gameObject != Camera.main?.gameObject)
                {
                    DestroyImmediate(cam.gameObject);
                }
            }
        }

        /// <summary>
        /// Создание ядровых систем
        /// </summary>
        private void CreateCoreSystems()
        {
            // GameManager
            var managerGo = new GameObject("GameManager3D");
            var gameManager = managerGo.AddComponent<Core.GameManager3D>();
            DontDestroyOnLoad(managerGo);
            
            // InputSystem
            var inputGo = new GameObject("InputSystem3D");
            var inputSystem = inputGo.AddComponent<Core.InputSystem3D>();
            
            // Пробуем найти InputActions
            var inputActions = Resources.Load<InputActionAsset>("InputSystem_Actions");
            if (inputActions != null)
            {
                inputSystem.SetInputActions(inputActions);
            }
            else
            {
                Debug.LogWarning("[SceneSetup3D] InputSystem_Actions.inputactions not found in Resources!");
            }
            
            DontDestroyOnLoad(inputGo);
        }

        /// <summary>
        /// Создание игрока
        /// </summary>
        private void CreatePlayer()
        {
            var spawnPos = playerSpawnPoint != null ? playerSpawnPoint.position : Vector3.zero;
            
            // Создаем игрока
            var playerGo = new GameObject("Player3D");
            playerGo.transform.position = spawnPos;
            
            // Добавляем компоненты
            var rb = playerGo.AddComponent<Rigidbody>();
            var collider = playerGo.AddComponent<CapsuleCollider>();
            var playerController = playerGo.AddComponent<Player.PlayerController3D>();
            var disguiseSystem = playerGo.AddComponent<Mechanics.DisguiseSystem3D>();
            var disguiseVisual = playerGo.AddComponent<Player.PlayerDisguiseVisual3D>();
            var noiseEmitter = playerGo.AddComponent<Mechanics.NoiseEmitter3D>();
            
            // Настройка collider
            collider.height = 1.8f;
            collider.radius = 0.4f;
            collider.center = new Vector3(0f, 0.9f, 0f);
            
            // Настройка Rigidbody
            rb.useGravity = false;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.linearDamping = 5f;
            
            // Создаем визуальный объект
            var visualGo = new GameObject("PlayerVisual");
            visualGo.transform.SetParent(playerGo.transform, false);
            playerController.SetVisualTransform(visualGo.transform);
            // PlayerDisguiseVisual3D сам создаёт ModelContainer для модели маскировки
            
            // Настройка параметров NoiseEmitter
            noiseEmitter.Configure(8f, 1f);
            
            DontDestroyOnLoad(playerGo);
        }

        /// <summary>
        /// Настройка освещения
        /// </summary>
        private void SetupLighting()
        {
            // Проверяем, есть ли уже направленный свет
            var directionalLight = FindObjectOfType<Light>();
            if (directionalLight == null)
            {
                var lightGo = new GameObject("DirectionalLight");
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.transform.rotation = Quaternion.LookRotation(lightDirection.normalized);
                light.color = lightColor;
                light.intensity = lightIntensity;
                light.shadows = LightShadows.Soft;
                light.shadowStrength = 0.5f;
            }
            else
            {
                directionalLight.transform.rotation = Quaternion.LookRotation(lightDirection.normalized);
                directionalLight.color = lightColor;
                directionalLight.intensity = lightIntensity;
            }
            
            // Настройка ambient light
            RenderSettings.ambientLight = new Color(0.2f, 0.2f, 0.25f);
            RenderSettings.ambientMode = AmbientMode.Flat;
        }

        /// <summary>
        /// Настройка камеры
        /// </summary>
        private void SetupCamera()
        {
            var player = FindObjectOfType<Player.PlayerController3D>();
            
            // Проверяем, есть ли уже камера
            var mainCamera = Camera.main;
            if (mainCamera != null)
            {
                // Добавляем CameraSystem3D к существующей камере
                var cameraSystem = mainCamera.gameObject.GetComponent<Core.CameraSystem3D>();
                if (cameraSystem == null)
                {
                    cameraSystem = mainCamera.gameObject.AddComponent<Core.CameraSystem3D>();
                }
                
                if (player != null)
                {
                    cameraSystem.SetTarget(player.transform);
                }
                
                // Настройка изометрического вида
                cameraSystem.SetRotationAngle(35f);
                cameraSystem.SetOrthographic(false);
                mainCamera.fieldOfView = 60f;
            }
            else
            {
                // Создаем новую камеру
                var cameraGo = new GameObject("MainCamera");
                var camera = cameraGo.AddComponent<Camera>();
                camera.tag = "MainCamera";
                
                var cameraSystem = cameraGo.AddComponent<Core.CameraSystem3D>();
                if (player != null)
                {
                    cameraSystem.SetTarget(player.transform);
                }
                
                // Настройка изометрического вида
                cameraSystem.SetRotationAngle(35f);
                cameraSystem.SetOrthographic(false);
                camera.fieldOfView = 60f;
                
                DontDestroyOnLoad(cameraGo);
            }
        }

        /// <summary>
        /// Создание тестовой среды
        /// </summary>
        private void CreateTestEnvironment()
        {
            var groundLayer = LayerOrDefault("Ground");
            
            // Создаем пол
            CreateFloor(groundLayer);
            
            // Создаем стены
            CreateWalls(LayerOrDefault("Wall"));
            
            // Создаем тестовые пропсы для маскировки
            CreateDisguiseProps(LayerOrDefault("Prop"));
            
            // Создаем точки выхода
            CreateLevelExit();
        }

        /// <summary>
        /// Индекс слоя по имени; Default, если слой не заведён в Tags and Layers
        /// </summary>
        private static int LayerOrDefault(string layerName)
        {
            var layer = LayerMask.NameToLayer(layerName);
            return layer >= 0 ? layer : 0;
        }

        /// <summary>
        /// Создание пола
        /// </summary>
        private void CreateFloor(int groundLayer)
        {
            var floorSize = new Vector3(20f, 0.1f, 20f);
            
            if (floorPrefab != null)
            {
                var floor = Instantiate(floorPrefab, Vector3.zero, Quaternion.identity);
                floor.name = "Floor";
                floor.transform.localScale = floorSize;
                floor.layer = groundLayer;
            }
            else
            {
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "Floor";
                floor.transform.position = new Vector3(0f, -0.05f, 0f);
                floor.transform.localScale = floorSize;
                floor.layer = groundLayer;
                
                var renderer = floor.GetComponent<Renderer>();
                renderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                renderer.material.color = new Color(0.3f, 0.3f, 0.35f);
            }
        }

        /// <summary>
        /// Создание стен
        /// </summary>
        private void CreateWalls(int wallLayer)
        {
            var wallPositions = new Vector3[]
            {
                new Vector3(-9f, 1f, 0f),    // Левая стена
                new Vector3(9f, 1f, 0f),     // Правая стена
                new Vector3(0f, 1f, -9f),    // Задняя стена
                new Vector3(0f, 1f, 9f),     // Передняя стена
            };
            
            var wallScales = new Vector3[]
            {
                new Vector3(0.5f, 2f, 18f),  // Левая
                new Vector3(0.5f, 2f, 18f),  // Правая
                new Vector3(18f, 2f, 0.5f), // Задняя
                new Vector3(18f, 2f, 0.5f), // Передняя
            };
            
            var wallColors = new Color[]
            {
                new Color(0.4f, 0.3f, 0.35f),
                new Color(0.4f, 0.3f, 0.35f),
                new Color(0.4f, 0.3f, 0.35f),
                new Color(0.4f, 0.3f, 0.35f),
            };
            
            for (int i = 0; i < wallPositions.Length; i++)
            {
                if (wallPrefab != null)
                {
                    var wall = Instantiate(wallPrefab, wallPositions[i], Quaternion.identity);
                    wall.name = "Wall_" + i;
                    wall.transform.localScale = wallScales[i];
                    wall.layer = wallLayer;
                }
                else
                {
                    var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wall.name = "Wall_" + i;
                    wall.transform.position = wallPositions[i];
                    wall.transform.localScale = wallScales[i];
                    wall.layer = wallLayer;
                    
                    var renderer = wall.GetComponent<Renderer>();
                    renderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    renderer.material.color = wallColors[i];
                }
            }
        }

        /// <summary>
        /// Создание пропсов для маскировки
        /// </summary>
        private void CreateDisguiseProps(int propLayer)
        {
            var propPositions = new Vector3[]
            {
                new Vector3(-3f, 0.5f, -3f),
                new Vector3(3f, 0.5f, 3f),
                new Vector3(-4f, 0.5f, 4f),
                new Vector3(4f, 0.5f, -4f),
            };
            
            for (int i = 0; i < propPositions.Length; i++)
            {
                var prop = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                prop.name = "DisguiseProp_" + i;
                prop.transform.position = propPositions[i];
                prop.transform.localScale = new Vector3(0.6f, 1.2f, 0.6f);
                prop.layer = propLayer;
                
                var renderer = prop.GetComponent<Renderer>();
                renderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                renderer.material.color = new Color(0.6f, 0.4f, 0.2f);
                
                // Добавляем компонент DisguisableProp3D
                var disguisableProp = prop.AddComponent<Mechanics.DisguisableProp3D>();
                disguisableProp.SetDisplayName("Barrel");
            }
        }

        /// <summary>
        /// Создание точки выхода
        /// </summary>
        private void CreateLevelExit()
        {
            var exit = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            exit.name = "LevelExit";
            exit.transform.position = new Vector3(0f, 0.2f, 8f);
            exit.transform.localScale = new Vector3(1f, 0.4f, 1f);
            exit.layer = LayerOrDefault("Exit");
            
            var renderer = exit.GetComponent<Renderer>();
            renderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            renderer.material.color = new Color(0f, 1f, 0f);
            
            // Добавляем компонент LevelExit3D
            exit.AddComponent<World.LevelExit3D>();
            // LevelExit3D срабатывает по OnTriggerEnter
            exit.GetComponent<Collider>().isTrigger = true;
        }

        /// <summary>
        /// Создание тестовых врагов
        /// </summary>
        private void CreateTestEnemies()
        {
            var player = FindObjectOfType<Player.PlayerController3D>();
            if (player == null) return;
            
            var enemyPositions = new Vector3[]
            {
                new Vector3(-5f, 1f, -2f),
                new Vector3(5f, 1f, 2f),
                new Vector3(-2f, 1f, 5f),
            };
            
            for (int i = 0; i < enemyPositions.Length; i++)
            {
                if (testEnemyPrefab != null)
                {
                    var enemyGo = Instantiate(testEnemyPrefab, enemyPositions[i], Quaternion.identity);
                    enemyGo.name = "TestEnemy_" + i;
                    
                    var enemyAI = enemyGo.GetComponent<Enemies.EnemyAI3D>();
                    if (enemyAI != null)
                    {
                        enemyAI.PlayerTransform = player.transform;
                        enemyAI.AddWaypoint(enemyPositions[i] + new Vector3(0f, 0f, 2f));
                        enemyAI.AddWaypoint(enemyPositions[i] + new Vector3(0f, 0f, -2f));
                    }
                }
                else
                {
                    var enemyGo = new GameObject("TestEnemy_" + i);
                    enemyGo.transform.position = enemyPositions[i];
                    
                    var enemyAI = enemyGo.AddComponent<Enemies.EnemyAI3D>();
                    enemyAI.PlayerTransform = player.transform;
                    enemyAI.Configure(visionRange: 5f, fovDegrees: 90f, patrolSpeed: 1.5f);
                    
                    enemyAI.AddWaypoint(enemyPositions[i] + new Vector3(0f, 0f, 2f));
                    enemyAI.AddWaypoint(enemyPositions[i] + new Vector3(0f, 0f, -2f));
                    
                    // Создаем коллайдер
                    var collider = enemyGo.AddComponent<CapsuleCollider>();
                    collider.height = 1.8f;
                    collider.radius = 0.4f;
                    collider.isTrigger = true;
                    
                    // Rigidbody добавлен через RequireComponent и настраивается в EnemyAI3D.Awake
                }
            }
        }

        /// <summary>
        /// Создание scene setup через код
        /// </summary>
        [ContextMenu("Create 3D Scene Setup")]
        public void CreateSceneSetupManually()
        {
            autoSetup = true;
            
            // Очищаем текущую сцену
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}