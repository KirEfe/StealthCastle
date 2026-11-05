namespace StealthCastle.Isometric3D.Meta
{
    /// <summary>
    /// Текущий забег: взятый артефакт и добыча при себе. Добыча попадает в тайник только по возвращении в хаб;
    /// при поимке уносишь лишь часть.
    /// </summary>
    public class RunState3D
    {
        public Core.ArtifactData3D Artifact { get; }
        public int CarriedLoot;
        public int LevelsCleared;
        public int ChestsOpened;

        public RunState3D(Core.ArtifactData3D artifact)
        {
            Artifact = artifact;
        }
    }

    /// <summary>
    /// Итог забега для хаба: показывается один раз при возвращении
    /// </summary>
    public class RunReport3D
    {
        public bool Escaped;
        public int Level;          // Уровень, на котором забег закончился
        public int LevelsCleared;
        public int Carried;        // Было при себе
        public int Kept;           // Попало в тайник
        public int Chests;

        /// <summary>Отчёт, который ждёт показа в хабе (переживает смену сцены)</summary>
        public static RunReport3D Pending;
    }
}
