using System;
using NeonRequiem.Cameras;
using NeonRequiem.Controls;
using NeonRequiem.Core;
using UnityEngine;

namespace NeonRequiem.Player
{
    /// <summary>
    /// Composition point for the player character. Feeds the frame's input and the camera
    /// heading into <see cref="PlayerLocomotion"/>, applies the result to the transform and keeps
    /// the presentation rig informed. Gameplay rules live in the locomotion class, physics in
    /// the motor, visuals in the rig; this component only wires them together.
    /// </summary>
    [DefaultExecutionOrder(ExecutionOrder.Player)]
    [RequireComponent(typeof(PlayerMotor))]
    public sealed class PlayerController : MonoBehaviour
    {
        /// <summary>A recorded position must be this old before a respawn will use it.</summary>
        public const float SafePositionMinimumAge = 0.75f;

        private const string LogCategory = "Player";

        private PlayerMotor m_Motor;
        private PlayerLocomotion m_Locomotion;
        private PlayerInputRouter m_Input;
        private ICameraYawSource m_CameraYaw;
        private PlayerVisualRig m_Visual;
        private Transform m_CameraTarget;
        private readonly SafePositionTracker m_SafePositions = new SafePositionTracker();
        private bool m_Initialized;

        /// <summary>Raised after the character was moved by a teleport or respawn.</summary>
        public event Action Teleported;

        /// <summary>Movement state and events. Null until <see cref="Initialize"/> has run.</summary>
        public PlayerLocomotion Locomotion => m_Locomotion;

        /// <summary>Point the camera orbits, at upper-chest height.</summary>
        public Transform CameraTarget => m_CameraTarget != null ? m_CameraTarget : transform;

        public bool IsInitialized => m_Initialized;

        /// <summary>
        /// Supplies dependencies. Must be called once before the first Update.
        /// </summary>
        /// <param name="tuning">Movement numbers; the controller keeps this reference.</param>
        /// <param name="input">Source of the per-frame input.</param>
        /// <param name="cameraYaw">Camera heading used for camera-relative movement. May be null (world-relative).</param>
        /// <param name="visual">Presentation rig, may be null.</param>
        /// <param name="cameraTarget">Transform the camera follows, may be null.</param>
        public void Initialize(
            MovementTuning tuning,
            PlayerInputRouter input,
            ICameraYawSource cameraYaw,
            PlayerVisualRig visual,
            Transform cameraTarget)
        {
            if (tuning == null)
            {
                throw new ArgumentNullException(nameof(tuning));
            }

            m_Input = input != null ? input : throw new ArgumentNullException(nameof(input));
            m_CameraYaw = cameraYaw;
            m_Visual = visual;
            m_CameraTarget = cameraTarget;

            m_Motor = GetComponent<PlayerMotor>();
            m_Motor.Configure(tuning);
            m_Locomotion = new PlayerLocomotion(tuning, m_Motor);
            if (m_Visual != null)
            {
                m_Visual.Bind(m_Locomotion);
            }

            m_Initialized = true;
        }

        public void SetCameraYawSource(ICameraYawSource cameraYaw)
        {
            m_CameraYaw = cameraYaw;
        }

        /// <summary>Moves the character instantly, cancelling all motion.</summary>
        public void Teleport(Vector3 position, float facingYaw)
        {
            if (!m_Initialized)
            {
                NRLog.Warning(LogCategory, "Teleport ignored: the player is not initialized.", this);
                return;
            }

            m_Locomotion.Teleport(position, facingYaw);
            transform.rotation = Quaternion.Euler(0f, m_Locomotion.FacingYaw, 0f);
            Teleported?.Invoke();
        }

        /// <summary>
        /// Returns the character to the last stable ground it stood on, or to the supplied
        /// fallback when no position has been recorded yet.
        /// </summary>
        public void RespawnAtSafePosition(Vector3 fallbackPosition, float fallbackYaw)
        {
            if (m_SafePositions.TryGetSafePosition(SafePositionMinimumAge, out Vector3 position, out float yaw))
            {
                Teleport(position, yaw);
            }
            else
            {
                Teleport(fallbackPosition, fallbackYaw);
            }
        }

        /// <summary>Forgets recorded safe positions, e.g. after the player chose "return to spawn".</summary>
        public void ClearSafePositions()
        {
            m_SafePositions.Clear();
        }

        private void Update()
        {
            if (!m_Initialized)
            {
                return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return;
            }

            PlayerInputFrame frame = m_Input.Current;
            var input = new LocomotionInput
            {
                Move = frame.Move,
                CameraYaw = m_CameraYaw != null ? m_CameraYaw.YawDegrees : 0f,
                JumpPressed = frame.JumpPressed,
                JumpHeld = frame.JumpHeld,
                DodgePressed = frame.DodgePressed,
                SprintHeld = frame.SprintHeld,
            };

            m_Locomotion.Tick(input, dt);
            transform.rotation = Quaternion.Euler(0f, m_Locomotion.FacingYaw, 0f);

            bool stable = m_Locomotion.Mode == LocomotionMode.Grounded && !m_Locomotion.IsSliding;
            m_SafePositions.Tick(stable, transform.position, m_Locomotion.FacingYaw, dt);

            if (m_Visual != null)
            {
                m_Visual.Tick(dt);
            }
        }
    }
}
