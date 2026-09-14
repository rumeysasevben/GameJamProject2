using System.Collections.Generic;
using UnityEngine;

namespace TenCandles
{
    // Pulls enemies from a pool and puts them on the road.
    public class EnemySpawner : MonoBehaviour
    {
        [SerializeField] PathRoute route;
        [Tooltip("Optional. Used when WaveManager sends an enemy down the second road.")]
        [SerializeField] PathRoute secondRoute;
        [SerializeField] Sprite squareSprite;
        [SerializeField] Sprite ringSprite;

        readonly Dictionary<EnemyData, ObjectPool<Enemy>> pools = new Dictionary<EnemyData, ObjectPool<Enemy>>();

        public PathRoute Route => route;
        public PathRoute SecondRoute => secondRoute;
        public bool HasSecondRoute => secondRoute != null && secondRoute.Count > 1;

        public Enemy Spawn(EnemyData data, EnemyStats stats, bool useSecondRoute = false)
        {
            if (!pools.TryGetValue(data, out var pool))
            {
                pool = new ObjectPool<Enemy>(() => Create(data));
                pools[data] = pool;
            }

            Enemy enemy = pool.Get();
            enemy.Init(data, stats, useSecondRoute && HasSecondRoute ? secondRoute : route, pool.Release);
            return enemy;
        }

        Enemy Create(EnemyData data)
        {
            var go = new GameObject(data.displayName);
            go.transform.SetParent(transform, false);
            go.SetActive(false);
            var skin = go.AddComponent<EnemySkin>();
            skin.Build(squareSprite, ringSprite);
            return go.AddComponent<Enemy>();
        }
    }
}
