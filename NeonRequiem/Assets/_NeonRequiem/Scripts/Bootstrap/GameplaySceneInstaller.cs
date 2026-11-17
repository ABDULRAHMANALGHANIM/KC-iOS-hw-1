using NeonRequiem.Cameras;
using NeonRequiem.Core;
using NeonRequiem.Player;
using NeonRequiem.World;
using UnityEngine;

namespace NeonRequiem.App
{
    /// <summary>
    /// Assembles a playable scene: builds the arena, spawns the player, attaches the camera and
    /// connects the HUD, then tells the game to start playing. One of these sits in every
    /// gameplay scene; it is the scene-level counterpart of <see cref="GameRuntime"/>.
    /// </summary>
    public sealed class GameplaySceneInstaller : MonoBehaviour
    {
        private const string LogCategory = "Installer";

        [Tooltip("Scene camera to drive. Falls back to the camera tagged MainCamera, then creates one.")]
        [SerializeField] private Camera m_Camera = null;

        [Tooltip("Directional light restyled as moonlight. Optional.")]
        [SerializeField] private Light m_Sun = null;

        [SerializeField] private ArenaLayout m_ArenaLayout = ArenaLayout.MovementCourse;

        [Tooltip("Show the live input diagnostics panel (mobile controls test scene).")]
        [SerializeField] private bool m_ShowInputDiagnostics = false;

        [Tooltip("Movement tuning for this scene. Empty uses Resources/NeonRequiem/PlayerMovementConfig.")]
        [SerializeField] private PlayerMovementConfig m_MovementConfig = null;

        private GameRuntime m_Runtime;

        /// <summary>The installer of the currently loaded gameplay scene, or null.</summary>
        public static GameplaySceneInstaller Active { get; private set; }

        public PlayerController Player { get; private set; }

        public ThirdPersonCameraController CameraController { get; private set; }

        public FallRecovery FallRecovery { get; private set; }

        public ArenaInfo Arena { get; private set; }

        /// <summary>True once the scene has been assembled and gameplay has started.</summary>
        public bool IsReady { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Active = null;
        }

        private void Awake()
        {
            Active = this;
        }

        private void Start()
        {
            m_Runtime = GameRuntime.EnsureExists();

            Camera sceneCamera = ResolveCamera();
            Arena = TestArenaBuilder.Build(m_ArenaLayout, m_Runtime.Materials, m_Sun, sceneCamera);

            CameraController = sceneCamera.GetComponent<ThirdPersonCameraController>();
            if (CameraController == null)
            {
                CameraController = sceneCamera.gameObject.AddComponent<ThirdPersonCameraController>();
            }

            CameraController.Initialize(m_Runtime.Input, m_Runtime.Settings);

            Player = PlayerFactory.Create(ResolveTuning(), m_Runtime.Materials, m_Runtime.Input, CameraController);
            Player.Teleport(Arena.SpawnPosition, Arena.SpawnYaw);
            CameraController.SetTarget(Player);

            FallRecovery = gameObject.AddComponent<FallRecovery>();
            FallRecovery.Initialize(Player, Arena.KillPlaneY, Arena.SpawnPosition, Arena.SpawnYaw);

            m_Runtime.Hud.BindPlayer(Player);
            m_Runtime.Hud.SetInputDiagnosticsVisible(m_ShowInputDiagnostics);
            m_Runtime.Hud.RespawnRequested += OnRespawnRequested;

            if (!m_Runtime.GameManager.BeginPlaying())
            {
                NRLog.Warning(LogCategory, "Game state did not enter Playing (state: " + m_Runtime.GameManager.State.Current + ").", this);
            }

            IsReady = true;
        }

        private void OnDestroy()
        {
            if (Active == this)
            {
                Active = null;
            }

            // The runtime outlives the scene; detach everything that referenced scene objects.
            if (m_Runtime == null || m_Runtime.Hud == null)
            {
                return;
            }

            m_Runtime.Hud.RespawnRequested -= OnRespawnRequested;
            m_Runtime.Hud.SetInputDiagnosticsVisible(false);
            if (Player != null)
            {
                m_Runtime.Hud.UnbindPlayer(Player);
            }
        }

        private void OnRespawnRequested()
        {
            if (FallRecovery != null)
            {
                FallRecovery.ReturnToSpawn();
            }
        }

        private Camera ResolveCamera()
        {
            if (m_Camera != null)
            {
                return m_Camera;
            }

            Camera main = Camera.main;
            if (main != null)
            {
                return main;
            }

            NRLog.Warning(LogCategory, "Scene has no camera; creating one.", this);
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var created = go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
            return created;
        }

        private MovementTuning ResolveTuning()
        {
            PlayerMovementConfig config = m_MovementConfig;
            if (config == null)
            {
                config = Resources.Load<PlayerMovementConfig>(PlayerMovementConfig.DefaultResourcePath);
            }

            if (config != null)
            {
                return config.Tuning;
            }

            NRLog.Warning(
                LogCategory,
                "No PlayerMovementConfig found at Resources/" + PlayerMovementConfig.DefaultResourcePath + "; using built-in defaults.",
                this);
            return new MovementTuning();
        }
    }
}
