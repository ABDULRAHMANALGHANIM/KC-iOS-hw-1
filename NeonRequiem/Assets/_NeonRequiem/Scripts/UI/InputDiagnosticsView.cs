using System.Text;
using NeonRequiem.Controls;
using NeonRequiem.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace NeonRequiem.UI
{
    /// <summary>
    /// Live readout of the input pipeline for the mobile controls test scene: the merged move
    /// vector (plotted and numeric), look rate, button states, active scheme, touch count and the
    /// screen metrics that drive touch sensitivity. Used to verify ergonomics on real devices.
    /// </summary>
    public sealed class InputDiagnosticsView : MonoBehaviour
    {
        private const float PlotRadius = 70f;
        private const float FlashSeconds = 0.35f;

        private readonly StringBuilder m_Builder = new StringBuilder(384);
        private PlayerInputRouter m_Input;
        private Text m_Text;
        private RectTransform m_PlotDot;
        private float m_JumpFlash;
        private float m_DodgeFlash;
        private Vector2 m_LookRate;

        public static InputDiagnosticsView Create(Transform parent, PlayerInputRouter input)
        {
            Image background = UIFactory.CreateImage("InputDiagnostics", parent, ProceduralSprites.RoundedRect, new Color(0f, 0f, 0f, 0.55f), false);
            UIFactory.Place(background.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(760f, 330f));

            var view = background.gameObject.AddComponent<InputDiagnosticsView>();
            view.m_Input = input;

            Text title = UIFactory.CreateText("Title", background.transform, Loc.Get(LocKeys.DiagnosticsTitle), 26, TextAnchor.UpperLeft, UITheme.Cyan);
            UIFactory.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -12f), new Vector2(500f, 36f));

            view.m_Text = UIFactory.CreateText("Readout", background.transform, string.Empty, 25, TextAnchor.UpperLeft, UITheme.TextPrimary);
            UIFactory.Place(view.m_Text.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -52f), new Vector2(540f, 270f));

            Image ring = UIFactory.CreateImage("MovePlot", background.transform, ProceduralSprites.Ring, UITheme.TextMuted, false);
            UIFactory.Place(ring.rectTransform, new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-110f, 0f), Vector2.one * (PlotRadius * 2f + 24f));
            Image dot = UIFactory.CreateImage("MoveDot", ring.transform, ProceduralSprites.Disc, UITheme.Cyan, false);
            UIFactory.Place(dot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * 26f);
            view.m_PlotDot = dot.rectTransform;
            return view;
        }

        private void Update()
        {
            if (m_Input == null || m_Text == null)
            {
                return;
            }

            float dt = Time.unscaledDeltaTime;
            PlayerInputFrame frame = m_Input.Current;

            m_JumpFlash = frame.JumpPressed ? FlashSeconds : Mathf.Max(0f, m_JumpFlash - dt);
            m_DodgeFlash = frame.DodgePressed ? FlashSeconds : Mathf.Max(0f, m_DodgeFlash - dt);
            if (dt > 0f)
            {
                // Smoothed so the number is readable; the raw per-frame value flickers.
                m_LookRate = Vector2.Lerp(m_LookRate, frame.LookDegrees / dt, NRMath.DampFactor(12f, dt));
            }

            m_PlotDot.anchoredPosition = frame.Move * PlotRadius;

            int touches = 0;
            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                for (int i = 0; i < touchscreen.touches.Count; i++)
                {
                    if (touchscreen.touches[i].press.isPressed)
                    {
                        touches++;
                    }
                }
            }

            Rect safe = Screen.safeArea;
            m_Builder.Length = 0;
            m_Builder.Append("Move   x ").Append(frame.Move.x.ToString("+0.00;-0.00")).Append("  y ")
                .Append(frame.Move.y.ToString("+0.00;-0.00")).Append("  |").Append(frame.Move.magnitude.ToString("F2")).Append("|\n");
            m_Builder.Append("Look   yaw ").Append(m_LookRate.x.ToString("+000;-000")).Append("  pitch ")
                .Append(m_LookRate.y.ToString("+000;-000")).Append("  deg/s\n");
            m_Builder.Append("Jump   ").Append(m_JumpFlash > 0f ? "PRESSED" : "-").Append(frame.JumpHeld ? "  held" : string.Empty).Append('\n');
            m_Builder.Append("Dodge  ").Append(m_DodgeFlash > 0f ? "PRESSED" : "-").Append(frame.SprintHeld ? "  sprint held" : string.Empty).Append('\n');
            m_Builder.Append("Scheme ").Append(m_Input.ActiveScheme).Append("   touches ").Append(touches).Append('\n');
            m_Builder.Append("Screen ").Append(Screen.width).Append('x').Append(Screen.height).Append("  dpi ")
                .Append(Mathf.RoundToInt(Screen.dpi)).Append('\n');
            m_Builder.Append("Safe   ").Append(Mathf.RoundToInt(safe.x)).Append(',').Append(Mathf.RoundToInt(safe.y)).Append("  ")
                .Append(Mathf.RoundToInt(safe.width)).Append('x').Append(Mathf.RoundToInt(safe.height));
            m_Text.text = m_Builder.ToString();
        }
    }
}
