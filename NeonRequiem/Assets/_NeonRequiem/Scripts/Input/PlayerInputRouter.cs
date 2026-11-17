using System;
using System.Collections.Generic;
using NeonRequiem.Core;
using UnityEngine;

namespace NeonRequiem.Controls
{
    /// <summary>
    /// Merges every input source (touch controls, keyboard and mouse, gamepad) into one
    /// <see cref="PlayerInputFrame"/> per frame. Gameplay reads <see cref="Current"/> and never
    /// talks to devices directly. Runs after the uGUI EventSystem so touches reach gameplay in
    /// the same frame they are dispatched.
    /// </summary>
    [DefaultExecutionOrder(ExecutionOrder.InputRouter)]
    public sealed class PlayerInputRouter : MonoBehaviour
    {
        private const float MaxLookDeltaTime = 0.05f;

        [SerializeField] private LookTuning m_LookTuning = new LookTuning();

        private readonly List<IPlayerInputSource> m_Sources = new List<IPlayerInputSource>(4);
        private SettingsManager m_Settings;
        private PlayerInputFrame m_Current;
        private InputScheme m_ActiveScheme = InputScheme.None;

        /// <summary>Raised when the player switches between touch, keyboard/mouse and gamepad.</summary>
        public event Action<InputScheme> SchemeChanged;

        /// <summary>Input for the current frame. Valid from this component's Update onwards.</summary>
        public PlayerInputFrame Current => m_Current;

        public InputScheme ActiveScheme => m_ActiveScheme;

        public LookTuning LookTuning => m_LookTuning;

        /// <summary>
        /// When false (pause menu, loading) movement, look and action input are zeroed.
        /// <see cref="PlayerInputFrame.PausePressed"/> is still delivered.
        /// </summary>
        public bool GameplayInputEnabled { get; set; } = true;

        public void Initialize(SettingsManager settings)
        {
            m_Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public void AddSource(IPlayerInputSource source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (!m_Sources.Contains(source))
            {
                m_Sources.Add(source);
            }
        }

        public bool RemoveSource(IPlayerInputSource source)
        {
            return m_Sources.Remove(source);
        }

        /// <summary>
        /// Reports a scheme observed outside the sources, e.g. a raw screen touch while the touch
        /// controls are hidden for gamepad play.
        /// </summary>
        public void NotifyScheme(InputScheme scheme)
        {
            if (scheme == InputScheme.None || scheme == m_ActiveScheme)
            {
                return;
            }

            m_ActiveScheme = scheme;
            SchemeChanged?.Invoke(m_ActiveScheme);
        }

        /// <summary>Samples all sources immediately. Called from Update; exposed for tests.</summary>
        public void SampleNow(float unscaledDeltaTime)
        {
            var sample = default(PlayerInputSample);
            for (int i = 0; i < m_Sources.Count; i++)
            {
                // Sources are always polled so one-shot presses never leak into a later frame.
                m_Sources[i].Sample(ref sample);
            }

            if (sample.ActiveScheme != InputScheme.None && sample.ActiveScheme != m_ActiveScheme)
            {
                m_ActiveScheme = sample.ActiveScheme;
                SchemeChanged?.Invoke(m_ActiveScheme);
            }

            var frame = default(PlayerInputFrame);
            frame.PausePressed = sample.PausePressed;

            if (GameplayInputEnabled)
            {
                GameSettings settings = m_Settings != null ? m_Settings.Current : null;
                float sensitivity = settings != null ? settings.lookSensitivity : 1f;
                bool invertY = settings != null && settings.invertLookY;

                frame.Move = Vector2.ClampMagnitude(sample.Move, 1f);
                frame.LookDegrees = LookMath.ComputeLookDegrees(
                    sample,
                    m_LookTuning,
                    sensitivity,
                    invertY,
                    Screen.dpi,
                    Screen.width,
                    Mathf.Min(unscaledDeltaTime, MaxLookDeltaTime));
                frame.JumpPressed = sample.JumpPressed;
                frame.JumpHeld = sample.JumpHeld;
                frame.DodgePressed = sample.DodgePressed;
                frame.SprintHeld = sample.SprintHeld;
            }

            m_Current = frame;
        }

        private void Update()
        {
            SampleNow(Time.unscaledDeltaTime);
        }

        private void OnDisable()
        {
            m_Current = default;
        }
    }
}
