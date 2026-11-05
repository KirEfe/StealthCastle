using System.Collections.Generic;
using UnityEngine;

namespace StealthCastle.Isometric3D.World
{
    /// <summary>
    /// Описание куска уровня на корне его префаба: тип, проёмы, вес. Источник правды для авторинга —
    /// инспектор куска перестраивает по нему стены периметра и обновляет PieceData3D для генератора.
    /// Кусок 12×12 м, пол на y = 0, центр в начале координат; проёмы 3 м по центрам сторон.
    /// </summary>
    public class LevelPiece3D : MonoBehaviour
    {
        public const float Size = 12f;
        public const float DoorWidth = 3f;

        [SerializeField] private PieceData3D.Tag pieceTag = PieceData3D.Tag.Empty;

        [Header("Doors")]
        [SerializeField] private bool doorNorth = true;
        [SerializeField] private bool doorEast;
        [SerializeField] private bool doorSouth = true;
        [SerializeField] private bool doorWest;

        [SerializeField, Min(0f)] private float weight = 1f; // Вес при случайном выборе куска генератором

        [Tooltip("Данные для генератора. Создаются и обновляются кнопкой «Обновить PieceData»")]
        [SerializeField] private PieceData3D data;

        public PieceData3D.Tag PieceTag => pieceTag;
        public float Weight => weight;
        public PieceData3D Data { get => data; set => data = value; }

        public bool HasDoor(ConnectionPoint3D.Dir dir) => dir switch
        {
            ConnectionPoint3D.Dir.North => doorNorth,
            ConnectionPoint3D.Dir.East => doorEast,
            ConnectionPoint3D.Dir.South => doorSouth,
            _ => doorWest,
        };

        public List<ConnectionPoint3D.Dir> Doors
        {
            get
            {
                var doors = new List<ConnectionPoint3D.Dir>();
                foreach (ConnectionPoint3D.Dir dir in System.Enum.GetValues(typeof(ConnectionPoint3D.Dir)))
                {
                    if (HasDoor(dir)) doors.Add(dir);
                }
                return doors;
            }
        }

        public void Configure(PieceData3D.Tag tag, IEnumerable<ConnectionPoint3D.Dir> doors, float pieceWeight = 1f)
        {
            pieceTag = tag;
            doorNorth = doorEast = doorSouth = doorWest = false;
            foreach (var dir in doors)
            {
                switch (dir)
                {
                    case ConnectionPoint3D.Dir.North: doorNorth = true; break;
                    case ConnectionPoint3D.Dir.East: doorEast = true; break;
                    case ConnectionPoint3D.Dir.South: doorSouth = true; break;
                    default: doorWest = true; break;
                }
            }
            weight = pieceWeight;
        }

        /// <summary>Центр проёма на границе куска, в плане (x, z)</summary>
        public static Vector2 DoorLocalPosition(ConnectionPoint3D.Dir dir) => dir switch
        {
            ConnectionPoint3D.Dir.North => new Vector2(0f, Size * 0.5f),
            ConnectionPoint3D.Dir.East => new Vector2(Size * 0.5f, 0f),
            ConnectionPoint3D.Dir.South => new Vector2(0f, -Size * 0.5f),
            _ => new Vector2(-Size * 0.5f, 0f),
        };

        private void OnDrawGizmos()
        {
            // Граница куска и проёмы: при сборке руками видно, где стыкуются соседи
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, 1f, 1f, 0.35f);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(Size, 0.02f, Size));

            Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.9f);
            foreach (var dir in Doors)
            {
                var door = DoorLocalPosition(dir);
                var center = new Vector3(door.x, 0.05f, door.y);
                bool alongX = dir == ConnectionPoint3D.Dir.North || dir == ConnectionPoint3D.Dir.South;
                Gizmos.DrawCube(center, alongX ? new Vector3(DoorWidth, 0.1f, 0.3f) : new Vector3(0.3f, 0.1f, DoorWidth));
            }
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
