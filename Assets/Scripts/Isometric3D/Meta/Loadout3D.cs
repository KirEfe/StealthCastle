using UnityEngine;

namespace StealthCastle.Isometric3D.Meta
{
    /// <summary>
    /// Снаряжение на забег: улучшения скупщика + выбранный артефакт → параметры игрока и генератора.
    /// Применяется один раз в начале забега (игрок и генератор живут до конца сцены).
    /// </summary>
    public static class Loadout3D
    {
        public struct Stats
        {
            public float Breath;
            public float ScanRadius;
            public float WalkSpeed;
            public float KeptWhenCaught;
        }

        /// <summary>Итоговые параметры от базовых значений — их же показывает хаб</summary>
        public static Stats Compute(float baseBreath, float baseScan, float baseWalk,
            Core.ArtifactData3D artifact, MetaProgress3D progress, MetaConfig3D config)
        {
            var stats = new Stats { Breath = baseBreath, ScanRadius = baseScan, WalkSpeed = baseWalk };
            if (config != null)
            {
                stats.Breath += config.UpgradeBonus(progress, MetaConfig3D.UpgradeStat.Breath);
                stats.ScanRadius += config.UpgradeBonus(progress, MetaConfig3D.UpgradeStat.ScanRadius);
                stats.WalkSpeed += config.UpgradeBonus(progress, MetaConfig3D.UpgradeStat.WalkSpeed);
                stats.KeptWhenCaught = config.KeptWhenCaught(progress);
            }
            if (artifact != null)
            {
                stats.Breath += artifact.DisguiseDurationBonus;
                stats.ScanRadius += artifact.ScanRadiusBonus;
                stats.WalkSpeed += artifact.MovementSpeedBonus;
            }
            return stats;
        }

        public static void Apply(Core.ArtifactData3D artifact, MetaProgress3D progress, MetaConfig3D config,
            Player.PlayerController3D player, World.LevelGenerator3D generator)
        {
            if (player != null)
            {
                var disguise = player.GetComponent<Mechanics.DisguiseSystem3D>();
                var stats = Compute(disguise != null ? disguise.TotalTime : 0f, disguise != null ? disguise.ScanRadius : 0f,
                    player.WalkSpeed, artifact, progress, config);

                player.SetMoveSpeed(stats.WalkSpeed);
                if (disguise != null)
                {
                    disguise.SetDisguiseDuration(stats.Breath);
                    disguise.SetScanRadius(stats.ScanRadius);
                }
                Debug.Log($"[Loadout3D] {(artifact != null ? artifact.DisplayName : "no artifact")}: breath {stats.Breath:0.0} s, " +
                          $"scan {stats.ScanRadius:0.0} m, walk {stats.WalkSpeed:0.0} m/s");
            }

            if (generator != null)
            {
                generator.ClearModifiers();
                if (artifact != null)
                {
                    artifact.ApplyToGenerator(generator);
                }
            }
        }
    }
}
