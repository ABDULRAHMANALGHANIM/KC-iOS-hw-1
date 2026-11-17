using NeonRequiem.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace NeonRequiem.EditorTools
{
    /// <summary>
    /// Editor conveniences: quick scene access, "play from the beginning", and opening the
    /// Bootstrap scene the first time the project is loaded so pressing Play works immediately.
    /// </summary>
    [InitializeOnLoad]
    public static class NeonRequiemMenu
    {
        private const string SceneRoot = "Assets/_NeonRequiem/Scenes/";
        private const string BootstrapScenePath = SceneRoot + "Bootstrap/" + SceneIds.Bootstrap + ".unity";
        private const string MovementTestScenePath = SceneRoot + "TestScenes/" + SceneIds.MovementTest + ".unity";
        private const string ControlsTestScenePath = SceneRoot + "TestScenes/" + SceneIds.MobileControlsTest + ".unity";
        private const string FirstOpenSessionKey = "NeonRequiem.FirstOpenHandled";

        static NeonRequiemMenu()
        {
            EditorApplication.delayCall += OpenBootstrapOnFirstLoad;
        }

        [MenuItem("Neon Requiem/Play From Bootstrap", priority = 20)]
        private static void PlayFromBootstrap()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (OpenScene(BootstrapScenePath))
            {
                EditorApplication.EnterPlaymode();
            }
        }

        [MenuItem("Neon Requiem/Open Scene/Bootstrap", priority = 40)]
        private static void OpenBootstrap() => OpenScene(BootstrapScenePath);

        [MenuItem("Neon Requiem/Open Scene/Movement Test", priority = 41)]
        private static void OpenMovementTest() => OpenScene(MovementTestScenePath);

        [MenuItem("Neon Requiem/Open Scene/Mobile Controls Test", priority = 42)]
        private static void OpenControlsTest() => OpenScene(ControlsTestScenePath);

        private static bool OpenScene(string path)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return false;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
            {
                NRLog.Error("Editor", "Scene not found: " + path);
                return false;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return false;
            }

            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            return true;
        }

        /// <summary>
        /// A freshly cloned project opens on an empty untitled scene. Replace it with the Bootstrap
        /// scene once per editor session, and only when nothing would be lost.
        /// </summary>
        private static void OpenBootstrapOnFirstLoad()
        {
            if (SessionState.GetBool(FirstOpenSessionKey, false))
            {
                return;
            }

            SessionState.SetBool(FirstOpenSessionKey, true);

            if (UnityEngine.Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (SceneManager.sceneCount != 1)
            {
                return;
            }

            Scene active = SceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(active.path) || active.isDirty)
            {
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BootstrapScenePath) != null)
            {
                EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
            }
        }
    }
}
