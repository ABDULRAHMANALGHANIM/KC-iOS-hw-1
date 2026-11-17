using System;
using NeonRequiem.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NeonRequiem.Controls
{
    /// <summary>
    /// Keyboard, mouse and gamepad input read from the project's Input Actions asset
    /// (Resources/NeonRequiem/NeonRequiemControls.inputactions). Works on a private copy of the
    /// asset so enabling actions or applying binding overrides never mutates the shared asset.
    /// </summary>
    public sealed class InputActionsSource : IPlayerInputSource, IDisposable
    {
        public const string ResourcePath = "NeonRequiem/NeonRequiemControls";
        public const string GameplayMapName = "Gameplay";
        public const string MoveAction = "Move";
        public const string LookAction = "Look";
        public const string PointerLookAction = "PointerLook";
        public const string PointerLookHoldAction = "PointerLookHold";
        public const string JumpAction = "Jump";
        public const string DodgeAction = "Dodge";
        public const string SprintAction = "Sprint";
        public const string PauseAction = "Pause";

        private const string LogCategory = "Input";

        private readonly InputActionAsset m_Asset;
        private readonly bool m_OwnsAsset;
        private readonly InputActionMap m_Gameplay;
        private readonly InputAction m_Move;
        private readonly InputAction m_Look;
        private readonly InputAction m_PointerLook;
        private readonly InputAction m_PointerLookHold;
        private readonly InputAction m_Jump;
        private readonly InputAction m_Dodge;
        private readonly InputAction m_Sprint;
        private readonly InputAction m_Pause;
        private bool m_Disposed;

        private InputActionsSource(InputActionAsset asset, bool ownsAsset)
        {
            m_Asset = asset;
            m_OwnsAsset = ownsAsset;
            m_Gameplay = asset.FindActionMap(GameplayMapName, false);
            if (m_Gameplay == null)
            {
                return;
            }

            m_Move = m_Gameplay.FindAction(MoveAction, false);
            m_Look = m_Gameplay.FindAction(LookAction, false);
            m_PointerLook = m_Gameplay.FindAction(PointerLookAction, false);
            m_PointerLookHold = m_Gameplay.FindAction(PointerLookHoldAction, false);
            m_Jump = m_Gameplay.FindAction(JumpAction, false);
            m_Dodge = m_Gameplay.FindAction(DodgeAction, false);
            m_Sprint = m_Gameplay.FindAction(SprintAction, false);
            m_Pause = m_Gameplay.FindAction(PauseAction, false);
        }

        /// <summary>True when the map and every action this source reads were found in the asset.</summary>
        public bool IsValid =>
            m_Gameplay != null && m_Move != null && m_Look != null && m_PointerLook != null &&
            m_PointerLookHold != null && m_Jump != null && m_Dodge != null && m_Sprint != null && m_Pause != null;

        /// <summary>
        /// Loads the controls asset from Resources. Returns null (after logging the reason) when the
        /// asset is missing or incomplete; touch input keeps working in that case.
        /// </summary>
        public static InputActionsSource CreateFromResources()
        {
            InputActionAsset template = Resources.Load<InputActionAsset>(ResourcePath);
            if (template == null)
            {
                NRLog.Error(
                    LogCategory,
                    "Input Actions asset not found at Resources/" + ResourcePath +
                    ". Keyboard, mouse and gamepad input are unavailable; touch controls still work.");
                return null;
            }

            return Create(UnityEngine.Object.Instantiate(template), true);
        }

        /// <summary>Wraps an existing asset. The caller keeps ownership unless <paramref name="takeOwnership"/> is set.</summary>
        public static InputActionsSource Create(InputActionAsset asset, bool takeOwnership)
        {
            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            var source = new InputActionsSource(asset, takeOwnership);
            if (!source.IsValid)
            {
                NRLog.Error(
                    LogCategory,
                    "Input Actions asset '" + asset.name + "' is missing the '" + GameplayMapName +
                    "' map or one of its required actions.");
                source.Dispose();
                return null;
            }

            return source;
        }

        public void Enable()
        {
            if (!m_Disposed && IsValid)
            {
                m_Gameplay.Enable();
            }
        }

        public void Disable()
        {
            if (!m_Disposed && m_Gameplay != null)
            {
                m_Gameplay.Disable();
            }
        }

        public void Sample(ref PlayerInputSample sample)
        {
            if (m_Disposed || !IsValid || !m_Gameplay.enabled)
            {
                return;
            }

            bool gamepad = false;
            bool active = false;

            Vector2 move = m_Move.ReadValue<Vector2>();
            if (move.sqrMagnitude > 0.0001f)
            {
                sample.MergeMove(Vector2.ClampMagnitude(move, 1f));
                Note(m_Move, ref gamepad, ref active);
            }

            Vector2 stick = m_Look.ReadValue<Vector2>();
            if (stick.sqrMagnitude > 0.0001f)
            {
                sample.LookStick += stick;
                Note(m_Look, ref gamepad, ref active);
            }

            if (m_PointerLookHold.IsPressed())
            {
                Vector2 pointer = m_PointerLook.ReadValue<Vector2>();
                if (pointer.sqrMagnitude > 0f)
                {
                    sample.LookMousePixels += pointer;
                    active = true;
                }
            }

            if (m_Jump.WasPressedThisFrame())
            {
                sample.JumpPressed = true;
                Note(m_Jump, ref gamepad, ref active);
            }

            if (m_Jump.IsPressed())
            {
                sample.JumpHeld = true;
            }

            if (m_Dodge.WasPressedThisFrame())
            {
                sample.DodgePressed = true;
                Note(m_Dodge, ref gamepad, ref active);
            }

            if (m_Sprint.IsPressed())
            {
                sample.SprintHeld = true;
                Note(m_Sprint, ref gamepad, ref active);
            }

            if (m_Pause.WasPressedThisFrame())
            {
                sample.PausePressed = true;
                Note(m_Pause, ref gamepad, ref active);
            }

            if (active)
            {
                sample.ActiveScheme = gamepad ? InputScheme.Gamepad : InputScheme.KeyboardMouse;
            }
        }

        public void Dispose()
        {
            if (m_Disposed)
            {
                return;
            }

            m_Disposed = true;
            if (m_Gameplay != null)
            {
                m_Gameplay.Disable();
            }

            if (m_OwnsAsset)
            {
                ObjectUtility.SafeDestroy(m_Asset);
            }
        }

        private static void Note(InputAction action, ref bool gamepad, ref bool active)
        {
            active = true;
            InputControl control = action.activeControl;
            if (control != null && control.device is Gamepad)
            {
                gamepad = true;
            }
        }
    }
}
