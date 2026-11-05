using UnityEngine;

namespace StealthCastle.Isometric3D.World
{
    /// <summary>
    /// Данные куска локации для 3D изометрической версии.
    /// Содержит информацию о префабе, размере, тегах и точках соединения.
    /// </summary>
    [CreateAssetMenu(fileName = "PieceData3D", menuName = "StealthCastle/3D Piece Data")]
    public class PieceData3D : ScriptableObject
    {
        /// <summary>Теги куска для генератора уровней</summary>
        public enum Tag { Start, Exit, Treasure, Combat, Empty }

        [Header("Piece Settings")]
        [SerializeField] private GameObject prefab; // Префаб куска
        [SerializeField] private Vector2Int sizeInTiles = new Vector2Int(4, 4); // Размер в тайлах
        [SerializeField] private Tag pieceTag = Tag.Empty; // Тег куска
        [SerializeField, Min(0f)] private float weight = 1f; // Вес для генерации

        [Header("Connection Points")]
        [SerializeField] private ConnectionPoint3D[] connections; // Точки соединения

        [Header("Visual Settings")]
        [SerializeField] private Color floorColor = new Color(0.25f, 0.25f, 0.3f); // Цвет пола

        /// <summary>Префаб куска</summary>
        public GameObject Prefab => prefab;

        /// <summary>Размер в тайлах</summary>
        public Vector2Int SizeInTiles => sizeInTiles;

        /// <summary>Тег куска</summary>
        public Tag PieceTag => pieceTag;

        /// <summary>Вес для генерации</summary>
        public float Weight => weight;

        /// <summary>Точки соединения</summary>
        public ConnectionPoint3D[] Connections => connections;

        /// <summary>Цвет пола</summary>
        public Color FloorColor => floorColor;
    }
}