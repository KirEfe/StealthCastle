using System.Collections.Generic;
using UnityEngine;

namespace StealthCastle.Isometric3D.Utils
{
    /// <summary>
    /// Система пула объектов для оптимизации производительности.
    /// Позволяет переиспользовать объекты вместо создания новых.
    /// </summary>
    public class PoolSystem : MonoBehaviour
    {
        [System.Serializable]
        public class PoolItem
        {
            public GameObject prefab;
            public int initialCount = 10;
            public int maxCount = 100;
            public bool expandable = true;
        }

        [SerializeField] private List<PoolItem> poolItems = new List<PoolItem>();
        
        private Dictionary<GameObject, Pool> pools = new Dictionary<GameObject, Pool>();

        private void Awake()
        {
            InitializePools();
        }

        private void OnDestroy()
        {
            ClearPools();
        }

        /// <summary>
        /// Инициализация всех пулов
        /// </summary>
        private void InitializePools()
        {
            foreach (var item in poolItems)
            {
                if (item.prefab != null)
                {
                    CreatePool(item.prefab, item.initialCount, item.maxCount, item.expandable);
                }
            }
        }

        /// <summary>
        /// Создание пула для определенного префаба
        /// </summary>
        private void CreatePool(GameObject prefab, int initialCount, int maxCount, bool expandable)
        {
            if (pools.ContainsKey(prefab)) return;

            var pool = new Pool(prefab, initialCount, maxCount, expandable);
            pool.Initialize();
            pools[prefab] = pool;
        }

        /// <summary>
        /// Получение объекта из пула
        /// </summary>
        public GameObject GetFromPool(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (!pools.TryGetValue(prefab, out var pool)) return null;
            return pool.Get(position, rotation);
        }

        /// <summary>
        /// Возврат объекта в пул
        /// </summary>
        public void ReturnToPool(GameObject obj)
        {
            if (obj == null) return;
            
            foreach (var pool in pools.Values)
            {
                if (pool.Contains(obj))
                {
                    pool.Return(obj);
                    return;
                }
            }
            
            Debug.LogWarning($"[PoolSystem] Object {obj.name} not found in any pool!");
        }

        /// <summary>
        /// Создание нового пула в runtime
        /// </summary>
        public void CreateRuntimePool(GameObject prefab, int initialCount = 5, int maxCount = 50, bool expandable = true)
        {
            if (prefab == null) return;
            if (pools.ContainsKey(prefab)) return;

            CreatePool(prefab, initialCount, maxCount, expandable);
        }

        /// <summary>
        /// Очистка всех пулов
        /// </summary>
        private void ClearPools()
        {
            foreach (var pool in pools.Values)
            {
                pool.Clear();
            }
            pools.Clear();
        }

        /// <summary>
        /// Очистка конкретного пула
        /// </summary>
        public void ClearPool(GameObject prefab)
        {
            if (pools.TryGetValue(prefab, out var pool))
            {
                pool.Clear();
                pools.Remove(prefab);
            }
        }

        /// <summary>
        /// Получение количества активных объектов в пуле
        /// </summary>
        public int GetActiveCount(GameObject prefab)
        {
            if (pools.TryGetValue(prefab, out var pool))
            {
                return pool.ActiveCount;
            }
            return 0;
        }

        /// <summary>
        /// Получение общего количества объектов в пуле
        /// </summary>
        public int GetTotalCount(GameObject prefab)
        {
            if (pools.TryGetValue(prefab, out var pool))
            {
                return pool.TotalCount;
            }
            return 0;
        }

        /// <summary>
        /// Пул объектов
        /// </summary>
        private class Pool
        {
            private GameObject prefab;
            private Stack<GameObject> inactiveObjects = new Stack<GameObject>();
            private List<GameObject> activeObjects = new List<GameObject>();
            private int maxCount;
            private bool expandable;
            private Transform poolParent;

            public int ActiveCount => activeObjects.Count;
            public int TotalCount => inactiveObjects.Count + activeObjects.Count;

            public Pool(GameObject prefab, int initialCount, int maxCount, bool expandable)
            {
                this.prefab = prefab;
                this.maxCount = maxCount;
                this.expandable = expandable;
                
                // Создаем контейнер для пула
                poolParent = new GameObject($"Pool_{prefab.name}").transform;
                poolParent.SetParent(PoolSystemManager.Instance != null ? 
                    PoolSystemManager.Instance.transform : null);
            }

            public void Initialize()
            {
                for (int i = 0; i < Mathf.Min(30, maxCount); i++) // Не создаем слишком много сразу
                {
                    CreateNewObject();
                }
            }

            private GameObject CreateNewObject()
            {
                var obj = Instantiate(prefab, Vector3.zero, Quaternion.identity, poolParent);
                obj.name = $"{prefab.name}_PoolInstance_{TotalCount}";
                obj.SetActive(false);
                inactiveObjects.Push(obj);
                return obj;
            }

            public GameObject Get(Vector3 position, Quaternion rotation)
            {
                if (inactiveObjects.Count > 0)
                {
                    var obj = inactiveObjects.Pop();
                    obj.transform.position = position;
                    obj.transform.rotation = rotation;
                    obj.SetActive(true);
                    activeObjects.Add(obj);
                    return obj;
                }
                else if (expandable && TotalCount < maxCount)
                {
                    var obj = CreateNewObject();
                    obj.transform.position = position;
                    obj.transform.rotation = rotation;
                    obj.SetActive(true);
                    activeObjects.Add(obj);
                    return obj;
                }

                Debug.LogWarning($"[PoolSystem] Pool for {prefab.name} exhausted! Max: {maxCount}, Active: {ActiveCount}");
                return null;
            }

            public void Return(GameObject obj)
            {
                if (obj == null) return;
                
                obj.SetActive(false);
                obj.transform.SetParent(poolParent, true);
                
                activeObjects.Remove(obj);
                inactiveObjects.Push(obj);
            }

            public bool Contains(GameObject obj)
            {
                return activeObjects.Contains(obj) || inactiveObjects.Contains(obj);
            }

            public void Clear()
            {
                // Уничтожаем все неактивные объекты
                foreach (var obj in inactiveObjects)
                {
                    if (obj != null) Destroy(obj);
                }
                inactiveObjects.Clear();

                // Уничтожаем все активные объекты
                foreach (var obj in activeObjects)
                {
                    if (obj != null) Destroy(obj);
                }
                activeObjects.Clear();

                // Уничтожаем контейнер
                if (poolParent != null) Destroy(poolParent.gameObject);
            }
        }
    }

    /// <summary>
    /// Менеджер пулов (Singleton)
    /// </summary>
    public class PoolSystemManager : MonoBehaviour
    {
        public static PoolSystemManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Получение объекта из пула
        /// </summary>
        public GameObject GetFromPool(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            var poolSystem = FindObjectOfType<PoolSystem>();
            if (poolSystem != null)
            {
                return poolSystem.GetFromPool(prefab, position, rotation);
            }
            return Instantiate(prefab, position, rotation);
        }

        /// <summary>
        /// Возврат объекта в пул
        /// </summary>
        public void ReturnToPool(GameObject obj)
        {
            var poolSystem = FindObjectOfType<PoolSystem>();
            if (poolSystem != null)
            {
                poolSystem.ReturnToPool(obj);
            }
            else
            {
                Destroy(obj);
            }
        }
    }
}