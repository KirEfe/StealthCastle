using System.Collections.Generic;
using StealthCastle.Isometric3D.Enemies;
using StealthCastle.Isometric3D.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Dir = StealthCastle.Isometric3D.World.ConnectionPoint3D.Dir;

namespace StealthCastle.Isometric3D.EditorTools
{
    /// <summary>
    /// Общий код авторинга кусков: пол и стены периметра под проёмы, синхронизация PieceData3D,
    /// маршруты стражей, проверка куска. Используют инспектор куска, мастер нового куска и сборщик набора.
    /// </summary>
    public static class PieceAuthoring3D
    {
        public const string DataFolder = "Assets/Prefabs/Isometric3D/PieceData";
        public const string PiecesFolder = "Assets/Prefabs/Isometric3D/Pieces";

        public const float WallHeight = 2.6f;   // Выше роста (1.8 м); стены между камерой и игроком срезаются (WallCutaway3D)
        public const float WallThickness = 0.5f;
        public const float BodyHeight = 0.9f;   // Центр капсулы стража над полом

        private const float Half = LevelPiece3D.Size * 0.5f;
        private const float DoorHalf = LevelPiece3D.DoorWidth * 0.5f;

        // --- Пол и стены периметра ------------------------------------------------------------

        /// <summary>
        /// Пересоздаёт пол, стены периметра и заглушки проёмов «Blocker_{Dir}» под текущие проёмы куска.
        /// Внутреннее наполнение (стены внутри, пропы, стражи, маршруты) не трогается.
        /// </summary>
        public static void RebuildShell(LevelPiece3D piece, LevelKit3D kit, bool recordUndo)
        {
            var toRemove = new List<GameObject>();
            foreach (Transform child in piece.transform)
            {
                if (IsShellPart(child.name)) toRemove.Add(child.gameObject);
            }
            foreach (var go in toRemove)
            {
                if (recordUndo) Undo.DestroyObjectImmediate(go);
                else Object.DestroyImmediate(go);
            }

            int groundLayer = LayerOrDefault(kit.groundLayer);
            int wallLayer = LayerOrDefault(kit.wallLayer);

            var floor = Block(piece.transform, "Floor", new Vector3(0f, -0.1f, 0f), new Vector3(Half * 2f, 0.2f, Half * 2f),
                kit.FloorFor(piece.PieceTag), groundLayer, recordUndo);
            floor.transform.SetAsFirstSibling();

            float edge = Half - WallThickness * 0.5f;
            float sideSpan = Half - WallThickness; // Восток/запад короче на толщину севера/юга — углы не перекрываются
            BuildSide(piece, kit, Dir.North, -Half, Half, edge, true, wallLayer, recordUndo);
            BuildSide(piece, kit, Dir.South, -Half, Half, -edge, true, wallLayer, recordUndo);
            BuildSide(piece, kit, Dir.East, -sideSpan, sideSpan, edge, false, wallLayer, recordUndo);
            BuildSide(piece, kit, Dir.West, -sideSpan, sideSpan, -edge, false, wallLayer, recordUndo);
        }

        private static bool IsShellPart(string name) => WalkabilityCheck3D.IsShellPart(name);

        private static void BuildSide(LevelPiece3D piece, LevelKit3D kit, Dir dir, float from, float to, float offset, bool alongX,
            int wallLayer, bool recordUndo)
        {
            void Segment(string name, float a, float b)
            {
                float center = (a + b) * 0.5f;
                float length = b - a;
                var position = alongX ? new Vector3(center, 0f, offset) : new Vector3(offset, 0f, center);
                var size = alongX ? new Vector3(length, WallHeight, WallThickness) : new Vector3(WallThickness, WallHeight, length);
                WallBlocks3D.Create(piece.transform, name, position, size, WallBlock3D.Style.Stone, wallLayer, recordUndo);
            }

            if (!piece.HasDoor(dir))
            {
                Segment($"Wall_{dir}", from, to);
                return;
            }

            Segment($"Wall_{dir}_A", from, -DoorHalf);
            Segment($"Wall_{dir}_B", DoorHalf, to);
            // Заглушку генератор ищет по имени прямого потомка корня и выключает, если через проём стыкуется сосед
            Segment($"Blocker_{dir}", -DoorHalf, DoorHalf);
        }

        private static GameObject Block(Transform parent, string name, Vector3 position, Vector3 scale, Material material, int layer,
            bool recordUndo)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.layer = layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (recordUndo) Undo.RegisterCreatedObjectUndo(go, "Rebuild piece shell");
            return go;
        }

        // --- PieceData3D ------------------------------------------------------------------------

        /// <summary>
        /// Создаёт/обновляет PieceData3D куска: префаб, размер, тег, вес, проёмы. Генератор берёт куски отсюда
        /// </summary>
        public static PieceData3D SyncData(LevelPiece3D piece, string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[PieceAuthoring3D] Prefab not found: {prefabPath}");
                return null;
            }

            var data = piece.Data;
            if (data == null)
            {
                EnsureFolder(DataFolder);
                var dataPath = $"{DataFolder}/{prefab.name}.asset";
                data = AssetDatabase.LoadAssetAtPath<PieceData3D>(dataPath);
                if (data == null)
                {
                    data = ScriptableObject.CreateInstance<PieceData3D>();
                    AssetDatabase.CreateAsset(data, dataPath);
                }
            }

            var so = new SerializedObject(data);
            so.FindProperty("prefab").objectReferenceValue = prefab;
            so.FindProperty("sizeInTiles").vector2IntValue = new Vector2Int(6, 6); // × pieceSpacing генератора (2) = 12 м
            so.FindProperty("pieceTag").enumValueIndex = (int)piece.PieceTag;
            so.FindProperty("weight").floatValue = piece.Weight;
            var doors = piece.Doors;
            var connections = so.FindProperty("connections");
            connections.arraySize = doors.Count;
            for (int i = 0; i < doors.Count; i++)
            {
                var element = connections.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("localPosition").vector2Value = LevelPiece3D.DoorLocalPosition(doors[i]);
                element.FindPropertyRelative("direction").enumValueIndex = (int)doors[i];
                element.FindPropertyRelative("openingType").stringValue = "door";
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);

            if (piece.Data != data)
            {
                Undo.RecordObject(piece, "Link piece data");
                piece.Data = data;
                EditorUtility.SetDirty(piece);
            }
            AssetDatabase.SaveAssets();
            return data;
        }

        /// <summary>Путь к ассету префаба куска: открыт в Prefab Mode, выделен в Project или стоит экземпляром в сцене</summary>
        public static string PrefabPathOf(GameObject go)
        {
            var stage = PrefabStageUtility.GetPrefabStage(go);
            if (stage != null) return stage.assetPath;
            var assetPath = AssetDatabase.GetAssetPath(go);
            if (!string.IsNullOrEmpty(assetPath)) return assetPath;
            return PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
        }

        // --- Детали и маршруты ------------------------------------------------------------------

        /// <summary>Поставить деталь набора в кусок (экземпляр префаба, изменения префаба доходят до всех кусков)</summary>
        public static GameObject AddPart(LevelPiece3D piece, GameObject prefab, Vector3 localPosition, string undoName)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, piece.transform);
            go.transform.localPosition = localPosition;
            Undo.RegisterCreatedObjectUndo(go, undoName);
            return go;
        }

        /// <summary>Страж + его маршрут из точек; точки в координатах куска (x, z), пол на y = 0</summary>
        public static EnemyAI3D AddGuard(LevelPiece3D piece, LevelKit3D kit, PatrolRoute3D.RouteMode mode, params Vector2[] points)
        {
            var start = points.Length > 0 ? points[0] : Vector2.zero;
            var guardGo = AddPart(piece, kit.guardPrefab, new Vector3(start.x, BodyHeight, start.y), "Add guard");
            var guard = guardGo.GetComponent<EnemyAI3D>();
            var route = CreateRoute(guard, mode, points);
            guardGo.name = "Guard_" + route.name.Replace("Route_", "");
            return guard;
        }

        /// <summary>Создаёт маршрут рядом со стражем (в том же родителе) и назначает его стражу</summary>
        public static PatrolRoute3D CreateRoute(EnemyAI3D guard, PatrolRoute3D.RouteMode mode, params Vector2[] points)
        {
            var parent = guard.transform.parent;
            int index = 0;
            while (parent != null && parent.Find($"Route_{index}") != null) index++;

            var routeGo = new GameObject($"Route_{index}");
            Undo.RegisterCreatedObjectUndo(routeGo, "Create patrol route");
            routeGo.transform.SetParent(parent, false);
            var route = routeGo.AddComponent<PatrolRoute3D>();
            var routeSo = new SerializedObject(route);
            routeSo.FindProperty("mode").enumValueIndex = (int)mode;
            routeSo.ApplyModifiedPropertiesWithoutUndo();

            if (points == null || points.Length == 0)
            {
                // По умолчанию — две точки: где стоит страж и на 3 м вперёд
                var local = parent != null ? parent.InverseTransformPoint(guard.transform.position) : guard.transform.position;
                var forward = parent != null ? parent.InverseTransformDirection(guard.transform.forward) : guard.transform.forward;
                points = new[] { new Vector2(local.x, local.z), new Vector2(local.x + forward.x * 3f, local.z + forward.z * 3f) };
            }
            foreach (var point in points)
            {
                AddPoint(route, new Vector3(point.x, 0f, point.y));
            }

            var guardSo = new SerializedObject(guard);
            guardSo.FindProperty("route").objectReferenceValue = route;
            guardSo.ApplyModifiedProperties();
            return route;
        }

        /// <summary>Добавляет точку в конец маршрута (локальная позиция внутри маршрута)</summary>
        public static Transform AddPoint(PatrolRoute3D route, Vector3 localPosition)
        {
            var point = new GameObject($"Point_{route.transform.childCount}").transform;
            Undo.RegisterCreatedObjectUndo(point.gameObject, "Add patrol point");
            point.SetParent(route.transform, false);
            point.localPosition = localPosition;
            return point;
        }

        // --- Проверка ---------------------------------------------------------------------------

        /// <summary>Проблемы куска, из-за которых он соберётся неправильно или будет неиграбельным</summary>
        public static List<string> Validate(LevelPiece3D piece)
        {
            var issues = new List<string>();
            var doors = piece.Doors;
            if (doors.Count == 0) issues.Add("Нет ни одного проёма — кусок не к чему пристыковать.");

            foreach (var dir in doors)
            {
                if (piece.transform.Find("Blocker_" + dir) == null)
                    issues.Add($"Нет заглушки Blocker_{dir}: нажми «Перестроить пол и стены».");
            }

            if (piece.PieceTag == PieceData3D.Tag.Start && piece.GetComponentInChildren<SpawnPoint3D>(true) == null)
                issues.Add("Стартовый кусок без точки спавна игрока.");
            if (piece.PieceTag == PieceData3D.Tag.Exit && piece.GetComponentInChildren<LevelExit3D>(true) == null)
                issues.Add("Кусок-выход без LevelExit.");
            if (piece.PieceTag == PieceData3D.Tag.Start && piece.GetComponentInChildren<EnemyAI3D>(true) != null)
                issues.Add("В стартовом куске стоит страж — старт должен быть безопасным.");

            foreach (var guard in piece.GetComponentsInChildren<EnemyAI3D>(true))
            {
                var route = new SerializedObject(guard).FindProperty("route").objectReferenceValue as PatrolRoute3D;
                if (route == null) issues.Add($"{guard.name}: нет маршрута патруля.");
                else if (route.Count == 0) issues.Add($"{guard.name}: в маршруте нет точек.");

                float height = piece.transform.InverseTransformPoint(guard.transform.position).y;
                if (Mathf.Abs(height - BodyHeight) > 0.05f)
                    issues.Add($"{guard.name}: высота {height:0.00} м, нужна {BodyHeight} (кнопка «Поставить на пол» у стража).");
            }

            var data = piece.Data;
            if (data == null) issues.Add("Нет PieceData — генератор не увидит кусок (кнопка «Обновить PieceData»).");
            else if (data.PieceTag != piece.PieceTag || (data.Connections?.Length ?? 0) != doors.Count)
                issues.Add("PieceData устарел (тег или проёмы) — нажми «Обновить PieceData».");

            return issues;
        }

        // --- Утилиты ----------------------------------------------------------------------------

        public static int LayerOrDefault(string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            return layer >= 0 ? layer : 0;
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
