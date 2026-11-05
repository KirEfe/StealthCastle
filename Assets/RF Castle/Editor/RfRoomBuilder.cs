using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using StealthCastle.Isometric.World;

namespace StealthCastle.RfCastle.Editor
{
    /// <summary>Сборка одной комнаты из тайлов RF Castle: пол + стены по периметру + проём в указанной стороне.</summary>
    public static class RfRoomBuilder
    {
        /// <summary>
        /// Собрать GO комнаты.
        /// </summary>
        /// <param name="name">Имя префаба (Piece_Start и т.д.).</param>
        /// <param name="sizeInTiles">Размер комнаты в тайлах (ширина, высота).</param>
        /// <param name="doorDirs">Стороны с проёмами (для стыковки с ConnectionPoint).</param>
        /// <param name="tileSet">Набор тайлов.</param>
        /// <param name="floorY">Y пола (тайлы стен ставятся от floorY до floorY+height-1).</param>
        public static GameObject BuildRoom(string name, Vector2Int sizeInTiles,
            List<ConnectionPoint.Dir> doorDirs, RfTileSet tileSet, int floorY = 0)
        {
            var root = new GameObject(name);
            root.transform.position = Vector3.zero;

            var floorParent = new GameObject("Floor");
            floorParent.transform.SetParent(root.transform, false);
            var wallParent = new GameObject("Walls");
            wallParent.transform.SetParent(root.transform, false);

            // Пол на всю комнату
            for (var x = 0; x < sizeInTiles.x; x++)
            for (var y = 0; y < sizeInTiles.y; y++)
            {
                var tile = CreateTile(tileSet.floor, new Vector3(x, y, 0f), floorParent.transform);
                tile.name = $"Floor_{x}_{y}";
            }

            // Стены по периметру: нижний ряд (y=0), левый/правый столбцы, верхний ряд (y=h-1)
            for (var x = 0; x < sizeInTiles.x; x++)
            {
                SetWall(x, 0, wallParent.transform, tileSet, doorDirs, sizeInTiles, floorY);            // низ
                SetWall(x, sizeInTiles.y - 1, wallParent.transform, tileSet, doorDirs, sizeInTiles, floorY); // верх
            }
            for (var y = 1; y < sizeInTiles.y - 1; y++)
            {
                SetWall(0, y, wallParent.transform, tileSet, doorDirs, sizeInTiles, floorY);            // лево
                SetWall(sizeInTiles.x - 1, y, wallParent.transform, tileSet, doorDirs, sizeInTiles, floorY); // право
            }

            return root;
        }

        // Поставить стену в (x,y), если тут нет проёма
        private static void SetWall(int x, int y, Transform parent, RfTileSet tileSet,
            List<ConnectionPoint.Dir> doorDirs, Vector2Int sizeInTiles, int floorY)
        {
            // Определяем, является ли эта клетка проёмом
            if (IsDoorAt(x, y, doorDirs, sizeInTiles))
            {
                CreateTile(tileSet.door, new Vector3(x, y, 0f), parent).name = $"Door_{x}_{y}";
                return;
            }

            var sprite = tileSet.accent != null && Random.value < 0.15f ? tileSet.accent : tileSet.wall;
            CreateTile(sprite, new Vector3(x, y, 0f), parent).name = $"Wall_{x}_{y}";
        }

        // Проём, если клетка на краю в указанной стороне
        private static bool IsDoorAt(int x, int y, List<ConnectionPoint.Dir> doorDirs, Vector2Int size)
        {
            foreach (var dir in doorDirs)
            {
                switch (dir)
                {
                    case ConnectionPoint.Dir.North:
                        if (y == size.y - 1 && x == size.x / 2) return true;
                        break;
                    case ConnectionPoint.Dir.South:
                        if (y == 0 && x == size.x / 2) return true;
                        break;
                    case ConnectionPoint.Dir.West:
                        if (x == 0 && y == size.y / 2) return true;
                        break;
                    case ConnectionPoint.Dir.East:
                        if (x == size.x - 1 && y == size.y / 2) return true;
                        break;
                }
            }
            return false;
        }

        private static GameObject CreateTile(Sprite sprite, Vector3 pos, Transform parent)
        {
            var go = new GameObject("Tile");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 0;
            // Коллайдер для стены (для raycast зрения врага, слой Wall)
            if (go.name.StartsWith("Wall_") || go.name.StartsWith("Door_"))
            {
                var col = go.AddComponent<BoxCollider2D>();
                col.size = new Vector2(1f, 1f);
            }
            return go;
        }
    }
}
