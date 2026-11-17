using System;
using NeonRequiem.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NeonRequiem.Optimization
{
    /// <summary>
    /// Applies the graphics preset and frame-rate target, records frame times and drives the
    /// adaptive resolution policy.
    /// <para>
    /// Presets map to the project's quality levels by name (Performance / Balanced / Quality),
    /// each of which carries its own URP pipeline asset. Adaptive resolution multiplies the
    /// active pipeline asset's render scale; the original value is restored on shutdown so the
    /// asset on disk is never left modified.
    /// </para>
    /// </summary>
    public sealed class PerformanceManager : MonoBehaviour
    {
        private const string LogCategory = "Performance";

        private readonly FrameTimeTracker m_FrameTimes = new FrameTimeTracker(240);
        private readonly AdaptiveResolutionPolicy m_Policy = new AdaptiveResolutionPolicy(AdaptiveResolutionConfig.Default);
        private SettingsManager m_Settings;
        private GameManager m_GameManager;
        private UniversalRenderPipelineAsset m_Pipeline;
        private float m_PipelineBaseScale = 1f;

        /// <summary>Rolling frame-time statistics for the overlay and for adaptive quality.</summary>
        public FrameTimeTracker FrameTimes => m_FrameTimes;

        /// <summary>Adaptive factor currently applied on top of the preset's render scale.</summary>
        public float AdaptiveScale => m_Policy.Scale;

        /// <summary>Effective render scale of the active pipeline, or 1 when URP is not active.</summary>
        public float RenderScale => m_Pipeline != null ? m_Pipeline.renderScale : 1f;

        public int TargetFrameRate { get; private set; } = 60;

        /// <summary>Name of the active quality level.</summary>
        public string ActiveQualityName
        {
            get
            {
                string[] names = QualitySettings.names;
                int index = QualitySettings.GetQualityLevel();
                return index >= 0 && index < names.Length ? names[index] : "?";
            }
        }

        public void Initialize(SettingsManager settings, GameManager gameManager)
        {
            m_Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            m_GameManager = gameManager;
            m_Settings.Changed += Apply;

            // A phone that dims or sleeps mid-fight is a defect in an action game.
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Apply(m_Settings.Current);
        }

        /// <summary>Index of the quality level for a preset, or -1 when the project has no level with that name.</summary>
        public static int FindQualityLevel(GraphicsPreset preset)
        {
            string wanted = preset.ToString();
            string[] names = QualitySettings.names;
            for (int i = 0; i < names.Length; i++)
            {
                if (string.Equals(names[i], wanted, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private void Apply(GameSettings settings)
        {
            int level = FindQualityLevel(settings.graphicsPreset);
            if (level < 0)
            {
                NRLog.Warning(
                    LogCategory,
                    "No quality level named '" + settings.graphicsPreset + "' exists; keeping '" + ActiveQualityName + "'.");
            }
            else if (level != QualitySettings.GetQualityLevel())
            {
                // Put the outgoing pipeline asset back to its authored scale before switching.
                RestorePipelineScale();
                QualitySettings.SetQualityLevel(level, true);
            }

            // Frame pacing is driven by targetFrameRate; vSyncCount must be zero for it to apply on desktop.
            TargetFrameRate = (int)settings.frameRateTarget;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = TargetFrameRate;

            CapturePipeline();
            if (!settings.adaptiveResolution)
            {
                m_Policy.Reset();
            }

            ApplyPipelineScale();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            m_FrameTimes.AddSample(dt);

            if (m_Settings == null || !m_Settings.Current.adaptiveResolution)
            {
                return;
            }

            // Menus and loading screens say nothing about gameplay cost.
            if (m_GameManager != null && !m_GameManager.IsPlaying)
            {
                return;
            }

            if (m_Policy.Update(m_FrameTimes, 1f / TargetFrameRate, dt))
            {
                CapturePipeline();
                ApplyPipelineScale();
            }
        }

        private void OnDestroy()
        {
            if (m_Settings != null)
            {
                m_Settings.Changed -= Apply;
            }

            RestorePipelineScale();
        }

        private void CapturePipeline()
        {
            var active = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (active == m_Pipeline)
            {
                return;
            }

            RestorePipelineScale();
            m_Pipeline = active;
            m_PipelineBaseScale = active != null ? active.renderScale : 1f;
        }

        private void ApplyPipelineScale()
        {
            if (m_Pipeline != null)
            {
                m_Pipeline.renderScale = m_PipelineBaseScale * m_Policy.Scale;
            }
        }

        private void RestorePipelineScale()
        {
            if (m_Pipeline != null)
            {
                m_Pipeline.renderScale = m_PipelineBaseScale;
            }
        }
    }
}
