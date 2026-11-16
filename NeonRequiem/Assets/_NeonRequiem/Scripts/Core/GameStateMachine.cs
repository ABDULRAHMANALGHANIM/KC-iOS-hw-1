using System;

namespace NeonRequiem.Core
{
    /// <summary>High-level application flow state.</summary>
    public enum GameFlowState
    {
        /// <summary>Services are being created. Nothing is playable yet.</summary>
        Boot = 0,

        /// <summary>A scene is being loaded or its content is being assembled.</summary>
        Loading = 1,

        /// <summary>Gameplay is running.</summary>
        Playing = 2,

        /// <summary>Gameplay is suspended (time scale zero) and the pause menu is shown.</summary>
        Paused = 3,
    }

    /// <summary>
    /// Owns the application flow state and validates every transition. Pure logic: it never
    /// touches engine state, so the transition table is unit tested without Unity.
    /// </summary>
    public sealed class GameStateMachine
    {
        /// <summary>Raised after a successful transition with (previous, current).</summary>
        public event Action<GameFlowState, GameFlowState> Changed;

        public GameFlowState Current { get; private set; } = GameFlowState.Boot;

        public static bool IsTransitionAllowed(GameFlowState from, GameFlowState to)
        {
            switch (from)
            {
                case GameFlowState.Boot:
                    return to == GameFlowState.Loading;
                case GameFlowState.Loading:
                    return to == GameFlowState.Playing;
                case GameFlowState.Playing:
                    return to == GameFlowState.Paused || to == GameFlowState.Loading;
                case GameFlowState.Paused:
                    return to == GameFlowState.Playing || to == GameFlowState.Loading;
                default:
                    return false;
            }
        }

        /// <summary>Moves to <paramref name="next"/> when the transition table allows it.</summary>
        /// <returns>True when the state changed.</returns>
        public bool TryTransition(GameFlowState next)
        {
            GameFlowState previous = Current;
            if (!IsTransitionAllowed(previous, next))
            {
                return false;
            }

            Current = next;
            Changed?.Invoke(previous, next);
            return true;
        }
    }
}
