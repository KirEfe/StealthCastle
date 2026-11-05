using UnityEngine;

namespace StealthCastle.Isometric.Stealth
{
    /// <summary>
    /// Контракт «цель для системы стелса» — используется врагами для проверки видимости.
    /// Адаптация StealthCastle.Stealth.IStealthTarget под изометрию.
    /// </summary>
    public interface IStealthTarget
    {
        bool IsDisguised { get; }
        Sprite CurrentDisguiseSprite { get; }
        bool CanBeDetected();
    }
}
