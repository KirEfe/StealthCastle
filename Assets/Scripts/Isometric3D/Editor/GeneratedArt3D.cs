using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using StealthCastle.Isometric3D.World;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace StealthCastle.Isometric3D.EditorTools
{
    /// <summary>
    /// Модели из Blender (Tools/Blender/castle_kit.py) → детали набора уровня.
    /// Blender запускается в фоне и пишет FBX в Assets/Art/Generated/Models; затем в префабах набора (Kit)
    /// меш-заглушка заменяется моделью, материалы Gen_* — ассетами проекта, коллайдер подгоняется под модель.
    /// Куски уровней и логово используют префабы набора, поэтому обновляются сами.
    /// </summary>
    public static class GeneratedArt3D
    {
        public const string ModelsFolder = "Assets/Art/Generated/Models";
        private const string MaterialsFolder = "Assets/Art/Generated/Materials";
        private const string KitFolder = "Assets/Prefabs/Isometric3D/Kit";
        public const string HubFolder = "Assets/Prefabs/Isometric3D/Hub";
        private const string TexturesFolder = "Assets/Art/Generated/Textures";
        private const string PieceMaterialsFolder = "Assets/Prefabs/Isometric3D/Materials";
        private const string BlenderScript = "Tools/Blender/castle_kit.py";
        private const string BlenderPathPref = "StealthCastle.BlenderPath";

        /// <summary>Цвета материалов (sRGB); существующие материалы не перекрашиваются — правки в проекте сохраняются</summary>
        private static readonly Dictionary<string, Color> Palette = new Dictionary<string, Color>
        {
            { "Gen_Wood", new Color(0.62f, 0.43f, 0.26f) },
            { "Gen_WoodDark", new Color(0.4f, 0.27f, 0.16f) },
            { "Gen_Metal", new Color(0.33f, 0.34f, 0.37f) },
            { "Gen_Gold", new Color(0.95f, 0.74f, 0.27f) },
            { "Gen_Stone", new Color(0.6f, 0.58f, 0.54f) },
            { "Gen_StoneDark", new Color(0.46f, 0.44f, 0.41f) },
            { "Gen_Mortar", new Color(0.27f, 0.26f, 0.24f) },
            { "Gen_Cloth", new Color(0.72f, 0.63f, 0.47f) },
            { "Gen_Rope", new Color(0.55f, 0.45f, 0.29f) },
            { "Gen_Blanket", new Color(0.4f, 0.47f, 0.36f) },
            { "Gen_Red", new Color(0.6f, 0.2f, 0.17f) },
            { "Gen_Parchment", new Color(0.9f, 0.83f, 0.64f) },
            { "Gen_Wax", new Color(0.94f, 0.9f, 0.78f) },
            { "Gen_Flame", new Color(1f, 0.62f, 0.22f) },
            { "Gen_GemBlue", new Color(0.3f, 0.6f, 1f) },
            { "Gen_GemPurple", new Color(0.7f, 0.35f, 0.95f) },
        };

        /// <summary>Серые текстуры из Blender, умножаются на цвет материала</summary>
        private static readonly Dictionary<string, string> MaterialTextures = new Dictionary<string, string>
        {
            { "Gen_Wood", "T_Wood" }, { "Gen_WoodDark", "T_Wood" },
            { "Gen_Stone", "T_Stone" }, { "Gen_StoneDark", "T_Stone" }, { "Gen_Mortar", "T_Stone" },
            { "Gen_Cloth", "T_Cloth" }, { "Gen_Rope", "T_Cloth" }, { "Gen_Blanket", "T_Cloth" }, { "Gen_Red", "T_Cloth" },
        };

        /// <summary>Пропы для маскировки из Blender: префаб, имя модели, подпись, форма коллайдера</summary>
        private static readonly (string prefab, string display, ColliderShape shape)[] NewProps =
        {
            ("Sack", "Мешок", ColliderShape.Capsule),
            ("Keg", "Бочонок", ColliderShape.Capsule),
            ("Table", "Стол", ColliderShape.Box),
        };

        /// <summary>Мебель логова: префаб = имя модели; коллайдер — у всего, что стоит на полу</summary>
        private static readonly (string name, bool solid)[] HubFurniture =
        {
            ("HubMapTable", true), ("HubWorkbench", true), ("HubCounter", true),
            ("HubTrophyShelf", true), ("HubBedroll", false), ("HubWallTorch", false),
        };

        private enum ColliderShape { None, Box, Capsule }

        [MenuItem("StealthCastle/3D/Art/Rebuild Models in Blender + Apply to Kit")]
        public static void RebuildAndApply()
        {
            if (ExportFromBlender()) Apply();
        }

        [MenuItem("StealthCastle/3D/Art/Apply Models to Kit")]
        public static void Apply()
        {
            PieceAuthoring3D.EnsureFolder(MaterialsFolder);
            PieceAuthoring3D.EnsureFolder(HubFolder);
            AssetDatabase.DeleteAsset($"{ModelsFolder}/WallSegment.fbx"); // Стены теперь — процедурная кладка WallBlock3D

            int count = 0;
            count += ApplyProp(KitFolder, "Crate", "Crate", ColliderShape.Box) ? 1 : 0;
            count += ApplyProp(KitFolder, "Barrel", "Barrel", ColliderShape.Capsule) ? 1 : 0;
            count += ApplyProp(KitFolder, "Pillar", "Pillar", ColliderShape.Box) ? 1 : 0;
            count += ApplyChest() ? 1 : 0;

            var kit = LevelKit3D.Find();
            int propLayer = PieceAuthoring3D.LayerOrDefault("Prop");
            foreach (var (prefab, display, shape) in NewProps)
            {
                EnsureMeshPrefab(KitFolder, prefab, propLayer, display);
                count += ApplyProp(KitFolder, prefab, prefab, shape) ? 1 : 0;
            }
            if (kit != null)
            {
                kit.sackPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{KitFolder}/Sack.prefab");
                kit.kegPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{KitFolder}/Keg.prefab");
                kit.tablePrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{KitFolder}/Table.prefab");
                EditorUtility.SetDirty(kit);
            }

            int wallLayer = PieceAuthoring3D.LayerOrDefault("Wall");
            foreach (var (name, solid) in HubFurniture)
            {
                EnsureMeshPrefab(HubFolder, name, solid ? wallLayer : 0, null);
                count += ApplyProp(HubFolder, name, name, solid ? ColliderShape.Box : ColliderShape.None) ? 1 : 0;
            }

            ApplyTextures();
            AssetDatabase.SaveAssets();
            Debug.Log($"[GeneratedArt3D] Prefabs updated: {count}");
        }

        [MenuItem("StealthCastle/3D/Art/Set Blender Path…")]
        public static void SetBlenderPath()
        {
            var path = EditorUtility.OpenFilePanel("blender.exe", Path.GetDirectoryName(FindBlender() ?? ""), "exe");
            if (!string.IsNullOrEmpty(path)) EditorPrefs.SetString(BlenderPathPref, path);
        }

        // --- Blender ------------------------------------------------------------------------

        /// <summary>Запуск castle_kit.py в фоновом Blender; FBX появляются в ModelsFolder</summary>
        public static bool ExportFromBlender()
        {
            var blender = FindBlender();
            if (blender == null)
            {
                EditorUtility.DisplayDialog("Blender не найден", "Укажи blender.exe: StealthCastle → 3D → Art → Set Blender Path…", "OK");
                return false;
            }

            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var outDir = Path.Combine(projectRoot, ModelsFolder);
            var start = new ProcessStartInfo(blender, $"--background --python \"{Path.Combine(projectRoot, BlenderScript)}\" -- \"{outDir}\"")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = projectRoot,
            };

            try
            {
                EditorUtility.DisplayProgressBar("Blender", "Building castle kit models…", 0.5f);
                using (var process = Process.Start(start))
                {
                    var output = process.StandardOutput.ReadToEnd();
                    var errors = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    foreach (var line in output.Split('\n'))
                    {
                        if (line.Contains("[castle_kit]")) Debug.Log(line.Trim());
                    }
                    if (process.ExitCode != 0 || output.Contains("Traceback") || errors.Contains("Traceback"))
                    {
                        Debug.LogError($"[GeneratedArt3D] Blender failed (exit {process.ExitCode}):\n{errors}\n{output}");
                        return false;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh();
            return true;
        }

        private static string FindBlender()
        {
            var custom = EditorPrefs.GetString(BlenderPathPref, "");
            if (File.Exists(custom)) return custom;

            // Самая новая установка в Program Files/Blender Foundation/Blender X.Y
            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Blender Foundation");
            if (!Directory.Exists(root)) return null;
            string best = null;
            Version bestVersion = null;
            foreach (var dir in Directory.GetDirectories(root))
            {
                var exe = Path.Combine(dir, "blender.exe");
                if (!File.Exists(exe)) continue;
                Version.TryParse(Path.GetFileName(dir).Replace("Blender ", ""), out var version);
                if (best == null || (version != null && (bestVersion == null || version > bestVersion)))
                {
                    best = exe;
                    bestVersion = version;
                }
            }
            return best;
        }

        // --- Префабы набора -----------------------------------------------------------------

        /// <summary>Модель из FBX: меш, материалы проекта и положение узла относительно корня модели</summary>
        private sealed class ModelPart
        {
            public Mesh Mesh;
            public Material[] Materials;
            public Vector3 LocalPosition;
        }

        private static Dictionary<string, ModelPart> LoadModel(string modelName)
        {
            var path = $"{ModelsFolder}/{modelName}.fbx";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null)
            {
                Debug.LogWarning($"[GeneratedArt3D] {path} not found — run Rebuild Models in Blender first");
                return null;
            }

            var parts = new Dictionary<string, ModelPart>();
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                var source = renderer != null ? renderer.sharedMaterials : new Material[0];
                var materials = new Material[source.Length];
                for (int i = 0; i < source.Length; i++)
                {
                    materials[i] = ProjectMaterial(source[i] != null ? source[i].name : "Gen_Wood");
                }
                parts[filter.sharedMesh.name] = new ModelPart
                {
                    Mesh = filter.sharedMesh,
                    Materials = materials,
                    LocalPosition = model.transform.InverseTransformPoint(filter.transform.position),
                };
            }
            return parts;
        }

        private static Material ProjectMaterial(string name)
        {
            var path = $"{MaterialsFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                color = Palette.TryGetValue(name, out var color) ? color : Color.gray,
            };
            material.SetFloat("_Smoothness", name == "Gen_Metal" || name == "Gen_Gold" || name.StartsWith("Gen_Gem") ? 0.45f : 0.12f);
            if (name == "Gen_Metal" || name == "Gen_Gold") material.SetFloat("_Metallic", 0.6f);
            if (name == "Gen_Flame")
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", material.color * 2.5f);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>
        /// Префаб «корень + дочерний Mesh», если его ещё нет. displayName — проп для маскировки (DisguisableProp3D)
        /// </summary>
        private static void EnsureMeshPrefab(string folder, string prefabName, int layer, string displayName)
        {
            var path = $"{folder}/{prefabName}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

            var root = new GameObject(prefabName);
            var mesh = new GameObject("Mesh", typeof(MeshFilter), typeof(MeshRenderer)) { layer = layer };
            mesh.transform.SetParent(root.transform, false);
            if (displayName != null)
            {
                mesh.AddComponent<BoxCollider>(); // Проп требует коллайдер; форму подгонит ApplyProp
                mesh.AddComponent<Mechanics.DisguisableProp3D>().SetDisplayName(displayName);
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
        }

        /// <summary>Текстуры: материалам моделей и стен, полам кусков (12 м → плита 1 м) и полу логова</summary>
        private static void ApplyTextures()
        {
            foreach (var pair in MaterialTextures)
            {
                SetBaseMap(AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/{pair.Key}.mat"), pair.Value, Vector2.one);
            }
            foreach (var guid in AssetDatabase.FindAssets("Floor_ t:Material", new[] { PieceMaterialsFolder }))
            {
                SetBaseMap(AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid)), "T_Floor", new Vector2(6f, 6f));
            }
            SetBaseMap(AssetDatabase.LoadAssetAtPath<Material>($"{PieceMaterialsFolder}/Hub_Floor.mat"), "T_Floor", new Vector2(8.5f, 6.5f));
        }

        private static void SetBaseMap(Material material, string textureName, Vector2 tiling)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesFolder}/{textureName}.png");
            if (material == null || texture == null) return;
            material.SetTexture("_BaseMap", texture);
            material.SetTextureScale("_BaseMap", tiling);
            EditorUtility.SetDirty(material);
        }

        /// <summary>Проп или глухая деталь: дочерний «Mesh» получает модель (пивот — центр основания)</summary>
        private static bool ApplyProp(string folder, string prefabName, string modelName, ColliderShape shape)
        {
            var parts = LoadModel(modelName);
            if (parts == null || !parts.TryGetValue(modelName, out var part)) return false;

            using (var editing = new PrefabUtility.EditPrefabContentsScope($"{folder}/{prefabName}.prefab"))
            {
                var meshObject = editing.prefabContentsRoot.transform.Find("Mesh");
                if (meshObject == null)
                {
                    Debug.LogWarning($"[GeneratedArt3D] {prefabName}: no child 'Mesh'");
                    return false;
                }
                AssignModel(meshObject, part);
                FitCollider(meshObject.gameObject, part.Mesh.bounds, shape);
            }
            return true;
        }

        /// <summary>Сундук: корпус в «Mesh», крышка — дочерний «Lid» с пивотом на петлях (её открывает LootChest3D)</summary>
        private static bool ApplyChest()
        {
            var parts = LoadModel("Chest");
            if (parts == null || !parts.TryGetValue("Chest_Body", out var body) || !parts.TryGetValue("Chest_Lid", out var lidPart)) return false;

            using (var editing = new PrefabUtility.EditPrefabContentsScope($"{KitFolder}/Chest.prefab"))
            {
                var root = editing.prefabContentsRoot.transform;
                var meshObject = root.Find("Mesh");
                AssignModel(meshObject, body);

                var lid = root.Find("Lid");
                if (lid == null)
                {
                    lid = new GameObject("Lid", typeof(MeshFilter), typeof(MeshRenderer)).transform;
                    lid.SetParent(root, false);
                }
                lid.gameObject.layer = meshObject.gameObject.layer;
                AssignModel(lid, lidPart);
                lid.localPosition = lidPart.LocalPosition - body.LocalPosition;

                // Коллайдер — по закрытому сундуку целиком
                var bounds = body.Mesh.bounds;
                var lidBounds = lidPart.Mesh.bounds;
                bounds.Encapsulate(new Bounds(lidBounds.center + lid.localPosition, lidBounds.size));
                FitCollider(meshObject.gameObject, bounds, ColliderShape.Box);

                // Крышка лежит от петель к передней стенке: открывается поворотом передним краем вверх
                var chest = root.GetComponent<LootChest3D>();
                if (chest != null)
                {
                    float angle = lidBounds.center.z < 0f ? 105f : -105f;
                    LevelPieces3DBuilder.SetField(chest, "lid", p => p.objectReferenceValue = lid);
                    LevelPieces3DBuilder.SetField(chest, "lidOpenAngle", p => p.floatValue = angle);
                }
            }
            return true;
        }

        private static void AssignModel(Transform target, ModelPart part)
        {
            target.localPosition = Vector3.zero;
            target.localRotation = Quaternion.identity;
            target.localScale = Vector3.one;
            target.GetComponent<MeshFilter>().sharedMesh = part.Mesh;
            target.GetComponent<MeshRenderer>().sharedMaterials = part.Materials;
        }

        private static void FitCollider(GameObject target, Bounds bounds, ColliderShape shape)
        {
            if (shape == ColliderShape.None) return;
            if (shape == ColliderShape.Capsule)
            {
                var capsule = target.GetComponent<CapsuleCollider>(); // ?? не годится: в редакторе отсутствующий компонент — «фальшивый null»
                if (capsule == null)
                {
                    capsule = target.AddComponent<CapsuleCollider>();
                    // Проп требует коллайдер: старый бокс убираем, когда капсула уже есть
                    var oldBox = target.GetComponent<BoxCollider>();
                    if (oldBox != null) UnityEngine.Object.DestroyImmediate(oldBox, true);
                }
                capsule.direction = 1;
                capsule.center = bounds.center;
                capsule.radius = Mathf.Max(bounds.extents.x, bounds.extents.z);
                capsule.height = bounds.size.y;
            }
            else
            {
                var box = target.GetComponent<BoxCollider>();
                if (box == null) box = target.AddComponent<BoxCollider>();
                box.center = bounds.center;
                box.size = bounds.size;
            }
        }
    }

    /// <summary>
    /// Импорт моделей из Blender: оси переводятся прямо в меш (Y вверх, без поворота узла), без анимаций и камер
    /// </summary>
    public class GeneratedModelPostprocessor : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(GeneratedArt3D.ModelsFolder)) return;
            var importer = (ModelImporter)assetImporter;
            importer.bakeAxisConversion = true;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
        }
    }
}
