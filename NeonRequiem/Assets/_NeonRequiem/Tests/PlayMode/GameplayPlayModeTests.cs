using System;
using System.Collections;
using NeonRequiem.App;
using NeonRequiem.Controls;
using NeonRequiem.Core;
using NeonRequiem.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace NeonRequiem.Tests
{
    /// <summary>
    /// End-to-end checks of the playable foundation, driven through the same input router the
    /// touch controls feed: boot, movement, jump, dodge, pause, fall recovery and scene changes.
    /// They use the real CharacterController and scenes, so they run in the Unity Test Runner
    /// (PlayMode tab) only. Settings are redirected to an in-memory store.
    /// </summary>
    public sealed class GameplayPlayModeTests
    {
        private const float BootTimeout = 15f;

        private ScriptedInputSource m_Input;

        private static GameRuntime Runtime => GameRuntime.Instance;

        private static GameplaySceneInstaller Installer => GameplaySceneInstaller.Active;

        private static PlayerController Player => Installer != null ? Installer.Player : null;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            GameRuntime.Shutdown();
            GameRuntime.SettingsStoreOverride = new InMemorySettingsStore();
            Time.timeScale = 1f;

            yield return SceneManager.LoadSceneAsync(SceneIds.MovementTest, LoadSceneMode.Single);
            yield return WaitUntil(() => Installer != null && Installer.IsReady, BootTimeout, "the gameplay scene to be assembled");

            m_Input = new ScriptedInputSource();
            Runtime.Input.AddSource(m_Input);

            yield return WaitUntil(() => Player.Locomotion.Mode == LocomotionMode.Grounded, 5f, "the player to settle on the ground");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Runtime != null && m_Input != null)
            {
                Runtime.Input.RemoveSource(m_Input);
            }

            GameRuntime.Shutdown();
            GameRuntime.SettingsStoreOverride = null;
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_Boots_IntoPlayingState_WithAGroundedPlayer()
        {
            Assert.IsNotNull(Runtime);
            Assert.AreEqual(GameFlowState.Playing, Runtime.GameManager.State.Current);
            Assert.IsNotNull(Player);
            Assert.IsTrue(Player.IsInitialized);
            Assert.AreEqual(GameLayers.Player, Player.gameObject.layer);
            Assert.IsNotNull(Installer.CameraController);
            Assert.That(Vector3.Distance(Player.transform.position, Installer.Arena.SpawnPosition), Is.LessThan(0.5f));
            Assert.IsNotNull(Runtime.Hud.TouchControls);
            yield break;
        }

        [UnityTest]
        public IEnumerator ForwardInput_RunsThePlayerAwayFromTheCamera()
        {
            Vector3 start = Player.transform.position;
            m_Input.Move = new Vector2(0f, 1f);
            yield return WaitSeconds(1f);
            m_Input.Move = Vector2.zero;

            Vector3 moved = Player.transform.position - start;
            float expected = Player.Locomotion.Tuning.runSpeed;
            Assert.That(moved.z, Is.InRange(expected * 0.6f, expected * 1.2f), "Distance run in one second.");
            Assert.That(Mathf.Abs(moved.x), Is.LessThan(0.5f), "The spawn camera faces +Z, so stick-forward is +Z.");

            yield return WaitSeconds(0.3f);
            Assert.That(Player.Locomotion.PlanarSpeed, Is.LessThan(0.1f), "The player stops when the stick is released.");
        }

        [UnityTest]
        public IEnumerator Jump_LeavesTheGround_ReachesItsHeight_AndLands()
        {
            float ground = Player.transform.position.y;
            int landed = 0;
            Player.Locomotion.Landed += impact => landed++;

            m_Input.JumpHeld = true;
            m_Input.PressJump();
            yield return WaitUntil(() => Player.Locomotion.Mode == LocomotionMode.Airborne, 1f, "the jump to start");

            float apex = ground;
            float deadline = Time.time + 5f;
            while (Player.Locomotion.Mode != LocomotionMode.Grounded && Time.time < deadline)
            {
                apex = Mathf.Max(apex, Player.transform.position.y);
                yield return null;
            }

            m_Input.JumpHeld = false;
            float height = Player.Locomotion.Tuning.jumpHeight;
            Assert.AreEqual(LocomotionMode.Grounded, Player.Locomotion.Mode);
            Assert.That(apex - ground, Is.InRange(height * 0.85f, height * 1.1f));
            Assert.AreEqual(1, landed);
        }

        [UnityTest]
        public IEnumerator Dodge_CoversItsDistance_WithAnInvulnerabilityWindow()
        {
            Vector3 start = Player.transform.position;
            bool ended = false;
            bool sawInvulnerable = false;
            Player.Locomotion.DodgeEnded += () => ended = true;

            m_Input.Move = new Vector2(0f, 1f);
            m_Input.PressDodge();

            float deadline = Time.time + 3f;
            while (!ended && Time.time < deadline)
            {
                sawInvulnerable |= Player.Locomotion.IsInvulnerable;
                yield return null;
            }

            m_Input.Move = Vector2.zero;
            Assert.IsTrue(ended, "The dodge never finished.");
            Assert.IsTrue(sawInvulnerable);

            float distance = NRMath.PlanarMagnitude(Player.transform.position - start);
            float expected = Player.Locomotion.Tuning.dodgeDistance;
            Assert.That(distance, Is.InRange(expected * 0.85f, expected * 1.15f));
        }

        [UnityTest]
        public IEnumerator Pause_FreezesTime_ShowsTheMenu_AndBlocksGameplayInput()
        {
            Assert.IsTrue(Runtime.GameManager.Pause());
            yield return null;

            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsTrue(Runtime.Hud.PauseMenu.IsVisible);
            Assert.IsFalse(Runtime.Hud.TouchControls.IsVisible);

            Vector3 before = Player.transform.position;
            m_Input.Move = new Vector2(0f, 1f);
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            Assert.AreEqual(Vector2.zero, Runtime.Input.Current.Move, "Gameplay input must be zeroed while paused.");
            Assert.That(Vector3.Distance(before, Player.transform.position), Is.LessThan(1e-3f));
            m_Input.Move = Vector2.zero;

            Assert.IsTrue(Runtime.GameManager.Resume());
            yield return null;
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsFalse(Runtime.Hud.PauseMenu.IsVisible);
        }

        [UnityTest]
        public IEnumerator PauseButton_TogglesPause()
        {
            m_Input.PressPause();
            yield return WaitFrames(3);
            Assert.IsTrue(Runtime.GameManager.IsPaused);

            m_Input.PressPause();
            yield return WaitFrames(3);
            Assert.IsFalse(Runtime.GameManager.IsPaused);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator FallingOutOfTheWorld_ReturnsThePlayerToSolidGround()
        {
            float killPlane = Installer.Arena.KillPlaneY;
            Player.Teleport(new Vector3(0f, killPlane - 5f, 0f), 0f);

            yield return WaitUntil(() => Installer.FallRecovery.RecoveryCount == 1, 2f, "fall recovery to trigger");
            Assert.Greater(Player.transform.position.y, killPlane);

            yield return WaitUntil(() => Player.Locomotion.Mode == LocomotionMode.Grounded, 5f, "the player to land after recovery");
            Assert.That(Player.transform.position.y, Is.InRange(-0.2f, 0.5f));
        }

        [UnityTest]
        public IEnumerator Settings_ReconfigureTheTouchLayout_Live()
        {
            var jump = (RectTransform)Runtime.Hud.TouchControls.JumpButton.transform;
            Assert.AreEqual(1f, jump.anchorMin.x, "Right-handed layout keeps actions on the right.");

            Runtime.Settings.Modify(s => s.leftHandedLayout = true);
            yield return null;
            Assert.AreEqual(0f, jump.anchorMin.x, "Left-handed layout mirrors actions to the left.");

            float before = jump.sizeDelta.x;
            Runtime.Settings.Modify(s => s.touchControlScale = 1.5f);
            yield return null;
            Assert.Greater(jump.sizeDelta.x, before);
        }

        [UnityTest]
        public IEnumerator SceneFlow_LoadsAnotherScene_AndKeepsTheRuntime()
        {
            GameRuntime runtime = Runtime;
            PlayerController firstPlayer = Player;

            Assert.IsTrue(runtime.SceneFlow.LoadScene(SceneIds.MobileControlsTest));
            Assert.AreEqual(GameFlowState.Loading, runtime.GameManager.State.Current);

            yield return WaitUntil(
                () => SceneManager.GetActiveScene().name == SceneIds.MobileControlsTest && Installer != null && Installer.IsReady,
                BootTimeout,
                "the second scene to be assembled");

            Assert.AreSame(runtime, Runtime, "The runtime must survive the scene change.");
            Assert.AreEqual(GameFlowState.Playing, runtime.GameManager.State.Current);
            Assert.IsNotNull(Player);
            Assert.AreNotSame(firstPlayer, Player);
            Assert.IsFalse(runtime.SceneFlow.IsLoading);
        }

        [UnityTest]
        public IEnumerator SceneFlow_RejectsUnknownScenes()
        {
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("not in the Build Settings"));
            Assert.IsFalse(Runtime.SceneFlow.LoadScene("NoSuchScene"));
            Assert.AreEqual(GameFlowState.Playing, Runtime.GameManager.State.Current);
            yield break;
        }

        [UnityTest]
        public IEnumerator BootstrapScene_HandsOverToTheFirstPlayableScene()
        {
            Runtime.Input.RemoveSource(m_Input);
            GameRuntime.Shutdown();

            yield return SceneManager.LoadSceneAsync(SceneIds.Bootstrap, LoadSceneMode.Single);
            yield return WaitUntil(
                () => SceneManager.GetActiveScene().name == SceneIds.MovementTest && Installer != null && Installer.IsReady,
                BootTimeout,
                "the Bootstrap scene to load the first playable scene");

            Assert.IsNotNull(Runtime);
            Assert.AreEqual(GameFlowState.Playing, Runtime.GameManager.State.Current);
            Assert.IsNotNull(Player);
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds, string description)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    Assert.Fail("Timed out after " + timeoutSeconds + " s waiting for " + description + ".");
                }

                yield return null;
            }
        }

        private static IEnumerator WaitSeconds(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end)
            {
                yield return null;
            }
        }

        private static IEnumerator WaitFrames(int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                yield return null;
            }
        }

        /// <summary>Input source the tests drive directly, standing in for the touch controls.</summary>
        private sealed class ScriptedInputSource : IPlayerInputSource
        {
            private bool m_Jump;
            private bool m_Dodge;
            private bool m_Pause;

            public Vector2 Move { get; set; }

            public bool JumpHeld { get; set; }

            public void PressJump() => m_Jump = true;

            public void PressDodge() => m_Dodge = true;

            public void PressPause() => m_Pause = true;

            public void Sample(ref PlayerInputSample sample)
            {
                sample.MergeMove(Move);
                sample.JumpHeld |= JumpHeld;

                if (m_Jump)
                {
                    sample.JumpPressed = true;
                    m_Jump = false;
                }

                if (m_Dodge)
                {
                    sample.DodgePressed = true;
                    m_Dodge = false;
                }

                if (m_Pause)
                {
                    sample.PausePressed = true;
                    m_Pause = false;
                }
            }
        }
    }
}
