using System.Text;
using NeonRequiem.Controls;
using NeonRequiem.Core;
using NeonRequiem.Optimization;
using NeonRequiem.Player;
using UnityEngine;
using UnityEngine.UI;

namespace NeonRequiem.UI
{
    /// <summary>
    /// Small always-on-top readout of frame pacing, render scale and player state. Development
    /// aid for device testing; toggled by the "Performance overlay" setting. Refreshes a few
    /// times per second so the text rebuild itself does not show up in the numbers it reports.
    /// </summary>
    public sealed class DebugOverlayView : MonoBehaviour
    {
        private const float RefreshInterval = 0.25f;

        private readonly StringBuilder m_Builder = new StringBuilder(256);
        private Text m_Text;
        private PerformanceManager m_Performance;
        private PlayerInputRouter m_Input;
        private PlayerController m_Player;
        private float m_Timer = RefreshInterval;

        public static DebugOverlayView Create(Transform parent, PerformanceManager performance, PlayerInputRouter input)
        {
            Image background = UIFactory.CreateImage("PerformanceOverlay", parent, ProceduralSprites.RoundedRect, new Color(0f, 0f, 0f, 0.5f), false);
            // Sits on the movement side's top corner, clear of the pause button.
            UIFactory.Place(background.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(520f, 150f));

            var view = background.gameObject.AddComponent<DebugOverlayView>();
            view.m_Performance = performance;
            view.m_Input = input;
            view.m_Text = UIFactory.CreateText("Readout", background.transform, string.Empty, 26, TextAnchor.UpperLeft, UITheme.TextPrimary);
            UIFactory.Stretch(view.m_Text.rectTransform);
            view.m_Text.rectTransform.offsetMin = new Vector2(18f, 10f);
            view.m_Text.rectTransform.offsetMax = new Vector2(-18f, -10f);
            return view;
        }

        public void SetPlayer(PlayerController player)
        {
            m_Player = player;
        }

        private void Update()
        {
            m_Timer += Time.unscaledDeltaTime;
            if (m_Timer < RefreshInterval || m_Text == null)
            {
                return;
            }

            m_Timer = 0f;
            m_Builder.Length = 0;

            if (m_Performance != null)
            {
                FrameTimeTracker frames = m_Performance.FrameTimes;
                float average = frames.AverageSeconds;
                float fps = average > 0f ? 1f / average : 0f;
                m_Builder.Append(Mathf.RoundToInt(fps)).Append(" FPS   avg ")
                    .Append((average * 1000f).ToString("F1")).Append(" ms   p95 ")
                    .Append((frames.GetPercentileSeconds(0.95f) * 1000f).ToString("F1")).Append(" ms\n");
                m_Builder.Append(m_Performance.ActiveQualityName).Append("   scale ")
                    .Append(m_Performance.RenderScale.ToString("F2")).Append("   target ")
                    .Append(m_Performance.TargetFrameRate).Append('\n');
            }

            PlayerLocomotion locomotion = m_Player != null ? m_Player.Locomotion : null;
            if (locomotion != null)
            {
                m_Builder.Append(locomotion.Mode).Append("   ")
                    .Append(locomotion.PlanarSpeed.ToString("F1")).Append(" m/s");
                if (locomotion.IsSprinting)
                {
                    m_Builder.Append("   sprint");
                }

                if (locomotion.IsInvulnerable)
                {
                    m_Builder.Append("   i-frames");
                }

                m_Builder.Append('\n');
            }

            if (m_Input != null)
            {
                m_Builder.Append("Input: ").Append(m_Input.ActiveScheme);
            }

            m_Text.text = m_Builder.ToString();
        }
    }
}
