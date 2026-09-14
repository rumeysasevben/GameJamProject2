using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TenCandles
{
    // Placement, cost queries, and paying CandleClock for builds and upgrades.
    public class BuildManager : MonoBehaviour
    {
        public static BuildManager Instance { get; private set; }

        [SerializeField] TowerData[] towers;
        [SerializeField] Transform spotsRoot;
        [SerializeField] SpriteRenderer rangeIndicator;
        [SerializeField] Sprite pipSprite;
        [SerializeField] float pickRadius = 1f;

        public TowerData[] Towers => towers;
        public TowerData SelectedType { get; private set; }
        public Tower SelectedTower { get; private set; }
        // Empty pad whose build ring is open, if any.
        public TowerSpot RingSpot { get; private set; }
        // Ring slot under the mouse, for the range preview.
        public TowerData RingHover { get; set; }
        public int TowersBuilt { get; private set; }

        // Local UI notification; not a game event.
        public event Action SelectionChanged;

        TowerSpot[] spots;
        TowerSpot hovered;
        Camera cam;

        void Awake()
        {
            Instance = this;
            RefreshSpots();
            cam = Camera.main;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // MapLoader rebuilds the pads when the player picks another map.
        public void RefreshSpots()
        {
            SetHovered(null);
            spots = spotsRoot != null ? spotsRoot.GetComponentsInChildren<TowerSpot>() : FindObjectsByType<TowerSpot>(FindObjectsSortMode.None);
        }

        void OnEnable() => GameEvents.StateChanged += OnStateChanged;
        void OnDisable() => GameEvents.StateChanged -= OnStateChanged;

        void OnStateChanged(GameState state)
        {
            if (state == GameState.Victory || state == GameState.Defeat || state == GameState.Boot) ClearSelection();
        }

        public bool CanBuildNow
        {
            get
            {
                var gm = GameManager.Instance;
                return gm != null && (gm.State == GameState.Intermission || gm.State == GameState.Wave);
            }
        }

        public bool HasFreeBuild => StatRegistry.FreeBuilds > 0;

        public float BuildCost(TowerData data) => HasFreeBuild ? 0f : Mathf.Round(data.baseCost * StatRegistry.BuildCostMultiplier);

        public float UpgradeCost(Tower tower) => tower.IsMaxLevel ? 0f : Mathf.Round(tower.Data.UpgradeCost(tower.Level + 1) * StatRegistry.UpgradeCostMultiplier);

        public bool CanAfford(float cost) => cost <= 0f || CandleClock.Instance.CanAfford(cost);

        public void SelectType(TowerData data)
        {
            SelectedType = SelectedType == data ? null : data;
            SelectedTower = null;
            RingSpot = null;
            SelectionChanged?.Invoke();
        }

        public void SelectType(int index)
        {
            if (index >= 0 && index < towers.Length) SelectType(towers[index]);
        }

        public void SelectTower(Tower tower)
        {
            SelectedTower = tower;
            SelectedType = null;
            RingSpot = null;
            SelectionChanged?.Invoke();
        }

        public void ClearSelection()
        {
            if (SelectedType == null && SelectedTower == null && RingSpot == null) return;
            SelectedType = null;
            SelectedTower = null;
            RingSpot = null;
            SelectionChanged?.Invoke();
        }

        public void OpenRing(TowerSpot spot)
        {
            SelectedType = null;
            SelectedTower = null;
            RingSpot = spot != null && spot.IsEmpty ? spot : null;
            RingHover = null;
            SelectionChanged?.Invoke();
        }

        public bool TryBuild(TowerData data, TowerSpot spot)
        {
            if (!CanBuildNow || spot == null || !spot.IsEmpty) return false;

            bool free = HasFreeBuild;
            float cost = BuildCost(data);
            if (!free && !CandleClock.Instance.TrySpend(cost, data.displayName))
            {
                GameEvents.RaiseBuildFailed("You need more time for " + data.displayName);
                return false;
            }
            if (free) StatRegistry.FreeBuilds--;

            var go = new GameObject(data.displayName);
            go.transform.SetParent(transform, false);
            var tower = go.AddComponent<Tower>();
            tower.Init(data, spot, pipSprite);
            spot.Tower = tower;
            TowersBuilt++;

            GameEvents.RaiseTowerBuilt(tower);
            SelectTower(tower);
            return true;
        }

        public bool TryUpgrade(Tower tower)
        {
            if (!CanBuildNow || tower == null || tower.IsMaxLevel) return false;

            float cost = UpgradeCost(tower);
            if (!CandleClock.Instance.TrySpend(cost, "Upgrade"))
            {
                GameEvents.RaiseBuildFailed("You need more time to upgrade");
                return false;
            }

            tower.SetLevel(tower.Level + 1);
            GameEvents.RaiseTowerUpgraded(tower, tower.Level);
            SelectionChanged?.Invoke();
            return true;
        }

        void Update()
        {
            if (cam == null) cam = Camera.main;
            bool active = CanBuildNow && !TimeControl.IsMenuPaused;

            Vector3 mouse = cam.ScreenToWorldPoint(Input.mousePosition);
            mouse.z = 0f;
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

            SetHovered(active && !overUI ? SpotAt(mouse) : null);

            if (active)
            {
                HandleKeys();
                if (!overUI && Input.GetMouseButtonDown(0)) Click();
                if (Input.GetMouseButtonDown(1)) ClearSelection();
            }

            UpdateRangeIndicator();
        }

        void HandleKeys()
        {
            for (int i = 0; i < towers.Length && i < 9; i++)
            {
                if (!Input.GetKeyDown(KeyCode.Alpha1 + i)) continue;
                // With the ring open the number builds straight onto that pad.
                if (RingSpot != null) TryBuild(towers[i], RingSpot);
                else SelectType(i);
            }

            if (Input.GetKeyDown(KeyCode.U) && SelectedTower != null) TryUpgrade(SelectedTower);
        }

        void Click()
        {
            if (hovered == null)
            {
                ClearSelection();
                return;
            }

            if (!hovered.IsEmpty) SelectTower(hovered.Tower);
            else if (SelectedType != null) TryBuild(SelectedType, hovered);
            else if (RingSpot == hovered) ClearSelection();
            else OpenRing(hovered);
        }

        TowerSpot SpotAt(Vector3 world)
        {
            TowerSpot best = null;
            float bestDist = pickRadius * pickRadius;
            foreach (var spot in spots)
            {
                float d = (spot.transform.position - world).sqrMagnitude;
                if (d <= bestDist)
                {
                    best = spot;
                    bestDist = d;
                }
            }
            return best;
        }

        void SetHovered(TowerSpot spot)
        {
            if (hovered == spot) return;
            if (hovered != null) hovered.SetHighlight(false);
            hovered = spot;
            if (hovered != null) hovered.SetHighlight(true);
        }

        void UpdateRangeIndicator()
        {
            if (rangeIndicator == null) return;

            Vector3 center = Vector3.zero;
            float range = 0f;

            if (SelectedTower != null)
            {
                center = SelectedTower.transform.position;
                range = SelectedTower.Range;
            }
            else if (RingSpot != null)
            {
                center = RingSpot.transform.position;
                range = RingHover != null ? RingHover.range * StatRegistry.RangeMultiplier : 0f;
            }
            else if (SelectedType != null && hovered != null && hovered.IsEmpty)
            {
                center = hovered.transform.position;
                range = SelectedType.range * StatRegistry.RangeMultiplier;
            }
            else if (hovered != null && !hovered.IsEmpty)
            {
                center = hovered.transform.position;
                range = hovered.Tower.Range;
            }

            rangeIndicator.enabled = range > 0f;
            if (range <= 0f) return;
            rangeIndicator.transform.position = center;
            rangeIndicator.transform.localScale = Vector3.one * range * 2f;
        }
    }
}
