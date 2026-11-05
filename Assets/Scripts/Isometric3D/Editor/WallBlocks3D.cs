using System.Collections.Generic;
using StealthCastle.Isometric3D.World;
using UnityEditor;
using UnityEngine;

namespace StealthCastle.Isometric3D.EditorTools
{
    /// <summary>
    /// Стены WallBlock3D в редакторе: создание, сборка визуалов (Visual_Base / Visual_Upper) и библиотека мешей кладки.
    /// Меши лежат одним ассетом (под-ассеты по ключу «стиль_размер»): одинаковые стены делят меш.
    /// </summary>
    public static class WallBlocks3D
    {
        private const string LibraryPath = "Assets/Art/Generated/WallMeshes.asset";
        private const string MaterialsFolder = "Assets/Art/Generated/Materials";

        /// <summary>Материалы стиля: камень — [раствор, камень, тёмный камень], дерево — [основа, доска]</summary>
        public static Material[] MaterialsFor(WallBlock3D.Style style)
        {
            var names = style == WallBlock3D.Style.Stone
                ? new[] { "Gen_Mortar", "Gen_Stone", "Gen_StoneDark" }
                : new[] { "Gen_WoodDark", "Gen_Wood" };
            var result = new Material[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                result[i] = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/{names[i]}.mat");
            }
            return result;
        }

        /// <summary>Новая стена: пивот на полу в (x, z) родителя</summary>
        public static WallBlock3D Create(Transform parent, string name, Vector3 floorPosition, Vector3 size,
            WallBlock3D.Style style, int layer, bool recordUndo)
        {
            var go = new GameObject(name) { layer = layer };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = floorPosition;
            var wall = go.AddComponent<WallBlock3D>();
            wall.Configure(size, style, MaterialsFor(style));
            Build(wall);
            if (recordUndo) Undo.RegisterCreatedObjectUndo(go, "Create wall");
            return wall;
        }

        /// <summary>Пересобрать коллайдер и визуалы стены под её размер и стиль</summary>
        public static void Build(WallBlock3D wall)
        {
            var size = wall.Size;
            var box = wall.GetComponent<BoxCollider>();
            box.center = new Vector3(0f, size.y * 0.5f, 0f);
            box.size = size;

            var meshes = LoadOrCreateMeshes(size, wall.WallStyle);
            var baseVisual = Visual(wall, "Visual_Base", meshes[0], 0f);
            Transform upperVisual = null;
            if (meshes[1] != null)
            {
                upperVisual = Visual(wall, "Visual_Upper", meshes[1], WallBlock3D.SplitHeight(size.y));
            }
            else
            {
                var stale = wall.transform.Find("Visual_Upper");
                if (stale != null) Object.DestroyImmediate(stale.gameObject);
            }
            wall.SetVisuals(baseVisual, upperVisual);
            EditorUtility.SetDirty(wall);
        }

        private static Transform Visual(WallBlock3D wall, string name, Mesh mesh, float height)
        {
            var visual = wall.transform.Find(name);
            if (visual == null)
            {
                visual = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)).transform;
                visual.SetParent(wall.transform, false);
            }
            visual.gameObject.layer = wall.gameObject.layer;
            visual.localPosition = new Vector3(0f, height, 0f);
            visual.localRotation = Quaternion.identity;
            visual.localScale = Vector3.one;
            visual.GetComponent<MeshFilter>().sharedMesh = mesh;
            var materials = new Material[mesh.subMeshCount];
            for (int i = 0; i < materials.Length; i++)
            {
                var source = wall.Materials;
                materials[i] = source != null && source.Length > 0 ? source[Mathf.Min(i, source.Length - 1)] : null;
            }
            visual.GetComponent<MeshRenderer>().sharedMaterials = materials;
            return visual;
        }

        private static Mesh[] LoadOrCreateMeshes(Vector3 size, WallBlock3D.Style style)
        {
            string key = WallBlock3D.MeshKey(size, style);
            var library = AssetDatabase.LoadAssetAtPath<WallMeshLibrary3D>(LibraryPath);
            if (library == null)
            {
                PieceAuthoring3D.EnsureFolder(System.IO.Path.GetDirectoryName(LibraryPath).Replace('\\', '/'));
                library = ScriptableObject.CreateInstance<WallMeshLibrary3D>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            Mesh baseMesh = null, upperMesh = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(LibraryPath))
            {
                if (asset is Mesh mesh)
                {
                    if (mesh.name == key + "_base") baseMesh = mesh;
                    else if (mesh.name == key + "_upper") upperMesh = mesh;
                }
            }
            bool needsUpper = WallBlock3D.SplitHeight(size.y) > 0f;
            if (baseMesh != null && (upperMesh != null || !needsUpper)) return new[] { baseMesh, upperMesh };

            var generated = WallBlock3D.GenerateMeshes(size, style);
            foreach (var mesh in generated)
            {
                if (mesh != null) AssetDatabase.AddObjectToAsset(mesh, library);
            }
            EditorUtility.SetDirty(library);
            return generated;
        }

        /// <summary>Перегенерировать все меши кладки (после правки генератора) и пересобрать стены в префабах</summary>
        [MenuItem("StealthCastle/3D/Art/Regenerate Wall Meshes")]
        public static void RegenerateAll()
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(LibraryPath))
            {
                if (asset is Mesh) Object.DestroyImmediate(asset, true);
            }

            var prefabPaths = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Isometric3D" }))
            {
                prefabPaths.Add(AssetDatabase.GUIDToAssetPath(guid));
            }
            int walls = 0;
            foreach (var path in prefabPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab.GetComponentInChildren<WallBlock3D>(true) == null) continue;
                using (var editing = new PrefabUtility.EditPrefabContentsScope(path))
                {
                    foreach (var wall in editing.prefabContentsRoot.GetComponentsInChildren<WallBlock3D>(true))
                    {
                        if (PrefabUtility.IsPartOfPrefabInstance(wall)) continue; // Вложенные префабы пересобираются у себя
                        Build(wall);
                        walls++;
                    }
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[WallBlocks3D] Rebuilt {walls} walls in prefabs. Scenes with walls (hub) — rebuild separately.");
        }
    }
}
