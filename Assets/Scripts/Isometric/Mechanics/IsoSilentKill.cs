using StealthCastle.Isometric.Enemies;
using StealthCastle.Isometric.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StealthCastle.Isometric.Mechanics
{
    /// <summary>
    /// Мимик-амбуш (Шаг 2). Наступательная маскировка: игрок атакует врага
    /// из маскировки или со спины. Убийство проходит ТОЛЬКО если враг
    /// НЕ видит игрока в момент удара (Вариант А). Иначе — раскрытие.
    /// </summary>
    [RequireComponent(typeof(IsoDisguiseSystem))]
    [RequireComponent(typeof(IsoPlayerController))]
    public class IsoSilentKill : MonoBehaviour
    {
        [Header("Настройки")]
        [SerializeField] private float killRange = 1.8f; // дистанция удара

        private IsoDisguiseSystem disguise;
        private IsoPlayerController player;
        private InputSystem_Actions inputActions;
        private InputAction attackAction;
        private bool killHandled; // одно убийство за нажатие (защита от спама в Update)

        private void Awake()
        {
            disguise = GetComponent<IsoDisguiseSystem>();
            player = GetComponent<IsoPlayerController>();

            inputActions = new InputSystem_Actions();
            attackAction = inputActions.Player.Attack;
            attackAction.Enable();
        }

        private void OnDestroy()
        {
            inputActions?.Dispose();
        }

        private void Update()
        {
            if (attackAction == null) return;

            // Атака только во время забега — иначе можно убить врагов после смерти/победы
            if (Core.RunManager.Instance == null || Core.RunManager.Instance.Phase != Core.RunPhase.Playing) return;

            // Сброс флага, когда кнопка отпущена — можно снова атаковать
            if (killHandled && !attackAction.IsPressed())
            {
                killHandled = false;
                return;
            }

            if (!attackAction.IsPressed()) return;
            if (killHandled) return;

            TryKill();
            killHandled = true;
        }

        private void TryKill()
        {
            var enemy = FindNearestEnemyInRange(killRange);
            if (enemy == null) return;

            // Вариант А: убийство только если враг НЕ смотрит на игрока
            // (покрывает маскировку И удар со спины/вне конуса)
            if (!enemy.CanSeePlayerNow())
            {
                enemy.SilentKill();
                disguise.RemoveDisguise("Амбуш: враг устранён");
            }
            else
            {
                // Враг смотрит — атака провалена, маскировка раскрыта
                enemy.TriggerAlert();
                disguise.RemoveDisguise("Амбуш раскрыт: враг смотрел");
            }
        }

        private IsoEnemyAI FindNearestEnemyInRange(float range)
        {
            var enemies = FindObjectsByType<IsoEnemyAI>(FindObjectsSortMode.None);
            IsoEnemyAI best = null;
            var bestDist = range;
            foreach (var e in enemies)
            {
                if (!e.gameObject.activeInHierarchy) continue;
                var d = Vector2.Distance(transform.position, e.transform.position);
                if (d <= bestDist) { bestDist = d; best = e; }
            }
            return best;
        }
    }
}
