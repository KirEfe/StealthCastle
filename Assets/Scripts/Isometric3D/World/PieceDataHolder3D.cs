using UnityEngine;

namespace StealthCastle.Isometric3D.World
{
    /// <summary>
    /// Контейнер для хранения данных куска на GameObject.
    /// Используется для доступа к PieceData3D из префабов.
    /// </summary>
    public class PieceDataHolder3D : MonoBehaviour
    {
        [SerializeField] private PieceData3D data;

        /// <summary>Данные куска</summary>
        public PieceData3D Data
        {
            get => data;
            set => data = value;
        }
    }
}