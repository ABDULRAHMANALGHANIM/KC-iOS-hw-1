using System;
using UnityEngine;

namespace NeonRequiem.Player
{
    /// <summary>
    /// All numbers that define how Kael moves. Plain serializable data so designers tune it in
    /// a <see cref="PlayerMovementConfig"/> asset and tests construct it directly.
    /// Units: metres, seconds, degrees.
    /// </summary>
    [Serializable]
    public sealed class MovementTuning
    {
        [Header("Ground")]
        [Tooltip("Speed at full stick deflection. Lower deflection walks proportionally slower.")]
        public float runSpeed = 5.6f;
        public float sprintSpeed = 8.4f;
        [Tooltip("Stick deflection required before sprint engages.")]
        public float sprintMinInput = 0.6f;
        public float groundAcceleration = 48f;
        public float groundDeceleration = 60f;
        [Tooltip("How fast the character turns to face the input direction, degrees per second.")]
        public float turnSpeed = 900f;

        [Header("Air")]
        public float airAcceleration = 22f;
        public float airDeceleration = 4f;
        public float gravity = 30f;
        [Tooltip("Gravity multiplier while falling. Above 1 shortens hang time for a snappier arc.")]
        public float fallGravityMultiplier = 1.55f;
        [Tooltip("Gravity multiplier while rising after the jump button was released (variable jump height).")]
        public float lowJumpGravityMultiplier = 2.2f;
        public float terminalFallSpeed = 38f;

        [Header("Jump")]
        public float jumpHeight = 1.7f;
        public float airJumpHeight = 1.35f;
        [Tooltip("Extra jumps available before touching the ground again.")]
        public int airJumps = 1;
        [Tooltip("Grace period after walking off a ledge during which a ground jump is still allowed.")]
        public float coyoteTime = 0.12f;
        [Tooltip("A jump pressed this long before landing is executed on landing.")]
        public float jumpBufferTime = 0.14f;

        [Header("Dodge")]
        public float dodgeDistance = 4.2f;
        public float dodgeDuration = 0.34f;
        [Tooltip("Delay after a dodge ends before another may start.")]
        public float dodgeCooldown = 0.35f;
        [Tooltip("Invulnerability window, measured from the start of the dodge.")]
        public float dodgeInvulnerabilityStart = 0.02f;
        public float dodgeInvulnerabilityEnd = 0.24f;
        public float dodgeBufferTime = 0.12f;
        [Tooltip("Dodges available while airborne before touching the ground again.")]
        public int airDodges = 1;

        [Header("Grounding")]
        [Tooltip("Constant downward speed while grounded so the capsule keeps contact.")]
        public float groundStickSpeed = 3f;
        [Tooltip("Extra downward speed per unit of horizontal speed, to stay glued to descending slopes.")]
        public float slopeStickFactor = 1f;
        public float maxGroundStickSpeed = 10f;
        [Tooltip("Steepest walkable slope in degrees. Steeper ground makes the character slide.")]
        public float slopeLimit = 48f;
        public float slideAcceleration = 18f;
        public float maxSlideSpeed = 12f;

        [Header("Capsule")]
        public float height = 1.8f;
        public float radius = 0.35f;
        public float stepOffset = 0.35f;
        public float skinWidth = 0.04f;

        /// <summary>Initial upward speed that reaches <see cref="jumpHeight"/> under base gravity.</summary>
        public float JumpVelocity => Mathf.Sqrt(2f * gravity * jumpHeight);

        public float AirJumpVelocity => Mathf.Sqrt(2f * gravity * airJumpHeight);

        /// <summary>Clamps values that would otherwise cause divisions by zero or nonsensical motion.</summary>
        public void Sanitize()
        {
            runSpeed = Mathf.Max(0.1f, runSpeed);
            sprintSpeed = Mathf.Max(runSpeed, sprintSpeed);
            sprintMinInput = Mathf.Clamp01(sprintMinInput);
            groundAcceleration = Mathf.Max(0.1f, groundAcceleration);
            groundDeceleration = Mathf.Max(0.1f, groundDeceleration);
            turnSpeed = Mathf.Max(1f, turnSpeed);
            airAcceleration = Mathf.Max(0f, airAcceleration);
            airDeceleration = Mathf.Max(0f, airDeceleration);
            gravity = Mathf.Max(0.1f, gravity);
            fallGravityMultiplier = Mathf.Max(0.1f, fallGravityMultiplier);
            lowJumpGravityMultiplier = Mathf.Max(1f, lowJumpGravityMultiplier);
            terminalFallSpeed = Mathf.Max(1f, terminalFallSpeed);
            jumpHeight = Mathf.Max(0.05f, jumpHeight);
            airJumpHeight = Mathf.Max(0.05f, airJumpHeight);
            airJumps = Mathf.Max(0, airJumps);
            coyoteTime = Mathf.Max(0f, coyoteTime);
            jumpBufferTime = Mathf.Max(0f, jumpBufferTime);
            dodgeDistance = Mathf.Max(0.1f, dodgeDistance);
            dodgeDuration = Mathf.Max(0.05f, dodgeDuration);
            dodgeCooldown = Mathf.Max(0f, dodgeCooldown);
            dodgeInvulnerabilityStart = Mathf.Clamp(dodgeInvulnerabilityStart, 0f, dodgeDuration);
            dodgeInvulnerabilityEnd = Mathf.Clamp(dodgeInvulnerabilityEnd, dodgeInvulnerabilityStart, dodgeDuration);
            dodgeBufferTime = Mathf.Max(0f, dodgeBufferTime);
            airDodges = Mathf.Max(0, airDodges);
            groundStickSpeed = Mathf.Max(0.5f, groundStickSpeed);
            slopeStickFactor = Mathf.Max(0f, slopeStickFactor);
            maxGroundStickSpeed = Mathf.Max(groundStickSpeed, maxGroundStickSpeed);
            slopeLimit = Mathf.Clamp(slopeLimit, 1f, 89f);
            slideAcceleration = Mathf.Max(0f, slideAcceleration);
            maxSlideSpeed = Mathf.Max(0.1f, maxSlideSpeed);
            radius = Mathf.Max(0.05f, radius);
            height = Mathf.Max(radius * 2f, height);
            stepOffset = Mathf.Clamp(stepOffset, 0f, height);
            skinWidth = Mathf.Clamp(skinWidth, 0.001f, radius);
        }
    }
}
