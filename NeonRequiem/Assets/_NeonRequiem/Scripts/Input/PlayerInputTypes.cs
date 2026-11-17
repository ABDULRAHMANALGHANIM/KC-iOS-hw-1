using UnityEngine;

namespace NeonRequiem.Controls
{
    /// <summary>Which family of devices produced the most recent input.</summary>
    public enum InputScheme
    {
        None = 0,
        Touch = 1,
        KeyboardMouse = 2,
        Gamepad = 3,
    }

    /// <summary>
    /// Raw contributions gathered from every input source for one frame, before sensitivity and
    /// unit conversion. Sources add to it; they never overwrite another source's contribution.
    /// </summary>
    public struct PlayerInputSample
    {
        /// <summary>Movement in stick space (x right, y forward). The strongest source wins.</summary>
        public Vector2 Move;

        /// <summary>Touch drag on the look zone this frame, in screen pixels.</summary>
        public Vector2 LookTouchPixels;

        /// <summary>Mouse delta this frame, in screen pixels.</summary>
        public Vector2 LookMousePixels;

        /// <summary>Analog look stick in [-1, 1].</summary>
        public Vector2 LookStick;

        public bool JumpPressed;
        public bool JumpHeld;
        public bool DodgePressed;
        public bool SprintHeld;
        public bool PausePressed;

        /// <summary>Set by a source that saw deliberate input this frame.</summary>
        public InputScheme ActiveScheme;

        public void MergeMove(Vector2 move)
        {
            if (move.sqrMagnitude > Move.sqrMagnitude)
            {
                Move = move;
            }
        }
    }

    /// <summary>
    /// The player's intent for one frame, in gameplay units. This is the only input type gameplay
    /// code reads, which keeps it independent of devices and of the Input System package.
    /// </summary>
    public struct PlayerInputFrame
    {
        /// <summary>Movement in stick space (x right, y forward), magnitude at most 1.</summary>
        public Vector2 Move;

        /// <summary>Camera rotation requested this frame, in degrees (x yaw to the right, y pitch upward).</summary>
        public Vector2 LookDegrees;

        public bool JumpPressed;
        public bool JumpHeld;
        public bool DodgePressed;
        public bool SprintHeld;

        /// <summary>Delivered even while gameplay input is disabled, so the pause menu can be closed.</summary>
        public bool PausePressed;
    }

    /// <summary>A device family that can contribute to the frame's input.</summary>
    public interface IPlayerInputSource
    {
        /// <summary>Adds this source's contribution for the current frame.</summary>
        void Sample(ref PlayerInputSample sample);
    }
}
