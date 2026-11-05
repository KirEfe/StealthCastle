using UnityEngine;

namespace StealthCastle.Isometric.Core
{
    /// <summary>
    /// Состояния цикла роглайта (MVP без мета-прогрессии).
    /// </summary>
    public enum RunPhase
    {
        StartScreen,   // стартовый экран: выбор артефакта + «Начать забег»
        Building,      // сборка уровня на глазах игрока
        Playing,       // прохождение уровня
        Results        // экран итогов (выход/смерть)
    }
}
