using System.Collections.Generic;
using UnityEngine;

namespace StealthCastle.Isometric.World
{
    /// <summary>
    /// Правила генерации уровня (контракт из ГДД).
    /// Артефакты влияют ТОЛЬКО через модификаторы весов — генератор не хардкодится.
    /// </summary>
    [CreateAssetMenu(fileName = "GenerationRules", menuName = "StealthCastle/Generation Rules")]
    public class GenerationRules : ScriptableObject
    {
        [Header("Длина уровня")]
        [SerializeField, Min(2)] private int minPieces = 4;
        [SerializeField, Min(2)] private int maxPieces = 6;

        [Header("Гарантии")]
        [SerializeField] private bool requireSingleStart = true;  // ровно один Start
        [SerializeField] private bool requireSingleExit = true;   // ровно один Exit
        [SerializeField] private bool treasureOptional = true;    // сокровище опционально

        [Header("Пул кусков")]
        [SerializeField] private List<PieceData> piecePool = new List<PieceData>();

        // Модификаторы весов: тег → множитель (сюда пишутся артефакты)
        private readonly Dictionary<PieceData.Tag, float> tagWeightMultipliers =
            new Dictionary<PieceData.Tag, float>();

        public int MinPieces => minPieces;
        public int MaxPieces => maxPieces;
        public IReadOnlyList<PieceData> PiecePool => piecePool;

        /// <summary>Установить множитель веса для тега (вызывается артефактами).</summary>
        public void SetTagMultiplier(PieceData.Tag tag, float multiplier)
            => tagWeightMultipliers[tag] = multiplier;

        /// <summary>Сбросить все модификаторы (новый забег).</summary>
        public void ClearTagMultipliers() => tagWeightMultipliers.Clear();

        /// <summary>Взвешенный случайный выбор куска с учётом теговых множителей.</summary>
        public PieceData PickWeighted(System.Random rng, PieceData.Tag? excludeTag = null, PieceData.Tag? excludeTag2 = null)
        {
            var candidates = new List<(PieceData piece, float w)>();
            var total = 0f;
            foreach (var p in piecePool)
            {
                if (p == null) continue;
                if (excludeTag.HasValue && p.PieceTagValue.Value == excludeTag.Value) continue;
                if (excludeTag2.HasValue && p.PieceTagValue.Value == excludeTag2.Value) continue;
                var mult = tagWeightMultipliers.TryGetValue(p.PieceTagValue.Value, out var m) ? m : 1f;
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
    }
}
