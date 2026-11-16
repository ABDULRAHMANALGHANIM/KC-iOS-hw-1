using System;

namespace NeonRequiem.Core
{
    /// <summary>The three shipping graphics presets. Values index the project's quality levels by name.</summary>
    public enum GraphicsPreset
    {
        Performance = 0,
        Balanced = 1,
        Quality = 2,
    }

    /// <summary>Frame rate the game paces itself to.</summary>
    public enum FrameRateTarget
    {
        Fps30 = 30,
        Fps60 = 60,
    }

    /// <summary>
    /// Player-adjustable options. These are non-critical preferences (controls, camera, graphics,
    /// accessibility) and are the only data the game keeps in PlayerPrefs; progression will live
    /// in the versioned save system.
    /// </summary>
    [Serializable]
    public sealed class GameSettings
    {
        public const float MinLookSensitivity = 0.2f;
        public const float MaxLookSensitivity = 3f;
        public const float MinTouchControlScale = 0.7f;
        public const float MaxTouchControlScale = 1.5f;
        public const float MinTouchControlOpacity = 0.25f;
        public const float MaxTouchControlOpacity = 1f;

        // Controls
        public float lookSensitivity = 1f;
        public bool invertLookY;
        public bool leftHandedLayout;
        public bool floatingJoystick = true;
        public float touchControlScale = 1f;
        public float touchControlOpacity = 0.8f;

        // Camera / accessibility
        public bool cameraAssist = true;
        public bool reduceCameraMotion;

        // Graphics
        public GraphicsPreset graphicsPreset = GraphicsPreset.Balanced;
        public FrameRateTarget frameRateTarget = FrameRateTarget.Fps60;
        public bool adaptiveResolution = true;

        // Diagnostics
        public bool showPerformanceOverlay = true;

        /// <summary>Forces every field into its legal range. Called after loading and after every edit.</summary>
        public void Sanitize()
        {
            lookSensitivity = ClampFinite(lookSensitivity, MinLookSensitivity, MaxLookSensitivity, 1f);
            touchControlScale = ClampFinite(touchControlScale, MinTouchControlScale, MaxTouchControlScale, 1f);
            touchControlOpacity = ClampFinite(touchControlOpacity, MinTouchControlOpacity, MaxTouchControlOpacity, 0.8f);

            if (graphicsPreset < GraphicsPreset.Performance || graphicsPreset > GraphicsPreset.Quality)
            {
                graphicsPreset = GraphicsPreset.Balanced;
            }

            if (frameRateTarget != FrameRateTarget.Fps30 && frameRateTarget != FrameRateTarget.Fps60)
            {
                frameRateTarget = FrameRateTarget.Fps60;
            }
        }

        public void CopyFrom(GameSettings other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            lookSensitivity = other.lookSensitivity;
            invertLookY = other.invertLookY;
            leftHandedLayout = other.leftHandedLayout;
            floatingJoystick = other.floatingJoystick;
            touchControlScale = other.touchControlScale;
            touchControlOpacity = other.touchControlOpacity;
            cameraAssist = other.cameraAssist;
            reduceCameraMotion = other.reduceCameraMotion;
            graphicsPreset = other.graphicsPreset;
            frameRateTarget = other.frameRateTarget;
            adaptiveResolution = other.adaptiveResolution;
            showPerformanceOverlay = other.showPerformanceOverlay;
        }

        public GameSettings Clone()
        {
            var copy = new GameSettings();
            copy.CopyFrom(this);
            return copy;
        }

        private static float ClampFinite(float value, float min, float max, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return fallback;
            }

            return value < min ? min : (value > max ? max : value);
        }
    }
}
