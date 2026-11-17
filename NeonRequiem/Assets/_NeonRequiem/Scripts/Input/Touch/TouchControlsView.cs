using NeonRequiem.Core;
using NeonRequiem.UI;
using UnityEngine;
using UnityEngine.UI;

namespace NeonRequiem.Controls
{
    /// <summary>
    /// The on-screen gameplay controls: floating movement stick on one side, camera drag area and
    /// action buttons on the other. Layout is data (reference-resolution offsets below) and is
    /// re-applied whenever settings change, which is how size, opacity and the left-handed
    /// mirror are supported without separate prefabs. Acts as an input source for the router.
    /// </summary>
    public sealed class TouchControlsView : MonoBehaviour, IPlayerInputSource
    {
        /// <summary>Holding the dodge button longer than this turns the hold into a sprint.</summary>
        public const float SprintHoldSeconds = 0.28f;

        // Layout in reference units (1920x1080, scaled with screen height). Offsets are measured
        // from the bottom corner of the side the control lives on; x is mirrored for left-handed play.
        private const float MoveZoneWidth = 0.42f;
        private const float MoveZoneHeight = 0.78f;
        private const float JoystickBaseDiameter = 300f;
        private const float JoystickHandleDiameter = 132f;
        private const float JoystickTravel = 104f;
        private const float JumpDiameter = 184f;
        private const float DodgeDiameter = 152f;
        private const float PauseDiameter = 96f;

        private static readonly Vector2 k_JoystickRest = new Vector2(290f, 260f);
        private static readonly Vector2 k_JumpOffset = new Vector2(210f, 200f);
        private static readonly Vector2 k_DodgeOffset = new Vector2(440f, 150f);
        private static readonly Vector2 k_PauseOffset = new Vector2(84f, 84f);

        private RectTransform m_Root;
        private CanvasGroup m_Group;
        private RectTransform m_MoveZone;
        private RectTransform m_LookZoneRect;
        private RectTransform m_JoystickBase;
        private RectTransform m_JoystickHandle;
        private VirtualJoystick m_Joystick;
        private TouchLookZone m_LookZone;
        private TouchButton m_Jump;
        private TouchButton m_Dodge;
        private TouchButton m_Pause;
        private float m_Opacity = 0.8f;
        private bool m_Visible = true;

        public VirtualJoystick Joystick => m_Joystick;

        public TouchLookZone LookZone => m_LookZone;

        public TouchButton JumpButton => m_Jump;

        public TouchButton DodgeButton => m_Dodge;

        public TouchButton PauseButton => m_Pause;

        public bool IsVisible => m_Visible;

        /// <summary>Builds the controls under <paramref name="parent"/> (normally the HUD safe-area rect).</summary>
        public static TouchControlsView Create(Transform parent)
        {
            RectTransform root = UIFactory.CreateRect("TouchControls", parent);
            UIFactory.Stretch(root);
            var view = root.gameObject.AddComponent<TouchControlsView>();
            view.Build(root);
            return view;
        }

        public void ApplySettings(GameSettings settings)
        {
            if (settings == null || m_Root == null)
            {
                return;
            }

            bool leftHanded = settings.leftHandedLayout;
            float scale = settings.touchControlScale;

            // Movement side and action side swap for left-handed play.
            float moveMinX = leftHanded ? 1f - MoveZoneWidth : 0f;
            float moveMaxX = leftHanded ? 1f : MoveZoneWidth;
            SetAnchors(m_MoveZone, moveMinX, 0f, moveMaxX, MoveZoneHeight);
            SetAnchors(m_LookZoneRect, leftHanded ? 0f : MoveZoneWidth, 0f, leftHanded ? 1f - MoveZoneWidth : 1f, 1f);

            float moveCorner = leftHanded ? 1f : 0f;
            float moveSign = leftHanded ? -1f : 1f;
            var rest = new Vector2(k_JoystickRest.x * moveSign, k_JoystickRest.y) * scale;
            UIFactory.Place(
                m_JoystickBase,
                new Vector2(moveCorner, 0f),
                new Vector2(0.5f, 0.5f),
                rest,
                Vector2.one * (JoystickBaseDiameter * scale));
            m_JoystickHandle.sizeDelta = Vector2.one * (JoystickHandleDiameter * scale);
            m_Joystick.Configure(m_JoystickBase, m_JoystickHandle, JoystickTravel * scale, settings.floatingJoystick, rest);

            float actionCorner = leftHanded ? 0f : 1f;
            float actionSign = leftHanded ? 1f : -1f;
            PlaceButton(m_Jump, new Vector2(actionCorner, 0f), k_JumpOffset, actionSign, 1f, JumpDiameter, scale);
            PlaceButton(m_Dodge, new Vector2(actionCorner, 0f), k_DodgeOffset, actionSign, 1f, DodgeDiameter, scale);
            PlaceButton(m_Pause, new Vector2(actionCorner, 1f), k_PauseOffset, actionSign, -1f, PauseDiameter, 1f);

            m_Opacity = settings.touchControlOpacity;
            ApplyVisibility();
        }

        /// <summary>Shows or hides the controls. Hidden controls release every held input.</summary>
        public void SetVisible(bool visible)
        {
            if (m_Visible == visible)
            {
                return;
            }

            m_Visible = visible;
            ApplyVisibility();
            if (!visible)
            {
                m_Joystick.ResetState();
                m_LookZone.ResetState();
                m_Jump.ResetState();
                m_Dodge.ResetState();
                m_Pause.ResetState();
            }
        }

        public void Sample(ref PlayerInputSample sample)
        {
            if (!m_Visible || m_Joystick == null)
            {
                return;
            }

            bool active = false;

            Vector2 move = m_Joystick.Value;
            if (move.sqrMagnitude > 0f)
            {
                sample.MergeMove(move);
                active = true;
            }

            Vector2 look = m_LookZone.ConsumeDelta();
            if (look.sqrMagnitude > 0f)
            {
                sample.LookTouchPixels += look;
                active = true;
            }

            if (m_Jump.ConsumePress())
            {
                sample.JumpPressed = true;
                active = true;
            }

            if (m_Jump.IsHeld)
            {
                sample.JumpHeld = true;
            }

            if (m_Dodge.ConsumePress())
            {
                sample.DodgePressed = true;
                active = true;
            }

            if (m_Dodge.IsHeld && m_Dodge.HeldSeconds >= SprintHoldSeconds)
            {
                sample.SprintHeld = true;
            }

            if (m_Pause.ConsumePress())
            {
                sample.PausePressed = true;
                active = true;
            }

            if (active || m_Joystick.IsEngaged || m_LookZone.IsEngaged)
            {
                sample.ActiveScheme = InputScheme.Touch;
            }
        }

        private void Build(RectTransform root)
        {
            m_Root = root;
            m_Group = root.gameObject.AddComponent<CanvasGroup>();

            // Sibling order is hit-test order: zones first, buttons last so they sit on top.
            m_LookZoneRect = UIFactory.CreateRect("LookZone", root);
            m_LookZoneRect.gameObject.AddComponent<RaycastZone>();
            m_LookZone = m_LookZoneRect.gameObject.AddComponent<TouchLookZone>();

            m_MoveZone = UIFactory.CreateRect("MoveZone", root);
            m_MoveZone.pivot = Vector2.zero;
            m_MoveZone.gameObject.AddComponent<RaycastZone>();
            m_Joystick = m_MoveZone.gameObject.AddComponent<VirtualJoystick>();

            Image baseImage = UIFactory.CreateImage("JoystickBase", m_MoveZone, ProceduralSprites.Ring, UITheme.TouchIdle, false);
            m_JoystickBase = baseImage.rectTransform;
            Image handleImage = UIFactory.CreateImage("JoystickHandle", m_JoystickBase, ProceduralSprites.Disc, UITheme.TouchIdle, false);
            m_JoystickHandle = handleImage.rectTransform;
            UIFactory.Place(m_JoystickHandle, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * JoystickHandleDiameter);

            m_Jump = CreateButton("JumpButton", LocKeys.HudJump, 34, UITheme.Cyan);
            m_Dodge = CreateButton("DodgeButton", LocKeys.HudDodge, 30, UITheme.Magenta);
            m_Pause = CreateButton("PauseButton", LocKeys.HudPause, 34, UITheme.TextPrimary);
        }

        private TouchButton CreateButton(string objectName, string labelKey, int fontSize, Color accent)
        {
            var idle = new Color(accent.r, accent.g, accent.b, 0.42f);
            var pressed = new Color(accent.r, accent.g, accent.b, 0.95f);
            Image image = UIFactory.CreateImage(objectName, m_Root, ProceduralSprites.Disc, idle, true);
            Text label = UIFactory.CreateText("Label", image.transform, Loc.Get(labelKey), fontSize, TextAnchor.MiddleCenter, UITheme.TextPrimary);
            label.fontStyle = FontStyle.Bold;
            UIFactory.Stretch(label.rectTransform);

            var button = image.gameObject.AddComponent<TouchButton>();
            button.Configure(image, idle, pressed);
            return button;
        }

        private static void PlaceButton(TouchButton button, Vector2 anchor, Vector2 offset, float signX, float signY, float diameter, float scale)
        {
            // Never let a scaled-down control fall below the minimum comfortable touch target.
            float size = Mathf.Max(UITheme.MinTouchTarget, diameter * scale);
            var position = new Vector2(offset.x * signX, offset.y * signY) * scale;
            UIFactory.Place((RectTransform)button.transform, anchor, new Vector2(0.5f, 0.5f), position, Vector2.one * size);
        }

        private static void SetAnchors(RectTransform rect, float minX, float minY, float maxX, float maxY)
        {
            rect.anchorMin = new Vector2(minX, minY);
            rect.anchorMax = new Vector2(maxX, maxY);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void ApplyVisibility()
        {
            if (m_Group == null)
            {
                return;
            }

            m_Group.alpha = m_Visible ? m_Opacity : 0f;
            m_Group.blocksRaycasts = m_Visible;
            m_Group.interactable = m_Visible;
        }
    }
}
