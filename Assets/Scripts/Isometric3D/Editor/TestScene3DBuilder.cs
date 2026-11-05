using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace StealthCastle.Isometric3D.EditorTools
{
    /// <summary>
    /// Собирает тестовую 3D-сцену для проверки изометрической версии в редакторе:
    /// Universal Renderer для камеры, пол, стены, пропы для маскировки, выход, игрок и два стража.
    /// Меню: StealthCastle → 3D → Build Test Scene. Batchmode: -executeMethod ...TestScene3DBuilder.BuildFromCommandLine
    /// </summary>
    public static class TestScene3DBuilder
    {
        private const string ScenePath = "Assets/Scenes/Isometric3D_Test.unity";
        private const string AssetsFolder = "Assets/Scenes/Isometric3D_Test";
        private const string PipelinePath = "Assets/Settings/UniversalRP.asset";
        private const string RendererPath = "Assets/Settings/UniversalRenderer3D.asset";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";

        // Центр капсулы над полом: игрок и стражи стоят pivot'ом на этой высоте,
        // чтобы рейкасты зрения шли горизонтально над полом
        private const float BodyHeight = 0.9f;

        [MenuItem("StealthCastle/3D/Build Test Scene")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        public static void BuildFromCommandLine()
        {
            try
            {
                Build();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        private static void Build()
        {
            EnsureFolder(AssetsFolder);
            int rendererIndex = EnsureUniversalRenderer();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            int groundLayer = Layer("Ground");
            int wallLayer = Layer("Wall");
            int propLayer = Layer("Prop");
            int playerLayer = Layer("Player");
            int enemyLayer = Layer("Enemy");

            var floorMat = CreateMaterial("Floor", new Color(0.28f, 0.27f, 0.3f));
            var wallMat = CreateMaterial("Wall", new Color(0.55f, 0.53f, 0.5f));
            var propMat = CreateMaterial("Prop", new Color(0.6f, 0.4f, 0.2f));
            var exitMat = CreateMaterial("Exit", new Color(0.2f, 0.8f, 0.3f));
            var playerMat = CreateMaterial("Player", new Color(0.25f, 0.45f, 0.9f));
            var enemyMat = CreateMaterial("Enemy", new Color(0.85f, 0.25f, 0.2f));
            var markerMat = CreateMaterial("FacingMarker", new Color(0.95f, 0.95f, 0.9f));
            var coneMat = CreateVisionConeMaterial();

            SetupLighting();
            CreateCoreSystems();

            var level = new GameObject("Level").transform;
            CreateBlock("Floor", PrimitiveType.Cube, new Vector3(0f, -0.1f, 0f), new Vector3(20f, 0.2f, 20f), floorMat, groundLayer, level);

            // Внешние стены
            CreateBlock("Wall_N", PrimitiveType.Cube, new Vector3(0f, 1f, 10f), new Vector3(20.5f, 2f, 0.5f), wallMat, wallLayer, level);
            CreateBlock("Wall_S", PrimitiveType.Cube, new Vector3(0f, 1f, -10f), new Vector3(20.5f, 2f, 0.5f), wallMat, wallLayer, level);
            CreateBlock("Wall_E", PrimitiveType.Cube, new Vector3(10f, 1f, 0f), new Vector3(0.5f, 2f, 20.5f), wallMat, wallLayer, level);
            CreateBlock("Wall_W", PrimitiveType.Cube, new Vector3(-10f, 1f, 0f), new Vector3(0.5f, 2f, 20.5f), wallMat, wallLayer, level);

            // Внутренние стены — укрытия, ломают линию взгляда стражей
            CreateBlock("Wall_Inner_A", PrimitiveType.Cube, new Vector3(-4f, 1f, 2f), new Vector3(6f, 2f, 0.5f), wallMat, wallLayer, level);
            CreateBlock("Wall_Inner_B", PrimitiveType.Cube, new Vector3(5f, 1f, -2f), new Vector3(0.5f, 2f, 6f), wallMat, wallLayer, level);

            CreateProps(level, propMat, propLayer);
            CreateExit(level, exitMat);

            var player = CreatePlayer(playerMat, markerMat, playerLayer);
            CreateGuard("Guard_A", new Vector3(0f, BodyHeight, -4f),
                new[] { new Vector3(-6f, BodyHeight, -4f), new Vector3(3f, BodyHeight, -4f) },
                player.transform, enemyMat, markerMat, coneMat, enemyLayer, wallLayer);
            CreateGuard("Guard_B", new Vector3(2f, BodyHeight, 6f),
                new[] { new Vector3(-2f, BodyHeight, 7f), new Vector3(7f, BodyHeight, 4f) },
                player.transform, enemyMat, markerMat, coneMat, enemyLayer, wallLayer);

            CreateCamera(player.transform, rendererIndex);

            var ui = new GameObject("UI");
            ui.AddComponent<Setup.UISetup3D>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[TestScene3DBuilder] Scene saved: {ScenePath} (camera renderer index {rendererIndex})");
        }

        /// <summary>
        /// Добавляет Universal Renderer в URP-ассет, не трогая 2D Renderer по умолчанию
        /// (2D-сцены прототипа продолжают работать). Возвращает индекс рендерера.
        /// </summary>
        private static int EnsureUniversalRenderer()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null) pipeline = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null) throw new InvalidOperationException($"URP asset not found at {PipelinePath}");

            var pipelineSo = new SerializedObject(pipeline);
            var rendererList = pipelineSo.FindProperty("m_RendererDataList");

            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();

                // Берём PostProcessData у существующего рендерера, чтобы не лезть во внутренний API URP
                if (rendererList.arraySize > 0 && rendererList.GetArrayElementAtIndex(0).objectReferenceValue is ScriptableRendererData existing)
                {
                    var postProcess = new SerializedObject(existing).FindProperty("m_PostProcessData");
                    if (postProcess != null)
                    {
                        rendererData.postProcessData = postProcess.objectReferenceValue as PostProcessData;
                    }
                }

                AssetDatabase.CreateAsset(rendererData, RendererPath);
            }

            for (int i = 0; i < rendererList.arraySize; i++)
            {
                if (rendererList.GetArrayElementAtIndex(i).objectReferenceValue == rendererData) return i;
            }

            int index = rendererList.arraySize;
            rendererList.arraySize++;
            rendererList.GetArrayElementAtIndex(index).objectReferenceValue = rendererData;
            pipelineSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
            return index;
        }

        private static void SetupLighting()
        {
            var lightGo = new GameObject("Directional Light");
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.35f, 0.35f, 0.4f);
        }

        private static void CreateCoreSystems()
        {
            new GameObject("GameManager3D").AddComponent<Core.GameManager3D>();

            var inputSystem = new GameObject("InputSystem3D").AddComponent<Core.InputSystem3D>();
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (actions == null) throw new InvalidOperationException($"Input actions not found at {InputActionsPath}");
            SetField(inputSystem, "inputActions", p => p.objectReferenceValue = actions);
        }

        private static void CreateProps(Transform parent, Material material, int layer)
        {
            var barrels = new[] { new Vector3(-2f, 0.6f, -6f), new Vector3(3f, 0.6f, 4f), new Vector3(-8f, 0.6f, -8f) };
            for (int i = 0; i < barrels.Length; i++)
            {
                var barrel = CreateBlock($"Prop_Barrel_{i}", PrimitiveType.Cylinder, barrels[i], new Vector3(0.8f, 0.6f, 0.8f), material, layer, parent);
                barrel.AddComponent<Mechanics.DisguisableProp3D>().SetDisplayName("Бочка");
            }

            var crates = new[] { new Vector3(-6f, 0.5f, -1f), new Vector3(6f, 0.5f, 6f), new Vector3(-7f, 0.5f, 6f) };
            for (int i = 0; i < crates.Length; i++)
            {
                var crate = CreateBlock($"Prop_Crate_{i}", PrimitiveType.Cube, crates[i], Vector3.one, material, layer, parent);
                crate.AddComponent<Mechanics.DisguisableProp3D>().SetDisplayName("Ящик");
            }
        }

        private static void CreateExit(Transform parent, Material material)
        {
            var exit = CreateBlock("LevelExit", PrimitiveType.Cylinder, new Vector3(7.5f, 0.05f, 8f), new Vector3(1.5f, 0.05f, 1.5f), material, 0, parent);
            exit.GetComponent<Collider>().isTrigger = true; // LevelExit3D срабатывает по OnTriggerEnter
            exit.AddComponent<World.LevelExit3D>();
        }

        private static GameObject CreatePlayer(Material bodyMat, Material markerMat, int layer)
        {
            var player = new GameObject("Player3D") { tag = "Player", layer = layer };
            player.transform.position = new Vector3(-7f, BodyHeight, -7f);

            var collider = player.AddComponent<CapsuleCollider>();
            collider.height = 1.8f;
            collider.radius = 0.4f;
            var controller = player.AddComponent<Player.PlayerController3D>(); // Rigidbody через RequireComponent
            player.AddComponent<Mechanics.DisguiseSystem3D>();
            var disguiseVisual = player.AddComponent<Player.PlayerDisguiseVisual3D>();
            player.AddComponent<Mechanics.NoiseEmitter3D>();

            // Визуал поворачивается контроллером; модель игрока внутри скрывается при маскировке
            var visual = new GameObject("PlayerVisual").transform;
            visual.SetParent(player.transform, false);
            SetField(controller, "visualTransform", p => p.objectReferenceValue = visual);
            SetField(disguiseVisual, "modelContainer", p => p.objectReferenceValue = visual);
            SetField(disguiseVisual, "defaultPlayerModel", p => p.objectReferenceValue = CreateBodyPrefab("PlayerModel", bodyMat, markerMat));

            return player;
        }

        private static void CreateGuard(string name, Vector3 position, Vector3[] waypoints, Transform player,
            Material bodyMat, Material markerMat, Material coneMat, int layer, int wallLayer)
        {
            var guard = new GameObject(name) { tag = "Enemy", layer = layer };
            guard.transform.position = position;

            // Триггер: поимка считается по дистанции 0.6 м, сплошные капсулы не дали бы подойти
            var collider = guard.AddComponent<CapsuleCollider>();
            collider.height = 1.8f;
            collider.radius = 0.4f;
            collider.isTrigger = true;

            var ai = guard.AddComponent<Enemies.EnemyAI3D>(); // Rigidbody через RequireComponent
            guard.AddComponent<Mechanics.NoiseReceiver3D>();
            var cone = guard.AddComponent<Enemies.VisionCone3D>();
            SetField(cone, "coneMaterial", p => p.objectReferenceValue = coneMat);

            var visual = CreateBody("Visual", bodyMat, markerMat).transform;
            visual.SetParent(guard.transform, false);

            int wallMask = 1 << wallLayer;
            var so = new SerializedObject(ai);
            so.FindProperty("playerTransform").objectReferenceValue = player;
            so.FindProperty("visualTransform").objectReferenceValue = visual;
            so.FindProperty("wallMask").intValue = wallMask;
            so.FindProperty("obstacleMask").intValue = wallMask;
            var points = so.FindProperty("waypoints");
            points.arraySize = waypoints.Length;
            for (int i = 0; i < waypoints.Length; i++)
            {
                points.GetArrayElementAtIndex(i).vector3Value = waypoints[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateCamera(Transform target, int rendererIndex)
        {
            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.08f, 0.1f);
            cameraGo.AddComponent<AudioListener>();
            camera.GetUniversalAdditionalCameraData().SetRenderer(rendererIndex);

            var cameraSystem = cameraGo.AddComponent<Core.CameraSystem3D>();
            var so = new SerializedObject(cameraSystem);
            so.FindProperty("target").objectReferenceValue = target;
            so.FindProperty("rotationAngle").floatValue = 50f; // наклон вниз, близко к Hades
            so.FindProperty("zoomDistance").floatValue = 22f;
            so.FindProperty("fieldOfView").floatValue = 35f;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Стартовая позиция в редакторе, чтобы Scene/Game view сразу показывали уровень
            var rotation = Quaternion.Euler(50f, 45f, 0f);
            cameraGo.transform.SetPositionAndRotation(target.position + rotation * new Vector3(0f, 0f, -22f), rotation);
        }

        /// <summary>Капсула-тело с меткой «лица», чтобы было видно, куда смотрит персонаж</summary>
        private static GameObject CreateBody(string name, Material bodyMat, Material markerMat)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = name;
            UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.localScale = new Vector3(0.8f, 0.9f, 0.8f);
            body.GetComponent<Renderer>().sharedMaterial = bodyMat;

            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "FacingMarker";
            UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());
            marker.transform.SetParent(body.transform, false);
            marker.transform.localPosition = new Vector3(0f, 0.45f, 0.45f);
            marker.transform.localScale = new Vector3(0.5f, 0.12f, 0.2f);
            marker.GetComponent<Renderer>().sharedMaterial = markerMat;
            return body;
        }

        private static GameObject CreateBodyPrefab(string name, Material bodyMat, Material markerMat)
        {
            // PlayerDisguiseVisual3D сбрасывает localScale модели в 1 — масштаб держим на дочернем теле
            var root = new GameObject(name);
            CreateBody("Body", bodyMat, markerMat).transform.SetParent(root.transform, false);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{AssetsFolder}/{name}.prefab");
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateBlock(string name, PrimitiveType type, Vector3 position, Vector3 scale,
            Material material, int layer, Transform parent)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.layer = layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        private static Material CreateVisionConeMaterial()
        {
            var path = $"{AssetsFolder}/VisionCone.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                // Ассет нужен, чтобы прозрачный Unlit-шейдер попал в билд
                material = Enemies.VisionCone3D.CreateDefaultMaterial();
                AssetDatabase.CreateAsset(material, path);
            }
            return material;
        }

        /// <summary>Сцена в Build Settings — для перезапуска после поимки в билде</summary>
        private static void AddSceneToBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes;
            foreach (var s in scenes)
            {
                if (s.path == path) return;
            }
            var updated = new EditorBuildSettingsScene[scenes.Length + 1];
            scenes.CopyTo(updated, 0);
            updated[scenes.Length] = new EditorBuildSettingsScene(path, true);
            EditorBuildSettings.scenes = updated;
        }

        private static Material CreateMaterial(string name, Color color)
        {
            var path = $"{AssetsFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void SetField(UnityEngine.Object target, string field, Action<SerializedProperty> assign)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field) ?? throw new InvalidOperationException($"{target.GetType().Name}.{field} not found");
            assign(property);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static int Layer(string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer < 0)
            {
                Debug.LogWarning($"[TestScene3DBuilder] Layer '{layerName}' not found, using Default");
                return 0;
            }
            return layer;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
