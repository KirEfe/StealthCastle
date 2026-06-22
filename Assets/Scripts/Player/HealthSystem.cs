using UnityEngine;
using System;
using StealthCastle.Core;

namespace StealthCastle.Player
{
    public class HealthSystem : MonoBehaviour
    {
        [SerializeField] private PlayerStatsSO stats;
        
        private float currentHealth;

        // События для подключения UI или других систем
        public event Action<float, float> OnDamaged; // (currentHealth, maxHealth)
        public event Action<float, float> OnHealed;  // (currentHealth, maxHealth)
        public event Action OnDeath;

        public bool IsDead => currentHealth <= 0;

        private void Awake()
        {
            if (stats != null)
            {
                currentHealth = stats.maxHealth;
            }
            else
            {
                Debug.LogError($"[HealthSystem] Ссылка на PlayerStatsSO не назначена на объекте {gameObject.name}!");
            }
        }

        public void TakeDamage(float amount)
        {
            if (IsDead) return;

            float damage = Mathf.Max(0, amount);
            currentHealth -= damage;
            currentHealth = Mathf.Max(0, currentHealth);

            Debug.Log($"[HealthSystem] Получен урон: {damage}. Текущее здоровье: {currentHealth}/{stats.maxHealth}");

            OnDamaged?.Invoke(currentHealth, stats != null ? stats.maxHealth : 0);

            if (IsDead)
            {
                Die();
            }
        }

        public void Heal(float amount)
        {
            if (IsDead) return;

            float healAmount = Mathf.Max(0, amount);
            currentHealth += healAmount;

            if (stats != null)
            {
                currentHealth = Mathf.Min(currentHealth, stats.maxHealth);
            }

            Debug.Log($"[HealthSystem] Исцеление: {healAmount}. Текущее здоровье: {currentHealth}/{stats.maxHealth}");

            OnHealed?.Invoke(currentHealth, stats != null ? stats.maxHealth : 0);
        }

        private void Die()
        {
            Debug.Log("Герой погиб");
            OnDeath?.Invoke();
        }
    }
}