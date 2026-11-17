using UnityEngine;

namespace NeonRequiem.Player
{
    /// <summary>
    /// Designer-facing asset wrapping <see cref="MovementTuning"/>. The default instance lives at
    /// Resources/NeonRequiem/PlayerMovementConfig.asset; scenes may reference a different one
    /// through their installer.
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerMovementConfig", menuName = "Neon Requiem/Player Movement Config", order = 0)]
    public sealed class PlayerMovementConfig : ScriptableObject
    {
        public const string DefaultResourcePath = "NeonRequiem/PlayerMovementConfig";

        [SerializeField] private MovementTuning m_Tuning = new MovementTuning();

        /// <summary>The tuning values. Sanitized on load and whenever edited in the inspector.</summary>
        public MovementTuning Tuning => m_Tuning;

        private void OnEnable()
        {
            if (m_Tuning == null)
            {
                m_Tuning = new MovementTuning();
            }

            m_Tuning.Sanitize();
        }

        private void OnValidate()
        {
            if (m_Tuning != null)
            {
                m_Tuning.Sanitize();
            }
        }
    }
}
