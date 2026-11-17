using NeonRequiem.Controls;
using NeonRequiem.Core;
using NeonRequiem.Optimization;
using NeonRequiem.UI;
using NeonRequiem.World;
using UnityEngine;

namespace NeonRequiem.App
{
    /// <summary>
    /// The persistent service container and composition root. Created once, survives scene
    /// loads, and is the only place where long-lived systems are constructed and wired together
    /// (in dependency order). Everything else receives its dependencies explicitly; nothing
    /// looks services up by type at runtime.
    /// </summary>
    [DefaultExecutionOrder(ExecutionOrder.Runtime)]
    public sealed class GameRuntime : MonoBehaviour
    {
        private const string LogCategory = "Runtime";
        private const string ObjectName = "[NeonRequiem Runtime]";

        private InputActionsSource m_ActionsSource;

        /// <summary>The live runtime, or null before boot and after shutdown.</summary>
        public static GameRuntime Instance { get; private set; }

        /// <summary>
        /// Test seam: when set before the runtime is created, settings are read from and written
        /// to this store instead of PlayerPrefs, so automated tests never touch a real profile.
        /// </summary>
        public static ISettingsStore SettingsStoreOverride { get; set; }

        public SettingsManager Settings { get; private set; }

        public GameManager GameManager { get; private set; }

        public SceneFlowController SceneFlow { get; private set; }

        public PlayerInputRouter Input { get; private set; }

        public PerformanceManager Performance { get; private set; }

        public NeonMaterialLibrary Materials { get; private set; }

        public HudController Hud { get; private set; }

        /// <summary>
        /// Returns the runtime, creating it on first use. Gameplay scenes call this so they can be
        /// started directly from the editor without going through the Bootstrap scene.
        /// </summary>
        public static GameRuntime EnsureExists()
        {
            if (Instance != null)
            {
                return Instance;
            }

            var go = new GameObject(ObjectName);
            DontDestroyOnLoad(go);
            var runtime = go.AddComponent<GameRuntime>();
            Instance = runtime;
            runtime.Build();
            NRLog.Info(LogCategory, "Runtime services created.");
            return runtime;
        }

        /// <summary>Destroys the runtime immediately. Used by tests to isolate cases.</summary>
        public static void Shutdown()
        {
            if (Instance == null)
            {
                return;
            }

            GameObject go = Instance.gameObject;
            Instance.Teardown();
            Instance = null;
            DestroyImmediate(go);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Required when "Enter Play Mode" is configured without a domain reload.
            Instance = null;
            SettingsStoreOverride = null;
        }

        private void Build()
        {
            // 1. Settings: everything below reads them.
            var defaults = new GameSettings
            {
                graphicsPreset = DeviceTierHeuristic.SuggestPreset(
                    Application.isMobilePlatform, SystemInfo.systemMemorySize, SystemInfo.processorCount),
                showPerformanceOverlay = Debug.isDebugBuild,
            };
            ISettingsStore store = SettingsStoreOverride ?? new PlayerPrefsSettingsStore();
            Settings = new SettingsManager(store, defaults);
            Settings.Load();

            // 2. Game state and scene flow.
            GameManager = gameObject.AddComponent<GameManager>();
            SceneFlow = gameObject.AddComponent<SceneFlowController>();
            SceneFlow.Initialize(GameManager);

            // 3. Input: hardware actions first, the touch source is added once the HUD exists.
            Input = gameObject.AddComponent<PlayerInputRouter>();
            Input.Initialize(Settings);
            m_ActionsSource = InputActionsSource.CreateFromResources();
            if (m_ActionsSource != null)
            {
                m_ActionsSource.Enable();
                Input.AddSource(m_ActionsSource);
            }

            // 4. Rendering and performance.
            Performance = gameObject.AddComponent<PerformanceManager>();
            Performance.Initialize(Settings, GameManager);
            Materials = new NeonMaterialLibrary();

            // 5. Interface.
            Hud = HudController.Create(transform, GameManager, Settings, Input, Performance);
            Input.AddSource(Hud.TouchControls);
        }

        private void Teardown()
        {
            if (m_ActionsSource != null)
            {
                if (Input != null)
                {
                    Input.RemoveSource(m_ActionsSource);
                }

                m_ActionsSource.Dispose();
                m_ActionsSource = null;
            }

            if (Materials != null)
            {
                Materials.Dispose();
                Materials = null;
            }
        }

        private void OnDestroy()
        {
            Teardown();
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
