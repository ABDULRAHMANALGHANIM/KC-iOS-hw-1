using NeonRequiem.Controls;
using NeonRequiem.Core;
using NeonRequiem.Player;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace NeonRequiem.Cameras
{
    /// <summary>
    /// Third-person orbit camera. Touch, mouse and stick look all arrive as degrees from the
    /// input router; this component follows the player, keeps the view out of geometry and
    /// applies the accessibility options. Runs in LateUpdate so it always sees the character's
    /// final position for the frame.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class ThirdPersonCameraController : MonoBehaviour, ICameraYawSource
    {
        /// <summary>Layers the camera refuses to pass through.</summary>
        public const int CollisionMask = GameLayers.WorldGeometryMask;

        private const float MaxDeltaTime = 0.05f;

        [SerializeField] private CameraTuning m_Tuning = new CameraTuning();

        private Camera m_Camera;
        private OrbitCameraModel m_Model;
        private PlayerController m_Player;
        private PlayerInputRouter m_Input;
        private SettingsManager m_Settings;
        private Vector3 m_Pivot;
        private bool m_HasPivot;

        public float YawDegrees => m_Model != null ? m_Model.Yaw : transform.eulerAngles.y;

        public OrbitCameraModel Model => m_Model;

        public CameraTuning Tuning => m_Tuning;

        public void Initialize(PlayerInputRouter input, SettingsManager settings)
        {
            EnsureModel();
            m_Input = input;

            if (m_Settings != null)
            {
                m_Settings.Changed -= OnSettingsChanged;
            }

            m_Settings = settings;
            if (m_Settings != null)
            {
                m_Settings.Changed += OnSettingsChanged;
                OnSettingsChanged(m_Settings.Current);
            }
        }

        /// <summary>Starts following <paramref name="player"/> and snaps behind them.</summary>
        public void SetTarget(PlayerController player)
        {
            if (m_Player != null)
            {
                m_Player.Teleported -= SnapToTarget;
            }

            m_Player = player;
            if (m_Player != null)
            {
                m_Player.Teleported += SnapToTarget;
                SnapToTarget();
            }
        }

        /// <summary>Re-centres the camera behind the player with no smoothing (spawn, teleport, respawn).</summary>
        public void SnapToTarget()
        {
            if (m_Player == null)
            {
                return;
            }

            EnsureModel();
            float yaw = m_Player.Locomotion != null ? m_Player.Locomotion.FacingYaw : m_Player.transform.eulerAngles.y;
            m_Model.Reset(yaw);
            m_Pivot = m_Player.CameraTarget.position;
            m_HasPivot = true;
            m_Camera.fieldOfView = m_Tuning.fieldOfView;
            ApplyTransform();
        }

        private void Awake()
        {
            EnsureModel();
        }

        private void OnDestroy()
        {
            if (m_Settings != null)
            {
                m_Settings.Changed -= OnSettingsChanged;
            }

            if (m_Player != null)
            {
                m_Player.Teleported -= SnapToTarget;
            }
        }

        private void LateUpdate()
        {
            // The camera is frozen while paused (time scale zero) and has nothing to do without a target.
            if (m_Player == null || Time.deltaTime <= 0f)
            {
                return;
            }

            float dt = Mathf.Min(Time.unscaledDeltaTime, MaxDeltaTime);
            GameSettings settings = m_Settings != null ? m_Settings.Current : null;
            bool reduceMotion = settings != null && settings.reduceCameraMotion;
            bool assist = settings == null || settings.cameraAssist;

            Vector2 look = m_Input != null ? m_Input.Current.LookDegrees : Vector2.zero;
            m_Model.ApplyLook(look, dt);

            PlayerLocomotion locomotion = m_Player.Locomotion;
            if (assist && locomotion != null)
            {
                Vector3 velocity = locomotion.Velocity;
                float planarSpeed = locomotion.PlanarSpeed;
                if (planarSpeed > 0.01f)
                {
                    m_Model.ApplyFollowAssist(NRMath.DirectionToYaw(velocity), planarSpeed, dt);
                }
            }

            UpdatePivot(dt, reduceMotion);
            UpdateCollision(dt);
            UpdateLens(dt, reduceMotion, locomotion);
            ApplyTransform();
        }

        private void UpdatePivot(float dt, bool reduceMotion)
        {
            Vector3 target = m_Player.CameraTarget.position;
            if (!m_HasPivot)
            {
                m_Pivot = target;
                m_HasPivot = true;
                return;
            }

            float planarSharpness = reduceMotion ? m_Tuning.reducedMotionFollowSharpness : m_Tuning.followSharpness;
            float verticalSharpness = reduceMotion ? m_Tuning.reducedMotionFollowSharpness : m_Tuning.verticalFollowSharpness;
            float planarFactor = NRMath.DampFactor(planarSharpness, dt);
            m_Pivot.x += (target.x - m_Pivot.x) * planarFactor;
            m_Pivot.z += (target.z - m_Pivot.z) * planarFactor;
            m_Pivot.y = NRMath.Damp(m_Pivot.y, target.y, verticalSharpness, dt);
        }

        private void UpdateCollision(float dt)
        {
            Vector3 back = -m_Model.Forward;
            float free = m_Tuning.distance;
            if (Physics.SphereCast(
                    m_Pivot,
                    m_Tuning.collisionRadius,
                    back,
                    out RaycastHit hit,
                    m_Tuning.distance,
                    CollisionMask,
                    QueryTriggerInteraction.Ignore))
            {
                free = hit.distance - m_Tuning.collisionPadding;
            }

            m_Model.UpdateDistance(free, dt);
        }

        private void UpdateLens(float dt, bool reduceMotion, PlayerLocomotion locomotion)
        {
            float target = m_Tuning.fieldOfView;
            if (!reduceMotion && locomotion != null && locomotion.IsSprinting)
            {
                target += m_Tuning.sprintFieldOfViewBoost;
            }

            m_Camera.fieldOfView = reduceMotion
                ? target
                : NRMath.Damp(m_Camera.fieldOfView, target, m_Tuning.fieldOfViewSharpness, dt);
        }

        private void ApplyTransform()
        {
            Vector3 position = m_Pivot - m_Model.Forward * m_Model.Distance;
            Quaternion rotation = Quaternion.Euler(m_Model.Pitch, m_Model.Yaw, 0f);
            transform.SetPositionAndRotation(position, rotation);
        }

        private void OnSettingsChanged(GameSettings settings)
        {
            if (m_Camera == null || settings == null)
            {
                return;
            }

            // The Performance preset skips the post-processing stack entirely (bloom is the most
            // expensive full-screen effect on mobile GPUs).
            UniversalAdditionalCameraData data = m_Camera.GetUniversalAdditionalCameraData();
            if (data != null)
            {
                data.renderPostProcessing = settings.graphicsPreset != GraphicsPreset.Performance;
            }
        }

        private void EnsureModel()
        {
            if (m_Camera == null)
            {
                m_Camera = GetComponent<Camera>();
            }

            if (m_Model == null)
            {
                m_Model = new OrbitCameraModel(m_Tuning);
            }
        }
    }
}
