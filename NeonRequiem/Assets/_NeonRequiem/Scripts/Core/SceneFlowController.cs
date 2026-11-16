using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NeonRequiem.Core
{
    /// <summary>
    /// Single entry point for scene transitions. Loads one scene at a time, moves the game state
    /// through Loading, and refuses requests it cannot honour instead of failing silently.
    /// The scene's installer calls <see cref="GameManager.BeginPlaying"/> once its content is ready.
    /// </summary>
    public sealed class SceneFlowController : MonoBehaviour
    {
        private const string LogCategory = "SceneFlow";

        private GameManager m_GameManager;
        private Coroutine m_ActiveLoad;

        /// <summary>Raised when a load starts, with the scene name.</summary>
        public event Action<string> LoadStarted;

        /// <summary>Raised when the scene is active, with the scene name.</summary>
        public event Action<string> LoadCompleted;

        public bool IsLoading => m_ActiveLoad != null;

        public string CurrentSceneName => SceneManager.GetActiveScene().name;

        public void Initialize(GameManager gameManager)
        {
            m_GameManager = gameManager != null ? gameManager : throw new ArgumentNullException(nameof(gameManager));
        }

        /// <summary>Starts loading <paramref name="sceneName"/> as the single active scene.</summary>
        /// <returns>False when the request was rejected (already loading, unknown scene, bad state).</returns>
        public bool LoadScene(string sceneName)
        {
            if (m_GameManager == null)
            {
                NRLog.Error(LogCategory, "LoadScene called before Initialize.", this);
                return false;
            }

            if (string.IsNullOrEmpty(sceneName))
            {
                NRLog.Error(LogCategory, "LoadScene called with an empty scene name.", this);
                return false;
            }

            if (IsLoading)
            {
                NRLog.Warning(LogCategory, "Ignoring request to load '" + sceneName + "': a load is already running.", this);
                return false;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                NRLog.Error(
                    LogCategory,
                    "Scene '" + sceneName + "' is not in the Build Settings scene list. " +
                    "Run 'Neon Requiem > Validate Project Setup' in the editor.",
                    this);
                return false;
            }

            if (!m_GameManager.BeginLoading())
            {
                NRLog.Warning(
                    LogCategory,
                    "Cannot load '" + sceneName + "' from state " + m_GameManager.State.Current + ".",
                    this);
                return false;
            }

            m_ActiveLoad = StartCoroutine(LoadRoutine(sceneName));
            return true;
        }

        private IEnumerator LoadRoutine(string sceneName)
        {
            LoadStarted?.Invoke(sceneName);

            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (operation == null)
            {
                NRLog.Error(LogCategory, "Unity refused to load scene '" + sceneName + "'.", this);
                m_ActiveLoad = null;
                yield break;
            }

            while (!operation.isDone)
            {
                yield return null;
            }

            m_ActiveLoad = null;
            NRLog.Info(LogCategory, "Scene '" + sceneName + "' loaded.");
            LoadCompleted?.Invoke(sceneName);
        }
    }
}
