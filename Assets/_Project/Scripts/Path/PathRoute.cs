using UnityEngine;

namespace TenCandles
{
    // The road enemies walk. Waypoints are the child transforms, in order.
    public class PathRoute : MonoBehaviour
    {
        Vector3[] points;

        public int Count => Points.Length;
        public Vector3 this[int index] => Points[index];
        public Vector3 StartPoint => Points[0];
        public Vector3 EndPoint => Points[Points.Length - 1];

        Vector3[] Points
        {
            get
            {
                if (points == null || points.Length != transform.childCount) Cache();
                return points;
            }
        }

        void Awake() => Cache();

        // Call after moving, adding or removing waypoints at runtime.
        public void Recache() => Cache();

        void Cache()
        {
            points = new Vector3[transform.childCount];
            for (int i = 0; i < points.Length; i++) points[i] = transform.GetChild(i).position;
        }

        // Shortest distance from a point to the road, for placement checks and decor.
        public float DistanceTo(Vector3 p)
        {
            float best = float.MaxValue;
            for (int i = 0; i < Count - 1; i++)
            {
                Vector3 a = this[i], b = this[i + 1];
                Vector3 ab = b - a;
                float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, Vector3.Distance(p, a + ab * t));
            }
            return best;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f);
            for (int i = 0; i < transform.childCount; i++)
            {
                Vector3 p = transform.GetChild(i).position;
                Gizmos.DrawWireSphere(p, 0.2f);
                if (i > 0) Gizmos.DrawLine(transform.GetChild(i - 1).position, p);
            }
        }
    }
}
