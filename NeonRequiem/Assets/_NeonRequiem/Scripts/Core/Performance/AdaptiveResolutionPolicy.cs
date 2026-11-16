using System;

namespace NeonRequiem.Core
{
    /// <summary>Tuning for <see cref="AdaptiveResolutionPolicy"/>.</summary>
    [Serializable]
    public struct AdaptiveResolutionConfig
    {
        /// <summary>Lowest scale factor the policy may select.</summary>
        public float minScale;

        /// <summary>Highest scale factor the policy may select.</summary>
        public float maxScale;

        /// <summary>Scale removed when the frame budget is exceeded.</summary>
        public float stepDown;

        /// <summary>Scale added when probing for headroom.</summary>
        public float stepUp;

        /// <summary>The budget counts as exceeded when p95 frame time is above target times this ratio.</summary>
        public float overBudgetRatio;

        /// <summary>Seconds between decisions.</summary>
        public float decisionInterval;

        /// <summary>Seconds of in-budget frames required before the first attempt to raise the scale.</summary>
        public float initialProbeDelay;

        /// <summary>Upper bound for the probe delay after repeated failed probes.</summary>
        public float maxProbeDelay;

        /// <summary>Frame samples required before a decision is made.</summary>
        public int minSamples;

        public static AdaptiveResolutionConfig Default => new AdaptiveResolutionConfig
        {
            minScale = 0.7f,
            maxScale = 1f,
            stepDown = 0.1f,
            stepUp = 0.05f,
            overBudgetRatio = 1.2f,
            decisionInterval = 1f,
            initialProbeDelay = 8f,
            maxProbeDelay = 64f,
            minSamples = 45,
        };
    }

    /// <summary>
    /// Decides a resolution scale factor from measured frame times.
    /// <para>
    /// Frame pacing hides headroom (a frame capped at 16.6 ms looks the same whether the GPU is
    /// idle or saturated), so the policy cannot read "spare capacity" from delta time. Instead it
    /// steps down as soon as p95 exceeds the budget, and periodically probes one step up after a
    /// sustained in-budget period. A probe that immediately fails doubles the wait before the
    /// next one, which prevents visible oscillation on devices that are thermally limited.
    /// </para>
    /// Pure logic; the caller owns applying <see cref="Scale"/> to the renderer.
    /// </summary>
    public sealed class AdaptiveResolutionPolicy
    {
        private readonly AdaptiveResolutionConfig m_Config;
        private float m_TimeSinceDecision;
        private float m_InBudgetTime;
        private float m_ProbeDelay;
        private bool m_LastChangeWasProbe;

        public AdaptiveResolutionPolicy(AdaptiveResolutionConfig config)
        {
            if (config.minScale <= 0f || config.maxScale < config.minScale)
            {
                throw new ArgumentException("Scale range is invalid.", nameof(config));
            }

            if (config.stepDown <= 0f || config.stepUp <= 0f || config.decisionInterval <= 0f)
            {
                throw new ArgumentException("Steps and decision interval must be positive.", nameof(config));
            }

            m_Config = config;
            Reset();
        }

        /// <summary>Current scale factor in [minScale, maxScale].</summary>
        public float Scale { get; private set; }

        /// <summary>Seconds of in-budget time currently required before a probe.</summary>
        public float CurrentProbeDelay => m_ProbeDelay;

        /// <summary>Returns to the maximum scale and forgets all history.</summary>
        public void Reset()
        {
            Scale = m_Config.maxScale;
            m_TimeSinceDecision = 0f;
            m_InBudgetTime = 0f;
            m_ProbeDelay = m_Config.initialProbeDelay;
            m_LastChangeWasProbe = false;
        }

        /// <summary>
        /// Advances the policy. Call once per frame with the tracker that receives frame samples.
        /// </summary>
        /// <returns>True when <see cref="Scale"/> changed and must be applied. The tracker is cleared on change.</returns>
        public bool Update(FrameTimeTracker tracker, float targetFrameSeconds, float deltaSeconds)
        {
            if (tracker == null)
            {
                throw new ArgumentNullException(nameof(tracker));
            }

            if (targetFrameSeconds <= 0f || deltaSeconds <= 0f || float.IsNaN(deltaSeconds))
            {
                return false;
            }

            m_TimeSinceDecision += deltaSeconds;
            if (m_TimeSinceDecision < m_Config.decisionInterval || tracker.SampleCount < m_Config.minSamples)
            {
                return false;
            }

            float elapsed = m_TimeSinceDecision;
            m_TimeSinceDecision = 0f;

            float p95 = tracker.GetPercentileSeconds(0.95f);
            if (p95 > targetFrameSeconds * m_Config.overBudgetRatio)
            {
                m_InBudgetTime = 0f;
                if (m_LastChangeWasProbe)
                {
                    // The step up did not hold: wait longer before trying again.
                    m_ProbeDelay = Math.Min(m_ProbeDelay * 2f, m_Config.maxProbeDelay);
                    m_LastChangeWasProbe = false;
                }

                float lowered = Math.Max(m_Config.minScale, Scale - m_Config.stepDown);
                if (lowered >= Scale)
                {
                    return false;
                }

                Scale = lowered;
                tracker.Clear();
                return true;
            }

            m_InBudgetTime += elapsed;
            if (m_InBudgetTime < m_ProbeDelay || Scale >= m_Config.maxScale)
            {
                return false;
            }

            if (m_LastChangeWasProbe)
            {
                // The previous probe survived a full delay: the device has headroom again.
                m_ProbeDelay = m_Config.initialProbeDelay;
            }

            Scale = Math.Min(m_Config.maxScale, Scale + m_Config.stepUp);
            m_InBudgetTime = 0f;
            m_LastChangeWasProbe = true;
            tracker.Clear();
            return true;
        }
    }
}
