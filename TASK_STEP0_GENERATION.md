# Задача Шаг 0 — Гарантия сборки уровня без софтлоков

> Для: программиста. Уровень детализации: максимальный («для тупых»).
> Предыдущий документ: `MVP_PLAN.md` (шаг 0 — блокер).
> Дата: 2026-08-24.

---

## 0. Суть проблемы (почему мы это делаем)

Текущий `LevelGenerator.cs` собирает уровень так:

```
для каждого куска: x += 12f; SpawnPiece(data, new Vector3(x, 0, 0), ...)
```

То есть куски выстраиваются **в линию слева направо** через фиксированный
шаг 12 метров. `ConnectionPoint` (проёмы) вообще **не используются**.

Это значит:
- Куски не стыкуются по дверям — проёмы могут упасть в стену.
- Игрок может оказаться заперт между стенами → **софтлок**.
- В прототипе это и было причиной «долгой добивки адекватной сборки».

**Цель Шага 0:** переписать генератор так, чтобы он:
1. Стыковал куски по реальным `ConnectionPoint` (матчинг N↔S, E↔W).
2. Гарантированно давал проходимый уровень (проверка BFS от Start до Exit).
3. При любом раскладе не создавал софтлоков (100/100 запусков).

---

## 1. Что уже готово (НЕ ТРОГАТЬ)

- `ConnectionPoint` (файл `ConnectionPoint.cs`) — структура уже правильная:
  - поля: `localPosition` (Vector2), `direction` (Dir: North/East/South/West),
    `openingType` (string).
  - метод `Opposite(Dir)` уже есть: N↔S, E↔W. Используем как есть.
- `PieceData` (файл `PieceData.cs`) — поля `prefab`, `sizeInTiles`,
  `pieceTag`, `weight`, `connections` (массив `ConnectionPoint`). Готово.
- `GenerationRules` (файл `GenerationRules.cs`) — `PickWeighted`,
  `SetTagMultiplier`, гарантии Start/Exit. Готово, не трогаем.
- `PieceDataHolder`, `IsoPiecePrefabFactory`, `RunManager` — работают, не трогаем.
- Логика `WireUpPiece` (подключение врагов/пропов) в `LevelGenerator` —
  **сохранить без изменений**, только вызывать из нового кода спавна.

---

## 2. Что НУЖНО сделать (пошагово)

### 2.1. Добавить в `ConnectionPoint` мировую позицию (метод, не поле)

В `ConnectionPoint.cs` добавить метод, который переводит локальную позицию
проёма в мировую относительно корня куска:

```csharp
// внутри struct ConnectionPoint
public Vector3 WorldPos(Transform pieceRoot)
    => pieceRoot.TransformPoint(new Vector3(localPosition.x, localPosition.y, 0f));
```

> Почему: префаб куска инстанцируется в мировых координатах, и проёмы
> должны считаться от его корня. `TransformPoint` учитывает позицию/поворот.

### 2.2. Переписать `LevelGenerator.BuildRoutine` → граф + матчинг

Удалить линейный цикл `x += spacing`. Заменить на процедуру ниже.

**Алгоритм (псевдокод):**

```
ФУНКЦИЯ GenerateLevel(rng, player, onCaught):
    попыток = 0
    ПОКА попыток < MAX_TRIES (напр. 50):
        попыток++
        pieces = []              // список (PieceData, worldPos, usedConnections)
        graph = []               // рёбра: (индекс куска A, индекс куска B)

        // 1. Стартовый кусок в (0,0,0)
        start = FindByTag(rules, Tag.Start)
        pieces.Add(start, pos=(0,0,0), used=[])

        // 2. Расти цепочку длиной rules.MinPieces..MaxPieces
        текущий = start (индекс 0)
        для i в 1..(длина-1):
            // найти свободный проём текущего куска
            freeConn = первый ConnectionPoint текущего, ещё не использованный
            если freeConn == null: break   // больше некуда расти

            // выбрать следующий кусок (исключаем Start; Exit — только в конце)
            если i == длина-1:
                nextData = FindByTag(rules, Tag.Exit)
            иначе:
                nextData = rules.PickWeighted(rng, excludeTag=Start, excludeTag2=Exit)

            // найти у nextData проём, противоположный freeConn.direction
            matchConn = первый ConnectionPoint nextData, где direction == Opposite(freeConn.direction)
            если matchConn == null: continue   // этот кусок не подходит, попробуем другой (или break)

            // ПОЗИЦИЯ nextData: так, чтобы matchConn совпал с freeConn
            // формула: worldPos_next = worldPos_current + (freeConn.WorldPos - matchConn.WorldPos_относительно_next)
            // проще: сдвиг = freeConn.localPosition - matchConn.localPosition (в локальных осях куска)
            //         но с учётом поворота — для MVP куски БЕЗ поворота (quaternion identity)
            delta = freeConn.localPosition - matchConn.localPosition
            worldPos_next = worldPos_current + new Vector3(delta.x, delta.y, 0)

            pieces.Add(nextData, worldPos_next, used=[matchConn])
            отметить freeConn текущего как used
            граф.Add(ребро: текущий -> nextData)
            текущий = nextData

        // 3. ПРОВЕРКА ПРОХОДИМОСТИ (BFS) — см. 2.3
        если BFS_Reachable(pieces, graph, startIndex=0, exitIndex):
            // валидно! спавним
            для каждого piece в pieces: SpawnPiece(piece.data, piece.worldPos, player, onCaught)
            проиграть анимацию сборки (как сейчас RevealRoutine)
            return   // УСПЕХ

    // 4. ФОЛБЭК: если за 50 попыток не собрали — гарантированно-валидная цепочка
    SpawnFallbackChain(rng, player, onCaught)   // см. 2.4
```

### 2.3. Проверка проходимости BFS

Кусок считается «проходимым в соседний», если между ними есть совпадающая
пара проёмов (уже построили в graph). BFS от стартового индекса:

```
ФУНКЦИЯ BFS_Reachable(pieces, graph, start, exit):
    visited = {start}
    queue = [start]
    ПОКА queue не пуст:
        node = queue.pop()
        если node == exit: return TRUE
        для neighbor в graph[node]:
            если neighbor не в visited:
                visited.Add(neighbor)
                queue.push(neighbor)
    return FALSE
```

> Это гарантирует: если BFS дошёл до Exit — игрок физически может пройти
> от старта до выхода по соединённым кускам. Софтлок невозможен.

### 2.4. Фолбэк-цепочка (гарантия 100%)

Если случайная сборка не дала валидный граф за 50 попыток (теоретически
возможно при плохих пулах), собираем «гарантированно проходимую» цепочку:

- Start → Empty → Empty → Exit (все с одним N-проёмом и одним S-проёмом).
- Фиксированные `ConnectionPoint` у этих кусков: North наверху, South внизу,
  `localPosition` = (0, ±halfHeight). Матчинг N↔S даст ровную линию без
  наложений.

> Это НЕ идеально по разнообразию, но гарантирует: игра никогда не выдаст
> непроходимый уровень. Игрок всегда может пройти.

---

## 3. Конкретные правила для `ConnectionPoint` префабов (инструкция сборщику)

Чтобы матчинг работал, ВСЕ префабы кусков должны соблюдать контракт:

1. Куски НЕ поворачиваются (инстанцируются с `Quaternion.identity`).
2. Каждый проём — `ConnectionPoint` с:
   - `localPosition`: в локальных координатах куска (центр = 0,0).
   - `direction`: куда выходит проём (N=вверх по Y, S=вниз, E=вправо, W=влево).
   - `openingType`: пока `"default"` (для будущих фильтров).
3. Проёмы на противоположных сторонах должны быть симметричны:
   - если N на `localPosition=(0, +H)`, то S на `(0, -H)`.
   - если E на `(+W, 0)`, то W на `(-W, 0)`.
4. Размер куска `sizeInTiles` должен соответствовать визуалу пола, чтобы
   соседние куски не накладывались (щель или стык = 0).

> Проверка: в Editor можно написать маленькую кнопку "Validate Connections",
> которая логирует, что у каждого куска проёмы симметричны. (опционально)

---

## 4. Что сохранить из старого кода (не сломать)

- `WireUpPiece(...)` — вызывать после `Instantiate` в новом `SpawnPiece`.
- `RevealRoutine(...)` — анимация сборки (появление снизу + растворение).
  Вызывать по очереди для каждого собранного куска.
- `Clear()` — очистка перед новой генерацией.
- `SetInputEnabled(false)` на время сборки, `true` после.

---

## 5. Приёмка (как проверить, что Шаг 0 закрыт)

1. Запустить `RunManager.BeginRun()` **100 раз подряд** (можно скриптом:
   цикл вызова `Generate` с разными seed, или просто жать Пробел на экране
   итогов 100 раз).
2. Для каждого запуска проверить вручную/автотестом:
   - ✅ Уровень собрался (нет NullReference на ExitPiece).
   - ✅ От стартового куска до выходного можно дойти (BFS вернул TRUE).
   - ✅ Куски не накладываются (проёмы совпадают, стены не пересекаются
     визуально).
3. Если хоть 1 из 100 — софтлок или наложение → Шаг 0 НЕ закрыт, чинить.

> Критерий готовности MVP №1: «100 запусков генерации → 0 софтлоков/наложений».

---

## 6. Чек-лист для программиста

- [ ] В `ConnectionPoint` добавлен `WorldPos(Transform)`.
- [ ] `LevelGenerator.BuildRoutine` переписан: граф + матчинг по `Opposite`.
- [ ] Позиция следующего куска считается по `freeConn.localPosition - matchConn.localPosition`.
- [ ] Добавлена BFS-проверка проходимости перед спавном.
- [ ] Добавлен фолбэк-спавн гарантированной цепочки.
- [ ] `WireUpPiece` и `RevealRoutine` вызываются из нового кода.
- [ ] `ConnectionPoint` всех 6 префабов симметричны (N↔S, E↔W).
- [ ] Прогнан тест 100 запусков → 0 софтлоков.

---

## 7. Частые ошибки (не делай так)

❌ НЕ оставляй `x += spacing` — это и есть баг.
❌ НЕ используй `FindObjectsByType` для поиска проёмов — они в `PieceData.connections`.
❌ НЕ поворачивай куски (`Quaternion`) — матчинг сломается (для MVP).
❌ НЕ спавни кусок, пока BFS не подтвердил проходимость.
❌ НЕ удаляй `WireUpPiece` — враги/пропсы перестанут подключаться.
