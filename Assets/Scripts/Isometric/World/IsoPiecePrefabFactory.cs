using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using StealthCastle.Isometric.Enemies;
using StealthCastle.Isometric.Mechanics;
using StealthCastle.Isometric.Player;
using StealthCastle.Isometric.Core;

namespace StealthCastle.Isometric.World.Editor
{
    /// <summary>
    /// Фабрика префабов кусков (Editor-only). Создаёт 6 тестовых кусков по ГДД:
    /// пол, стены-плейсхолдеры, пропы для маскировки, враг, триггер выхода.
    /// Проёмы заданы в единой сетке, чтобы стыковка в LevelGenerator совпадала.
    /// </summary>
    public static class IsoPiecePrefabFactory
    {
        private const float Tile = 2f;       // размер тайла, как в LevelGenerator
        private const string Folder = "Assets/Prefabs/World/IsoPieces";

        // Цвет пола по тегу
        private static Color32 FloorColor(PieceData.Tag tag) => tag switch
        {
            PieceData.Tag.Start => new Color32(80, 140, 90, 255),
            PieceData.Tag.Exit => new Color32(120, 100, 200, 255),
            PieceData.Tag.Treasure => new Color32(200, 170, 70, 255),
            PieceData.Tag.Combat => new Color32(160, 80, 80, 255),
            _ => new Color32(70, 70, 90, 255),
        };

        [MenuItem("StealthCastle/Создать префабы кусков (Iso)")]
        public static void CreateAll()
        {
            System.IO.Directory.CreateDirectory(Folder);
            var wallLayer = LayerMask.NameToLayer("Wall");
            var propLayer = LayerMask.NameToLayer("Prop");
            if (wallLayer < 0) wallLayer = 0;
            if (propLayer < 0) propLayer = 0;

            CreatePiece("Piece_Start", PieceData.Tag.Start, new Vector2Int(4, 4),
                new[] { Dir2Conn(ConnectionPoint.Dir.South, new Vector2Int(4,4)), Dir2Conn(ConnectionPoint.Dir.East, new Vector2Int(4,4)) },
                wallLayer, propLayer, false, 0, false);
            CreatePiece("Piece_CorridorStraight", PieceData.Tag.Empty, new Vector2Int(2, 4),
                new[] { Dir2Conn(ConnectionPoint.Dir.North, new Vector2Int(2,4)), Dir2Conn(ConnectionPoint.Dir.South, new Vector2Int(2,4)) },
                wallLayer, propLayer, false, 0, false);
            CreatePiece("Piece_CorridorL", PieceData.Tag.Empty, new Vector2Int(4, 4),
                new[] { Dir2Conn(ConnectionPoint.Dir.North, new Vector2Int(4,4)), Dir2Conn(ConnectionPoint.Dir.West, new Vector2Int(4,4)), Dir2Conn(ConnectionPoint.Dir.South, new Vector2Int(4,4)) },
                wallLayer, propLayer, false, 0, false);
            CreatePiece("Piece_CombatHall", PieceData.Tag.Combat, new Vector2Int(6, 6),
                new[] { Dir2Conn(ConnectionPoint.Dir.North, new Vector2Int(6,6)), Dir2Conn(ConnectionPoint.Dir.South, new Vector2Int(6,6)) },
                wallLayer, propLayer, false, 1, false);
            CreatePiece("Piece_TreasureRoom", PieceData.Tag.Treasure, new Vector2Int(4, 4),
                new[] { Dir2Conn(ConnectionPoint.Dir.North, new Vector2Int(4,4)) },
                wallLayer, propLayer, false, 0, true);
            CreatePiece("Piece_ExitHall", PieceData.Tag.Exit, new Vector2Int(4, 4),
                new[] { Dir2Conn(ConnectionPoint.Dir.North, new Vector2Int(4,4)) },
                wallLayer, propLayer, true, 0, false);

            AssetDatabase.SaveAssets();
            Debug.Log("[IsoPiecePrefabFactory] Созданы префабы кусков в " + Folder);
        }

        // Коннектор по направлению (проём по центру соответствующей стены, на краю куска)
        private static ConnectionPoint Dir2Conn(ConnectionPoint.Dir d, Vector2Int sizeTiles)
        {
            var half = new Vector2(sizeTiles.x, sizeTiles.y) * (Tile * 0.5f);
            return d switch
            {
                ConnectionPoint.Dir.North => new ConnectionPoint(new Vector2(0, half.y), ConnectionPoint.Dir.North),
                ConnectionPoint.Dir.South => new ConnectionPoint(new Vector2(0, -half.y), ConnectionPoint.Dir.South),
                ConnectionPoint.Dir.East => new ConnectionPoint(new Vector2(half.x, 0), ConnectionPoint.Dir.East),
                _ => new ConnectionPoint(new Vector2(-half.x, 0), ConnectionPoint.Dir.West),
            };
        }

        // Создание одного префаба куска
        private static void CreatePiece(string name, PieceData.Tag tag, Vector2Int sizeTiles,
            ConnectionPoint[] conns, int wallLayer, int propLayer, bool isExit, int enemyCount,
            bool withChest)
        {
            var sizeM = new Vector2(sizeTiles.x, sizeTiles.y) * Tile;
            var root = new GameObject(name);
            root.AddComponent<PieceDataHolder>();

            // Пол
            var floor = new GameObject("Floor");
            floor.transform.SetParent(root.transform, false);
            floor.transform.localScale = new Vector3(sizeM.x, sizeM.y * 0.5774f, 1f);
            var fsr = floor.AddComponent<SpriteRenderer>();
            fsr.sprite = StealthCastle.Debugging.PlaceholderSpriteFactory.CreateSquareSpriteAsset(FloorColor(tag));
            fsr.sortingOrder = -10;

            // Стены по периметру: сплошные сегменты с проломом 2 м в центре, если там проём
            const float gap = 2f; // ширина проёма
            bool hasN = HasConn(conns, ConnectionPoint.Dir.North);
            bool hasS = HasConn(conns, ConnectionPoint.Dir.South);
            bool hasE = HasConn(conns, ConnectionPoint.Dir.East);
            bool hasW = HasConn(conns, ConnectionPoint.Dir.West);

            // Север/юг (горизонтальные): делим на левый/правый сегменты
            BuildWallWithGap(root, Axis.Horiz, sizeM.y * 0.5f, sizeM.x, gap, hasN, wallLayer);
            BuildWallWithGap(root, Axis.Horiz, -sizeM.y * 0.5f, sizeM.x, gap, hasS, wallLayer);
            // Запад/восток (вертикальные)
            BuildWallWithGap(root, Axis.Vert, -sizeM.x * 0.5f, sizeM.y, gap, hasW, wallLayer);
            BuildWallWithGap(root, Axis.Vert, sizeM.x * 0.5f, sizeM.y, gap, hasE, wallLayer);

            // Пропы для маскировки (2-3 в боевых/сокровищных)
            if (tag == PieceData.Tag.Combat || tag == PieceData.Tag.Treasure)
            {
                AddProp(root, new Vector3(-1f, 1f, 0), propLayer, "Barrel");
                AddProp(root, new Vector3(1f, -1f, 0), propLayer, "Crate");
                if (tag == PieceData.Tag.Combat) AddProp(root, new Vector3(0, 2f, 0), propLayer, "Statue");
            }

            // Сундук как отдельный проп (цель Treasure)
            if (withChest) AddProp(root, new Vector3(0, 0, 0), propLayer, "Chest");

            // Враг(и)
            for (var i = 0; i < enemyCount; i++)
                AddEnemy(root, new Vector3(0, -1f, 0), wallLayer, sizeTiles);

            // Триггер выхода
            if (isExit)
            {
                var exit = new GameObject("ExitTrigger");
                exit.transform.SetParent(root.transform, false);
                exit.transform.position = new Vector3(0, 1f, 0);
                exit.AddComponent<BoxCollider2D>().size = new Vector2(1.5f, 1.5f);
                exit.AddComponent<LevelExit>();
                var esr = exit.AddComponent<SpriteRenderer>();
                esr.sprite = StealthCastle.Debugging.PlaceholderSpriteFactory.CreateSquareSpriteAsset(new Color32(150, 220, 255, 200));
                esr.sortingOrder = -5;
            }

            // Сохранение префаба + привязка PieceData (ассет создаётся рядом, если нет)
            var prefabPath = $"{Folder}/{name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);

            // Создаём/обновляем ассет PieceData и привязываем к holder в префабе
            var dataPath = $"{Folder}/{name}_Data.asset";
            var data = AssetDatabase.LoadAssetAtPath<PieceData>(dataPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<PieceData>();
                AssetDatabase.CreateAsset(data, dataPath);
            }
            // Поля PieceData — read-only свойства; пишем через сериализованные поля
            SetPieceField(data, "prefab", prefab);
            SetPieceField(data, "sizeInTiles", sizeTiles);
            SetPieceField(data, "connections", conns);
            // Тег/вес — тоже через сериализованные поля
            SetPieceTag(data, tag);
            SetPieceWeight(data, tag);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();

            // Привязываем PieceData к PieceDataHolder внутри префаба
            var prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            var holder = prefabRoot.GetComponent<PieceDataHolder>();
            holder.Data = data;
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        // Ось стены: горизонтальная (вдоль X) или вертикальная (вдоль Y)
        private enum Axis { Horiz, Vert }

        // Есть ли проём в заданном направлении
        private static bool HasConn(ConnectionPoint[] conns, ConnectionPoint.Dir d)
        {
            foreach (var c in conns) if (c.Direction == d) return true;
            return false;
        }

        // Стена вдоль оси с проломом gap в центре (если hasConn — проём открыт)
        private static void BuildWallWithGap(GameObject parent, Axis axis, float fixedPos, float length, float gap, bool hasConn, int layer)
        {
            if (!hasConn)
            {
                // Сплошная стена
                AddWallSegment(parent, axis, fixedPos, 0f, length, layer);
                return;
            }
            // Два сегмента по бокам от пролома
            var segLen = (length - gap) * 0.5f;
            var offset = (segLen + gap) * 0.5f;
            AddWallSegment(parent, axis, fixedPos, -offset, segLen, layer);
            AddWallSegment(parent, axis, fixedPos, offset, segLen, layer);
        }

        // Один сегмент стены (centerOffset — смещение от центра стены вдоль её оси)
        private static void AddWallSegment(GameObject parent, Axis axis, float fixedPos, float centerOffset, float length, int layer)
        {
            var w = new GameObject("Wall");
            w.transform.SetParent(parent.transform, false);
            if (axis == Axis.Horiz)
            {
                w.transform.localPosition = new Vector3(centerOffset, fixedPos, 0);
                w.transform.localScale = new Vector3(length, 0.4f, 1);
            }
            else
            {
                w.transform.localPosition = new Vector3(fixedPos, centerOffset, 0);
                w.transform.localScale = new Vector3(0.4f, length, 1);
            }
            w.layer = layer;
            w.AddComponent<BoxCollider2D>();
            var sr = w.AddComponent<SpriteRenderer>();
            sr.sprite = StealthCastle.Debugging.PlaceholderSpriteFactory.CreateSquareSpriteAsset(new Color32(110, 110, 130, 255));
            sr.sortingOrder = -9;
        }

        // Проп для маскировки (Sprite + CircleCollider2D trigger + IsoDisguisableProp)
        private static void AddProp(GameObject parent, Vector3 pos, int layer, string label)
        {
            var p = new GameObject("Prop_" + label);
            p.transform.SetParent(parent.transform, false);
            p.transform.localPosition = pos;
            p.layer = layer;
            var col = p.AddComponent<CircleCollider2D>();
            col.radius = 0.4f;
            col.isTrigger = true;
            var sr = p.AddComponent<SpriteRenderer>();
            sr.sprite = StealthCastle.Debugging.PlaceholderSpriteFactory.CreateSquareSpriteAsset(
                label == "Chest" ? new Color32(210, 180, 60, 255) : new Color32(150, 120, 90, 255));
            sr.sortingOrder = 0;
            p.AddComponent<IsoDisguisableProp>();
        }

        // Враг: Rigidbody2D + IsoEnemyAI + дочерний визуал + VisionRoot со спрайтом зоны + 4 waypoints
        private static void AddEnemy(GameObject parent, Vector3 pos, int wallLayer, Vector2 roomSizeTiles = default)
        {
            var e = new GameObject("Enemy");
            e.transform.SetParent(parent.transform, false);
            e.transform.localPosition = pos;
            e.transform.localScale = new Vector3(0.8f, 0.8f, 1f); // визуальный размер врага ~0.8 м
            var rb = e.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            var ai = e.AddComponent<IsoEnemyAI>();
            ai.WallMask = 1 << wallLayer;

            // Дочерний визуал врага (спрайт + flipX) — отдельно от зоны обзора
            var visual = new GameObject("Visual");
            visual.transform.SetParent(e.transform, false);
            var esr = visual.AddComponent<SpriteRenderer>();
            esr.sprite = StealthCastle.Debugging.PlaceholderSpriteFactory.CreateSquareSpriteAsset(new Color32(200, 60, 60, 255));
            esr.sortingOrder = 1;
            SetEnemyField(ai, "guardSprite", esr);

            // VisionRoot: pivot в центре врага, вращается по IVisionSource
            var visionRoot = new GameObject("VisionRoot");
            visionRoot.transform.SetParent(e.transform, false);
            visionRoot.transform.localPosition = Vector3.zero;
            visionRoot.AddComponent<EnemyVision>();

            // Дочерний спрайт зоны — просто покрашенная плашка, скейл от родителя
            var zoneSprite = new GameObject("ZoneSprite");
            zoneSprite.transform.SetParent(visionRoot.transform, false);
            var zsr = zoneSprite.AddComponent<SpriteRenderer>();
            zsr.color = new Color(1f, 1f, 0f, 0.15f);
            zsr.sortingOrder = -5;
            zsr.sprite = StealthCastle.Debugging.PlaceholderSpriteFactory.CreateWhiteSprite();

            // Четыре waypoint'а по углам комнаты (локальные), с отступом 1 м от стен.
            // Если размер комнаты задан — точки привязаны к ней, иначе дефолт ±5 (комната 12×12).
            var halfW = roomSizeTiles != default ? roomSizeTiles.x - 1f : 5f;
            var halfH = roomSizeTiles != default ? roomSizeTiles.y - 1f : 5f;
            var wpField = serializedWaypointArray(ai);
            wpField.arraySize = 4;
            wpField.GetArrayElementAtIndex(0).vector3Value = new Vector3(-halfW, -halfH, 0);
            wpField.GetArrayElementAtIndex(1).vector3Value = new Vector3(halfW, -halfH, 0);
            wpField.GetArrayElementAtIndex(2).vector3Value = new Vector3(halfW, halfH, 0);
            wpField.GetArrayElementAtIndex(3).vector3Value = new Vector3(-halfW, halfH, 0);
            wpField.serializedObject.ApplyModifiedProperties();
        }

        // Сериализованная привязка приватного [SerializeField] поля IsoEnemyAI
        private static void SetEnemyField(IsoEnemyAI ai, string fieldName, Object value)
        {
            var so = new SerializedObject(ai);
            var prop = so.FindProperty(fieldName);
            if (prop != null) { prop.objectReferenceValue = value; so.ApplyModifiedProperties(); }
        }

        // Reflection-хелпер для приватного [SerializeField] Vector3[] waypoints
        private static SerializedProperty serializedWaypointArray(IsoEnemyAI ai)
        {
            var so = new SerializedObject(ai);
            return so.FindProperty("waypoints");
        }

        // Установка сериализованного поля PieceData по имени (для read-only свойств)
        private static void SetPieceField(PieceData data, string fieldName, Object value)
        {
            var so = new SerializedObject(data);
            var prop = so.FindProperty(fieldName);
            prop.objectReferenceValue = value;
            so.ApplyModifiedProperties();
        }

        private static void SetPieceField(PieceData data, string fieldName, Vector2Int value)
        {
            var so = new SerializedObject(data);
            var prop = so.FindProperty(fieldName);
            prop.vector2IntValue = value;
            so.ApplyModifiedProperties();
        }

        private static void SetPieceField(PieceData data, string fieldName, ConnectionPoint[] value)
        {
            var so = new SerializedObject(data);
            var prop = so.FindProperty(fieldName);
            prop.arraySize = value.Length;
            for (var i = 0; i < value.Length; i++)
            {
                var elem = prop.GetArrayElementAtIndex(i);
                var lp = elem.FindPropertyRelative("localPosition");
                var dirP = elem.FindPropertyRelative("direction");
                var typeP = elem.FindPropertyRelative("openingType");
                lp.vector2Value = value[i].LocalPosition;
                dirP.enumValueIndex = (int)value[i].Direction;
                typeP.stringValue = value[i].OpeningType;
            }
            so.ApplyModifiedProperties();
        }

        private static void SetPieceTag(PieceData data, PieceData.Tag tag)
        {
            var so = new SerializedObject(data);
            var prop = so.FindProperty("pieceTag");
            prop.enumValueIndex = (int)tag;
            so.ApplyModifiedProperties();
        }

        private static void SetPieceWeight(PieceData data, PieceData.Tag tag)
        {
            var so = new SerializedObject(data);
            var prop = so.FindProperty("weight");
            // Базовые веса по ГДД (Start/Exit — не участвуют в случайном пуле как веса, но зададим)
            prop.floatValue = tag switch
            {
                PieceData.Tag.Start => 1f,
                PieceData.Tag.Exit => 1f,
                PieceData.Tag.Combat => 1.5f,
                PieceData.Tag.Treasure => 1f,
                _ => 1f,
            };
            so.ApplyModifiedProperties();
        }
    }
}
