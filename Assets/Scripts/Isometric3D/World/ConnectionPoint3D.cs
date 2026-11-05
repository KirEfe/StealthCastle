using UnityEngine;

namespace StealthCastle.Isometric3D.World
{
    /// <summary>
    /// Точка соединения для 3D кусков.
    /// Определяет позицию и направление проёма для стыковки с другими кусками.
    /// </summary>
    [System.Serializable]
    public struct ConnectionPoint3D
    {
        /// <summary>Направления проёма в 3D пространстве</summary>
        public enum Dir { North, East, South, West }

        [SerializeField] private Vector2 localPosition; // Локальная позиция в куске
        [SerializeField] private Dir direction; // Направление проёма
        [SerializeField] private string openingType; // Тип проёма

        /// <summary>Локальная позиция</summary>
        public Vector2 LocalPosition => localPosition;

        /// <summary>Направление</summary>
        public Dir Direction => direction;

        /// <summary>Тип проёма</summary>
        public string OpeningType => openingType;

        /// <summary>
        /// Конструктор точки соединения
        /// </summary>
        public ConnectionPoint3D(Vector2 pos, Dir dir, string type = "default")
        {
            localPosition = pos;
            direction = dir;
            openingType = type;
        }

        /// <summary>
        /// Получение противоположного направления
        /// </summary>
        public static Dir Opposite(Dir d) => d switch
        {
            Dir.North => Dir.South,
            Dir.South => Dir.North,
            Dir.East => Dir.West,
            _ => Dir.East,
        };

        /// <summary>
        /// Получение мировой позиции точки относительно корня куска
        /// </summary>
        public Vector3 WorldPos(Transform pieceRoot)
        {
            return pieceRoot.TransformPoint(new Vector3(localPosition.x, 0f, localPosition.y));
        }

        /// <summary>
        /// Преобразование направления в вектор
        /// </summary>
        public static Vector3 DirectionToVector(Dir dir) => dir switch
        {
            Dir.North => Vector3.forward,
            Dir.East => Vector3.right,
            Dir.South => Vector3.back,
            Dir.West => Vector3.left,
            _ => Vector3.right,
        };

        /// <summary>
        /// Преобразование вектора в направление
        /// </summary>
        public static Dir VectorToDirection(Vector3 vector)
        {
            vector.Normalize();
            
            if (Mathf.Abs(vector.z) > Mathf.Abs(vector.x))
            {
                return vector.z > 0 ? Dir.North : Dir.South;
            }
            else
            {
                return vector.x > 0 ? Dir.East : Dir.West;
            }
        }
    }
}