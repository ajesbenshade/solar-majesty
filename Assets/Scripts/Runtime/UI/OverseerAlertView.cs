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
        private readonly List<Alert> _sorted = new List<Alert>(8);
        private GUIStyle _text;
        private GUIStyle _hint;
        private GUIStyle _legendTitle;
        private GUIStyle _legendLabel;
        private Texture2D _panel;
        private Texture2D _panelCrit;
        private readonly Dictionary<int, Texture2D> _swatches = new Dictionary<int, Texture2D>(8);

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

        private void OnDestroy()
        {
            DestroyTex(_panel);
            DestroyTex(_panelCrit);
            foreach (var kv in _swatches)
                DestroyTex(kv.Value);
            _swatches.Clear();
        }

        private static void DestroyTex(Texture2D tex)
        {
            if (tex == null) return;
            if (Application.isPlaying) Destroy(tex);
            else DestroyImmediate(tex);
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

            DrawFeed(sw);
            DrawLegend(Screen.height / s);

            GUI.matrix = prev;
        }

        private void DrawFeed(float sw)
        {
            const float width = 300f;
            float x = sw - width - 12f;
            float y = 96f;

            for (int i = 0; i < _sorted.Count; i++)
            {
                Alert alert = _sorted[i];
                var rect = new Rect(x, y, width, 32f);
                GUI.DrawTexture(rect, alert.Severity == AlertSeverity.Critical ? _panelCrit : _panel);
                GUI.DrawTexture(new Rect(rect.x, rect.y, 3f, rect.height), Swatch(ColorFor(alert.Severity)));

                if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
                {
                    if (alert.HasPosition)
                        _loop.GlanceAt(alert.WorldPosition, force: true);
                    _loop.Alerts.Acknowledge(alert);
                }

                GUI.Label(new Rect(rect.x + 10f, rect.y + 4f, rect.width - 44f, 24f),
                    alert.DisplayMessage, _text);
                if (alert.HasPosition)
                    GUI.Label(new Rect(rect.xMax - 36f, rect.y + 8f, 28f, 16f), "GO", _hint);

                y += 36f;
            }
        }

        private void DrawLegend(float sh)
        {
            MapOverlayMode mode = _loop.OverlayMode;
            if (mode == MapOverlayMode.None) return;

            var entries = MapOverlay.LegendFor(mode);
            float height = 22f + entries.Count * 16f;
            var box = new Rect(12f, sh - 96f - height, 168f, height);
            GUI.DrawTexture(box, _panel);
            GUI.DrawTexture(new Rect(box.x, box.y, box.width, 2f), Swatch(new Color(0.84f, 0.64f, 0.24f)));
            GUI.Label(new Rect(box.x + 8f, box.y + 4f, box.width - 12f, 16f),
                MapOverlay.TitleFor(mode), _legendTitle);

            float y = box.y + 22f;
            for (int i = 0; i < entries.Count; i++)
            {
                GUI.DrawTexture(new Rect(box.x + 8f, y + 3f, 10f, 10f), Swatch(entries[i].Color));
                GUI.Label(new Rect(box.x + 22f, y, box.width - 28f, 16f), entries[i].Label, _legendLabel);
                y += 16f;
            }
        }

        private void EnsureStyles()
        {
            if (_text != null) return;

            _panel = Solid(new Color(0.07f, 0.067f, 0.063f, 0.92f));
            _panelCrit = Solid(new Color(0.19f, 0.07f, 0.06f, 0.94f));

            _text = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                wordWrap = true,
                clipping = TextClipping.Clip,
                normal = { textColor = new Color(0.93f, 0.92f, 0.89f) }
            };
            _hint = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = new Color(0.62f, 0.60f, 0.56f) }
            };
            _legendTitle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.95f, 0.54f, 0.12f) }
            };
            _legendLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                normal = { textColor = new Color(0.62f, 0.60f, 0.56f) }
            };
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

        private Texture2D Swatch(Color color)
        {
            int key = color.GetHashCode();
            if (_swatches.TryGetValue(key, out Texture2D existing) && existing != null)
                return existing;

            var tex = Solid(color);
            _swatches[key] = tex;
            return tex;
        }

        private static Texture2D Solid(Color color)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "SM_AlertSwatch",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point
            };
            tex.SetPixel(0, 0, color);
            tex.Apply(false, true);
            return tex;
        }
    }
}
