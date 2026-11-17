using NeonRequiem.Player;
using UnityEngine;

namespace NeonRequiem.World
{
    /// <summary>
    /// Catches a player who has fallen out of the playable space and puts them back on the last
    /// stable ground they stood on. Also serves the pause menu's "return to spawn" action.
    /// </summary>
    public sealed class FallRecovery : MonoBehaviour
    {
        private PlayerController m_Player;
        private float m_KillPlaneY;
        private Vector3 m_SpawnPosition;
        private float m_SpawnYaw;

        /// <summary>How many times the player has been recovered from a fall.</summary>
        public int RecoveryCount { get; private set; }

        public void Initialize(PlayerController player, float killPlaneY, Vector3 spawnPosition, float spawnYaw)
        {
            m_Player = player;
            m_KillPlaneY = killPlaneY;
            m_SpawnPosition = spawnPosition;
            m_SpawnYaw = spawnYaw;
        }

        /// <summary>Sends the player back to the scene's spawn point and forgets recorded safe positions.</summary>
        public void ReturnToSpawn()
        {
            if (m_Player == null)
            {
                return;
            }

            m_Player.ClearSafePositions();
            m_Player.Teleport(m_SpawnPosition, m_SpawnYaw);
        }

        private void Update()
        {
            if (m_Player == null || m_Player.transform.position.y >= m_KillPlaneY)
            {
                return;
            }

            RecoveryCount++;
            m_Player.RespawnAtSafePosition(m_SpawnPosition, m_SpawnYaw);
        }
    }
}
