using UnityEngine;
using UnityEngine.UI;

namespace TenCandles
{
    // One line of guidance at the bottom of the play area, plus short warnings.
    public class HintUI : MonoBehaviour
    {
        const float WarningSeconds = 1.8f;

        Text label;
        string warning;
        float warningUntil;
        UISkin skin;

        public void Build(RectTransform root)
        {
            skin = UIFactory.Skin;
            label = UIFactory.Label(root, "Hint", "", 28, TextAnchor.MiddleCenter, skin.text, FontStyle.Bold);
            label.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1200f, 50f));
            UIFactory.AddOutline(label, new Color(0f, 0f, 0f, 0.8f), 2f);
        }

        void OnEnable() => GameEvents.BuildFailed += OnBuildFailed;
        void OnDisable() => GameEvents.BuildFailed -= OnBuildFailed;

        void OnBuildFailed(string reason)
        {
            warning = reason;
            warningUntil = Time.unscaledTime + WarningSeconds;
        }

        void Update()
        {
            if (label == null) return;

            if (Time.unscaledTime < warningUntil)
            {
                label.text = warning;
                label.color = skin.danger;
                return;
            }

            label.color = skin.text;
            if (!GameSettings.Hints)
            {
                label.text = "";
                return;
            }
            var gm = GameManager.Instance;
            var build = BuildManager.Instance;
            if (gm == null || build == null)
            {
                label.text = "";
                return;
            }

            bool early = gm.Age <= 1 && build.TowersBuilt == 0;
            if (early && (gm.State == GameState.Intermission || gm.State == GameState.Wave))
                label.text = build.SelectedType == null
                    ? (build.RingSpot != null ? "Pick your tower from the ring (1-4)" : "Click a glowing pad next to the road to build your first tower")
                    : "Now click a glowing pad next to the road";
            else if (gm.Age == 1 && gm.State == GameState.Intermission)
                label.text = "Press SPACE to call the wave early; you get half the wait back";
            else
                label.text = "";
        }
    }
}
