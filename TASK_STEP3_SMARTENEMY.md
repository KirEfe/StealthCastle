# Задача Шаг 3 — Умные враги (база FSM)

> Для: программиста. Уровень: максимальный.
> Связь: `MVP_PLAN.md` шаг 3. База FSM УЖЕ есть в `IsoEnemyAI.cs`
> (Patrol/Investigate/Chase/Search + CanSeePlayer + crawlDetectDistance).
> Дата: 2026-08-24.

---

## 0. Суть

База FSM работает, но враг тупой: не проверяет подозрительные предметы,
не смотрит назад. Без этого Мимик-амбуш (Шаг 2) не имеет тактической
глубины — игрок просто ждёт, пока враг отвернётся, и бьёт. Нужно добавить
«интеллект», который делает амбуш рискованным и интересным.

---

## 1. Что добавить в `IsoEnemyAI.cs`

### 1.1. Подозрение к пропам (пинок)
Когда игрок в маскировке ДВИГАЛСЯ рядом с врагом (уже есть `crawlDetectDistance`),
враг должен подойти и «пнуть» проп:

```
[Header("Подозрение")]
[SerializeField] private float suspicionRange = 2.5f;   // радиус пинка пропа
[SerializeField] private float propCheckCooldown = 3f;   // не пинать чаще раза в 3 сек

private float propCheckTimer;

// в UpdatePatrol / UpdateInvestigate, если рядом замаскированный ДВИГАВШИЙСЯ игрок:
private void MaybeInspectProp()
{
    if (playerDisguise == null || !playerDisguise.IsDisguised) return;
    if (!playerDisguise.IsMoving) return;   // неподвижный — не проверяем (честно)
    if (propCheckTimer > 0) return;
    var dist = Vector2.Distance(transform.position, player.position);
    if (dist > suspicionRange) return;

    // Подходим к пропу и "пинаем"
    propCheckTimer = propCheckCooldown;
    EnterInvestigate(player.position);   // идём проверить
    // TODO: если дошёл и игрок всё ещё замаскирован и не двигается — возвращаемся
    //       (в MVP: просто подходим, этого достаточно для угрозы амбуша)
}
```

> Эффект: если игрок пополз к врагу в маскировке, враг пойдёт проверять —
> амбуш стал опаснее. Честно: неподвижный проп не проверяется.

### 1.2. Обзор назад на повороте
На смене waypoint враг на ~1 сек смотрит назад (разворот `lastMoveDirection`):

```
[SerializeField] private float lookBackDuration = 1f;

// в UpdatePatrol при достижении waypoint (до смены currentWaypoint):
private void LookBackAtWaypoint()
{
    // разворачиваемся на 180° на короткий таймер
    var backDir = -lastMoveDirection;
    // визуально: VisionZone смотрит назад
    if (visionZone != null) visionZone.SetFacing(backDir.x >= 0f);
    // логика CanSeePlayer уже использует lastMoveDirection — временно инвертируем
    lookBackTimer = lookBackDuration;
}

// в Update(): если lookBackTimer > 0, CanSeePlayer считается с инвертированным направлением
```

> Реализация: добавить `lookBackTimer`, в `CanSeePlayer` использовать
> `lookBackTimer > 0 ? -lastMoveDirection : lastMoveDirection` как forward.

### 1.3. Search умнее
В `UpdateSearch` враг не просто стоит — он осматривает ближайшие пропы:
```
private void UpdateSearch()
{
    // медленно поворачиваемся, "оглядываясь" (меняем lastMoveDirection по кругу)
    // ...существующий таймер searchTimer...
}
```
Для MVP достаточно: враг в Search крутится на месте (меняет `lastMoveDirection`),
имитируя осмотр. Это видно игроку и создаёт напряжение.

---

## 2. Честность (обязательно)
- Конус зрения (`VisionZone`) визуализирован — игрок всегда видит, куда враг смотрит.
- Если враг «пинает» проп — это видно (анимация/звук позже, пока логика).
- Игрок понимает, почему заметили: конус коснулся + не в маскировке ИЛИ
  ползком вблизи.

---

## 3. Что НЕ ломать
- `CanSeePlayer` — ядро обнаружения, дополняем инверсией для обзора назад.
- `OnPlayerCaught` — смерть игрока.
- FSM-переходы Patrol/Investigate/Chase/Search — расширяем, не переписываем.

---

## 4. Приёмка
- [ ] Враг подходит и "проверяет" проп, если игрок полз к нему в маскировке.
- [ ] Враг смотрит назад на повороте waypoint (видно по конусу).
- [ ] Враг в Search осматривается (крутится), а не стоит столбом.
- [ ] Неподвижный замаскированный игрок НЕ проверяется (честно).
- [ ] Амбуш (Шаг 2) стал рискованнее: нельзя просто ждать — враг может
      обернуться или подойти.

---

## 5. Частые ошибки
❌ НЕ проверяй неподвижный проп — сломаешь честность маскировки.
❌ НЕ трогай `CanSeePlayer` напрямую для обзора назад — делай обёртку с
    `lookBackTimer`, иначе Chase сломается.
❌ НЕ делай врага "всевидящим" — только конус + близость ползком.
