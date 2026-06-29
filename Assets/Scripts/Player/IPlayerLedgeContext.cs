using UnityEngine;

namespace StealthCastle.Player
{
    /// <summary>
    /// Контекст игрока для системы залезания — разрыв зависимости от PlayerController
    /// </summary>
    public interface IPlayerLedgeContext
    {
        bool IsGrounded { get; }
        bool IsCrouching { get; }
        bool IsDisguised { get; }
        Vector2 MoveInput { get; }
        LayerMask ObstacleLayer { get; }
        Rigidbody2D Rigidbody { get; }
        CapsuleCollider2D Collider { get; }
        Animator Animator { get; }
        float ColliderHeight { get; }
    }
}