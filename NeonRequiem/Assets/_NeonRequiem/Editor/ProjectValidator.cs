using System.Collections.Generic;
using System.IO;
using NeonRequiem.Controls;
using NeonRequiem.Core;
using NeonRequiem.Optimization;
using NeonRequiem.Player;
using NeonRequiem.World;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NeonRequiem.EditorTools
{
    /// <summary>
    /// Checks that the project is configured the way the runtime code assumes: render pipeline,
    /// quality levels, layers, build scenes, input backend and the Resources assets that are
    /// loaded by path. Run from the menu, and by the EditMode test suite so a broken
    /// configuration fails CI instead of failing on a device.
    /// </summary>
    public static class ProjectValidator
    {
        public const string MenuPath = "Neon Requiem/Validate Project Setup";

        private const string LogCategory = "Validator";

        /// <summary>One finding. Errors break the game; warnings are deviations worth knowing about.</summary>
        public readonly struct Issue
        {
            public Issue(bool isError, string message)
            {
                IsError = isError;
                Message = message;
            }

            public bool IsError { get; }

            public string Message { get; }

            public override string ToString() => (IsError ? "ERROR: " : "WARNING: ") + Message;
        }

        /// <summary>Scenes that must be in the Build Settings, in order. Index 0 is the entry point.</summary>
        public static readonly string[] RequiredScenes =
        {
            SceneIds.Bootstrap,
            SceneIds.MovementTest,
            SceneIds.MobileControlsTest,
        };

        [MenuItem(MenuPath, priority = 0)]
        public static void RunFromMenu()
        {
            List<Issue> issues = Validate();
            int errors = 0;
            foreach (Issue issue in issues)
            {
                if (issue.IsError)
                {
                    errors++;
                    NRLog.Error(LogCategory, issue.Message);
                }
                else
                {
                    NRLog.Warning(LogCategory, issue.Message);
                }
            }

            string summary = issues.Count == 0
                ? "Project setup is valid."
                : errors + " error(s), " + (issues.Count - errors) + " warning(s). See the Console for details.";
            NRLog.Info(LogCategory, summary);
            EditorUtility.DisplayDialog("NEON REQUIEM - Project Setup", summary, "OK");
        }

        /// <summary>Runs every check and returns the findings (empty when everything is correct).</summary>
        public static List<Issue> Validate()
        {
            var issues = new List<Issue>();
            CheckEditorVersion(issues);
            CheckRenderPipeline(issues);
            CheckQualityLevels(issues);
            CheckLayers(issues);
            CheckBuildScenes(issues);
            CheckInputBackend(issues);
            CheckResources(issues);
            CheckPlayerSettings(issues);
            return issues;
        }

        private static void CheckEditorVersion(List<Issue> issues)
        {
            string version = Application.unityVersion;
            if (!version.StartsWith("6000."))
            {
                issues.Add(new Issue(true, "Unity 6 is required; this editor is " + version + "."));
            }
            else if (!version.StartsWith("6000.3."))
            {
                issues.Add(new Issue(false, "The project targets Unity 6.3 LTS (6000.3.x); this editor is " + version + "."));
            }
        }

        private static void CheckRenderPipeline(List<Issue> issues)
        {
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset))
            {
                issues.Add(new Issue(true, "The active render pipeline is not a URP asset. Assign one in Project Settings > Graphics and > Quality."));
            }
        }

        private static void CheckQualityLevels(List<Issue> issues)
        {
            foreach (GraphicsPreset preset in new[] { GraphicsPreset.Performance, GraphicsPreset.Balanced, GraphicsPreset.Quality })
            {
                int level = PerformanceManager.FindQualityLevel(preset);
                if (level < 0)
                {
                    issues.Add(new Issue(true, "No quality level named '" + preset + "' (Project Settings > Quality). The graphics preset cannot be applied."));
                    continue;
                }

                if (!(QualitySettings.GetRenderPipelineAssetAt(level) is UniversalRenderPipelineAsset))
                {
                    issues.Add(new Issue(true, "Quality level '" + preset + "' has no URP asset assigned."));
                }
            }
        }

        private static void CheckLayers(List<Issue> issues)
        {
            string actual = LayerMask.LayerToName(GameLayers.Player);
            if (actual != GameLayers.PlayerName)
            {
                issues.Add(new Issue(true, "Layer " + GameLayers.Player + " must be named '" + GameLayers.PlayerName + "' (found '" + actual + "'). See Tags and Layers."));
            }
        }

        private static void CheckBuildScenes(List<Issue> issues)
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            var enabledNames = new List<string>();
            foreach (EditorBuildSettingsScene scene in scenes)
            {
                if (!scene.enabled)
                {
                    continue;
                }

                if (!File.Exists(scene.path))
                {
                    issues.Add(new Issue(true, "Build Settings lists a missing scene: " + scene.path));
                    continue;
                }

                enabledNames.Add(Path.GetFileNameWithoutExtension(scene.path));
            }

            foreach (string required in RequiredScenes)
            {
                if (!enabledNames.Contains(required))
                {
                    issues.Add(new Issue(true, "Scene '" + required + "' is not enabled in the Build Settings scene list."));
                }
            }

            if (enabledNames.Count > 0 && enabledNames[0] != SceneIds.Bootstrap)
            {
                issues.Add(new Issue(true, "The first scene in the Build Settings must be '" + SceneIds.Bootstrap + "' (found '" + enabledNames[0] + "')."));
            }
        }

        private static void CheckInputBackend(List<Issue> issues)
        {
#if !ENABLE_INPUT_SYSTEM
            issues.Add(new Issue(true, "The Input System backend is disabled. Set Project Settings > Player > Active Input Handling to 'Input System Package (New)'."));
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            issues.Add(new Issue(false, "The legacy Input Manager is also enabled. The game does not use it; 'Input System Package (New)' alone is recommended."));
#endif
        }

        private static void CheckResources(List<Issue> issues)
        {
            InputActionAsset actions = Resources.Load<InputActionAsset>(InputActionsSource.ResourcePath);
            if (actions == null)
            {
                issues.Add(new Issue(true, "Input Actions asset missing at Resources/" + InputActionsSource.ResourcePath + "."));
            }
            else
            {
                InputActionMap map = actions.FindActionMap(InputActionsSource.GameplayMapName, false);
                if (map == null)
                {
                    issues.Add(new Issue(true, "Input Actions asset has no '" + InputActionsSource.GameplayMapName + "' map."));
                }
                else
                {
                    string[] required =
                    {
                        InputActionsSource.MoveAction, InputActionsSource.LookAction, InputActionsSource.PointerLookAction,
                        InputActionsSource.PointerLookHoldAction, InputActionsSource.JumpAction, InputActionsSource.DodgeAction,
                        InputActionsSource.SprintAction, InputActionsSource.PauseAction,
                    };
                    foreach (string name in required)
                    {
                        if (map.FindAction(name, false) == null)
                        {
                            issues.Add(new Issue(true, "Input action '" + name + "' is missing from the '" + InputActionsSource.GameplayMapName + "' map."));
                        }
                    }
                }
            }

            if (Resources.Load<PlayerMovementConfig>(PlayerMovementConfig.DefaultResourcePath) == null)
            {
                issues.Add(new Issue(true, "PlayerMovementConfig missing at Resources/" + PlayerMovementConfig.DefaultResourcePath + "."));
            }

            CheckMaterial(issues, NeonMaterialLibrary.SurfaceTemplatePath, "Universal Render Pipeline/Lit");
            CheckMaterial(issues, NeonMaterialLibrary.EmissiveTemplatePath, "Universal Render Pipeline/Lit");
            CheckMaterial(issues, NeonMaterialLibrary.NeonTemplatePath, "Universal Render Pipeline/Unlit");
        }

        private static void CheckMaterial(List<Issue> issues, string resourcePath, string expectedShader)
        {
            Material material = Resources.Load<Material>(resourcePath);
            if (material == null)
            {
                issues.Add(new Issue(true, "Template material missing at Resources/" + resourcePath + "."));
                return;
            }

            Shader shader = material.shader;
            if (shader == null || shader.name != expectedShader)
            {
                issues.Add(new Issue(true, "Material '" + resourcePath + "' should use shader '" + expectedShader + "' (found '" + (shader != null ? shader.name : "none") + "')."));
            }
            else if (ShaderUtil.ShaderHasError(shader))
            {
                issues.Add(new Issue(true, "Shader '" + shader.name + "' has compile errors."));
            }
        }

        private static void CheckPlayerSettings(List<Issue> issues)
        {
            if (PlayerSettings.colorSpace != ColorSpace.Linear)
            {
                issues.Add(new Issue(false, "Color space is not Linear; lighting and bloom were tuned for Linear."));
            }

            if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) != ScriptingImplementation.IL2CPP)
            {
                issues.Add(new Issue(false, "Android scripting backend is not IL2CPP. Google Play requires 64-bit builds, which need IL2CPP."));
            }

            if ((PlayerSettings.Android.targetArchitectures & AndroidArchitecture.ARM64) == 0)
            {
                issues.Add(new Issue(false, "Android target architectures do not include ARM64."));
            }

            if (PlayerSettings.allowedAutorotateToPortrait || PlayerSettings.allowedAutorotateToPortraitUpsideDown)
            {
                issues.Add(new Issue(false, "Portrait orientations are enabled; the game's interface is designed for landscape only."));
            }
        }
    }
}
