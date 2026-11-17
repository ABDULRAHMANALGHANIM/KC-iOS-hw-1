using System;
using NeonRequiem.Core;
using UnityEngine;

namespace NeonRequiem.Cameras
{
    /// <summary>Numbers that define the third-person camera. Units: metres, seconds, degrees.</summary>
    [Serializable]
    public sealed class CameraTuning
    {
        [Header("Orbit")]
        public float distance = 5.2f;
        [Tooltip("Closest the camera may be pushed toward the character by obstacles.")]
        public float minDistance = 0.7f;
        [Tooltip("Lowest pitch (looking up from below the character).")]
        public float minPitch = -35f;
        [Tooltip("Highest pitch (looking down on the character).")]
        public float maxPitch = 70f;
        public float defaultPitch = 14f;

        [Header("Follow")]
        public float followSharpness = 20f;
        [Tooltip("Vertical follow is softer so jumps and steps do not jolt the view.")]
        public float verticalFollowSharpness = 10f;
        [Tooltip("Follow sharpness used with the Reduce Camera Motion setting: tighter, with less float.")]
        public float reducedMotionFollowSharpness = 40f;

        [Header("Collision")]
        public float collisionRadius = 0.22f;
        public float collisionPadding = 0.08f;
        [Tooltip("How quickly the camera moves back out once an obstruction clears.")]
        public float distanceRecoverSharpness = 5f;

        [Header("Follow assist")]
        [Tooltip("Seconds without manual camera input before the assist starts turning the camera.")]
        public float assistDelay = 1.2f;
        [Tooltip("Maximum assist turn rate in degrees per second.")]
        public float assistRate = 55f;
        public float assistMinSpeed = 1.5f;
        [Tooltip("The assist does nothing when the character runs toward the camera by more than this angle.")]
        public float assistMaxAngle = 140f;

        [Header("Lens")]
        public float fieldOfView = 60f;
        public float sprintFieldOfViewBoost = 5f;
        public float fieldOfViewSharpness = 5f;
    }

    /// <summary>
    /// Orbit state of the third-person camera: yaw, pitch and distance, plus the rules that change
    /// them. Engine-free so the behaviour (pitch limits, assist rules, collision distance
    /// handling) is unit tested without a scene.
    /// </summary>
    public sealed class OrbitCameraModel
    {
        private const float AssistDeadAngle = 2f;
        private const float AssistEaseAngle = 60f;

        private readonly CameraTuning m_Tuning;
        private float m_TimeSinceManualLook;

        public OrbitCameraModel(CameraTuning tuning)
        {
            m_Tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
            Reset(0f);
        }

        /// <summary>Heading in degrees, wrapped to (-180, 180].</summary>
        public float Yaw { get; private set; }

        /// <summary>Elevation in degrees; positive looks down at the character.</summary>
        public float Pitch { get; private set; }

        /// <summary>Current distance from the pivot.</summary>
        public float Distance { get; private set; }

        /// <summary>Unit view direction for the current yaw and pitch.</summary>
        public Vector3 Forward
        {
            get
            {
                float yaw = Yaw * Mathf.Deg2Rad;
                float pitch = Pitch * Mathf.Deg2Rad;
                float cosPitch = Mathf.Cos(pitch);
                return new Vector3(Mathf.Sin(yaw) * cosPitch, -Mathf.Sin(pitch), Mathf.Cos(yaw) * cosPitch);
            }
        }

        /// <summary>Places the camera behind a character facing <paramref name="yaw"/>.</summary>
        public void Reset(float yaw)
        {
            Yaw = NRMath.WrapAngle180(yaw);
            Pitch = Mathf.Clamp(m_Tuning.defaultPitch, m_Tuning.minPitch, m_Tuning.maxPitch);
            Distance = m_Tuning.distance;
            m_TimeSinceManualLook = m_Tuning.assistDelay;
        }

        /// <summary>
        /// Applies manual look input in degrees (x turns right, y looks up). Any manual input
        /// restarts the follow-assist delay so the assist never fights the player's hand.
        /// </summary>
        public void ApplyLook(Vector2 lookDegrees, float deltaTime)
        {
            if (lookDegrees.sqrMagnitude > 1e-8f)
            {
                Yaw = NRMath.WrapAngle180(Yaw + lookDegrees.x);
                Pitch = Mathf.Clamp(Pitch - lookDegrees.y, m_Tuning.minPitch, m_Tuning.maxPitch);
                m_TimeSinceManualLook = 0f;
            }
            else
            {
                m_TimeSinceManualLook += Mathf.Max(0f, deltaTime);
            }
        }

        /// <summary>
        /// Gently turns the camera toward the travel direction. Predictable by design: it only acts
        /// while the character is moving, only after the look input has been idle for the delay,
        /// never when the character runs toward the camera, and it slows as it converges.
        /// </summary>
        /// <returns>True when the yaw was changed.</returns>
        public bool ApplyFollowAssist(float travelYaw, float planarSpeed, float deltaTime)
        {
            if (deltaTime <= 0f || planarSpeed < m_Tuning.assistMinSpeed || m_TimeSinceManualLook < m_Tuning.assistDelay)
            {
                return false;
            }

            float delta = Mathf.DeltaAngle(Yaw, travelYaw);
            float magnitude = Mathf.Abs(delta);
            if (magnitude < AssistDeadAngle || magnitude > m_Tuning.assistMaxAngle)
            {
                return false;
            }

            float rate = m_Tuning.assistRate * Mathf.Clamp01(magnitude / AssistEaseAngle);
            Yaw = NRMath.WrapAngle180(Mathf.MoveTowardsAngle(Yaw, travelYaw, rate * deltaTime));
            return true;
        }

        /// <summary>
        /// Updates the camera distance. An obstruction pulls the camera in immediately (it must
        /// never sit inside geometry); once clear it eases back out to avoid popping.
        /// </summary>
        /// <param name="unobstructedDistance">Free distance along the view ray, or a large value when nothing is hit.</param>
        public void UpdateDistance(float unobstructedDistance, float deltaTime)
        {
            float limit = Mathf.Clamp(unobstructedDistance, m_Tuning.minDistance, m_Tuning.distance);
            float eased = NRMath.Damp(Distance, m_Tuning.distance, m_Tuning.distanceRecoverSharpness, deltaTime);
            Distance = Mathf.Min(eased, limit);
        }
    }
}
