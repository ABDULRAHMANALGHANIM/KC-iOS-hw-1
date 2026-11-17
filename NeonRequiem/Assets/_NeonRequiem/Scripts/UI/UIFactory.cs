using NeonRequiem.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace NeonRequiem.UI
{
    /// <summary>Shared colours and metrics for the interface.</summary>
    public static class UITheme
    {
        public static readonly Color Cyan = new Color(0.30f, 0.95f, 1.00f, 1f);
        public static readonly Color Magenta = new Color(1.00f, 0.28f, 0.72f, 1f);
        public static readonly Color TextPrimary = new Color(0.93f, 0.96f, 1.00f, 1f);
        public static readonly Color TextMuted = new Color(0.62f, 0.68f, 0.80f, 1f);
        public static readonly Color PanelBackground = new Color(0.035f, 0.045f, 0.085f, 0.94f);
        public static readonly Color ButtonNormal = new Color(0.10f, 0.14f, 0.24f, 0.95f);
        public static readonly Color ButtonHighlighted = new Color(0.16f, 0.24f, 0.40f, 1f);
        public static readonly Color ButtonPressed = new Color(0.22f, 0.55f, 0.70f, 1f);
        public static readonly Color TouchIdle = new Color(0.75f, 0.90f, 1.00f, 0.55f);
        public static readonly Color TouchPressed = new Color(0.30f, 0.95f, 1.00f, 0.95f);
        public static readonly Color Scrim = new Color(0f, 0f, 0f, 0.55f);

        /// <summary>Layout is authored against this resolution and scaled with screen height.</summary>
        public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        /// <summary>Minimum touch target edge in reference units (about 9 mm on a typical phone).</summary>
        public const float MinTouchTarget = 88f;
    }

    /// <summary>
    /// Builds uGUI objects from code. Phase 0-1 UI is assembled procedurally so the project runs
    /// without authored prefabs; these helpers keep that code short and consistent.
    /// </summary>
    public static class UIFactory
    {
        private const string BuiltinFontName = "LegacyRuntime.ttf";

        private static Font s_Font;

        /// <summary>Engine built-in font. PLACEHOLDER: replaced by the licensed UI typeface (see ASSET_REQUIREMENTS.md).</summary>
        public static Font DefaultFont
        {
            get
            {
                if (s_Font == null)
                {
                    s_Font = Resources.GetBuiltinResource<Font>(BuiltinFontName);
                }

                return s_Font;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        {
            s_Font = null;
        }

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = GameLayers.UI;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>Positions a fixed-size rect relative to a single anchor point of its parent.</summary>
        public static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        public static Image CreateImage(string name, Transform parent, Sprite sprite, Color color, bool raycastTarget)
        {
            RectTransform rect = CreateRect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = raycastTarget;
            if (sprite != null && sprite.border.sqrMagnitude > 0f)
            {
                image.type = Image.Type.Sliced;
            }

            return image;
        }

        public static Text CreateText(string name, Transform parent, string content, int fontSize, TextAnchor alignment, Color color)
        {
            RectTransform rect = CreateRect(name, parent);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = DefaultFont;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.text = content;
            text.raycastTarget = false;
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        /// <summary>Creates a rounded button with a centred label. The label is returned for later updates.</summary>
        public static Button CreateButton(string name, Transform parent, string label, int fontSize, UnityAction onClick, out Text labelText)
        {
            Image background = CreateImage(name, parent, ProceduralSprites.RoundedRect, Color.white, true);
            var button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;

            ColorBlock colors = button.colors;
            colors.normalColor = UITheme.ButtonNormal;
            colors.highlightedColor = UITheme.ButtonHighlighted;
            colors.selectedColor = UITheme.ButtonHighlighted;
            colors.pressedColor = UITheme.ButtonPressed;
            colors.disabledColor = new Color(0.1f, 0.1f, 0.12f, 0.5f);
            colors.fadeDuration = 0.06f;
            button.colors = colors;

            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }

            labelText = CreateText("Label", background.transform, label, fontSize, TextAnchor.MiddleCenter, UITheme.TextPrimary);
            Stretch(labelText.rectTransform);
            return button;
        }

        /// <summary>Creates a screen-space overlay canvas that scales with screen height.</summary>
        public static Canvas CreateCanvas(string name, Transform parent, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = GameLayers.UI;
            go.transform.SetParent(parent, false);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = UITheme.ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        /// <summary>Returns the active EventSystem, creating one driven by the Input System if none exists.</summary>
        public static EventSystem EnsureEventSystem(Transform parent)
        {
            EventSystem existing = EventSystem.current;
            if (existing != null)
            {
                return existing;
            }

            var go = new GameObject("EventSystem");
            go.transform.SetParent(parent, false);
            var eventSystem = go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
            return eventSystem;
        }
    }
}
