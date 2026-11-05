using UnityEngine;

namespace StealthCastle.Isometric3D.Core
{
    /// <summary>
    /// Данные артефакта для 3D изометрической версии.
    /// Берётся на верстаке хаба перед забегом: влияет на генерацию уровней, стражей и параметры игрока.
    /// Id артефакта — имя ассета (по нему он хранится в сохранении).
    /// </summary>
    [CreateAssetMenu(fileName = "ArtifactData3D", menuName = "StealthCastle/3D Artifact")]
    public class ArtifactData3D : ScriptableObject
    {
        [Header("Display Info")]
        [SerializeField] private string displayName = "Artifact";
        [SerializeField, TextArea] private string description = "Artifact description";
        [SerializeField] private Sprite icon;

        [Header("Hub")]
        [SerializeField, Min(0)] private int unlockCost = 0; // Цена на верстаке; 0 — открыт сразу

        [Header("Generation Effects")]
        [SerializeField] private World.PieceData3D.Tag affectedTag = World.PieceData3D.Tag.Combat;
        [SerializeField] private float weightMultiplier = 1f; // Множитель веса кусков с этим тегом; 1 — без эффекта

        [Header("Player Bonuses")]
        [SerializeField] private float disguiseDurationBonus = 0f; // Бонус к времени маскировки, с
        [SerializeField] private float scanRadiusBonus = 0f; // Бонус к радиусу сканирования, м
        [SerializeField] private float movementSpeedBonus = 0f; // Бонус к скорости шага, м/с

        [Header("Guards")]
        [Tooltip("Изменение дальности зрения стражей: -0.2 — видят на 20% ближе")]
        [SerializeField, Range(-0.9f, 1f)] private float guardVisionBonus = 0f;

        [Header("Rarity")]
        [SerializeField] private ArtifactRarity rarity = ArtifactRarity.Common;
        [SerializeField, Range(0f, 1f)] private float spawnChance = 1f;

        public enum ArtifactRarity { Common, Rare, Epic, Legendary }

        /// <summary>Имя артефакта</summary>
        public string DisplayName => displayName;

        /// <summary>Описание артефакта</summary>
        public string Description => description;

        /// <summary>Иконка артефакта</summary>
        public Sprite Icon => icon;

        /// <summary>Цена открытия на верстаке</summary>
        public int UnlockCost => unlockCost;

        /// <summary>Затрагиваемый тег</summary>
        public World.PieceData3D.Tag AffectedTag => affectedTag;

        /// <summary>Множитель веса</summary>
        public float WeightMultiplier => weightMultiplier;

        /// <summary>Бонус к времени маскировки</summary>
        public float DisguiseDurationBonus => disguiseDurationBonus;

        /// <summary>Бонус к радиусу сканирования</summary>
        public float ScanRadiusBonus => scanRadiusBonus;

        /// <summary>Бонус к скорости движения</summary>
        public float MovementSpeedBonus => movementSpeedBonus;

        /// <summary>Изменение дальности зрения стражей (доля)</summary>
        public float GuardVisionBonus => guardVisionBonus;

        /// <summary>Редкость</summary>
        public ArtifactRarity Rarity => rarity;

        /// <summary>Шанс появления</summary>
        public float SpawnChance => spawnChance;

        /// <summary>
        /// Применение эффекта к правилам генерации
        /// </summary>
        public void Apply(World.GenerationRules3D rules)
        {
            if (rules != null)
            {
                rules.SetTagMultiplier(affectedTag, weightMultiplier);
            }
        }

        /// <summary>
        /// Эффекты на уровни забега: веса кусков и зрение стражей
        /// </summary>
        public void ApplyToGenerator(World.LevelGenerator3D generator)
        {
            if (!Mathf.Approximately(weightMultiplier, 1f))
            {
                generator.SetTagMultiplier(affectedTag, weightMultiplier);
            }
            generator.GuardVisionMultiplier = 1f + guardVisionBonus;
        }

        /// <summary>
        /// Получение цвета редкости
        /// </summary>
        public Color GetRarityColor()
        {
            return rarity switch
            {
                ArtifactRarity.Common => new Color(0.5f, 0.5f, 0.5f), // Серый
                ArtifactRarity.Rare => new Color(0f, 0.5f, 1f),     // Синий
                ArtifactRarity.Epic => new Color(0.5f, 0f, 1f),     // Сиреневый
                ArtifactRarity.Legendary => new Color(1f, 0.5f, 0f), // Золотой
                _ => Color.white
            };
        }

        /// <summary>
        /// Получение множителя редкости для шанса появления
        /// </summary>
        public static float GetRarityMultiplier(ArtifactRarity rarity)
        {
            return rarity switch
            {
                ArtifactRarity.Common => 1f,
                ArtifactRarity.Rare => 0.7f,
                ArtifactRarity.Epic => 0.3f,
                ArtifactRarity.Legendary => 0.1f,
                _ => 1f
            };
        }
    }
}
