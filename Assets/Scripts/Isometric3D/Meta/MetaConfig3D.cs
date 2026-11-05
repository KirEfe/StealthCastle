using System;
using System.Collections.Generic;
using UnityEngine;

namespace StealthCastle.Isometric3D.Meta
{
    /// <summary>
    /// Настройки мета-прогресса: артефакты верстака, улучшения скупщика, экономика добычи.
    /// Один ассет в папке Resources — его читают и хаб, и игровая сцена.
    /// </summary>
    [CreateAssetMenu(fileName = "MetaConfig3D", menuName = "StealthCastle/3D Meta Config")]
    public class MetaConfig3D : ScriptableObject
    {
        public const string ResourcePath = "MetaConfig3D";

        public enum UpgradeStat { Breath, ScanRadius, WalkSpeed, Pockets }

        /// <summary>Постоянное улучшение у скупщика: каждый уровень даёт perLevel к параметру</summary>
        [Serializable]
        public class Upgrade
        {
            public string id = "breath";
            public string displayName = "Deep Lungs";
            [TextArea] public string description = "+1 s of breath in disguise";
            public UpgradeStat stat = UpgradeStat.Breath;
            public float perLevel = 1f;
            [Tooltip("Цена каждого уровня; длина массива = максимальный уровень")]
            public int[] costs = { 30, 60, 100 };

            public int MaxLevel => costs != null ? costs.Length : 0;
        }

        [Header("Artifacts (workbench)")]
        [SerializeField] private List<Core.ArtifactData3D> artifacts = new List<Core.ArtifactData3D>();

        [Header("Upgrades (fence)")]
        [SerializeField] private List<Upgrade> upgrades = new List<Upgrade>();

        [Header("Loot")]
        [SerializeField] private int chestLootBase = 20;          // Сундук на первом уровне
        [SerializeField] private int chestLootPerLevel = 10;      // +за каждый следующий уровень забега
        [SerializeField] private int levelClearLootBase = 5;      // За дошедший до выхода уровень
        [SerializeField] private int levelClearLootPerLevel = 5;
        [SerializeField, Range(0f, 1f)] private float caughtLootKept = 0.25f; // Доля добычи, которую уносишь при поимке

        public IReadOnlyList<Core.ArtifactData3D> Artifacts => artifacts;
        public IReadOnlyList<Upgrade> Upgrades => upgrades;
        public float CaughtLootKept => caughtLootKept;

        public int ChestLoot(int level) => chestLootBase + chestLootPerLevel * Mathf.Max(0, level - 1);
        public int LevelClearLoot(int level) => levelClearLootBase + levelClearLootPerLevel * Mathf.Max(0, level - 1);

        /// <summary>Артефакт по id (имя ассета); пустой id — без артефакта</summary>
        public Core.ArtifactData3D FindArtifact(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var artifact in artifacts)
            {
                if (artifact != null && artifact.name == id) return artifact;
            }
            return null;
        }

        /// <summary>Суммарный бонус купленных улучшений к параметру</summary>
        public float UpgradeBonus(MetaProgress3D progress, UpgradeStat stat)
        {
            float bonus = 0f;
            foreach (var upgrade in upgrades)
            {
                if (upgrade.stat == stat) bonus += upgrade.perLevel * progress.GetUpgradeLevel(upgrade.id);
            }
            return bonus;
        }

        /// <summary>Доля добычи, которая остаётся при поимке (база + «Глубокие карманы»)</summary>
        public float KeptWhenCaught(MetaProgress3D progress)
        {
            return Mathf.Clamp01(caughtLootKept + UpgradeBonus(progress, UpgradeStat.Pockets));
        }

        private static MetaConfig3D cached;

        /// <summary>Ассет настроек из Resources (null, если его ещё не создали)</summary>
        public static MetaConfig3D Load()
        {
            if (cached == null) cached = Resources.Load<MetaConfig3D>(ResourcePath);
            return cached;
        }
    }
}
