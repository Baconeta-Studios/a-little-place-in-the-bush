using UnityEngine;
using UnityEngine.Pool;

namespace Utils
{
    /// <summary>
    /// Thin wrapper over Unity's built-in ObjectPool for pooling prefab instances.
    ///
    ///   private PrefabPool&lt;Bullet&gt; _bullets;
    ///   void Awake() => _bullets = new PrefabPool&lt;Bullet&gt;(bulletPrefab, transform);
    ///   Bullet b = _bullets.Get(); ... _bullets.Release(b);
    ///
    /// Objects are activated on Get and deactivated on Release. Reset per-use state yourself
    /// (e.g. in OnEnable) - the pool doesn't know what "fresh" means for your object.
    /// </summary>
    public class PrefabPool<T> where T : Component
    {
        private readonly ObjectPool<T> _pool;

        public int CountActive => _pool.CountActive;
        public int CountInactive => _pool.CountInactive;

        public PrefabPool(T prefab, Transform parent = null, int defaultCapacity = 10, int maxSize = 1000)
        {
            _pool = new ObjectPool<T>(
                createFunc: () =>
                {
                    T item = Object.Instantiate(prefab, parent);
                    item.gameObject.SetActive(false);
                    return item;
                },
                actionOnRelease: item => item.gameObject.SetActive(false),
                actionOnDestroy: item =>
                {
                    if (item) Object.Destroy(item.gameObject);
                },
                // Catches double-release bugs in the editor; skipped in builds for speed.
                collectionCheck: Application.isEditor,
                defaultCapacity: defaultCapacity,
                maxSize: maxSize);
        }

        public T Get()
        {
            T item = _pool.Get();
            item.gameObject.SetActive(true);
            return item;
        }

        public T Get(Vector3 position, Quaternion rotation)
        {
            // Position before activating so OnEnable (and trails/particles) start in the right place.
            T item = _pool.Get();
            item.transform.SetPositionAndRotation(position, rotation);
            item.gameObject.SetActive(true);
            return item;
        }

        public void Release(T item) => _pool.Release(item);

        public void Clear() => _pool.Clear();
    }
}
