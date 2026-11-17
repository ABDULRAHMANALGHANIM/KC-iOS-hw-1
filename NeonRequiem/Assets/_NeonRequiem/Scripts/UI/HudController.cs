using System;
using NeonRequiem.Controls;
using NeonRequiem.Core;
using NeonRequiem.Optimization;
using NeonRequiem.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NeonRequiem.UI
{
    /// <summary>
    /// Owns the in-game interface: touch controls, performance overlay, input diagnostics and
    /// the pause menu. It reacts to game state (pause), settings and the active input scheme;
    /// it holds no gameplay state of its own.
    /// </summary>
    [DefaultExecutionOrder(ExecutionOrder.Hud)]
    public sealed class HudController : MonoBehaviour
    {
        private const int HudSortingOrder = 100;

        private GameManager m_GameManager;
        private SettingsManager m_Settings;
        private PlayerInputRouter m_Input;
        private PerformanceManager m_Performance;

        private RectTransform m_SafeArea;
        private TouchControlsView m_TouchControls;
        private DebugOverlayView m_Overlay;
        private InputDiagnosticsView m_Diagnostics;
        private PauseMenuView m_PauseMenu;
        private PlayerController m_Player;
        private bool m_DiagnosticsRequested;

        /// <summary>The player chose "return to spawn" in the pause menu.</summary>
        public event Action RespawnRequested;

        public TouchControlsView TouchControls => m_TouchControls;

        public PauseMenuView PauseMenu => m_PauseMenu;

        /// <summary>Builds the HUD under <paramref name="parent"/>.</summary>
        public static HudController Create(
            Transform parent,
            GameManager gameManager,
            SettingsManager settings,
            PlayerInputRouter input,
            PerformanceManager performance)
        {
            Canvas canvas = UIFactory.CreateCanvas("HUD", parent, HudSortingOrder);
            UIFactory.EnsureEventSystem(parent);

            var hud = canvas.gameObject.AddComponent<HudController>();
            hud.m_GameManager = gameManager != null ? gameManager : throw new ArgumentNullException(nameof(gameManager));
            hud.m_Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            hud.m_Input = input != null ? input : throw new ArgumentNullException(nameof(input));
            hud.m_Performance = performance;
            hud.Build(canvas.transform);
            return hud;
        }

        /// <summary>Shows the gameplay HUD for <paramref name="player"/>.</summary>
        public void BindPlayer(PlayerController player)
        {
            m_Player = player;
            m_Overlay.SetPlayer(player);
            RefreshVisibility();
        }

        /// <summary>Hides the gameplay HUD when <paramref name="player"/> is the bound player.</summary>
        public void UnbindPlayer(PlayerController player)
        {
            if (m_Player != player)
            {
                return;
            }

            m_Player = null;
            m_Overlay.SetPlayer(null);
            RefreshVisibility();
        }

        /// <summary>Turns the input diagnostics panel on or off (controls test scene).</summary>
        public void SetInputDiagnosticsVisible(bool visible)
        {
            m_DiagnosticsRequested = visible;
            RefreshVisibility();
        }

        private void Build(Transform canvas)
        {
            m_SafeArea = UIFactory.CreateRect("SafeArea", canvas);
            UIFactory.Stretch(m_SafeArea);
            m_SafeArea.gameObject.AddComponent<SafeAreaFitter>();

            m_TouchControls = TouchControlsView.Create(m_SafeArea);
            m_Overlay = DebugOverlayView.Create(m_SafeArea, m_Performance, m_Input);
            m_Diagnostics = InputDiagnosticsView.Create(m_SafeArea, m_Input);
            m_PauseMenu = PauseMenuView.Create(canvas, m_Settings);

            m_PauseMenu.ResumeRequested += OnResumeRequested;
            m_PauseMenu.RespawnRequested += OnRespawnRequested;
            m_GameManager.PauseChanged += OnPauseChanged;
            m_Input.SchemeChanged += OnSchemeChanged;
            m_Settings.Changed += OnSettingsChanged;

            OnSettingsChanged(m_Settings.Current);
        }

        private void OnDestroy()
        {
            if (m_PauseMenu != null)
            {
                m_PauseMenu.ResumeRequested -= OnResumeRequested;
                m_PauseMenu.RespawnRequested -= OnRespawnRequested;
            }

            if (m_GameManager != null)
            {
                m_GameManager.PauseChanged -= OnPauseChanged;
            }

            if (m_Input != null)
            {
                m_Input.SchemeChanged -= OnSchemeChanged;
            }

            if (m_Settings != null)
            {
                m_Settings.Changed -= OnSettingsChanged;
            }
        }

        private void Update()
        {
            if (m_Input.Current.PausePressed)
            {
                m_GameManager.TogglePause();
            }

            // Touch controls hide while a gamepad is in use; any touch (or a click in the editor) brings them back.
            if (m_Input.ActiveScheme == InputScheme.Gamepad && PointerPressedThisFrame())
            {
                m_Input.NotifyScheme(InputScheme.Touch);
            }
        }

        private static bool PointerPressedThisFrame()
        {
            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen != null && touchscreen.primaryTouch.press.wasPressedThisFrame)
            {
                return true;
            }

            Mouse mouse = Mouse.current;
            return mouse != null && mouse.leftButton.wasPressedThisFrame;
        }

        private static bool PlatformUsesTouchControls()
        {
            // The editor shows them too so the layout can be exercised with a mouse.
            return Application.isMobilePlatform || Application.isEditor || Touchscreen.current != null;
        }

        private void OnResumeRequested()
        {
            m_GameManager.Resume();
        }

        private void OnRespawnRequested()
        {
            RespawnRequested?.Invoke();
            m_GameManager.Resume();
        }

        private void OnPauseChanged(bool paused)
        {
            m_Input.GameplayInputEnabled = !paused;
            m_PauseMenu.SetVisible(paused);
            RefreshVisibility();
        }

        private void OnSchemeChanged(InputScheme scheme)
        {
            RefreshVisibility();
        }

        private void OnSettingsChanged(GameSettings settings)
        {
            m_TouchControls.ApplySettings(settings);
            RefreshVisibility();
        }

        private void RefreshVisibility()
        {
            bool inGameplay = m_Player != null && !m_GameManager.IsPaused;
            bool touch = inGameplay && PlatformUsesTouchControls() && m_Input.ActiveScheme != InputScheme.Gamepad;
            m_TouchControls.SetVisible(touch);
            m_Overlay.gameObject.SetActive(m_Settings.Current.showPerformanceOverlay);
            m_Diagnostics.gameObject.SetActive(m_DiagnosticsRequested && inGameplay);
        }
    }
}
