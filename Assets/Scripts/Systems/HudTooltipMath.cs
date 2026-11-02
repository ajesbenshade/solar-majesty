using UnityEngine;

namespace SolarMajesty
{
    /// <summary>One tooltip: a short title, an optional sentence or two, and an optional key hint.</summary>
    public readonly struct HudTip
    {
        public readonly string Title;
        public readonly string Body;
        public readonly string Key;

        public HudTip(string title, string body = null, string key = null)
        {
            Title = title;
            Body = body;
            Key = key;
        }

        public bool IsEmpty => string.IsNullOrEmpty(Title) && string.IsNullOrEmpty(Body);

        /// <summary>Identity for the hover delay: the same tip under the cursor keeps its timer.</summary>
        public string Id => Title + "\u001f" + Body;

        public static implicit operator HudTip(string title) => new HudTip(title);
    }

    /// <summary>
    /// Placement and timing for the HUD tooltip. Pure math so it can be tested without a GUI:
    /// the tooltip follows the cursor (below and right of the arrow), flips to the other side when
    /// it would leave the view, and is clamped inside the margins as a last resort.
    /// </summary>
    public static class HudTooltipMath
    {
        /// <summary>Seconds the cursor rests on a control before its tooltip appears.</summary>
        public const float Delay = 0.45f;
        /// <summary>After a tooltip closes, the next one within this window appears at once (sweeping across the dock).</summary>
        public const float Grace = 0.5f;
        public const float OffsetX = 16f;
        public const float OffsetY = 22f;
        public const float Margin = 8f;
        /// <summary>Widest tooltip at 100 % text; the body wraps inside it.</summary>
        public const float MaxWidth = 300f;
        public const float MinWidth = 90f;

        /// <summary>
        /// Rect for a tooltip of <paramref name="size"/> next to <paramref name="cursor"/>, inside a
        /// <paramref name="viewW"/> × <paramref name="viewH"/> view. Never covers the cursor unless the
        /// view is too small to avoid it.
        /// </summary>
        public static Rect Place(Vector2 cursor, Vector2 size, float viewW, float viewH, float margin = Margin)
        {
            float w = Mathf.Max(0f, size.x);
            float h = Mathf.Max(0f, size.y);

            float x = cursor.x + OffsetX;
            if (x + w > viewW - margin)
                x = cursor.x - 8f - w; // flip to the left of the cursor
            float y = cursor.y + OffsetY;
            if (y + h > viewH - margin)
                y = cursor.y - 8f - h; // flip above the cursor

            x = Mathf.Clamp(x, margin, Mathf.Max(margin, viewW - margin - w));
            y = Mathf.Clamp(y, margin, Mathf.Max(margin, viewH - margin - h));
            return new Rect(x, y, w, h);
        }

        /// <summary>Wrap width: the text's own width when it is short, never wider than the view allows.</summary>
        public static float WrapWidth(float natural, float maxWidth, float viewW, float margin = Margin)
        {
            float cap = Mathf.Min(maxWidth, Mathf.Max(MinWidth, viewW - margin * 2f));
            return Mathf.Clamp(natural, Mathf.Min(MinWidth, cap), cap);
        }

        /// <summary>
        /// Hover delay. Feed it the tip under the cursor once per repaint (null when there is none or
        /// tooltips are suppressed); it answers whether to show it.
        /// </summary>
        public sealed class Hover
        {
            private string _id;
            private float _since;
            private float _lastVisible = float.NegativeInfinity;
            private bool _visible;

            public bool Visible => _visible;

            public bool Update(string id, float now)
            {
                if (string.IsNullOrEmpty(id))
                {
                    if (_visible) _lastVisible = now;
                    _visible = false;
                    _id = null;
                    return false;
                }
                if (id != _id)
                {
                    bool warm = _visible || now - _lastVisible <= Grace;
                    _id = id;
                    _since = warm ? now - Delay : now;
                }
                _visible = now - _since >= Delay;
                if (_visible) _lastVisible = now;
                return _visible;
            }

            /// <summary>Hide at once and forget the grace window (a click, a screen change).</summary>
            public void Reset()
            {
                _id = null;
                _visible = false;
                _lastVisible = float.NegativeInfinity;
            }
        }
    }
}
