using UnityEngine;

namespace StealthCastle.Isometric.World
{
    /// <summary>
    /// Компонент-держатель PieceData на инстансе куска в сцене.
    /// </summary>
    public class PieceDataHolder : MonoBehaviour
    {
        [SerializeField] private PieceData data;
        public PieceData Data { get => data; set => data = value; }
    }
}
