using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StealthCastle.Isometric3D.World
{
    /// <summary>
    /// Генератор уровней для 3D изометрической версии.
    /// Генерирует уровни из кусков (комнат) с проверкой проходимости.
    /// </summary>
    public class LevelGenerator3D : MonoBehaviour
    {
        [Header("Generation Settings")]
        [SerializeField] private GenerationRules3D rules;
        [SerializeField] private float pieceSpacing = 2f; // Расстояние между кусками
        [SerializeField] private float revealStepDelay = 0.4f; // Задержка между появлениями кусков
        [SerializeField] private float revealStartDelay = 0.8f; // Пауза перед первым куском: камера отъезжает к общему плану
        [SerializeField] private float revealRiseDuration = 0.7f; // Подъём одного куска из-под пола
        [SerializeField] private LayerMask wallMask = -1; // Слой стен
        [SerializeField] private LayerMask propMask = -1; // Слой предметов
        [SerializeField] private LayerMask defaultLayer; // Дефолтный слой

        [Header("Level Pieces")]
        [SerializeField] private List<PieceData3D> availablePieces = new List<PieceData3D>();

        private readonly List<GameObject> spawnedPieces = new List<GameObject>();
        private System.Action onCaught; // Колбэк поимки
        private readonly List<Coroutine> revealCoroutines = new List<Coroutine>();
        private Coroutine buildRoutine;
        private readonly Dictionary<PieceData3D.Tag, float> tagMultipliers = new Dictionary<PieceData3D.Tag, float>();

        /// <summary>Множитель дальности зрения стражей на уровнях забега (артефакты)</summary>
        public float GuardVisionMultiplier { get; set; } = 1f;

        /// <summary>Множитель веса кусков с тегом (артефакты): 3 — втрое чаще, 0.5 — вдвое реже</summary>
        public void SetTagMultiplier(PieceData3D.Tag tag, float multiplier) => tagMultipliers[tag] = Mathf.Max(0f, multiplier);

        /// <summary>Сброс модификаторов забега</summary>
        public void ClearModifiers()
        {
            tagMultipliers.Clear();
            GuardVisionMultiplier = 1f;
        }

        public IReadOnlyList<GameObject> SpawnedPieces => spawnedPieces;
        public GameObject StartPiece { get; private set; }
        public GameObject ExitPiece { get; private set; }

        /// <summary>
        /// Генерация уровня
        /// </summary>
        /// <param name="onBuilt">
        /// Уровень собран и показан. Если задан, управление игроку не возвращается — это решает вызывающий
        /// </param>
        /// <param name="onLayout">Раскладка готова (границы уровня), куски вот-вот начнут появляться</param>
        public void Generate(System.Random rng, Player.PlayerController3D player, System.Action onPlayerCaught = null,
            System.Action onBuilt = null, System.Action<Bounds> onLayout = null)
        {
            Clear();
            onCaught = onPlayerCaught;
            buildRoutine = StartCoroutine(BuildRoutine(rng, player, onBuilt, onLayout));
        }

        /// <summary>
        /// Очистка текущего уровня
        /// </summary>
        public void Clear()
        {
            // Останавливаем сборку и анимации: иначе старая сборка продолжит перебирать уже удалённые куски
            if (buildRoutine != null)
            {
                StopCoroutine(buildRoutine);
                buildRoutine = null;
            }
            foreach (var cr in revealCoroutines)
            {
                if (cr != null) StopCoroutine(cr);
            }
            revealCoroutines.Clear();

            DestroySpawned();
        }

        private void DestroySpawned()
        {
            foreach (var go in spawnedPieces)
            {
                if (go != null) Destroy(go);
            }
            spawnedPieces.Clear();
            StartPiece = null;
            ExitPiece = null;
        }

        private IEnumerator BuildRoutine(System.Random rng, Player.PlayerController3D player, System.Action onBuilt, System.Action<Bounds> onLayout)
        {
            if (availablePieces.Count == 0)
            {
                Debug.LogError("[LevelGenerator3D] No pieces available for generation!");
                yield break;
            }

            // Отключаем управление на время сборки
            if (player != null) player.SetInputEnabled(false);

            // Clear() удаляет куски прошлого уровня только в конце кадра — иначе их коллайдеры попадут в проверку пути
            yield return null;

            // Раскладка → куски на местах (ещё скрыты) → физическая проверка пути от старта до выхода.
            // Граф проёмов связен всегда, но предметы внутри кусков могут перекрыть путь — такую раскладку отбрасываем
            LevelCandidate3D built = null;
            for (int layout = 1; layout <= MaxLayoutAttempts; layout++)
            {
                built = PlanLayout(rng);
                if (built == null)
                {
                    if (player != null) player.SetInputEnabled(true);
                    yield break;
                }
                SpawnBuilt(built, player);

                var report = CheckSpawned();
                if (!report.HasErrors) break;

                string pieces = string.Join(", ", built.pieces.ConvertAll(p => p.name));
                if (layout == MaxLayoutAttempts)
                {
                    Debug.LogError($"[LevelGenerator3D] No walkable layout after {MaxLayoutAttempts} tries, keeping the last: {pieces}");
                    break;
                }
                string why = string.Join("; ", report.issues) + " " +
                             string.Join(" ", report.links.ConvertAll(l => $"{l.from}->{l.to}:{l.status}"));
                Debug.LogWarning($"[LevelGenerator3D] Layout rejected: {pieces} | {why}. Run StealthCastle/3D/Piece Validator.");
                DestroySpawned();
                yield return null; // Destroy срабатывает в конце кадра — иначе старые коллайдеры попадут в следующую проверку
            }

            // Раскладка известна до показа — камера успевает отъехать и показать уровень целиком
            onLayout?.Invoke(GetLevelBounds(built));
            yield return new WaitForSeconds(revealStartDelay);
            yield return RevealBuilt(); // Вложенный итератор: останавливается вместе со сборкой
            yield return new WaitForSeconds(revealRiseDuration); // Последний кусок поднимается до конца

            // Если сборку ведёт вызывающий (onBuilt), управление отдаёт он — после вступления уровня
            if (onBuilt == null && player != null) player.SetInputEnabled(true);
            onBuilt?.Invoke();
        }

        private const int MaxLayoutAttempts = 5;

        /// <summary>Раскладка уровня по графу проёмов; если за 50 попыток не вышло — старт и выход вплотную</summary>
        private LevelCandidate3D PlanLayout(System.Random rng)
        {
            PieceData3D startPiece = FindPieceByTag(PieceData3D.Tag.Start);
            PieceData3D exitPiece = FindPieceByTag(PieceData3D.Tag.Exit);
            if (startPiece == null || exitPiece == null)
            {
                Debug.LogError("[LevelGenerator3D] Start or Exit piece not found!");
                return null;
            }

            int levelLength = rng.Next(4, 7); // 4-6 кусков
            const int maxTries = 50;
            for (int attempt = 0; attempt < maxTries; attempt++)
            {
                if (TryBuildCandidate(rng, levelLength, startPiece, exitPiece, out var candidate)) return candidate;
            }
            var fallback = BuildFallbackChain(startPiece, exitPiece);
            fallback.isFallback = true;
            return fallback;
        }

        /// <summary>Путь от точки спавна до выходов по реальным коллайдерам; скрытые куски на время проверки включаются</summary>
        private WalkabilityCheck3D.Report CheckSpawned()
        {
            var hidden = new List<GameObject>();
            foreach (var go in spawnedPieces)
            {
                if (go.activeSelf) continue;
                go.SetActive(true);
                hidden.Add(go);
            }

            var pieces = new List<LevelPiece3D>();
            var exits = new List<Vector3>();
            foreach (var go in spawnedPieces)
            {
                var piece = go.GetComponent<LevelPiece3D>();
                if (piece != null) pieces.Add(piece);
                foreach (var exit in go.GetComponentsInChildren<LevelExit3D>()) exits.Add(exit.transform.position);
            }
            var spawn = StartPiece != null ? StartPiece.GetComponentInChildren<SpawnPoint3D>() : null;
            var start = spawn != null ? spawn.transform.position : StartPiece != null ? StartPiece.transform.position : Vector3.zero;
            var report = WalkabilityCheck3D.AnalyzeLevel(start, pieces, exits, WalkabilityGrid3D.Settings.Default);

            foreach (var go in hidden) go.SetActive(false);
            return report;
        }

        /// <summary>Раскладка для пакетных проверок в редакторе: какие куски, где и какие проёмы открыты</summary>
        public class LayoutInfo
        {
            public List<PieceData3D> pieces;
            public List<Vector3> positions;
            public List<HashSet<ConnectionPoint3D.Dir>> openDirs;
            public bool isFallback;
        }

        public LayoutInfo PlanLayoutInfo(System.Random rng)
        {
            var c = PlanLayout(rng);
            if (c == null) return null;
            return new LayoutInfo { pieces = c.pieces, positions = c.positions, openDirs = c.openDirs, isFallback = c.isFallback };
        }

        /// <summary>Границы собранного уровня (куски + высота стен)</summary>
        private Bounds GetLevelBounds(LevelCandidate3D c)
        {
            var bounds = new Bounds(c.positions[0], Vector3.zero);
            for (int i = 0; i < c.pieces.Count; i++)
            {
                var size = Footprint(c.pieces[i]);
                bounds.Encapsulate(new Bounds(c.positions[i] + Vector3.up, new Vector3(size.x, 2f, size.y)));
            }
            return bounds;
        }

        /// <summary>
        /// Попытка построить кандидат уровня
        /// </summary>
        private bool TryBuildCandidate(
            System.Random rng, 
            int length, 
            PieceData3D startPiece, 
            PieceData3D exitPiece,
            out LevelCandidate3D result)
        {
            result = null;

            var candidate = new LevelCandidate3D();
            AddStart(candidate, startPiece, rng);

            var current = 0;
            
            // Растим цепь до (length-2) средних кусков
            while (candidate.pieces.Count < length - 1)
            {
                var next = PickNextForCurrent(candidate, current, rng, null, exitPiece);
                if (next != null)
                {
                    current = candidate.pieces.Count - 1;
                    continue;
                }

                // Не вышло - помечаем текущий проём занятым
                MarkFirstFreeConnUsed(candidate, current);
                
                // Если все проёмы заняты - откат
                if (AllConnsUsed(candidate, current))
                {
                    if (current == 0) return false; // Тупик у старта
                    RemoveLastPiece(candidate);
                    current--;
                }
            }

            // Добавляем финишный кусок
            var exitNext = PickNextForCurrent(candidate, current, rng, exitPiece, exitPiece);
            if (exitNext == null) return false;

            // Проверяем проходимость (BFS)
            var exitIndex = candidate.pieces.Count - 1;
            if (!BfsReachable(candidate, 0, exitIndex)) return false;

            result = candidate;
            return true;
        }

        /// <summary>
        /// Помечаем первый свободный проём как использованный
        /// </summary>
        private static void MarkFirstFreeConnUsed(LevelCandidate3D c, int idx)
        {
            var conns = c.connOrder[idx];
            if (conns == null) return;
            
            foreach (var conn in conns)
            {
                if (!c.usedDirs[idx].Contains(conn.Direction))
                {
                    c.usedDirs[idx].Add(conn.Direction);
                    return;
                }
            }
        }

        /// <summary>
        /// Проверяем, все ли проёмы куска заняты
        /// </summary>
        private static bool AllConnsUsed(LevelCandidate3D c, int idx)
        {
            var conns = c.connOrder[idx];
            if (conns == null || conns.Length == 0) return true;
            
            foreach (var conn in conns)
            {
                if (!c.usedDirs[idx].Contains(conn.Direction)) return false;
            }
            return true;
        }

        /// <summary>
        /// Выбор следующего куска для текущего
        /// </summary>
        private LevelCandidate3D PickNextForCurrent(
            LevelCandidate3D c, 
            int current, 
            System.Random rng, 
            PieceData3D forced,
            PieceData3D exitPiece)
        {
            var curData = c.pieces[current];
            var curConns = c.connOrder[current]; // Проёмы куска в его случайном порядке
            if (curConns == null || curConns.Length == 0) return null;

            // Ищем первый свободный проём
            ConnectionPoint3D freeConn = default;
            var found = false;
            foreach (var conn in curConns)
            {
                if (!c.usedDirs[current].Contains(conn.Direction))
                {
                    freeConn = conn;
                    found = true;
                    break;
                }
            }
            if (!found) return null;

            var needDir = ConnectionPoint3D.Opposite(freeConn.Direction);

            // Ищем совместимый кусок
            for (var scan = 0; scan < availablePieces.Count * 2; scan++)
            {
                var data = forced ?? GetRandomPiece(rng, curData, exitPiece);
                if (data == null) continue;
                if (data == curData) continue;

                var match = default(ConnectionPoint3D);
                var hasMatch = false;
                if (data.Connections != null)
                {
                    foreach (var mc in data.Connections)
                    {
                        if (mc.Direction == needDir)
                        {
                            match = mc;
                            hasMatch = true;
                            break;
                        }
                    }
                }
                if (!hasMatch) continue;

                // Вычисляем позицию: проёмы двух кусков совпадают
                var worldPosNext = c.positions[current] + ToXZ(freeConn.LocalPosition - match.LocalPosition);

                // Цепь может завернуть назад — кусок не должен лечь на уже поставленный
                if (Overlaps(c, data, worldPosNext)) continue;

                AddPiece(c, data, worldPosNext, current, freeConn.Direction, match.Direction, rng);
                return c;
            }
            return null;
        }

        /// <summary>
        /// Случайный средний кусок с учётом веса куска и множителя его тега
        /// </summary>
        private PieceData3D GetRandomPiece(System.Random rng, PieceData3D exclude1, PieceData3D exclude2)
        {
            var validPieces = new List<PieceData3D>();
            var weights = new List<float>();
            float total = 0f;
            foreach (var piece in availablePieces)
            {
                if (piece != null && piece != exclude1 && piece != exclude2 && piece.PieceTag != PieceData3D.Tag.Start && piece.PieceTag != PieceData3D.Tag.Exit)
                {
                    float weight = piece.Weight * (tagMultipliers.TryGetValue(piece.PieceTag, out var m) ? m : 1f);
                    validPieces.Add(piece);
                    weights.Add(weight);
                    total += weight;
                }
            }

            if (validPieces.Count == 0) return null;
            if (total <= 0f) return validPieces[rng.Next(validPieces.Count)];

            double roll = rng.NextDouble() * total;
            for (int i = 0; i < validPieces.Count; i++)
            {
                roll -= weights[i];
                if (roll < 0) return validPieces[i];
            }
            return validPieces[validPieces.Count - 1];
        }

        /// <summary>
        /// Проверка проходимости через BFS
        /// </summary>
        private static bool BfsReachable(LevelCandidate3D c, int start, int exit)
        {
            var visited = new HashSet<int> { start };
            var queue = new Queue<int>();
            queue.Enqueue(start);
            
            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                if (node == exit) return true;
                
                foreach (var (from, to) in c.edges)
                {
                    if (from == node && visited.Add(to)) queue.Enqueue(to);
                    if (to == node && visited.Add(from)) queue.Enqueue(from);
                }
            }
            return false;
        }

        /// <summary>
        /// Фолбэк: старт и выход вплотную по первому подходящему проёму старта
        /// </summary>
        private LevelCandidate3D BuildFallbackChain(PieceData3D startPiece, PieceData3D exitPiece)
        {
            var c = new LevelCandidate3D();
            AddStart(c, startPiece, null);

            foreach (var conn in startPiece.Connections)
            {
                if (TryFindConnection(exitPiece, ConnectionPoint3D.Opposite(conn.Direction), out var match))
                {
                    AddPiece(c, exitPiece, ToXZ(conn.LocalPosition - match.LocalPosition), 0, conn.Direction, match.Direction, null);
                    return c;
                }
            }

            Debug.LogError("[LevelGenerator3D] Exit piece has no connection matching the start piece");
            return c;
        }

        private static bool TryFindConnection(PieceData3D data, ConnectionPoint3D.Dir need, out ConnectionPoint3D result)
        {
            if (data.Connections != null)
            {
                foreach (var conn in data.Connections)
                {
                    if (conn.Direction == need)
                    {
                        result = conn;
                        return true;
                    }
                }
            }
            result = default;
            return false;
        }

        private static void AddStart(LevelCandidate3D c, PieceData3D data, System.Random rng)
        {
            c.connOrder.Add(Shuffled(data.Connections, rng));
            c.pieces.Add(data);
            c.positions.Add(Vector3.zero);
            c.usedDirs.Add(new HashSet<ConnectionPoint3D.Dir>());
            c.openDirs.Add(new HashSet<ConnectionPoint3D.Dir>());
            c.parentDirs.Add(default);
        }

        /// <summary>
        /// Пристыковать кусок к родителю: проём родителя parentDir ↔ проём куска ownDir
        /// </summary>
        private static void AddPiece(LevelCandidate3D c, PieceData3D data, Vector3 position, int parent,
            ConnectionPoint3D.Dir parentDir, ConnectionPoint3D.Dir ownDir, System.Random rng)
        {
            c.connOrder.Add(Shuffled(data.Connections, rng));
            c.pieces.Add(data);
            c.positions.Add(position);
            c.usedDirs.Add(new HashSet<ConnectionPoint3D.Dir> { ownDir });
            c.openDirs.Add(new HashSet<ConnectionPoint3D.Dir> { ownDir });
            c.parentDirs.Add(parentDir);
            c.usedDirs[parent].Add(parentDir);
            c.openDirs[parent].Add(parentDir);
            c.edges.Add((parent, c.pieces.Count - 1));
        }

        /// <summary>
        /// Откат последнего куска. Проём родителя остаётся «использованным» (не пробуем его снова), но закрывается
        /// </summary>
        private static void RemoveLastPiece(LevelCandidate3D c)
        {
            int last = c.pieces.Count - 1;
            var (parent, _) = c.edges[c.edges.Count - 1];
            c.openDirs[parent].Remove(c.parentDirs[last]);

            c.pieces.RemoveAt(last);
            c.positions.RemoveAt(last);
            c.usedDirs.RemoveAt(last);
            c.openDirs.RemoveAt(last);
            c.parentDirs.RemoveAt(last);
            c.connOrder.RemoveAt(last);
            c.edges.RemoveAt(c.edges.Count - 1);
        }

        /// <summary>
        /// Проёмы в случайном порядке: цепь продолжается не всегда через первый проём из списка,
        /// иначе уровень почти всегда растёт в одну сторону и часть кусков почти не выпадает
        /// </summary>
        private static ConnectionPoint3D[] Shuffled(ConnectionPoint3D[] connections, System.Random rng)
        {
            var result = connections != null ? (ConnectionPoint3D[])connections.Clone() : new ConnectionPoint3D[0];
            if (rng == null) return result;
            for (int i = result.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (result[i], result[j]) = (result[j], result[i]);
            }
            return result;
        }

        /// <summary>Размер куска в плане, м</summary>
        private Vector2 Footprint(PieceData3D data) => new Vector2(data.SizeInTiles.x, data.SizeInTiles.y) * pieceSpacing;

        /// <summary>
        /// Ляжет ли кусок на уже поставленные. Касание по границе — норма (соседи), наложение — нет
        /// </summary>
        private bool Overlaps(LevelCandidate3D c, PieceData3D data, Vector3 position)
        {
            var size = Footprint(data);
            for (int i = 0; i < c.pieces.Count; i++)
            {
                var other = Footprint(c.pieces[i]);
                var delta = position - c.positions[i];
                if (Mathf.Abs(delta.x) < (size.x + other.x) * 0.5f - 0.01f &&
                    Mathf.Abs(delta.z) < (size.y + other.y) * 0.5f - 0.01f)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Поиск куска по тегу
        /// </summary>
        private PieceData3D FindPieceByTag(PieceData3D.Tag tag)
        {
            foreach (var piece in availablePieces)
            {
                if (piece != null && piece.PieceTag == tag)
                    return piece;
            }
            return null;
        }

        /// <summary>
        /// Локальная позиция проёма (X,Y в плане куска) → смещение в мире по XZ
        /// </summary>
        private static Vector3 ToXZ(Vector2 v) => new Vector3(v.x, 0f, v.y);

        /// <summary>
        /// Спавн собранного уровня
        /// </summary>
        private void SpawnBuilt(LevelCandidate3D c, Player.PlayerController3D player)
        {
            for (var i = 0; i < c.pieces.Count; i++)
            {
                var go = SpawnPiece(c.pieces[i], c.positions[i], player, onCaught);
                ApplyDoorways(go, c.openDirs[i]);
                if (i == 0) StartPiece = go;
                if (i == c.pieces.Count - 1) ExitPiece = go;
            }

            // Игрок начинает в точке спавна стартового куска
            if (player != null && StartPiece != null)
            {
                // Точка спавна стоит на полу — игрок встаёт центром капсулы над ней
                var spawn = StartPiece.GetComponentInChildren<SpawnPoint3D>(true);
                var ground = spawn != null ? spawn.transform.position : StartPiece.transform.position;
                var capsule = player.GetComponent<CapsuleCollider>();
                player.TeleportTo(ground + Vector3.up * (capsule != null ? capsule.height * 0.5f : 1f));
            }
        }

        /// <summary>
        /// Проёмы, через которые куски стыкуются, открыты; остальные закрыты заглушками «Blocker_{Dir}»,
        /// чтобы из уровня нельзя было выйти в пустоту
        /// </summary>
        public static void ApplyDoorways(GameObject piece, HashSet<ConnectionPoint3D.Dir> openDirs)
        {
            foreach (ConnectionPoint3D.Dir dir in Enum.GetValues(typeof(ConnectionPoint3D.Dir)))
            {
                var blocker = piece.transform.Find("Blocker_" + dir);
                if (blocker != null)
                {
                    blocker.gameObject.SetActive(!openDirs.Contains(dir));
                }
            }
        }

        /// <summary>
        /// Спавн отдельного куска
        /// </summary>
        private GameObject SpawnPiece(
            PieceData3D data, 
            Vector3 worldPos, 
            Player.PlayerController3D player, 
            System.Action onCaught)
        {
            GameObject go;
            if (data.Prefab != null)
            {
                go = Instantiate(data.Prefab, worldPos, Quaternion.identity, transform);
            }
            else
            {
                go = CreatePlaceholderPiece(data, worldPos);
            }
            
            go.SetActive(false);
            spawnedPieces.Add(go);
            
            WireUpPiece(go, player, onCaught);
            return go;
        }

        /// <summary>
        /// Подключение куска (враги, пропы)
        /// </summary>
        private void WireUpPiece(GameObject piece, Player.PlayerController3D player, System.Action onCaught)
        {
            // Враги
            var enemies = piece.GetComponentsInChildren<Enemies.EnemyAI3D>(true);
            foreach (var enemy in enemies)
            {
                enemy.PlayerTransform = player != null ? player.transform : null;
                if (onCaught != null) enemy.OnPlayerCaught += onCaught;
                enemy.SetWallMask(wallMask);
                if (!Mathf.Approximately(GuardVisionMultiplier, 1f)) enemy.ScaleVision(GuardVisionMultiplier);
            }

            // Пропы для маскировки
            var props = piece.GetComponentsInChildren<Mechanics.DisguisableProp3D>(true);
            foreach (var prop in props)
            {
                if (propMask != (LayerMask)(-1))
                {
                    prop.gameObject.layer = LayerMaskToLayer(propMask);
                }
            }

            //Level Exit
            var levelExit = piece.GetComponentInChildren<LevelExit3D>(true);
            if (levelExit != null)
            {
                levelExit.OnLevelCompleted += HandleLevelCompleted;
            }
        }

        /// <summary>
        /// Преобразование LayerMask в индекс слоя
        /// </summary>
        private static int LayerMaskToLayer(LayerMask mask)
        {
            var v = mask.value;
            for (var i = 0; i < 32; i++)
                if ((v & (1 << i)) != 0) return i;
            return 0;
        }

        /// <summary>
        /// Создание плейсхолдера для куска
        /// </summary>
        private GameObject CreatePlaceholderPiece(PieceData3D data, Vector3 pos)
        {
            var size = new Vector2(data.SizeInTiles.x, data.SizeInTiles.y) * pieceSpacing;
            var go = new GameObject($"Piece_{data.name}");
            go.transform.position = pos;
            go.transform.localScale = Vector3.one;

            // Создаем пол комнат
            var floor = GameObject.CreatePrimitive(PrimitiveType.Quad);
            floor.name = "Floor";
            floor.transform.SetParent(go.transform, false);
            floor.transform.localScale = new Vector3(size.x, size.y, 1f); // Quad повёрнут на 90° по X: локальный Y = мировой Z
            floor.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            
            var floorRenderer = floor.GetComponent<Renderer>();
            floorRenderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            floorRenderer.material.color = GetFloorColor(data);

            // Добавляем компонент для данных
            var pieceDataHolder = go.AddComponent<PieceDataHolder3D>();
            pieceDataHolder.Data = data;

            return go;
        }

        /// <summary>
        /// Цвет пола по тегу
        /// </summary>
        private Color GetFloorColor(PieceData3D data)
        {
            return data.PieceTag switch
            {
                PieceData3D.Tag.Start => new Color(0.3f, 0.5f, 0.4f),    // Зеленый
                PieceData3D.Tag.Exit => new Color(0.5f, 0.3f, 0.8f),     // Сиреневый
                PieceData3D.Tag.Treasure => new Color(0.8f, 0.6f, 0.2f), // Золотой
                PieceData3D.Tag.Combat => new Color(0.6f, 0.2f, 0.2f),   // Красный
                _ => new Color(0.25f, 0.25f, 0.3f)                   // Серый
            };
        }

        /// <summary>
        /// Обработка завершения уровня
        /// </summary>
        private void HandleLevelCompleted()
        {
            var gameManager = Core.GameManager3D.Instance;
            if (gameManager != null)
            {
                gameManager.CompleteLevel();
            }
        }

        /// <summary>
        /// Установка правил генерации
        /// </summary>
        public void SetRules(GenerationRules3D newRules)
        {
            rules = newRules;
        }

        /// <summary>
        /// Установка доступных кусков
        /// </summary>
        public void SetAvailablePieces(List<PieceData3D> pieces)
        {
            availablePieces = pieces;
        }

        /// <summary>
        /// Анимация появления уровня
        /// </summary>
        private IEnumerator RevealBuilt()
        {
            foreach (var go in spawnedPieces)
            {
                yield return new WaitForSeconds(revealStepDelay);
                revealCoroutines.Add(StartCoroutine(RevealRoutine(go)));
            }
        }

        /// <summary>
        /// Корутина появления отдельного куска
        /// </summary>
        private IEnumerator RevealRoutine(GameObject go)
        {
            // Кусок поднимается из-под пола. Прозрачность не трогаем: непрозрачные материалы её не показывают,
            // а .material подменил бы материалы, которыми управляют компоненты (конус зрения, подсветка пропов)
            go.SetActive(true);
            var targetPos = go.transform.position;

            // Стражей двигает подъём куска, а не физика: на время анимации — кинематика без интерполяции,
            // иначе интерполяция перезаписывает позицию и тело остаётся на чужой высоте
            var bodies = go.GetComponentsInChildren<Rigidbody>();
            var savedKinematic = new bool[bodies.Length];
            var savedInterpolation = new RigidbodyInterpolation[bodies.Length];
            for (int i = 0; i < bodies.Length; i++)
            {
                savedKinematic[i] = bodies[i].isKinematic;
                savedInterpolation[i] = bodies[i].interpolation;
                bodies[i].isKinematic = true;
                bodies[i].interpolation = RigidbodyInterpolation.None;
            }

            go.transform.position = targetPos + Vector3.down * 1.5f;

            float duration = revealRiseDuration;
            var t = 0f;
            while (t < duration)
            {
                if (go == null) yield break; // Уровень пересобрали посреди анимации
                t += Time.deltaTime;
                var k = Mathf.SmoothStep(0f, 1f, t / duration);
                go.transform.position = Vector3.Lerp(targetPos + Vector3.down * 1.5f, targetPos, k);
                yield return null;
            }
            if (go == null) yield break;
            go.transform.position = targetPos;

            for (int i = 0; i < bodies.Length; i++)
            {
                if (bodies[i] == null) continue;
                bodies[i].position = bodies[i].transform.position;
                bodies[i].isKinematic = savedKinematic[i];
                bodies[i].interpolation = savedInterpolation[i];
            }
        }

        /// <summary>
        /// Кандидат уровня для генерации
        /// </summary>
        private sealed class LevelCandidate3D
        {
            public readonly List<PieceData3D> pieces = new List<PieceData3D>();
            public readonly List<Vector3> positions = new List<Vector3>();
            public readonly List<HashSet<ConnectionPoint3D.Dir>> usedDirs = new List<HashSet<ConnectionPoint3D.Dir>>(); // Перебранные проёмы
            public readonly List<HashSet<ConnectionPoint3D.Dir>> openDirs = new List<HashSet<ConnectionPoint3D.Dir>>(); // Реально соединённые
            public readonly List<ConnectionPoint3D.Dir> parentDirs = new List<ConnectionPoint3D.Dir>(); // Проём родителя, ведущий в кусок
            public readonly List<ConnectionPoint3D[]> connOrder = new List<ConnectionPoint3D[]>(); // Порядок перебора проёмов куска
            public readonly List<(int from, int to)> edges = new List<(int, int)>();
            public bool isFallback; // Не собралось за 50 попыток — старт и выход вплотную
        }
    }
}