using UnityEngine;

namespace StealthCastle.Isometric.World
{
    /// <summary>
    /// Данные куска локации (контракт из ГДД).
    /// Артефакты фильтруют/взвешивают куски по тегам.
    /// </summary>
    [CreateAssetMenu(fileName = "PieceData", menuName = "StealthCastle/Piece Data")]
    public class PieceData : ScriptableObject
    {
        /// <summary>Теги куска — артефакты работают через них.</summary>
        public enum Tag { Start, Exit, Treasure, Combat, Empty }

        [Header("Кусок")]
        [SerializeField] private GameObject prefab;              // префаб куска
        [SerializeField] private Vector2Int sizeInTiles = new Vector2Int(4, 4); // размер в тайлах кусковой сетки
        [SerializeField] private Tag pieceTag = Tag.Empty;       // тег для генератора
        [SerializeField, Min(0f)] private float weight = 1f;      // базовый шанс выпадения

        [Header("Точки стыковки")]
        [SerializeField] private ConnectionPoint[] connections;   // проёмы куска

        public GameObject Prefab => prefab;
        public Vector2Int SizeInTiles => sizeInTiles;
        public PieceTag PieceTagValue => new PieceTag(pieceTag);
        public float Weight => weight;
        public ConnectionPoint[] Connections => connections;

        // Обёртка над enum, чтобы не ломать внешние ссылки при расширении
        public readonly struct PieceTag
        {
            public readonly Tag Value;
            public PieceTag(Tag value) => Value = value;
        }
    }
}
