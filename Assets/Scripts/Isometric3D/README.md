# StealthCastle 3D Isometric - Модуль 3D изометрической версии

## 📋 Обзор

Этот модуль представляет собой полноценную 3D изометрическую версию игры StealthCastle. Он объединяет лучшие механики из оригинальной 2D версии и существующего изометрического прототипа.

## 🏗️ Структура

```
Isometric3D/
├── Core/              # Ядровые системы
│   ├── GameManager3D.cs    # Основной менеджер игры
│   ├── CameraSystem3D.cs   # Система камеры с изометрическим видом
│   ├── InputSystem3D.cs    # Унифицированная система ввода
│   ├── RunPhase3D.cs       # Управление фазами уровня
│   ├── ArtifactData3D.cs   # Данные артефактов
│   └── Bootstrap3D.cs      # Автоматическая инициализация систем
│
├── Player/             # Игрок
│   ├── PlayerController3D.cs      # Контроллер игрока
│   └── PlayerDisguiseVisual3D.cs # Визуальная маскировка
│
├── Enemies/            # Враги
│   └── EnemyAI3D.cs              # ИИ врагов
│
├── Mechanics/          # Механики
│   ├── DisguiseSystem3D.cs       # Система маскировки
│   ├── DisguisableProp3D.cs      # Объекты для маскировки
│   └── NoiseEmitter3D.cs         # Излучатель шума
│
├── World/              # Мир
│   ├── LevelGenerator3D.cs      # Генератор уровней
│   ├── PieceData3D.cs           # Данные кусков уровня
│   ├── ConnectionPoint3D.cs     # Точки соединения кусков
│   ├── GenerationRules3D.cs     # Правила генерации
│   ├── PieceDataHolder3D.cs    # Хранитель данных куска
│   └── LevelExit3D.cs           # Точка выхода
│
├── UI/                 # Интерфейс
│   └── HUDController.cs         # Контроллер HUD
│
├── Utils/              # Утилиты
│   ├── MathUtils.cs            # Математические функции
│   ├── PhysicsUtils3D.cs      # Утилиты физики
│   └── PoolSystem.cs           # Система пулинга объектов
│
└── Setup/              # Настройка сцены
    └── SceneSetup3D.cs          # Автоматическая настройка сцены
```

## 🚀 Быстрый старт

### 1. Создание новой сцены

1. Создайте новую сцену в Unity (`File > New Scene`)
2. Добавьте пустой GameObject и называем его `SceneSetup3D`
3. Добавьте компонент `SceneSetup3D` к этому объекту
4. Настройте параметры в инспекторе:
   - `autoSetup = true` (для автоматической настройки)
   - `includeTestEnvironment = true` (для создания тестовой среды)
   - `includeTestEnemies = true` (для создания тестовых врагов)

### 2. Ручная настройка

Если вы хотите настроить сцену вручную:

#### Ядровые системы
- Создайте GameObject `GameManager3D` с компонентом `GameManager3D`
- Создайте GameObject `InputSystem3D` с компонентом `InputSystem3D`
- Назначьте Input Actions asset (`Assets/InputSystem_Actions.inputactions`)

#### Игрок
- Создайте GameObject `Player3D` с компонентами:
  - `Rigidbody`
  - `CapsuleCollider`
  - `PlayerController3D`
  - `DisguiseSystem3D`
  - `PlayerDisguiseVisual3D`
  - `NoiseEmitter3D`
- Настройте параметры:
  - `walkSpeed = 5`
  - `runSpeed = 8`
  - `crawlSpeed = 1.5`

#### Камера
- Создайте GameObject `MainCamera` с компонентами:
  - `Camera` (tag: "MainCamera")
  - `CameraSystem3D`
- Настройте параметры:
  - `rotationAngle = 35` (угол наклона)
  - `zoomDistance = 15`
  - `useOrthographic = false`
  - Назначьте цель (игрок) через `SetTarget()`

#### Генератор уровней
- Создайте GameObject `LevelGenerator3D` с компонентом `LevelGenerator3D`
- Настройте параметры:
  - `pieceSpacing = 2`
  - `revealStepDelay = 0.15`
  - Добавьте куски в `availablePieces`

#### UI
- Создайте Canvas с компонентом `HUDController`
- Настройте UI элементы для отображения информации

## 🔧 Интеграция систем

### GameManager3D
Основной менеджер игры, который управляет:
- Состоянием игры (Menu, Loading, Playing, Paused, GameOver, Victory)
- Загрузкой уровней
- Прогрессом игры
- Сохранением данных

```csharp
// Пример использования
GameManager3D.Instance.StartNewGame();
GameManager3D.Instance.LoadLevel(1);
GameManager3D.Instance.PauseGame();
GameManager3D.Instance.ResumeGame();
```

### CameraSystem3D
Изометрическая камера с поддержкой:
- Плавного слежения за игроком
- Зума колесиком мыши
- Вращения средней кнопкой мыши
- Ортографической и перспективной проекции

```csharp
// Настройка камеры
cameraSystem.SetTarget(player.transform);
cameraSystem.SetRotationAngle(35f);
cameraSystem.SetOrthographic(false);
cameraSystem.SetZoom(15f);
```

### InputSystem3D
Унифицированная система ввода с поддержкой:
- Клавиатуры (WASD, стрелки)
- Геймпада
- Мыши
- Кастомизации управления

```csharp
// Получение ввода
Vector2 moveInput = inputSystem.MoveInput;
bool isSprinting = inputSystem.SprintPressed;
bool interactPressed = inputSystem.InteractPressed;
```

### LevelGenerator3D
Генератор уровней с поддержкой:
- Процедурной генерации из кусков
- Проверки проходимости (BFS)
- Анимации появления уровней
- Кастомизации правил генерации

```csharp
// Генерация уровня
levelGenerator.Generate(random, playerController);

// Настройка правил
levelGenerator.SetRules(generationRules);
levelGenerator.SetAvailablePieces(pieceList);
```

## 🎮 Механики

### Система маскировки (DisguiseSystem3D)
Позволяет игроку маскироваться под объекты в радиусе сканирования.

**Как работает:**
1. Игрок подходит к объекту с `DisguisableProp3D`
2. Нажимает кнопку взаимодействия
3. Игрок маскируется под этот объект
4. Враги не видят замаскированного игрока

```csharp
// Настройка
[SerializeField] private float disguiseDuration = 6f; // Время маскировки
[SerializeField] private float scanRadius = 2f;     // Радиус сканирования
```

### Система шума (NoiseEmitter3D / NoiseReceiver3D)
Враги реагируют на шум и investigating его источник.

```csharp
// Излучение шума
enemy.EmitNoise();
NoiseEmitter3D.EmitNoise(position, radius, intensity);

// Настройка
[SerializeField] private float noiseRadius = 8f;
[SerializeField] private float noiseIntensity = 1f;
```

### Управление игрока
Поддерживает:
- 8-направленное движение
- Спринт
- Взаимодействие с объектами
- Маскировку

```csharp
// Настройка скоростей
playerController.SetMoveSpeed(5f);
playerController.SetRunSpeed(8f);
playerController.SetCrawlSpeed(1.5f);

// Телепорт
playerController.TeleportTo(new Vector3(0f, 0f, 0f));
```

## 🌍 Генерация уровней

### Создание кусков уровня

1. Создайте префаб комнат
2. Добавьте компонент `PieceDataHolder3D`
3. Настройте данные в `PieceData3D`:

```csharp
// Создание скриптабельного объекта
[CreateAssetMenu(fileName = "PieceData3D", menuName = "StealthCastle/3D Piece")]
public class PieceData3D : ScriptableObject
{
    public Vector2Int SizeInTiles = new Vector2Int(3, 3);
    public Tag PieceTag = Tag.Regular;
    public GameObject Prefab;
    public ConnectionPoint3D[] Connections;
}
```

### Точки соединения

```csharp
// Пример определения точек соединения
[Serializable]
public class ConnectionPoint3D
{
    public Vector2 LocalPosition = Vector2.zero;
    public Dir Direction = Dir.North;
    
    public enum Dir { North, East, South, West }
}
```

### Правила генерации

```csharp
// Настройка весов кусков
rules.SetTagMultiplier(PieceData3D.Tag.Combat, 0.5f);
rules.SetTagMultiplier(PieceData3D.Tag.Treasure, 2f);
```

## 📊 Производительность

### PoolSystem
Система пулинга объектов для оптимизации производительности.

```csharp
// Создание пула
PoolSystem pool = new PoolSystem();

// Создание объекта из пула
gameObject = pool.GetObject(prefab);

// Возврат объекта в пул
pool.ReturnObject(gameObject);
```

### PhysicsUtils3D
Утилиты для работы с физикой в изометрии.

```csharp
// Проверка видимости
bool isVisible = PhysicsUtils3D.IsObjectVisible(obj, camera);

// Получение направления в изометрии
Vector3 isoDirection = PhysicsUtils3D.GetIsometricDirection(input);
```

## 🎨 ViSUal Style

### Изометрическая проекция
- Угол камеры: 35° наклон, 45° поворот
- Отношение осей: X:1, Y:0.7071, Z:0.5774
- Сортировка прозрачности: по оси Y

### Цветовая палитра
- Пол: темно-серый (#4A4A5A)
- Стены: серо-коричневый (#664D5B)
- Враги: красный (#8B2525)
- Игрок: синий (#2563EB)
- Пропсы: коричневый (#9A6B3B)

## 🔍 Отладка

### Gizmos
Все основные компоненты поддерживают отрисовку gizmos для отладки:

- `PlayerController3D`: направление движения, ground check
- `EnemyAI3D`: радиус видения, патрульные точки
- `DisguiseSystem3D`: радиус сканирования
- `NoiseEmitter3D`: радиус шума
- `LevelGenerator3D`: границы кусков

### Логирование
Все системы используют `Debug.Log` с тегами для удобного поиска:

```
[GameManager3D] Game state changed to: Playing
[LevelGenerator3D] Level generated with 5 pieces
[EnemyAI3D] Player detected!
```

## 📦 Зависимости

- Unity 2021.3 LTS или новее
- Input System Package
- 3D моделей (опционально)

## 🚀 Следующие шаги

1. **Создать prefab игрока** с всеми необходимыми компонентами
2. **Настроить Input System** с правильными действиями
3. **Создать базовые куски** для генератора уровней
4. **Настроить UI** для отображения состояния игры
5. **Тестировать базовое движение** и механики
6. **Оптимизировать производительность** для больших уровней
7. **Добавить визуальные эффекты** для маскировки и шума

## 📚 Документация

- [Анализ проекта](stealth_castle_analysis.md)
- [Отчет о прогрессе](refactoring_progress_report.md)
- [API Reference](#) (планируется)

---

📧 **Вопросы и поддержка**: В случае проблем обратитесь к основной документации проекта.