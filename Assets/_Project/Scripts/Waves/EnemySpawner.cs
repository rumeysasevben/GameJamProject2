using System.Collections.Generic;
using UnityEngine;

namespace TenCandles
{
    // Pulls enemies from a pool and puts them on the road.
    public class EnemySpawner : MonoBehaviour
    {
        [SerializeField] PathRoute route;
        [Tooltip("Optional second entrance. Used once WaveManager opens a second lane.")]
        [SerializeField] PathRoute secondRoute;
        [SerializeField] Sprite squareSprite;
        [SerializeField] Sprite ringSprite;

        readonly Dictionary<EnemyData, ObjectPool<Enemy>> pools = new Dictionary<EnemyData, ObjectPool<Enemy>>();

        public PathRoute Route => route;
        public PathRoute SecondRoute => secondRoute;
        public bool HasSecondRoute => secondRoute != null && secondRoute.Count > 1;
        // Map entrances. WaveManager decides how many are in use (spec §12.2).
        public int LaneCount => HasSecondRoute ? 2 : 1;

        public Enemy Spawn(EnemyData data, EnemyStats stats, int lane = 0)
        {
            if (!pools.TryGetValue(data, out var pool))
            {
                pool = new ObjectPool<Enemy>(() => Create(data));
                pools[data] = pool;
            }

            Enemy enemy = pool.Get();
            enemy.Init(data, stats, lane > 0 && HasSecondRoute ? secondRoute : route, pool.Release);
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
