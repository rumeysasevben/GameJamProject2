using System;
using UnityEngine;
using UnityEngine.UI;

namespace TenCandles
{
    // Small helpers so the HUD can be built from code.
    public static class UIFactory
    {
        public static UISkin Skin { get; private set; }
        static Font font;

        public static void Init(UISkin skin)
        {
            Skin = skin != null ? skin : ScriptableObject.CreateInstance<UISkin>();
            font = Skin.font != null ? Skin.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        public static Canvas Canvas(string name, Transform parent, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        // Fixed-size element pinned to an anchor point.
        public static RectTransform Place(this RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        // Stretch to fill the parent with insets (left, bottom, right, top).
        public static RectTransform Fill(this RectTransform rt, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static Image Image(Transform parent, string name, Color color, Sprite sprite = null)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            return img;
        }

        public static Image Panel(Transform parent, string name, Color color)
        {
            var img = Image(parent, name, color, Skin.panel);
            if (Skin.panel != null) img.type = UnityEngine.UI.Image.Type.Sliced;
            return img;
        }

        public static Text Label(Transform parent, string name, string value, int size, TextAnchor align, Color? color = null, FontStyle style = FontStyle.Normal)
        {
            var rt = Rect(name, parent);
            var text = rt.gameObject.AddComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = Mathf.Max(8, Mathf.RoundToInt(size * Skin.fontScale));
            text.alignment = align;
            // Display fonts are already heavy; faking bold on top of them smears the letters.
            text.fontStyle = Skin.font != null ? FontStyle.Normal : style;
            text.color = color ?? Skin.text;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.supportRichText = true;
            return text;
        }

        public static Button Button(Transform parent, string name, Color color, Action onClick)
        {
            var img = Panel(parent, name, color);
            var button = img.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.normalColor = new Color(0.9f, 0.9f, 0.9f);
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
            colors.selectedColor = colors.normalColor;
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(() =>
            {
                // Drop focus so SPACE (call wave early) doesn't "submit" the last clicked button again.
                if (UnityEngine.EventSystems.EventSystem.current != null)
                    UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
                onClick?.Invoke();
            });
            return button;
        }

        public static Outline AddOutline(Graphic graphic, Color color, float distance)
        {
            var outline = graphic.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(distance, -distance);
            return outline;
        }

        public static string FormatSeconds(float seconds)
        {
            seconds = Mathf.Max(0f, seconds);
            int total = Mathf.CeilToInt(seconds);
            return $"{total / 60:00}:{total % 60:00}";
        }
    }
}
