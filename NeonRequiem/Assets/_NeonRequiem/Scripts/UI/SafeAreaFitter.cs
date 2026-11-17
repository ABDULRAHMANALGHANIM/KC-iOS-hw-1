using UnityEngine;

namespace NeonRequiem.UI
{
    /// <summary>Pure safe-area math, unit tested without the engine.</summary>
    public static class SafeAreaMath
    {
        /// <summary>
        /// Converts a safe-area rectangle in pixels into normalized anchors.
        /// Returns false (and full-screen anchors) when the inputs are unusable.
        /// </summary>
        public static bool TryComputeAnchors(Rect safeArea, int screenWidth, int screenHeight, out Vector2 anchorMin, out Vector2 anchorMax)
        {
            anchorMin = Vector2.zero;
            anchorMax = Vector2.one;
            if (screenWidth <= 0 || screenHeight <= 0 || safeArea.width <= 0f || safeArea.height <= 0f)
            {
                return false;
            }

            float minX = Mathf.Clamp01(safeArea.xMin / screenWidth);
            float minY = Mathf.Clamp01(safeArea.yMin / screenHeight);
            float maxX = Mathf.Clamp01(safeArea.xMax / screenWidth);
            float maxY = Mathf.Clamp01(safeArea.yMax / screenHeight);
            if (maxX <= minX || maxY <= minY)
            {
                return false;
            }

            anchorMin = new Vector2(minX, minY);
            anchorMax = new Vector2(maxX, maxY);
            return true;
        }
    }

    /// <summary>
    /// Keeps a RectTransform inside the device safe area (notches, rounded corners, home
    /// indicator). Re-applies whenever the safe area or resolution changes, e.g. on rotation.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform m_Rect;
        private Rect m_AppliedSafeArea;
        private int m_AppliedWidth;
        private int m_AppliedHeight;

        private void Awake()
        {
            m_Rect = (RectTransform)transform;
        }

        private void OnEnable()
        {
            Apply();
        }

        private void Update()
        {
            if (Screen.safeArea != m_AppliedSafeArea || Screen.width != m_AppliedWidth || Screen.height != m_AppliedHeight)
            {
                Apply();
            }
        }

        private void Apply()
        {
            if (m_Rect == null)
            {
                m_Rect = (RectTransform)transform;
            }

            m_AppliedSafeArea = Screen.safeArea;
            m_AppliedWidth = Screen.width;
            m_AppliedHeight = Screen.height;

            SafeAreaMath.TryComputeAnchors(m_AppliedSafeArea, m_AppliedWidth, m_AppliedHeight, out Vector2 min, out Vector2 max);
            m_Rect.anchorMin = min;
            m_Rect.anchorMax = max;
            m_Rect.offsetMin = Vector2.zero;
            m_Rect.offsetMax = Vector2.zero;
        }
    }
}
