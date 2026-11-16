using System;

namespace NeonRequiem.Core
{
    /// <summary>
    /// Loads, validates, persists and broadcasts <see cref="GameSettings"/>. All edits go through
    /// <see cref="Modify"/> so listeners (touch layout, camera, performance manager) are notified
    /// exactly once per change and stored values are always sanitized.
    /// </summary>
    public sealed class SettingsManager
    {
        /// <summary>Bump when a stored key changes meaning; add the migration in <see cref="Load"/>.</summary>
        public const int SchemaVersion = 1;

        private const string LogCategory = "Settings";
        private const string Prefix = "nr.settings.";
        private const string KeySchema = Prefix + "schema";
        private const string KeyLookSensitivity = Prefix + "lookSensitivity";
        private const string KeyInvertLookY = Prefix + "invertLookY";
        private const string KeyLeftHanded = Prefix + "leftHanded";
        private const string KeyFloatingJoystick = Prefix + "floatingJoystick";
        private const string KeyTouchScale = Prefix + "touchScale";
        private const string KeyTouchOpacity = Prefix + "touchOpacity";
        private const string KeyCameraAssist = Prefix + "cameraAssist";
        private const string KeyReduceCameraMotion = Prefix + "reduceCameraMotion";
        private const string KeyGraphicsPreset = Prefix + "graphicsPreset";
        private const string KeyFrameRate = Prefix + "frameRate";
        private const string KeyAdaptiveResolution = Prefix + "adaptiveResolution";
        private const string KeyPerformanceOverlay = Prefix + "performanceOverlay";

        private readonly ISettingsStore m_Store;
        private readonly GameSettings m_Defaults;
        private readonly GameSettings m_Current = new GameSettings();

        /// <summary>Raised after settings were loaded, modified or reset.</summary>
        public event Action<GameSettings> Changed;

        /// <param name="store">Persistence backend.</param>
        /// <param name="defaults">Values used on first run and by <see cref="ResetToDefaults"/>; null uses built-in defaults.</param>
        public SettingsManager(ISettingsStore store, GameSettings defaults = null)
        {
            m_Store = store ?? throw new ArgumentNullException(nameof(store));
            m_Defaults = defaults != null ? defaults.Clone() : new GameSettings();
            m_Defaults.Sanitize();
            m_Current.CopyFrom(m_Defaults);
        }

        /// <summary>Live settings. Treat as read-only; use <see cref="Modify"/> to change values.</summary>
        public GameSettings Current => m_Current;

        /// <summary>True when the last <see cref="Load"/> found previously stored settings.</summary>
        public bool HasStoredSettings { get; private set; }

        public void Load()
        {
            m_Current.CopyFrom(m_Defaults);
            HasStoredSettings = m_Store.HasKey(KeySchema);

            if (HasStoredSettings)
            {
                int storedSchema = m_Store.GetInt(KeySchema, SchemaVersion);
                if (storedSchema > SchemaVersion)
                {
                    // Written by a newer build. Unknown keys are ignored and known keys keep their meaning,
                    // so read what we understand instead of discarding the player's preferences.
                    NRLog.Warning(LogCategory, "Stored settings use newer schema " + storedSchema + "; reading known keys only.");
                }

                // Schema migrations go here, oldest first (none yet: version 1 is the first schema).
                m_Current.lookSensitivity = m_Store.GetFloat(KeyLookSensitivity, m_Current.lookSensitivity);
                m_Current.invertLookY = ReadBool(KeyInvertLookY, m_Current.invertLookY);
                m_Current.leftHandedLayout = ReadBool(KeyLeftHanded, m_Current.leftHandedLayout);
                m_Current.floatingJoystick = ReadBool(KeyFloatingJoystick, m_Current.floatingJoystick);
                m_Current.touchControlScale = m_Store.GetFloat(KeyTouchScale, m_Current.touchControlScale);
                m_Current.touchControlOpacity = m_Store.GetFloat(KeyTouchOpacity, m_Current.touchControlOpacity);
                m_Current.cameraAssist = ReadBool(KeyCameraAssist, m_Current.cameraAssist);
                m_Current.reduceCameraMotion = ReadBool(KeyReduceCameraMotion, m_Current.reduceCameraMotion);
                m_Current.graphicsPreset = (GraphicsPreset)m_Store.GetInt(KeyGraphicsPreset, (int)m_Current.graphicsPreset);
                m_Current.frameRateTarget = (FrameRateTarget)m_Store.GetInt(KeyFrameRate, (int)m_Current.frameRateTarget);
                m_Current.adaptiveResolution = ReadBool(KeyAdaptiveResolution, m_Current.adaptiveResolution);
                m_Current.showPerformanceOverlay = ReadBool(KeyPerformanceOverlay, m_Current.showPerformanceOverlay);
            }

            m_Current.Sanitize();
            Changed?.Invoke(m_Current);
        }

        /// <summary>Applies <paramref name="edit"/>, sanitizes, persists and notifies listeners.</summary>
        public void Modify(Action<GameSettings> edit)
        {
            if (edit == null)
            {
                throw new ArgumentNullException(nameof(edit));
            }

            edit(m_Current);
            m_Current.Sanitize();
            Save();
            Changed?.Invoke(m_Current);
        }

        public void ResetToDefaults()
        {
            m_Current.CopyFrom(m_Defaults);
            Save();
            Changed?.Invoke(m_Current);
        }

        public void Save()
        {
            m_Store.SetInt(KeySchema, SchemaVersion);
            m_Store.SetFloat(KeyLookSensitivity, m_Current.lookSensitivity);
            WriteBool(KeyInvertLookY, m_Current.invertLookY);
            WriteBool(KeyLeftHanded, m_Current.leftHandedLayout);
            WriteBool(KeyFloatingJoystick, m_Current.floatingJoystick);
            m_Store.SetFloat(KeyTouchScale, m_Current.touchControlScale);
            m_Store.SetFloat(KeyTouchOpacity, m_Current.touchControlOpacity);
            WriteBool(KeyCameraAssist, m_Current.cameraAssist);
            WriteBool(KeyReduceCameraMotion, m_Current.reduceCameraMotion);
            m_Store.SetInt(KeyGraphicsPreset, (int)m_Current.graphicsPreset);
            m_Store.SetInt(KeyFrameRate, (int)m_Current.frameRateTarget);
            WriteBool(KeyAdaptiveResolution, m_Current.adaptiveResolution);
            WriteBool(KeyPerformanceOverlay, m_Current.showPerformanceOverlay);
            m_Store.Flush();
            HasStoredSettings = true;
        }

        private bool ReadBool(string key, bool defaultValue)
        {
            return m_Store.GetInt(key, defaultValue ? 1 : 0) != 0;
        }

        private void WriteBool(string key, bool value)
        {
            m_Store.SetInt(key, value ? 1 : 0);
        }
    }
}
