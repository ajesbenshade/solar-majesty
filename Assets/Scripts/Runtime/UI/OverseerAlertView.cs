using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// IMGUI view for the alert feed and overlay legend.
    ///
    /// A runtime-created UI Toolkit <c>UIDocument</c> + <c>PanelSettings</c> paints an opaque
    /// full-screen panel (Unity's default theme fills the document root) and can clear the Game
    /// view before the world is composited. Until an authored PanelSettings asset exists, this
    /// stays on IMGUI so Play Mode shows the title, HUD, and world.
    /// </summary>
    public sealed class OverseerAlertView : MonoBehaviour
    {
        private GameLoop _loop;
        private OverseerHud _hud;
        private readonly List<Alert> _sorted = new List<Alert>(8);
        private GUIStyle _text;
        private GUIStyle _legendTitle;
        private GUIStyle _legendLabel;

        public static OverseerAlertView Ensure(GameLoop loop)
        {
            if (loop == null) return null;

            var view = loop.GetComponent<OverseerAlertView>();
            if (view == null) view = loop.gameObject.AddComponent<OverseerAlertView>();
            view._loop = loop;
            return view;
        }

        private void Awake()
        {
            if (_loop == null) _loop = GetComponent<GameLoop>();
        }

        private void OnGUI()
        {
            if (_loop == null || _loop.Screen != DemoScreen.Playing) return;

            EnsureStyles();

            AlertFeed feed = _loop.Alerts;
            if (feed == null) return;

            feed.Tick(Time.unscaledTime);
            feed.Sorted(_sorted);

            float s = Mathf.Clamp(DemoSettings.HudScale, 0.85f, 1.25f);
            float sw = Screen.width / s;
            var prev = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s, s, 1f));

            if (_hud == null) _hud = GetComponent<OverseerHud>();
            DrawFeed(sw, _hud != null ? _hud.RightColumnBottom + 10f : 96f);
            DrawLegend(Screen.height / s);

            GUI.matrix = prev;
        }

        private void DrawFeed(float sw, float top)
        {
            const float width = 300f;
            float x = sw - width - 16f;
            float y = top;

            for (int i = 0; i < _sorted.Count; i++)
            {
                Alert alert = _sorted[i];
                var rect = new Rect(x, y, width, 36f);
                Color sev = ColorFor(alert.Severity);
                bool critical = alert.Severity == AlertSeverity.Critical;
                bool hot = rect.Contains(Event.current.mousePosition);

                if (critical)
                {
                    float pulse = DemoSettings.ReduceMotion ? 0.4f : 0.25f + 0.3f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f));
                    HudSkin.Glow(rect, HudSkin.WithAlpha(sev, pulse), 0.55f);
                }
                HudSkin.Panel(rect, hot ? 1f : 0.92f, false);
                if (critical) HudSkin.Wash(rect, HudSkin.WithAlpha(sev, 0.16f));
                HudSkin.Pill(new Rect(rect.x + 5f, rect.y + 8f, 3f, rect.height - 16f), sev);
                HudSkin.Icon(new Rect(rect.x + 14f, rect.y + 9f, 18f, 18f), IconFor(alert.Severity), TintFor(alert.Severity));

                if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
                {
                    if (alert.HasPosition)
                        _loop.GlanceAt(alert.WorldPosition, force: true);
                    _loop.Alerts.Acknowledge(alert);
                }

                GUI.Label(new Rect(rect.x + 40f, rect.y + 3f, rect.width - 66f, 30f), alert.DisplayMessage, _text);
                if (alert.HasPosition)
                    HudSkin.Icon(new Rect(rect.xMax - 20f, rect.y + 13f, 10f, 10f), UiIcons.Get(IconId.Play),
                        hot ? HudSkin.GoldBright : HudSkin.TextFaint);

                y += 42f;
            }
        }

        private void DrawLegend(float sh)
        {
            MapOverlayMode mode = _loop.OverlayMode;
            if (mode == MapOverlayMode.None) return;

            var entries = MapOverlay.LegendFor(mode);
            float height = 32f + entries.Count * 18f;
            var box = new Rect(16f, sh - 96f - height, 180f, height);
            HudSkin.Panel(box);
            GUI.Label(new Rect(box.x + 12f, box.y + 8f, box.width - 20f, 16f), MapOverlay.TitleFor(mode), _legendTitle);

            float y = box.y + 28f;
            for (int i = 0; i < entries.Count; i++)
            {
                Color c = entries[i].Color;
                HudSkin.Dot(new Vector2(box.x + 17f, y + 8f), 10f, new Color(c.r, c.g, c.b, Mathf.Max(0.8f, c.a)));
                GUI.Label(new Rect(box.x + 28f, y, box.width - 34f, 16f), entries[i].Label, _legendLabel);
                y += 18f;
            }
        }

        private void EnsureStyles()
        {
            if (_text != null) return;

            _text = HudSkin.Text(HudSkin.Body, 12, FontStyle.Normal, HudSkin.TextPrimary, TextAnchor.MiddleLeft);
            _text.wordWrap = true;
            _legendTitle = HudSkin.Text(HudSkin.Display, 11, FontStyle.Bold, HudSkin.Gold, TextAnchor.MiddleLeft);
            _legendLabel = HudSkin.Text(HudSkin.Body, 11, FontStyle.Normal, HudSkin.TextMuted, TextAnchor.MiddleLeft);
        }

        private static Texture2D IconFor(AlertSeverity severity)
        {
            switch (severity)
            {
                case AlertSeverity.Good: return UiIcons.Get(IconId.Check);
                case AlertSeverity.Warning:
                case AlertSeverity.Critical: return UiIcons.Get(IconId.Alert);
                default: return UiIcons.Get(IconId.Sun);
            }
        }

        private static Color TintFor(AlertSeverity severity)
        {
            switch (severity)
            {
                case AlertSeverity.Good: return HudSkin.Good;
                case AlertSeverity.Warning: return Color.white;
                case AlertSeverity.Critical: return new Color(1f, 0.55f, 0.5f);
                default: return HudSkin.Gold;
            }
        }

        private static Color ColorFor(AlertSeverity severity)
        {
            switch (severity)
            {
                case AlertSeverity.Good: return new Color(0.49f, 0.80f, 0.49f);
                case AlertSeverity.Warning: return new Color(0.93f, 0.71f, 0.24f);
                case AlertSeverity.Critical: return new Color(0.91f, 0.33f, 0.26f);
                default: return new Color(0.84f, 0.64f, 0.24f);
            }
        }
    }
}
