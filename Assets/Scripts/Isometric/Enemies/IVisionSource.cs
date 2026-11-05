using UnityEngine;

namespace StealthCastle.Isometric.Enemies
{
    /// <summary>
    /// Контракт «источник взгляда» для EnemyVision.
    /// Зрение не зависит от IsoEnemyAI — только направление, дальность и угол.
    /// </summary>
    public interface IVisionSource
    {
        /// <summary>Куда смотрит враг прямо сейчас (мировое направление).</summary>
        Vector2 LookDirection { get; }

        /// <summary>Дальность зрения, м — влияет на длину зоны.</summary>
        float VisionRange { get; }

        /// <summary>Угол обзора, градусы — влияет на ширину зоны.</summary>
        float FovDegrees { get; }
    }
}
