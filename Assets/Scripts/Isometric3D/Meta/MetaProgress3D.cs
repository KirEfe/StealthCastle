using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace StealthCastle.Isometric3D.Meta
{
    /// <summary>
    /// Мета-прогресс между забегами: добыча в тайнике, открытые артефакты, уровни улучшений, статистика.
    /// Хранится в JSON в persistentDataPath.
    /// </summary>
    [Serializable]
    public class MetaProgress3D
    {
        [Serializable]
        public class UpgradeLevel
        {
            public string id;
            public int level;
        }

        public int loot;              // Добыча в тайнике — тратится у скупщика и на верстаке
        public int totalLootBanked;   // Всего принесено за всё время
        public int runs;
        public int escapes;           // Забеги, из которых вернулся сам
        public int deepestLevel;
        public int levelsCleared;
        public int chestsOpened;
        public string selectedArtifact = "";
        public List<string> unlockedArtifacts = new List<string>();
        public List<UpgradeLevel> upgrades = new List<UpgradeLevel>();

        public int GetUpgradeLevel(string id)
        {
            foreach (var upgrade in upgrades)
            {
                if (upgrade.id == id) return upgrade.level;
            }
            return 0;
        }

        public void SetUpgradeLevel(string id, int level)
        {
            foreach (var upgrade in upgrades)
            {
                if (upgrade.id == id)
                {
                    upgrade.level = level;
                    return;
                }
            }
            upgrades.Add(new UpgradeLevel { id = id, level = level });
        }

        /// <summary>Бесплатные артефакты открыты сразу, платные — после покупки на верстаке</summary>
        public bool IsUnlocked(Core.ArtifactData3D artifact)
        {
            return artifact != null && (artifact.UnlockCost <= 0 || unlockedArtifacts.Contains(artifact.name));
        }

        // --- Сохранение -------------------------------------------------------------------

        private static MetaProgress3D current;

        public static string FilePath => Path.Combine(Application.persistentDataPath, "meta3d.json");

        /// <summary>Прогресс игрока (читается с диска при первом обращении)</summary>
        public static MetaProgress3D Current => current ??= LoadFromDisk();

        public static void Save()
        {
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(Current, true));
            }
            catch (Exception e)
            {
                Debug.LogError($"[MetaProgress3D] Save failed: {e.Message}");
            }
        }

        /// <summary>Сброс прогресса (для отладки)</summary>
        public static void ResetProgress()
        {
            current = new MetaProgress3D();
            if (File.Exists(FilePath)) File.Delete(FilePath);
        }

        private static MetaProgress3D LoadFromDisk()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    return JsonUtility.FromJson<MetaProgress3D>(File.ReadAllText(FilePath)) ?? new MetaProgress3D();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[MetaProgress3D] Load failed, starting fresh: {e.Message}");
            }
            return new MetaProgress3D();
        }
    }
}
