using NeonRequiem.Core;
using UnityEngine;

namespace NeonRequiem.Player
{
    /// <summary>
    /// The player's physical body: a thin wrapper over Unity's CharacterController that reports
    /// what happened during each move. Holds no gameplay decisions.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMotor : MonoBehaviour, ICharacterMotor
    {
        /// <summary>Layers the body can stand on: everything except the player itself and non-solid utility layers.</summary>
        public const int GroundProbeMask = ~(
            (1 << GameLayers.Player) |
            (1 << GameLayers.IgnoreRaycast) |
            (1 << GameLayers.TransparentFX) |
            (1 << GameLayers.Water) |
            (1 << GameLayers.UI));

        private const float GroundProbeExtra = 0.4f;

        private CharacterController m_Controller;
        private float m_StepOffset;
        private bool m_Grounded;
        private Vector3 m_GroundNormal = Vector3.up;

        public Vector3 Position => transform.position;

        public bool IsGrounded => m_Grounded;

        public Vector3 GroundNormal => m_GroundNormal;

        public CharacterController Controller => m_Controller;

        /// <summary>Applies capsule dimensions and slope settings from tuning.</summary>
        public void Configure(MovementTuning tuning)
        {
            EnsureController();
            m_Controller.height = tuning.height;
            m_Controller.radius = tuning.radius;
            m_Controller.center = new Vector3(0f, tuning.height * 0.5f, 0f);
            m_Controller.slopeLimit = tuning.slopeLimit;
            m_Controller.skinWidth = tuning.skinWidth;
            m_Controller.minMoveDistance = 0f;
            m_StepOffset = Mathf.Min(tuning.stepOffset, tuning.height);
            m_Controller.stepOffset = m_StepOffset;
        }

        public MotorMoveResult Move(Vector3 displacement)
        {
            EnsureController();

            // Stepping is a ground behaviour; in the air it makes the capsule snag and hop on wall edges.
            m_Controller.stepOffset = m_Grounded ? m_StepOffset : 0f;

            Vector3 before = transform.position;
            CollisionFlags flags = m_Controller.Move(displacement);
            Vector3 after = transform.position;

            m_Grounded = (flags & CollisionFlags.Below) != 0;
            m_GroundNormal = m_Grounded ? ProbeGroundNormal() : Vector3.up;

            return new MotorMoveResult
            {
                ActualDisplacement = after - before,
                Grounded = m_Grounded,
                HitSides = (flags & CollisionFlags.Sides) != 0,
                HitAbove = (flags & CollisionFlags.Above) != 0,
            };
        }

        public void Teleport(Vector3 position)
        {
            EnsureController();

            // A CharacterController overrides direct transform writes unless it is disabled first.
            bool wasEnabled = m_Controller.enabled;
            m_Controller.enabled = false;
            transform.position = position;
            m_Controller.enabled = wasEnabled;

            m_Grounded = false;
            m_GroundNormal = Vector3.up;
        }

        private void Awake()
        {
            EnsureController();
        }

        private void EnsureController()
        {
            if (m_Controller == null)
            {
                m_Controller = GetComponent<CharacterController>();
            }
        }

        /// <summary>
        /// Surface normal directly below the capsule's centre line. A ray is used instead of the
        /// capsule's contact normal because contacts on a ledge corner are tilted and would be
        /// mistaken for a steep slope; when the centre hangs over an edge the ground counts as flat.
        /// </summary>
        private Vector3 ProbeGroundNormal()
        {
            float radius = m_Controller.radius;
            Vector3 origin = transform.position + m_Controller.center
                             - Vector3.up * (m_Controller.height * 0.5f - radius);
            float distance = radius + m_Controller.skinWidth + GroundProbeExtra;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, distance, GroundProbeMask, QueryTriggerInteraction.Ignore))
            {
                return hit.normal;
            }

            return Vector3.up;
        }
    }
}
