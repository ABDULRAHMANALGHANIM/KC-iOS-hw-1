using UnityEngine;

namespace NeonRequiem.Core
{
    /// <summary>
    /// Small math helpers shared by gameplay and camera code. Everything here is plain managed
    /// arithmetic (no native engine calls) so it can be unit tested outside the Unity runtime.
    /// Yaw is in degrees, measured clockwise from +Z when seen from above (Unity's convention).
    /// </summary>
    public static class NRMath
    {
        /// <summary>Frame-rate independent interpolation factor for exponential smoothing.</summary>
        public static float DampFactor(float rate, float deltaTime)
        {
            if (rate <= 0f || deltaTime <= 0f)
            {
                return 0f;
            }

            return 1f - Mathf.Exp(-rate * deltaTime);
        }

        public static float Damp(float current, float target, float rate, float deltaTime)
        {
            return current + (target - current) * DampFactor(rate, deltaTime);
        }

        public static Vector3 Damp(Vector3 current, Vector3 target, float rate, float deltaTime)
        {
            return current + (target - current) * DampFactor(rate, deltaTime);
        }

        /// <summary>Exponential smoothing for angles in degrees; takes the shortest way around.</summary>
        public static float DampAngle(float current, float target, float rate, float deltaTime)
        {
            return current + Mathf.DeltaAngle(current, target) * DampFactor(rate, deltaTime);
        }

        /// <summary>Wraps an angle in degrees into (-180, 180].</summary>
        public static float WrapAngle180(float degrees)
        {
            float wrapped = Mathf.Repeat(degrees + 180f, 360f) - 180f;
            return wrapped <= -180f ? 180f : wrapped;
        }

        /// <summary>Unit forward vector on the ground plane for a yaw angle.</summary>
        public static Vector3 YawToForward(float yawDegrees)
        {
            float radians = yawDegrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
        }

        /// <summary>Unit right vector on the ground plane for a yaw angle.</summary>
        public static Vector3 YawToRight(float yawDegrees)
        {
            float radians = yawDegrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(radians), 0f, -Mathf.Sin(radians));
        }

        /// <summary>Yaw angle of a direction projected on the ground plane.</summary>
        public static float DirectionToYaw(Vector3 direction)
        {
            return Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        }

        /// <summary>Removes the vertical component.</summary>
        public static Vector3 Flatten(Vector3 value)
        {
            return new Vector3(value.x, 0f, value.z);
        }

        /// <summary>Length of the horizontal part of a vector.</summary>
        public static float PlanarMagnitude(Vector3 value)
        {
            return Mathf.Sqrt(value.x * value.x + value.z * value.z);
        }

        /// <summary>
        /// Radial dead zone with rescaling: input inside <paramref name="innerRadius"/> yields zero,
        /// and the remaining travel is remapped to the full 0..1 range so there is no jump at the edge.
        /// </summary>
        public static Vector2 ApplyRadialDeadZone(Vector2 value, float innerRadius, float outerRadius = 1f)
        {
            float magnitude = value.magnitude;
            if (magnitude <= innerRadius || outerRadius <= innerRadius)
            {
                return Vector2.zero;
            }

            float scaled = Mathf.Clamp01((magnitude - innerRadius) / (outerRadius - innerRadius));
            return value * (scaled / magnitude);
        }

        /// <summary>Moves a vector toward a target by at most <paramref name="maxDelta"/>.</summary>
        public static Vector3 MoveTowards(Vector3 current, Vector3 target, float maxDelta)
        {
            Vector3 delta = target - current;
            float distance = delta.magnitude;
            if (distance <= maxDelta || distance < 1e-6f)
            {
                return target;
            }

            return current + delta * (maxDelta / distance);
        }
    }
}
