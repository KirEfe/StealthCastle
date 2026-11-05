namespace StealthCastle.Isometric3D.Core
{
    /// <summary>
    /// Фазы забега для 3D изометрической версии.
    /// </summary>
    public enum RunPhase3D
    {
        /// <summary>Стартовый экран</summary>
        StartScreen,
        
        /// <summary>Выбор артефакта</summary>
        ArtifactSelection,
        
        /// <summary>Сборка уровня</summary>
        Building,
        
        /// <summary>Игровой процесс</summary>
        Playing,
        
        /// <summary>Пауза</summary>
        Paused,
        
        /// <summary>Результаты забега</summary>
        Results,
        
        /// <summary>Переход между уровнями</summary>
        LevelTransition
    }
}