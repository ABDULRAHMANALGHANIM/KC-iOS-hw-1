using UnityEngine;
using UnityEngine.EventSystems;

namespace NeonRequiem.Controls
{
    /// <summary>
    /// Invisible drag area that turns the camera. Accumulates the tracked finger's movement in
    /// screen pixels; the input router drains it once per frame. Buttons drawn above the zone
    /// take their own touches, so pressing an action button never nudges the camera.
    /// </summary>
    public sealed class TouchLookZone : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IPointerUpHandler, IInitializePotentialDragHandler
    {
        private const int NoPointer = int.MinValue;

        private int m_PointerId = NoPointer;
        private Vector2 m_LastPosition;
        private Vector2 m_Accumulated;

        public bool IsEngaged => m_PointerId != NoPointer;

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            eventData.useDragThreshold = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (m_PointerId != NoPointer)
            {
                return;
            }

            m_PointerId = eventData.pointerId;
            m_LastPosition = eventData.position;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != m_PointerId)
            {
                return;
            }

            m_Accumulated += eventData.position - m_LastPosition;
            m_LastPosition = eventData.position;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId == m_PointerId)
            {
                m_PointerId = NoPointer;
            }
        }

        /// <summary>Returns the drag since the last call, in pixels, and clears it.</summary>
        public Vector2 ConsumeDelta()
        {
            Vector2 delta = m_Accumulated;
            m_Accumulated = Vector2.zero;
            return delta;
        }

        public void ResetState()
        {
            m_PointerId = NoPointer;
            m_Accumulated = Vector2.zero;
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
    }
}
