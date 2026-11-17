using System;
using UnityEngine;

namespace NeonRequiem.Controls
{
    /// <summary>Conversion factors from device units to camera degrees.</summary>
    [Serializable]
    public sealed class LookTuning
    {
        [Tooltip("Camera yaw produced by one physical inch of touch drag at sensitivity 1.")]
        public float touchDegreesPerInch = 80f;

        [Tooltip("Fallback when the device does not report a usable DPI: yaw for a drag across the full screen width.")]
        public float touchDegreesPerScreenWidth = 320f;

        [Tooltip("Camera yaw per pixel of mouse movement at sensitivity 1.")]
        public float mouseDegreesPerPixel = 0.12f;

        [Tooltip("Yaw speed at full stick deflection, degrees per second.")]
        public float stickYawDegreesPerSecond = 220f;

        [Tooltip("Pitch speed at full stick deflection, degrees per second.")]
        public float stickPitchDegreesPerSecond = 140f;

        [Tooltip("Response curve exponent for the look stick. Above 1 gives finer control near the centre.")]
        public float stickResponseExponent = 1.6f;

        [Tooltip("Vertical pointer look is scaled by this factor; horizontal precision matters more in combat.")]
        public float pointerPitchScale = 0.75f;
    }

    /// <summary>Pure look-input conversion, unit tested without the engine.</summary>
    public static class LookMath
    {
        public const float MinTrustedDpi = 60f;
        public const float MaxTrustedDpi = 1200f;

        /// <summary>
        /// Degrees of camera rotation per pixel of touch drag. Uses physical distance when the
        /// reported DPI is plausible so the same thumb movement turns the camera equally on a
        /// phone and a tablet; otherwise falls back to a fraction of the screen width.
        /// </summary>
        public static float TouchDegreesPerPixel(LookTuning tuning, float dpi, float screenWidth)
        {
            if (dpi >= MinTrustedDpi && dpi <= MaxTrustedDpi)
            {
                return tuning.touchDegreesPerInch / dpi;
            }

            return screenWidth > 0f ? tuning.touchDegreesPerScreenWidth / screenWidth : 0f;
        }

        /// <summary>Applies the response curve to a stick vector while preserving its direction.</summary>
        public static Vector2 ApplyStickCurve(Vector2 stick, float exponent)
        {
            float magnitude = stick.magnitude;
            if (magnitude < 1e-5f)
            {
                return Vector2.zero;
            }

            float clamped = Mathf.Min(magnitude, 1f);
            float curved = Mathf.Pow(clamped, Mathf.Max(0.1f, exponent));
            return stick * (curved / magnitude);
        }

        /// <summary>
        /// Combines every look contribution into degrees for this frame
        /// (x = yaw to the right, y = pitch upward).
        /// </summary>
        public static Vector2 ComputeLookDegrees(
            in PlayerInputSample sample,
            LookTuning tuning,
            float sensitivity,
            bool invertY,
            float dpi,
            float screenWidth,
            float unscaledDeltaTime)
        {
            float touchScale = TouchDegreesPerPixel(tuning, dpi, screenWidth);
            Vector2 pointer = sample.LookTouchPixels * touchScale + sample.LookMousePixels * tuning.mouseDegreesPerPixel;
            pointer.y *= tuning.pointerPitchScale;

            Vector2 stick = ApplyStickCurve(sample.LookStick, tuning.stickResponseExponent);
            float dt = Mathf.Max(0f, unscaledDeltaTime);
            var stickDegrees = new Vector2(
                stick.x * tuning.stickYawDegreesPerSecond * dt,
                stick.y * tuning.stickPitchDegreesPerSecond * dt);

            Vector2 total = (pointer + stickDegrees) * sensitivity;
            if (invertY)
            {
                total.y = -total.y;
            }

            return total;
        }
    }
}
