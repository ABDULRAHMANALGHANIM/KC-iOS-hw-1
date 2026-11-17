using System;
using NeonRequiem.Player;
using NUnit.Framework;
using UnityEngine;

namespace NeonRequiem.Tests
{
    /// <summary>
    /// Exercises the movement rules against a simulated body, so results are deterministic and
    /// independent of the physics engine. The real CharacterController integration is covered by
    /// the PlayMode suite.
    /// </summary>
    public sealed class PlayerLocomotionTests
    {
        private const float Dt = 1f / 60f;

        private MovementTuning m_Tuning;
        private SimulatedMotor m_Motor;
        private PlayerLocomotion m_Locomotion;
        private int m_Jumped;
        private int m_AirJumped;
        private int m_Landed;
        private int m_DodgeStarted;
        private int m_DodgeEnded;
        private float m_LastImpact;
        private Vector3 m_DodgeEndPosition;

        [SetUp]
        public void SetUp()
        {
            Create(new MovementTuning());
        }

        // ---------------------------------------------------------------------------------- ground

        [Test]
        public void Idle_StaysGroundedAndStill()
        {
            Step(default, 120);

            Assert.AreEqual(LocomotionMode.Grounded, m_Locomotion.Mode);
            Assert.That(m_Locomotion.PlanarSpeed, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(m_Motor.Position.x, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(m_Motor.Position.z, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(m_Motor.Position.y, Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void FullStick_ReachesRunSpeed_AlongCameraForward()
        {
            Step(Forward(), 60);

            Assert.That(m_Locomotion.PlanarSpeed, Is.EqualTo(m_Tuning.runSpeed).Within(1e-3f));
            Assert.Greater(m_Motor.Position.z, 4f);
            Assert.That(m_Motor.Position.x, Is.EqualTo(0f).Within(1e-3f));
        }

        [Test]
        public void Movement_IsRelativeToCameraYaw()
        {
            var input = Forward();
            input.CameraYaw = 90f;
            Step(input, 60);

            Assert.Greater(m_Motor.Position.x, 4f, "Stick forward with the camera facing +X must move along +X.");
            Assert.That(m_Motor.Position.z, Is.EqualTo(0f).Within(1e-2f));
            Assert.That(m_Locomotion.FacingYaw, Is.EqualTo(90f).Within(0.5f));
        }

        [Test]
        public void StickRight_MovesToTheCamerasRight()
        {
            Step(new LocomotionInput { Move = new Vector2(1f, 0f) }, 60);
            Assert.Greater(m_Motor.Position.x, 4f);
        }

        [Test]
        public void PartialStick_GivesProportionalSpeed()
        {
            Step(new LocomotionInput { Move = new Vector2(0f, 0.5f) }, 60);
            Assert.That(m_Locomotion.PlanarSpeed, Is.EqualTo(m_Tuning.runSpeed * 0.5f).Within(1e-3f));
        }

        [Test]
        public void OversizedInput_IsClampedToRunSpeed()
        {
            Step(new LocomotionInput { Move = new Vector2(3f, 3f) }, 60);
            Assert.That(m_Locomotion.PlanarSpeed, Is.EqualTo(m_Tuning.runSpeed).Within(1e-3f));
        }

        [Test]
        public void Sprint_NeedsHoldAndEnoughStick()
        {
            var sprint = Forward();
            sprint.SprintHeld = true;
            Step(sprint, 60);
            Assert.That(m_Locomotion.PlanarSpeed, Is.EqualTo(m_Tuning.sprintSpeed).Within(1e-3f));
            Assert.IsTrue(m_Locomotion.IsSprinting);

            Create(new MovementTuning());
            var weak = new LocomotionInput { Move = new Vector2(0f, 0.3f), SprintHeld = true };
            Step(weak, 60);
            Assert.IsFalse(m_Locomotion.IsSprinting);
            Assert.That(m_Locomotion.PlanarSpeed, Is.EqualTo(m_Tuning.runSpeed * 0.3f).Within(1e-3f));
        }

        [Test]
        public void ReleasingTheStick_StopsQuickly()
        {
            Step(Forward(), 60);
            Step(default, 12);
            Assert.That(m_Locomotion.PlanarSpeed, Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void Facing_TurnsTowardInput_AtTheConfiguredRate()
        {
            Step(new LocomotionInput { Move = new Vector2(1f, 0f) }, 1);
            Assert.That(m_Locomotion.FacingYaw, Is.EqualTo(m_Tuning.turnSpeed * Dt).Within(1e-2f));

            Step(new LocomotionInput { Move = new Vector2(1f, 0f) }, 30);
            Assert.That(m_Locomotion.FacingYaw, Is.EqualTo(90f).Within(1e-2f));
        }

        // ------------------------------------------------------------------------------------ jump

        [TestCase(1f / 30f)]
        [TestCase(1f / 60f)]
        [TestCase(1f / 120f)]
        public void HeldJump_ReachesConfiguredHeight_AtAnyFrameRate(float dt)
        {
            float apex = SimulateJump(dt, holdSeconds: 10f);
            Assert.That(apex, Is.EqualTo(m_Tuning.jumpHeight).Within(0.02f));
        }

        [Test]
        public void ReleasedJump_IsMuchLower()
        {
            float tap = SimulateJump(Dt, holdSeconds: 0f);
            Assert.Less(tap, m_Tuning.jumpHeight * 0.6f);
            Assert.Greater(tap, 0.3f);
        }

        [Test]
        public void Jump_RaisesJumpedThenLanded_AndReturnsToGround()
        {
            SimulateJump(Dt, holdSeconds: 10f);

            Assert.AreEqual(1, m_Jumped);
            Assert.AreEqual(0, m_AirJumped);
            Assert.AreEqual(1, m_Landed);
            Assert.Greater(m_LastImpact, 5f);
            Assert.AreEqual(LocomotionMode.Grounded, m_Locomotion.Mode);
            Assert.That(m_Motor.Position.y, Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void FallSpeed_IsCappedAtTerminalVelocity()
        {
            m_Motor.GroundHeight = position => float.NegativeInfinity;
            m_Motor.Position = new Vector3(0f, 500f, 0f);
            Create(m_Tuning, keepMotor: true);

            Step(default, 600);

            Assert.That(m_Locomotion.Velocity.y, Is.EqualTo(-m_Tuning.terminalFallSpeed).Within(1e-3f));
        }

        [Test]
        public void AirJump_IsAvailableOncePerAirtime()
        {
            var press = new LocomotionInput { JumpPressed = true, JumpHeld = true };
            var hold = new LocomotionInput { JumpHeld = true };

            Step(press, 1);
            Step(hold, 10);
            Assert.AreEqual(1, m_Locomotion.AirJumpsRemaining);

            Step(press, 1);
            Assert.AreEqual(1, m_AirJumped);
            Assert.AreEqual(0, m_Locomotion.AirJumpsRemaining);

            Step(hold, 10);
            Step(press, 1);
            Step(hold, 5);
            Assert.AreEqual(1, m_AirJumped, "A third jump must not be possible before landing.");

            StepUntil(hold, () => m_Locomotion.Mode == LocomotionMode.Grounded, 5f);
            Assert.AreEqual(1, m_Locomotion.AirJumpsRemaining, "Landing restores the air jump.");
        }

        [Test]
        public void AirJump_AddsHeight()
        {
            float single = SimulateJump(Dt, holdSeconds: 10f);

            Create(new MovementTuning());
            var press = new LocomotionInput { JumpPressed = true, JumpHeld = true };
            var hold = new LocomotionInput { JumpHeld = true };
            Step(press, 1);
            float apex = 0f;
            bool usedAirJump = false;
            for (int i = 0; i < 600 && m_Locomotion.Mode != LocomotionMode.Grounded; i++)
            {
                bool atApex = !usedAirJump && m_Locomotion.Velocity.y <= 0f;
                Step(atApex ? press : hold, 1);
                usedAirJump |= atApex;
                apex = Mathf.Max(apex, m_Motor.Position.y);
            }

            Assert.That(apex, Is.EqualTo(single + m_Tuning.airJumpHeight).Within(0.06f));
        }

        [Test]
        public void CoyoteTime_AllowsAGroundJumpJustAfterLeavingALedge()
        {
            m_Motor.GroundHeight = position => position.z <= 1f ? 0f : float.NegativeInfinity;

            StepUntil(Forward(), () => m_Locomotion.Mode == LocomotionMode.Airborne, 2f);
            Step(Forward(), 3); // 0.05 s past the edge, inside the 0.12 s window

            var press = Forward();
            press.JumpPressed = true;
            press.JumpHeld = true;
            Step(press, 1);

            Assert.AreEqual(1, m_Jumped, "This must count as a ground jump.");
            Assert.AreEqual(0, m_AirJumped);
            Assert.AreEqual(1, m_Locomotion.AirJumpsRemaining, "The air jump is still available afterwards.");
            Assert.Greater(m_Locomotion.Velocity.y, 0f);
        }

        [Test]
        public void CoyoteTime_Expired_FallsBackToTheAirJump()
        {
            m_Motor.GroundHeight = position => position.z <= 1f ? 0f : float.NegativeInfinity;

            StepUntil(Forward(), () => m_Locomotion.Mode == LocomotionMode.Airborne, 2f);
            Step(Forward(), 18); // 0.3 s past the edge

            var press = Forward();
            press.JumpPressed = true;
            press.JumpHeld = true;
            Step(press, 1);

            Assert.AreEqual(0, m_Jumped);
            Assert.AreEqual(1, m_AirJumped);
            Assert.AreEqual(0, m_Locomotion.AirJumpsRemaining);
        }

        [Test]
        public void WalkingOffALedge_StartsTheFallFromRest()
        {
            m_Motor.GroundHeight = position => position.z <= 1f ? 0f : float.NegativeInfinity;
            StepUntil(Forward(), () => m_Locomotion.Mode == LocomotionMode.Airborne, 2f);

            Assert.That(m_Locomotion.Velocity.y, Is.EqualTo(0f).Within(1e-5f));
            Step(Forward(), 1);
            Assert.Less(m_Locomotion.Velocity.y, 0f);
        }

        [Test]
        public void BufferedJump_FiresOnLanding()
        {
            var tuning = new MovementTuning { airJumps = 0 };
            Create(tuning);

            var press = new LocomotionInput { JumpPressed = true, JumpHeld = true };
            var hold = new LocomotionInput { JumpHeld = true };
            Step(press, 1);
            StepUntil(hold, () => m_Locomotion.Velocity.y < 0f && m_Motor.Position.y < 0.35f, 5f);

            Step(press, 1); // pressed a few frames before touching down
            Assert.AreEqual(1, m_Jumped, "No jump may start in mid-air without an air jump.");

            StepUntil(hold, () => m_Jumped == 2, 0.5f);
            Assert.AreEqual(1, m_Landed);
            Assert.AreEqual(2, m_Jumped);
        }

        [Test]
        public void StaleBufferedJump_IsDiscarded()
        {
            var tuning = new MovementTuning { airJumps = 0 };
            Create(tuning);

            var press = new LocomotionInput { JumpPressed = true, JumpHeld = true };
            var hold = new LocomotionInput { JumpHeld = true };
            Step(press, 1);
            StepUntil(hold, () => m_Locomotion.Velocity.y <= 0f, 5f); // apex: far more than the buffer time from landing
            Step(press, 1);

            StepUntil(hold, () => m_Locomotion.Mode == LocomotionMode.Grounded, 5f);
            Step(hold, 30);

            Assert.AreEqual(1, m_Jumped);
            Assert.AreEqual(LocomotionMode.Grounded, m_Locomotion.Mode);
        }

        [Test]
        public void HeadBump_CancelsUpwardSpeed()
        {
            m_Motor.Ceiling = 0.8f;
            var press = new LocomotionInput { JumpPressed = true, JumpHeld = true };
            var hold = new LocomotionInput { JumpHeld = true };
            Step(press, 1);

            float apex = 0f;
            for (int i = 0; i < 300 && m_Locomotion.Mode != LocomotionMode.Grounded; i++)
            {
                Step(hold, 1);
                apex = Mathf.Max(apex, m_Motor.Position.y);
            }

            Assert.That(apex, Is.EqualTo(0.8f).Within(1e-4f));
            Assert.AreEqual(LocomotionMode.Grounded, m_Locomotion.Mode);
        }

        // ----------------------------------------------------------------------------------- dodge

        [TestCase(1f / 30f)]
        [TestCase(1f / 60f)]
        [TestCase(1f / 120f)]
        public void Dodge_CoversConfiguredDistance_AtAnyFrameRate(float dt)
        {
            var press = Forward();
            press.DodgePressed = true;
            m_Locomotion.Tick(press, dt);
            Assert.AreEqual(LocomotionMode.Dodging, m_Locomotion.Mode);

            for (int i = 0; i < 600 && m_DodgeEnded == 0; i++)
            {
                m_Locomotion.Tick(Forward(), dt);
            }

            Assert.AreEqual(1, m_DodgeStarted);
            Assert.AreEqual(1, m_DodgeEnded);
            Assert.That(m_DodgeEndPosition.z, Is.EqualTo(m_Tuning.dodgeDistance).Within(0.01f));
            Assert.That(m_DodgeEndPosition.x, Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void DodgeDistanceFraction_IsMonotonic_FromZeroToOne()
        {
            Assert.That(PlayerLocomotion.DodgeDistanceFraction(0f), Is.EqualTo(0f).Within(1e-6f));
            Assert.That(PlayerLocomotion.DodgeDistanceFraction(1f), Is.EqualTo(1f).Within(1e-6f));
            Assert.That(PlayerLocomotion.DodgeDistanceFraction(2f), Is.EqualTo(1f).Within(1e-6f));

            float previous = 0f;
            for (int i = 1; i <= 100; i++)
            {
                float value = PlayerLocomotion.DodgeDistanceFraction(i / 100f);
                Assert.Greater(value, previous);
                previous = value;
            }
        }

        [Test]
        public void Dodge_WithoutInput_BackstepsAndKeepsFacing()
        {
            Step(new LocomotionInput { DodgePressed = true }, 1);
            StepUntil(default, () => m_DodgeEnded == 1, 2f);

            Assert.That(m_DodgeEndPosition.z, Is.EqualTo(-m_Tuning.dodgeDistance).Within(0.01f));
            Assert.That(m_Locomotion.FacingYaw, Is.EqualTo(0f).Within(1e-3f));
        }

        [Test]
        public void Dodge_FacesTheDodgeDirectionImmediately()
        {
            var press = new LocomotionInput { Move = new Vector2(-1f, 0f), DodgePressed = true };
            Step(press, 1);
            Assert.That(m_Locomotion.FacingYaw, Is.EqualTo(-90f).Within(1e-3f));
        }

        [Test]
        public void Dodge_IsInvulnerableOnlyInsideItsWindow()
        {
            var press = Forward();
            press.DodgePressed = true;
            Step(press, 1);

            bool sawInvulnerable = false;
            bool sawVulnerableTail = false;
            float elapsed = Dt;
            while (m_DodgeEnded == 0 && elapsed < 2f)
            {
                bool expected = elapsed >= m_Tuning.dodgeInvulnerabilityStart && elapsed <= m_Tuning.dodgeInvulnerabilityEnd;
                Assert.AreEqual(expected, m_Locomotion.IsInvulnerable, "at t=" + elapsed);
                sawInvulnerable |= m_Locomotion.IsInvulnerable;
                sawVulnerableTail |= !m_Locomotion.IsInvulnerable && elapsed > m_Tuning.dodgeInvulnerabilityEnd;

                Step(Forward(), 1);
                elapsed += Dt;
            }

            Assert.IsTrue(sawInvulnerable);
            Assert.IsTrue(sawVulnerableTail, "The dodge must end with a punishable recovery.");
            Assert.IsFalse(m_Locomotion.IsInvulnerable);
        }

        [Test]
        public void Dodge_CannotBeRepeatedDuringCooldown()
        {
            var press = Forward();
            press.DodgePressed = true;

            Step(press, 1);
            StepUntil(Forward(), () => m_DodgeEnded == 1, 2f);

            Step(press, 1);
            Step(Forward(), 5);
            Assert.AreEqual(1, m_DodgeStarted, "Still cooling down.");

            Step(Forward(), Mathf.CeilToInt(m_Tuning.dodgeCooldown / Dt) + 2);
            Step(press, 1);
            Assert.AreEqual(2, m_DodgeStarted);
        }

        [Test]
        public void DodgePressedShortlyBeforeCooldownEnds_IsBuffered()
        {
            var press = Forward();
            press.DodgePressed = true;
            Step(press, 1);
            StepUntil(Forward(), () => m_DodgeEnded == 1, 2f);

            // Leave less than the buffer time (0.12 s) on the cooldown, then press.
            int waitFrames = Mathf.FloorToInt((m_Tuning.dodgeCooldown - m_Tuning.dodgeBufferTime * 0.5f) / Dt);
            Step(Forward(), waitFrames);
            Step(press, 1);
            Assert.AreEqual(1, m_DodgeStarted);

            Step(Forward(), 6);
            Assert.AreEqual(2, m_DodgeStarted);
        }

        [Test]
        public void AirDodge_SuspendsGravity_AndIsLimitedPerAirtime()
        {
            m_Motor.GroundHeight = position => float.NegativeInfinity;
            m_Motor.Position = new Vector3(0f, 100f, 0f);
            Create(m_Tuning, keepMotor: true);
            Step(Forward(), 10);

            var press = Forward();
            press.DodgePressed = true;
            Step(press, 1);
            Assert.AreEqual(LocomotionMode.Dodging, m_Locomotion.Mode);

            float height = m_Motor.Position.y;
            while (m_DodgeEnded == 0)
            {
                Step(Forward(), 1);
                Assert.That(m_Motor.Position.y, Is.EqualTo(height).Within(1e-4f), "An air dodge travels level.");
            }

            Step(Forward(), Mathf.CeilToInt(m_Tuning.dodgeCooldown / Dt) + 5);
            Step(press, 1);
            Step(Forward(), 5);
            Assert.AreEqual(1, m_DodgeStarted, "Only one air dodge before landing.");
            Assert.AreEqual(LocomotionMode.Airborne, m_Locomotion.Mode);
        }

        [Test]
        public void GroundDodgeOffALedge_FinishesLevel()
        {
            m_Motor.GroundHeight = position => position.z <= 1f ? 0f : float.NegativeInfinity;
            var press = Forward();
            press.DodgePressed = true;
            Step(press, 1);
            StepUntil(Forward(), () => m_DodgeEnded == 1, 2f);

            Assert.Greater(m_DodgeEndPosition.z, 4f);
            Assert.Greater(m_DodgeEndPosition.y, -0.6f, "Must not dive at ground-stick speed for the rest of the dodge.");
            Assert.AreEqual(LocomotionMode.Airborne, m_Locomotion.Mode);
        }

        [Test]
        public void Dodge_ExitSpeed_NeverExceedsSprintSpeed()
        {
            var press = Forward();
            press.DodgePressed = true;
            Step(press, 1);
            StepUntil(Forward(), () => m_DodgeEnded == 1, 2f);
            Assert.LessOrEqual(m_Locomotion.PlanarSpeed, m_Tuning.sprintSpeed + 1e-3f);
        }

        // ------------------------------------------------------------------------------ collisions

        [Test]
        public void PushingIntoAWall_DoesNotStoreSpeed()
        {
            m_Motor.WallZ = 2f;
            Step(Forward(), 120);

            Assert.That(m_Motor.Position.z, Is.EqualTo(2f).Within(1e-4f));
            Assert.Less(m_Locomotion.PlanarSpeed, 1f);
        }

        [Test]
        public void SteepGround_SlidesDownhill_AndRefusesUphillInput()
        {
            // A 60 degree face descending toward -Z.
            m_Motor.GroundNormal = new Vector3(0f, Mathf.Cos(60f * Mathf.Deg2Rad), -Mathf.Sin(60f * Mathf.Deg2Rad));

            Step(default, 30);
            Assert.IsTrue(m_Locomotion.IsSliding);
            Assert.Less(m_Locomotion.Velocity.z, -1f);

            Step(Forward(), 60); // pushing uphill
            Assert.LessOrEqual(m_Locomotion.Velocity.z, 0f);
            Assert.LessOrEqual(m_Locomotion.PlanarSpeed, m_Tuning.maxSlideSpeed + 1e-3f);
        }

        [Test]
        public void WalkableSlope_DoesNotSlide()
        {
            m_Motor.GroundNormal = new Vector3(0f, Mathf.Cos(30f * Mathf.Deg2Rad), -Mathf.Sin(30f * Mathf.Deg2Rad));
            Step(default, 30);
            Assert.IsFalse(m_Locomotion.IsSliding);
            Assert.That(m_Locomotion.PlanarSpeed, Is.EqualTo(0f).Within(1e-5f));
        }

        // ---------------------------------------------------------------------------------- safety

        [Test]
        public void NonPositiveOrInvalidDeltaTime_IsIgnored()
        {
            var press = Forward();
            press.JumpPressed = true;
            m_Locomotion.Tick(press, 0f);
            m_Locomotion.Tick(press, -1f);
            m_Locomotion.Tick(press, float.NaN);

            Assert.AreEqual(0, m_Jumped);
            Assert.AreEqual(Vector3.zero, m_Motor.Position);
        }

        [Test]
        public void HugeDeltaTime_IsClampedToOneSafeStep()
        {
            Step(Forward(), 60);
            float before = m_Motor.Position.z;
            m_Locomotion.Tick(Forward(), 5f);
            float moved = m_Motor.Position.z - before;
            Assert.That(moved, Is.EqualTo(m_Tuning.runSpeed * PlayerLocomotion.MaxStepSeconds).Within(1e-3f));
        }

        [Test]
        public void Teleport_MovesTheBody_AndClearsMotion()
        {
            Step(Forward(), 60);
            m_Locomotion.Teleport(new Vector3(10f, 5f, -3f), 450f);

            Assert.AreEqual(new Vector3(10f, 5f, -3f), m_Motor.Position);
            Assert.AreEqual(Vector3.zero, m_Locomotion.Velocity);
            Assert.That(m_Locomotion.FacingYaw, Is.EqualTo(90f).Within(1e-3f));
            Assert.AreEqual(LocomotionMode.Airborne, m_Locomotion.Mode);
        }

        [Test]
        public void TeleportDuringDodge_EndsTheDodge()
        {
            var press = Forward();
            press.DodgePressed = true;
            Step(press, 1);
            m_Locomotion.Teleport(Vector3.zero, 0f);

            Assert.AreEqual(1, m_DodgeEnded);
            Assert.IsFalse(m_Locomotion.IsInvulnerable);
        }

        [Test]
        public void Constructor_RejectsNullArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new PlayerLocomotion(null, m_Motor));
            Assert.Throws<ArgumentNullException>(() => new PlayerLocomotion(new MovementTuning(), null));
        }

        // --------------------------------------------------------------------------------- helpers

        private static LocomotionInput Forward()
        {
            return new LocomotionInput { Move = new Vector2(0f, 1f) };
        }

        private void Create(MovementTuning tuning, bool keepMotor = false)
        {
            m_Tuning = tuning;
            if (!keepMotor || m_Motor == null)
            {
                m_Motor = new SimulatedMotor();
            }

            m_Locomotion = new PlayerLocomotion(m_Tuning, m_Motor);
            m_Jumped = m_AirJumped = m_Landed = m_DodgeStarted = m_DodgeEnded = 0;
            m_LastImpact = 0f;
            m_Locomotion.Jumped += () => m_Jumped++;
            m_Locomotion.AirJumped += () => m_AirJumped++;
            m_Locomotion.Landed += impact =>
            {
                m_Landed++;
                m_LastImpact = impact;
            };
            m_Locomotion.DodgeStarted += () => m_DodgeStarted++;
            m_Locomotion.DodgeEnded += () =>
            {
                m_DodgeEnded++;
                m_DodgeEndPosition = m_Motor.Position;
            };
        }

        private void Step(LocomotionInput input, int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                m_Locomotion.Tick(input, Dt);
            }
        }

        private void StepUntil(LocomotionInput input, Func<bool> condition, float timeoutSeconds)
        {
            int frames = Mathf.CeilToInt(timeoutSeconds / Dt);
            for (int i = 0; i < frames; i++)
            {
                if (condition())
                {
                    return;
                }

                m_Locomotion.Tick(input, Dt);
            }

            Assert.IsTrue(condition(), "Condition not reached within " + timeoutSeconds + " s.");
        }

        /// <summary>Jumps from rest and returns the highest point reached.</summary>
        private float SimulateJump(float dt, float holdSeconds)
        {
            float apex = 0f;
            float elapsed = 0f;
            m_Locomotion.Tick(new LocomotionInput { JumpPressed = true, JumpHeld = holdSeconds > 0f }, dt);
            for (int i = 0; i < 2000 && m_Locomotion.Mode != LocomotionMode.Grounded; i++)
            {
                elapsed += dt;
                apex = Mathf.Max(apex, m_Motor.Position.y);
                m_Locomotion.Tick(new LocomotionInput { JumpHeld = elapsed < holdSeconds }, dt);
            }

            return apex;
        }

        /// <summary>Minimal body: flat ground (optionally with holes), an optional ceiling and an optional wall.</summary>
        private sealed class SimulatedMotor : ICharacterMotor
        {
            public Func<Vector3, float> GroundHeight = position => 0f;
            public float Ceiling = float.PositiveInfinity;
            public float WallZ = float.PositiveInfinity;

            public Vector3 Position { get; set; }

            public bool IsGrounded { get; private set; } = true;

            public Vector3 GroundNormal { get; set; } = Vector3.up;

            public MotorMoveResult Move(Vector3 displacement)
            {
                Vector3 start = Position;
                Vector3 target = start + displacement;
                var result = default(MotorMoveResult);

                if (target.z > WallZ)
                {
                    target.z = WallZ;
                    result.HitSides = true;
                }

                if (target.y > Ceiling)
                {
                    target.y = Ceiling;
                    result.HitAbove = true;
                }

                float ground = GroundHeight(target);
                if (displacement.y <= 0f && target.y <= ground)
                {
                    target.y = ground;
                    result.Grounded = true;
                }

                Position = target;
                IsGrounded = result.Grounded;
                result.ActualDisplacement = target - start;
                return result;
            }

            public void Teleport(Vector3 position)
            {
                Position = position;
                IsGrounded = false;
            }
        }
    }

    public sealed class SafePositionTrackerTests
    {
        [Test]
        public void NothingRecorded_ReturnsFalse()
        {
            var tracker = new SafePositionTracker();
            Assert.IsFalse(tracker.TryGetSafePosition(0.5f, out Vector3 _, out float _));
        }

        [Test]
        public void RecordsOnlyOnStableGround()
        {
            var tracker = new SafePositionTracker(8, 0.25f);
            for (int i = 0; i < 60; i++)
            {
                tracker.Tick(false, new Vector3(i, 0f, 0f), 0f, 1f / 60f);
            }

            Assert.AreEqual(0, tracker.Count);
        }

        [Test]
        public void SamplesAtTheConfiguredInterval()
        {
            var tracker = new SafePositionTracker(32, 0.25f);
            for (int i = 0; i < 120; i++)
            {
                tracker.Tick(true, new Vector3(i, 0f, 0f), 0f, 1f / 60f);
            }

            // Two seconds at four samples per second, plus the immediate first sample.
            Assert.That(tracker.Count, Is.InRange(8, 9));
        }

        [Test]
        public void ReturnsTheNewestPositionThatIsOldEnough()
        {
            var tracker = new SafePositionTracker(16, 0.25f);

            // Walk along +X for two seconds, then "fall" for one second without recording.
            for (int i = 0; i < 120; i++)
            {
                tracker.Tick(true, new Vector3(i / 60f * 5f, 0f, 0f), 30f, 1f / 60f);
            }

            for (int i = 0; i < 60; i++)
            {
                tracker.Tick(false, new Vector3(10f, -i, 0f), 0f, 1f / 60f);
            }

            Assert.IsTrue(tracker.TryGetSafePosition(0.75f, out Vector3 position, out float yaw));
            Assert.That(position.y, Is.EqualTo(0f).Within(1e-5f));
            Assert.Greater(position.x, 8f, "Should be near where the character left the ground, not the start.");
            Assert.That(yaw, Is.EqualTo(30f).Within(1e-5f));
        }

        [Test]
        public void SkipsPositionsRecordedTooRecently()
        {
            var tracker = new SafePositionTracker(16, 0.25f);
            for (int i = 0; i < 120; i++)
            {
                tracker.Tick(true, new Vector3(i, 0f, 0f), 0f, 1f / 60f);
            }

            Assert.IsTrue(tracker.TryGetSafePosition(1f, out Vector3 position, out float _));
            Assert.LessOrEqual(position.x, 60f, "Positions younger than one second must be skipped.");
        }

        [Test]
        public void FallsBackToTheOldest_WhenNothingIsOldEnough()
        {
            var tracker = new SafePositionTracker(16, 0.25f);
            tracker.Tick(true, new Vector3(1f, 0f, 0f), 0f, 0.1f);
            tracker.Tick(true, new Vector3(2f, 0f, 0f), 0f, 0.3f);

            Assert.IsTrue(tracker.TryGetSafePosition(100f, out Vector3 position, out float _));
            Assert.That(position.x, Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void Clear_ForgetsEverything()
        {
            var tracker = new SafePositionTracker();
            tracker.Tick(true, Vector3.one, 0f, 0.1f);
            tracker.Clear();
            Assert.AreEqual(0, tracker.Count);
            Assert.IsFalse(tracker.TryGetSafePosition(0f, out Vector3 _, out float _));
        }
    }

    public sealed class MovementTuningTests
    {
        [Test]
        public void JumpVelocity_ReachesJumpHeightUnderGravity()
        {
            var tuning = new MovementTuning();
            float height = tuning.JumpVelocity * tuning.JumpVelocity / (2f * tuning.gravity);
            Assert.That(height, Is.EqualTo(tuning.jumpHeight).Within(1e-4f));
        }

        [Test]
        public void Sanitize_RepairsDangerousValues()
        {
            var tuning = new MovementTuning
            {
                runSpeed = -1f,
                sprintSpeed = 0f,
                gravity = 0f,
                dodgeDuration = 0f,
                dodgeInvulnerabilityStart = 5f,
                dodgeInvulnerabilityEnd = -1f,
                airJumps = -2,
                radius = 0f,
                height = 0f,
                stepOffset = 99f,
            };

            tuning.Sanitize();

            Assert.Greater(tuning.runSpeed, 0f);
            Assert.GreaterOrEqual(tuning.sprintSpeed, tuning.runSpeed);
            Assert.Greater(tuning.gravity, 0f);
            Assert.Greater(tuning.dodgeDuration, 0f);
            Assert.LessOrEqual(tuning.dodgeInvulnerabilityStart, tuning.dodgeDuration);
            Assert.GreaterOrEqual(tuning.dodgeInvulnerabilityEnd, tuning.dodgeInvulnerabilityStart);
            Assert.AreEqual(0, tuning.airJumps);
            Assert.Greater(tuning.radius, 0f);
            Assert.GreaterOrEqual(tuning.height, tuning.radius * 2f);
            Assert.LessOrEqual(tuning.stepOffset, tuning.height);
        }

        [Test]
        public void Defaults_AreAlreadySane()
        {
            var tuning = new MovementTuning();
            var reference = new MovementTuning();
            tuning.Sanitize();

            Assert.AreEqual(reference.runSpeed, tuning.runSpeed);
            Assert.AreEqual(reference.jumpHeight, tuning.jumpHeight);
            Assert.AreEqual(reference.dodgeDistance, tuning.dodgeDistance);
            Assert.AreEqual(reference.dodgeInvulnerabilityEnd, tuning.dodgeInvulnerabilityEnd);
            Assert.AreEqual(reference.stepOffset, tuning.stepOffset);
        }
    }
}
