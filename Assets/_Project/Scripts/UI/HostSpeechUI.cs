using UnityEngine;
using UnityEngine.UI;

namespace TenCandles
{
    // Speech bubble over Farum's head.
    public class HostSpeechUI : MonoBehaviour
    {
        const float Width = 380f;
        const float FadeSeconds = 0.2f;

        RectTransform canvas;
        RectTransform bubble;
        CanvasGroup group;
        Text text;
        string shown;
        float shownAt;

        public void Build(RectTransform root)
        {
            var skin = UIFactory.Skin;
            canvas = root;
            var panel = UIFactory.Panel(root, "Speech", skin.panelColor);
            panel.raycastTarget = false;
            bubble = panel.rectTransform;
            bubble.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(Width, 70f));
            UIFactory.AddOutline(panel, skin.accent, 2f);
            group = bubble.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;

            text = UIFactory.Label(bubble, "Line", "", 20, TextAnchor.MiddleCenter, skin.text);
            text.rectTransform.Fill(12f, 6f, 12f, 6f);
            bubble.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            var host = BirthdayHost.Instance;
            var cam = Camera.main;
            if (bubble == null || host == null || cam == null) return;

            float now = Time.unscaledTime;
            bool talking = now < host.LineUntil && !string.IsNullOrEmpty(host.Line);
            bubble.gameObject.SetActive(talking);
            if (!talking) return;

            if (host.Line != shown)
            {
                shown = host.Line;
                shownAt = now;
                text.text = shown;
                bubble.sizeDelta = new Vector2(Width, Mathf.Max(56f, text.preferredHeight + 18f));
            }

            float fadeIn = Mathf.Clamp01((now - shownAt) / FadeSeconds);
            float fadeOut = Mathf.Clamp01((host.LineUntil - now) / FadeSeconds);
            group.alpha = Mathf.Min(fadeIn, fadeOut);
            bubble.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, fadeIn);

            Vector2 screen = cam.WorldToScreenPoint(host.HeadPosition);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, screen, null, out Vector2 local);
            // Keep the bubble between the side panels and under the top bar.
            Vector2 half = canvas.rect.size * 0.5f;
            float minX = -half.x + HUD.SidePanelWidth + Width * 0.5f + 10f;
            float maxX = half.x - HUD.SidePanelWidth - Width * 0.5f - 10f;
            local.x = Mathf.Clamp(local.x, minX, maxX);
            local.y = Mathf.Min(local.y + 16f, half.y - HUD.TopBarHeight - bubble.sizeDelta.y - 10f);
            bubble.anchoredPosition = local;
        }
    }
}
