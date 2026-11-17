using System;
using NeonRequiem.Core;
using UnityEngine;

namespace NeonRequiem.Player
{
    /// <summary>What the character's body is doing.</summary>
    public enum LocomotionMode
    {
        Grounded = 0,
        Airborne = 1,
        Dodging = 2,
    }

    /// <summary>Input for one locomotion step, already expressed in gameplay terms.</summary>
    public struct LocomotionInput
    {
        /// <summary>Stick-space movement (x right, y forward), magnitude at most 1.</summary>
        public Vector2 Move;

        /// <summary>Camera yaw in degrees; movement is relative to it.</summary>
        public float CameraYaw;

        public bool JumpPressed;
        public bool JumpHeld;
        public bool DodgePressed;
        public bool SprintHeld;
    }

    /// <summary>
    /// Authoritative movement logic for the player: running, sprinting, jumping (with coyote time,
    /// input buffering, variable height and air jumps) and dodging (with an invulnerability
    /// window). It decides velocities and state; an <see cref="ICharacterMotor"/> resolves
    /// collisions. Presentation (animation, VFX, audio) listens to the events and never feeds
    /// back into this class.
    /// <para>
    /// Contains no engine calls beyond vector math, so the whole class is unit tested against a
    /// simulated motor. Vertical motion and the dodge are integrated analytically per step, which
    /// keeps jump height and dodge distance independent of frame rate.
    /// </para>
    /// </summary>
    public sealed class PlayerLocomotion
    {
        /// <summary>Longest step simulated at once; longer frames are clamped to avoid tunnelling.</summary>
        public const float MaxStepSeconds = 0.05f;

        private const float InputEpsilon = 0.01f;
        private const float BackstepInputThreshold = 0.1f;
        private const float SteepSlopeTolerance = 1f;

        private readonly MovementTuning m_Tuning;
        private readonly ICharacterMotor m_Motor;

        private Vector3 m_Velocity;
        private float m_FacingYaw;
        private float m_JumpBuffer;
        private float m_DodgeBuffer;
        private float m_DodgeCooldown;
        private float m_TimeSinceGrounded;
        private bool m_CoyoteAvailable;
        private bool m_VariableJumpActive;
        private int m_AirJumpsUsed;
        private int m_AirDodgesUsed;
        private bool m_Sprinting;
        private bool m_Sliding;

        private float m_DodgeElapsed;
        private Vector3 m_DodgeDirection;
        private bool m_DodgeOnGround;

        public PlayerLocomotion(MovementTuning tuning, ICharacterMotor motor)
        {
            m_Tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
            m_Motor = motor ?? throw new ArgumentNullException(nameof(motor));
            m_Tuning.Sanitize();
            Mode = m_Motor.IsGrounded ? LocomotionMode.Grounded : LocomotionMode.Airborne;
        }

        /// <summary>Raised when a ground (or coyote) jump starts.</summary>
        public event Action Jumped;

        /// <summary>Raised when an air jump starts.</summary>
        public event Action AirJumped;

        /// <summary>Raised on landing with the downward speed at impact, in metres per second.</summary>
        public event Action<float> Landed;

        public event Action DodgeStarted;

        public event Action DodgeEnded;

        public LocomotionMode Mode { get; private set; }

        /// <summary>Current velocity in metres per second.</summary>
        public Vector3 Velocity => m_Velocity;

        public float PlanarSpeed => NRMath.PlanarMagnitude(m_Velocity);

        /// <summary>Direction the character faces, in degrees of yaw.</summary>
        public float FacingYaw => m_FacingYaw;

        public bool IsGrounded => Mode == LocomotionMode.Grounded || (Mode == LocomotionMode.Dodging && m_DodgeOnGround);

        public bool IsSprinting => m_Sprinting && Mode != LocomotionMode.Dodging;

        /// <summary>True while standing on ground too steep to walk on.</summary>
        public bool IsSliding => m_Sliding;

        /// <summary>True during the dodge's invulnerability window. Combat will query this.</summary>
        public bool IsInvulnerable =>
            Mode == LocomotionMode.Dodging &&
            m_DodgeElapsed >= m_Tuning.dodgeInvulnerabilityStart &&
            m_DodgeElapsed <= m_Tuning.dodgeInvulnerabilityEnd;

        /// <summary>Dodge progress in [0, 1]; zero when not dodging.</summary>
        public float DodgeProgress => Mode == LocomotionMode.Dodging ? Mathf.Clamp01(m_DodgeElapsed / m_Tuning.dodgeDuration) : 0f;

        public int AirJumpsRemaining => Mathf.Max(0, m_Tuning.airJumps - m_AirJumpsUsed);

        public float DodgeCooldownRemaining => m_DodgeCooldown;

        public MovementTuning Tuning => m_Tuning;

        /// <summary>
        /// Fraction of the dodge distance covered at normalized time <paramref name="u"/>.
        /// The speed profile is 1.5 - u (fast start, easing to half the average speed), so its
        /// integral is 1.5u - 0.5u^2 and reaches exactly 1 at u = 1.
        /// </summary>
        public static float DodgeDistanceFraction(float u)
        {
            float clamped = Mathf.Clamp01(u);
            return 1.5f * clamped - 0.5f * clamped * clamped;
        }

        /// <summary>Advances the simulation by <paramref name="deltaTime"/> seconds.</summary>
        public void Tick(in LocomotionInput input, float deltaTime)
        {
            if (deltaTime <= 0f || float.IsNaN(deltaTime))
            {
                return;
            }

            float dt = Mathf.Min(deltaTime, MaxStepSeconds);

            m_JumpBuffer = input.JumpPressed ? m_Tuning.jumpBufferTime + dt : Mathf.Max(0f, m_JumpBuffer - dt);
            m_DodgeBuffer = input.DodgePressed ? m_Tuning.dodgeBufferTime + dt : Mathf.Max(0f, m_DodgeBuffer - dt);
            if (Mode != LocomotionMode.Dodging)
            {
                m_DodgeCooldown = Mathf.Max(0f, m_DodgeCooldown - dt);
            }

            float wishMagnitude = ComputeWish(input, out Vector3 wishDirection);

            if (Mode != LocomotionMode.Dodging && m_DodgeBuffer > 0f && CanDodge())
            {
                StartDodge(wishDirection, wishMagnitude);
            }

            Vector3 displacement;
            float fallSpeedBeforeMove;
            switch (Mode)
            {
                case LocomotionMode.Dodging:
                    displacement = StepDodge(dt);
                    fallSpeedBeforeMove = 0f;
                    break;

                case LocomotionMode.Grounded:
                    StepGroundedPlanar(input, wishDirection, wishMagnitude, dt);
                    if (m_JumpBuffer > 0f)
                    {
                        StartJump(false);
                        displacement = StepAirborneVertical(input, dt);
                    }
                    else
                    {
                        m_Velocity.y = -GroundStickSpeed();
                        displacement = m_Velocity * dt;
                    }

                    fallSpeedBeforeMove = 0f;
                    break;

                default:
                    StepAirbornePlanar(input, wishDirection, wishMagnitude, dt);
                    TryAirborneJump();
                    displacement = StepAirborneVertical(input, dt);
                    fallSpeedBeforeMove = Mathf.Max(0f, -m_Velocity.y);
                    break;
            }

            MotorMoveResult result = m_Motor.Move(displacement);
            ResolveMove(result, dt, fallSpeedBeforeMove);
            UpdateFacing(wishDirection, wishMagnitude, dt);
        }

        /// <summary>Moves the character instantly and clears all motion state.</summary>
        public void Teleport(Vector3 position, float facingYaw)
        {
            if (Mode == LocomotionMode.Dodging)
            {
                DodgeEnded?.Invoke();
            }

            m_Motor.Teleport(position);
            m_Velocity = Vector3.zero;
            m_FacingYaw = NRMath.WrapAngle180(facingYaw);
            m_JumpBuffer = 0f;
            m_DodgeBuffer = 0f;
            m_DodgeCooldown = 0f;
            m_TimeSinceGrounded = 0f;
            m_CoyoteAvailable = false;
            m_VariableJumpActive = false;
            m_AirJumpsUsed = 0;
            m_AirDodgesUsed = 0;
            m_Sprinting = false;
            m_Sliding = false;
            m_DodgeElapsed = 0f;

            // Start airborne: the next step settles onto whatever ground is below without a landing event spike.
            Mode = LocomotionMode.Airborne;
        }

        private float ComputeWish(in LocomotionInput input, out Vector3 direction)
        {
            Vector2 move = Vector2.ClampMagnitude(input.Move, 1f);
            Vector3 wish = NRMath.YawToRight(input.CameraYaw) * move.x + NRMath.YawToForward(input.CameraYaw) * move.y;
            float magnitude = wish.magnitude;
            if (magnitude < InputEpsilon)
            {
                direction = Vector3.zero;
                return 0f;
            }

            direction = wish / magnitude;
            return Mathf.Min(magnitude, 1f);
        }

        private float GroundStickSpeed()
        {
            float planar = NRMath.PlanarMagnitude(m_Velocity);
            return Mathf.Min(m_Tuning.groundStickSpeed + planar * m_Tuning.slopeStickFactor, m_Tuning.maxGroundStickSpeed);
        }

        private void StepGroundedPlanar(in LocomotionInput input, Vector3 wishDirection, float wishMagnitude, float dt)
        {
            m_Sprinting = input.SprintHeld && wishMagnitude >= m_Tuning.sprintMinInput;
            float targetSpeed = m_Sprinting ? m_Tuning.sprintSpeed : m_Tuning.runSpeed * wishMagnitude;
            Vector3 target = wishDirection * targetSpeed;
            Vector3 planar = NRMath.Flatten(m_Velocity);

            Vector3 normal = m_Motor.GroundNormal;
            float slopeAngle = Vector3.Angle(normal, Vector3.up);
            m_Sliding = slopeAngle > m_Tuning.slopeLimit + SteepSlopeTolerance;

            if (m_Sliding)
            {
                // Too steep to stand. Velocity is split into a downhill part, which gravity owns
                // (it only ever grows, and any uphill motion is removed), and a part across the
                // slope, which is the only thing input can steer.
                Vector3 downhill = NRMath.Flatten(Vector3.ProjectOnPlane(Vector3.down, normal));
                float downhillLength = downhill.magnitude;
                if (downhillLength > 1e-4f)
                {
                    downhill /= downhillLength;

                    float downhillSpeed = Mathf.Max(0f, Vector3.Dot(planar, downhill));
                    Vector3 across = planar - downhill * Vector3.Dot(planar, downhill);
                    Vector3 acrossTarget = target - downhill * Vector3.Dot(target, downhill);

                    across = NRMath.MoveTowards(across, acrossTarget, m_Tuning.groundAcceleration * dt);
                    downhillSpeed = Mathf.Min(downhillSpeed + m_Tuning.slideAcceleration * dt, m_Tuning.maxSlideSpeed);
                    planar = Vector3.ClampMagnitude(across + downhill * downhillSpeed, m_Tuning.maxSlideSpeed);
                }
            }
            else
            {
                float rate = wishMagnitude > 0f ? m_Tuning.groundAcceleration : m_Tuning.groundDeceleration;
                planar = NRMath.MoveTowards(planar, target, rate * dt);
            }

            m_Velocity.x = planar.x;
            m_Velocity.z = planar.z;
        }

        private void StepAirbornePlanar(in LocomotionInput input, Vector3 wishDirection, float wishMagnitude, float dt)
        {
            // Sprint momentum carries through a jump for as long as sprint stays held.
            m_Sprinting = m_Sprinting && input.SprintHeld && wishMagnitude >= m_Tuning.sprintMinInput;

            Vector3 planar = NRMath.Flatten(m_Velocity);
            if (wishMagnitude > 0f)
            {
                float targetSpeed = m_Sprinting ? m_Tuning.sprintSpeed : m_Tuning.runSpeed * wishMagnitude;
                planar = NRMath.MoveTowards(planar, wishDirection * targetSpeed, m_Tuning.airAcceleration * dt);
            }
            else
            {
                planar = NRMath.MoveTowards(planar, Vector3.zero, m_Tuning.airDeceleration * dt);
            }

            m_Velocity.x = planar.x;
            m_Velocity.z = planar.z;
        }

        private void TryAirborneJump()
        {
            if (m_JumpBuffer <= 0f)
            {
                return;
            }

            if (m_CoyoteAvailable && m_TimeSinceGrounded <= m_Tuning.coyoteTime)
            {
                StartJump(false);
            }
            else if (m_AirJumpsUsed < m_Tuning.airJumps)
            {
                m_AirJumpsUsed++;
                StartJump(true);
            }

            // Otherwise the press stays buffered and fires on landing if it is still fresh.
        }

        private void StartJump(bool airJump)
        {
            m_Velocity.y = airJump ? m_Tuning.AirJumpVelocity : m_Tuning.JumpVelocity;
            m_JumpBuffer = 0f;
            m_CoyoteAvailable = false;
            m_VariableJumpActive = true;
            m_Sliding = false;
            Mode = LocomotionMode.Airborne;

            if (airJump)
            {
                AirJumped?.Invoke();
            }
            else
            {
                Jumped?.Invoke();
            }
        }

        /// <summary>
        /// Integrates gravity for one step and returns the displacement. Uses the trapezoid rule,
        /// which is exact for constant acceleration, so apex height does not depend on frame rate.
        /// </summary>
        private Vector3 StepAirborneVertical(in LocomotionInput input, float dt)
        {
            float multiplier = 1f;
            if (m_Velocity.y <= 0f)
            {
                multiplier = m_Tuning.fallGravityMultiplier;
                m_VariableJumpActive = false;
            }
            else if (m_VariableJumpActive && !input.JumpHeld)
            {
                multiplier = m_Tuning.lowJumpGravityMultiplier;
            }

            float startSpeed = m_Velocity.y;
            float endSpeed = Mathf.Max(startSpeed - m_Tuning.gravity * multiplier * dt, -m_Tuning.terminalFallSpeed);
            m_Velocity.y = endSpeed;

            return new Vector3(m_Velocity.x * dt, (startSpeed + endSpeed) * 0.5f * dt, m_Velocity.z * dt);
        }

        private bool CanDodge()
        {
            if (m_DodgeCooldown > 0f)
            {
                return false;
            }

            return Mode == LocomotionMode.Grounded || m_AirDodgesUsed < m_Tuning.airDodges;
        }

        private void StartDodge(Vector3 wishDirection, float wishMagnitude)
        {
            bool backstep = wishMagnitude <= BackstepInputThreshold;
            m_DodgeDirection = backstep ? -NRMath.YawToForward(m_FacingYaw) : wishDirection;
            m_DodgeOnGround = Mode == LocomotionMode.Grounded;
            if (!m_DodgeOnGround)
            {
                m_AirDodgesUsed++;
            }

            if (!backstep)
            {
                m_FacingYaw = NRMath.DirectionToYaw(m_DodgeDirection);
            }

            m_DodgeElapsed = 0f;
            m_DodgeBuffer = 0f;
            m_VariableJumpActive = false;
            m_Sliding = false;
            m_Velocity.y = 0f;
            Mode = LocomotionMode.Dodging;
            DodgeStarted?.Invoke();
        }

        private Vector3 StepDodge(float dt)
        {
            float duration = m_Tuning.dodgeDuration;
            float startFraction = DodgeDistanceFraction(m_DodgeElapsed / duration);
            float previousElapsed = m_DodgeElapsed;
            m_DodgeElapsed = Mathf.Min(m_DodgeElapsed + dt, duration);
            float endFraction = DodgeDistanceFraction(m_DodgeElapsed / duration);

            float stepTime = m_DodgeElapsed - previousElapsed;
            Vector3 planar = m_DodgeDirection * (m_Tuning.dodgeDistance * (endFraction - startFraction));
            Vector3 planarVelocity = stepTime > 1e-6f ? planar / stepTime : Vector3.zero;
            m_Velocity.x = planarVelocity.x;
            m_Velocity.z = planarVelocity.z;

            // A ground dodge hugs the floor; an air dodge suspends gravity for its duration.
            m_Velocity.y = m_DodgeOnGround ? -GroundStickSpeed() : 0f;
            return new Vector3(planar.x, m_Velocity.y * dt, planar.z);
        }

        private void EndDodge(bool grounded)
        {
            m_DodgeCooldown = m_Tuning.dodgeCooldown;
            m_Velocity.y = 0f;

            Vector3 planar = Vector3.ClampMagnitude(NRMath.Flatten(m_Velocity), m_Tuning.sprintSpeed);
            m_Velocity.x = planar.x;
            m_Velocity.z = planar.z;

            if (grounded)
            {
                Mode = LocomotionMode.Grounded;
                m_AirJumpsUsed = 0;
                m_AirDodgesUsed = 0;
                m_TimeSinceGrounded = 0f;
            }
            else
            {
                Mode = LocomotionMode.Airborne;
                m_CoyoteAvailable = false;
            }

            DodgeEnded?.Invoke();
        }

        private void ResolveMove(MotorMoveResult result, float dt, float fallSpeedBeforeMove)
        {
            if (result.HitAbove && m_Velocity.y > 0f)
            {
                m_Velocity.y = 0f;
                m_VariableJumpActive = false;
            }

            if (result.HitSides && Mode != LocomotionMode.Dodging)
            {
                // Do not keep pushing speed into a wall: adopt the slower, actually achieved planar velocity.
                Vector3 achieved = NRMath.Flatten(result.ActualDisplacement) / dt;
                if (achieved.sqrMagnitude < m_Velocity.x * m_Velocity.x + m_Velocity.z * m_Velocity.z)
                {
                    m_Velocity.x = achieved.x;
                    m_Velocity.z = achieved.z;
                }
            }

            switch (Mode)
            {
                case LocomotionMode.Dodging:
                    if (m_DodgeOnGround && !result.Grounded)
                    {
                        // Dodged off a ledge: finish the move level instead of diving.
                        m_DodgeOnGround = false;
                    }

                    if (m_DodgeElapsed >= m_Tuning.dodgeDuration)
                    {
                        EndDodge(result.Grounded);
                    }

                    break;

                case LocomotionMode.Grounded:
                    if (result.Grounded)
                    {
                        m_TimeSinceGrounded = 0f;
                    }
                    else
                    {
                        // Walked off an edge: begin falling from rest and open the coyote window.
                        Mode = LocomotionMode.Airborne;
                        m_Velocity.y = 0f;
                        m_TimeSinceGrounded = 0f;
                        m_CoyoteAvailable = true;
                        m_Sliding = false;
                    }

                    break;

                default:
                    if (result.Grounded && m_Velocity.y <= 0f)
                    {
                        Land(fallSpeedBeforeMove);
                    }
                    else
                    {
                        m_TimeSinceGrounded += dt;
                    }

                    break;
            }
        }

        private void Land(float impactSpeed)
        {
            Mode = LocomotionMode.Grounded;
            m_Velocity.y = 0f;
            m_AirJumpsUsed = 0;
            m_AirDodgesUsed = 0;
            m_TimeSinceGrounded = 0f;
            m_CoyoteAvailable = false;
            m_VariableJumpActive = false;
            Landed?.Invoke(impactSpeed);
        }

        private void UpdateFacing(Vector3 wishDirection, float wishMagnitude, float dt)
        {
            if (Mode == LocomotionMode.Dodging || wishMagnitude <= InputEpsilon)
            {
                return;
            }

            float target = NRMath.DirectionToYaw(wishDirection);
            m_FacingYaw = NRMath.WrapAngle180(Mathf.MoveTowardsAngle(m_FacingYaw, target, m_Tuning.turnSpeed * dt));
        }
    }
}
