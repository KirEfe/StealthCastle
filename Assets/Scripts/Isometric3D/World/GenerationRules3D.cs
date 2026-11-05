using System.Collections.Generic;
using UnityEngine;

namespace StealthCastle.Isometric3D.World
{
    /// <summary>
    /// Правила генерации уровней для 3D изометрической версии.
    /// Определяет параметры генерации и модификаторы весов для разных типов кусков.
    /// </summary>
    [CreateAssetMenu(fileName = "GenerationRules3D", menuName = "StealthCastle/3D Generation Rules")]
    public class GenerationRules3D : ScriptableObject
    {
        [Header("Level Length")]
        [SerializeField, Min(2)] private int minPieces = 4;
        [SerializeField, Min(2)] private int maxPieces = 6;

        [Header("Guarantees")]
        [SerializeField] private bool requireSingleStart = true; // Ровно один старт
        [SerializeField] private bool requireSingleExit = true;  // Ровно один выход
        [SerializeField] private bool treasureOptional = true;   // Сокровище опционально

        [Header("Piece Pool")]
        [SerializeField] private List<PieceData3D> piecePool = new List<PieceData3D>();

        // Модификаторы весов по тегам (для артефактов)
        private readonly Dictionary<PieceData3D.Tag, float> tagWeightMultipliers = new Dictionary<PieceData3D.Tag, float>();

        /// <summary>Минимальное количество кусков</summary>
        public int MinPieces => minPieces;

        /// <summary>Максимальное количество кусков</summary>
        public int MaxPieces => maxPieces;

        /// <summary>Пул кусков</summary>
        public IReadOnlyList<PieceData3D> PiecePool => piecePool;

        /// <summary>
        /// Установка модификатора веса для тега
        /// </summary>
        public void SetTagMultiplier(PieceData3D.Tag tag, float multiplier)
        {
            tagWeightMultipliers[tag] = multiplier;
        }

        /// <summary>
        /// Сброс всех модификаторов
        /// </summary>
        public void ClearTagMultipliers()
        {
            tagWeightMultipliers.Clear();
        }

        /// <summary>
        /// Взвешенный случайный выбор куска
        /// </summary>
        public PieceData3D PickWeighted(
            System.Random rng, 
            PieceData3D.Tag? excludeTag = null, 
            PieceData3D.Tag? excludeTag2 = null)
        {
            var candidates = new List<(PieceData3D piece, float w)>();
            var total = 0f;

            foreach (var p in piecePool)
            {
                if (p == null) continue;
                if (excludeTag.HasValue && p.PieceTag == excludeTag.Value) continue;
                if (excludeTag2.HasValue && p.PieceTag == excludeTag2.Value) continue;
                
                var mult = tagWeightMultipliers.TryGetValue(p.PieceTag, out var m) ? m : 1f;
                var w = p.Weight * mult;
                if (w <= 0f) continue;
                
                candidates.Add((p, w));
                total += w;
            }

            if (candidates.Count == 0) return null;
            
            var roll = (float)rng.NextDouble() * total;
            foreach (var (piece, w) in candidates)
            {
                roll -= w;
                if (roll <= 0f) return piece;
            }
            return candidates[candidates.Count - 1].piece;
        }

        /// <summary>
        /// Добавление куска в пул
        /// </summary>
        public void AddPieceToPool(PieceData3D piece)
        {
            if (piece != null && !piecePool.Contains(piece))
            {
                piecePool.Add(piece);
            }
        }

        /// <summary>
        /// Удаление куска из пула
        /// </summary>
        public void RemovePieceFromPool(PieceData3D piece)
        {
            piecePool.Remove(piece);
        }
    }
}