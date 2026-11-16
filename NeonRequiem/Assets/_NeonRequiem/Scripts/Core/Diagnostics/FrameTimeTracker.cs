using System;

namespace NeonRequiem.Core
{
    /// <summary>
    /// Rolling window of frame durations with average and percentile queries. Percentiles matter
    /// more than averages for perceived smoothness, so adaptive quality is driven by p95.
    /// Allocation-free after construction.
    /// </summary>
    public sealed class FrameTimeTracker
    {
        /// <summary>Samples longer than this are treated as hitches (loading, app resume) and ignored.</summary>
        public const float MaxAcceptedSampleSeconds = 0.5f;

        private readonly CircularBuffer<float> m_Samples;
        private readonly float[] m_Scratch;

        public FrameTimeTracker(int windowSize = 240)
        {
            m_Samples = new CircularBuffer<float>(windowSize);
            m_Scratch = new float[windowSize];
        }

        public int SampleCount => m_Samples.Count;

        public int WindowSize => m_Samples.Capacity;

        /// <summary>Records one frame duration in seconds. Invalid or hitch samples are rejected.</summary>
        /// <returns>True when the sample was accepted.</returns>
        public bool AddSample(float seconds)
        {
            if (float.IsNaN(seconds) || seconds <= 0f || seconds > MaxAcceptedSampleSeconds)
            {
                return false;
            }

            m_Samples.PushBack(seconds);
            return true;
        }

        public float AverageSeconds
        {
            get
            {
                int count = m_Samples.Count;
                if (count == 0)
                {
                    return 0f;
                }

                double sum = 0d;
                for (int i = 0; i < count; i++)
                {
                    sum += m_Samples[i];
                }

                return (float)(sum / count);
            }
        }

        public float WorstSeconds
        {
            get
            {
                float worst = 0f;
                int count = m_Samples.Count;
                for (int i = 0; i < count; i++)
                {
                    float sample = m_Samples[i];
                    if (sample > worst)
                    {
                        worst = sample;
                    }
                }

                return worst;
            }
        }

        /// <summary>Nearest-rank percentile, e.g. 0.95 for p95. Returns 0 when there are no samples.</summary>
        public float GetPercentileSeconds(float percentile01)
        {
            int count = m_Samples.CopyTo(m_Scratch);
            if (count == 0)
            {
                return 0f;
            }

            Array.Sort(m_Scratch, 0, count);
            float clamped = percentile01 < 0f ? 0f : (percentile01 > 1f ? 1f : percentile01);
            int rank = (int)Math.Ceiling(clamped * count);
            int index = Math.Min(count - 1, Math.Max(0, rank - 1));
            return m_Scratch[index];
        }

        public void Clear()
        {
            m_Samples.Clear();
        }
    }
}
