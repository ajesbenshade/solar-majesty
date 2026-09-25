using System;
using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Shared look for every IMGUI surface: palette, typefaces, and the baked chrome (smoked-glass
    /// panels, soft shadows, button plates, minimap bezel, treasury plaque).
    ///
    /// Like <see cref="UiIcons"/>, all art is drawn from signed distances into small textures at
    /// first use, so nothing ships as a binary asset and edges stay anti-aliased at any HUD scale.
    /// Panels are 9-sliced: corner radius and shadow falloff keep their pixel size however large
    /// the panel is.
    /// </summary>
    public static class HudSkin
    {
        // ---- palette ---------------------------------------------------------------------------
        // Smoked glass over the world, brass for structure, one warm orange for "this is live".

        public static readonly Color GlassTop = new Color(0.070f, 0.078f, 0.094f, 0.80f);
        public static readonly Color GlassBottom = new Color(0.028f, 0.031f, 0.040f, 0.88f);
        public static readonly Color Gold = new Color(0.88f, 0.71f, 0.40f);
        public static readonly Color GoldBright = new Color(1f, 0.87f, 0.58f);
        public static readonly Color GoldDeep = new Color(0.58f, 0.43f, 0.20f);
        public static readonly Color Accent = new Color(1f, 0.53f, 0.16f);
        public static readonly Color AccentDeep = new Color(0.86f, 0.33f, 0.06f);
        public static readonly Color TextPrimary = new Color(0.95f, 0.94f, 0.91f);
        public static readonly Color TextMuted = new Color(0.62f, 0.64f, 0.68f);
        public static readonly Color TextFaint = new Color(0.46f, 0.48f, 0.52f);
        public static readonly Color Ink = new Color(0.08f, 0.065f, 0.05f);
        public static readonly Color Good = new Color(0.56f, 0.87f, 0.63f);
        public static readonly Color Alarm = new Color(1f, 0.38f, 0.31f);
        public static readonly Color Info = new Color(0.45f, 0.82f, 1f);

        public static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        // ---- typefaces -------------------------------------------------------------------------
        // OS fonts, so nothing is bundled: Bahnschrift on Windows, DIN / Avenir Next on macOS.
        // A Font dropped at Resources/UI/Fonts/HudDisplay|HudNumeric|HudBody wins over both.

        private static readonly string[] DisplayFaces =
            { "Bahnschrift SemiBold", "Bahnschrift", "Avenir Next Condensed", "Roboto Condensed", "Barlow Semi Condensed", "Segoe UI Semibold", "Helvetica Neue", "Arial" };
        private static readonly string[] NumericFaces =
            { "Bahnschrift", "DIN Alternate", "Avenir Next Condensed", "Roboto Condensed", "Segoe UI", "Helvetica Neue", "Arial" };
        private static readonly string[] BodyFaces =
            { "Avenir Next", "Segoe UI", "Helvetica Neue", "Roboto", "Noto Sans", "Ubuntu", "DejaVu Sans", "Arial" };

        private static bool _fontsResolved;
        private static Font _display;
        private static Font _numeric;
        private static Font _body;

        /// <summary>Condensed caps: headers, labels, buttons.</summary>
        public static Font Display { get { ResolveFonts(); return _display; } }
        /// <summary>Tabular-feeling numerals for the treasury, stockpile, and clocks.</summary>
        public static Font Numeric { get { ResolveFonts(); return _numeric; } }
        /// <summary>Humanist sans for sentences: toasts, cards, tutorial.</summary>
        public static Font Body { get { ResolveFonts(); return _body; } }

        private static void ResolveFonts()
        {
            if (_fontsResolved) return;
            _fontsResolved = true;
            HashSet<string> installed = null;
            try
            {
                installed = new HashSet<string>(Font.GetOSInstalledFontNames(), StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                // Some platforms cannot enumerate; the default IMGUI font stands in.
            }
            _display = Resources.Load<Font>("UI/Fonts/HudDisplay") ?? OsFont(DisplayFaces, installed);
            _numeric = Resources.Load<Font>("UI/Fonts/HudNumeric") ?? OsFont(NumericFaces, installed) ?? _display;
            _body = Resources.Load<Font>("UI/Fonts/HudBody") ?? OsFont(BodyFaces, installed) ?? _display;
        }

        private static Font OsFont(string[] faces, HashSet<string> installed)
        {
            if (installed == null || installed.Count == 0) return null;
            for (int i = 0; i < faces.Length; i++)
            {
                if (!installed.Contains(faces[i])) continue;
                var font = Font.CreateDynamicFontFromOSFont(faces[i], 14);
                if (font != null)
                {
                    font.hideFlags = HideFlags.HideAndDontSave;
                    return font;
                }
            }
            return null;
        }

        public static GUIStyle Text(Font font, int size, FontStyle style, Color color, TextAnchor anchor) =>
            new GUIStyle
            {
                font = font,
                fontSize = size,
                fontStyle = style,
                alignment = anchor,
                wordWrap = false,
                richText = false,
                clipping = TextClipping.Clip,
                normal = { textColor = color }
            };

        // ---- draw helpers ----------------------------------------------------------------------

        private static bool Repaint => Event.current != null && Event.current.type == EventType.Repaint;

        public static void Fill(Rect r, Color c)
        {
            if (!Repaint) return;
            var prev = GUI.color;
            GUI.color = prev * c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        public static void Tex(Rect r, Texture tex, Color tint)
        {
            if (tex == null || !Repaint) return;
            var prev = GUI.color;
            GUI.color = prev * tint;
            GUI.DrawTexture(r, tex, ScaleMode.StretchToFill, true);
            GUI.color = prev;
        }

        public static void Icon(Rect r, Texture tex, Color tint)
        {
            if (tex == null || !Repaint) return;
            var prev = GUI.color;
            GUI.color = prev * tint;
            GUI.DrawTexture(r, tex, ScaleMode.ScaleToFit, true);
            GUI.color = prev;
        }

        private static void Slice(Rect r, GUIStyle style, Color tint)
        {
            if (style == null || !Repaint) return;
            var prev = GUI.color;
            GUI.color = prev * tint;
            style.Draw(r, GUIContent.none, false, false, false, false);
            GUI.color = prev;
        }

        /// <summary>Soft drop shadow whose core lines up with <paramref name="r"/>.</summary>
        public static void Shadow(Rect r, float alpha = 0.5f)
        {
            EnsureChrome();
            Slice(Expand(r, ShadowSpread), _shadowStyle, new Color(0f, 0f, 0f, alpha));
        }

        /// <summary>Coloured halo, same falloff as the shadow.</summary>
        public static void Glow(Rect r, Color c, float spread = 1f)
        {
            EnsureChrome();
            float s = ShadowSpread * spread;
            Slice(Expand(r, s), _shadowStyle, c);
        }

        /// <summary>Smoked-glass panel: shadow, gradient plate, hairline edge, brass sheen on top.</summary>
        public static void Panel(Rect r, float opacity = 1f, bool sheen = true)
        {
            EnsureChrome();
            Shadow(r, 0.55f * opacity);
            Slice(r, _glassStyle, new Color(1f, 1f, 1f, opacity));
            if (sheen)
                Tex(new Rect(r.x + 10f, r.y, r.width - 20f, 1f), _fadeH, WithAlpha(Gold, 0.75f * opacity));
        }

        /// <summary>A darker inset well (meters, text fields, slots).</summary>
        public static void Well(Rect r, float opacity = 1f)
        {
            EnsureChrome();
            Slice(r, _wellStyle, new Color(1f, 1f, 1f, opacity));
        }

        /// <summary>Rounded-cap bar of any height.</summary>
        public static void Pill(Rect r, Color c)
        {
            EnsureChrome();
            if (!Repaint || r.width <= 0.5f || r.height <= 0.5f) return;
            float cap = Mathf.Min(r.height * 0.5f, r.width * 0.5f);
            var prev = GUI.color;
            GUI.color = prev * c;
            GUI.DrawTextureWithTexCoords(new Rect(r.x, r.y, cap, r.height), _disc, new Rect(0f, 0f, 0.5f, 1f));
            GUI.DrawTextureWithTexCoords(new Rect(r.xMax - cap, r.y, cap, r.height), _disc, new Rect(0.5f, 0f, 0.5f, 1f));
            if (r.width > cap * 2f)
                GUI.DrawTexture(new Rect(r.x + cap, r.y, r.width - cap * 2f, r.height), Texture2D.whiteTexture);
            GUI.color = prev;
        }

        /// <summary>Rounded meter: dark track, lit fill with a top highlight.</summary>
        public static void Meter(Rect r, float t01, Color fill)
        {
            Pill(r, new Color(0f, 0f, 0f, 0.45f));
            Pill(new Rect(r.x, r.y, r.width, Mathf.Max(1f, r.height * 0.5f)), new Color(1f, 1f, 1f, 0.04f));
            float w = r.width * Mathf.Clamp01(t01);
            if (w < 0.5f) return;
            w = Mathf.Max(w, Mathf.Min(r.height, r.width));
            var f = new Rect(r.x, r.y, w, r.height);
            Pill(f, fill);
            if (r.height >= 4f)
                Pill(new Rect(f.x + 1f, f.y + 0.5f, Mathf.Max(0f, f.width - 2f), Mathf.Max(1f, r.height * 0.35f)),
                    new Color(1f, 1f, 1f, 0.28f));
        }

        public static void Dot(Vector2 center, float size, Color c)
        {
            EnsureChrome();
            Tex(new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size), _disc, c);
        }

        public static void SoftDot(Vector2 center, float size, Color c)
        {
            EnsureChrome();
            Tex(new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size), _softDot, c);
        }

        public static void Ring(Rect r, Color c)
        {
            EnsureChrome();
            Tex(r, _ring, c);
        }

        /// <summary>Horizontal hairline that fades out at both ends.</summary>
        public static void RuleH(Rect r, Color c)
        {
            EnsureChrome();
            Tex(r, _fadeH, c);
        }

        /// <summary>Vertical divider that fades out at both ends.</summary>
        public static void RuleV(Rect r, Color c)
        {
            EnsureChrome();
            Tex(r, _fadeV, c);
        }

        /// <summary>Dark band that fades to nothing at both ends — subtitle backing over the world.</summary>
        public static void Band(Rect r, float alpha)
        {
            EnsureChrome();
            Tex(r, _band, new Color(0f, 0f, 0f, alpha));
        }

        /// <summary>Coloured version of <see cref="Band"/>: a soft light wash (modal headers).</summary>
        public static void Wash(Rect r, Color c)
        {
            EnsureChrome();
            Tex(r, _band, c);
        }

        /// <summary>Modal dressing: theme-coloured sheen and header wash, brass corner ticks.</summary>
        public static void ModalChrome(Rect r, Color theme)
        {
            Wash(new Rect(r.x, r.y - 24f, r.width, 72f), WithAlpha(theme, 0.16f));
            RuleH(new Rect(r.x, r.y, r.width, 2f), theme);
            CornerTicks(r, WithAlpha(Gold, 0.75f));
        }

        /// <summary>Full-screen dim that darkens the edges more than the centre.</summary>
        public static void Vignette(Rect screen, Color tint, float baseAlpha)
        {
            EnsureChrome();
            Fill(screen, WithAlpha(tint, baseAlpha));
            Tex(screen, _vignette, new Color(0f, 0f, 0f, 0.85f));
        }

        /// <summary>Label with a one-pixel drop shadow, for text that floats over the world.</summary>
        public static void ShadowLabel(Rect r, string text, GUIStyle style, float shadowAlpha = 0.8f)
        {
            if (string.IsNullOrEmpty(text) || style == null) return;
            var col = style.normal.textColor;
            style.normal.textColor = new Color(0f, 0f, 0f, shadowAlpha * col.a);
            GUI.Label(new Rect(r.x + 1f, r.y + 1f, r.width, r.height), text, style);
            style.normal.textColor = col;
            GUI.Label(r, text, style);
        }

        /// <summary>Brass L-brackets just outside the corners (modals, the crest, key cards).</summary>
        public static void CornerTicks(Rect r, Color c, float len = 9f, float t = 1.5f, float inset = -3f)
        {
            var o = Expand(r, -inset);
            Fill(new Rect(o.x, o.y, len, t), c);
            Fill(new Rect(o.x, o.y, t, len), c);
            Fill(new Rect(o.xMax - len, o.y, len, t), c);
            Fill(new Rect(o.xMax - t, o.y, t, len), c);
            Fill(new Rect(o.x, o.yMax - t, len, t), c);
            Fill(new Rect(o.x, o.yMax - len, t, len), c);
            Fill(new Rect(o.xMax - len, o.yMax - t, len, t), c);
            Fill(new Rect(o.xMax - t, o.yMax - len, t, len), c);
        }

        public static Rect Expand(Rect r, float by) =>
            new Rect(r.x - by, r.y - by, r.width + by * 2f, r.height + by * 2f);

        // ---- button plates ---------------------------------------------------------------------

        public enum Plate { Quiet, Primary, Row, RowOn }

        /// <summary>Button style whose states are baked rounded plates.</summary>
        public static GUIStyle Button(Plate plate, Font font, int size, FontStyle fs, TextAnchor anchor, int padLeft, Color text)
        {
            EnsureChrome();
            var s = Text(font, size, fs, text, anchor);
            s.border = new RectOffset(PlateBorder, PlateBorder, PlateBorder, PlateBorder);
            s.padding = new RectOffset(padLeft, 8, 0, 0);
            switch (plate)
            {
                case Plate.Primary:
                    s.normal.background = _plateOn;
                    s.hover.background = _plateOnHover;
                    s.active.background = _plateOnHover;
                    s.focused.background = _plateOn;
                    s.hover.textColor = text;
                    s.active.textColor = text;
                    s.focused.textColor = text;
                    break;
                case Plate.RowOn:
                    s.normal.background = _plateRowOn;
                    s.hover.background = _plateRowOn;
                    s.active.background = _plateRowOn;
                    s.focused.background = _plateRowOn;
                    s.hover.textColor = Color.white;
                    s.active.textColor = text;
                    s.focused.textColor = text;
                    break;
                case Plate.Row:
                    s.normal.background = _plateRow;
                    s.hover.background = _plateRowHover;
                    s.active.background = _plateHover;
                    s.focused.background = _plateRow;
                    s.hover.textColor = Color.white;
                    s.active.textColor = text;
                    s.focused.textColor = text;
                    break;
                default:
                    s.normal.background = _plateOff;
                    s.hover.background = _plateHover;
                    s.active.background = _platePress;
                    s.focused.background = _plateOff;
                    s.hover.textColor = Color.white;
                    s.active.textColor = text;
                    s.focused.textColor = text;
                    break;
            }
            return s;
        }

        /// <summary>Draws a plate directly (for custom-drawn buttons).</summary>
        public static void DrawPlate(Rect r, Plate plate, bool hover)
        {
            EnsureChrome();
            GUIStyle st = plate == Plate.Primary ? (hover ? _plateOnHoverStyle : _plateOnStyle)
                : plate == Plate.RowOn ? _plateRowOnStyle
                : hover ? _plateHoverStyle : _plateOffStyle;
            Slice(r, st, Color.white);
        }

        // ---- skin (scrollbars, sliders, text field) --------------------------------------------

        private static GUISkin _skin;

        /// <summary>Copy of the default skin with slim scrollbars, brass sliders, and a glass text field. Call inside OnGUI.</summary>
        public static GUISkin Skin()
        {
            if (_skin != null) return _skin;
            EnsureChrome();
            _skin = UnityEngine.Object.Instantiate(GUI.skin);
            _skin.hideFlags = HideFlags.HideAndDontSave;
            _skin.font = Body;

            var none = new GUIStyle { fixedWidth = 0.001f, fixedHeight = 0.001f };
            _skin.verticalScrollbar = new GUIStyle
            {
                fixedWidth = 6f,
                margin = new RectOffset(4, 0, 0, 0),
                normal = { background = _track },
                border = new RectOffset(3, 3, 3, 3)
            };
            _skin.verticalScrollbarThumb = new GUIStyle
            {
                fixedWidth = 6f,
                normal = { background = _thumb },
                border = new RectOffset(3, 3, 3, 3)
            };
            _skin.verticalScrollbarUpButton = none;
            _skin.verticalScrollbarDownButton = none;
            _skin.horizontalScrollbar = new GUIStyle(_skin.verticalScrollbar) { fixedWidth = 0f, fixedHeight = 6f };
            _skin.horizontalScrollbarThumb = new GUIStyle(_skin.verticalScrollbarThumb) { fixedWidth = 0f, fixedHeight = 6f };
            _skin.horizontalScrollbarLeftButton = none;
            _skin.horizontalScrollbarRightButton = none;

            _skin.horizontalSlider = new GUIStyle
            {
                fixedHeight = 4f,
                margin = new RectOffset(0, 0, 6, 6),
                normal = { background = _sliderTrack },
                border = new RectOffset(2, 2, 2, 2)
            };
            _skin.horizontalSliderThumb = new GUIStyle
            {
                fixedWidth = 14f,
                fixedHeight = 14f,
                margin = new RectOffset(0, 0, -5, 0),
                normal = { background = _knob },
                hover = { background = _knobHot },
                active = { background = _knobHot }
            };

            _skin.textField = new GUIStyle
            {
                font = Body,
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(8, 8, 2, 2),
                border = new RectOffset(PlateBorder, PlateBorder, PlateBorder, PlateBorder),
                clipping = TextClipping.Clip,
                normal = { background = _wellTex, textColor = TextPrimary },
                hover = { background = _wellTex, textColor = TextPrimary },
                focused = { background = _fieldFocus, textColor = Color.white },
                active = { background = _fieldFocus, textColor = Color.white }
            };
            _skin.settings.cursorColor = GoldBright;
            _skin.settings.selectionColor = WithAlpha(Accent, 0.45f);
            return _skin;
        }

        // ---- baked art -------------------------------------------------------------------------

        private const int PlateBorder = 7;
        private const float ShadowSpread = 18f;

        private static bool _chromeReady;
        private static Texture2D _glass, _well, _wellTex, _shadow, _disc, _softDot, _ring, _fadeH, _fadeV, _band, _vignette;
        private static Texture2D _plateOff, _plateHover, _platePress, _plateOn, _plateOnHover, _plateRow, _plateRowHover, _plateRowOn;
        private static Texture2D _track, _sliderTrack, _thumb, _knob, _knobHot, _fieldFocus;
        private static GUIStyle _glassStyle, _wellStyle, _shadowStyle;
        private static GUIStyle _plateOffStyle, _plateHoverStyle, _plateOnStyle, _plateOnHoverStyle, _plateRowOnStyle;
        private static Texture2D _bezel, _mapDisc, _crest;

        /// <summary>Brass minimap bezel with compass ticks (transparent centre).</summary>
        public static Texture2D MinimapBezel { get { if (_bezel == null) _bezel = BakeBezel(256); return _bezel; } }
        /// <summary>Dark terrain disc with survey grid and range rings.</summary>
        public static Texture2D MinimapDisc { get { if (_mapDisc == null) _mapDisc = BakeMapDisc(256); return _mapDisc; } }
        /// <summary>Treasury plaque: chamfered brass-rimmed plate with a crown jewel. 256×68 at 2×.</summary>
        public static Texture2D CrestPlate { get { if (_crest == null) _crest = BakeCrest(); return _crest; } }
        /// <summary>Plate area inside <see cref="CrestPlate"/>, in display pixels.</summary>
        public static readonly Vector2 CrestSize = new Vector2(240f, 52f);
        public const float CrestPad = 8f;

        private static void EnsureChrome()
        {
            if (_chromeReady && _glass != null) return;
            _chromeReady = true;

            _glass = BakePlate(32, 9f, GlassTop, GlassBottom, new Color(1f, 1f, 1f, 0.085f), 1f, new Color(1f, 1f, 1f, 0.05f));
            _well = BakePlate(24, 6f, new Color(0f, 0f, 0f, 0.42f), new Color(0f, 0f, 0f, 0.30f), new Color(1f, 1f, 1f, 0.06f), 1f, Color.clear);
            _wellTex = _well;
            _fieldFocus = BakePlate(24, 6f, new Color(0f, 0f, 0f, 0.5f), new Color(0f, 0f, 0f, 0.38f), WithAlpha(Gold, 0.7f), 1f, Color.clear);
            _shadow = BakeShadow(64, 18f, 6f);
            _disc = BakeDisc(64, false);
            _softDot = BakeDisc(64, true);
            _ring = BakeRing(64, 2.5f);
            _fadeH = BakeFade(128, true);
            _fadeV = BakeFade(128, false);
            _band = BakeBand(128);
            _vignette = BakeVignette(128);

            _plateOff = BakePlate(24, 6f, new Color(1f, 1f, 1f, 0.07f), new Color(1f, 1f, 1f, 0.035f), new Color(1f, 1f, 1f, 0.11f), 1f, new Color(1f, 1f, 1f, 0.05f));
            _plateHover = BakePlate(24, 6f, new Color(1f, 1f, 1f, 0.14f), new Color(1f, 1f, 1f, 0.08f), WithAlpha(Gold, 0.55f), 1f, new Color(1f, 1f, 1f, 0.08f));
            _platePress = BakePlate(24, 6f, new Color(1f, 1f, 1f, 0.05f), new Color(1f, 1f, 1f, 0.10f), WithAlpha(Gold, 0.7f), 1f, Color.clear);
            _plateOn = BakePlate(24, 6f, new Color(1f, 0.64f, 0.26f, 1f), AccentDeep, WithAlpha(GoldBright, 0.9f), 1f, new Color(1f, 0.92f, 0.75f, 0.55f));
            _plateOnHover = BakePlate(24, 6f, new Color(1f, 0.72f, 0.36f, 1f), new Color(0.94f, 0.42f, 0.10f, 1f), GoldBright, 1f, new Color(1f, 0.95f, 0.82f, 0.7f));
            _plateRow = BakePlate(24, 5f, new Color(1f, 1f, 1f, 0.035f), new Color(1f, 1f, 1f, 0.02f), Color.clear, 0f, Color.clear);
            _plateRowHover = BakePlate(24, 5f, new Color(1f, 1f, 1f, 0.09f), new Color(1f, 1f, 1f, 0.06f), WithAlpha(Gold, 0.35f), 1f, Color.clear);
            _plateRowOn = BakePlate(24, 5f, WithAlpha(Accent, 0.30f), WithAlpha(Accent, 0.18f), WithAlpha(Accent, 0.85f), 1f, new Color(1f, 0.85f, 0.6f, 0.25f));

            _track = BakePlate(8, 3f, new Color(0f, 0f, 0f, 0.35f), new Color(0f, 0f, 0f, 0.35f), new Color(1f, 1f, 1f, 0.05f), 1f, Color.clear);
            _sliderTrack = BakePlate(8, 3f, WithAlpha(Gold, 0.45f), WithAlpha(GoldDeep, 0.45f), new Color(0f, 0f, 0f, 0.4f), 1f, Color.clear);
            _thumb = BakePlate(8, 3f, new Color(1f, 1f, 1f, 0.26f), new Color(1f, 1f, 1f, 0.20f), Color.clear, 0f, Color.clear);
            _knob = BakeKnob(32, false);
            _knobHot = BakeKnob(32, true);

            _glassStyle = SliceStyle(_glass, 11);
            _wellStyle = SliceStyle(_well, PlateBorder);
            _shadowStyle = SliceStyle(_shadow, 28);
            _plateOffStyle = SliceStyle(_plateOff, PlateBorder);
            _plateHoverStyle = SliceStyle(_plateHover, PlateBorder);
            _plateOnStyle = SliceStyle(_plateOn, PlateBorder);
            _plateOnHoverStyle = SliceStyle(_plateOnHover, PlateBorder);
            _plateRowOnStyle = SliceStyle(_plateRowOn, PlateBorder);
        }

        private static GUIStyle SliceStyle(Texture2D tex, int border) =>
            new GUIStyle { normal = { background = tex }, border = new RectOffset(border, border, border, border) };

        private static Texture2D NewTex(int w, int h, bool mips, string name)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, mips)
            {
                name = "SM_Hud_" + name,
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 0
            };
        }

        private static Texture2D Finish(Texture2D tex, Color[] px)
        {
            tex.SetPixels(px);
            tex.Apply(tex.mipmapCount > 1, false);
            return tex;
        }

        /// <summary>Signed distance to a rounded box centred on the origin (negative inside).</summary>
        private static float RoundBox(Vector2 p, Vector2 half, float r)
        {
            var q = new Vector2(Mathf.Abs(p.x) - half.x + r, Mathf.Abs(p.y) - half.y + r);
            var outside = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f));
            return outside.magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
        }

        /// <summary>Straight-alpha "a over b".</summary>
        private static Color Over(Color a, float coverage, Color b)
        {
            float aa = Mathf.Clamp01(a.a * coverage);
            float outA = aa + b.a * (1f - aa);
            if (outA <= 0.0001f) return Color.clear;
            Color rgb = (a * aa + b * b.a * (1f - aa)) / outA;
            return new Color(rgb.r, rgb.g, rgb.b, outA);
        }

        /// <summary>Rounded plate: vertical gradient, inner border, one-pixel top highlight.</summary>
        private static Texture2D BakePlate(int size, float radius, Color top, Color bottom, Color border, float borderW, Color highlight)
        {
            var tex = NewTex(size, size, false, "Plate");
            var px = new Color[size * size];
            var half = new Vector2(size * 0.5f, size * 0.5f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float fromTop = size - 1 - y + 0.5f;
                var p = new Vector2(x + 0.5f - half.x, fromTop - half.y);
                float d = RoundBox(p, half, radius);
                float cover = Mathf.Clamp01(0.5f - d);
                if (cover <= 0f) { px[y * size + x] = Color.clear; continue; }

                float t = Mathf.Clamp01((fromTop - radius) / Mathf.Max(1f, size - radius * 2f));
                Color c = Color.Lerp(top, bottom, t);
                if (highlight.a > 0f)
                {
                    float hl = Mathf.Clamp01(1f - Mathf.Abs(fromTop - (borderW + 1f)));
                    c = Over(highlight, hl * Mathf.Clamp01(-d - borderW), c);
                }
                if (borderW > 0f && border.a > 0f)
                {
                    float band = Mathf.Clamp01(d + borderW + 0.5f);
                    c = Over(border, band, c);
                }
                c.a *= cover;
                px[y * size + x] = c;
            }
            return Finish(tex, px);
        }

        private static Texture2D BakeShadow(int size, float blur, float radius)
        {
            var tex = NewTex(size, size, false, "Shadow");
            var px = new Color[size * size];
            float inset = blur;
            var half = new Vector2(size * 0.5f - inset, size * 0.5f - inset);
            var c = new Vector2(size * 0.5f, size * 0.5f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = RoundBox(new Vector2(x + 0.5f, y + 0.5f) - c, half, radius);
                float a = d <= 0f ? 1f : Mathf.Pow(Mathf.Clamp01(1f - d / blur), 2.2f);
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
            return Finish(tex, px);
        }

        private static Texture2D BakeDisc(int size, bool soft)
        {
            var tex = NewTex(size, size, true, soft ? "SoftDot" : "Disc");
            var px = new Color[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                float a = soft
                    ? Mathf.Pow(Mathf.Clamp01(1f - d / r), 2f)
                    : Mathf.Clamp01(r - 1f - d + 0.5f);
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
            return Finish(tex, px);
        }

        private static Texture2D BakeRing(int size, float thickness)
        {
            var tex = NewTex(size, size, true, "Ring");
            var px = new Color[size * size];
            float c = size * 0.5f;
            float r = c - thickness - 1f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Abs(Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c)) - r);
                px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(thickness * 0.5f - d + 0.5f));
            }
            return Finish(tex, px);
        }

        private static Texture2D BakeFade(int len, bool horizontal)
        {
            var tex = horizontal ? NewTex(len, 1, false, "FadeH") : NewTex(1, len, false, "FadeV");
            var px = new Color[len];
            for (int i = 0; i < len; i++)
            {
                float u = (i + 0.5f) / len;
                float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Min(u, 1f - u) * 3.2f));
                px[i] = new Color(1f, 1f, 1f, a);
            }
            return Finish(tex, px);
        }

        private static Texture2D BakeBand(int size)
        {
            var tex = NewTex(size, 16, false, "Band");
            var px = new Color[size * 16];
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size;
                float v = (y + 0.5f) / 16f;
                float ax = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Min(u, 1f - u) * 4f));
                float ay = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Min(v, 1f - v) * 2.6f));
                px[y * size + x] = new Color(1f, 1f, 1f, ax * ay);
            }
            return Finish(tex, px);
        }

        private static Texture2D BakeVignette(int size)
        {
            var tex = NewTex(size, size, false, "Vignette");
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f;
                float v = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Sqrt(u * u + v * v) / 1.4142f;
                px[y * size + x] = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 0.25f) / 0.75f)));
            }
            return Finish(tex, px);
        }

        private static Texture2D BakeKnob(int size, bool hot)
        {
            var tex = NewTex(size, size, true, "Knob");
            var px = new Color[size * size];
            float c = size * 0.5f;
            float r = c - 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float d = Vector2.Distance(p, new Vector2(c, c));
                float cover = Mathf.Clamp01(r - d + 0.5f);
                float shade = Mathf.Clamp01((p.y - (c - r)) / (2f * r));
                Color body = Color.Lerp(hot ? GoldDeep * 1.2f : GoldDeep, hot ? Color.white : GoldBright, shade);
                body.a = 1f;
                float rim = Mathf.Clamp01(1f - Mathf.Abs(d - (r - 1f)));
                body = Over(new Color(0.2f, 0.14f, 0.06f, 0.9f), rim, body);
                float core = Mathf.Clamp01(r * 0.32f - d + 0.5f);
                body = Over(new Color(0.18f, 0.12f, 0.05f, 0.9f), core, body);
                body.a *= cover;
                px[y * size + x] = body;
            }
            return Finish(tex, px);
        }

        private static Texture2D BakeBezel(int size)
        {
            var tex = NewTex(size, size, true, "Bezel");
            var px = new Color[size * size];
            float c = size * 0.5f;
            float outer = c - 3f;
            float rimIn = outer - 7f;
            float ringIn = rimIn - 11f;
            var light = new Vector2(-0.55f, 0.83f).normalized;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2(x + 0.5f - c, y + 0.5f - c);
                float d = p.magnitude;
                Color col = Color.clear;

                // Soft outer shadow
                if (d > outer)
                    col = new Color(0f, 0f, 0f, 0.45f * Mathf.Pow(Mathf.Clamp01(1f - (d - outer) / 3f), 2f));

                // Dark ring holding the tick marks
                float ringCover = Mathf.Clamp01(rimIn - d + 0.5f) * Mathf.Clamp01(d - ringIn + 0.5f);
                if (ringCover > 0f)
                {
                    Color ring = new Color(0.035f, 0.037f, 0.045f, 0.94f);
                    float ang = Mathf.Atan2(p.x, p.y) * Mathf.Rad2Deg;
                    if (ang < 0f) ang += 360f;
                    float step = 7.5f;
                    float m = Mathf.Abs(Mathf.Repeat(ang + step * 0.5f, step) - step * 0.5f);
                    bool major = Mathf.Abs(Mathf.Repeat(ang + 45f, 90f) - 45f) < step * 0.5f;
                    float tickLen = major ? 9f : 4f;
                    float px2 = m * Mathf.Deg2Rad * d;
                    float tick = Mathf.Clamp01((major ? 1.1f : 0.7f) - px2 + 0.5f) *
                                 Mathf.Clamp01(d - (rimIn - tickLen) + 0.5f);
                    ring = Over(WithAlpha(Gold, major ? 0.95f : 0.45f), tick, ring);
                    col = Over(ring, ringCover, col);
                }

                // Brass rim, lit from the upper left
                float rimCover = Mathf.Clamp01(outer - d + 0.5f) * Mathf.Clamp01(d - rimIn + 0.5f);
                if (rimCover > 0f)
                {
                    float lit = d > 0.01f ? Vector2.Dot(p / d, light) : 0f;
                    float across = Mathf.Clamp01((d - rimIn) / (outer - rimIn));
                    float bevel = 1f - Mathf.Abs(across - 0.45f) * 1.4f;
                    Color brass = Color.Lerp(GoldDeep, GoldBright, Mathf.Clamp01(0.45f + 0.4f * lit + 0.25f * bevel));
                    brass.a = 1f;
                    col = Over(brass, rimCover, col);
                }

                // Hairline on the inner edge
                float inner = Mathf.Clamp01(1f - Mathf.Abs(d - ringIn));
                col = Over(WithAlpha(Gold, 0.7f), inner, col);

                px[y * size + x] = col;
            }

            // North marker: small brass notch on top of the rim
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float fy = y + 0.5f - c;
                float fx = x + 0.5f - c;
                float top = outer - 1f;
                if (fy < top - 12f || fy > top + 2f) continue;
                float w = (fy - (top - 12f)) * 0.55f;
                float cover = Mathf.Clamp01(w - Mathf.Abs(fx) + 0.5f) * Mathf.Clamp01(top + 2f - fy);
                if (cover <= 0f) continue;
                px[y * size + x] = Over(new Color(1f, 0.6f, 0.2f, 1f), cover, px[y * size + x]);
            }
            return Finish(tex, px);
        }

        private static Texture2D BakeMapDisc(int size)
        {
            var tex = NewTex(size, size, true, "MapDisc");
            var px = new Color[size * size];
            float c = size * 0.5f;
            float r = c - 1f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2(x + 0.5f - c, y + 0.5f - c);
                float d = p.magnitude;
                float cover = Mathf.Clamp01(r - d + 0.5f);
                if (cover <= 0f) { px[y * size + x] = Color.clear; continue; }
                float t = d / r;
                Color col = Color.Lerp(new Color(0.13f, 0.115f, 0.10f), new Color(0.035f, 0.036f, 0.042f), t * t);
                col.a = 0.94f;
                float gx = Mathf.Abs(Mathf.Repeat(x + 0.5f, size / 8f) - size / 16f);
                float gy = Mathf.Abs(Mathf.Repeat(y + 0.5f, size / 8f) - size / 16f);
                float grid = Mathf.Max(Mathf.Clamp01(1f - Mathf.Abs(gx - size / 16f)), Mathf.Clamp01(1f - Mathf.Abs(gy - size / 16f)));
                col = Over(new Color(1f, 1f, 1f, 0.035f), grid, col);
                for (int k = 1; k <= 2; k++)
                {
                    float rr = r * k / 3f;
                    col = Over(WithAlpha(Gold, 0.10f), Mathf.Clamp01(1f - Mathf.Abs(d - rr)), col);
                }
                col = Over(new Color(0f, 0f, 0f, 0.5f), Mathf.Pow(t, 6f), col);
                col.a *= cover;
                px[y * size + x] = col;
            }
            return Finish(tex, px);
        }

        private static Texture2D BakeCrest()
        {
            const int k = 2; // bake at 2x
            int pad = (int)CrestPad * k;
            int w = (int)CrestSize.x * k;
            int h = (int)CrestSize.y * k;
            int tw = w + pad * 2;
            int th = h + pad * 2;
            var tex = NewTex(tw, th, true, "Crest");
            var px = new Color[tw * th];

            float chamfer = 22f * k;
            // Convex hexagon, clockwise in top-down coordinates.
            var poly = new[]
            {
                new Vector2(pad, pad + h * 0.5f),
                new Vector2(pad + chamfer, pad),
                new Vector2(pad + w - chamfer, pad),
                new Vector2(pad + w, pad + h * 0.5f),
                new Vector2(pad + w - chamfer, pad + h),
                new Vector2(pad + chamfer, pad + h),
            };
            var jewel = new Vector2(pad + w * 0.5f, pad + 1f * k);

            for (int y = 0; y < th; y++)
            for (int x = 0; x < tw; x++)
            {
                float fromTop = th - 1 - y + 0.5f;
                var p = new Vector2(x + 0.5f, fromTop);
                float d = ConvexSdf(p, poly);
                float dj = (Mathf.Abs(p.x - jewel.x) + Mathf.Abs(p.y - jewel.y)) * 0.7071f - 6f * k;
                Color col = Color.clear;

                // Drop shadow under the plate
                float ds = ConvexSdf(p - new Vector2(0f, 3f * k), poly);
                if (ds > 0f)
                    col = new Color(0f, 0f, 0f, 0.55f * Mathf.Pow(Mathf.Clamp01(1f - ds / (pad * 0.9f)), 2f));
                else
                    col = new Color(0f, 0f, 0f, 0.55f);

                float cover = Mathf.Clamp01(0.5f - d);
                if (cover > 0f)
                {
                    float v = Mathf.Clamp01((p.y - pad) / h);
                    Color plate = Color.Lerp(new Color(0.11f, 0.11f, 0.13f, 0.96f), new Color(0.025f, 0.027f, 0.034f, 0.97f), v);
                    // Faint brass wash along the top half
                    plate = Over(WithAlpha(Gold, 0.07f), Mathf.Clamp01(1f - v * 2f), plate);
                    // Inner hairline
                    plate = Over(WithAlpha(Gold, 0.5f), Mathf.Clamp01(1.1f * k * 0.5f - Mathf.Abs(d + 6f * k) + 0.5f), plate);
                    // Brass rim
                    float rim = Mathf.Clamp01(d + 3f * k + 0.5f);
                    Color brass = Color.Lerp(GoldBright, GoldDeep, v);
                    brass.a = 1f;
                    plate = Over(brass, rim, plate);
                    col = Over(plate, cover, col);
                }

                float jc = Mathf.Clamp01(0.5f - dj);
                if (jc > 0f)
                {
                    float jv = Mathf.Clamp01((p.y - (jewel.y - 6f * k)) / (12f * k));
                    Color gem = Color.Lerp(new Color(1f, 0.78f, 0.45f), new Color(0.9f, 0.36f, 0.08f), jv);
                    gem.a = 1f;
                    gem = Over(new Color(0.25f, 0.15f, 0.05f, 1f), Mathf.Clamp01(dj + 1.2f * k + 0.5f), gem);
                    col = Over(gem, jc, col);
                }
                px[y * tw + x] = col;
            }
            return Finish(tex, px);
        }

        /// <summary>Signed distance to a convex polygon given clockwise (y-down) vertices. Exact inside, close enough outside for AA and shadows.</summary>
        private static float ConvexSdf(Vector2 p, Vector2[] poly)
        {
            float d = float.MinValue;
            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 a = poly[i];
                Vector2 b = poly[(i + 1) % poly.Length];
                Vector2 e = (b - a).normalized;
                var n = new Vector2(e.y, -e.x); // outward for clockwise y-down winding
                d = Mathf.Max(d, Vector2.Dot(p - a, n));
            }
            return d;
        }
    }
}
