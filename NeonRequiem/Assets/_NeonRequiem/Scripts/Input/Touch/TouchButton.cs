using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NeonRequiem.Controls
{
    /// <summary>
    /// On-screen action button. Reports the press on pointer-down (not on release) so actions
    /// fire with the lowest possible latency, and exposes how long it has been held so one
    /// button can serve tap and hold actions (dodge / sprint).
    /// </summary>
    public sealed class TouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private const int NoPointer = int.MinValue;
        private const float PressedScale = 0.92f;

        private Graphic m_Graphic;
        private Color m_IdleColor = Color.white;
        private Color m_PressedColor = Color.white;
        private int m_PointerId = NoPointer;
        private bool m_PressLatch;
        private float m_PressedAt;

        public bool IsHeld => m_PointerId != NoPointer;

        /// <summary>Seconds the button has been held, in unscaled time; zero when not held.</summary>
        public float HeldSeconds => IsHeld ? Time.unscaledTime - m_PressedAt : 0f;

        public void Configure(Graphic graphic, Color idleColor, Color pressedColor)
        {
            m_Graphic = graphic;
            m_IdleColor = idleColor;
            m_PressedColor = pressedColor;
            ApplyVisual(false);
        }

        /// <summary>True once per press. Reading clears the latch.</summary>
        public bool ConsumePress()
        {
            bool pressed = m_PressLatch;
            m_PressLatch = false;
            return pressed;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (m_PointerId != NoPointer)
            {
                return;
            }

            m_PointerId = eventData.pointerId;
            m_PressedAt = Time.unscaledTime;
            m_PressLatch = true;
            ApplyVisual(true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != m_PointerId)
            {
                return;
            }

            m_PointerId = NoPointer;
            ApplyVisual(false);
        }

        public void ResetState()
        {
            m_PointerId = NoPointer;
            m_PressLatch = false;
            ApplyVisual(false);
        }

        private void OnDisable()
        {
            ResetState();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                ResetState();
            }
        }

        private void ApplyVisual(bool pressed)
        {
            if (m_Graphic != null)
            {
                m_Graphic.color = pressed ? m_PressedColor : m_IdleColor;
            }

            float scale = pressed ? PressedScale : 1f;
            transform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
