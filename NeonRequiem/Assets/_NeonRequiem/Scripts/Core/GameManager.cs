using System;
using UnityEngine;

namespace NeonRequiem.Core
{
    /// <summary>
    /// Owns the application flow state and the pause contract. Pausing sets the time scale to
    /// zero; systems that must keep running while paused (input routing, menus) use unscaled time.
    /// </summary>
    public sealed class GameManager : MonoBehaviour
    {
        private const string LogCategory = "Game";

        private readonly GameStateMachine m_State = new GameStateMachine();

        /// <summary>Raised with true when the game pauses and false when it resumes.</summary>
        public event Action<bool> PauseChanged;

        public GameStateMachine State => m_State;

        public bool IsPaused => m_State.Current == GameFlowState.Paused;

        public bool IsPlaying => m_State.Current == GameFlowState.Playing;

        /// <summary>Marks the start of a scene load. Clears any pause that was active.</summary>
        public bool BeginLoading()
        {
            bool wasPaused = IsPaused;
            if (!m_State.TryTransition(GameFlowState.Loading))
            {
                return false;
            }

            Time.timeScale = 1f;
            if (wasPaused)
            {
                PauseChanged?.Invoke(false);
            }

            return true;
        }

        /// <summary>Marks the scene as ready; gameplay starts.</summary>
        public bool BeginPlaying()
        {
            if (m_State.Current == GameFlowState.Boot)
            {
                // A gameplay scene was started directly (no Bootstrap scene): route through Loading
                // so observers always see the same sequence.
                m_State.TryTransition(GameFlowState.Loading);
            }

            if (m_State.Current != GameFlowState.Loading)
            {
                return false;
            }

            Time.timeScale = 1f;
            return m_State.TryTransition(GameFlowState.Playing);
        }

        public bool Pause()
        {
            if (!m_State.TryTransition(GameFlowState.Paused))
            {
                return false;
            }

            Time.timeScale = 0f;
            PauseChanged?.Invoke(true);
            return true;
        }

        public bool Resume()
        {
            if (!IsPaused || !m_State.TryTransition(GameFlowState.Playing))
            {
                return false;
            }

            Time.timeScale = 1f;
            PauseChanged?.Invoke(false);
            return true;
        }

        public void TogglePause()
        {
            if (IsPaused)
            {
                Resume();
            }
            else
            {
                Pause();
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            // Backgrounding the app on a phone must never leave the player in live combat.
            if (pauseStatus && Application.isMobilePlatform && Pause())
            {
                NRLog.Info(LogCategory, "Application moved to background; game paused.");
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && Application.isMobilePlatform && Pause())
            {
                NRLog.Info(LogCategory, "Application lost focus; game paused.");
            }
        }

        private void OnDestroy()
        {
            // Never leak a zero time scale into the next play session or test.
            Time.timeScale = 1f;
        }
    }
}
