# Задача Шаг 4 — Артефакты v1

> Для: программиста. Уровень: максимальный.
> Связь: `MVP_PLAN.md` шаг 4. Готово: `ArtifactData.cs`, `GenerationRules.cs`
> (SetTagMultiplier УЖЕ реализован), `ArtifactSelector.cs`.
> Дата: 2026-08-24.

---

## 0. Суть

Артефакты — фишка игры: влияют на генерацию уровня, и эффект виден ДО входа
(цвет пола по тегу в `LevelGenerator.FloorColor`). Нужно сделать 2-3
артефакта MVP, доказывающих критерий готовности №4: «артефакт меняет
генерацию видимо до входа».

`GenerationRules.SetTagMultiplier(tag, mult)` УЖЕ готов — артефакты встанут
БЕЗ переделки генератора. Это ключевое решение из GDD.

---

## 1. Что уже есть (НЕ трогать)
- `ArtifactData` — поля `displayName`, `description`, `affectedTag`,
  `weightMultiplier`, `enemySpeedBonus`. Метод `Apply(rules)` →
  `rules.SetTagMultiplier(affectedTag, weightMultiplier)`.
- `GenerationRules.SetTagMultiplier` — меняет вес тега для `PickWeighted`.
- `ArtifactSelector` — выбор на стартовом экране, вызывает `ApplySelected()`.

---

## 2. Что сделать

### 2.1. Создать 3 ассета артефактов (в Project, меню StealthCastle/Artifact)
Через `ArtifactData` (ScriptableObject):

| Артефакт | affectedTag | weightMultiplier | enemySpeedBonus | Описание |
|---|---|---|---|---|
| «Жадный взгляд» | Treasure | 3.0 | 0 | Больше комнат с сокровищами |
| «Тихий путь» | Combat | 0.5 | 0 | Меньше боевых кусков (врагов) |
| «Острый клинок» | Empty | 1.0 | 0 | +скан/дыхание (см. 2.2) |

> `enemySpeedBonus` оставляем 0 — это заглушка (решено в MVP: артефакт №2
> только ×0.5 Combat, без ускорения врагов). Поле в коде есть, но НЕ
> используем.

### 2.2. Бонус «Острый клинок» (не через веса)
Артефакт №3 должен давать игроку бонус к маскировке. `ArtifactData.Apply`
только меняет веса — нужно расширить:

```
// в ArtifactData.cs добавить:
[SerializeField] private float disguiseDurationBonus = 0f;  // бонус к таймеру дыхания
[SerializeField] private float scanRadiusBonus = 0f;       // бонус к радиусу скана
public float DisguiseDurationBonus => disguiseDurationBonus;
public float ScanRadiusBonus => scanRadiusBonus;

// в Apply(rules) НЕ ТРОГАТЬ (только веса). Бонус применяет RunManager/Player:
```

В `RunManager.BeginRun()` ПОСЛЕ `artifactSelector.ApplySelected()` добавить:
```
playerDisguise.disguiseDuration += artifact.DisguiseDurationBonus;  // через поле или метод
playerDisguise.scanRadius += artifact.ScanRadiusBonus;
```
> Или через отдельный `PlayerUpgrades` — на усмотрение, но бонус ДОЛЖЕН
> применяться до начала уровня.

### 2.3. Видимость эффекта ДО входа
Уже работает: `LevelGenerator.FloorColor` красит пола по тегу. Если
Treasure ×3 — на сборке видно больше жёлтых кусков. Если Combat ×0.5 —
меньше красных. Это и есть критерий №4.

Дополнительно (опционально): в `ArtifactSelector` показывать описание
артефакта и примерный эффект («Больше жёлтых комнат»).

---

## 3. Что НЕ ломать
- `GenerationRules.SetTagMultiplier` — ядро, не переписывать.
- `ArtifactData.Apply` — только веса. Бонусы маскировки — отдельно (2.2).
- `ConnectionPoint` / генератор Шага 0 — не трогать.

---

## 4. Приёмка
- [ ] 3 ассета артефактов созданы в Project.
- [ ] «Жадный взгляд» → на сборке заметно больше жёлтых (Treasure) кусков.
- [ ] «Тихий путь» → заметно меньше красных (Combat) кусков.
- [ ] «Острый клинок» → таймер дыхания/скан больше (проверить в игре).
- [ ] `enemySpeedBonus` НЕ используется (заглушка соблюдена).
- [ ] Эффект виден ДО входа на уровень (критерий №4).

---

## 5. Частые ошибки
❌ НЕ используй `enemySpeedBonus` — он в заглушке, договорённость MVP.
❌ НЕ меняй `GenerationRules` — работает через `SetTagMultiplier`.
❌ НЕ применяй бонус маскировки ПОСЛЕ старта уровня — только в `BeginRun`.
❌ НЕ хардкодь эффект в генераторе — артефакт через веса (контракт GDD).
