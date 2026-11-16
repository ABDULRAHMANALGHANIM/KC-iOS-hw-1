using System.Collections.Generic;
using UnityEngine;

namespace NeonRequiem.Core
{
    /// <summary>Key/value persistence for settings. Abstracted so tests never touch PlayerPrefs.</summary>
    public interface ISettingsStore
    {
        bool HasKey(string key);

        int GetInt(string key, int defaultValue);

        float GetFloat(string key, float defaultValue);

        void SetInt(string key, int value);

        void SetFloat(string key, float value);

        /// <summary>Commits pending writes to durable storage.</summary>
        void Flush();
    }

    /// <summary>Stores settings in PlayerPrefs (non-critical preferences only).</summary>
    public sealed class PlayerPrefsSettingsStore : ISettingsStore
    {
        public bool HasKey(string key) => PlayerPrefs.HasKey(key);

        public int GetInt(string key, int defaultValue) => PlayerPrefs.GetInt(key, defaultValue);

        public float GetFloat(string key, float defaultValue) => PlayerPrefs.GetFloat(key, defaultValue);

        public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);

        public void SetFloat(string key, float value) => PlayerPrefs.SetFloat(key, value);

        public void Flush() => PlayerPrefs.Save();
    }

    /// <summary>Volatile store used by tests and as a safe fallback.</summary>
    public sealed class InMemorySettingsStore : ISettingsStore
    {
        private readonly Dictionary<string, float> m_Floats = new Dictionary<string, float>();
        private readonly Dictionary<string, int> m_Ints = new Dictionary<string, int>();

        public int FlushCount { get; private set; }

        public bool HasKey(string key) => m_Floats.ContainsKey(key) || m_Ints.ContainsKey(key);

        public int GetInt(string key, int defaultValue) => m_Ints.TryGetValue(key, out int value) ? value : defaultValue;

        public float GetFloat(string key, float defaultValue) => m_Floats.TryGetValue(key, out float value) ? value : defaultValue;

        public void SetInt(string key, int value) => m_Ints[key] = value;

        public void SetFloat(string key, float value) => m_Floats[key] = value;

        public void Flush() => FlushCount++;
    }
}
