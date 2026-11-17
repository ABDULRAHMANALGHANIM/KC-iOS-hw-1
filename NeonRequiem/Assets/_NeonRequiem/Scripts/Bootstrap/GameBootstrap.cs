using NeonRequiem.Core;
using UnityEngine;

namespace NeonRequiem.App
{
    /// <summary>
    /// Entry point of the game. Lives in the Bootstrap scene (build index 0): creates the
    /// persistent runtime, then hands over to the first playable scene.
    /// </summary>
    [DefaultExecutionOrder(ExecutionOrder.Bootstrap)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        private const string LogCategory = "Bootstrap";

        [Tooltip("Scene loaded once the runtime services are up. Must be in the Build Settings scene list.")]
        [SerializeField] private string m_FirstScene = SceneIds.MovementTest;

        private void Awake()
        {
            GameRuntime.EnsureExists();
        }

        private void Start()
        {
            GameRuntime runtime = GameRuntime.Instance;
            if (runtime == null)
            {
                NRLog.Error(LogCategory, "Runtime services are missing; cannot continue.", this);
                return;
            }

            if (!runtime.SceneFlow.LoadScene(m_FirstScene))
            {
                NRLog.Error(LogCategory, "Could not start loading the first scene '" + m_FirstScene + "'.", this);
            }
        }
    }
}
