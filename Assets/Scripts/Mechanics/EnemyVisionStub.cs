using StealthCastle.Stealth;
using UnityEngine;

namespace StealthCastle.Mechanics
{
    public class EnemyVisionStub : MonoBehaviour
    {
        [SerializeField] float checkInterval = 1f;

        IStealthTarget stealthTarget;
        float timer;

        void Awake()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                stealthTarget = player.GetComponent<DisguiseSystem>();

            if (stealthTarget == null)
                Debug.LogWarning($"{nameof(EnemyVisionStub)}: не найден игрок с {nameof(DisguiseSystem)}.", this);
        }

        void Update()
        {
            if (stealthTarget == null)
                return;

            timer += Time.deltaTime;
            if (timer < checkInterval)
                return;

            timer = 0f;

            if (stealthTarget.CanBeDetected())
                Debug.Log("[EnemyVision] Враг видит игрока.");
            else
                Debug.Log("[EnemyVision] Игрок замаскирован — не обнаружен.");
        }
    }
}
