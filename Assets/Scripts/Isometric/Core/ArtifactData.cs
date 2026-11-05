using UnityEngine;

namespace StealthCastle.Isometric.Core
{
    /// <summary>
    /// Артефакт: влияет на генерацию ТОЛЬКО через модификаторы весов GenerationRules.
    /// </summary>
    [CreateAssetMenu(fileName = "ArtifactData", menuName = "StealthCastle/Artifact")]
    public class ArtifactData : ScriptableObject
    {
        [Header("Веса")]
        [SerializeField] private string displayName = "Артефакт";
        [SerializeField, TextArea] private string description;
        [SerializeField] private World.PieceData.Tag affectedTag; // тег, чей вес меняется
        [SerializeField] private float weightMultiplier = 1f;     // множитель веса тега
        [SerializeField] private float enemySpeedBonus = 0f;      // бонус скорости врагов (заглушка MVP — не используется)

        [Header("Бонусы игрока (не через веса)")]
        [SerializeField] private float disguiseDurationBonus = 0f; // бонус к таймеру дыхания
        [SerializeField] private float scanRadiusBonus = 0f;       // бонус к радиусу скана

        public string DisplayName => displayName;
        public string Description => description;
        public PieceDataTag AffectedTag => new PieceDataTag(affectedTag);
        public float WeightMultiplier => weightMultiplier;
        public float EnemySpeedBonus => enemySpeedBonus;
        public float DisguiseDurationBonus => disguiseDurationBonus;
        public float ScanRadiusBonus => scanRadiusBonus;

        // Обёртка для сериализуемости (enum в property нельзя)
        public readonly struct PieceDataTag
        {
            public readonly World.PieceData.Tag Value;
            public PieceDataTag(World.PieceData.Tag v) => Value = v;
        }

        /// <summary>Применить эффект к правилам генерации.</summary>
        public void Apply(World.GenerationRules rules)
        {
            rules.SetTagMultiplier(affectedTag, weightMultiplier);
        }
    }
}
