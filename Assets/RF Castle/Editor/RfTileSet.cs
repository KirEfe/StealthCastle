using UnityEngine;

namespace StealthCastle.RfCastle
{
    /// <summary>Набор тайлов RF Castle для MVP-комнат (однотонные, 1 тайл = 1x1 м, PPU=16).</summary>
    [CreateAssetMenu(fileName = "RfTileSet", menuName = "StealthCastle/RF Castle/Tile Set")]
    public class RfTileSet : ScriptableObject
    {
        [Header("Тайлы (по 1 на роль, MVP)")]
        public Sprite floor;   // однотонный пол
        public Sprite wall;    // однотонная стена
        public Sprite door;    // проём/дверь (тёмный)
        public Sprite accent;  // мох/акцент (опционально)
    }
}
