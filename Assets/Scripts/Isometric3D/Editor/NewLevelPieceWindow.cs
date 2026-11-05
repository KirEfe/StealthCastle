using System.Collections.Generic;
using StealthCastle.Isometric3D.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Dir = StealthCastle.Isometric3D.World.ConnectionPoint3D.Dir;

namespace StealthCastle.Isometric3D.EditorTools
{
    /// <summary>
    /// Мастер нового куска: заготовка 12×12 с полом, стенами и проёмами, данные для генератора.
    /// Дальше кусок собирается руками в Prefab Mode из деталей набора (инспектор LevelPiece3D).
    /// </summary>
    public class NewLevelPieceWindow : EditorWindow
    {
        private string pieceName = "P20_NewPiece";
        private PieceData3D.Tag pieceTag = PieceData3D.Tag.Empty;
        private bool north = true, east, south = true, west;
        private float weight = 1f;

        [MenuItem("StealthCastle/3D/New Level Piece…")]
        public static void Open()
        {
            var window = GetWindow<NewLevelPieceWindow>(true, "New Level Piece");
            window.minSize = new Vector2(320f, 210f);
        }

        private void OnGUI()
        {
            pieceName = EditorGUILayout.TextField("Имя префаба", pieceName);
            pieceTag = (PieceData3D.Tag)EditorGUILayout.EnumPopup("Тип", pieceTag);
            weight = EditorGUILayout.FloatField("Вес при выборе", weight);

            EditorGUILayout.LabelField("Проёмы");
            using (new EditorGUILayout.HorizontalScope())
            {
                north = GUILayout.Toggle(north, "Север", "Button");
                east = GUILayout.Toggle(east, "Восток", "Button");
                south = GUILayout.Toggle(south, "Юг", "Button");
                west = GUILayout.Toggle(west, "Запад", "Button");
            }

            var path = $"{PieceAuthoring3D.PiecesFolder}/{pieceName}.prefab";
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
            bool anyDoor = north || east || south || west;
            if (exists) EditorGUILayout.HelpBox("Кусок с таким именем уже есть.", MessageType.Warning);
            if (!anyDoor) EditorGUILayout.HelpBox("Нужен хотя бы один проём.", MessageType.Warning);

            using (new EditorGUI.DisabledScope(exists || !anyDoor || string.IsNullOrWhiteSpace(pieceName)))
            {
                if (GUILayout.Button("Создать кусок", GUILayout.Height(28f)))
                {
                    Create(path);
                    Close();
                }
            }
        }

        private void Create(string path)
        {
            var kit = LevelKit3D.Find();
            if (kit == null)
            {
                EditorUtility.DisplayDialog("Нет набора деталей", "Сначала запусти StealthCastle → 3D → Rebuild Starter Pieces.", "OK");
                return;
            }

            var doors = new List<Dir>();
            if (north) doors.Add(Dir.North);
            if (east) doors.Add(Dir.East);
            if (south) doors.Add(Dir.South);
            if (west) doors.Add(Dir.West);

            var root = new GameObject(pieceName);
            var piece = root.AddComponent<LevelPiece3D>();
            piece.Configure(pieceTag, doors, weight);
            PieceAuthoring3D.RebuildShell(piece, kit, recordUndo: false);

            // Обязательные детали для старта и выхода — чтобы кусок сразу проходил проверку
            if (pieceTag == PieceData3D.Tag.Start && kit.spawnPrefab != null)
                PrefabUtility.InstantiatePrefab(kit.spawnPrefab, root.transform);
            if (pieceTag == PieceData3D.Tag.Exit && kit.exitPrefab != null)
                PrefabUtility.InstantiatePrefab(kit.exitPrefab, root.transform);

            PieceAuthoring3D.EnsureFolder(PieceAuthoring3D.PiecesFolder);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            DestroyImmediate(root);

            // Данные генератора: ссылку на них сохраняем в самом префабе
            using (var editing = new PrefabUtility.EditPrefabContentsScope(path))
            {
                PieceAuthoring3D.SyncData(editing.prefabContentsRoot.GetComponent<LevelPiece3D>(), path);
            }

            // Если открыта игровая сцена — сразу подключаем кусок к генератору
            var generator = FindFirstObjectByType<LevelGenerator3D>();
            if (generator != null)
            {
                LevelGenerator3DEditor.Collect(new SerializedObject(generator));
                EditorSceneManager.MarkSceneDirty(generator.gameObject.scene);
            }

            AssetDatabase.OpenAsset(prefab);
            Debug.Log($"[NewLevelPiece] {path} создан. Собери его из деталей (инспектор корня куска) и нажми «Проверить кусок».");
        }
    }
}
