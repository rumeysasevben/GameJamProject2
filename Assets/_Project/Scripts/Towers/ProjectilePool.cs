using UnityEngine;

namespace TenCandles
{
    public class ProjectilePool : MonoBehaviour
    {
        public static ProjectilePool Instance { get; private set; }

        ObjectPool<Projectile> pool;

        void Awake()
        {
            Instance = this;
            pool = new ObjectPool<Projectile>(Create, 30);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Launch(TowerData data, Vector3 from, Enemy target, ProjectileHit hit)
        {
            pool.Get().Launch(data, from, target, hit, pool.Release);
        }

        Projectile Create()
        {
            var go = new GameObject("Projectile");
            go.transform.SetParent(transform, false);
            go.SetActive(false);
            var p = go.AddComponent<Projectile>();
            p.Build();
            return p;
        }
    }
}
