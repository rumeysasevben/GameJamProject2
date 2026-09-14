using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TenCandles
{
    // Right panel, top half: countdown, enemies left, early call.
    public class WavePanelUI : MonoBehaviour
    {
        Text title;
        Text big;
        Text detail;
        Button early;
        Text earlyLabel;
        UISkin skin;
        RectTransform previewRow;
        readonly List<Image> previewIcons = new List<Image>();
        List<EnemyData> previewTypes;
        int previewYear = -1;
        const int MaxPreview = 5;

        public void Build(RectTransform root)
        {
            skin = UIFactory.Skin;
            var panel = UIFactory.Panel(root, "WavePanel", skin.panelColor).rectTransform;
            panel.anchorMin = new Vector2(1f, 1f);
            panel.anchorMax = new Vector2(1f, 1f);
            panel.pivot = new Vector2(1f, 1f);
            panel.anchoredPosition = new Vector2(0f, -HUD.TopBarHeight);
            panel.sizeDelta = new Vector2(HUD.SidePanelWidth, 320f);

            title = UIFactory.Label(panel, "Title", "", 18, TextAnchor.MiddleCenter, skin.accent);
            title.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(230f, 28f));

            big = UIFactory.Label(panel, "Big", "", 48, TextAnchor.MiddleCenter, skin.text);
            big.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(230f, 60f));

            detail = UIFactory.Label(panel, "Detail", "", 15, TextAnchor.UpperCenter, skin.mutedText);
            detail.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -104f), new Vector2(226f, 60f));

            // Who is coming next year, standing around idly.
            previewRow = UIFactory.Rect("NextWave", panel);
            previewRow.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -102f), new Vector2(230f, 56f));
            for (int i = 0; i < MaxPreview; i++)
            {
                var icon = UIFactory.Image(previewRow, "Enemy" + i, Color.white);
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                previewIcons.Add(icon);
            }

            early = UIFactory.Button(panel, "CallEarly", skin.panelLight, () => GameManager.Instance.CallWaveEarly());
            early.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(222f, 96f));
            UIFactory.AddOutline((Image)early.targetGraphic, skin.accent, 3f);
            earlyLabel = UIFactory.Label(early.transform, "Label", "", 18, TextAnchor.MiddleCenter, skin.text);
            earlyLabel.rectTransform.Fill(8f, 8f, 8f, 8f);
        }

        void ShowPreview(WaveManager waves, int year)
        {
            if (year != previewYear)
            {
                previewYear = year;
                previewTypes = waves.UpcomingTypes(year);
                int n = Mathf.Min(previewTypes.Count, MaxPreview);
                const float size = 50f, gap = 4f;
                float start = -(n * size + (n - 1) * gap) * 0.5f + size * 0.5f;
                for (int i = 0; i < previewIcons.Count; i++)
                {
                    previewIcons[i].gameObject.SetActive(i < n);
                    if (i < n) previewIcons[i].rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(start + i * (size + gap), 0f), new Vector2(size, size));
                }
            }

            float now = Time.unscaledTime;
            for (int i = 0; i < previewTypes.Count && i < MaxPreview; i++)
            {
                EnemyData data = previewTypes[i];
                EnemyAnimation look = data.variants != null && data.variants.Length > 0 ? data.variants[0] : null;
                Sprite[] clip = look == null ? null : EnemyAnimation.Has(look.idle) ? look.idle : look.walk;
                previewIcons[i].sprite = EnemyAnimation.Has(clip) ? clip[Mathf.FloorToInt(now * 12f + i * 3) % clip.Length] : data.sprite;
                // The boss preview is drawn bigger so it reads as a threat.
                previewIcons[i].rectTransform.localScale = Vector3.one * (data.isBoss ? 1.25f : 1f);
            }
        }

        void Update()
        {
            var gm = GameManager.Instance;
            var waves = WaveManager.Instance;
            if (gm == null || waves == null || title == null) return;

            early.gameObject.SetActive(gm.State == GameState.Intermission);
            bool previewing = gm.State == GameState.Intermission;
            previewRow.gameObject.SetActive(previewing);
            detail.rectTransform.anchoredPosition = new Vector2(0f, previewing ? -164f : -104f);

            switch (gm.State)
            {
                case GameState.Boot:
                    title.text = "WELCOME";
                    big.text = "";
                    detail.text = $"{Story.Hero} is counting on you.";
                    break;

                case GameState.Intermission:
                    title.text = $"YEAR {gm.Year} STARTS IN";
                    big.text = Mathf.CeilToInt(gm.IntermissionLeft).ToString();
                    detail.text = "Waiting burns your candles too.";
                    ShowPreview(waves, gm.Year);
                    earlyLabel.text = $"CALL EARLY\n<size=14>[SPACE]</size>\n<color=#{ColorUtility.ToHtmlStringRGB(skin.good)}>+{gm.EarlyCallBonus:0.0} s</color>";
                    break;

                case GameState.Wave:
                    title.text = $"YEAR {gm.Year}";
                    big.text = waves.Remaining.ToString();
                    detail.text = waves.Remaining == 1 ? "enemy left" : "enemies left";
                    break;

                case GameState.LevelUp:
                    title.text = "LEVEL UP";
                    big.text = "";
                    detail.text = "Choose your gift.";
                    break;

                default:
                    title.text = gm.State == GameState.Victory ? "YOU DID IT!" : "LIGHTS OUT";
                    big.text = "";
                    detail.text = "";
                    break;
            }
        }
    }
}
