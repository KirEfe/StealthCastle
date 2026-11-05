using System.Collections.Generic;
using UnityEngine;

namespace StealthCastle.Isometric.World
{
    /// <summary>
    /// Генератор уровня: собирает весь уровень сразу из кусков
    /// стартовый → цепочка → выход, затем показывает сборку игроку.
    /// </summary>
    public class LevelGenerator : MonoBehaviour
    {
        [SerializeField] private GenerationRules rules;         // правила генерации
        [SerializeField] private float tileSize = 2f;           // размер тайла кусковой сетки в метрах
        [SerializeField] private float revealStepDelay = 0.15f; // пауза между появлениями кусков
        [SerializeField] private LayerMask wallMask = -1;       // слой стен (для обхода врагом)
        [SerializeField] private LayerMask propMask = -1;       // слой пропов (для маскировки)

        private readonly List<GameObject> spawnedPieces = new List<GameObject>();
        private System.Action onCaught; // колбэк поимки
        private readonly List<Coroutine> revealCoroutines = new List<Coroutine>(); // корутины анимации сборки — надо гасить при Clear()

        public IReadOnlyList<GameObject> SpawnedPieces => spawnedPieces;
        public GameObject StartPiece { get; private set; }
        public GameObject ExitPiece { get; private set; }

        /// <summary>Сгенерировать уровень и проиграть анимацию сборки.
        /// onPlayerCaught — колбэк поимки (цепляем к RunManager).</summary>
        public void Generate(System.Random rng, Player.IsoPlayerController player, System.Action onPlayerCaught = null)
        {
            Clear();
            onCaught = onPlayerCaught;
            StartCoroutine(BuildRoutine(rng, player));
        }

        // Очистить предыдущий уровень (публично — для сброса между забегами)
        public void Clear()
        {
            // Гасим корутины анимации — иначе лезут в transform уничтоженных объектов
            foreach (var cr in revealCoroutines)
                if (cr != null) StopCoroutine(cr);
            revealCoroutines.Clear();

            foreach (var go in spawnedPieces)
                if (go != null) Destroy(go);
            spawnedPieces.Clear();
            StartPiece = null;
            ExitPiece = null;
        }

        private System.Collections.IEnumerator BuildRoutine(System.Random rng, Player.IsoPlayerController player)
        {
            if (rules == null || rules.PiecePool.Count == 0) yield break;

            // Отключаем управление на время сборки
            if (player != null) player.SetInputEnabled(false);

            var length = rng.Next(rules.MinPieces, rules.MaxPieces + 1);
            const int maxTries = 50;

            // Пытаемся собрать валидный (проходимый по BFS) уровень
            for (var attempt = 0; attempt < maxTries; attempt++)
            {
                if (TryBuildCandidate(rng, length, out var built))
                {
                    SpawnBuilt(built, player);
                    yield return StartCoroutine(RevealBuilt());
                    yield return new WaitForSeconds(revealStepDelay * 2f);
                    if (player != null) player.SetInputEnabled(true);
                    yield break;
                }
            }

            // Фолбэк: гарантированно проходимая цепочка (S↔N)
            SpawnBuilt(BuildFallbackChain(), player);
            yield return StartCoroutine(RevealBuilt());
            yield return new WaitForSeconds(revealStepDelay * 2f);
            if (player != null) player.SetInputEnabled(true);
        }

        // Один кандидат уровня: список кусков + рёбра графа
        private sealed class LevelCandidate
        {
            public readonly List<PieceData> pieces = new List<PieceData>();
            public readonly List<Vector3> positions = new List<Vector3>();
            public readonly List<HashSet<ConnectionPoint.Dir>> usedDirs = new List<HashSet<ConnectionPoint.Dir>>();
            public readonly List<(int from, int to)> edges = new List<(int, int)>();
        }

        // Попытка собрать кандидата с матчингом проёмов + BFS-проверкой
        private bool TryBuildCandidate(System.Random rng, int length, out LevelCandidate result)
        {
            result = null;
            var startData = FindByTag(rules, PieceData.Tag.Start);
            var exitData = FindByTag(rules, PieceData.Tag.Exit);
            if (startData == null || exitData == null) return false;

            var c = new LevelCandidate();
            c.pieces.Add(startData);
            c.positions.Add(Vector3.zero);
            c.usedDirs.Add(new HashSet<ConnectionPoint.Dir>());

            var current = 0;
            // Растим цепь до (length-2) средних кусков, с бэктреком по неудачным проёмам
            while (c.pieces.Count < length - 1)
            {
                var next = PickNextForCurrent(c, current, rng, null);
                if (next != null) { current = c.pieces.Count - 1; continue; }

                // Не вышло — помечаем текущий проём занятым и пробуем другой проём этого же куска
                MarkFirstFreeConnUsed(c, current);
                // Если у current вообще нет свободных проёмов — откат к предыдущему
                if (AllConnsUsed(c, current))
                {
                    if (current == 0) return false; // тупик у старта — весь кандидат невалиден
                    c.pieces.RemoveAt(c.pieces.Count - 1);
                    c.positions.RemoveAt(c.positions.Count - 1);
                    c.usedDirs.RemoveAt(c.usedDirs.Count - 1);
                    c.edges.RemoveAt(c.edges.Count - 1);
                    current--;
                }
            }

            // Выходной кусок — в конце, стыкуем к последнему
            var exitNext = PickNextForCurrent(c, current, rng, exitData);
            if (exitNext == null) return false;

            // BFS: Exit достижим от Start?
            var exitIndex = c.pieces.Count - 1;
            if (!BfsReachable(c, 0, exitIndex)) return false;

            result = c;
            return true;
        }

        // Пометить первый свободный проём куска занятым (чтобы попробовать другой при бэктреке)
        private static void MarkFirstFreeConnUsed(LevelCandidate c, int idx)
        {
            var conns = c.pieces[idx].Connections;
            if (conns == null) return;
            foreach (var cc in conns)
                if (!c.usedDirs[idx].Contains(cc.Direction)) { c.usedDirs[idx].Add(cc.Direction); return; }
        }

        // Все проёмы куска уже заняты?
        private static bool AllConnsUsed(LevelCandidate c, int idx)
        {
            var conns = c.pieces[idx].Connections;
            if (conns == null || conns.Length == 0) return true;
            foreach (var cc in conns)
                if (!c.usedDirs[idx].Contains(cc.Direction)) return false;
            return true;
        }

        // Выбрать и присоединить следующий кусок к current; возвращает null при неудаче
        private LevelCandidate PickNextForCurrent(LevelCandidate c, int current, System.Random rng, PieceData forced)
        {
            var curData = c.pieces[current];
            var curConns = curData.Connections;
            if (curConns == null || curConns.Length == 0) return null;

            // Первый свободный проём текущего куска
            ConnectionPoint freeConn = default;
            var found = false;
            foreach (var conn in curConns)
            {
                if (!c.usedDirs[current].Contains(conn.Direction))
                {
                    freeConn = conn; found = true; break;
                }
            }
            if (!found) return null;

            var needDir = ConnectionPoint.Opposite(freeConn.Direction);

            // Сканируем пул: ищем кусок с проёмом needDir (с учётом forced/исключений)
            var pool = rules.PiecePool;
            for (var scan = 0; scan < pool.Count * 2; scan++)
            {
                var data = forced ?? rules.PickWeighted(rng,
                    excludeTag: PieceData.Tag.Start, excludeTag2: PieceData.Tag.Exit);
                if (data == null) continue;
                if (data == curData) continue;

                var match = default(ConnectionPoint);
                var hasMatch = false;
                if (data.Connections != null)
                {
                    foreach (var mc in data.Connections)
                        if (mc.Direction == needDir) { match = mc; hasMatch = true; break; }
                }
                if (!hasMatch) continue;

                // Позиция: совмещаем проёмы
                var worldPosNext = c.positions[current] + (Vector3)(freeConn.LocalPosition - match.LocalPosition);

                c.pieces.Add(data);
                c.positions.Add(worldPosNext);
                c.usedDirs.Add(new HashSet<ConnectionPoint.Dir> { match.Direction });
                c.usedDirs[current].Add(freeConn.Direction);
                c.edges.Add((current, c.pieces.Count - 1));
                return c;
            }
            return null; // не нашли совместимый — цепь прервана
        }

        // BFS-проверка достижимости exit от start по графу рёбер
        private static bool BfsReachable(LevelCandidate c, int start, int exit)
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

        // Гарантированно проходимая цепочка (чистый S↔N матчинг)
        private LevelCandidate BuildFallbackChain()
        {
            var c = new LevelCandidate();
            var start = FindByTag(rules, PieceData.Tag.Start);
            var exit = FindByTag(rules, PieceData.Tag.Exit);
            // Берём любой коридор/кусок с N и S проёмами для середины
            PieceData mid = null;
            foreach (var p in rules.PiecePool)
            {
                if (p == null || p == start || p == exit) continue;
                var hasN = false; var hasS = false;
                if (p.Connections != null)
                    foreach (var cc in p.Connections)
                    { if (cc.Direction == ConnectionPoint.Dir.North) hasN = true; if (cc.Direction == ConnectionPoint.Dir.South) hasS = true; }
                if (hasN && hasS) { mid = p; break; }
            }

            c.pieces.Add(start); c.positions.Add(Vector3.zero); c.usedDirs.Add(new HashSet<ConnectionPoint.Dir>());
            if (mid != null)
            {
                var freeConn = StartFreeConn(start);
                var match = MidMatch(mid, ConnectionPoint.Opposite(freeConn.Direction));
                // MidMatch может вернуть default (если у mid нет нужного проёма) —
                // у default направление North (0), поэтому проверяем явным совпадением, а не «не South».
                if (match.Direction != ConnectionPoint.Dir.North || match.LocalPosition == default)
                    match = new ConnectionPoint(new Vector2(0, -mid.SizeInTiles.y), ConnectionPoint.Dir.South); // запас
                var pos = (Vector3)(freeConn.LocalPosition - match.LocalPosition);
                c.pieces.Add(mid); c.positions.Add(pos); c.usedDirs.Add(new HashSet<ConnectionPoint.Dir> { match.Direction });
                c.usedDirs[0].Add(freeConn.Direction);
                c.edges.Add((0, 1));
            }
            // Exit
            var lastIdx = c.pieces.Count - 1;
            var freeLast = StartFreeConn(c.pieces[lastIdx]);
            var exitMatch = MidMatch(exit, ConnectionPoint.Opposite(freeLast.Direction));
            if (exitMatch.Direction != ConnectionPoint.Dir.North || exitMatch.LocalPosition == default)
                exitMatch = new ConnectionPoint(new Vector2(0, -4f), ConnectionPoint.Dir.South); // запас для Exit
            var exitPos = c.positions[lastIdx] + (Vector3)(freeLast.LocalPosition - exitMatch.LocalPosition);
            c.pieces.Add(exit); c.positions.Add(exitPos); c.usedDirs.Add(new HashSet<ConnectionPoint.Dir> { exitMatch.Direction });
            c.usedDirs[lastIdx].Add(freeLast.Direction);
            c.edges.Add((lastIdx, c.pieces.Count - 1));
            return c;
        }

        private static ConnectionPoint StartFreeConn(PieceData d)
        {
            foreach (var cc in d.Connections)
                return cc; // первый проём (для фолбэка достаточно)
            return default;
        }

        private static ConnectionPoint MidMatch(PieceData d, ConnectionPoint.Dir need)
        {
            if (d.Connections != null)
                foreach (var cc in d.Connections)
                    if (cc.Direction == need) return cc;
            return default;
        }

        // Спавн всех кусков кандидата (через существующий SpawnPiece + WireUpPiece)
        private void SpawnBuilt(LevelCandidate c, Player.IsoPlayerController player)
        {
            for (var i = 0; i < c.pieces.Count; i++)
            {
                var go = SpawnPiece(c.pieces[i], c.positions[i], player, onCaught);
                if (i == 0) StartPiece = go;
                if (i == c.pieces.Count - 1) ExitPiece = go;
            }
        }

        // Анимация сборки всех кусков по очереди
        private System.Collections.IEnumerator RevealBuilt()
        {
            foreach (var go in spawnedPieces)
            {
                yield return new WaitForSeconds(revealStepDelay);
                revealCoroutines.Add(StartCoroutine(RevealRoutine(go)));
            }
            revealCoroutines.Clear(); // сборка закончена — очищаем список
        }

        // Поиск куска по тегу в пуле
        private static PieceData FindByTag(GenerationRules r, PieceData.Tag tag)
        {
            foreach (var p in r.PiecePool)
                if (p != null && p.PieceTagValue.Value == tag) return p;
            return null;
        }

        // Спавн куска в мировых координатах + подключение врагов/пропов
        private GameObject SpawnPiece(PieceData data, Vector3 worldPos, Player.IsoPlayerController player, System.Action onCaught)
        {
            GameObject go;
            if (data.Prefab != null)
                go = Instantiate(data.Prefab, worldPos, Quaternion.identity, transform);
            else
                go = CreatePlaceholderPiece(data, worldPos); // плейсхолдер без префаба
            go.SetActive(false);
            spawnedPieces.Add(go);

            WireUpPiece(go, player, onCaught);
            return go;
        }

        // Подключение врагов из префаба к игроку и событию поимки; пропов — к слою маскировки
        private void WireUpPiece(GameObject piece, Player.IsoPlayerController player, System.Action onCaught)
        {
            // Враги
            foreach (var enemy in piece.GetComponentsInChildren<Enemies.IsoEnemyAI>(true))
            {
                if (player != null)
                {
                    enemy.Player = player.transform;
                    if (onCaught != null) enemy.OnPlayerCaught += onCaught;
                    enemy.WallMask = wallMask;
                }
            }

            // Пропы — метка слоем для маскировки
            foreach (var prop in piece.GetComponentsInChildren<Mechanics.IsoDisguisableProp>(true))
            {
                if (propMask != (LayerMask)(-1)) prop.gameObject.layer = LayerMaskToLayer(propMask);
            }
        }

        // Маска "только один слой" → индекс слоя
        private static int LayerMaskToLayer(LayerMask mask)
        {
            var v = mask.value;
            for (var i = 0; i < 32; i++)
                if ((v & (1 << i)) != 0) return i;
            return 0;
        }

        // Плейсхолдер куска: пол-ромб + стены по периметру
        private GameObject CreatePlaceholderPiece(PieceData data, Vector3 pos)
        {
            var size = new Vector2(data.SizeInTiles.x, data.SizeInTiles.y) * tileSize;
            var go = new GameObject($"Piece_{data.name}");
            go.transform.position = pos;
            go.AddComponent<PieceDataHolder>().Data = data;

            var floor = new GameObject("Floor");
            floor.transform.SetParent(go.transform, false);
            floor.transform.localScale = new Vector3(size.x, size.y * 0.5774f, 1f);
            var fsr = floor.AddComponent<SpriteRenderer>();
            fsr.sprite = Debugging.PlaceholderSpriteFactory.CreateSquareSprite(FloorColor(data));
            fsr.sortingOrder = -10;

            return go;
        }

        // Цвет пола по тегу куска — визуальная читаемость сборки
        private static Color32 FloorColor(PieceData data) => data.PieceTagValue.Value switch
        {
            PieceData.Tag.Start => new Color32(80, 140, 90, 255),
            PieceData.Tag.Exit => new Color32(120, 100, 200, 255),
            PieceData.Tag.Treasure => new Color32(200, 170, 70, 255),
            PieceData.Tag.Combat => new Color32(160, 80, 80, 255),
            _ => new Color32(70, 70, 90, 255),
        };

        // Эффект появления: снизу вверх + растворение из прозрачного
        private static System.Collections.IEnumerator RevealRoutine(GameObject go)
        {
            go.SetActive(true);
            var targetPos = go.transform.position;
            go.transform.position = targetPos + Vector3.down * 1.5f;

            var renderers = go.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var r in renderers)
            {
                var c = r.color; c.a = 0f; r.color = c;
            }

            const float duration = 0.35f;
            var t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                var k = Mathf.SmoothStep(0f, 1f, t / duration);
                go.transform.position = Vector3.Lerp(targetPos + Vector3.down * 1.5f, targetPos, k);
                foreach (var r in renderers)
                {
                    var c = r.color; c.a = k; r.color = c;
                }
                yield return null;
            }
        }
    }
}
