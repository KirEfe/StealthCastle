using UnityEngine;

namespace StealthCastle.Stealth
{
    public interface IStealthTarget
    {
        bool IsDisguised { get; }
        Sprite CurrentDisguiseSprite { get; }
        bool CanBeDetected();
    }
}
