using System;
using NeonRequiem.Cameras;
using NeonRequiem.Controls;
using NeonRequiem.Core;
using NeonRequiem.World;
using UnityEngine;

namespace NeonRequiem.Player
{
    /// <summary>
    /// Assembles the player character from code. This is the single place that knows the player
    /// object's structure, so swapping in an authored prefab later means replacing this factory,
    /// not touching gameplay scripts.
    /// </summary>
    public static class PlayerFactory
    {
        public const string PlayerTag = "Player";
        public const string PlayerObjectName = "Player_Kael";

        /// <summary>Height of the camera's follow point as a fraction of the capsule height.</summary>
        private const float CameraTargetHeightFraction = 0.82f;

        public static PlayerController Create(
            MovementTuning tuning,
            NeonMaterialLibrary materials,
            PlayerInputRouter input,
            ICameraYawSource cameraYaw)
        {
            if (tuning == null)
            {
                throw new ArgumentNullException(nameof(tuning));
            }

            if (materials == null)
            {
                throw new ArgumentNullException(nameof(materials));
            }

            tuning.Sanitize();

            var root = new GameObject(PlayerObjectName);
            root.layer = GameLayers.Player;
            root.tag = PlayerTag;

            root.AddComponent<CharacterController>();
            root.AddComponent<PlayerMotor>();
            var controller = root.AddComponent<PlayerController>();

            var visualObject = new GameObject("Visual");
            visualObject.layer = GameLayers.Player;
            visualObject.transform.SetParent(root.transform, false);
            var visual = visualObject.AddComponent<PlayerVisualRig>();
            visual.Build(materials, tuning);

            var cameraTarget = new GameObject("CameraTarget");
            cameraTarget.layer = GameLayers.Player;
            cameraTarget.transform.SetParent(root.transform, false);
            cameraTarget.transform.localPosition = new Vector3(0f, tuning.height * CameraTargetHeightFraction, 0f);

            controller.Initialize(tuning, input, cameraYaw, visual, cameraTarget.transform);
            return controller;
        }
    }
}
