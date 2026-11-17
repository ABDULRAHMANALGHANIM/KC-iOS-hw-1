using NeonRequiem.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NeonRequiem.Controls
{
    /// <summary>
    /// On-screen movement stick. The component sits on a large invisible zone; with the floating
    /// option the stick re-centres under the thumb wherever the zone is first touched, which is
    /// more forgiving than a fixed stick on glass. Tracks exactly one pointer.
    /// </summary>
    public sealed class VirtualJoystick : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IPointerUpHandler, IInitializePotentialDragHandler
    {
        public const float DefaultDeadZone = 0.12f;

        private const int NoPointer = int.MinValue;

        private RectTransform m_Zone;
        private RectTransform m_Base;
        private RectTransform m_Handle;
        private float m_Radius = 100f;
        private float m_DeadZone = DefaultDeadZone;
        private bool m_Floating = true;
        private Vector2 m_RestPosition;
        private int m_PointerId = NoPointer;
        private Vector2 m_Value;

        /// <summary>Stick value after dead zone, magnitude in [0, 1].</summary>
        public Vector2 Value => m_Value;

        public bool IsEngaged => m_PointerId != NoPointer;

        /// <param name="stickBase">Visual base, a child of this zone.</param>
        /// <param name="handle">Visual thumb, a child of <paramref name="stickBase"/> anchored at its centre.</param>
        /// <param name="radius">Handle travel in canvas units.</param>
        /// <param name="restPosition">Anchored position of the base while idle.</param>
        public void Configure(RectTransform stickBase, RectTransform handle, float radius, bool floating, Vector2 restPosition)
        {
            m_Zone = (RectTransform)transform;
            m_Base = stickBase;
            m_Handle = handle;
            m_Radius = Mathf.Max(1f, radius);
            m_Floating = floating;
            m_RestPosition = restPosition;
            ResetState();
        }

        /// <summary>Converts a handle offset into a stick value. Pure; unit tested.</summary>
        public static Vector2 ComputeValue(Vector2 offset, float radius, float deadZone)
        {
            if (radius <= 0f)
            {
                return Vector2.zero;
            }

            Vector2 normalized = Vector2.ClampMagnitude(offset / radius, 1f);
            return NRMath.ApplyRadialDeadZone(normalized, deadZone);
        }

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            // A stick must respond to the first pixel of movement, not after the UI drag threshold.
            eventData.useDragThreshold = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (m_PointerId != NoPointer || m_Base == null)
            {
                return;
            }

            if (!TryGetLocalPoint(eventData, out Vector2 local))
            {
                return;
            }

            m_PointerId = eventData.pointerId;
            if (m_Floating)
            {
                m_Base.anchoredPosition = local - AnchorReference();
            }

            UpdateHandle(local);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != m_PointerId)
            {
                return;
            }

            if (TryGetLocalPoint(eventData, out Vector2 local))
            {
                UpdateHandle(local);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId == m_PointerId)
            {
                ResetState();
            }
        }

        /// <summary>Releases the stick and returns it to its rest position.</summary>
        public void ResetState()
        {
            m_PointerId = NoPointer;
            m_Value = Vector2.zero;
            if (m_Base != null)
            {
                m_Base.anchoredPosition = m_RestPosition;
            }

            if (m_Handle != null)
            {
                m_Handle.anchoredPosition = Vector2.zero;
            }
        }

        private void OnDisable()
        {
            ResetState();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            // The pointer-up event is lost when the app is backgrounded mid-touch.
            if (!hasFocus)
            {
                ResetState();
            }
        }

        private void UpdateHandle(Vector2 localPoint)
        {
            Vector2 baseCentre = m_Base.anchoredPosition + AnchorReference();
            Vector2 offset = Vector2.ClampMagnitude(localPoint - baseCentre, m_Radius);
            m_Handle.anchoredPosition = offset;
            m_Value = ComputeValue(offset, m_Radius, m_DeadZone);
        }

        /// <summary>The base's anchor point expressed in this zone's local space.</summary>
        private Vector2 AnchorReference()
        {
            Rect rect = m_Zone.rect;
            return rect.min + Vector2.Scale(m_Base.anchorMin, rect.size);
        }

        private bool TryGetLocalPoint(PointerEventData eventData, out Vector2 local)
        {
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                m_Zone, eventData.position, eventData.pressEventCamera, out local);
        }
    }
}
