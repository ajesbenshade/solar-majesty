using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// The HUD's one tooltip. Controls offer a <see cref="HudTip"/> for their rect while they draw;
    /// the last offer under the cursor wins (IMGUI draws front-most last), and <see cref="Draw"/>
    /// paints it at the end of the pass, above every panel.
    ///
    /// It appears after a short rest (<see cref="HudTooltipMath.Delay"/>), follows the cursor, wraps,
    /// and stays on screen (<see cref="HudTooltipMath.Place"/>). The gamepad's virtual cursor moves
    /// the real OS cursor, so it works the same with a controller. It stays hidden while a text field
    /// or a key rebind has the keyboard, while a mouse button is held (dragging a slider), and while
    /// a camera beat owns the screen (the HUD does not draw then).
    /// </summary>
    public sealed class HudTooltip
    {
        private const float Pad = 9f;

        private readonly HudTooltipMath.Hover _hover = new HudTooltipMath.Hover();
        private HudTip _tip;
        private bool _has;
        private Vector2 _pointer;
        private Rect _clip;
        private bool _clipped;

        private GUIStyle _title;
        private GUIStyle _key;
        private GUIStyle _body;
        private int _styleVersion = -1;
        private readonly GUIContent _measure = new GUIContent();

        /// <summary>QA captures: show the offered tip at once instead of after the hover delay.</summary>
        public static bool NoDelay;

        /// <summary>
        /// QA captures: remember where each titled tip's control was drawn (screen pixels, top-left
        /// origin) so the capture harness can put the cursor on it the way the gamepad cursor does.
        /// </summary>
        public static bool RecordAnchors;
        public static readonly System.Collections.Generic.Dictionary<string, Rect> Anchors =
            new System.Collections.Generic.Dictionary<string, Rect>();

        /// <summary>The tip being shown this pass (null when none). For the HUD capture harness.</summary>
        public string ShownTitle => (_hover.Visible || NoDelay) && _has ? _tip.Title : null;

        /// <summary>Start of an OnGUI pass. <paramref name="pointer"/> is the cursor in top-level GUI space.</summary>
        public void Begin(Vector2 pointer)
        {
            _pointer = pointer;
            _has = false;
            _clipped = false;
        }

        /// <summary>
        /// Inside a scroll view, rows scrolled out of sight still contain the local mouse position.
        /// Bracket the scroll view with its on-screen rect so only what is visible can offer a tip.
        /// </summary>
        public void BeginClip(Rect viewInTopLevelSpace)
        {
            _clip = viewInTopLevelSpace;
            _clipped = true;
        }

        public void EndClip() => _clipped = false;

        /// <summary>Offer <paramref name="tip"/> for <paramref name="anchor"/> (in the current GUI space). True when the cursor is on it.</summary>
        public bool Offer(Rect anchor, HudTip tip)
        {
            if (tip.IsEmpty) return false;
            var e = Event.current;
            if (e == null) return false;
            if (RecordAnchors && e.type == EventType.Repaint && !string.IsNullOrEmpty(tip.Title))
                Anchors[tip.Title] = GUIUtility.GUIToScreenRect(anchor);
            if (!anchor.Contains(e.mousePosition)) return false;
            if (_clipped && !_clip.Contains(_pointer)) return false;
            _tip = tip;
            _has = true;
            return true;
        }

        /// <summary>Hide now and restart the delay (screen change).</summary>
        public void Reset() => _hover.Reset();

        /// <summary>End of the pass: paints the tip in a <paramref name="viewW"/> × <paramref name="viewH"/> GUI space.</summary>
        public void Draw(float viewW, float viewH, bool suppressed)
        {
            var e = Event.current;
            if (e == null) return;
            bool blocked = suppressed || InputBindings.TextEntryActive || GUIUtility.hotControl != 0 ||
                           Input.GetMouseButton(0) || Input.GetMouseButton(1);
            string id = _has && !blocked ? _tip.Id : null;
            if (e.type != EventType.Repaint) return;
            float now = Time.unscaledTime;
            bool show = _hover.Update(id, now) || (NoDelay && id != null);
            if (!show || id == null) return;
            EnsureStyles();
            Paint(viewW, viewH);
        }

        private void EnsureStyles()
        {
            if (_title != null && _styleVersion == HudTextScale.Version) return;
            _styleVersion = HudTextScale.Version;
            _title = HudSkin.Text(HudSkin.Display, 11, FontStyle.Bold, HudSkin.GoldBright, TextAnchor.UpperLeft);
            _title.wordWrap = true;
            _key = HudSkin.Text(HudSkin.Display, 10, FontStyle.Bold, HudSkin.TextPrimary, TextAnchor.MiddleCenter);
            _key.clipping = TextClipping.Overflow;
            _body = HudSkin.Text(HudSkin.Body, 11, FontStyle.Normal, HudSkin.TextPrimary, TextAnchor.UpperLeft);
            _body.wordWrap = true;
        }

        private float Natural(GUIStyle style, string text)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            _measure.text = text;
            bool wrap = style.wordWrap;
            style.wordWrap = false;
            float w = style.CalcSize(_measure).x;
            style.wordWrap = wrap;
            return w;
        }

        private float Height(GUIStyle style, string text, float width)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            _measure.text = text;
            return style.CalcHeight(_measure, width);
        }

        private void Paint(float viewW, float viewH)
        {
            string title = _tip.Title;
            string body = _tip.Body;
            string key = _tip.Key;

            float keyW = 0f;
            if (!string.IsNullOrEmpty(key))
                keyW = Natural(_key, key) + HudTextScale.Line(12f);
            float keyGap = keyW > 0f ? 8f : 0f;

            float natural = Mathf.Max(Natural(_title, title) + keyW + keyGap, Natural(_body, body));
            float maxContent = HudTextScale.Width(HudTooltipMath.MaxWidth) - Pad * 2f;
            float contentW = HudTooltipMath.WrapWidth(natural + 1f, maxContent, viewW - Pad * 2f);

            float titleW = Mathf.Max(20f, contentW - keyW - keyGap);
            float titleH = Mathf.Max(Height(_title, title, titleW), keyW > 0f ? HudTextScale.Line(16f) : 0f);
            float bodyH = Height(_body, body, contentW);
            float gap = titleH > 0f && bodyH > 0f ? 4f : 0f;
            var size = new Vector2(contentW + Pad * 2f, Pad + titleH + gap + bodyH + Pad - 1f);
            Rect r = HudTooltipMath.Place(_pointer, size, viewW, viewH);

            HudSkin.Shadow(r, 0.55f);
            HudSkin.Panel(r, 1f, false);
            HudSkin.RuleH(new Rect(r.x + 4f, r.y, r.width - 8f, 1f), HudSkin.Accent);

            float x = r.x + Pad;
            float y = r.y + Pad - 1f;
            if (!string.IsNullOrEmpty(title))
                GUI.Label(new Rect(x, y, titleW, titleH), title, _title);
            if (keyW > 0f)
            {
                var keyRect = new Rect(r.xMax - Pad - keyW, y - 1f, keyW, HudTextScale.Line(16f));
                HudSkin.Well(keyRect);
                GUI.Label(keyRect, key, _key);
            }
            if (bodyH > 0f)
                GUI.Label(new Rect(x, y + titleH + gap, contentW, bodyH), body, _body);
        }
    }
}
