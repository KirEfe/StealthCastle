using UnityEngine;

namespace StealthCastle.Isometric3D.Core
{
    /// <summary>
    /// Баланс в одном месте: параметры стражей и игрока + кривая сложности по уровням забега.
    /// Ассет лежит в Resources; в игре работает его копия (Active), которую правит панель F1 —
    /// так эксперименты не портят ассет, пока их явно не сохранили.
    /// </summary>
    [CreateAssetMenu(fileName = "BalanceProfile3D", menuName = "StealthCastle/3D Balance Profile")]
    public class BalanceProfile3D : ScriptableObject
    {
        public const string ResourcePath = "BalanceProfile3D";

        [Header("Guards (override Guard.prefab)")]
        public bool overrideGuards = true;
        public float visionRange = 6f;
        [Range(10f, 120f)] public float fovDegrees = 70f;
        public float patrolSpeed = 2f;
        public float chaseSpeed = 5.5f;
        public float waitAtWaypoint = 2f;
        public float lookBackDuration = 1f;
        public float turnSpeed = 200f;
        public float loseSightTime = 2f;
        public float searchDuration = 4f;
        public float crawlDetectDistance = 2f;

        [Header("Player")]
        public float walkSpeed = 5f;
        public float runSpeed = 8f;
        public float crawlSpeed = 1.5f;
        public float breath = 6f;
        public float scanRadius = 2f;

        [Header("Difficulty curve: level length")]
        [Tooltip("Кусков на первом уровне (вместе со стартом и выходом)")]
        public int piecesFirstLevel = 4;
        [Tooltip("Прибавка кусков за уровень")]
        public float piecesPerLevel = 0.5f;
        public int maxPieces = 9;
        [Tooltip("Случайный разброс длины: ± кусков")]
        public int piecesSpread = 1;

        [Header("Difficulty curve: piece choice")]
        [Tooltip("Целевая сложность кусков (0..1, из Piece Lab) по номеру уровня")]
        public AnimationCurve targetDifficulty = new AnimationCurve(new Keyframe(1f, 0.2f), new Keyframe(6f, 0.75f));
        [Tooltip("Насколько строго держать цель: меньше — строже")]
        public float difficultyFocus = 0.3f;

        [Header("Difficulty curve: guards")]
        [Tooltip("Прибавка к дальности зрения за уровень (доля)")]
        public float visionGrowthPerLevel = 0.04f;
        [Tooltip("Прибавка к скоростям стражей за уровень (доля)")]
        public float speedGrowthPerLevel = 0.03f;
        [Tooltip("После этого уровня стражи больше не усиливаются")]
        public int growthCapLevel = 8;

        /// <summary>Длина уровня (кусков) для номера уровня</summary>
        public int PiecesFor(int level, System.Random rng)
        {
            int baseCount = Mathf.RoundToInt(piecesFirstLevel + piecesPerLevel * (level - 1));
            int count = baseCount + (piecesSpread > 0 ? rng.Next(-piecesSpread, piecesSpread + 1) : 0);
            return Mathf.Clamp(count, 3, Mathf.Max(3, maxPieces));
        }

        /// <summary>Множитель веса куска с измеренной сложностью: колокол вокруг цели уровня</summary>
        public float DifficultyWeight(float pieceDifficulty, int level)
        {
            if (pieceDifficulty < 0f) return 1f; // Кусок ещё не измерен в Piece Lab
            float target = targetDifficulty.Evaluate(level);
            float d = (pieceDifficulty - target) / Mathf.Max(0.05f, difficultyFocus);
            return 0.05f + Mathf.Exp(-d * d);
        }

        public float VisionScale(int level) => 1f + visionGrowthPerLevel * Mathf.Clamp(level - 1, 0, growthCapLevel);
        public float SpeedScale(int level) => 1f + speedGrowthPerLevel * Mathf.Clamp(level - 1, 0, growthCapLevel);

        // --- Активный профиль ---------------------------------------------------------------

        private static BalanceProfile3D active;
        private static BalanceProfile3D source;

        /// <summary>Профиль текущей игры: копия ассета из Resources (или значения по умолчанию)</summary>
        public static BalanceProfile3D Active
        {
            get
            {
                if (active == null)
                {
                    source = Resources.Load<BalanceProfile3D>(ResourcePath);
                    active = source != null ? Instantiate(source) : CreateInstance<BalanceProfile3D>();
                    active.name = source != null ? source.name + " (live)" : "Default (live)";
                }
                return active;
            }
        }

        /// <summary>Ассет, из которого сделана копия (для сохранения правок панели)</summary>
        public static BalanceProfile3D Source => source;

        /// <summary>Отбросить правки: снова скопировать ассет</summary>
        public static void ResetActive()
        {
            if (active != null) Destroy(active);
            active = null;
        }
    }

    /// <summary>Отладочные флаги для тестов (панель F1)</summary>
    public static class DebugFlags3D
    {
        /// <summary>Стражи замечают и гонятся, но не ловят</summary>
        public static bool GodMode;
    }
}
