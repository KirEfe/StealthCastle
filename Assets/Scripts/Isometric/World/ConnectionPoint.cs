using UnityEngine;

namespace StealthCastle.Isometric.World
{
    /// <summary>
    /// Точка стыковки куска локации: позиция + направление проёма.
    /// </summary>
    [System.Serializable]
    public struct ConnectionPoint
    {
        // Направления проёма по кусковой сетке
        public enum Dir { North, East, South, West }

        [SerializeField] private Vector2 localPosition; // позиция в локальных координатах куска
        [SerializeField] private Dir direction;          // куда выходит проём
        [SerializeField] private string openingType;     // тип проёма (для будущих фильтров)

        public Vector2 LocalPosition => localPosition;
        public Dir Direction => direction;
        public string OpeningType => openingType;

        public ConnectionPoint(Vector2 pos, Dir dir, string type = "default")
        {
            localPosition = pos;
            direction = dir;
            openingType = type;
        }

        // Противоположное направление проёма (N↔S, E↔W)
        public static Dir Opposite(Dir d) => d switch
        {
            Dir.North => Dir.South,
            Dir.South => Dir.North,
            Dir.East => Dir.West,
            _ => Dir.East,
        };

        // Мировая позиция проёма относительно корня куска
        public Vector3 WorldPos(Transform pieceRoot)
            => pieceRoot.TransformPoint(new Vector3(localPosition.x, localPosition.y, 0f));
    }
}
