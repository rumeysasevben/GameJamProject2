using System.Collections.Generic;
using TenCandles.Lifetime;
using UnityEngine;
using UnityEngine.UI;

namespace TenCandles
{
    // Left drawer: one button per tower, priced in seconds. Starts closed; the arrow tab (or TAB) slides it in and out.
    public class BuildPanelUI : MonoBehaviour
    {
        class Entry
        {
            public TowerData data;
            public Button button;
            public Image background;
            public Outline outline;
            public Text cost;
            public Text title;
            public Image icon;
        }

        const float SlideSeconds = 0.18f;

        readonly List<Entry> entries = new List<Entry>();
        Text description;
        Text header;
        UISkin skin;
        RectTransform panel;
        Text arrow;
        bool open;
        float shown; // 0 closed .. 1 open
        TowerData lastSelected;

        public void Build(RectTransform root)
        {
            skin = UIFactory.Skin;
            var build = BuildManager.Instance;
            if (build == null) return;

            panel = UIFactory.Panel(root, "BuildPanel", skin.panelColor).rectTransform;
            panel.anchorMin = new Vector2(0f, 0f);
            panel.anchorMax = new Vector2(0f, 1f);
            panel.pivot = new Vector2(0f, 0.5f);
            panel.offsetMin = new Vector2(0f, 0f);
            panel.offsetMax = new Vector2(HUD.SidePanelWidth, -HUD.TopBarHeight);

            header = UIFactory.Label(panel, "Header", "TOWERS", 24, TextAnchor.MiddleCenter, skin.accent);
            header.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(220f, 36f));

            const float height = 104f, gap = 10f;
            for (int i = 0; i < build.Towers.Length; i++)
            {
                TowerData data = build.Towers[i];
                var e = new Entry { data = data };
                int index = i;
                e.button = UIFactory.Button(panel, data.displayName, skin.panelLight, () => BuildManager.Instance.SelectType(index));
                e.background = (Image)e.button.targetGraphic;
                e.button.GetComponent<RectTransform>().Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -54f - i * (height + gap)), new Vector2(222f, height));
                e.outline = UIFactory.AddOutline(e.background, skin.accent, 4f);

                e.icon = UIFactory.Image(e.button.transform, "Icon", data.tint, data.SpriteForLevel(1));
                e.icon.preserveAspect = true;
                e.icon.raycastTarget = false;
                e.icon.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 4f), new Vector2(56f, 56f));

                var key = UIFactory.Label(e.button.transform, "Key", $"[{i + 1}]", 14, TextAnchor.UpperLeft, skin.mutedText);
                key.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -4f), new Vector2(40f, 20f));

                e.title = UIFactory.Label(e.button.transform, "Name", data.displayName, 17, TextAnchor.UpperLeft, skin.text);
                e.title.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(74f, -12f), new Vector2(142f, 48f));

                e.cost = UIFactory.Label(e.button.transform, "Cost", "", 22, TextAnchor.LowerLeft, skin.accent);
                e.cost.rectTransform.Place(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(74f, 8f), new Vector2(142f, 34f));

                entries.Add(e);
            }

            description = UIFactory.Label(panel, "Description", "", 15, TextAnchor.UpperLeft, skin.mutedText);
            description.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -60f - build.Towers.Length * (height + gap)), new Vector2(214f, 220f));

            // The tab sticks out of the drawer's right edge, so it slides along with it.
            var tab = UIFactory.Button(panel, "Tab", skin.panelColor, Toggle);
            tab.GetComponent<RectTransform>().Place(new Vector2(1f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 60f), new Vector2(46f, 120f));
            UIFactory.AddOutline((Image)tab.targetGraphic, skin.accent, 2f);
            arrow = UIFactory.Label(tab.transform, "Arrow", ">", 34, TextAnchor.MiddleCenter, skin.accent);
            arrow.rectTransform.Fill();
            var tabKey = UIFactory.Label(tab.transform, "Key", "TAB", 11, TextAnchor.LowerCenter, skin.mutedText);
            tabKey.rectTransform.Fill(0f, 6f, 0f, 0f);

            SetOffset(0f);
        }

        public void Toggle() => open = !open;

        void SetOffset(float t)
        {
            float x = -HUD.SidePanelWidth * (1f - t);
            panel.offsetMin = new Vector2(x, 0f);
            panel.offsetMax = new Vector2(x + HUD.SidePanelWidth, -HUD.TopBarHeight);
        }

        void Update()
        {
            var build = BuildManager.Instance;
            if (build == null || skin == null || panel == null) return;
            bool canBuild = build.CanBuildNow;

            if (Input.GetKeyDown(KeyCode.Tab) && !TimeControl.IsMenuPaused) Toggle();
            // Picking a tower with 1-4 opens the drawer so you can see what you picked.
            if (build.SelectedType != lastSelected)
            {
                if (build.SelectedType != null) open = true;
                lastSelected = build.SelectedType;
            }
            float target = open ? 1f : 0f;
            if (!Mathf.Approximately(shown, target))
            {
                shown = Mathf.MoveTowards(shown, target, Time.unscaledDeltaTime / SlideSeconds);
                SetOffset(Mathf.SmoothStep(0f, 1f, shown));
            }
            arrow.text = open ? "<" : ">";

            bool full = build.AtTowerCapacity;
            header.text = build.TowerCapacity < int.MaxValue ? $"TOWERS  {build.TowersBuilt}/{build.TowerCapacity}" : "TOWERS";
            header.color = full ? skin.danger : skin.accent;

            foreach (var e in entries)
            {
                float cost = build.BuildCost(e.data);
                bool affordable = build.CanAfford(cost) && !full;
                bool selected = build.SelectedType == e.data;

                e.cost.text = cost <= 0f ? "FREE" : $"{cost:0} s";
                e.cost.color = affordable ? (cost <= 0f ? skin.good : skin.accent) : skin.danger;
                e.background.color = affordable && canBuild ? skin.panelLight : skin.disabled;
                e.icon.color = affordable ? e.data.tint : new Color(0.5f, 0.5f, 0.5f, 0.8f);
                e.outline.enabled = selected;
                e.button.interactable = canBuild;
            }

            TowerData focus = build.SelectedType;
            if (full && canBuild)
                description.text = $"You have all the towers you can have at age {GameManager.Instance.Age}. One more at age {LifetimeManager.NextCapacityAge(GameManager.Instance.Age)}.\nUpgrade the ones you have.";
            else if (focus != null)
                description.text = $"<b>{focus.displayName}</b>\n{focus.description}\n\nClick a glowing pad to build it.";
            else if (build.SelectedTower == null && canBuild)
                description.text = "Click a pad on the map to open the build ring, or pick a tower here first.\nEvery tower you build costs you seconds.";
            else
                description.text = "";
        }
    }
}
