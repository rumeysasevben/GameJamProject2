using UnityEngine;
using UnityEngine.UI;

namespace TenCandles
{
    // Right panel, bottom half: the selected tower and its upgrade button.
    public class TowerInfoUI : MonoBehaviour
    {
        RectTransform panel;
        Text title;
        Text stats;
        Button upgrade;
        Text upgradeLabel;
        UISkin skin;

        public void Build(RectTransform root)
        {
            skin = UIFactory.Skin;
            panel = UIFactory.Panel(root, "TowerInfo", skin.panelColor).rectTransform;
            panel.anchorMin = new Vector2(1f, 0f);
            panel.anchorMax = new Vector2(1f, 0f);
            panel.pivot = new Vector2(1f, 0f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(HUD.SidePanelWidth, 440f);

            title = UIFactory.Label(panel, "Title", "", 28, TextAnchor.MiddleCenter, skin.accent, FontStyle.Bold);
            title.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(230f, 70f));

            stats = UIFactory.Label(panel, "Stats", "", 21, TextAnchor.UpperLeft, skin.text);
            stats.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(214f, 180f));

            upgrade = UIFactory.Button(panel, "Upgrade", skin.panelLight, () =>
            {
                var b = BuildManager.Instance;
                if (b != null && b.SelectedTower != null) b.TryUpgrade(b.SelectedTower);
            });
            upgrade.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(222f, 110f));
            upgradeLabel = UIFactory.Label(upgrade.transform, "Label", "", 24, TextAnchor.MiddleCenter, skin.text, FontStyle.Bold);
            upgradeLabel.rectTransform.Fill(6f, 6f, 6f, 6f);
        }

        void Update()
        {
            var build = BuildManager.Instance;
            if (build == null || panel == null) return;

            Tower t = build.SelectedTower;
            panel.gameObject.SetActive(t != null);
            if (t == null) return;

            title.text = $"{t.Data.displayName}\n<size=20>Level {t.Level} / {TowerData.MaxLevel}</size>";

            string extra = "";
            if (t.SplashRadius > 0f) extra += $"\nSplash  {t.SplashRadius:0.0}";
            if (t.SlowPercent > 0f) extra += $"\nSlow  {t.SlowPercent * 100f:0}%";
            if (t.BurnDps > 0f) extra += $"\nBurn  {t.BurnDps:0.0}/s";
            stats.text = $"Damage  {t.Damage:0.0}\nShots  {1f / t.FireInterval:0.00}/s\nRange  {t.Range:0.0}{extra}";

            if (t.IsMaxLevel)
            {
                upgrade.interactable = false;
                ((Image)upgrade.targetGraphic).color = skin.disabled;
                upgradeLabel.text = "MAX LEVEL";
                upgradeLabel.color = skin.mutedText;
                return;
            }

            float cost = build.UpgradeCost(t);
            bool affordable = build.CanAfford(cost);
            upgrade.interactable = build.CanBuildNow;
            ((Image)upgrade.targetGraphic).color = affordable && build.CanBuildNow ? skin.panelLight : skin.disabled;
            string costColor = ColorUtility.ToHtmlStringRGB(affordable ? skin.accent : skin.danger);
            upgradeLabel.text = $"UPGRADE [U]\n<color=#{costColor}>{cost:0} s</color>";
            upgradeLabel.color = skin.text;
        }
    }
}
