using NeonRequiem.Core;
using UnityEngine;

namespace NeonRequiem.Player
{
    /// <summary>
    /// Remembers recent places where the character stood on stable ground, so a fall out of the
    /// world can be undone by returning to solid footing instead of the level start. Echo Shift
    /// will reuse it for safe placement when geometry changes under the player.
    /// </summary>
    public sealed class SafePositionTracker
    {
        private struct Entry
        {
            public Vector3 Position;
            public float Yaw;
            public float Time;
        }

        private readonly CircularBuffer<Entry> m_History;
        private readonly float m_SampleInterval;
        private float m_Clock;
        private float m_TimeSinceSample;

        public SafePositionTracker(int capacity = 16, float sampleInterval = 0.25f)
        {
            m_History = new CircularBuffer<Entry>(capacity);
            m_SampleInterval = Mathf.Max(0.01f, sampleInterval);
            m_TimeSinceSample = m_SampleInterval;
        }

        public int Count => m_History.Count;

        /// <summary>Advances time and records the position when the character is on stable ground.</summary>
        public void Tick(bool onStableGround, Vector3 position, float yaw, float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            m_Clock += deltaTime;
            m_TimeSinceSample += deltaTime;
            if (!onStableGround || m_TimeSinceSample < m_SampleInterval)
            {
                return;
            }

            m_TimeSinceSample = 0f;
            m_History.PushBack(new Entry { Position = position, Yaw = yaw, Time = m_Clock });
        }

        /// <summary>
        /// Returns the most recent recorded position that is at least <paramref name="minimumAge"/>
        /// seconds old (so a spot recorded a moment before the fall is skipped), or the oldest
        /// one when none is old enough.
        /// </summary>
        public bool TryGetSafePosition(float minimumAge, out Vector3 position, out float yaw)
        {
            for (int i = m_History.Count - 1; i >= 0; i--)
            {
                Entry entry = m_History[i];
                if (m_Clock - entry.Time >= minimumAge)
                {
                    position = entry.Position;
                    yaw = entry.Yaw;
                    return true;
                }
            }

            if (m_History.Count > 0)
            {
                Entry oldest = m_History.Oldest;
                position = oldest.Position;
                yaw = oldest.Yaw;
                return true;
            }

            position = Vector3.zero;
            yaw = 0f;
            return false;
        }

        public void Clear()
        {
            m_History.Clear();
            m_TimeSinceSample = m_SampleInterval;
        }
    }
}
