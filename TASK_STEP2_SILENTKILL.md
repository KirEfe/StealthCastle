# Задача Шаг 2 — Скрытное убийство (Мимик-амбуш)

> Для: программиста. Уровень: максимальный.
> Связь: `MVP_PLAN.md` шаг 2 (главная фиша). Готово из прототипа:
> `IsoDisguiseSystem.cs`, `IsoEnemyAI.cs` (FSM), `IStealthTarget.cs`.
> Дата: 2026-08-24.

---

## 0. Суть

Маскировка была только «прятками». Теперь она **наступательная**: игрок
может напасть и тихо убить врага ИЗ маскировки или со спины. Это ДНК игры.

**Решение (Вариант А, утверждён):** убийство проходит ТОЛЬКО если враг
**НЕ смотрит** на игрока в момент удара. Если враг смотрит — атака не
проходит, враг переходит в Chase (маскировка раскрыта).

Открытого боя НЕТ — только скрытные убийства.

---

## 1. Что уже есть (НЕ трогать)
- `IsoDisguiseSystem` — фазы Idle/Scanning/Disguised, слой `DisguisedPlayer`,
  `CanBeDetected()` (false в маскировке), `IsMoving`.
- `IsoEnemyAI` — FSM Patrol/Investigate/Chase/Search, метод `CanSeePlayer()`
  (raycast + FOV + wallMask), `OnPlayerCaught`.
- `IStealthTarget` — контракт `IsDisguised`, `CanBeDetected()`.

---

## 2. Что добавить

### 2.1. Новый файл `IsoSilentKill.cs`
Компонент на игроке (рядом с `IsoDisguiseSystem`). Логика:

```
[RequireComponent(typeof(IsoDisguiseSystem))]
[RequireComponent(typeof(IsoPlayerController))]
public class IsoSilentKill : MonoBehaviour
{
    [SerializeField] private float killRange = 1.8f;   // дистанция удара
    [SerializeField] private InputAction killAction;    // кнопка атаки (напр. ЛКМ / E)

    private IsoDisguiseSystem disguise;
    private IsoPlayerController player;

    private void Awake() { disguise = GetComponent<...>(); player = GetComponent<...>(); }

    private void Update()
    {
        if (killAction == null || !killAction.IsPressed()) return;
        TryKill();
    }

    private void TryKill()
    {
        // Найти ближайшего врага в радиусе
        var enemy = FindNearestEnemyInRange(killRange);
        if (enemy == null) return;

        // УСЛОВИЕ ВАРИАНТА А: враг НЕ видит игрока прямо сейчас
        bool enemyLooksAway = !enemy.CanSeePlayerNow();   // см. 2.2

        if (enemyLooksAway)
        {
            // ТИХОЕ УБИЙСТВО
            enemy.SilentKill();                  // см. 2.3
            disguise.RemoveDisguise("Амбуш: враг устранён");  // выходим из маски
            // опционально: частичный расход таймера дыхания
        }
        else
        {
            // Враг смотрит — атака провалилась, маскировка раскрыта
            enemy.TriggerAlert();                // → Chase
            disguise.RemoveDisguise("Амбуш раскрыт: враг смотрел");
        }
    }
}
```

### 2.2. Добавить в `IsoEnemyAI` публичный метод
```csharp
// в IsoEnemyAI.cs
public bool CanSeePlayerNow()
{
    if (player == null) return false;
    if (playerDisguise != null && playerDisguise.IsDisguised && !playerDisguise.IsMoving)
        return false;  // неподвижный замаскированный невидим
    return CanSeePlayer();   // существующий метод (FOV + raycast)
}
```

### 2.3. Добавить в `IsoEnemyAI` метод тихого убийства
```csharp
// в IsoEnemyAI.cs
public void SilentKill()
{
    // остановить FSM, убрать врага со сцены
    state = State.Search;     // или новое State.Dead
    rb.linearVelocity = Vector2.zero;
    OnPlayerCaught = null;    // не триггерить смерть игрока
    gameObject.SetActive(false);   // или пул/анимация смерти
    // TODO: анимация "враг упал" (позже)
}
```

> Важно: `SilentKill()` НЕ вызывает `OnPlayerCaught` — иначе игрок умрёт
> вместе с врагом. Это баг №1, не допусти.

### 2.4. HUD-подсказка (опционально, но желательно)
В `IsoSilentKill.Update` при валидной цели (враг в радиусе + смотрит в сторону)
подсвечивать иконку «можно убить» (через `BreathTimer` UI или отдельный
`KillPrompt`). Помогает игроку понять механику.

---

## 3. Что НЕ ломать
- `IsoDisguiseSystem.RemoveDisguise` — вызываем, но не меняем его логику.
- `IsoEnemyAI.CanSeePlayer` — используем как есть.
- `OnPlayerCaught` цепочку в `RunManager` — НЕ трогаем (смерть игрока работает).

---

## 4. Приёмка
- [ ] Игрок в маскировке рядом с врагом, враг НЕ смотрит → атака убивает врага,
      игрок выходит из маски, забег продолжается.
- [ ] Игрок атакует врага, который СМОТРИТ на него → атака не проходит,
      враг → Chase, маскировка снята.
- [ ] Убийство НЕ триггерит `OnPlayerCaught` (игрок не умирает).
- [ ] Тихое убийство со спины (вне конуса) тоже работает (CanSeePlayerNow=false).
- [ ] Открытого боя нет: вне маскировки атака просто не находит цель (или тоже
      работает со спины — решить в балансе, но НЕ давать убивать в лоб на виду).

---

## 5. Частые ошибки
❌ НЕ вызывай `OnPlayerCaught` внутри `SilentKill` — игрок умрёт.
❌ НЕ делай убийство без проверки `CanSeePlayerNow` — сломаешь Вариант А.
❌ НЕ трогай `CanSeePlayer` (он используется в Chase) — делай обёртку
    `CanSeePlayerNow`.
❌ НЕ забудь `killRange` — иначе игрок убьёт врага через стену.
