using System;
using System.Collections.Generic;
using StealthCastle.Isometric3D.Hub;
using StealthCastle.Isometric3D.Meta;
using StealthCastle.Isometric3D.World;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using Kind = StealthCastle.Isometric3D.Hub.HubStation3D.Kind;

namespace StealthCastle.Isometric3D.EditorTools
{
    /// <summary>
    /// Логово воров (хаб) и настройки мета-прогресса.
    /// Сцена Isometric3D_Hub: комната 16×12 из примитивов и деталей набора, четыре станции, игрок без маскировки.
    /// MetaConfig3D (Resources) и артефакты создаются, только если их ещё нет — правки в проекте не затираются.
    /// </summary>
    public static class HubScene3DBuilder
    {
        public const string SceneName = "Isometric3D_Hub";
        private const string ScenePath = "Assets/Scenes/" + SceneName + ".unity";
        private const string Root = "Assets/Prefabs/Isometric3D";
        private const string ResourcesFolder = Root + "/Resources";
        private const string ArtifactsFolder = Root + "/Artifacts";
        private const string ConfigPath = ResourcesFolder + "/" + MetaConfig3D.ResourcePath + ".asset";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";

        private const float WallHeight = PieceAuthoring3D.WallHeight;
        private const float HalfX = 8f;   // Комната 16 × 12 м
        private const float HalfZ = 6f;

        [MenuItem("StealthCastle/3D/Build Hub Scene")]
        public static void BuildFromMenu()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null &&
                !EditorUtility.DisplayDialog("Пересобрать логово?", $"Сцена {ScenePath} будет перезаписана.\n" +
                    "MetaConfig3D и артефакты не трогаются.", "Пересобрать", "Отмена"))
            {
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        public static void Build()
        {
            EnsureMetaConfig();
            var kit = LevelKit3D.Find();
            if (kit != null) LevelPieces3DBuilder.EnsureChestLoot(kit.chestPrefab);
            BuildScene(kit);
            AssetDatabase.SaveAssets();
            Debug.Log($"[HubScene3DBuilder] {ScenePath} + {ConfigPath}");
        }

        // --- Мета-прогресс ------------------------------------------------------------------

        [MenuItem("StealthCastle/3D/Meta/Reset Progress")]
        public static void ResetProgress()
        {
            MetaProgress3D.ResetProgress();
            Debug.Log($"[Meta] Progress reset ({MetaProgress3D.FilePath})");
        }

        [MenuItem("StealthCastle/3D/Meta/Add 200 Loot")]
        public static void AddLoot()
        {
            MetaProgress3D.Current.loot += 200;
            MetaProgress3D.Save();
            Debug.Log($"[Meta] Stash: {MetaProgress3D.Current.loot}");
        }

        [MenuItem("StealthCastle/3D/Meta/Select Config")]
        public static void SelectConfig()
        {
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<MetaConfig3D>(ConfigPath);
        }

        private static void EnsureMetaConfig()
        {
            PieceAuthoring3D.EnsureFolder(ResourcesFolder);
            PieceAuthoring3D.EnsureFolder(ArtifactsFolder);

            var artifacts = new List<Core.ArtifactData3D>
            {
                Artifact("A01_BellowsCharm", "Bellows Charm", "A little leather bellows on a string. You can hold your breath much longer.",
                    0, so => so.FindProperty("disguiseDurationBonus").floatValue = 3f),
                Artifact("A02_OwlLens", "Owl Lens", "See every hiding spot around you: disguise as props further away.",
                    0, so => so.FindProperty("scanRadiusBonus").floatValue = 1.5f),
                Artifact("A03_SoftBoots", "Soft Boots", "Felt soles. Walk faster without making a sound.",
                    40, so => so.FindProperty("movementSpeedBonus").floatValue = 1f, Core.ArtifactData3D.ArtifactRarity.Rare),
                Artifact("A04_GreedyCompass", "Greedy Compass", "The needle always points to gold: treasure rooms show up far more often.",
                    60, so =>
                    {
                        so.FindProperty("affectedTag").enumValueIndex = (int)PieceData3D.Tag.Treasure;
                        so.FindProperty("weightMultiplier").floatValue = 3f;
                    }, Core.ArtifactData3D.ArtifactRarity.Rare),
                Artifact("A05_QuietMap", "Quiet Map", "Marks the guard posts. Heavily guarded halls show up less often.",
                    50, so =>
                    {
                        so.FindProperty("affectedTag").enumValueIndex = (int)PieceData3D.Tag.Combat;
                        so.FindProperty("weightMultiplier").floatValue = 0.4f;
                    }, Core.ArtifactData3D.ArtifactRarity.Rare),
                Artifact("A06_ShadowCloak", "Shadow Cloak", "Woven from dusk. Guards notice you from a shorter distance.",
                    100, so => so.FindProperty("guardVisionBonus").floatValue = -0.2f, Core.ArtifactData3D.ArtifactRarity.Epic),
            };

            if (AssetDatabase.LoadAssetAtPath<MetaConfig3D>(ConfigPath) != null) return;

            var config = ScriptableObject.CreateInstance<MetaConfig3D>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            var configSo = new SerializedObject(config);

            var artifactList = configSo.FindProperty("artifacts");
            artifactList.arraySize = artifacts.Count;
            for (int i = 0; i < artifacts.Count; i++)
            {
                artifactList.GetArrayElementAtIndex(i).objectReferenceValue = artifacts[i];
            }

            var upgrades = new[]
            {
                (id: "breath", name: "Deep Lungs", desc: "+1 s of breath in disguise per level.", stat: MetaConfig3D.UpgradeStat.Breath, per: 1f, costs: new[] { 30, 60, 100 }),
                (id: "scan", name: "Keen Eye", desc: "+0.5 m scan radius per level.", stat: MetaConfig3D.UpgradeStat.ScanRadius, per: 0.5f, costs: new[] { 25, 50, 90 }),
                (id: "speed", name: "Light Feet", desc: "+0.4 m/s walking speed per level.", stat: MetaConfig3D.UpgradeStat.WalkSpeed, per: 0.4f, costs: new[] { 40, 80 }),
                (id: "pockets", name: "Deep Pockets", desc: "Keep 15% more loot per level when caught.", stat: MetaConfig3D.UpgradeStat.Pockets, per: 0.15f, costs: new[] { 35, 70, 120 }),
            };
            var upgradeList = configSo.FindProperty("upgrades");
            upgradeList.arraySize = upgrades.Length;
            for (int i = 0; i < upgrades.Length; i++)
            {
                var element = upgradeList.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("id").stringValue = upgrades[i].id;
                element.FindPropertyRelative("displayName").stringValue = upgrades[i].name;
                element.FindPropertyRelative("description").stringValue = upgrades[i].desc;
                element.FindPropertyRelative("stat").enumValueIndex = (int)upgrades[i].stat;
                element.FindPropertyRelative("perLevel").floatValue = upgrades[i].per;
                var costs = element.FindPropertyRelative("costs");
                costs.arraySize = upgrades[i].costs.Length;
                for (int c = 0; c < upgrades[i].costs.Length; c++)
                {
                    costs.GetArrayElementAtIndex(c).intValue = upgrades[i].costs[c];
                }
            }
            configSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Core.ArtifactData3D Artifact(string id, string displayName, string description, int cost,
            Action<SerializedObject> effects, Core.ArtifactData3D.ArtifactRarity rarity = Core.ArtifactData3D.ArtifactRarity.Common)
        {
            var path = $"{ArtifactsFolder}/{id}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Core.ArtifactData3D>(path);
            if (existing != null) return existing;

            var artifact = ScriptableObject.CreateInstance<Core.ArtifactData3D>();
            AssetDatabase.CreateAsset(artifact, path);
            var so = new SerializedObject(artifact);
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("description").stringValue = description;
            so.FindProperty("unlockCost").intValue = cost;
            so.FindProperty("rarity").enumValueIndex = (int)rarity;
            effects(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            return artifact;
        }

        // --- Сцена ----------------------------------------------------------------------

        private static void BuildScene(LevelKit3D kit)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var assets = new LevelPieces3DBuilder.BuildAssets();
            var mats = new HubMaterials();

            // Тёплый свет подвала: слабое солнце сквозь решётки + факелы
            var lightGo = new GameObject("Directional Light");
            lightGo.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
            var sun = lightGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.88f, 0.75f);
            sun.intensity = 0.7f;
            sun.shadows = LightShadows.Soft;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.3f, 0.26f, 0.24f);

            // Менеджер переживает переход в замок и обратно; в логове он только отдаёт управление
            var gameManager = new GameObject("GameManager3D").AddComponent<Core.GameManager3D>();
            LevelPieces3DBuilder.SetField(gameManager, "gameScene", p => p.stringValue = "Isometric3D_Game");
            LevelPieces3DBuilder.SetField(gameManager, "hubScene", p => p.stringValue = SceneName);

            var inputSystem = new GameObject("InputSystem3D").AddComponent<Core.InputSystem3D>();
            LevelPieces3DBuilder.SetField(inputSystem, "inputActions",
                p => p.objectReferenceValue = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath));

            var room = new GameObject("Hideout").transform;
            BuildRoom(room, assets, mats);
            BuildStations(room, assets, mats);
            BuildDecor(room, kit, assets, mats);

            var player = LevelPieces3DBuilder.CreatePlayer(assets, stealthKit: false);
            player.transform.position = new Vector3(0f, PieceAuthoring3D.BodyHeight, -2.5f);
            LevelPieces3DBuilder.CreateCamera(player.transform, zoom: 17f);

            var hub = new GameObject("Hub");
            hub.AddComponent<HubUI3D>();
            hub.AddComponent<HubController3D>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneFirstInBuildSettings(ScenePath);
        }

        private static void BuildRoom(Transform room, LevelPieces3DBuilder.BuildAssets assets, HubMaterials mats)
        {
            Block(room, "Floor", new Vector3(0f, -0.25f, 0f), new Vector3(HalfX * 2f + 1f, 0.5f, HalfZ * 2f + 1f), mats.Floor, assets.GroundLayer);

            // Стены — та же кладка, что в замке: высокие, срезаются, когда закрывают игрока
            float t = 0.5f;
            Wall(room, "Wall_North", new Vector3(0f, 0f, HalfZ + t * 0.5f), new Vector3(HalfX * 2f + t * 2f, WallHeight, t), assets);
            Wall(room, "Wall_South", new Vector3(0f, 0f, -HalfZ - t * 0.5f), new Vector3(HalfX * 2f + t * 2f, WallHeight, t), assets);
            Wall(room, "Wall_West", new Vector3(-HalfX - t * 0.5f, 0f, 0f), new Vector3(t, WallHeight, HalfZ * 2f), assets);
            Wall(room, "Wall_East", new Vector3(HalfX + t * 0.5f, 0f, 0f), new Vector3(t, WallHeight, HalfZ * 2f), assets);

            // Ход в замок за столом с картой: решётка и зелёный отсвет на полу (как у выхода уровня)
            Block(room, "CastleGate", new Vector3(0f, 1.3f, HalfZ - 0.05f), new Vector3(3f, 2.6f, 0.7f), mats.Iron, assets.WallLayer);
            Decor(room, "GateGlow", PrimitiveType.Cylinder, new Vector3(0f, 0.02f, HalfZ - 1.1f), new Vector3(2.6f, 0.02f, 1.2f), assets.ExitMat);

            Decor(room, "Rug", PrimitiveType.Cube, new Vector3(0f, 0.01f, 0.6f), new Vector3(6f, 0.02f, 4.6f), mats.Rug);

            // Факелы
            // Факел смотрит от стены в комнату (модель торчит в -Z)
            Torch(room, new Vector3(-HalfX, 1.8f, -1.5f), -90f, mats);
            Torch(room, new Vector3(HalfX, 1.8f, -1.5f), 90f, mats);
            Torch(room, new Vector3(-2.2f, 1.8f, HalfZ), 0f, mats);
            Torch(room, new Vector3(2.2f, 1.8f, HalfZ), 0f, mats);
        }

        private static void BuildStations(Transform room, LevelPieces3DBuilder.BuildAssets assets, HubMaterials mats)
        {
            // Стол с картой замка — вход в забег
            var map = Station(room, "Station_MapTable", new Vector3(0f, 0f, 2.6f), Kind.StartRun, "Map Table", "Plan the heist");
            if (!Furniture(map, "HubMapTable", Vector3.zero, 0f))
            {
                Block(map, "Table", new Vector3(0f, 0.45f, 0f), new Vector3(2.6f, 0.9f, 1.5f), mats.Wood, assets.WallLayer);
                Decor(map, "Map", PrimitiveType.Cube, new Vector3(0f, 0.91f, 0f), new Vector3(2.2f, 0.02f, 1.1f), mats.Parchment);
            }

            // Верстак — артефакты
            var bench = Station(room, "Station_Workbench", new Vector3(-HalfX + 1.4f, 0f, 0.5f), Kind.Artifacts, "Workbench", "Pick an artifact");
            if (!Furniture(bench, "HubWorkbench", Vector3.zero, 0f))
            {
                Block(bench, "Bench", new Vector3(0f, 0.45f, 0f), new Vector3(1.4f, 0.9f, 3.2f), mats.Workbench, assets.WallLayer);
            }

            // Скупщик — улучшения за добычу
            var fence = Station(room, "Station_Fence", new Vector3(HalfX - 1.4f, 0f, 0.5f), Kind.Upgrades, "The Fence", "Trade loot for upgrades");
            if (!Furniture(fence, "HubCounter", Vector3.zero, 0f))
            {
                Block(fence, "Counter", new Vector3(0f, 0.5f, 0f), new Vector3(1.4f, 1f, 3.2f), mats.Counter, assets.WallLayer);
            }

            // Полка трофеев — статистика
            var shelf = Station(room, "Station_Trophies", new Vector3(-4.5f, 0f, -HalfZ + 0.5f), Kind.Records, "Trophy Shelf", "Look at your trophies");
            if (!Furniture(shelf, "HubTrophyShelf", Vector3.zero, 0f))
            {
                Block(shelf, "Shelf", new Vector3(0f, 0.75f, 0f), new Vector3(3.4f, 1.5f, 0.6f), mats.Wood, assets.WallLayer);
            }
        }

        private static void BuildDecor(Transform room, LevelKit3D kit, LevelPieces3DBuilder.BuildAssets assets, HubMaterials mats)
        {
            // Лежанка и припасы — логово обжито
            if (!Furniture(room, "HubBedroll", new Vector3(4.2f, 0f, -4.4f), 0f))
            {
                Decor(room, "Bedroll", PrimitiveType.Cube, new Vector3(4.2f, 0.1f, -4.6f), new Vector3(1.3f, 0.2f, 2.2f), mats.Bedroll);
            }
            if (kit == null) return;
            KitPart(room, kit.sackPrefab, new Vector3(-6.9f, 0f, -4.9f));
            KitPart(room, kit.sackPrefab, new Vector3(-6.2f, 0f, -5.3f));
            KitPart(room, kit.kegPrefab, new Vector3(5.4f, 0f, 5.2f));
            KitPart(room, kit.cratePrefab, new Vector3(6.7f, 0f, -4.7f));
            KitPart(room, kit.cratePrefab, new Vector3(6.9f, 0f, -3.3f));
            KitPart(room, kit.barrelPrefab, new Vector3(5.8f, 0f, -5.2f));
            KitPart(room, kit.barrelPrefab, new Vector3(-7f, 0f, 4.9f));
            KitPart(room, kit.cratePrefab, new Vector3(-5.8f, 0f, 5f));
            KitPart(room, kit.cratePrefab, new Vector3(6.8f, 0f, 4.8f));
        }

        // --- Детали -----------------------------------------------------------------------

        private static Transform Station(Transform parent, string name, Vector3 position, Kind kind, string title, string prompt)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.AddComponent<HubStation3D>().Configure(kind, title, prompt);

            // Подпись над станцией, всегда лицом к камере
            var label = new GameObject("Label");
            label.transform.SetParent(go.transform, false);
            label.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            var text = label.AddComponent<TextMeshPro>();
            text.text = title;
            text.fontSize = 5f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(1f, 0.85f, 0.45f);
            text.rectTransform.sizeDelta = new Vector2(6f, 1.2f);
            label.AddComponent<Utils.FaceCamera3D>();
            return go.transform;
        }

        private static void Block(Transform parent, string name, Vector3 localCenter, Vector3 size, Material material, int layer)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.layer = layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCenter;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
        }

        /// <summary>Украшение без коллайдера</summary>
        private static void Decor(Transform parent, string name, PrimitiveType type, Vector3 localPosition, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
        }

        /// <param name="yaw">Поворот факела: модель торчит от стены в -Z</param>
        private static void Torch(Transform parent, Vector3 wallPoint, float yaw, HubMaterials mats)
        {
            var rotation = Quaternion.Euler(0f, yaw, 0f);
            if (!Furniture(parent, "HubWallTorch", wallPoint, yaw))
            {
                Decor(parent, "Flame", PrimitiveType.Sphere, wallPoint + rotation * new Vector3(0f, 0.3f, -0.25f), Vector3.one * 0.2f, mats.Flame);
            }

            var lightGo = new GameObject("TorchLight");
            lightGo.transform.SetParent(parent, false);
            lightGo.transform.localPosition = wallPoint + rotation * new Vector3(0f, 0.45f, -0.5f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.62f, 0.3f);
            light.range = 8f;
            light.intensity = 2.5f;
        }

        /// <summary>Мебель логова из Blender (префаб в Hub); false — моделей ещё нет, ставим примитивы</summary>
        private static bool Furniture(Transform parent, string prefabName, Vector3 localPosition, float yaw)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{GeneratedArt3D.HubFolder}/{prefabName}.prefab");
            if (prefab == null) return false;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return true;
        }

        private static void Wall(Transform parent, string name, Vector3 floorPosition, Vector3 size, LevelPieces3DBuilder.BuildAssets assets)
        {
            WallBlocks3D.Create(parent, name, floorPosition, size, WallBlock3D.Style.Stone, assets.WallLayer, false);
        }

        private static void KitPart(Transform parent, GameObject prefab, Vector3 position)
        {
            if (prefab == null) return;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.localPosition = position;
        }

        private sealed class HubMaterials
        {
            public readonly Material Floor = Lit("Hub_Floor", new Color(0.33f, 0.26f, 0.2f));
            public readonly Material Rug = Lit("Hub_Rug", new Color(0.5f, 0.16f, 0.14f));
            public readonly Material Wood = Lit("Hub_Wood", new Color(0.4f, 0.27f, 0.16f));
            public readonly Material Parchment = Lit("Hub_Parchment", new Color(0.86f, 0.78f, 0.58f));
            public readonly Material Workbench = Lit("Hub_Workbench", new Color(0.36f, 0.26f, 0.42f));
            public readonly Material Counter = Lit("Hub_Counter", new Color(0.2f, 0.32f, 0.24f));
            public readonly Material Iron = Lit("Hub_Iron", new Color(0.16f, 0.16f, 0.18f));
            public readonly Material GemBlue = Lit("Hub_GemBlue", new Color(0.25f, 0.55f, 1f));
            public readonly Material GemPurple = Lit("Hub_GemPurple", new Color(0.65f, 0.3f, 0.95f));
            public readonly Material Bedroll = Lit("Hub_Bedroll", new Color(0.35f, 0.38f, 0.3f));
            public readonly Material Flame = Emissive("Hub_Flame", new Color(1f, 0.6f, 0.2f));

            private static Material Lit(string name, Color color) => LevelPieces3DBuilder.BuildAssets.Lit(name, color);

            private static Material Emissive(string name, Color color)
            {
                var material = Lit(name, color);
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 2f);
                EditorUtility.SetDirty(material);
                return material;
            }
        }

        /// <summary>Логово — первая сцена сборки: игра начинается в нём</summary>
        private static void AddSceneFirstInBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == path);
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
