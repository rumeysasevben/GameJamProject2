using System;
using System.Collections.Generic;
using UnityEngine;

namespace TenCandles
{
    // Lays the chosen map into the scene: background, tile props, road waypoints, tower pads and the cake.
    // Runs before everything else so BuildManager and the spawner see the finished layout.
    [DefaultExecutionOrder(-100)]
    public class MapLoader : MonoBehaviour
    {
        const string PrefsKey = "TenCandles.Map";

        public static MapLoader Instance { get; private set; }

        // Survives the scene reload on "Play again".
        static int selected = -1;

        [SerializeField] MapData[] maps;
        [SerializeField] SpriteRenderer background;
        [SerializeField] SpriteRenderer filler;
        [SerializeField] PathRoute route;
        [SerializeField] PathRoute secondRoute;
        [SerializeField] Transform spotsRoot;
        [SerializeField] Transform propsRoot;
        [SerializeField] Transform party;
        [Tooltip("Hover/idle highlight drawn over every pad.")]
        [SerializeField] Sprite highlightSprite;
        [SerializeField] Vector2 highlightSize = new Vector2(2.1f, 1.05f);

        public int Count => maps != null ? maps.Length : 0;
        public int Index { get; private set; } = -1;
        public MapData Selected => Index >= 0 && Index < Count ? maps[Index] : null;
        // The layout actually in play: the selected map, or the layout generated from it.
        public MapData Current { get; private set; }
        public bool IsGenerated => Selected != null && Selected.generated;
        public bool HasSecondRoute => Current != null && Current.secondRoute != null && Current.secondRoute.Length > 1 && secondRoute != null;

        public event Action MapChanged;

        readonly List<SpriteRenderer> tinted = new List<SpriteRenderer>();
        Color appliedTint = Color.white;

        void Awake()
        {
            Instance = this;
            if (Count == 0) return;
            if (selected < 0) selected = PlayerPrefs.GetInt(PrefsKey, 0);
            Apply(Mathf.Clamp(selected, 0, Count - 1));
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Select(int index)
        {
            if (Count == 0) return;
            index = ((index % Count) + Count) % Count;
            if (index == Index) return;
            selected = index;
            PlayerPrefs.SetInt(PrefsKey, index);
            Apply(index);
            AfterChange();
        }

        public void Next() => Select(Index + 1);
        public void Previous() => Select(Index - 1);

        // New random layout for a generated map.
        public void Reroll()
        {
            if (!IsGenerated) return;
            Apply(Index);
            AfterChange();
        }

        void AfterChange()
        {
            if (BuildManager.Instance != null) BuildManager.Instance.RefreshSpots();
            MapChanged?.Invoke();
        }

        void Apply(int index)
        {
            Index = index;
            MapData map = maps[index];
            // Generated layouts are runtime instances; assets must never be destroyed.
            if (Current != null && Current.name.EndsWith(MapGenerator.InstanceSuffix)) Destroy(Current);
            Current = map.generated ? MapGenerator.Generate(map, Environment.TickCount ^ (index * 7919)) : map;

            tinted.Clear();
            if (background != null)
            {
                background.sprite = Current.background;
                tinted.Add(background);
            }
            if (filler != null)
            {
                filler.sprite = Current.filler;
                filler.enabled = Current.filler != null;
                tinted.Add(filler);
            }
            var cam = Camera.main;
            if (cam != null) cam.backgroundColor = Current.cameraColor;

            BuildProps(Current);
            BuildRoute(route, Current.route);
            BuildRoute(secondRoute, Current.secondRoute);
            BuildSpots(Current);

            if (party != null && Current.route.Length > 1)
            {
                Vector2[] r = Current.route;
                party.position = MapData.ToWorld(r[r.Length - 1]);
                Vector3 dir = MapData.ToWorld(r[r.Length - 1]) - MapData.ToWorld(r[r.Length - 2]);
                var host = party.GetComponentInChildren<BirthdayHost>();
                if (host != null) host.PlaceBeside(dir);
            }
            appliedTint = background != null ? background.color : Color.white;
            foreach (var sr in tinted) sr.color = appliedTint;
        }

        // ThemeManager fades the background between eras; everything painted on the map follows it.
        void LateUpdate()
        {
            if (background == null || background.color == appliedTint) return;
            appliedTint = background.color;
            foreach (var sr in tinted)
                if (sr != null) sr.color = appliedTint;
        }

        void BuildProps(MapData map)
        {
            if (propsRoot == null) return;
            Clear(propsRoot);
            if (map.props == null) return;
            foreach (var p in map.props)
            {
                if (p.sprite == null) continue;
                var sr = new GameObject(p.sprite.name).AddComponent<SpriteRenderer>();
                sr.transform.SetParent(propsRoot, false);
                sr.transform.position = MapData.ToWorld(p.pixel);
                sr.sprite = p.sprite;
                sr.flipX = p.flipX;
                sr.sortingOrder = p.order;
                tinted.Add(sr);
            }
        }

        static void BuildRoute(PathRoute target, Vector2[] points)
        {
            if (target == null) return;
            Clear(target.transform);
            if (points != null)
                for (int i = 0; i < points.Length; i++)
                {
                    var wp = new GameObject("Waypoint" + i);
                    wp.transform.SetParent(target.transform, false);
                    wp.transform.position = MapData.ToWorld(points[i]);
                }
            target.Recache();
        }

        void BuildSpots(MapData map)
        {
            if (spotsRoot == null) return;
            Clear(spotsRoot);
            if (map.paintedSpots != null) foreach (var p in map.paintedSpots) AddSpot(p, null);
            if (map.extraSpots != null) foreach (var p in map.extraSpots) AddSpot(p, map.padSprite);
        }

        void AddSpot(Vector2 pixel, Sprite ground)
        {
            var go = new GameObject("Spot");
            go.transform.SetParent(spotsRoot, false);
            go.transform.position = MapData.ToWorld(pixel);

            if (ground != null)
            {
                var g = new GameObject("Ground").AddComponent<SpriteRenderer>();
                g.transform.SetParent(go.transform, false);
                g.sprite = ground;
                g.sortingOrder = -50;
                tinted.Add(g);
            }

            var pad = new GameObject("Highlight").AddComponent<SpriteRenderer>();
            pad.transform.SetParent(go.transform, false);
            pad.transform.localScale = new Vector3(highlightSize.x, highlightSize.y, 1f);
            pad.sprite = highlightSprite;
            pad.color = Color.clear;
            pad.sortingOrder = -5;

            go.AddComponent<TowerSpot>().SetPad(pad);
        }

        // Detach first so childCount is correct this frame; Destroy itself is deferred.
        static void Clear(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                child.SetParent(null, false);
                Destroy(child.gameObject);
            }
        }
    }
}
