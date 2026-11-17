using System;
using System.Collections.Generic;
using NeonRequiem.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NeonRequiem.UI
{
    /// <summary>
    /// Pause screen with the Phase 1 settings: controls, camera, accessibility and graphics.
    /// Every row edits <see cref="SettingsManager"/> directly, so changes apply live behind the
    /// menu and persist immediately. Rows are sized for thumbs and scroll when they do not fit.
    /// </summary>
    public sealed class PauseMenuView : MonoBehaviour
    {
        private const float PanelWidth = 1240f;
        private const float PanelHeight = 1000f;
        private const float HeaderHeight = 104f;
        private const float FooterHeight = 136f;
        private const float RowHeight = 112f;
        private const float ControlHeight = 92f;
        private const float StepButtonWidth = 108f;
        private const float ValueWidth = 170f;
        private const float ToggleWidth = 386f;
        private const float Padding = 36f;

        private readonly List<Action> m_Refreshers = new List<Action>();
        private SettingsManager m_Settings;
        private RectTransform m_Content;
        private Button m_ResumeButton;
        private int m_RowCount;

        public event Action ResumeRequested;

        public event Action RespawnRequested;

        public bool IsVisible => gameObject.activeSelf;

        public static PauseMenuView Create(Transform parent, SettingsManager settings)
        {
            // Full-screen scrim: dims the game and swallows touches aimed at the world.
            Image scrim = UIFactory.CreateImage("PauseMenu", parent, null, UITheme.Scrim, true);
            UIFactory.Stretch(scrim.rectTransform);

            var view = scrim.gameObject.AddComponent<PauseMenuView>();
            view.m_Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            view.Build();
            view.gameObject.SetActive(false);
            return view;
        }

        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf == visible)
            {
                return;
            }

            gameObject.SetActive(visible);
            if (visible)
            {
                RefreshAll();

                // Give controller and keyboard users a starting point for navigation.
                EventSystem eventSystem = EventSystem.current;
                if (eventSystem != null && m_ResumeButton != null)
                {
                    eventSystem.SetSelectedGameObject(m_ResumeButton.gameObject);
                }
            }
        }

        private void OnDestroy()
        {
            if (m_Settings != null)
            {
                m_Settings.Changed -= OnSettingsChanged;
            }
        }

        private void Build()
        {
            Image panel = UIFactory.CreateImage("Panel", transform, ProceduralSprites.RoundedRect, UITheme.PanelBackground, true);
            UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PanelWidth, PanelHeight));

            Text title = UIFactory.CreateText("Title", panel.transform, Loc.Get(LocKeys.PauseTitle), 54, TextAnchor.MiddleCenter, UITheme.Cyan);
            title.fontStyle = FontStyle.Bold;
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(PanelWidth, HeaderHeight));

            BuildScrollArea(panel.rectTransform);
            BuildRows();
            BuildFooter(panel.rectTransform);

            m_Settings.Changed += OnSettingsChanged;
        }

        private void BuildScrollArea(RectTransform panel)
        {
            RectTransform viewport = UIFactory.CreateRect("Viewport", panel);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(Padding, FooterHeight);
            viewport.offsetMax = new Vector2(-Padding, -HeaderHeight);
            viewport.gameObject.AddComponent<RectMask2D>();
            // Invisible hit area so a drag that starts between rows still scrolls the list.
            viewport.gameObject.AddComponent<RaycastZone>();

            m_Content = UIFactory.CreateRect("Content", viewport);
            m_Content.anchorMin = new Vector2(0f, 1f);
            m_Content.anchorMax = new Vector2(1f, 1f);
            m_Content.pivot = new Vector2(0.5f, 1f);
            m_Content.anchoredPosition = Vector2.zero;

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = m_Content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 60f;
        }

        private void BuildRows()
        {
            AddStepper(
                LocKeys.SettingLookSensitivity, 0.1f,
                s => s.lookSensitivity, (s, v) => s.lookSensitivity = v,
                v => v.ToString("F1"));
            AddToggle(LocKeys.SettingInvertLookY, s => s.invertLookY, (s, v) => s.invertLookY = v);
            AddToggle(LocKeys.SettingCameraAssist, s => s.cameraAssist, (s, v) => s.cameraAssist = v);
            AddToggle(LocKeys.SettingReduceCameraMotion, s => s.reduceCameraMotion, (s, v) => s.reduceCameraMotion = v);
            AddToggle(LocKeys.SettingLeftHanded, s => s.leftHandedLayout, (s, v) => s.leftHandedLayout = v);
            AddToggle(LocKeys.SettingFloatingJoystick, s => s.floatingJoystick, (s, v) => s.floatingJoystick = v);
            AddStepper(
                LocKeys.SettingControlSize, 0.1f,
                s => s.touchControlScale, (s, v) => s.touchControlScale = v,
                v => Mathf.RoundToInt(v * 100f) + "%");
            AddStepper(
                LocKeys.SettingControlOpacity, 0.05f,
                s => s.touchControlOpacity, (s, v) => s.touchControlOpacity = v,
                v => Mathf.RoundToInt(v * 100f) + "%");
            AddCycle(LocKeys.SettingGraphicsPreset, DescribePreset, AdvancePreset);
            AddCycle(LocKeys.SettingFrameRate, s => ((int)s.frameRateTarget).ToString(), AdvanceFrameRate);
            AddToggle(LocKeys.SettingAdaptiveResolution, s => s.adaptiveResolution, (s, v) => s.adaptiveResolution = v);
            AddToggle(LocKeys.SettingPerformanceOverlay, s => s.showPerformanceOverlay, (s, v) => s.showPerformanceOverlay = v);

            m_Content.sizeDelta = new Vector2(0f, m_RowCount * RowHeight);
        }

        private void BuildFooter(RectTransform panel)
        {
            float width = (PanelWidth - Padding * 4f) / 3f;
            m_ResumeButton = FooterButton(panel, LocKeys.PauseResume, 0, width, () => ResumeRequested?.Invoke());
            FooterButton(panel, LocKeys.PauseRespawn, 1, width, () => RespawnRequested?.Invoke());
            FooterButton(panel, LocKeys.PauseResetSettings, 2, width, () => m_Settings.ResetToDefaults());
        }

        private static Button FooterButton(RectTransform panel, string labelKey, int index, float width, Action onClick)
        {
            Button button = UIFactory.CreateButton(labelKey, panel, Loc.Get(labelKey), 32, () => onClick(), out Text _);
            float x = Padding + index * (width + Padding);
            UIFactory.Place((RectTransform)button.transform, Vector2.zero, Vector2.zero, new Vector2(x, 22f), new Vector2(width, ControlHeight));
            return button;
        }

        private static string DescribePreset(GameSettings settings)
        {
            switch (settings.graphicsPreset)
            {
                case GraphicsPreset.Performance:
                    return Loc.Get(LocKeys.GraphicsPerformance);
                case GraphicsPreset.Quality:
                    return Loc.Get(LocKeys.GraphicsQuality);
                default:
                    return Loc.Get(LocKeys.GraphicsBalanced);
            }
        }

        private static void AdvancePreset(GameSettings settings)
        {
            settings.graphicsPreset = settings.graphicsPreset == GraphicsPreset.Quality
                ? GraphicsPreset.Performance
                : settings.graphicsPreset + 1;
        }

        private static void AdvanceFrameRate(GameSettings settings)
        {
            settings.frameRateTarget = settings.frameRateTarget == FrameRateTarget.Fps60
                ? FrameRateTarget.Fps30
                : FrameRateTarget.Fps60;
        }

        private RectTransform AddRow(string labelKey)
        {
            RectTransform row = UIFactory.CreateRect(labelKey, m_Content);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.anchoredPosition = new Vector2(0f, -m_RowCount * RowHeight);
            row.sizeDelta = new Vector2(0f, RowHeight);
            m_RowCount++;

            Text label = UIFactory.CreateText("Label", row, Loc.Get(labelKey), 34, TextAnchor.MiddleLeft, UITheme.TextPrimary);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(8f, 0f);
            label.rectTransform.offsetMax = new Vector2(-(ToggleWidth + 16f), 0f);
            return row;
        }

        private void AddToggle(string labelKey, Func<GameSettings, bool> get, Action<GameSettings, bool> set)
        {
            RectTransform row = AddRow(labelKey);
            Button button = UIFactory.CreateButton(
                "Toggle", row, string.Empty, 32,
                () => m_Settings.Modify(s => set(s, !get(s))),
                out Text value);
            UIFactory.Place((RectTransform)button.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(ToggleWidth, ControlHeight));

            m_Refreshers.Add(() =>
            {
                bool on = get(m_Settings.Current);
                value.text = Loc.Get(on ? LocKeys.ValueOn : LocKeys.ValueOff);
                value.color = on ? UITheme.Cyan : UITheme.TextMuted;
            });
        }

        private void AddCycle(string labelKey, Func<GameSettings, string> describe, Action<GameSettings> advance)
        {
            RectTransform row = AddRow(labelKey);
            Button button = UIFactory.CreateButton("Cycle", row, string.Empty, 32, () => m_Settings.Modify(advance), out Text value);
            UIFactory.Place((RectTransform)button.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(ToggleWidth, ControlHeight));
            m_Refreshers.Add(() => value.text = describe(m_Settings.Current));
        }

        private void AddStepper(
            string labelKey,
            float step,
            Func<GameSettings, float> get,
            Action<GameSettings, float> set,
            Func<float, string> format)
        {
            RectTransform row = AddRow(labelKey);

            Button plus = UIFactory.CreateButton("Increase", row, "+", 44, () => Step(get, set, step), out Text _);
            UIFactory.Place((RectTransform)plus.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(StepButtonWidth, ControlHeight));

            Text value = UIFactory.CreateText("Value", row, string.Empty, 34, TextAnchor.MiddleCenter, UITheme.Cyan);
            UIFactory.Place(value.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-StepButtonWidth, 0f), new Vector2(ValueWidth, ControlHeight));

            Button minus = UIFactory.CreateButton("Decrease", row, "-", 44, () => Step(get, set, -step), out Text _);
            UIFactory.Place((RectTransform)minus.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-(StepButtonWidth + ValueWidth), 0f), new Vector2(StepButtonWidth, ControlHeight));

            m_Refreshers.Add(() => value.text = format(get(m_Settings.Current)));
        }

        private void Step(Func<GameSettings, float> get, Action<GameSettings, float> set, float delta)
        {
            // Snap to the step grid so repeated presses never accumulate floating point drift.
            m_Settings.Modify(s =>
            {
                float stepSize = Mathf.Abs(delta);
                float next = Mathf.Round((get(s) + delta) / stepSize) * stepSize;
                set(s, next);
            });
        }

        private void OnSettingsChanged(GameSettings settings)
        {
            if (gameObject.activeInHierarchy)
            {
                RefreshAll();
            }
        }

        private void RefreshAll()
        {
            for (int i = 0; i < m_Refreshers.Count; i++)
            {
                m_Refreshers[i]();
            }
        }
    }
}
