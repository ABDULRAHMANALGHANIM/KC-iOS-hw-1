using System.Collections.Generic;

namespace NeonRequiem.Core
{
    /// <summary>
    /// String keys for all player-facing text. UI code never contains literal display strings,
    /// so moving to Unity Localization string tables (planned with the Arabic pass) is a
    /// table swap behind <see cref="Loc.Get"/> rather than a UI rewrite.
    /// </summary>
    public static class LocKeys
    {
        public const string HudJump = "hud.jump";
        public const string HudDodge = "hud.dodge";
        public const string HudPause = "hud.pause";

        public const string PauseTitle = "pause.title";
        public const string PauseResume = "pause.resume";
        public const string PauseRespawn = "pause.respawn";
        public const string PauseResetSettings = "pause.reset_settings";

        public const string SettingLookSensitivity = "settings.look_sensitivity";
        public const string SettingInvertLookY = "settings.invert_look_y";
        public const string SettingCameraAssist = "settings.camera_assist";
        public const string SettingReduceCameraMotion = "settings.reduce_camera_motion";
        public const string SettingLeftHanded = "settings.left_handed";
        public const string SettingFloatingJoystick = "settings.floating_joystick";
        public const string SettingControlSize = "settings.control_size";
        public const string SettingControlOpacity = "settings.control_opacity";
        public const string SettingGraphicsPreset = "settings.graphics_preset";
        public const string SettingFrameRate = "settings.frame_rate";
        public const string SettingAdaptiveResolution = "settings.adaptive_resolution";
        public const string SettingPerformanceOverlay = "settings.performance_overlay";

        public const string ValueOn = "value.on";
        public const string ValueOff = "value.off";
        public const string GraphicsPerformance = "graphics.performance";
        public const string GraphicsBalanced = "graphics.balanced";
        public const string GraphicsQuality = "graphics.quality";

        public const string DiagnosticsTitle = "diagnostics.title";
    }

    /// <summary>
    /// Minimal localization lookup. English is the only table today. A missing key returns the
    /// key itself so gaps are obvious on screen during QA instead of rendering as blank text.
    /// </summary>
    public static class Loc
    {
        private static readonly Dictionary<string, string> s_English = new Dictionary<string, string>
        {
            { LocKeys.HudJump, "JUMP" },
            { LocKeys.HudDodge, "DODGE" },
            { LocKeys.HudPause, "II" },

            { LocKeys.PauseTitle, "PAUSED" },
            { LocKeys.PauseResume, "RESUME" },
            { LocKeys.PauseRespawn, "RETURN TO SPAWN" },
            { LocKeys.PauseResetSettings, "RESET SETTINGS" },

            { LocKeys.SettingLookSensitivity, "Camera sensitivity" },
            { LocKeys.SettingInvertLookY, "Invert vertical look" },
            { LocKeys.SettingCameraAssist, "Camera follow assist" },
            { LocKeys.SettingReduceCameraMotion, "Reduce camera motion" },
            { LocKeys.SettingLeftHanded, "Left-handed layout" },
            { LocKeys.SettingFloatingJoystick, "Floating joystick" },
            { LocKeys.SettingControlSize, "Touch control size" },
            { LocKeys.SettingControlOpacity, "Touch control opacity" },
            { LocKeys.SettingGraphicsPreset, "Graphics preset" },
            { LocKeys.SettingFrameRate, "Frame rate target" },
            { LocKeys.SettingAdaptiveResolution, "Adaptive resolution" },
            { LocKeys.SettingPerformanceOverlay, "Performance overlay" },

            { LocKeys.ValueOn, "On" },
            { LocKeys.ValueOff, "Off" },
            { LocKeys.GraphicsPerformance, "Performance" },
            { LocKeys.GraphicsBalanced, "Balanced" },
            { LocKeys.GraphicsQuality, "Quality" },

            { LocKeys.DiagnosticsTitle, "INPUT DIAGNOSTICS" },
        };

        /// <summary>True when the active language lays out right-to-left (Arabic). Always false for now.</summary>
        public static bool IsRightToLeft => false;

        public static bool HasKey(string key)
        {
            return key != null && s_English.ContainsKey(key);
        }

        public static string Get(string key)
        {
            if (key == null)
            {
                return string.Empty;
            }

            return s_English.TryGetValue(key, out string value) ? value : key;
        }

        /// <summary>All keys in the active table. Used by tests to verify coverage.</summary>
        public static IEnumerable<string> Keys => s_English.Keys;
    }
}
