using System.Collections.Generic;
using NeonRequiem.Controls;
using NeonRequiem.Core;
using NeonRequiem.EditorTools;
using NeonRequiem.Optimization;
using NeonRequiem.Player;
using NeonRequiem.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NeonRequiem.Tests
{
    /// <summary>
    /// Verifies the project configuration and the assets that code loads by path. These need the
    /// Unity Editor (asset database, project settings) and run in the Test Runner's EditMode tab.
    /// </summary>
    public sealed class ProjectSetupTests
    {
        [Test]
        public void ProjectValidator_ReportsNoErrors()
        {
            var errors = new List<string>();
            foreach (ProjectValidator.Issue issue in ProjectValidator.Validate())
            {
                if (issue.IsError)
                {
                    errors.Add(issue.Message);
                }
            }

            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [Test]
        public void PlayerLayer_MatchesTheCodeConstant()
        {
            Assert.AreEqual(GameLayers.Player, LayerMask.NameToLayer(GameLayers.PlayerName));
        }

        [Test]
        public void EveryGraphicsPreset_HasAQualityLevel()
        {
            Assert.GreaterOrEqual(PerformanceManager.FindQualityLevel(GraphicsPreset.Performance), 0);
            Assert.GreaterOrEqual(PerformanceManager.FindQualityLevel(GraphicsPreset.Balanced), 0);
            Assert.GreaterOrEqual(PerformanceManager.FindQualityLevel(GraphicsPreset.Quality), 0);
        }

        [Test]
        public void InputActionsAsset_ProvidesEveryGameplayAction()
        {
            InputActionAsset asset = Resources.Load<InputActionAsset>(InputActionsSource.ResourcePath);
            Assert.IsNotNull(asset, "Missing Resources/" + InputActionsSource.ResourcePath);

            InputActionsSource source = InputActionsSource.Create(Object.Instantiate(asset), true);
            Assert.IsNotNull(source, "The asset is missing the Gameplay map or one of its actions.");
            Assert.IsTrue(source.IsValid);
            source.Dispose();
        }

        [Test]
        public void InputActions_HaveKeyboardAndGamepadBindings()
        {
            InputActionAsset asset = Resources.Load<InputActionAsset>(InputActionsSource.ResourcePath);
            Assert.IsNotNull(asset);
            InputActionMap map = asset.FindActionMap(InputActionsSource.GameplayMapName, false);
            Assert.IsNotNull(map);

            foreach (string name in new[] { InputActionsSource.MoveAction, InputActionsSource.JumpAction, InputActionsSource.DodgeAction, InputActionsSource.SprintAction, InputActionsSource.PauseAction })
            {
                InputAction action = map.FindAction(name, false);
                Assert.IsNotNull(action, name);
                bool keyboard = false;
                bool gamepad = false;
                foreach (InputBinding binding in action.bindings)
                {
                    keyboard |= binding.path != null && binding.path.StartsWith("<Keyboard>");
                    gamepad |= binding.path != null && binding.path.StartsWith("<Gamepad>");
                }

                Assert.IsTrue(keyboard, name + " has no keyboard binding.");
                Assert.IsTrue(gamepad, name + " has no gamepad binding.");
            }
        }

        [Test]
        public void DefaultMovementConfig_LoadsWithSaneValues()
        {
            PlayerMovementConfig config = Resources.Load<PlayerMovementConfig>(PlayerMovementConfig.DefaultResourcePath);
            Assert.IsNotNull(config, "Missing Resources/" + PlayerMovementConfig.DefaultResourcePath);
            Assert.IsNotNull(config.Tuning);
            Assert.Greater(config.Tuning.runSpeed, 0f);
            Assert.Greater(config.Tuning.jumpHeight, 0f);
            Assert.Greater(config.Tuning.dodgeDistance, 0f);
            Assert.GreaterOrEqual(config.Tuning.sprintSpeed, config.Tuning.runSpeed);
        }

        [Test]
        public void TemplateMaterials_AreAvailable()
        {
            Assert.IsNotNull(Resources.Load<Material>(NeonMaterialLibrary.SurfaceTemplatePath));
            Assert.IsNotNull(Resources.Load<Material>(NeonMaterialLibrary.EmissiveTemplatePath));
            Assert.IsNotNull(Resources.Load<Material>(NeonMaterialLibrary.NeonTemplatePath));
        }
    }
}
