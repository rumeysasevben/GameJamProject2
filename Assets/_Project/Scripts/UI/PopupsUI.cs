using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TenCandles
{
    // Floating damage numbers, kill rewards, and big time gains/losses under the candle bar.
    public class PopupsUI : MonoBehaviour
    {
        const int MaxPopups = 48;

        class Popup
        {
            public Text text;
            public Vector3 world;
            public bool followsWorld;
            public Vector2 screenOffset;
            public float bornAt;
            public float life;
            public float rise;
            public float scale;
        }

        readonly List<Popup> live = new List<Popup>();
        readonly Stack<Popup> free = new Stack<Popup>();
        RectTransform layer;
        Camera cam;
        UISkin skin;

        public void Build(RectTransform root)
        {
            skin = UIFactory.Skin;
            layer = UIFactory.Rect("Popups", root);
            layer.Fill();
            for (int i = 0; i < MaxPopups; i++)
            {
                var t = UIFactory.Label(layer, "Popup", "", 26, TextAnchor.MiddleCenter, skin.text, FontStyle.Bold);
                t.rectTransform.sizeDelta = new Vector2(600f, 60f);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                UIFactory.AddOutline(t, new Color(0f, 0f, 0f, 0.75f), 2f);
                t.gameObject.SetActive(false);
                free.Push(new Popup { text = t });
            }
        }

        void OnEnable()
        {
            GameEvents.EnemyDamaged += OnDamaged;
            GameEvents.EnemyKilled += OnKilled;
            GameEvents.TimeAdjusted += OnTimeAdjusted;
        }

        void OnDisable()
        {
            GameEvents.EnemyDamaged -= OnDamaged;
            GameEvents.EnemyKilled -= OnKilled;
            GameEvents.TimeAdjusted -= OnTimeAdjusted;
        }

        const float DamagePopupInterval = 0.35f;
        readonly Dictionary<Enemy, float> lastDamagePopup = new Dictionary<Enemy, float>();
        int bannerSlot;

        void OnDamaged(Enemy enemy, float amount, bool crit)
        {
            // Splash hits a crowd many times a second; one number per guest keeps it readable.
            float now = Time.unscaledTime;
            if (lastDamagePopup.TryGetValue(enemy, out float last) && now - last < DamagePopupInterval && !crit) return;
            lastDamagePopup[enemy] = now;

            Vector3 p = enemy.transform.position + new Vector3(Random.Range(-0.25f, 0.25f), 0.45f, 0f);
            if (crit) SpawnWorld(p, $"{amount:0}!", skin.accent, 34, 0.8f);
            else SpawnWorld(p, Mathf.Max(1f, Mathf.Round(amount)).ToString("0"), skin.text, 20, 0.45f);
        }

        void OnKilled(Enemy enemy, float reward)
        {
            lastDamagePopup.Remove(enemy);
            float total = reward + StatRegistry.KillBonus + enemy.Data.killBonusTime;
            SpawnWorld(enemy.transform.position + Vector3.up * 0.8f, $"+{total:0.0} s", skin.good, 24, 0.9f);
        }

        // Big time changes line up under the candle bar, newest below the previous one.
        void OnTimeAdjusted(float delta, string reason)
        {
            string sign = delta >= 0f ? "+" : "−";
            Color color = delta >= 0f ? skin.good : skin.danger;
            int size = Mathf.Abs(delta) >= 20f ? 40 : 30;
            if (!live.Exists(x => !x.followsWorld)) bannerSlot = 0;
            var p = Spawn($"{sign}{Mathf.Abs(delta):0.#} s  <size=22>{reason}</size>", color, size, 1.6f);
            if (p == null) return;
            p.followsWorld = false;
            p.screenOffset = new Vector2(-300f, -HUD.TopBarHeight - 50f - (bannerSlot++ % 4) * 46f);
            p.rise = 20f;
        }

        void SpawnWorld(Vector3 world, string value, Color color, int size, float life)
        {
            var p = Spawn(value, color, size, life);
            if (p == null) return;
            p.followsWorld = true;
            p.world = world;
            p.rise = 50f;
        }

        Popup Spawn(string value, Color color, int size, float life)
        {
            if (free.Count == 0 || layer == null) return null;
            var p = free.Pop();
            p.text.text = value;
            p.text.color = color;
            p.text.fontSize = size;
            p.bornAt = Time.unscaledTime;
            p.life = life;
            p.scale = 1f;
            p.text.gameObject.SetActive(true);
            live.Add(p);
            return p;
        }

        void LateUpdate()
        {
            if (layer == null) return;
            if (cam == null) cam = Camera.main;

            for (int i = live.Count - 1; i >= 0; i--)
            {
                Popup p = live[i];
                float t = (Time.unscaledTime - p.bornAt) / p.life;
                if (t >= 1f)
                {
                    p.text.gameObject.SetActive(false);
                    live.RemoveAt(i);
                    free.Push(p);
                    continue;
                }

                Vector2 pos;
                if (p.followsWorld && cam != null)
                {
                    Vector3 screen = cam.WorldToScreenPoint(p.world);
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screen, null, out pos);
                }
                else
                {
                    pos = new Vector2(layer.rect.width * 0.5f, layer.rect.height * 0.5f) * new Vector2(0f, 1f) + p.screenOffset;
                }

                p.text.rectTransform.anchoredPosition = pos + Vector2.up * (p.rise * EaseOut(t));
                float pop = t < 0.12f ? Mathf.Lerp(1.5f, 1f, t / 0.12f) : 1f;
                p.text.rectTransform.localScale = Vector3.one * pop;
                var c = p.text.color;
                c.a = t > 0.65f ? 1f - (t - 0.65f) / 0.35f : 1f;
                p.text.color = c;
            }
        }

        static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
    }
}
