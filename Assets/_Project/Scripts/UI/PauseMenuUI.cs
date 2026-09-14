using UnityEngine;
using UnityEngine.UI;

namespace TenCandles
{
    // ESC during a game: first it cancels whatever you are building, then it pauses.
    public class PauseMenuUI : MonoBehaviour
    {
        RectTransform panel;
        UISkin skin;

        public void Build(RectTransform root)
        {
            skin = UIFactory.Skin;
            panel = UIFactory.Image(root, "PauseMenu", skin.overlay).rectTransform;
            panel.Fill();

            var title = UIFactory.Label(panel, "Title", "PAUSED", 100, TextAnchor.MiddleCenter, skin.accent);
            title.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 250f), new Vector2(1200f, 140f));
            UIFactory.AddOutline(title, new Color(0f, 0f, 0f, 0.8f), 5f);

            var note = UIFactory.Label(panel, "Note", $"{Story.HeroPossessive} candles wait for you.", 30, TextAnchor.MiddleCenter, skin.mutedText);
            note.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 160f), new Vector2(1200f, 50f));

            Add("RESUME  <size=20>[ESC]</size>", 40f, () => SetOpen(false), true);
            Add("RESTART", -70f, () => GameManager.Instance.Restart(), false);
            Add("MAIN MENU", -180f, () => GameManager.Instance.BackToMenu(), false);

            panel.gameObject.SetActive(false);
        }

        void Add(string label, float y, System.Action onClick, bool primary)
        {
            var button = UIFactory.Button(panel, label, skin.panelLight, onClick);
            button.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(460f, 90f));
            if (primary) UIFactory.AddOutline((Image)button.targetGraphic, skin.accent, 4f);
            UIFactory.Label(button.transform, "Label", label, 34, TextAnchor.MiddleCenter, primary ? skin.accent : skin.text).rectTransform.Fill();
        }

        static bool CanPause
        {
            get
            {
                var gm = GameManager.Instance;
                return gm != null && (gm.State == GameState.Intermission || gm.State == GameState.Wave);
            }
        }

        void SetOpen(bool open)
        {
            open &= CanPause;
            panel.gameObject.SetActive(open);
            TimeControl.SetMenuPaused(open);
        }

        void Update()
        {
            if (panel == null) return;
            bool open = panel.gameObject.activeSelf;

            // Never leave the game frozen if the state changes underneath (e.g. the clock ran out).
            if (open && !CanPause)
            {
                SetOpen(false);
                return;
            }

            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            if (open)
            {
                SetOpen(false);
                return;
            }

            var build = BuildManager.Instance;
            if (build != null && (build.SelectedType != null || build.SelectedTower != null || build.RingSpot != null))
                build.ClearSelection();
            else
                SetOpen(true);
        }
    }
}
