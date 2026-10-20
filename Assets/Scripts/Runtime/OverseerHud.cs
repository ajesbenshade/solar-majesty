using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Player-facing overseer console (IMGUI): stockpile, tools, flag/build menus,
    /// mission stakes, specialist cards (selected only), win/fail banners.
    /// DebugHud stays on F8 for deep scores.
    /// </summary>
    public partial class OverseerHud : MonoBehaviour
    {
        // Smoked glass over the world, brass for structure, orange only for what is live.
        // Palette and baked chrome live in HudSkin so the alert feed matches.
        private static readonly Color PanelBg = new Color(0.035f, 0.038f, 0.048f, 0.86f);
        private static readonly Color Hairline = new Color(0.88f, 0.71f, 0.40f, 0.28f);
        private static readonly Color Gold = HudSkin.Gold;
        private static readonly Color Accent = HudSkin.Accent;
        private static readonly Color Ink = HudSkin.Ink;
        private static readonly Color TextPrimary = HudSkin.TextPrimary;
        private static readonly Color TextMuted = HudSkin.TextMuted;
        private static readonly Color Track = new Color(1f, 1f, 1f, 0.10f);
        private static readonly Color HpFill = new Color(0.92f, 0.34f, 0.28f);
        private static readonly Color FatigueFill = new Color(0.66f, 0.74f, 0.84f);
        private static readonly Color Alarm = HudSkin.Alarm;
        private static readonly Color Good = HudSkin.Good;

        private const float M = HudLayout.Margin;
        private const float Pad = HudLayout.Pad;
        private const float TopW = 300f;  // top-left command width
        // Majesty 2-style console: one bottom-centre bar with the treasury crest on top.
        private const float CrestRise = HudLayout.CrestRise;
        private const float MapSize = HudLayout.MapSize;

        private GameLoop _loop;
        private bool _failLatched;
        private bool _winDismissed;
        private bool _deadlineDismissed;
        private bool _techOpen;
        private bool _buildMinimized;
        private bool _flagMinimized;
        private bool _orbitalMinimized;
        private Vector2 _techScroll;
        private Vector2 _buildScroll;
        private string _toast;
        private float _toastUntil;
        private int _lastFocusToast = -1;
        private float _contentBottom; // top of dock/popup stack — cards sit above this
        private float _sw;
        private float _sh;
        private float _playTop;
        private float _tutorialBottom = M;
        private float _hudScale = 1f;
        private HudScaleGesture _uiScaleGesture;
        private bool _powerAlarmLatched;
        private bool _confirmNewGame;
        private bool _titleLoad;
        private bool _pauseSlots;
        private bool _pauseSlotsAreLoad;
        private bool _pendingUnlockThrough;
        private int _newCampaignSlot = 1;
        private CelestialBodyId _pendingNewGameBody = CelestialBodyId.Earth;
        private float _toastStart;
        private ColonyStructure _cardFor;
        private float _cardMeasuredH;
        private Vector2 _settingsScroll;
        private float _settingsContentH = 700f;
        private static readonly GameAction[] RebindableActions = (GameAction[])System.Enum.GetValues(typeof(GameAction));
        private GameAction? _rebinding;
        private string _rebindNote;
        private bool _rebindNoteAlarm;
        private readonly HudTooltip _tips = new HudTooltip();
        private int _stylesVersion = -1;

        /// <summary>A row or line height designed at <paramref name="px"/> for 100 % text; grows with TEXT SIZE.</summary>
        private static float L(float px) => HudTextScale.Line(px);

        /// <summary>A panel width designed at <paramref name="px"/>; widens (by half the text growth) with TEXT SIZE.</summary>
        private static float W(float px) => HudTextScale.Width(px);

        /// <summary>The tooltip under the cursor this pass (QA capture harness).</summary>
        public string ShownTooltip => _tips.ShownTitle;


        private bool _stylesReady;
        private GUIStyle _brand;
        private GUIStyle _section;
        private GUIStyle _body;
        private GUIStyle _bodyWrap;
        private GUIStyle _muted;
        private GUIStyle _micro;
        private GUIStyle _microRight;
        private GUIStyle _microAlarm;
        private GUIStyle _value;
        private GUIStyle _pill;
        private GUIStyle _chipOn;
        private GUIStyle _chipOff;
        private GUIStyle _rowOn;
        private GUIStyle _rowOff;
        private GUIStyle _onText;
        private GUIStyle _banner;
        private GUIStyle _action;
        private GUIStyle _wrap;
        private GUIStyle _chipLabel;
        private GUIStyle _titleWord;
        private GUIStyle _titleButton;
        private GUIStyle _titleButtonOn;
        private GUIStyle _planetName;
        private GUIStyle _planetTag;
        private GUIStyle _treasury;
        private GUIStyle _treasuryRate;
        private GUIStyle _logLine;
        private GUIStyle _microCenter;
        private GUIStyle _caps;
        private GUIStyle _capsCenter;
        private GUIStyle _number;
        private GUIStyle _numberSmall;
        private GUIStyle _badge;
        private GUIStyle _tooltipStyle;
        private GUIStyle _tagline;
        private GUIStyle _chatTitle;
        private GUIStyle _chatName;
        private GUIStyle _chatText;
        private GUIStyle _chatHint;
        private GUIStyle _chatPlaceholder;
        private GUIStyle _bubbleText;

        /// <summary>True when the cursor is over a HUD panel (blocks world select).</summary>
        public bool PointerBlocksWorld { get; private set; }

        /// <summary>IMGUI chrome under the cursor (title orrery skips these clicks).</summary>
        public bool HitsHudPanels()
        {
            float s = _hudScale < 0.05f ? DemoSettings.HudScale : _hudScale;
            Vector2 guiMouse = HudPointer.ScreenToGui(Input.mousePosition, Screen.height, s);
            return HudPointer.OverRects(_hitRects, guiMouse);
        }

        public bool TitleConfirmOpen => _confirmNewGame || _titleLoad;

        /// <summary>Esc on the pause screen closes Save/Load before it resumes.</summary>
        public bool ConsumePauseSubmenuEscape()
        {
            if (!_pauseSlots) return false;
            _pauseSlots = false;
            return true;
        }

        public void PromptNewCampaign(CelestialBodyId body, bool unlockThrough)
        {
            _pendingNewGameBody = body;
            _pendingUnlockThrough = unlockThrough;
            _newCampaignSlot = CampaignSlots.SlotReplacedByNewCampaign(_newCampaignSlot);
            _titleLoad = false;
            _confirmNewGame = true;
        }

        /// <summary>Bottom of the right-hand column (objectives, research) in HUD units; the alert feed stacks under it.</summary>
        public float RightColumnBottom { get; private set; } = 96f;

        private readonly List<Rect> _hitRects = new List<Rect>(12);

        // UI sound bookkeeping. IMGUI has no per-button callbacks, so the click is detected once
        // per OnGUI pass (a left mouse-up that some control consumed) instead of at each of the
        // ~30 GUI.Button calls, and hover is tracked only on the big title and dock buttons.
        private Rect _hoverRect;
        private bool _hoverActive;
        private bool _hoverSeenThisRepaint;

        public void Bind(GameLoop loop)
        {
            _loop = loop;
            _failLatched = false;
            _winDismissed = false;
            _deadlineDismissed = false;
            _toast = null;
            _powerAlarmLatched = false;
            _lastFocusToast = _loop != null ? _loop.FocusedCampus : -1;
            if (_loop != null && _loop.Economy != null)
            {
                _loop.Economy.ResupplyArrived -= OnResupply;
                _loop.Economy.ResupplyArrived += OnResupply;
                _loop.Economy.ResupplyWavedOff -= OnResupplyWavedOff;
                _loop.Economy.ResupplyWavedOff += OnResupplyWavedOff;
                _loop.Economy.UpkeepApplied -= OnUpkeep;
                _loop.Economy.UpkeepApplied += OnUpkeep;
            }
        }

        public void ClearToast()
        {
            _toast = null;
            _toastUntil = 0f;
        }

        public void OnSessionPlaying()
        {
            _confirmNewGame = false;
            _titleLoad = false;
            _pauseSlots = false;
            if (StillCaptureHold.Active) return;
            if (_loop == null || !_loop.StartsEmpty) return;
            // W2 arrival cuts + advisor travel toasts replace the stale Earth briefing.
            if (CampaignCutsceneCatalog.TryGetArrival(_loop.ActiveBody, out _))
                return;
            var body = _loop.BodyProfile;
            string briefing = body != null && !string.IsNullOrEmpty(body.Briefing)
                ? body.BriefingText
                : Loc.S("Colony Commons is down. Build anywhere on open ground.");
            Toast(InputBindings.ExpandKeyTokens(briefing), 6.5f);
        }

        public void Notify(string message, float seconds)
        {
            if (StillCaptureHold.Active) return;
            Toast(message, seconds);
        }

        private void OnResupply()
        {
            string line = _loop?.Economy != null && !string.IsNullOrEmpty(_loop.Economy.LastResupplyLine)
                ? _loop.Economy.LastResupplyLine
                : Loc.S("Trade ship landed — caravan EU waits in the Market till for a collector.");
            Toast(line, 4f);
            DemoAudio.PlayCredits(); // caravan gold landed, not a menu confirm
        }

        private void OnResupplyWavedOff()
        {
            string line = _loop?.Economy != null && !string.IsNullOrEmpty(_loop.Economy.LastResupplyLine)
                ? _loop.Economy.LastResupplyLine
                : Loc.S("Earth ship waved off — no Landing Pad.");
            Toast(line, 4.5f);
        }

        private void OnUpkeep()
        {
            // No grid and no payroll: the upkeep clock has nothing to warn about.
        }

        private void Toast(string message, float seconds)
        {
            if (StillCaptureHold.Active || string.IsNullOrEmpty(message)) return;
            float now = Time.unscaledTime;
            bool repeat = NoticeDedupe.IsRepeat(_toast, _toastStart, message, now, NoticeDedupe.WindowSeconds);
            if (!repeat)
                _toastStart = now;
            _toast = message;
            _toastUntil = now + seconds;
        }

        // ---- drawing primitives -------------------------------------------------

        private static void Fill(Rect r, Color c) => HudSkin.Fill(r, c);

        private static void Outline(Rect r, Color c, float t = 1f)
        {
            Fill(new Rect(r.x, r.y, r.width, t), c);
            Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
            Fill(new Rect(r.x, r.y, t, r.height), c);
            Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
        }

        /// <summary>Glass panel with an optional small-caps header. Returns content rect.</summary>
        private Rect Panel(Rect r, string title, bool accentTab = true, HudTip? tip = null)
        {
            _hitRects.Add(r);
            HudSkin.Panel(r);

            float y = r.y + Pad;
            if (!string.IsNullOrEmpty(title))
            {
                float x = r.x + Pad;
                if (accentTab)
                {
                    HudSkin.Pill(new Rect(x, y + 1f, 3f, L(11f)), Accent);
                    x += 9f;
                }
                var header = new Rect(x, y - 1f, r.xMax - Pad - x, L(15f));
                FitLabel(header, title.ToUpperInvariant(), _section, tip);
                y += L(17f);
                Fill(new Rect(r.x + Pad, y, r.width - Pad * 2f, 1f), new Color(1f, 1f, 1f, 0.06f));
                HudSkin.RuleH(new Rect(r.x + Pad - 30f, y, 120f, 1f), new Color(Gold.r, Gold.g, Gold.b, 0.8f));
                y += 7f;
            }
            return new Rect(r.x + Pad, y, r.width - Pad * 2f, r.yMax - y - Pad);
        }

        private void Meter(Rect r, float t01, Color fill) => HudSkin.Meter(r, t01, fill);

        /// <summary>
        /// Game speed: hold/resume, slower, the current pace, faster, and a row of pips (one per
        /// running speed, clickable). Space and comma/period drive the same state. +/− edits a
        /// selected flag's bounty. The buttons make speed discoverable without a key list.
        /// </summary>
        private void DrawSpeedControl(Rect r)
        {
            bool paused = SimSpeed.IsPaused;
            const float btn = 22f;
            const float h = 20f;
            float x = r.x;

            var hold = new Rect(x, r.y, btn, h);
            if (SpeedButton(hold, UiIcons.Get(paused ? IconId.Play : IconId.Pause), null, paused, true))
            {
                SimSpeed.TogglePause();
                _loop.OnSpeedChanged();
            }
            Tooltip(hold, HudTips.Hold(paused));
            x += btn + 4f;

            var slower = new Rect(x, r.y, btn, h);
            if (SpeedButton(slower, null, "−", false, !SimSpeed.IsSlowest))
            {
                SimSpeed.Slower();
                _loop.OnSpeedChanged();
            }
            Tooltip(slower, HudTips.Slower());
            x += btn + 2f;

            var readout = new Rect(x, r.y, r.xMax - x - btn - 2f, h);
            HudSkin.Well(readout);
            var prev = _numberSmall.alignment;
            var prevColor = _numberSmall.normal.textColor;
            _numberSmall.alignment = TextAnchor.MiddleCenter;
            _numberSmall.normal.textColor = paused ? Alarm : TextPrimary;
            GUI.Label(readout, paused ? Loc.S("HOLD") : SimSpeed.Label, _numberSmall);
            _numberSmall.alignment = prev;
            _numberSmall.normal.textColor = prevColor;
            Tooltip(readout, HudTips.SpeedReadout());

            var faster = new Rect(readout.xMax + 2f, r.y, btn, h);
            if (SpeedButton(faster, null, "+", false, !SimSpeed.IsFastest || paused))
            {
                SimSpeed.Faster();
                _loop.OnSpeedChanged();
            }
            Tooltip(faster, HudTips.Faster());

            // One pip per running speed; the 1× pip is brass so the default is easy to find again.
            int steps = SimSpeed.Multipliers.Length - 1;
            float gap = 2f;
            float pipW = (r.width - gap * (steps - 1)) / steps;
            float py = r.y + h + 4f;
            for (int i = 1; i <= steps; i++)
            {
                var pip = new Rect(r.x + (i - 1) * (pipW + gap), py, pipW, 3f);
                var hit = new Rect(pip.x, pip.y - 4f, pip.width, pip.height + 8f);
                bool lit = !paused && i <= SimSpeed.Index;
                bool normal = Mathf.Approximately(SimSpeed.Multipliers[i], 1f);
                bool hot = hit.Contains(Event.current.mousePosition);
                Color c = lit ? Accent : normal ? new Color(Gold.r, Gold.g, Gold.b, 0.55f) : new Color(1f, 1f, 1f, hot ? 0.30f : 0.12f);
                HudSkin.Pill(pip, c);
                if (GUI.Button(hit, GUIContent.none, GUIStyle.none))
                {
                    SimSpeed.Set(i);
                    _loop.OnSpeedChanged();
                }
                Tooltip(hit, HudTips.SpeedPip(SimSpeed.LabelFor(i)));
            }
        }

        /// <summary>Small square button for the speed control: icon or glyph, dimmed when it can't act.</summary>
        private bool SpeedButton(Rect r, Texture2D icon, string glyph, bool on, bool enabled)
        {
            bool hot = enabled && r.Contains(Event.current.mousePosition);
            var prev = GUI.color;
            if (!enabled) GUI.color = new Color(prev.r, prev.g, prev.b, prev.a * 0.4f);
            HudSkin.DrawPlate(r, on ? HudSkin.Plate.Primary : HudSkin.Plate.Quiet, hot);
            Color tint = on ? Ink : hot ? Color.white : TextPrimary;
            if (icon != null)
                HudSkin.Icon(HudSkin.Expand(r, -5f), icon, tint);
            else
            {
                var st = _tooltipStyle;
                var c = st.normal.textColor;
                int size = st.fontSize;
                st.normal.textColor = tint;
                st.fontSize = 15; // a glyph on a fixed 22 px button, not text: keep it out of TEXT SIZE
                GUI.Label(new Rect(r.x, r.y - 1f, r.width, r.height), glyph, st);
                st.normal.textColor = c;
                st.fontSize = size;
            }
            GUI.color = prev;
            return GUI.Button(r, GUIContent.none, GUIStyle.none) && enabled;
        }

        private void Bar(Rect r, string label, float t01, Color fill)
        {
            float labelW = W(50f);
            float valueW = W(34f);
            FitLabel(new Rect(r.x, r.y, labelW, r.height), label, _caps);
            var track = new Rect(r.x + labelW, r.y + r.height * 0.5f - 2.5f, Mathf.Max(10f, r.width - labelW - valueW), 5f);
            Meter(track, t01, fill);
            GUI.Label(new Rect(r.xMax - valueW, r.y, valueW, r.height), $"{Mathf.Clamp01(t01) * 100f:F0}%", _microRight);
        }

        private void CheckBox(Rect r, bool done)
        {
            Vector2 c = r.center;
            float s = Mathf.Max(r.width, r.height) + 3f;
            var box = new Rect(c.x - s * 0.5f, c.y - s * 0.5f, s, s);
            if (done)
            {
                HudSkin.Dot(c, s, Gold);
                HudSkin.Icon(HudSkin.Expand(box, -s * 0.2f), UiIcons.Get(IconId.Check), Ink);
            }
            else
            {
                HudSkin.Ring(box, new Color(1f, 1f, 1f, 0.38f));
            }
        }

        /// <summary>Offer a tooltip for <paramref name="anchor"/>; the HUD tooltip (HudTooltip) draws it last, above every panel.</summary>
        private void Tooltip(Rect anchor, HudTip tip) => _tips.Offer(anchor, tip);

        /// <summary>
        /// A one-line label that never clips mid-glyph: too long for <paramref name="r"/>, it ends in "…"
        /// and the full text becomes the tooltip (long French or a large TEXT SIZE).
        /// </summary>
        private static string Lines(string a, string b) =>
            string.IsNullOrEmpty(a) ? b : string.IsNullOrEmpty(b) ? a : a + "\n" + b;

        private void FitLabel(Rect r, string text, GUIStyle style, HudTip? tip = null)
        {
            if (string.IsNullOrEmpty(text)) return;
            string shown = FitChipText(style, new[] { text }, r.width);
            GUI.Label(r, shown, style);
            if (tip.HasValue) Tooltip(r, tip.Value);
            else if (shown != text) Tooltip(r, new HudTip(null, text));
        }

        private void EnsureStyles()
        {
            if (_stylesReady && _chipLabel != null && _titleWord != null && _stylesVersion == HudTextScale.Version) return;
            _stylesReady = true;
            _stylesVersion = HudTextScale.Version; // TEXT SIZE changed: every style is rebuilt at the new size

            Font display = HudSkin.Display;
            Font numeric = HudSkin.Numeric;
            Font body = HudSkin.Body;

            _brand = Label(display, 17, FontStyle.Bold, TextPrimary, TextAnchor.MiddleLeft);
            _section = Label(display, 11, FontStyle.Bold, Gold, TextAnchor.MiddleLeft);
            _body = Label(body, 12, FontStyle.Normal, TextPrimary, TextAnchor.MiddleLeft);
            _bodyWrap = null; // rebuilt from _body on next use
            _muted = Label(body, 11, FontStyle.Normal, TextMuted, TextAnchor.MiddleLeft);
            _micro = Label(body, 10, FontStyle.Normal, TextMuted, TextAnchor.MiddleLeft);
            _microRight = Label(body, 10, FontStyle.Normal, TextMuted, TextAnchor.MiddleRight);
            _microAlarm = Label(display, 10, FontStyle.Bold, Accent, TextAnchor.MiddleRight);
            _value = Label(body, 13, FontStyle.Bold, TextPrimary, TextAnchor.MiddleLeft);
            _pill = Label(display, 10, FontStyle.Bold, Ink, TextAnchor.MiddleCenter);
            _banner = Label(display, 24, FontStyle.Bold, TextPrimary, TextAnchor.MiddleLeft);
            _action = Label(body, 12, FontStyle.Normal, TextPrimary, TextAnchor.MiddleLeft);
            _wrap = Label(body, 11, FontStyle.Normal, TextMuted, TextAnchor.UpperLeft);
            _wrap.wordWrap = true;
            _onText = Label(body, 11, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
            _chipLabel = Label(display, 10, FontStyle.Bold, TextMuted, TextAnchor.MiddleLeft);
            _chipLabel.clipping = TextClipping.Clip;
            _titleWord = Label(display, 44, FontStyle.Bold, TextPrimary, TextAnchor.MiddleLeft);
            _titleWord.clipping = TextClipping.Overflow;
            _planetName = Label(display, 12, FontStyle.Bold, TextPrimary, TextAnchor.MiddleCenter);
            _planetName.clipping = TextClipping.Overflow;
            _planetTag = Label(display, 9, FontStyle.Bold, TextMuted, TextAnchor.MiddleCenter);
            _planetTag.clipping = TextClipping.Overflow;
            _treasury = Label(numeric, 25, FontStyle.Bold, HudSkin.GoldBright, TextAnchor.MiddleCenter);
            _treasury.fontSize = 25; // already display-size; it has to stay inside the baked crest plate
            _treasuryRate = Label(display, 10, FontStyle.Bold, Good, TextAnchor.MiddleCenter);
            _logLine = Label(body, 12, FontStyle.Normal, TextPrimary, TextAnchor.MiddleCenter);
            _logLine.clipping = TextClipping.Overflow;
            _microCenter = Label(body, 10, FontStyle.Normal, TextMuted, TextAnchor.MiddleCenter);
            _caps = Label(display, 10, FontStyle.Bold, TextMuted, TextAnchor.MiddleLeft);
            _capsCenter = Label(display, 10, FontStyle.Bold, TextMuted, TextAnchor.MiddleCenter);
            _number = Label(numeric, 17, FontStyle.Bold, TextPrimary, TextAnchor.MiddleLeft);
            _numberSmall = Label(numeric, 12, FontStyle.Bold, TextPrimary, TextAnchor.MiddleLeft);
            _badge = Label(display, 9, FontStyle.Bold, TextMuted, TextAnchor.MiddleCenter);
            _badge.clipping = TextClipping.Overflow;
            // Key letter tucked under a 48 px verb icon: it cannot grow without hitting the icon. The
            // tooltip names the key at the player's TEXT SIZE.
            _badge.fontSize = Mathf.Min(_badge.fontSize, 10);
            _tooltipStyle = Label(display, 11, FontStyle.Bold, TextPrimary, TextAnchor.MiddleCenter);
            _tagline = Label(body, 13, FontStyle.Normal, TextMuted, TextAnchor.MiddleLeft);
            _chatTitle = Label(display, 16, FontStyle.Bold, TextPrimary, TextAnchor.MiddleLeft);
            _chatName = Label(display, 9, FontStyle.Bold, Gold, TextAnchor.MiddleLeft);
            _chatText = Label(body, 12, FontStyle.Normal, TextPrimary, TextAnchor.UpperLeft);
            _chatText.wordWrap = true;
            _chatHint = Label(body, 12, FontStyle.Italic, TextMuted, TextAnchor.MiddleCenter);
            _chatHint.wordWrap = true;
            _chatPlaceholder = Label(body, 12, FontStyle.Normal, HudSkin.TextFaint, TextAnchor.MiddleLeft);
            _bubbleText = Label(body, 12, FontStyle.Normal, TextPrimary, TextAnchor.UpperLeft);
            _bubbleText.wordWrap = true;

            _chipOff = HudSkin.Button(HudSkin.Plate.Quiet, display, 11, FontStyle.Bold, TextAnchor.MiddleCenter, 4, TextPrimary);
            _chipOn = HudSkin.Button(HudSkin.Plate.Primary, display, 11, FontStyle.Bold, TextAnchor.MiddleCenter, 4, Ink);
            _rowOff = HudSkin.Button(HudSkin.Plate.Row, body, 11, FontStyle.Normal, TextAnchor.MiddleLeft, 8, TextPrimary);
            _rowOn = HudSkin.Button(HudSkin.Plate.RowOn, body, 11, FontStyle.Bold, TextAnchor.MiddleLeft, 8, Color.white);
            _titleButton = Label(display, 13, FontStyle.Bold, TextPrimary, TextAnchor.MiddleLeft);
            _titleButton.padding = new RectOffset(18, 8, 0, 0);
            _titleButton.hover.textColor = Color.white;
            _titleButtonOn = Label(display, 13, FontStyle.Bold, Ink, TextAnchor.MiddleLeft);
            _titleButtonOn.padding = new RectOffset(18, 8, 0, 0);
            _titleButtonOn.hover.textColor = Ink;
        }

        private static GUIStyle Label(Font font, int size, FontStyle fs, Color color, TextAnchor anchor) =>
            HudSkin.Text(font, size, fs, color, anchor);

        // ---- frame --------------------------------------------------------------

        /// <summary>Store screenshots without any HUD.</summary>
        public static bool HideForCapture;

        private void OnGUI()
        {
            if (_loop == null || HideForCapture) return;
            if (CameraBeatDirector.HidesHud && _loop.Screen == DemoScreen.Playing)
            {
                PointerBlocksWorld = true; // the camera beat draws its own letterbox; nothing here is clickable
                _tips.Reset(); // no tooltip carries over the beat
                return;
            }
            EnsureStyles();
            GUI.skin = HudSkin.Skin();
            HandleDebugHopKeys();
            Event guiEvent = Event.current;
            bool leftMouseUp = guiEvent != null && guiEvent.type == EventType.MouseUp && guiEvent.button == 0;
            bool repaint = guiEvent != null && guiEvent.type == EventType.Repaint;
            if (repaint) _hoverSeenThisRepaint = false;
            _hitRects.Clear();
            _ordersFieldDrawn = false;
            _chatFieldDrawn = false;

            DemoSettings.RefreshHudScale(Screen.width, Screen.height);
            float s = DemoSettings.HudScale;
            _hudScale = s;
            var prevMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s, s, 1f));
            _sw = Screen.width / s;
            _sh = Screen.height / s;
            _tips.Begin(HudPointer.ScreenToGui(Input.mousePosition, Screen.height, s));

            // Campaign victory replaces the console so pause and dock buttons cannot eat the click.
            // Drawn in the scaled GUI space, so the UI Scale setting sizes the panel.
            if (_loop.Screen != DemoScreen.Title &&
                _loop.Screen != DemoScreen.Settings &&
                TryDrawCampaignVictory())
            {
                // The panel is the whole frame. Play and pause chrome stay undrawn.
            }
            else if (_loop.Screen == DemoScreen.Title)
            {
                DrawTitle();
            }
            else if (_loop.Screen == DemoScreen.Settings)
            {
                DrawSettings();
            }
            else if (_loop.Screen == DemoScreen.Paused)
            {
                DrawPause();
            }
            else
            {
                _playTop = M;
                RightColumnBottom = M;
                DrawMissionPanel();
                DrawTechPanel();
                DrawDropManifest();
                DrawMinimap();
                DrawSpeechBubble();

                float dockTop = HudLayout.DockTop(_sh);
                float stackTop = dockTop - CrestRise;
                _contentBottom = stackTop;
                DrawToolPopups();
                DrawBottomDock(dockTop);
                DrawLogFeed(stackTop);

                DrawConstructionPanel();
                DrawInspectPanel();
                DrawRoster();
                DrawTutorial();
                DrawToast();
                DrawCutsceneModal();
                DrawWinBanner();
                DrawFailBanner();
            }

            // One tooltip for every screen (title, settings, pause, play), above everything drawn.
            _tips.Draw(_sw, _sh, StillCaptureHold.Active);

            Vector2 imguiMouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y) / s;
            PointerBlocksWorld = _loop.Screen != DemoScreen.Playing;
            if (!PointerBlocksWorld)
            {
                for (int i = 0; i < _hitRects.Count; i++)
                {
                    if (_hitRects[i].Contains(imguiMouse))
                    {
                        PointerBlocksWorld = true;
                        break;
                    }
                }
            }

            GUI.matrix = prevMatrix;
            SyncOrdersFocus();

            // A control used the release, so something was pressed. Buttons whose action has its
            // own UI sound (restart, travel) suppress this inside DemoAudio.
            if (leftMouseUp && guiEvent.type == EventType.Used)
                DemoAudio.PlayUiClick();
            if (repaint && !_hoverSeenThisRepaint)
                _hoverActive = false;
        }

        /// <summary>Soft glint when the pointer first enters a button; silent while it stays.</summary>
        private void NoteHover(Rect r, bool hot)
        {
            if (!hot || Event.current == null || Event.current.type != EventType.Repaint) return;
            _hoverSeenThisRepaint = true;
            if (_hoverActive && _hoverRect == r) return;
            _hoverActive = true;
            _hoverRect = r;
            DemoAudio.PlayUiHover();
        }

        /// <summary>
        /// While the flag-orders box has focus, gameplay hotkeys stand down (typing "stay away"
        /// must not pan the camera or open menus). Focus drops when the box is hidden.
        /// </summary>
        private void SyncOrdersFocus()
        {
            string name = GUI.GetNameOfFocusedControl();
            bool orders = name == OrdersControl;
            bool chat = name == ChatControl;
            if ((orders && !_ordersFieldDrawn) || (chat && !_chatFieldDrawn))
            {
                GUI.FocusControl(null);
                orders = chat = false;
            }
            // Waiting for a new key binding counts as typing: the key is for the binding, not the game.
            if (_rebinding.HasValue && _loop.Screen != DemoScreen.Settings)
                _rebinding = null;
            InputBindings.TextEntryActive = orders || chat || _rebinding.HasValue;
        }

        private static string FormatRate(int rate)
        {
            if (rate == 0) return null;
            return rate > 0 ? $"+{rate}" : rate.ToString();
        }

        /// <summary>
        /// One stockpile readout: icon, small-caps label with its rate underneath, and the number
        /// right-aligned where the eye lands. Alarm pulses a red halo instead of recolouring the chip.
        /// </summary>
        private void ResourceChip(Rect r, string key, string label, int amount, bool alarm = false, string rate = null)
        {
            if (alarm)
            {
                float pulse = 0.25f + 0.35f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f));
                HudSkin.Glow(r, new Color(Alarm.r, Alarm.g, Alarm.b, pulse), 0.55f);
            }
            HudSkin.Well(r);
            if (alarm) HudSkin.Pill(new Rect(r.x + 1f, r.y + 5f, 2f, r.height - 10f), Alarm);

            // A drawn glyph reads instantly where a coloured square only reads once learned.
            var icon = new Rect(r.x + 6f, r.y + (r.height - 18f) * 0.5f, 18f, 18f);
            Texture2D tex = ChipIcon(key);
            if (tex != null) HudSkin.Icon(icon, tex, Color.white);
            else HudSkin.Dot(icon.center, 10f, ChipSwatch(key));

            HudLayout.ChipText(r, out var labelRect, out var numberRect);
            var prevLabel = _chipLabel.normal.textColor;
            _chipLabel.normal.textColor = alarm ? Alarm : TextMuted;
            // Rects and CalcSize share the GUI.matrix space, so scale = 1.
            GUI.Label(labelRect, FitChipText(_chipLabel, new[] { label }, labelRect.width), _chipLabel);
            _chipLabel.normal.textColor = prevLabel;
            Tooltip(r, HudTips.Chip(key));
            if (!string.IsNullOrEmpty(rate))
            {
                var rateRect = HudLayout.ChipRate(r, labelRect);
                // The amount is right-aligned and usually 1-3 digits: let the rate use the slack.
                _chipMeasure.text = amount.ToString();
                float amountW = Mathf.Min(numberRect.width, _number.CalcSize(_chipMeasure).x);
                rateRect.width = Mathf.Max(rateRect.width, numberRect.xMax - amountW - HudLayout.TextGap - rateRect.x);
                GUI.Label(rateRect, FitChipText(_micro, HudLayout.ChipRateVariants(rate), rateRect.width), _micro);
            }

            var prevAnchor = _number.alignment;
            _number.alignment = TextAnchor.MiddleRight;
            GUI.Label(numberRect, FitChipText(_number, new[] { amount.ToString() }, numberRect.width), _number);
            _number.alignment = prevAnchor;
        }

        private static readonly GUIContent _chipMeasure = new GUIContent();

        private static string FitChipText(GUIStyle style, string[] variants, float maxWidth)
        {
            return HudLayout.FitText(variants, t => { _chipMeasure.text = t; return style.CalcSize(_chipMeasure).x; }, maxWidth);
        }

        internal const string ChargersLabel = "CHARGERS";

        /// <summary>Which icon a stockpile chip label gets; null means a coloured dot.</summary>
        internal static IconId? ChipIconFor(string label)
        {
            if (label.StartsWith("REG")) return IconId.Regolith;
            if (label.StartsWith("ICE")) return IconId.Ice;
            if (label.StartsWith("EU")) return IconId.Coin;
            if (label.StartsWith("PWR")) return IconId.Power;
            if (label.StartsWith("BEDS")) return IconId.Beds;
            if (label.StartsWith("HEROES")) return IconId.Robot;
            if (label.StartsWith(ChargersLabel)) return IconId.House;
            if (label.StartsWith("HAUL")) return IconId.Bag;
            if (label.StartsWith("BOTS") || label.StartsWith("HALTED")) return IconId.Hammer;
            return null;
        }

        private static Texture2D ChipIcon(string label)
        {
            var id = ChipIconFor(label);
            return id.HasValue ? UiIcons.Get(id.Value) : null;
        }

        private static Color ChipSwatch(string label)
        {
            if (label.StartsWith("REG")) return new Color(0.62f, 0.38f, 0.18f);
            if (label.StartsWith("ICE")) return new Color(0.35f, 0.78f, 0.92f);
            if (label.StartsWith("EU")) return new Color(0.72f, 0.74f, 0.78f);
            if (label.StartsWith("PWR")) return new Color(0.92f, 0.78f, 0.22f);
            if (label.StartsWith("BEDS")) return new Color(0.96f, 0.42f, 0.08f);
            if (label.StartsWith("DENS")) return new Color(0.86f, 0.28f, 0.22f);
            return Gold;
        }

        private float DockWidth() => HudLayout.DockWidth(_sw);

        /// <summary>Centred, but never under the minimap on narrow screens.</summary>
        private float DockLeft() => HudLayout.DockLeft(_sw);

        /// <summary>
        /// Bottom-centre console: stockpile on the left, Overseer verbs in the middle, clock and
        /// threat on the right, and the treasury crest — the number this game is about — on top.
        /// </summary>
        private void DrawBottomDock(float top)
        {
            float w = DockWidth();
            var rect = new Rect(DockLeft(), top, w, HudLayout.DockHeight);
            var c = Panel(rect, null, false);

            // Column widths grow with TEXT SIZE when the screen has room (HudLayout.DockColumns).
            HudLayout.DockColumns(c.width, out float stockW, out float right);
            DrawStockpile(new Rect(c.x, c.y, stockW, c.height));

            var divider = new Color(Gold.r, Gold.g, Gold.b, 0.45f);
            HudSkin.RuleV(new Rect(c.x + stockW + 10f, rect.y + 6f, 1f, rect.height - 12f), divider);
            HudSkin.RuleV(new Rect(c.xMax - right - 10f, rect.y + 6f, 1f, rect.height - 12f), divider);

            // Verbs: build, bounty, orbital, research, campus, party, menu. Never unit orders.
            float sq = 48f;
            float gap = 6f;
            float verbsW = sq * 7f + gap * 6f;
            float midL = c.x + stockW + 18f;
            float midR = c.xMax - right - 18f;
            float x = midL + Mathf.Max(0f, (midR - midL - verbsW) * 0.5f);
            // Verbs sit centred over the levy line in whatever height the text left them.
            float vy = c.y + 3f + Mathf.Max(0f, (HudTextScale.Grow(HudLayout.DockTextRows) - HudTextScale.Grow(14f)) * 0.5f);
            if (SquareAction(new Rect(x, vy, sq, sq), IconId.GlyphBuild, InputBindings.Hint(GameAction.ToolBuild), _loop.ActiveTool == OverseerTool.Build, HudTips.Build()))
                _loop.ToggleTool(OverseerTool.Build);
            x += sq + gap;
            if (SquareAction(new Rect(x, vy, sq, sq), IconId.GlyphFlag, InputBindings.Hint(GameAction.ToolFlag), _loop.ActiveTool == OverseerTool.Flag, HudTips.Flag()))
                _loop.ToggleTool(OverseerTool.Flag);
            x += sq + gap;
            if (SquareAction(new Rect(x, vy, sq, sq), IconId.Sun, InputBindings.Hint(GameAction.ToolOrbital), _loop.ActiveTool == OverseerTool.Orbital, HudTips.Orbital()))
                _loop.ToggleTool(OverseerTool.Orbital);
            x += sq + gap;
            if (SquareAction(new Rect(x, vy, sq, sq), IconId.GlyphTech, InputBindings.Hint(GameAction.ToolTech), _techOpen, HudTips.Research()))
                ToggleTechPanel();
            x += sq + gap;
            if (SquareAction(new Rect(x, vy, sq, sq), IconId.Camp, "A/B", false, HudTips.SwitchYard()))
                _loop.FocusCampus(1 - _loop.FocusedCampus);
            x += sq + gap;
            int partyCount = _loop.Parties != null ? _loop.Parties.Count : 0;
            if (SquareAction(new Rect(x, vy, sq, sq), IconId.Party, InputBindings.Hint(GameAction.ToolParty), partyCount > 0, HudTips.Party()))
                _loop.FormParty();
            x += sq + gap;
            if (SquareAction(new Rect(x, vy, sq, sq), IconId.Menu, "Esc", false, HudTips.Menu()))
                _loop.TogglePause();

            DrawLevyLine(new Rect(midL, vy + sq + 7f, midR - midL, L(14f)));
            DrawClockAndThreat(new Rect(c.xMax - right, c.y, right, c.height));
            DrawTreasuryCrest(HudLayout.Crest(_sw, _sh));
        }

        /// <summary>Treasury (CRED is Majesty gold), live income, and what is locked in bounties.</summary>
        private void DrawTreasuryCrest(Rect r)
        {
            _hitRects.Add(r);
            int gold = _loop.Resources != null ? _loop.Resources.Get(ResourceId.Metals) : 0;
            int escrow = _loop.BountyEscrowTotal;
            bool thin = _loop.PayrollThin;
            if (thin)
            {
                float pulse = 0.3f + 0.35f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f));
                HudSkin.Glow(new Rect(r.x + 20f, r.y + 6f, r.width - 40f, r.height - 12f),
                    new Color(Alarm.r, Alarm.g, Alarm.b, pulse), 0.9f);
            }
            HudSkin.Tex(HudSkin.Expand(r, HudSkin.CrestPad), HudSkin.CrestPlate, Color.white);

            var coin = UiIcons.Get(IconId.Coin);
            float numW = _treasury.CalcSize(new GUIContent($"{gold:N0}")).x;
            float groupW = 26f + numW;
            float gx = r.x + (r.width - groupW) * 0.5f;
            HudSkin.Icon(new Rect(gx, r.y + 9f, 21f, 21f), coin, Color.white);
            HudSkin.ShadowLabel(new Rect(gx + 26f, r.y + 5f, numW + 4f, 28f), $"{gold:N0}", _treasury, 0.6f);

            float perMin = _loop.TreasuryIncomePerMin;
            string rate = HudLayout.TreasuryLine(perMin, escrow, thin);
            var prev = _treasuryRate.normal.textColor;
            _treasuryRate.normal.textColor = thin ? Alarm : (perMin >= 1f ? Good : TextMuted);
            var rateRect = new Rect(r.x + 26f, r.y + 32f, r.width - 52f, 13f);
            // Larger text: the line may run onto the plate's lower rim, never past it.
            rateRect.height = Mathf.Max(13f, Mathf.Min(L(13f), r.yMax + HudSkin.CrestPad - 2f - rateRect.y));
            FitLabel(rateRect, rate, _treasuryRate, HudTips.Treasury());
            _treasuryRate.normal.textColor = prev;
            Tooltip(new Rect(r.x + 20f, r.y, r.width - 40f, 32f), HudTips.Treasury());
        }

        /// <summary>The colony at a glance: heroes, tax houses, collectors on their rounds, dens left.</summary>
        private void DrawStockpile(Rect r)
        {
            FitLabel(new Rect(r.x, r.y - 2f, r.width, L(14f)),
                Loc.F("{0}  ·  COLONY", _loop.BodyProfile != null ? _loop.BodyProfile.DisplayNameText.ToUpperInvariant() : Loc.S("COLONY")), _section,
                HudTips.Colony());

            var set = _loop.Settlement;
            var village = _loop.Village;
            int heroes = _loop.RobotCount;
            int houses = set != null ? set.Habs : 0;
            int collectors = village != null ? village.Collectors.Count : 0;
            int bags = village != null ? village.Collectors.GoldInTransit : 0;
            int solar = set != null ? set.SolarFarms : 0;
            int replacing = village != null ? village.Collectors.Replacing : 0;
            bool building = village != null && village.VillageBuilding;
            bool halted = village != null && village.VillageHalted;
            int pct = village != null ? Mathf.RoundToInt(village.VillageProgress01 * 100f) : 0;

            float top = r.y + L(17f);
            float cw = HudLayout.StockpileChipWidth(r.width);
            float ch = HudLayout.ChipHeight;
            ResourceChip(new Rect(r.x, top, cw, ch), "HEROES", Loc.S("HEROES"), heroes, heroes <= 0);
            ResourceChip(new Rect(r.x + cw + HudLayout.StockpileChipGap, top, cw, ch), ChargersLabel, Loc.S("CHARGERS"), houses, false,
                HudLayout.ChargersRate(set != null ? set.Drones : 0, set != null ? set.DroneCapacity : 0, solar));
            ResourceChip(new Rect(r.x, top + ch + 4f, cw, ch), "HAUL", Loc.S("HAUL"), collectors, collectors <= 0 || replacing > 0,
                replacing > 0 ? Loc.F("-{0} replacing", replacing) : (bags > 0 ? Loc.F("{0} carried", bags) : null));
            ResourceChip(new Rect(r.x + cw + HudLayout.StockpileChipGap, top + ch + 4f, cw, ch), halted ? "HALTED" : "BOTS", halted ? Loc.S("HALTED") : Loc.S("BOTS"),
                building ? pct : 0, halted, building ? Loc.S("% built") : Loc.S("idle"));
        }

        /// <summary>Where the gold is on its way home: building tills, collector bags, daily tax.</summary>
        private void DrawLevyLine(Rect r)
        {
            var village = _loop.Village;
            int tills = _loop.SittingLevy;
            int bags = village != null ? village.Collectors.GoldInTransit : 0;
            int collectors = village != null ? village.Collectors.Count : 0;
            int daily = village != null ? village.DailyBuildingTax() : 0;
            // r and CalcSize are both in GUI.matrix space (the HUD scale is applied by the matrix), so scale = 1.
            var content = new GUIContent();
            string line = HudLayout.LevyLineFit(tills, collectors, bags, daily,
                t => { content.text = t; return _microCenter.CalcSize(content).x; }, r.width);
            GUI.Label(r, line, _microCenter);
            Tooltip(r, HudTips.Levy());
        }

        private void DrawClockAndThreat(Rect r)
        {
            int sol = 1 + Mathf.FloorToInt((_loop.Mission != null ? _loop.Mission.MissionElapsed : 0f) / MajestyEconomy.DaySeconds);
            HudLayout.ClockHeader(r, out var solRect, out var tagRect, out var speedRect);
            HudSkin.Icon(new Rect(r.x, r.y, 13f, 13f), UiIcons.Get(IconId.Sun), Gold);
            FitLabel(solRect, Loc.F("SOL {0}", sol), _section, HudTips.Sol());
            FitLabel(tagRect, ReplayRules.HudTag, _micro, HudTips.Sol());
            DrawSpeedControl(speedRect);

            // Rows sit below the speed control (28 px) and grow with TEXT SIZE.
            float top = Mathf.Max(r.y + 30f, tagRect.yMax + 3f);
            int posted = _loop.Flags != null && _loop.Flags.Flags != null ? _loop.Flags.Flags.Count : 0;
            var bountyRow = new Rect(r.x, top, r.width, L(13f));
            HudLayout.StatLine(bountyRow, out var bountyLabel, out var bountyValue);
            FitLabel(bountyLabel, Loc.S("BOUNTIES"), _caps);
            FitLabel(bountyValue, posted <= 0 ? Loc.S("none posted") : Loc.F("{0} posted", posted), _microRight);
            Tooltip(bountyRow, HudTips.Bounties());

            float threat = _loop.FocusedLocalThreat;
            var threatRow = new Rect(r.x, bountyRow.y + L(14f), r.width, L(13f));
            HudLayout.StatLine(threatRow, out var threatLabel, out var threatValue);
            FitLabel(threatLabel, Loc.S("THREAT"), _caps);
            var prev = _microRight.normal.textColor;
            _microRight.normal.textColor = threat > 0.6f ? Alarm : threat > 0.3f ? Accent : TextMuted;
            GUI.Label(threatValue, $"{threat * 100f:F0}%", _microRight);
            _microRight.normal.textColor = prev;
            float segY = threatRow.y + L(15f);
            DrawSegments(new Rect(r.x, segY, r.width - 2f, 4f), threat, 12);
            Tooltip(new Rect(r.x, threatRow.y, r.width, segY + 4f - threatRow.y), HudTips.Threat());
            var rating = _loop.CurrentRating;
            var ratingRect = new Rect(r.x, segY + 6f, r.width, L(12f));
            FitLabel(ratingRect, rating.Summary, _micro, HudTips.Rating(rating.Summary));
        }

        /// <summary>Segmented gauge that warms from brass through orange to red as it fills.</summary>
        private static void DrawSegments(Rect r, float t01, int count)
        {
            float gap = 2f;
            float w = (r.width - gap * (count - 1)) / count;
            int lit = Mathf.CeilToInt(Mathf.Clamp01(t01) * count - 0.001f);
            for (int i = 0; i < count; i++)
            {
                float k = count > 1 ? i / (count - 1f) : 0f;
                Color on = k < 0.5f ? Color.Lerp(Gold, Accent, k * 2f) : Color.Lerp(Accent, Alarm, (k - 0.5f) * 2f);
                HudSkin.Pill(new Rect(r.x + i * (w + gap), r.y, w, r.height),
                    i < lit ? on : new Color(1f, 1f, 1f, 0.10f));
            }
        }

        /// <summary>Last Overseer log lines, floating over the world just above the console like subtitles.</summary>
        private void DrawLogFeed(float stackTop)
        {
            if (_loop.ActiveTool != OverseerTool.None) return;
            if (_loop.SelectedStructure != null) return;
            if (_loop.SelectedAgents != null && _loop.SelectedAgents.Count > 0) return;
            var log = _loop.Log;
            if (log == null || log.Entries.Count == 0) return;
            int n = Mathf.Min(3, log.Entries.Count);
            float w = DockWidth();
            float x = DockLeft();
            for (int i = 0; i < n; i++)
            {
                var e = log.Entries[log.Entries.Count - n + i];
                float y = stackTop - 12f - (n - i) * L(18f);
                var rr = new Rect(x, y, w, L(18f));
                float a = 0.45f + 0.55f * (i + 1f) / n;
                float textW = Mathf.Min(w, _logLine.CalcSize(new GUIContent(e.Line)).x);
                HudSkin.Band(new Rect(rr.center.x - textW * 0.5f - 60f, rr.y - 1f, textW + 120f, rr.height + 2f), 0.42f * a);
                var prev = _logLine.normal.textColor;
                _logLine.normal.textColor = new Color(TextPrimary.r, TextPrimary.g, TextPrimary.b, a);
                HudSkin.ShadowLabel(rr, e.Line, _logLine, 0.7f);
                _logLine.normal.textColor = prev;
            }
        }

        private bool SquareAction(Rect r, IconId icon, string hotkey, bool on, HudTip tip)
        {
            bool hot = r.Contains(Event.current.mousePosition);
            NoteHover(r, hot);
            if (on) HudSkin.Glow(r, new Color(Accent.r, Accent.g, Accent.b, 0.45f), 0.6f);
            HudSkin.DrawPlate(r, on ? HudSkin.Plate.Primary : HudSkin.Plate.Quiet, hot);
            bool hit = GUI.Button(r, GUIContent.none, GUIStyle.none);

            Color tint = on ? Ink : hot ? Color.white : new Color(0.88f, 0.86f, 0.82f);
            HudSkin.Icon(new Rect(r.center.x - 12f, r.center.y - 14f, 24f, 24f), UiIcons.Get(icon), tint);
            var prev = _badge.normal.textColor;
            _badge.normal.textColor = on ? new Color(Ink.r, Ink.g, Ink.b, 0.75f) : hot ? Gold : HudSkin.TextFaint;
            GUI.Label(new Rect(r.x + 2f, r.yMax - 14f, r.width - 4f, 12f), hotkey, _badge);
            _badge.normal.textColor = prev;
            Tooltip(r, tip);
            return hit;
        }

        public bool TechPanelOpen => _techOpen;

        public void CloseTechPanel() => _techOpen = false;

        public void ToggleTechPanel()
        {
            _techOpen = !_techOpen;
            if (_techOpen)
                _loop?.NotifyTechOpened();
        }

        public void MinimizeActiveMenu()
        {
            if (_loop == null) return;
            if (_loop.ActiveTool == OverseerTool.Build) _buildMinimized = true;
            if (_loop.ActiveTool == OverseerTool.Flag) _flagMinimized = true;
            if (_loop.ActiveTool == OverseerTool.Orbital) _orbitalMinimized = true;
        }

        public void ExpandMenu(OverseerTool tool)
        {
            if (tool == OverseerTool.Build) _buildMinimized = false;
            if (tool == OverseerTool.Flag) _flagMinimized = false;
            if (tool == OverseerTool.Orbital) _orbitalMinimized = false;
        }

        public void ClearMinimizedMenus()
        {
            _buildMinimized = false;
            _flagMinimized = false;
            _orbitalMinimized = false;
        }

        /// <summary>B/G while a catalog is collapsed re-opens it instead of closing the tool.</summary>
        public bool TryExpandMinimizedMenu(OverseerTool tool)
        {
            if (tool == OverseerTool.Build && _buildMinimized)
            {
                _buildMinimized = false;
                return true;
            }

            if (tool == OverseerTool.Flag && _flagMinimized)
            {
                _flagMinimized = false;
                return true;
            }

            if (tool == OverseerTool.Orbital && _orbitalMinimized)
            {
                _orbitalMinimized = false;
                return true;
            }

            return false;
        }

        /// <summary>
        /// The body's departure tech stays above the scroll. Lunar Rocket is deep in the tree,
        /// so a player who only sees the first rows never finds the launch chain.
        /// </summary>
        private float DrawLaunchTechPin(Rect c, float y, ResearchManager research)
        {
            TechId id = research.LaunchTechId(_loop.ActiveBody);
            if (id == TechId.None || !DemoSlice.ShowTech(id)) return y;
            var def = TechCatalog.Get(id);
            if (def == null) return y;

            bool done = research.IsUnlocked(id);
            bool can = research.CanSelect(id);
            bool active = research.ActiveTech == id;
            var row = new Rect(c.x, y, c.width, L(36f));
            if (GUI.Button(row, GUIContent.none, active ? _rowOn : _rowOff) && can)
                research.TrySelect(id);

            string mark = done ? Loc.S("DONE") : active ? "…" : can ? Loc.S("GO") : "—";
            float markW = W(40f);
            FitLabel(new Rect(row.x + 6f, row.y + 2f, row.width - markW - 12f, L(16f)), def.DisplayNameText, _value);
            var prev = _microRight.normal.textColor;
            _microRight.normal.textColor = done ? Good : can ? TextPrimary : TextMuted;
            GUI.Label(new Rect(row.xMax - markW - 4f, row.y + 2f, markW, L(16f)), mark, _microRight);
            _microRight.normal.textColor = prev;

            string need = research.UnmetPrerequisiteLabel(id);
            string detail = done
                ? Loc.S("Researched. A Landing Pad stages the craft.")
                : string.IsNullOrEmpty(need) ? def.DescriptionText : Loc.F("needs {0}", need);
            FitLabel(new Rect(row.x + 6f, row.y + L(18f), row.width - 12f, L(16f)), detail, _micro);
            Tooltip(row, HudTips.TechEntry(def.DisplayNameText, def.DescriptionText,
                HudTips.TechState(done, active, can, need, def.ScienceCost, research.Progress01(id))));
            return y + L(36f) + 4f;
        }

        private void DrawTechPanel()
        {
            if (!_techOpen) return;
            var research = _loop.Research;
            if (research == null) return;

            float panelW = Mathf.Min(W(360f), _sw - M * 2f);
            float top = Mathf.Max(M + 140f, RightColumnBottom + 10f);
            float panelH = Mathf.Clamp(_sh - M - MapSize - 24f - top, 220f, L(480f));
            var rect = new Rect(_sw - M - panelW, top, panelW, panelH);
            RightColumnBottom = rect.yMax;
            var c = Panel(rect, Loc.F("Research · {0}", InputBindings.Hint(GameAction.ToolTech)), true, HudTips.ResearchPanel());
            float closeW = W(56f);
            var close = new Rect(rect.xMax - closeW - 12f, rect.y + 8f, closeW, L(18f));
            if (GUI.Button(close, Loc.S("CLOSE"), _chipOff))
                _techOpen = false;
            Tooltip(close, HudTips.Close());

            string launch = research.LaunchTechLabel(_loop.ActiveBody);
            FitLabel(new Rect(c.x, c.y, c.width, L(14f)),
                Loc.F("Labs {0} · rate {1:F1}/s · tip {2}", research.LabCount, research.CurrentRate, launch), _micro);
            float y = c.y + L(18f);
            y = DrawLaunchTechPin(c, y, research);

            if (research.ActiveTech != TechId.None)
            {
                var active = TechCatalog.Get(research.ActiveTech);
                string name = active != null ? active.DisplayNameText : research.ActiveTech.ToString();
                float frac = research.ActiveCost > 0f ? research.ActiveProgress / research.ActiveCost : 0f;
                FitLabel(new Rect(c.x, y, c.width, L(14f)), Loc.F("Active: {0}", name), _body);
                y += L(16f);
                Meter(new Rect(c.x, y, c.width, 7f), frac, Accent);
                y += 12f;
                FitLabel(new Rect(c.x, y, c.width, L(13f)),
                    Loc.F("{0:F0}/{1:F0} science", research.ActiveProgress, research.ActiveCost) +
                    (research.LastEvent == "awaiting_stockpile" ? Loc.S(" · need metals/ice") : ""),
                    _micro);
                y += L(18f);
            }
            else
            {
                var rec = research.RecommendedNext();
                var recDef = TechCatalog.Get(rec);
                string bank = research.BankedScience > 0.5f
                    ? Loc.F("Science banked {0:F0}. ", research.BankedScience)
                    : "";
                string tip = recDef != null
                    ? Loc.F("{0}Click a tech to start — next pick {1}.", bank, recDef.DisplayNameText)
                    : Loc.F("{0}Tree complete — launch tech unlocked.", bank);
                y = WrapBlock(c, y, tip) + 2f;
            }

            Fill(new Rect(c.x, y, c.width, 1f), Hairline);
            y += 8f;

            float listH = c.yMax - y;
            var view = new Rect(c.x, y, c.width, listH);
            var techs = TechCatalog.All;
            TechId pinned = research.LaunchTechId(_loop.ActiveBody);
            int visibleTechs = 0;
            for (int i = 0; i < techs.Count; i++)
            {
                if (techs[i].Id == pinned) continue;
                if (DemoSlice.ShowTech(techs[i].Id))
                    visibleTechs++;
            }
            float rowH = L(50f);
            float rowStep = rowH + 4f;
            var content = new Rect(0f, 0f, c.width - 18f, Mathf.Max(rowStep, visibleTechs * rowStep));
            _tips.BeginClip(view);
            _techScroll = GUI.BeginScrollView(view, _techScroll, content);

            float rowY = 0f;
            for (int i = 0; i < techs.Count; i++)
            {
                var t = techs[i];
                if (t.Id == pinned) continue;
                if (!DemoSlice.ShowTech(t.Id)) continue;
                bool done = research.IsUnlocked(t.Id);
                bool can = research.CanSelect(t.Id);
                bool active = research.ActiveTech == t.Id;
                var row = new Rect(0f, rowY, content.width, rowH);

                if (GUI.Button(row, GUIContent.none, active ? _rowOn : _rowOff) && can)
                    research.TrySelect(t.Id);

                string mark = done ? Loc.S("DONE") : active ? "…" : can ? (t.SecretProject ? "★" : Loc.S("GO")) : "—";
                Color markC = done ? Good : active ? Accent : can ? TextPrimary : TextMuted;
                string title = t.SecretProject ? $"★ {t.DisplayNameText}" : t.DisplayNameText;
                float markW = W(40f);
                FitLabel(new Rect(row.x + 6f, row.y + 4f, row.width - markW - 10f, L(16f)), title, _value);
                var prev = _microRight.normal.textColor;
                _microRight.normal.textColor = markC;
                GUI.Label(new Rect(row.xMax - markW - 4f, row.y + 4f, markW, L(16f)), mark, _microRight);
                _microRight.normal.textColor = prev;

                FitLabel(new Rect(row.x + 6f, row.y + L(22f), row.width - 12f, L(24f)), t.DescriptionText, _micro);

                float p = done ? 1f : research.Progress01(t.Id);
                if (!done && p > 0.01f)
                    Meter(new Rect(row.x + 6f, row.yMax - 6f, row.width - 12f, 3f), p, Accent);
                Tooltip(row, HudTips.TechEntry(title, t.DescriptionText,
                    HudTips.TechState(done, active, can, research.UnmetPrerequisiteLabel(t.Id), t.ScienceCost, p)));

                rowY += rowStep;
            }

            GUI.EndScrollView();
            _tips.EndClip();
        }

        private void DrawToolPopups()
        {
            if (_loop.ActiveTool == OverseerTool.Flag)
                DrawFlagPopup();
            else if (_loop.ActiveTool == OverseerTool.Build)
                DrawBuildPopup();
            else if (_loop.ActiveTool == OverseerTool.Orbital)
                DrawOrbitalPopup();
        }

        /// <summary>
        /// Orbital support list (Majesty 2 ruler spells as satellite powers). Each row shows the
        /// base price at an uplink; the armed strip shows the live price under the cursor.
        /// </summary>
        private void DrawOrbitalPopup()
        {
            var orbital = _loop.Orbital;
            var input = _loop.OrbitalInput;
            if (orbital == null || input == null) return;
            var powers = orbital.Tuning.powers;
            if (powers == null) return;

            if (_orbitalMinimized && input.Selected.HasValue &&
                orbital.Tuning.TryGet(input.Selected.Value, out var armed))
            {
                float wait = orbital.ReadyIn(armed.id);
                string hint = wait > 0f
                    ? Loc.F("recharging {0} s", Mathf.CeilToInt(wait))
                    : Loc.F("click the map · {0} EU here (x{1:0} from uplink)", input.CursorCost, input.CursorMultiplier);
                DrawMinimizedToolStrip(OverseerTool.Orbital, 360f,
                    armed.DisplayNameText.ToUpperInvariant(), hint, InputBindings.Hint(GameAction.ToolOrbital));
                return;
            }

            float rowH = L(24f);
            float popupH = L(58f) + powers.Length * (rowH + 4f);
            var rect = HudLayout.ToolPopup(_sw, _sh, W(360f), popupH);
            _contentBottom = rect.y;
            var c = Panel(rect, Loc.S("Orbital support"), true, HudTips.OrbitalCatalog());
            FitLabel(new Rect(c.x, c.y, c.width, L(13f)),
                Loc.S("Pick a power · LMB on the map · cost x1-x15 by distance from an uplink"), _micro);
            float y = c.y + L(17f);

            int treasury = _loop.Resources != null ? _loop.Resources.Get(ResourceId.Metals) : 0;
            for (int i = 0; i < powers.Length; i++)
            {
                var p = powers[i];
                bool locked = !orbital.IsUnlocked(p.id, _loop.OrbitalUnlocked);
                float wait = orbital.ReadyIn(p.id);
                bool on = input.Selected.HasValue && input.Selected.Value == p.id;
                var r = new Rect(c.x, y, c.width, rowH);

                if (GUI.Button(r, GUIContent.none, on ? _rowOn : _rowOff) && !locked)
                {
                    if (p.target == OrbitalTarget.Colony)
                    {
                        _loop.TryCastOrbital(p.id, Vector3.zero);
                    }
                    else
                    {
                        // Arm it, then get out of the way: the next click on the map fires.
                        _loop.SetTool(OverseerTool.Orbital);
                        input.Select(p.id);
                        _orbitalMinimized = true;
                    }
                }

                HudLayout.SplitRow(r.x, r.y, r.width, r.height, 8f, W(80f), out var nameRect, out var costRect);
                var nameStyle = on ? _onText : _action;
                var prevName = nameStyle.normal.textColor;
                if (!on && (locked || wait > 0f)) nameStyle.normal.textColor = TextMuted;
                GUI.Label(nameRect, FitChipText(nameStyle, new[] { $"{p.DisplayNameText}  ·  {p.DescriptionText}", p.DisplayNameText }, nameRect.width), nameStyle);
                nameStyle.normal.textColor = prevName;

                var costStyle = _microRight;
                var prevCost = costStyle.normal.textColor;
                string right;
                if (locked) { right = Loc.S("NEED TECH"); costStyle.normal.textColor = TextMuted; }
                else if (wait > 0f) { right = Loc.F("{0} s", Mathf.CeilToInt(wait)); costStyle.normal.textColor = TextMuted; }
                else if (p.cost <= 0) right = Loc.S("FREE");
                else
                {
                    right = Loc.F("{0}+ EU", p.cost);
                    if (p.cost > treasury) costStyle.normal.textColor = Alarm;
                }
                GUI.Label(costRect, right, costStyle);
                costStyle.normal.textColor = prevCost;

                Tooltip(r, new HudTip(p.DisplayNameText.ToUpperInvariant(),
                    Lines(p.DescriptionText, locked
                        ? Loc.F("Needs {0}.", TechCatalog.Get(OrbitalTuning.TechFor(p.tier))?.DisplayNameText)
                        : Loc.F("From {0} EU next to an uplink. Recharge {1:0} s.", p.cost, p.cooldownSeconds))));
                y += rowH + 4f;
            }
        }

        private void DrawFlagPopup()
        {
            var fp = _loop.FlagInput;
            if (fp == null) return;

            if (_flagMinimized)
            {
                DrawMinimizedFlagStrip(fp, W(300f));
                return;
            }

            // 398 at 100 % text; the text rows (board line, eight flags, bounty, hint, orders) grow.
            var rect = HudLayout.ToolPopup(_sw, _sh, W(300f), 398f + HudTextScale.Grow(306f));
            _contentBottom = rect.y;
            var c = Panel(rect, Loc.S("Flag orders"), true, HudTips.FlagCatalog());

            FitLabel(new Rect(c.x, c.y, c.width, L(13f)), FlagBoardLine(), _micro);
            float y = c.y + L(17f);

            float fh = L(24f);
            y = FlagRow(fp, new Rect(c.x, y, c.width, fh), FlagKey(GameAction.FlagExplore) + Loc.S("Explore  ·  survey 22 m / 90 s"), fp.ExploreFlag);
            y = FlagRow(fp, new Rect(c.x, y, c.width, fh), FlagKey(GameAction.FlagClearThreat) + Loc.S("Clear Threat"), fp.ClearThreatFlag);
            y = FlagRow(fp, new Rect(c.x, y, c.width, fh), FlagKey(GameAction.FlagBuild) + Loc.S("Build Here"), fp.BuildFlag);
            y = FlagRow(fp, new Rect(c.x, y, c.width, fh), FlagKey(GameAction.FlagExtract) + Loc.S("Extract"), fp.ExtractFlag);
            y = FlagRow(fp, new Rect(c.x, y, c.width, fh), FlagKey(GameAction.FlagDefend) + Loc.S("Defend  ·  watch 50 s after"), fp.DefendFlag);
            y = FlagRow(fp, new Rect(c.x, y, c.width, fh), FlagKey(GameAction.FlagResearchSite) + Loc.S("Research Site"), fp.ResearchSiteFlag);
            y = FlagRow(fp, new Rect(c.x, y, c.width, fh), FlagKey(GameAction.FlagOutpost) + Loc.S("Outpost"), fp.OutpostFlag);
            y = FlagRow(fp, new Rect(c.x, y, c.width, fh), FlagKey(GameAction.FlagTerraform) + Loc.S("Terraform"), fp.TerraformFlag);

            y += 4f;
            int shown = fp.ShownBounty;
            bool posted = fp.HasSelectedPosted;
            bool canPay = fp.ShownBountyAffordable;
            var bountyRow = new Rect(c.x, y, Mathf.Max(140f, c.width - 64f), L(24f));
            HudLayout.StatLine(bountyRow, out var bountyLabel, out var bountyValue);
            FitLabel(bountyLabel, Loc.S("BOUNTY"), _micro);
            FitLabel(bountyValue, Loc.F("{0:N0} EU", shown), _value);
            Tooltip(bountyRow, HudTips.BountyAmount());
            float by = y + (bountyRow.height - 20f) * 0.5f;
            BountyButtons(fp, new Rect(c.xMax - 58f, by, 26f, 20f), new Rect(c.xMax - 28f, by, 26f, 20f));

            y += L(22f);
            var prevC = _micro.normal.textColor;
            _micro.normal.textColor = canPay ? TextMuted : Alarm;
            string placeHint = posted
                ? Loc.F("this pole · {0} edits this bounty", BountyKeys())
                : canPay
                    ? Loc.F("next post · escrow {0} EU", shown)
                    : Loc.F("next post · need {0} EU", shown);
            FitLabel(new Rect(c.x, y, c.width, L(13f)), placeHint, _micro);
            _micro.normal.textColor = prevC;

            y += L(18f);
            DrawOrdersField(fp, new Rect(c.x, y, c.width, L(40f)));
        }

        /// <summary>The bounty's − / + pair (full flag list and the collapsed strip).</summary>
        private void BountyButtons(FlagPlacementInput fp, Rect minus, Rect plus)
        {
            if (GUI.Button(minus, "−", _chipOff)) fp.NudgeBounty(-MajestyEconomy.FlagBountyStep);
            if (GUI.Button(plus, "+", _chipOff)) fp.NudgeBounty(MajestyEconomy.FlagBountyStep);
            int step = Mathf.RoundToInt(MajestyEconomy.FlagBountyStep);
            Tooltip(minus, HudTips.BountyDown(step));
            Tooltip(plus, HudTips.BountyUp(step));
        }

        /// <summary>Flag-list key column: the bound key, padded so the flag names stay aligned.</summary>
        private static string FlagKey(GameAction action) => InputBindings.Hint(action).PadRight(3) + " ";

        private static string BountyKeys() =>
            $"{InputBindings.Hint(GameAction.BountyUp)}/{InputBindings.Hint(GameAction.BountyDown)}";

        private const string OrdersControl = "sm_flag_orders";
        private bool _ordersFieldDrawn;

        /// <summary>
        /// Optional written orders for the next flag ("stay away unless level 9+", "no scouts").
        /// Parsed on the spot so the player sees what heroes will understand before posting.
        /// </summary>
        private void DrawOrdersField(FlagPlacementInput fp, Rect r)
        {
            _ordersFieldDrawn = true;
            float labelW = W(52f);
            var field = new Rect(r.x + labelW, r.y, r.width - labelW, L(20f));
            var label = new Rect(r.x, r.y + 2f, labelW, L(18f));
            FitLabel(label, Loc.S("ORDERS"), _micro, HudTips.Orders());
            Tooltip(field, HudTips.Orders());

            var e = Event.current;
            bool focused = GUI.GetNameOfFocusedControl() == OrdersControl;
            if (focused && e.type == EventType.KeyDown &&
                (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter || e.keyCode == KeyCode.Escape))
            {
                GUI.FocusControl(null);
                e.Use();
            }
            else if (focused && e.type == EventType.MouseDown && !field.Contains(e.mousePosition))
            {
                GUI.FocusControl(null); // click-away returns the keyboard to the game
            }

            GUI.SetNextControlName(OrdersControl);
            fp.PendingOrders = GUI.TextField(field, fp.PendingOrders ?? "", 120);

            string read = fp.PendingOrdersSummary;
            string line = string.IsNullOrWhiteSpace(fp.PendingOrders)
                ? Loc.S("optional · e.g. \"stay away unless level 9+\"")
                : !string.IsNullOrEmpty(read) ? Loc.F("understood: {0}", read)
                : LayaBridge.Instance != null && LayaBridge.Instance.Online ? Loc.S("not understood — local AI will try on post")
                : Loc.S("not understood — heroes will ignore it");
            FitLabel(new Rect(r.x, r.y + L(22f), r.width, L(14f)), line, _micro);
        }

        private float FlagRow(FlagPlacementInput fp, Rect r, string label, FlagData data)
        {
            if (data == null) return r.y;

            bool on = fp.SelectedFlag == data;
            if (GUI.Button(r, GUIContent.none, on ? _rowOn : _rowOff))
            {
                // Pick, then get out of the way: the next click on the map posts the flag.
                _loop.SetTool(OverseerTool.Flag);
                fp.SelectFlag(data);
                _flagMinimized = true;
            }

            HudSkin.Dot(new Vector2(r.x + 11f, r.center.y), 8f, data.bannerColor);
            FitLabel(new Rect(r.x + 22f, r.y, r.width - 28f, r.height), label, on ? _onText : _action, HudTips.FlagKind(data.flagType));
            return r.yMax + 4f;
        }

        private void DrawBuildPopup()
        {
            var bp = _loop.BuildInput;
            if (bp == null || bp.Catalog == null) return;

            if (_buildMinimized)
            {
                string name = bp.Selected != null ? Loc.S(bp.Selected.displayName) : Loc.S("module");
                DrawMinimizedToolStrip(OverseerTool.Build, W(320f),
                    name.ToUpperInvariant(), Loc.S("click the map to place · click here for the list"), InputBindings.Hint(GameAction.ToolBuild));
                return;
            }

            var visible = bp.VisibleIndices;
            int count = visible.Count;
            float rowH = L(24f);
            float rowStep = rowH + 4f;
            float fullH = count * rowStep;
            float popupH = L(58f) + Mathf.Min(L(336f), fullH);
            var rect = HudLayout.ToolPopup(_sw, _sh, W(320f), popupH);
            _contentBottom = rect.y;
            var c = Panel(rect, Loc.S("Build catalog"), true, HudTips.BuildCatalog());

            FitLabel(new Rect(c.x, c.y, c.width, L(13f)),
                DemoSettings.FirstHourDemo
                    ? Loc.S("1 Engineer workshop · LMB on open ground")
                    : Loc.S("Pick a building · LMB on open ground"),
                _micro);
            float y = c.y + L(17f);

            var view = new Rect(c.x, y, c.width, c.yMax - y);
            var content = new Rect(0f, 0f, c.width - 18f, fullH);
            _tips.BeginClip(view);
            _buildScroll = GUI.BeginScrollView(view, _buildScroll, content);

            float rowY = 0f;
            for (int slot = 0; slot < count; slot++)
            {
                int i = visible[slot];
                var b = bp.Catalog[i];
                if (b == null) continue;

                bool on = bp.SelectedIndex == i;
                bool locked = !_loop.IsBuildingUnlocked(b.category);
                bool canAfford = !locked && (_loop.Resources == null || _loop.Resources.CanAfford(CostOf(b)));
                var r = new Rect(0f, rowY, content.width, rowH);

                if (GUI.Button(r, GUIContent.none, on ? _rowOn : _rowOff) && !locked)
                {
                    // Pick, then get out of the way: the next click on the map places it.
                    _loop.SetTool(OverseerTool.Build);
                    bp.SelectBuilding(i);
                    _buildMinimized = true;
                }

                float keyW = W(18f);
                GUI.Label(new Rect(r.x + 8f, r.y, keyW, r.height), BuildHotkeyLabel(slot), on ? _onText : _micro);
                string cost = FormatBuildCost(CostOf(b));
                HudLayout.SplitRow(r.x, r.y, r.width, r.height, 8f + keyW, W(80f), out var nameRect, out var costRect);
                var nameStyle = on ? _onText : _action;
                var prevColor = nameStyle.normal.textColor;
                if (!on && (!canAfford || locked)) nameStyle.normal.textColor = TextMuted;
                string full = BuildRowLabel(b);
                string label = Loc.S(b.displayName);
                GUI.Label(nameRect, FitChipText(nameStyle, new[] { full, label }, nameRect.width), nameStyle);
                nameStyle.normal.textColor = prevColor;

                var costStyle = _microRight;
                var prevCost = costStyle.normal.textColor;
                if (locked) costStyle.normal.textColor = TextMuted;
                else if (!canAfford) costStyle.normal.textColor = Alarm;
                GUI.Label(costRect, FitChipText(costStyle, new[] { locked ? Loc.S("NEED TECH") : cost }, costRect.width), costStyle);
                costStyle.normal.textColor = prevCost;

                string what = !string.IsNullOrEmpty(b.description) ? Loc.S(b.description) : (full != label ? full : null);
                Tooltip(r, HudTips.BuildEntry(label.ToUpperInvariant(), what, cost, locked, canAfford,
                    slot <= 9 ? BuildHotkeyLabel(slot) : null));

                rowY += rowStep;
            }

            GUI.EndScrollView();
            _tips.EndClip();
        }

        private static string BuildHotkeyLabel(int index)
        {
            if (index < 9) return (index + 1).ToString();
            if (index == 9) return "0";
            return "·";
        }

        private static string BuildRowLabel(BuildingData b)
        {
            if (b == null) return Loc.S("module");
            if (!string.IsNullOrEmpty(b.description))
                return $"{Loc.S(b.displayName)}  ·  {Loc.S(b.description)}";
            switch (b.category)
            {
                case BuildingCategory.Defense: return Loc.S("Defense Battery  ·  auto-fires 18 m");
                case BuildingCategory.Mining: return Loc.S("OPS Drop-off  ·  does not grow EU");
                case BuildingCategory.Mine: return Loc.S("Nuclear Mine");
                case BuildingCategory.Power: return Loc.S("Solar Farm");
                case BuildingCategory.ClimateLoom:
                case BuildingCategory.AegisSpire:
                case BuildingCategory.DeepArchive:
                    return Loc.F("{0}  ·  bonus while standing", Loc.S(b.displayName));
                default: return Loc.S(b.displayName);
            }
        }

        private static string FlagStripLabel(FlagPlacementInput fp)
        {
            var data = fp != null ? fp.SelectedFlag : null;
            string name = data != null && !string.IsNullOrEmpty(data.displayName)
                ? Loc.S(data.displayName)
                : Loc.S("flag");
            return name.ToUpperInvariant();
        }

        /// <summary>
        /// Collapsed catalog: what is armed for placement. Clicking the strip re-opens the list
        /// (as does B / G). The map stays clear for the placing click.
        /// </summary>
        private void DrawMinimizedToolStrip(
            OverseerTool tool, float width, string title, string hint, string key)
        {
            var rect = HudLayout.ToolPopup(_sw, _sh, width, StripHeight);
            _contentBottom = rect.y;
            bool hot = rect.Contains(Event.current.mousePosition);
            var c = Panel(rect, null);
            if (hot) HudSkin.RuleH(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), Gold);
            HudSkin.Pill(new Rect(rect.x + 6f, rect.y + 9f, 3f, rect.height - 18f), Accent);
            float keyW = Mathf.Max(22f, _tooltipStyle.CalcSize(new GUIContent(key)).x + 8f);
            FitLabel(new Rect(c.x, c.y - 6f, c.width - keyW - 8f, L(18f)), title, _value);
            FitLabel(new Rect(c.x, c.y - 6f + L(15f), c.width - keyW - 8f, L(12f)), hint, _micro);
            var keyRect = new Rect(c.xMax - keyW, rect.center.y - L(9f), keyW, L(18f));
            HudSkin.Well(keyRect);
            GUI.Label(keyRect, key, _tooltipStyle);
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
                ExpandMenu(tool);
            Tooltip(rect, HudTips.OpenList(key));
        }

        /// <summary>Collapsed catalog strip: title over hint; grows with TEXT SIZE.</summary>
        private static float StripHeight => 36f + HudTextScale.Grow(27f);

        /// <summary>Collapsed flag catalog: the armed flag, its bounty with − / +, and a way back to the list.</summary>
        private void DrawMinimizedFlagStrip(FlagPlacementInput fp, float width)
        {
            var rect = HudLayout.ToolPopup(_sw, _sh, width, StripHeight);
            _contentBottom = rect.y;
            var c = Panel(rect, null);
            HudSkin.Pill(new Rect(rect.x + 6f, rect.y + 9f, 3f, rect.height - 18f), Accent);

            // Bounty stays adjustable without re-opening the list; those buttons come first so the
            // strip's own "open the list" button below does not swallow their clicks.
            float by = rect.center.y - 10f;
            var minus = new Rect(c.xMax - 56f, by, 26f, 20f);
            var plus = new Rect(c.xMax - 28f, by, 26f, 20f);
            BountyButtons(fp, minus, plus);
            var prev = _number.alignment;
            _number.alignment = TextAnchor.MiddleRight;
            var prevC = _number.normal.textColor;
            _number.normal.textColor = fp.ShownBountyAffordable ? HudSkin.GoldBright : Alarm;
            float numW = 104f * HudTextScale.Font(17) / 17f;
            var amount = new Rect(minus.x - numW - 8f, rect.y, numW, rect.height);
            FitLabel(amount, Loc.F("{0:N0} EU", fp.ShownBounty), _number, HudTips.BountyAmount());
            _number.alignment = prev;
            _number.normal.textColor = prevC;

            var open = new Rect(rect.x, rect.y, Mathf.Max(0f, amount.x - 8f - rect.x), rect.height);
            bool hot = open.Contains(Event.current.mousePosition);
            if (hot) HudSkin.RuleH(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), Gold);
            FitLabel(new Rect(c.x, c.y - 6f, open.width - Pad, L(18f)), FlagStripLabel(fp), _value);
            FitLabel(new Rect(c.x, c.y - 6f + L(15f), open.width - Pad, L(12f)),
                fp.HasSelectedPosted ? Loc.F("this pole · {0} edits it", BountyKeys()) : Loc.S("next post · click the map"), _micro);
            if (GUI.Button(open, GUIContent.none, GUIStyle.none))
                ExpandMenu(OverseerTool.Flag);
            Tooltip(open, HudTips.OpenList(InputBindings.Hint(GameAction.ToolFlag).ToUpperInvariant()));
        }

        /// <summary>Plate button whose label fits (ends in "…" rather than clipping) with an optional tooltip.</summary>
        private bool Btn(Rect r, string label, GUIStyle style, HudTip? tip = null)
        {
            string shown = FitChipText(style, new[] { label }, r.width);
            if (tip.HasValue) Tooltip(r, tip.Value);
            else if (shown != label) Tooltip(r, new HudTip(null, label));
            return GUI.Button(r, shown, style);
        }

        /// <summary>Toggle-style button. A label too long for it (large text, long French) ends in "…" and shows whole as a tooltip.</summary>
        private bool Chip(Rect r, string label, bool on, HudTip? tip = null)
        {
            var style = on ? _chipOn : _chipOff;
            string shown = FitChipText(style, new[] { label }, r.width); // CalcSize includes the padding
            if (tip.HasValue)
            {
                var t = tip.Value;
                // A cut label still shows whole: it becomes the tooltip's title.
                Tooltip(r, shown != label ? new HudTip(label, t.Body, t.Key) : t);
            }
            else if (shown != label) Tooltip(r, new HudTip(null, label));
            return GUI.Button(r, shown, style);
        }

        /// <summary>Settings row height (26 at 100 % text) and the step between rows.</summary>
        private static float RowH => HudTextScale.Line(26f);
        private static float RowStep => RowH + 6f;

        private static bool ShiftHeld()
        {
            var e = Event.current;
            if (e != null && e.shift) return true;
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }

        /// <summary>F10 during Playing/Paused even when a HUD button has IMGUI focus.</summary>
        private void HandleDebugHopKeys()
        {
            if (!DevTools.Enabled) return;
            if (_loop.Screen != DemoScreen.Playing && _loop.Screen != DemoScreen.Paused) return;
            var e = Event.current;
            if (e == null || e.type != EventType.KeyDown || e.keyCode != KeyCode.F10) return;
            e.Use();
            _loop.RequestDebugBodyCycle(ShiftHeld());
        }

        /// <summary>Majesty duplicate pricing: the next building of a type costs 150% of the last.</summary>
        private ResourceAmount[] CostOf(BuildingData b) =>
            b == null ? null : (_loop != null && _loop.Placer != null ? _loop.Placer.CostFor(b) : b.buildCost);

        private static string FormatBuildCost(ResourceAmount[] cost)
        {
            if (cost == null || cost.Length == 0) return Loc.S("free");
            var parts = new System.Text.StringBuilder();
            for (int i = 0; i < cost.Length; i++)
            {
                if (i > 0) parts.Append(" · ");
                parts.Append(cost[i].amount);
                parts.Append(' ');
                parts.Append(ShortResource(cost[i].resource));
            }
            return parts.ToString();
        }

        private static string ShortResource(ResourceId id) => id switch
        {
            ResourceId.Regolith => Loc.S("REG"),
            ResourceId.WaterIce => Loc.S("ICE"),
            ResourceId.Metals => Loc.S("EU"),
            ResourceId.Power => Loc.S("PWR"),
            _ => id.ToString().ToUpperInvariant()
        };

        private void DrawMissionPanel()
        {
            var mission = _loop.Mission;
            if (mission == null) return;

            var flags = _loop.Flags != null ? _loop.Flags.Flags : null;
            int flagN = flags != null ? Mathf.Min(3, flags.Count) : 0;
            float h = L(136f);
            if (DemoSettings.FirstHourDemo) h += L(52f);
            if (mission.DeadlineEnabled) h += L(26f);
            h += L(18f + flagN * 16f);
            // Wrapped lines (long French, large text) can need more than the estimate: hug last pass's content.
            h = Mathf.Max(h, _missionMeasuredH);

            float panelW = HudLayout.ObjectivesWidth;
            var rect = new Rect(_sw - M - panelW, _playTop > 1f ? _playTop : M, panelW, h);
            RightColumnBottom = rect.yMax;
            var c = Panel(rect, Loc.S("Objectives"), true, HudTips.Objectives());
            float y = c.y;

            float stakeH = L(20f);
            Stake(new Rect(c.x, y, c.width, stakeH), mission.DensCleared,
                Loc.S("Clear dens"),
                mission.LairCount > 0
                    ? Loc.F("{0}/{1} left", mission.UnclearedLairs, mission.LairCount)
                    : Loc.F("{0} fauna", mission.StalkersRemaining));
            y += stakeH;

            var set = _loop.Settlement;
            string sustainVal = set != null
                ? $"{mission.TreasuryCurrent:N0}/{mission.TreasuryGoal:N0} · {_loop.FormatHold(mission.SustainElapsed)}/{_loop.FormatHold(mission.SustainRequired)}"
                : _loop.FormatHold(mission.SustainElapsed);
            Stake(new Rect(c.x, y, c.width, stakeH), mission.SustainComplete,
                Loc.S("Grow the treasury"),
                sustainVal);
            y += stakeH;

            string launchNeed = _loop.Research != null
                ? _loop.Research.LaunchTechLabel(_loop.ActiveBody)
                : Loc.S("Lunar Rocket");
            bool fullCampaign = !DemoSettings.FirstHourDemo;
            string launchLine = LaunchPath.ObjectiveText(fullCampaign, mission.LaunchReady, launchNeed);
            Stake(new Rect(c.x, y, c.width, stakeH), mission.LaunchReady,
                Loc.S("Launch craft"),
                fullCampaign ? launchLine : LaunchPath.Chain(launchNeed));
            y += L(22f);
            if (!fullCampaign)
                y = WrapBlock(c, y, launchLine) - 2f;

            if (set != null)
            {
                FitLabel(new Rect(c.x, y, c.width, L(14f)), set.SustainHint, _micro);
                y += L(16f);
            }

            if (mission.DeadlineEnabled)
            {
                float left = Mathf.Max(0f, mission.MissionDeadline - mission.MissionElapsed);
                float frac = mission.MissionDeadline > 0f ? left / mission.MissionDeadline : 0f;
                float timeW = W(60f);
                FitLabel(new Rect(c.x, y, c.width - timeW - 6f, L(14f)), Loc.S("DEADLINE"), _micro);
                GUI.Label(new Rect(c.xMax - timeW, y, timeW, L(14f)), _loop.FormatHold(left), _microRight);
                Meter(new Rect(c.x, y + L(16f), c.width, 5f), frac, frac < 0.25f ? Alarm : Accent);
                y += L(16f) + 10f;
            }

            var logHead = new Rect(c.x, y, c.width, L(14f));
            FitLabel(logHead, Loc.S("FLAG LOG"), _section, HudTips.FlagLog());
            y += L(16f);
            if (flagN <= 0)
            {
                FitLabel(new Rect(c.x, y, c.width, L(14f)), Loc.F("No flags posted — {0} to bounty.", InputBindings.Hint(GameAction.ToolFlag)), _micro);
                y += L(14f);
            }
            else
            {
                for (int i = 0; i < flagN; i++)
                {
                    var f = flags[flags.Count - flagN + i];
                    if (f?.Data == null) continue;
                    string claim = f.ClaimCount > 0
                        ? Loc.S("claimed")
                        : (string.IsNullOrEmpty(f.InterestLabel) ? Loc.S("open") : f.InterestLabel);
                    Color col = f.Data != null ? f.Data.bannerColor : Gold;
                    float lineH = L(15f);
                    HudSkin.Dot(new Vector2(c.x + 4f, y + lineH * 0.5f), 7f, col);
                    string orders = f.Orders != null && f.Orders.HasRules ? $"  ·  {f.Orders.Summary()}" : "";
                    FitLabel(new Rect(c.x + 12f, y, c.width - 12f, lineH),
                        Loc.F("{0}  {1:N0} EU  ·  {2}{3}", Loc.S(f.Data.displayName), FlagBountySync.Amount(f), claim, orders), _micro);
                    y += lineH;
                }
            }
            _missionMeasuredH = y + Pad - rect.y;
        }

        private float _missionMeasuredH;

        private void Stake(Rect r, bool done, string label, string value)
        {
            CheckBox(new Rect(r.x, r.y + r.height * 0.5f - 6f, 11f, 11f), done);
            HudLayout.Stake(r, out var labelRect, out var valueRect);
            // Value first so the label can take what the value does not need.
            var valueStyle = _microRight;
            float valueW = Mathf.Min(valueRect.width, valueStyle.CalcSize(new GUIContent(value)).x);
            if (valueW < valueRect.width)
            {
                float spare = valueRect.width - valueW;
                labelRect.width += spare;
                valueRect.x += spare;
                valueRect.width = valueW;
            }
            FitLabel(labelRect, label, done ? _muted : _body);
            FitLabel(valueRect, value, valueStyle);
        }

        private void DrawInspectPanel()
        {
            // The conversation ends when the hero is gone or the player selects someone else.
            if (!ReferenceEquals(_chatAgent, null) && (_chatAgent == null || !_loop.IsSelected(_chatAgent)))
                CloseChat();
            if (_loop.SelectedStructure != null)
            {
                DrawBuildingCard(_loop.SelectedStructure);
                return;
            }
            if (_chatAgent != null)
            {
                DrawChatPanel(_chatAgent);
                return;
            }
            DrawSpecialistCards();
        }

        private float DrawGuildBenefit(float x, float y, float width, RobotGuildDef guild)
        {
            if (guild == null || _loop.GuildBenefits == null) return y;
            var dir = _loop.GuildBenefits;
            bool researched = _loop.Research != null && _loop.Research.IsUnlocked(guild.BenefitTech);
            FitLabel(new Rect(x, y, width, L(13f)), guild.BenefitBlurb, _micro);
            y += L(14f);
            string label;
            if (!researched)
                label = Loc.F("Research {0}", guild.BenefitNameText);
            else if (dir.IsActive(guild.Id))
                label = Loc.F("{0} {1:F0}s", guild.BenefitNameText, dir.Remaining(guild.Id));
            else if (dir.CooldownLeft(guild.Id) > 0.5f)
                label = Loc.F("Cooldown {0:F0}s", dir.CooldownLeft(guild.Id));
            else
                label = Loc.F("ACTIVATE {0} EU", guild.ActivateCost);
            bool can = researched &&
                       dir.CanActivate(guild.Id, _loop.Research, _loop.Resources, true);
            var chip = new Rect(x, y, Mathf.Min(W(220f), width), L(22f));
            if (Chip(chip, label, can) && can)
                _loop.TryActivateGuildBenefit(guild.Id);
            Tooltip(chip, HudTips.GuildBenefit());
            return y + L(22f) + 2f;
        }

        private void DrawBuildingCard(ColonyStructure st)
        {
            float cardW = Mathf.Min(W(340f), _sw - M * 2f);
            bool yard = st.IsFobotYard || st.Category == BuildingCategory.FobotYard;
            bool wreckShop = st.IsWorkshop && st.HasPreferredClass &&
                             _loop.HasWreckFor(st.PreferredClass);
            bool padStall = st.IsLandingPad;
            float cardH = L(st.IsGuild ? 228f
                : yard || wreckShop || st.IsWatchtower || st.IsAidStation || padStall ? 188f
                : 148f);
            // After the first frame the card hugs what it drew instead of the per-kind guess.
            if (_cardFor == st && _cardMeasuredH > 0f) cardH = _cardMeasuredH;
            float y0 = _contentBottom - 8f - cardH;
            var rect = new Rect(M, y0, cardW, cardH);
            var c = Panel(rect, null);
            HudSkin.RuleH(new Rect(rect.x, rect.y, rect.width, 1f), Accent);

            float row = c.y;
            FitLabel(new Rect(c.x, row, c.width, L(16f)), Loc.S(st.DisplayName), _value);
            row += L(18f);

            string role = st.IsWorkshop
                ? (RefabLabel(st) ?? (st.RobotFabricated ? Loc.S("Workshop · robot online") : Loc.S("Workshop · fabricating…")))
                : st.IsGuild
                    ? (st.HasPreferredClass
                        ? (RobotGuildCatalog.ForClass(st.PreferredClass)?.CatalogLineText ?? Loc.S(st.DisplayName))
                        : Loc.S("Guild Hall · assign a class"))
                    : st.IsWonder ? Loc.S("Secret Project landmark")
                    : st.IsFobotYard || st.Category == BuildingCategory.FobotYard ? Loc.S("Fobot Yard · wrecks wait")
                    : st.IsWatchtower
                        ? (st.LaserArmed ? Loc.S("Watchtower · lasers armed") : Loc.S("Watchtower · guard post"))
                    : st.IsAidStation ? Loc.S("Aid Station · paid patch")
                    : st.IsLandingPad ? Loc.S("Weigh-station · tank vs wallet")
                    : st.IsResidential ? Loc.S("Habitat · worker bots")
                    : st.Role.ToString();
            string worker = st.IsResidential
                ? Loc.S("bots")
                : (st.HasPreferredClass ? ColonyStructure.ClassLabel(st.PreferredClass) : "—");
            string beds = st.IsResidential
                ? Loc.F(" · bots {0}/{1}", st.Residents, st.ResidentCapacity)
                : "";
            FitLabel(new Rect(c.x, row, c.width, L(13f)),
                st.IsResidential
                    ? $"{role}{beds}"
                    : $"{role} · {worker} {st.WorkerCount}/{st.WorkerSlots}{beds}",
                _micro);
            row += L(15f);

            var hpRow = new Rect(c.x, row, c.width, L(14f));
            Bar(hpRow, Loc.S("HP"), st.Health01, HpFill);
            Tooltip(hpRow, HudTips.Health());
            row += L(18f);

            string workers = st.IsResidential
                ? (st.Residents > 0
                    ? (st.LevyPurse > 0
                        ? Loc.F("{0:N0} EU sitting — a bot walks it home.", st.LevyPurse)
                        : Loc.S("Worker bots bunk here — EU sits until a bot walks it home."))
                    : Loc.S("Empty beds — seed crew arrives with the first HAB."))
                : (st.LevyPurse > 0 && !st.IsTreasuryChest
                    ? Loc.F("{0} · till {1} EU", FormatWorkers(st), st.LevyPurse)
                    : FormatWorkers(st));
            FitLabel(new Rect(c.x, row, c.width, L(13f)), workers, _micro);
            row += L(16f);

            // Each branch leaves `slot` = the height of its last element (a note or a chip row).
            float chipH = L(22f);
            float slot = chipH;
            if (st.IsResidential)
            {
                slot = CardNote(c, row,
                    st.LevyPurse > 0
                        ? Loc.F("{0:N0} EU — bots carry it to Commons.", st.LevyPurse)
                        : Loc.S("Home to worker bots. Contract mechs take the bounties."));
            }
            else if (st.IsFobotYard)
            {
                string wrecks = _loop.WreckSummary();
                row += CardNote(c, row,
                    string.IsNullOrEmpty(wrecks)
                        ? Loc.S("Wrecks wait here. Pay EU to stand THIS ego up.")
                        : wrecks) + 2f;
                int bill = _loop.FieldReviveMet;
                var pay = new Rect(c.x, row, W(140f), chipH);
                if (Chip(pay, bill > 0 ? Loc.F("PAY {0} EU", bill) : Loc.S("PAY YARD"),
                        bill > 0 && _loop.CanPayYard))
                    _loop.PayFobotYard();
                Tooltip(pay, HudTips.PayYard());
                FitLabel(new Rect(pay.xMax + 8f, row, c.xMax - pay.xMax - 8f, chipH),
                    Loc.F("{0}  ·  120s", InputBindings.Hint(GameAction.PayYard)), _micro);
            }
            else if (st.IsLandingPad)
            {
                slot = CardNote(c, row,
                    Loc.S("Trade ships land here. Each caravan pays EU into the Market till."));
            }
            else if (st.IsGuild)
            {
                var guild = st.HasPreferredClass
                    ? RobotGuildCatalog.ForClass(st.PreferredClass)
                    : null;
                if (guild != null)
                {
                    row = CardLine(c, row, guild.Motto);
                    row = CardLine(c, row, Loc.F("Wants {0} · ignores {1}", guild.WantsText, guild.IgnoresText));
                    row = CardLine(c, row, Loc.S("Flags near this hall pull them (no new robot)."));
                    row = DrawGuildBenefit(c.x, row, c.width, guild);
                }
                else
                {
                    row = CardLine(c, row, Loc.S("Assign a class. Flags near this hall pull them (no new robot)."));
                }

                if (!st.ClassLocked)
                    ClassChips(c.x, row, chipH, st);
            }
            else if (st.Category == BuildingCategory.FobotYard)
            {
                int met = _loop.FieldReviveMet;
                FitLabel(new Rect(c.x, row, c.width, L(13f)),
                    _loop.NeedsFieldRevive
                        ? Loc.F("Stand-up bill {0} EU. Cost scales with level.", met)
                        : Loc.S("No wrecks. Dock this yard before anyone goes down."), _micro);
                var wrecks = _loop.Corpses;
                if (wrecks != null && wrecks.Count > 0)
                {
                    row += L(14f);
                    var names = new System.Text.StringBuilder();
                    for (int i = 0; i < wrecks.Count && i < 3; i++)
                    {
                        if (i > 0) names.Append(" · ");
                        names.Append(ColonyStructure.ClassLabel(wrecks[i].Class));
                        names.Append(" L");
                        names.Append(Mathf.Max(1, wrecks[i].Level));
                    }
                    FitLabel(new Rect(c.x, row, c.width, L(13f)), names.ToString(), _micro);
                }
                row += L(16f);
                bool canPay = _loop.NeedsFieldRevive && _loop.HasFobotYard &&
                              _loop.FieldReviveReadyIn <= 0.5f;
                var pay = new Rect(c.x, row, W(220f), chipH);
                if (Chip(pay, canPay ? Loc.F("PAY {0} EU", met) : Loc.S("YARD IDLE"), canPay) && canPay)
                    _loop.RetryParty();
                Tooltip(pay, HudTips.PayYard());
            }
            else if (st.IsWatchtower)
            {
                int guards = st.WorkerCount;
                FitLabel(new Rect(c.x, row, c.width, L(13f)),
                    guards > 0
                        ? Loc.S("Guard posted. Haul can drop EU here when Commons is far.")
                        : Loc.S("Empty post. Aegis / Rim Watch will clock in."), _micro);
                row += L(16f);
                if (st.LaserArmed)
                {
                    slot = CardNote(c, row, Loc.S("Lasers armed — 18 m."));
                }
                else
                {
                    bool can = _loop.Resources != null &&
                               _loop.Resources.Get(ResourceId.Metals) >= OverseerRules.WatchtowerLaserCost;
                    var arm = new Rect(c.x, row, W(220f), chipH);
                    if (Chip(arm, Loc.F("ARM LASERS {0} EU", OverseerRules.WatchtowerLaserCost), can) && can)
                        _loop.TryArmWatchtower(st);
                    Tooltip(arm, HudTips.ArmLasers());
                }
            }
            else if (st.IsAidStation)
            {
                slot = CardNote(c, row,
                    st.WorkerCount > 0
                        ? Loc.F("Triage posted. Patch {0} EU.", OverseerRules.AidStationHealCost)
                        : Loc.F("Empty bay. Hurt robots pay {0} EU.", OverseerRules.AidStationHealCost));
            }
            else if (st.Category == BuildingCategory.Market)
            {
                slot = CardNote(c, row,
                    Loc.F("Potions + necklace. Pays {0} EU/day into its till.", MajestyEconomy.MarketDailyTax));
            }
            else if (st.Category == BuildingCategory.Blacksmith)
            {
                slot = CardNote(c, row, Loc.S("Lodge arms and armor. Heroes buy with their EU."));
            }
            else if (!st.ClassLocked)
            {
                ClassChips(c.x, row, chipH, st);
            }
            else
            {
                slot = CardNote(c, row,
                    st.IsWonder
                        ? Loc.S("Landmark — bonuses while standing.")
                        : st.RobotFabricated
                            ? Loc.F("Fabricates {0} — flags nearby pull them.", ColonyStructure.ClassLabel(st.PreferredClass))
                            : Loc.F("Building a {0} robot…", ColonyStructure.ClassLabel(st.PreferredClass)));
                var wreckClass = ColonyStructure.RobotClassForWorkshop(st.Category);
                if (wreckClass.HasValue && _loop.HasWreckFor(wreckClass.Value))
                {
                    row += slot + 2f;
                    slot = chipH;
                    int met = OverseerRules.RefabMetals(st.SourceData);
                    var refab = new Rect(c.x, row, W(150f), chipH);
                    if (Chip(refab, Loc.F("RE-FAB L1 {0} EU", met), true))
                        _loop.TryRefabRookie(st);
                    Tooltip(refab, HudTips.Refab());
                    FitLabel(new Rect(refab.xMax + 8f, row, c.xMax - refab.xMax - 8f, chipH),
                        Loc.S("new chassis · wreck gone"), _micro);
                }
            }
            row += slot + 4f;

            float flagH = L(24f);
            var flagHere = new Rect(c.x, row, W(116f), flagH);
            if (GUI.Button(flagHere, Loc.S("FLAG HERE"), _chipOff))
                _loop.PostAttractFlagOnSelected();
            Tooltip(flagHere, HudTips.FlagHere());
            FitLabel(new Rect(flagHere.xMax + 8f, row, c.xMax - flagHere.xMax - 8f, flagH),
                Loc.S("Progress via research & conquest gates"), _micro);
            _cardFor = st;
            _cardMeasuredH = Mathf.Max(96f, row + flagH + Pad - rect.y);
        }

        /// <summary>One line of building-card detail; returns the y below it.</summary>
        private float CardLine(Rect c, float y, string text)
        {
            FitLabel(new Rect(c.x, y, c.width, L(13f)), text, _micro);
            return y + L(14f);
        }

        /// <summary>A short note that wraps to a second line when it must; returns its height (22 px at 100 %).</summary>
        private float CardNote(Rect c, float y, string text)
        {
            if (_microWrap == null || _microWrap.fontSize != _micro.fontSize)
                _microWrap = new GUIStyle(_micro) { wordWrap = true, clipping = TextClipping.Overflow };
            float h = Mathf.Max(L(22f), _microWrap.CalcHeight(new GUIContent(text), c.width));
            GUI.Label(new Rect(c.x, y, c.width, h), text, _microWrap);
            return h;
        }

        private GUIStyle _microWrap;

        /// <summary>SCOUT / ENG / DEF / MED: which robot class a workshop or guild serves.</summary>
        private void ClassChips(float x, float y, float h, ColonyStructure st)
        {
            float w = W(54f);
            var labels = new[] { Loc.S("SCOUT"), Loc.S("ENG"), Loc.S("DEF"), Loc.S("MED") };
            var classes = new[] { SpecialistClass.ScoutDrone, SpecialistClass.EngineerBot, SpecialistClass.DefenseMech, SpecialistClass.Medic };
            for (int i = 0; i < labels.Length; i++)
            {
                var r = new Rect(x + i * (w + 4f), y, w, h);
                if (Chip(r, labels[i], st.HasPreferredClass && st.PreferredClass == classes[i]))
                    _loop.SetSelectedWorkplaceClass(classes[i]);
                Tooltip(r, HudTips.ClassChip(ColonyStructure.ClassLabel(classes[i])));
            }
        }

        private static string FormatWorkers(ColonyStructure st)
        {
            if (st.WorkerCount <= 0) return Loc.S("No one working here yet.");
            var names = new System.Text.StringBuilder(Loc.S("On duty: "));
            for (int i = 0; i < st.Workers.Count; i++)
            {
                var w = st.Workers[i];
                if (w == null) continue;
                if (i > 0) names.Append(", ");
                names.Append(w.Data != null ? Loc.S(w.Data.displayName) : Loc.S("Specialist"));
            }
            return names.ToString();
        }

        private string RefabLabel(ColonyStructure st)
        {
            var order = _loop != null ? _loop.RefabAt(st) : null;
            if (order == null) return null;
            float left = Mathf.Max(0f, order.RequiredSeconds - order.ProgressSeconds);
            string cls = order.RefabClass.HasValue
                ? ColonyStructure.ClassLabel(order.RefabClass.Value)
                : Loc.S("ROBOT");
            return Loc.F("RE-FAB {0}:{1:00} {2}", Mathf.FloorToInt(left / 60f), Mathf.FloorToInt(left % 60f), cls);
        }

        private void DrawSpecialistCards()
        {
            var selected = _loop.SelectedAgents;
            if (selected == null || selected.Count == 0)
                return;

            int n = selected.Count;
            float avail = _sw - M * 2f - (n - 1) * 8f;
            float cardW = Mathf.Clamp(avail / n, W(168f), W(230f));
            float cardH = L(222f);
            float y = _contentBottom - 8f - cardH;

            for (int i = 0; i < n; i++)
            {
                var a = selected[i];
                if (a == null) continue;

                var rect = new Rect(M + i * (cardW + 8f), y, cardW, cardH);
                if (a.IsIncapacitated) HudSkin.Glow(rect, new Color(Alarm.r, Alarm.g, Alarm.b, 0.35f), 0.5f);
                var c = Panel(rect, null);
                Color tint = ClassTint(a.Data != null ? a.Data.specialistClass : SpecialistClass.ScoutDrone);
                HudSkin.RuleH(new Rect(rect.x, rect.y, rect.width, 1f), a.IsIncapacitated ? Alarm : tint);
                HudSkin.Pill(new Rect(rect.x + 4f, rect.y + 12f, 3f, rect.height - 24f), tint);

                float row = c.y;
                float tagW = W(46f);
                FitLabel(new Rect(c.x, row, c.width - tagW - 4f, L(16f)), Loc.F("{0}  L{1}", a.Record.Name, a.Level), _value);
                {
                    string tagLabel = a.IsIncapacitated ? Loc.S("DOWN") : RosterStatus(a);
                    Color tagFill = a.IsIncapacitated ? Alarm
                        : a.CurrentAction == SpecialistAction.PursueFlag ? Accent
                        : a.CurrentAction == SpecialistAction.Rest ? new Color(0.55f, 0.78f, 1f)
                        : Good;
                    var tag = new Rect(c.xMax - tagW, row + 1f, tagW, L(14f));
                    HudSkin.Pill(tag, tagFill);
                    var prev = _pill.normal.textColor;
                    _pill.normal.textColor = Ink;
                    FitLabel(tag, tagLabel, _pill);
                    _pill.normal.textColor = prev;
                }
                row += L(18f);

                int campus = ColonyLayout.NearestCampusIndex(a.transform.position);
                FitLabel(new Rect(c.x, row, c.width, L(13f)),
                    Loc.F("{0} · {1} · Asks {2:N0} EU · Holds {3:N0} EU{4}", Loc.S(a.Data?.displayName) ?? Loc.S("Specialist"), ColonyLayout.CampusLabel(campus), a.HireMin, a.Credits, LevyCarryLine(a)), _micro);
                row += L(16f);

                var prevAct = _action.normal.textColor;
                _action.normal.textColor = ActionTint(a.CurrentAction);
                FitLabel(new Rect(c.x, row, c.width, L(14f)), FormatAction(a), _action);
                _action.normal.textColor = prevAct;
                row += L(16f);
                if (!string.IsNullOrEmpty(a.Data?.description))
                {
                    FitLabel(new Rect(c.x, row, c.width, L(13f)), Loc.S(a.Data.description), _micro);
                    row += L(14f);
                }

                var hp = new Rect(c.x, row, c.width, L(14f));
                Bar(hp, Loc.S("HP"), a.HealthNormalized, HpFill);
                Tooltip(hp, HudTips.Health());
                row += L(16f);
                var fatigue = new Rect(c.x, row, c.width, L(14f));
                Bar(fatigue, Loc.S("FATIGUE"), a.Fatigue, FatigueFill);
                Tooltip(fatigue, HudTips.Fatigue());
                row += L(16f);
                FitLabel(new Rect(c.x, row, c.width, L(13f)),
                    Loc.F("MOVE {0:F1}  ·  WORK {1:F2}  ·  status", a.EffectiveMoveSpeed, a.EffectiveWorkRate), _micro);
                row += L(15f);

                string gene = a.LevyCarried > 0
                    ? Loc.F("levy {0} EU", a.LevyCarried)
                    : a.GeneSecondsLeft > 0.5f
                        ? Loc.F("gene {0:F0}s", a.GeneSecondsLeft)
                        : Loc.S("shop at rest beacon");
                FitLabel(new Rect(c.x, row, c.width, L(13f)),
                    $"{Truncate(a.LastReason, 14)} · {a.SuitLabel} · {gene}", _micro);
                row += L(18f);
                var talk = new Rect(c.x, row, c.width, L(22f));
                if (Chip(talk, n == 1 ? Loc.F("TALK  ·  {0}", InputBindings.Hint(GameAction.Confirm).ToUpperInvariant()) : Loc.S("TALK"), false))
                {
                    _loop.SelectOnly(a);
                    OpenChat(a);
                }
                Tooltip(talk, HudTips.Talk());
            }
        }

        // ---- hero conversation ------------------------------------------------------

        private const string ChatControl = "sm_hero_chat";
        private SpecialistAgent _chatAgent;
        private string _chatInput = "";
        private Vector2 _chatScroll;
        private bool _chatFocusPending;
        private bool _chatFieldDrawn;
        private int _chatSeenLines = -1;

        /// <summary>A conversation panel is open.</summary>
        public bool ChatOpen => _chatAgent != null;

        /// <summary>Talk to a hero: the panel takes the card's place and replies stream in word by word.</summary>
        public void OpenChat(SpecialistAgent agent)
        {
            if (agent == null) return;
            _chatAgent = agent;
            _chatInput = "";
            _chatFocusPending = true;
            _chatSeenLines = -1;
            HeroNarrator.OpenChat(agent);
        }

        public void CloseChat()
        {
            if (ReferenceEquals(_chatAgent, null)) return;
            _chatAgent = null;
            _chatInput = "";
            HeroNarrator.CloseChat();
        }

        private void DrawChatPanel(SpecialistAgent a)
        {
            var record = a.Record;
            string name = record.Name;
            Color tint = ClassTint(a.Data != null ? a.Data.specialistClass : SpecialistClass.ScoutDrone);
            float h = Mathf.Clamp(_contentBottom - 8f - (M + 90f), 230f, 390f);
            var rect = new Rect(M, _contentBottom - 8f - h, Mathf.Min(W(440f), _sw - M * 2f - 320f), h);
            if (a.IsIncapacitated) HudSkin.Glow(rect, new Color(Alarm.r, Alarm.g, Alarm.b, 0.35f), 0.5f);
            var c = Panel(rect, null);
            HudSkin.RuleH(new Rect(rect.x, rect.y, rect.width, 1f), tint);
            HudSkin.Pill(new Rect(rect.x + 4f, rect.y + 12f, 3f, rect.height - 24f), tint);

            // Header: medallion (glows while the hero is talking), name, who they are, status, close.
            var medal = new Rect(c.x, c.y, 34f, 34f);
            if (HeroNarrator.ReplyStarted)
            {
                float pulse = DemoSettings.ReduceMotion ? 0.35f : 0.25f + 0.2f * Mathf.Sin(Time.unscaledTime * 9f);
                HudSkin.SoftDot(medal.center, 64f, new Color(tint.r, tint.g, tint.b, pulse));
            }
            HudSkin.Dot(medal.center, 34f, new Color(0f, 0f, 0f, 0.5f));
            HudSkin.Ring(medal, tint);
            HudSkin.Icon(HudSkin.Expand(medal, -8f), UiIcons.Get(IconId.Robot), Color.white);
            float tagW = W(46f);
            float headW = c.width - 44f - tagW - 26f - 14f;
            FitLabel(new Rect(c.x + 44f, c.y - 1f, headW, L(20f)), name, _chatTitle);
            FitLabel(new Rect(c.x + 44f, c.y - 1f + L(19f), c.width - 44f - 30f, L(14f)),
                Loc.F("“{0}”  ·  {1} L{2}  ·  {3}", record.Designation, Loc.S(a.Data?.displayName) ?? Loc.S("Specialist"), a.Level, record.RankText), _micro);

            string tagLabel = a.IsIncapacitated ? Loc.S("DOWN") : RosterStatus(a);
            var tag = new Rect(c.xMax - 36f - tagW, c.y + 3f, tagW, L(14f));
            HudSkin.Pill(tag, a.IsIncapacitated ? Alarm : Good);
            var prevPill = _pill.normal.textColor;
            _pill.normal.textColor = Ink;
            FitLabel(tag, tagLabel, _pill);
            _pill.normal.textColor = prevPill;
            var close = new Rect(c.xMax - 26f, c.y, 26f, 22f);
            if (GUI.Button(close, "×", _chipOff))
            {
                CloseChat();
                return;
            }
            Tooltip(close, HudTips.Close());
            float headH = Mathf.Max(42f, L(19f) + L(14f) + 9f);
            Fill(new Rect(c.x, c.y + headH, c.width, 1f), new Color(1f, 1f, 1f, 0.06f));

            float inputH = L(30f);
            float statusH = L(14f);
            var view = new Rect(c.x, c.y + headH + 8f, c.width, c.yMax - (c.y + headH + 8f) - inputH - statusH - 6f);
            DrawChatLog(a, view, name);

            string status;
            Color statusColor = TextMuted;
            if (!HeroNarrator.Enabled) status = "";
            else if (!HeroNarrator.ChatOnline) status = Loc.S("Waiting for the local model  ·  Tools/local_ai/start_narrator.sh --no-game");
            else if (HeroNarrator.Replying && !HeroNarrator.ReplyStarted) status = HeroNarrator.ReplyWait > 3f ? Loc.F("{0} is taking a while…", name) : Loc.F("{0} is thinking…", name);
            else if (HeroNarrator.Replying) status = Loc.F("{0} is talking…", name);
            else if (!string.IsNullOrEmpty(HeroNarrator.LastChatError)) { status = HeroNarrator.LastChatError; statusColor = Alarm; }
            else status = Loc.F("ENTER send  ·  ESC close  ·  {0}", HeroNarrator.ServerLabel);
            var prevMicro = _micro.normal.textColor;
            _micro.normal.textColor = statusColor;
            FitLabel(new Rect(c.x, view.yMax + 3f, c.width, statusH), status, _micro);
            _micro.normal.textColor = prevMicro;

            DrawChatInput(a, new Rect(c.x, c.yMax - inputH, c.width, inputH), name);
        }

        private void DrawChatLog(SpecialistAgent a, Rect view, string name)
        {
            if (!HeroNarrator.Enabled)
            {
                string off = Loc.S("Hero lines are off. Conversations run on a small model on this computer — nothing leaves it.");
                float msgH = Mathf.Max(36f, _wrap.CalcHeight(new GUIContent(off), view.width - 40f));
                var msg = new Rect(view.x + 20f, view.y + view.height * 0.5f - 4f - msgH, view.width - 40f, msgH);
                GUI.Label(msg, off, _wrap);
                float onW = Mathf.Min(view.width - 20f, W(220f));
                if (Chip(new Rect(view.center.x - onW * 0.5f, msg.yMax + 6f, onW, L(28f)), Loc.S("TURN ON HERO LINES"), true))
                {
                    DemoSettings.HeroVoices = true;
                    DemoSettings.SaveSettings();
                    HeroNarrator.OpenChat(a);
                }
                return;
            }

            var lines = HeroNarrator.LogFor(a)?.Lines;
            int count = lines != null ? lines.Count : 0;
            bool replying = HeroNarrator.Replying;
            string pending = replying ? HeroNarrator.PendingReply(a) : null;
            if (replying && string.IsNullOrEmpty(pending)) pending = TypingDots();
            else if (replying) pending += (Time.unscaledTime * 2f) % 1f < 0.5f ? " |" : "";

            if (count == 0 && !replying)
            {
                GUI.Label(new Rect(view.x + 24f, view.y + view.height * 0.5f - 20f, view.width - 48f, 40f),
                    Loc.F("{0} glances up from the work. Ask about the job, the colony, or what it would take.", name), _chatHint);
                return;
            }

            float contentW = view.width - 12f;
            float total = 4f;
            for (int i = 0; i < count; i++)
                total = ChatBubble(total, contentW, lines[i].Text, lines[i].FromPlayer, name, false);
            if (replying) total = ChatBubble(total, contentW, pending, false, name, false);

            // Follow the conversation: jump to the newest line as it arrives and while it streams.
            if (count != _chatSeenLines || replying)
            {
                _chatScroll.y = total;
                _chatSeenLines = count;
            }
            _chatScroll = GUI.BeginScrollView(view, _chatScroll, new Rect(0f, 0f, contentW, total));
            float y = 4f;
            for (int i = 0; i < count; i++)
                y = ChatBubble(y, contentW, lines[i].Text, lines[i].FromPlayer, name, true);
            if (replying) ChatBubble(y, contentW, pending, false, name, true);
            GUI.EndScrollView();
        }

        /// <summary>Lays out (and optionally draws) one bubble; returns the y below it.</summary>
        private float ChatBubble(float y, float contentW, string text, bool player, string heroName, bool draw)
        {
            float maxText = contentW * 0.82f - 20f;
            var content = new GUIContent(text);
            float textW = Mathf.Min(maxText, _chatText.CalcSize(content).x + 2f);
            textW = Mathf.Max(textW, 60f);
            float textH = _chatText.CalcHeight(content, textW);
            float bw = textW + 20f;
            float nameH = L(12f);
            float bh = textH + nameH + 15f;
            if (draw)
            {
                float x = player ? contentW - bw : 0f;
                var r = new Rect(x, y, bw, bh);
                if (player) HudSkin.DrawPlate(r, HudSkin.Plate.RowOn, false);
                else HudSkin.Well(r);
                var who = _chatName;
                var prevColor = who.normal.textColor;
                var prevAlign = who.alignment;
                who.normal.textColor = player ? new Color(1f, 0.82f, 0.6f) : Gold;
                who.alignment = player ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
                GUI.Label(new Rect(x + 10f, y + 5f, bw - 20f, nameH), player ? Loc.S("THE CORE") : heroName.ToUpperInvariant(), who);
                who.normal.textColor = prevColor;
                who.alignment = prevAlign;
                GUI.Label(new Rect(x + 10f, y + 7f + nameH, textW, textH), text, _chatText);
            }
            return y + bh + 8f;
        }

        private static string TypingDots()
        {
            int n = 1 + (int)(Time.unscaledTime * 3f) % 3;
            return n == 1 ? "•" : n == 2 ? "• •" : "• • •";
        }

        private void DrawChatInput(SpecialistAgent a, Rect r, string name)
        {
            _chatFieldDrawn = true;
            float sendW = W(70f);
            var field = new Rect(r.x, r.y, r.width - sendW - 6f, r.height);
            bool canSend = HeroNarrator.ChatOnline && !HeroNarrator.Replying;
            var e = Event.current;
            bool focused = GUI.GetNameOfFocusedControl() == ChatControl;
            if (focused && e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    if (canSend) SendChat(a);
                    e.Use();
                }
                else if (e.keyCode == KeyCode.Escape)
                {
                    e.Use();
                    GUI.FocusControl(null);
                    CloseChat();
                    return;
                }
            }

            GUI.SetNextControlName(ChatControl);
            _chatInput = GUI.TextField(field, _chatInput ?? "", HeroConversation.MaxPlayerChars);
            if (string.IsNullOrEmpty(_chatInput))
                GUI.Label(new Rect(field.x + 9f, field.y, field.width - 16f, field.height), Loc.F("Say something to {0}…", name), _chatPlaceholder);
            if (_chatFocusPending)
            {
                GUI.FocusControl(ChatControl);
                _chatFocusPending = false;
            }

            bool ready = canSend && !string.IsNullOrWhiteSpace(_chatInput);
            var prev = GUI.color;
            if (!ready) GUI.color = new Color(prev.r, prev.g, prev.b, prev.a * 0.45f);
            if (GUI.Button(new Rect(r.xMax - sendW, r.y, sendW, r.height), Loc.S("SEND"), _chipOn) && ready)
                SendChat(a);
            GUI.color = prev;
        }

        private void SendChat(SpecialistAgent a)
        {
            if (!HeroNarrator.Say(a, _chatInput)) return;
            _chatInput = "";
            _chatFocusPending = true;
        }

        /// <summary>The hero's words over their head while they talk, fading a few seconds after.</summary>
        private void DrawSpeechBubble()
        {
            var a = _chatAgent;
            if (a == null || Camera.main == null) return;
            string text;
            float alpha = 1f;
            if (HeroNarrator.Replying)
            {
                text = HeroNarrator.PendingReply(a);
                if (string.IsNullOrEmpty(text)) text = TypingDots();
            }
            else
            {
                float since = HeroNarrator.SinceReply;
                if (since > 8f) return;
                var lines = HeroNarrator.LogFor(a)?.Lines;
                if (lines == null || lines.Count == 0 || lines[lines.Count - 1].FromPlayer) return;
                text = lines[lines.Count - 1].Text;
                alpha = Mathf.Clamp01((8f - since) / 1.5f);
            }

            Vector3 sp = Camera.main.WorldToScreenPoint(a.transform.position + Vector3.up * 3.2f);
            if (sp.z <= 0f) return;
            var tip = new Vector2(sp.x / _hudScale, (Screen.height - sp.y) / _hudScale);
            var content = new GUIContent(text);
            float tw = Mathf.Min(W(250f), _bubbleText.CalcSize(content).x + 2f);
            float th = _bubbleText.CalcHeight(content, tw);
            var r = new Rect(tip.x - (tw + 22f) * 0.5f, tip.y - th - 26f, tw + 22f, th + 16f);
            r.x = Mathf.Clamp(r.x, M, _sw - M - r.width);
            r.y = Mathf.Clamp(r.y, M, _sh - M - r.height);

            var prev = GUI.color;
            GUI.color = new Color(prev.r, prev.g, prev.b, prev.a * alpha);
            HudSkin.Panel(r, 0.95f, false);
            HudSkin.RuleH(new Rect(r.x + 6f, r.y, r.width - 12f, 1f), Gold);
            float tx = Mathf.Clamp(tip.x, r.x + 12f, r.xMax - 12f);
            var tail = new Color(HudSkin.GlassBottom.r, HudSkin.GlassBottom.g, HudSkin.GlassBottom.b, 0.9f);
            for (int i = 0; i < 5; i++)
                Fill(new Rect(tx - (5f - i), r.yMax + i * 2f, (5f - i) * 2f, 2f), tail);
            GUI.Label(new Rect(r.x + 11f, r.y + 8f, tw, th), text, _bubbleText);
            GUI.color = prev;
        }

        private static string FormatAction(SpecialistAgent a)
        {
            if (!string.IsNullOrEmpty(a.FlavorLine))
                return a.FlavorLine;
            string action = a.CurrentAction switch
            {
                SpecialistAction.PursueFlag => Loc.S("Working a flag"),
                SpecialistAction.Rest => Loc.S("Resting / shopping"),
                SpecialistAction.Flee => Loc.S("Fleeing to inn"),
                SpecialistAction.Hunt => Loc.S("Hunting fauna"),
                SpecialistAction.Repair => Loc.S("Repairing module"),
                SpecialistAction.Wander => a.LastReason != null && a.LastReason.Contains("workshop")
                    ? Loc.S("At workshop")
                    : a.LastReason != null && a.LastReason.Contains("levy_home")
                        ? Loc.S("Walking levy home")
                        : a.LastReason != null && a.LastReason.Contains("levy_collect")
                            ? Loc.S("Collecting HAB levy")
                            : Loc.S("Kingdom vocation"),
                _ => Loc.S("Idle")
            };
            return string.IsNullOrEmpty(a.Status) ? action : $"{action} — {a.Status}";
        }

        private static string LevyCarryLine(SpecialistAgent a)
        {
            return a != null && a.LevyCarry > 0 ? Loc.F(" · levy {0}", a.LevyCarry) : "";
        }

        private string FlagBoardLine()
        {
            int posted = 0;
            int claimed = 0;
            var flags = _loop.Flags != null ? _loop.Flags.Flags : null;
            if (flags != null)
            {
                posted = flags.Count;
                for (int i = 0; i < flags.Count; i++)
                {
                    if (flags[i] != null && flags[i].ClaimCount > 0)
                        claimed++;
                }
            }
            return posted <= 0
                ? Loc.S("Bounty escrowed from EU. Bots keep their EU share; the till takes its cut.")
                : Loc.F("Posted {0}  ·  claimed {1}  ·  bots keep their EU share", posted, claimed);
        }

        private static Color ActionTint(SpecialistAction action) => action switch
        {
            SpecialistAction.Hunt => new Color(0.95f, 0.45f, 0.32f),
            SpecialistAction.Flee => new Color(0.95f, 0.32f, 0.26f),
            SpecialistAction.PursueFlag => new Color(1f, 0.62f, 0.22f),
            SpecialistAction.Repair => new Color(0.55f, 0.85f, 1f),
            SpecialistAction.Rest => new Color(0.55f, 0.78f, 1f),
            _ => TextPrimary
        };

        private static Color ClassTint(SpecialistClass cls) => cls switch
        {
            SpecialistClass.EngineerBot => new Color(1f, 0.55f, 0.15f),
            SpecialistClass.DefenseMech => new Color(0.85f, 0.22f, 0.22f),
            SpecialistClass.Medic => new Color(0.55f, 0.9f, 0.7f),
            SpecialistClass.HarvesterBot => new Color(0.82f, 0.62f, 0.18f),
            SpecialistClass.SurveyorBot => new Color(0.45f, 0.82f, 0.95f),
            SpecialistClass.TerraformerBot => new Color(0.42f, 0.82f, 0.38f),
            SpecialistClass.CourierBot => new Color(0.95f, 0.72f, 0.28f),
            SpecialistClass.GeologistBot => new Color(0.68f, 0.52f, 0.32f),
            SpecialistClass.SentinelMech => new Color(0.78f, 0.38f, 0.22f),
            _ => new Color(0.35f, 0.85f, 1f)
        };

        private void DrawRoster()
        {
            if (_loop.SelectedStructure != null) return;
            if (_loop.SelectedAgents != null && _loop.SelectedAgents.Count > 0) return;

            var agents = _loop.Agents;
            int living = 0;
            if (agents != null)
            {
                for (int i = 0; i < agents.Count; i++)
                    if (agents[i] != null && agents[i].IsAlive) living++;
            }

            if (living <= 0)
            {
                string hintText = Loc.S("Build anywhere on open ground · chargers pay EU · a workshop fabricates a mech");
                float hintH = L(26f);
                float hintW = Mathf.Min(_micro.CalcSize(new GUIContent(hintText)).x + 46f, Mathf.Max(200f, DockLeft() - M - 8f + DockWidth() * 0.5f));
                var hint = new Rect(M, _contentBottom - 8f - hintH, hintW, hintH);
                _hitRects.Add(hint);
                HudSkin.Panel(hint, 0.9f, false);
                HudSkin.Icon(new Rect(hint.x + 9f, hint.center.y - 7f, 14f, 14f), UiIcons.Get(IconId.GlyphBuild), Gold);
                FitLabel(new Rect(hint.x + 30f, hint.y, hint.width - 36f, hint.height), hintText, _micro);
                return;
            }

            int rows = Mathf.Min(8, living);
            float rowH = L(16f);
            float h = L(44f) + rows * (rowH + 2f);
            var rect = new Rect(M, _contentBottom - 8f - h, W(268f), h);
            var c = Panel(rect, Loc.S("Roster · status"), true, HudTips.Roster());
            float y = c.y;
            FitLabel(new Rect(c.x, y, c.width, L(14f)), ClassReadout(agents), _micro);
            y += L(16f);
            int drawn = 0;
            float classW = W(66f), statusW = W(44f);
            for (int i = 0; i < agents.Count && drawn < rows; i++)
            {
                var a = agents[i];
                if (a == null || !a.IsAlive) continue;
                var cls = a.Data != null ? a.Data.specialistClass : SpecialistClass.ScoutDrone;
                string status = RosterStatus(a);
                var row = new Rect(c.x, y, c.width, rowH);
                if (GUI.Button(row, GUIContent.none, _rowOff))
                    _loop.SelectOnly(a);
                HudSkin.Dot(new Vector2(row.x + 6f, row.center.y), 6f, ClassTint(cls));
                FitLabel(new Rect(row.x + 14f, row.y, classW, rowH),
                    Loc.F("{0} L{1}", ColonyStructure.ClassLabel(cls), a.Level), _micro);
                FitLabel(new Rect(row.x + 14f + classW, row.y, statusW, rowH), status, _body);
                float flavorX = 14f + classW + statusW + 2f;
                FitLabel(new Rect(row.x + flavorX, row.y, row.width - flavorX, rowH), a.FlavorLine, _micro);
                y += rowH + 2f;
                drawn++;
            }
        }

        private static string ClassReadout(IReadOnlyList<SpecialistAgent> agents)
        {
            int sct = 0, eng = 0, def = 0, med = 0, extra = 0;
            if (agents != null)
            {
                for (int i = 0; i < agents.Count; i++)
                {
                    var a = agents[i];
                    if (a == null || !a.IsAlive) continue;
                    var cls = a.Data != null ? a.Data.specialistClass : SpecialistClass.ScoutDrone;
                    switch (cls)
                    {
                        case SpecialistClass.ScoutDrone: sct++; break;
                        case SpecialistClass.EngineerBot: eng++; break;
                        case SpecialistClass.DefenseMech: def++; break;
                        case SpecialistClass.Medic: med++; break;
                        default: extra++; break;
                    }
                }
            }
            string line = Loc.F("SCT {0}  ENG {1}  DEF {2}  MED {3}", sct, eng, def, med);
            return extra > 0 ? line + $"  +{extra}" : line;
        }

        private static string RosterStatus(SpecialistAgent a)
        {
            if (a.IsIncapacitated) return Loc.S("DOWN");
            switch (a.CurrentAction)
            {
                case SpecialistAction.PursueFlag: return Loc.S("WORK");
                case SpecialistAction.Rest: return Loc.S("REST");
                case SpecialistAction.Hunt: return Loc.S("HUNT");
                case SpecialistAction.Flee: return Loc.S("FLEE");
                case SpecialistAction.Repair: return Loc.S("FIX");
                default: return Loc.S("IDLE");
            }
        }

        private void DrawMinimap()
        {
            float size = MapSize;
            var rect = new Rect(_sw - M - size, _sh - M - size - 16f, size, size + 16f);
            _hitRects.Add(rect);

            // Round dial: survey disc under a brass bezel. The disc runs slightly under the tick ring.
            var dial = new Rect(rect.x, rect.y, size, size);
            HudSkin.Shadow(HudSkin.Expand(dial, -18f), 0.5f);
            var disc = HudSkin.Expand(dial, -size * 0.07f);
            HudSkin.Tex(disc, HudSkin.MinimapDisc, Color.white);
            Vector2 centre = disc.center;
            float clip = disc.width * 0.5f - 4f;

            float worldW = _loop.Grid != null ? _loop.Grid.WorldWidth : 384f;
            float worldH = _loop.Grid != null ? _loop.Grid.WorldHeight : 384f;
            Vector2 MapPoint(Vector3 world)
            {
                float u = Mathf.Clamp01(world.x / worldW);
                float v = Mathf.Clamp01(world.z / worldH);
                return new Vector2(disc.x + u * disc.width, disc.yMax - v * disc.height);
            }

            void Pip(Vector3 world, Color color, float s, bool glow = false)
            {
                Vector2 p = MapPoint(world);
                if ((p - centre).sqrMagnitude > clip * clip) return;
                if (glow) HudSkin.SoftDot(p, s * 3.2f, new Color(color.r, color.g, color.b, 0.45f));
                HudSkin.Dot(p, s + 1f, color);
            }

            Pip(ColonyLayout.CampusOrigin, Accent, 6f, true);
            Pip(ColonyLayout.CampusBOrigin, new Color(0.35f, 0.85f, 1f), 5f, true);

            var world = _loop.World;
            if (world != null)
            {
                var lairs = world.Lairs;
                for (int i = 0; i < lairs.Count; i++)
                {
                    var l = lairs[i];
                    if (l == null || l.IsCleared || !l.IsScouted) continue;
                    Pip(l.WorldPosition, new Color(0.35f, 0.9f, 1f), 4f);
                }
            }

            var pieces = _loop.Placer != null ? _loop.Placer.Pieces : null;
            if (pieces != null && _loop.Grid != null)
            {
                for (int i = 0; i < pieces.Count; i++)
                {
                    var piece = pieces[i];
                    Vector3 a = _loop.Grid.CellToWorld(piece.Origin);
                    Vector3 b = _loop.Grid.CellToWorld(
                        piece.Origin + new Vector2Int(piece.Width - 1, piece.Height - 1));
                    Vector3 mid = (a + b) * 0.5f;
                    Color col = piece.Category == BuildingCategory.Commons
                        ? HudSkin.GoldBright
                        : new Color(0.90f, 0.91f, 0.93f);
                    Pip(mid, col, 4.5f);
                }
            }

            var flags = _loop.Flags != null ? _loop.Flags.Flags : null;
            if (flags != null)
            {
                for (int i = 0; i < flags.Count; i++)
                {
                    var f = flags[i];
                    if (f == null) continue;
                    Color col = f.Data != null ? f.Data.bannerColor : Gold;
                    Pip(f.WorldPosition, col, 4f, true);
                }
            }

            var agents = _loop.Agents;
            if (agents != null)
            {
                for (int i = 0; i < agents.Count; i++)
                {
                    var a = agents[i];
                    if (a == null || !a.IsAlive) continue;
                    Pip(a.transform.position, new Color(0.45f, 0.85f, 1f), 3f);
                }
            }

            var stalkers = _loop.Stalkers;
            if (stalkers != null)
            {
                for (int i = 0; i < stalkers.Count; i++)
                {
                    var s = stalkers[i];
                    if (s == null) continue;
                    Pip(s.transform.position, Alarm, 3f, true);
                }
            }

            if (Camera.main != null)
            {
                var ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                if (Mathf.Abs(ray.direction.y) > 0.01f)
                {
                    float t = -ray.origin.y / ray.direction.y;
                    Vector2 p = MapPoint(ray.origin + ray.direction * t);
                    if ((p - centre).sqrMagnitude <= clip * clip)
                    {
                        HudSkin.Ring(new Rect(p.x - 7f, p.y - 7f, 14f, 14f), new Color(1f, 1f, 1f, 0.9f));
                        HudSkin.Dot(p, 3f, Color.white);
                    }
                }
            }

            HudSkin.Tex(dial, HudSkin.MinimapBezel, Color.white);
            HudSkin.ShadowLabel(new Rect(rect.x, rect.yMax - 13f, rect.width, 12f), Loc.S("MAJESTY COLONY"), _capsCenter, 0.9f);
            Tooltip(dial, HudTips.Minimap());

            var e = Event.current;
            Vector2 mouse = e.mousePosition;
            mouse.x /= _hudScale;
            mouse.y /= _hudScale;
            if (e.type == EventType.MouseDown && e.button == 0 && (mouse - centre).sqrMagnitude <= clip * clip)
            {
                float u = (mouse.x - disc.x) / disc.width;
                float v = 1f - (mouse.y - disc.y) / disc.height;
                var glanceWorld = new Vector3(u * worldW, 0f, v * worldH);
                _loop.GlanceAt(glanceWorld, force: true);
                e.Use();
            }
        }

        private void DrawConstructionPanel()
        {
            if (_loop.Placer == null || _loop.Placer.Orders == null || _loop.Placer.Orders.Count == 0)
                return;

            int rows = Mathf.Min(4, _loop.Placer.Orders.Count);
            float rowStep = L(16f) + 10f;
            float h = L(40f) + rows * rowStep;
            float mapTop = _sh - M - MapSize;
            float panelW = W(286f);
            var rect = new Rect(_sw - M - panelW, mapTop - 8f - h, panelW, h);
            var c = Panel(rect, Loc.S("Construction"), true, HudTips.Construction());

            float y = c.y;
            float pctW = W(44f);
            for (int i = 0; i < _loop.Placer.Orders.Count && i < rows; i++)
            {
                var o = _loop.Placer.Orders[i];
                if (o == null) continue;
                float p = o.RequiredSeconds > 0f ? Mathf.Clamp01(o.ProgressSeconds / o.RequiredSeconds) : 1f;
                FitLabel(new Rect(c.x, y, c.width - pctW - 2f, L(14f)), o.Data != null ? Loc.S(o.Data.displayName) : Loc.S("Building"), _action);
                GUI.Label(new Rect(c.xMax - pctW, y, pctW, L(14f)), $"{p * 100f:F0}%", _microRight);
                Meter(new Rect(c.x, y + L(16f), c.width, 5f), p, Accent);
                y += rowStep;
            }
        }

        private void DrawToast()
        {
            if (StillCaptureHold.Active) return;
            float now = Time.unscaledTime;
            if (string.IsNullOrEmpty(_toast) || now > _toastUntil)
            {
                _toast = null;
                return;
            }

            // Ease in from above, fade out at the end; Reduce Motion keeps it still.
            float enter = Mathf.Clamp01((now - _toastStart) / 0.25f);
            float fade = Mathf.Clamp01((_toastUntil - now) / 0.45f) * (DemoSettings.ReduceMotion ? 1f : enter);
            float slide = DemoSettings.ReduceMotion ? 0f : (1f - enter) * (1f - enter) * -10f;

            float left = M + 10f;
            float right = _sw - M - HudLayout.ObjectivesWidth - 10f;
            float w = Mathf.Min(W(460f), Mathf.Max(220f, right - left));
            float x = Mathf.Clamp((_sw - w) * 0.5f, left, Mathf.Max(left, right - w));
            float textH = Mathf.Clamp(_wrap.CalcHeight(new GUIContent(_toast), w - 48f), 16f, L(64f));
            float top = _loop.Screen == DemoScreen.Playing && _loop.IsTutorialActive ? _tutorialBottom + 8f : M;
            var rect = new Rect(x, top + slide, w, textH + 20f);
            _hitRects.Add(rect);

            var prevColor = GUI.color;
            GUI.color = new Color(prevColor.r, prevColor.g, prevColor.b, prevColor.a * fade);
            HudSkin.Panel(rect, 1f, false);
            HudSkin.RuleH(new Rect(rect.x, rect.y, rect.width, 1f), Accent);
            var dot = new Vector2(rect.x + 18f, rect.y + 10f + Mathf.Min(textH, L(16f)) * 0.5f);
            HudSkin.SoftDot(dot, 20f, new Color(Accent.r, Accent.g, Accent.b, 0.55f));
            HudSkin.Dot(dot, 6f, HudSkin.GoldBright);
            var prevText = _wrap.normal.textColor;
            _wrap.normal.textColor = TextPrimary;
            GUI.Label(new Rect(rect.x + 34f, rect.y + 10f, rect.width - 46f, textH), _toast, _wrap);
            _wrap.normal.textColor = prevText;
            GUI.color = prevColor;
        }

        private void DrawCutsceneModal()
        {
            if (StillCaptureHold.Active) return;
            if (_loop == null || !_loop.TryPeekCutscene(out var cut)) return;

            HudSkin.Vignette(new Rect(0, 0, _sw, _sh), new Color(0.02f, 0.03f, 0.05f), 0.35f);
            _hitRects.Add(new Rect(0f, 0f, _sw, _sh));

            // Lines wrap and the card grows to fit; a fixed one-row label clipped longer story lines.
            float cardW = Mathf.Min(W(540f), _sw - M * 2f);
            if (_bodyWrap == null) _bodyWrap = new GUIStyle(_body) { wordWrap = true, alignment = TextAnchor.UpperLeft };
            float textW = cardW - Pad * 2f;
            string[] bodyText = cut.BodyText;
            int lines = bodyText != null ? bodyText.Length : 0;
            float bodyH = 0f;
            for (int i = 0; i < lines; i++)
                bodyH += Mathf.Max(18f, _bodyWrap.CalcHeight(new GUIContent(bodyText[i]), textW)) + 10f;
            float h = 100f + HudTextScale.Grow(28f + 30f) + bodyH;
            var rect = new Rect((_sw - cardW) * 0.5f, _sh * 0.22f, cardW, h);
            var c = Panel(rect, null, false);
            HudSkin.ModalChrome(rect, Gold);
            _hitRects.Add(rect);

            var prev = _banner.normal.textColor;
            _banner.normal.textColor = HudSkin.GoldBright;
            FitLabel(new Rect(c.x, c.y, c.width, L(28f)), cut.Title, _banner);
            _banner.normal.textColor = prev;

            float y = c.y + L(28f) + 6f;
            for (int i = 0; i < lines; i++)
            {
                float lineH = Mathf.Max(18f, _bodyWrap.CalcHeight(new GUIContent(bodyText[i]), c.width));
                GUI.Label(new Rect(c.x, y + 5f, c.width, lineH), cut.Body[i], _bodyWrap);
                y += lineH + 10f;
            }

            if (GUI.Button(new Rect(c.x, c.yMax - L(30f), W(180f), L(30f)), Loc.S("CONTINUE  ·  SPACE"), _chipOn))
                _loop.DismissCutscene();

            if (Event.current.type == EventType.KeyDown &&
                (Event.current.keyCode == KeyCode.Space || Event.current.keyCode == KeyCode.Return))
            {
                _loop.DismissCutscene();
                Event.current.Use();
            }
        }

        private bool TryDrawCampaignVictory()
        {
            if (StillCaptureHold.Active || ReplayRules.IsEndless || _winDismissed) return false;
            var mission = _loop.Mission;
            if (mission == null || !mission.IsWon) return false;
            if (DemoEndOffer.Applies(_loop.ActiveBody, won: true, endless: false))
            {
                if (!_loop.VictoryHeld) return false; // KEEP PLAYING EARTH released it
                DrawDemoEnd(); // Steam demo: OverseerHud.DemoEnd.cs
                return true;
            }
            bool nextBuilt = VictoryOffer.NextIsBuilt(_loop.ActiveBody);
            DrawCampaignVictory(VictoryOffer.For(_loop.ActiveBody, nextBuilt, endless: false));
            return true;
        }

        private void DrawWinBanner()
        {
            if (StillCaptureHold.Active || !ReplayRules.IsEndless) return;
            var mission = _loop.Mission;
            if (mission == null || !mission.IsWon || _winDismissed) return;
            DrawEndlessWinBanner(mission);
        }

        private void DrawCampaignVictory(VictoryOffer.View offer)
        {
            HudSkin.Vignette(new Rect(0, 0, _sw, _sh), new Color(0.02f, 0.05f, 0.03f), 0.42f);
            _hitRects.Add(new Rect(0f, 0f, _sw, _sh));

            var rect = VictoryOffer.PanelRect(_sw, _sh);
            var c = Panel(rect, null, false);
            HudSkin.ModalChrome(rect, Good);

            var prev = _banner.normal.textColor;
            _banner.normal.textColor = Good;
            FitLabel(new Rect(c.x, c.y, c.width, L(26f)), offer.Headline, _banner);
            _banner.normal.textColor = prev;

            float buttonH = L(30f);
            float buttonY = c.yMax - buttonH;
            float textBottom = buttonY - 8f;
            if (!string.IsNullOrEmpty(offer.ComingSoon))
            {
                textBottom -= L(22f);
                var soonPrev = _body.normal.textColor;
                _body.normal.textColor = HudSkin.GoldBright;
                FitLabel(new Rect(c.x, textBottom, c.width, L(20f)), offer.ComingSoon, _body);
                _body.normal.textColor = soonPrev;
            }

            float bodyTop = c.y + L(26f) + 4f;
            float bodyH = Mathf.Max(16f, textBottom - bodyTop);
            GUI.Label(new Rect(c.x, bodyTop, c.width, bodyH), offer.Body, _wrap != null ? _wrap : _body);

            float gap = 8f;
            if (offer.CanContinue && offer.ShowMainMenu)
            {
                float w = (c.width - gap) * 0.5f;
                if (GUI.Button(new Rect(c.x, buttonY, w, buttonH), offer.ContinueLabel, _chipOn))
                    _loop.ContinueAfterVictory();
                if (GUI.Button(new Rect(c.x + w + gap, buttonY, w, buttonH), VictoryOffer.MainMenu, _chipOff))
                {
                    _winDismissed = true;
                    _loop.ExitVictoryToTitle();
                }
            }
            else if (offer.ShowMainMenu)
            {
                float w = Mathf.Min(W(180f), c.width);
                if (GUI.Button(new Rect(c.x, buttonY, w, buttonH), VictoryOffer.MainMenu, _chipOn))
                {
                    _winDismissed = true;
                    _loop.ExitVictoryToTitle();
                }
            }
        }

        private void DrawEndlessWinBanner(MissionController mission)
        {
            HudSkin.Vignette(new Rect(0, 0, _sw, _sh), new Color(0.02f, 0.05f, 0.03f), 0.35f);
            _hitRects.Add(new Rect(0f, 0f, _sw, _sh));

            var rect = VictoryOffer.PanelRect(_sw, _sh);
            var c = Panel(rect, null, false);
            HudSkin.ModalChrome(rect, Good);

            var prev = _banner.normal.textColor;
            _banner.normal.textColor = Good;
            FitLabel(new Rect(c.x, c.y, c.width, L(26f)), mission.WinHeadline, _banner);
            _banner.normal.textColor = prev;

            float detailTop = c.y + L(26f) + 6f;
            GUI.Label(new Rect(c.x, detailTop, c.width, c.yMax - L(30f) - 6f - detailTop), mission.WinDetail, _wrap != null ? _wrap : _body);
            if (GUI.Button(new Rect(c.x, c.yMax - L(30f), Mathf.Min(W(220f), c.width), L(28f)),
                    Loc.F("CONTINUE OVERSEEING  ·  {0}", InputBindings.Hint(GameAction.PayYard).ToUpperInvariant()), _chipOn))
            {
                _winDismissed = true;
                mission.DismissWinToSandbox();
            }
        }

        private void DrawFailBanner()
        {
            if (StillCaptureHold.Active) return;

            var mission = _loop.Mission;
            bool lost = mission != null && mission.IsLost;
            bool deadline = lost && mission.WasDeadlineFail && !_deadlineDismissed;
            bool field = !lost && _loop.NeedsFieldRevive;
            if (!lost && !field) return;

            if (!_failLatched)
            {
                _failLatched = true;
                // A downed field party is recoverable; only a lost colony gets the stinger and eulogy.
                bool sting = lost && AdaptiveMusic.PlayStinger(false);
                if (!sting) DemoAudio.PlayFail();
                if (lost) OverseerVoice.SpeakCue(VoiceCue.Defeat, AlertSeverity.Critical, sting ? 4.2f : 1f);
            }

            HudSkin.Vignette(new Rect(0, 0, _sw, _sh), new Color(0.10f, 0.01f, 0.01f), 0.4f);
            _hitRects.Add(new Rect(0f, 0f, _sw, _sh));

            float bw = Mathf.Min(W(460f), _sw - M * 2f);
            var rect = new Rect((_sw - bw) * 0.5f, _sh * 0.32f, bw, L(180f));
            var c = Panel(rect, null, false);
            HudSkin.ModalChrome(rect, Alarm);

            var prev = _banner.normal.textColor;
            _banner.normal.textColor = Alarm;
            FitLabel(new Rect(c.x, c.y, c.width, L(26f)), mission != null ? mission.FailHeadline : Loc.S("OUTPOST LOST"), _banner);
            _banner.normal.textColor = prev;

            float btnH = L(28f);
            float btnY = c.yMax - L(30f);
            if (deadline || lost)
            {
                FitLabel(new Rect(c.x, c.y + L(32f), c.width, L(32f)),
                    mission != null ? mission.FailDetail : Loc.S("The outpost is gone."), _body);
                if (ReplayRules.BlocksManualSavesAndReloads)
                {
                    FitLabel(new Rect(c.x, c.y + L(66f), c.width, L(16f)),
                        Loc.S("Ironman — this body is lost. No restart."), _muted);
                    var title = new Rect(c.x, btnY, W(140f), btnH);
                    if (GUI.Button(title, Loc.S("TITLE"), _chipOff))
                        _loop.ReturnToTitle();
                    Tooltip(title, HudTips.ToTitle());
                }
                else
                {
                    FitLabel(new Rect(c.x, c.y + L(66f), c.width, L(16f)),
                        ReplayRules.IsEndless ? Loc.S("TRY AGAIN — this body is lost.") : Loc.S("Retry this body, or return to title."), _muted);
                    var restart = new Rect(c.x, btnY, W(160f), btnH);
                    if (GUI.Button(restart, Loc.S("RESTART MISSION"), _chipOn))
                        _loop.RestartMission();
                    var title = new Rect(restart.xMax + 16f, btnY, W(140f), btnH);
                    if (GUI.Button(title, Loc.S("TITLE"), _chipOff))
                        _loop.ReturnToTitle();
                    Tooltip(title, HudTips.ToTitle());
                }
            }
            else
            {
                int met = _loop.FieldReviveMet;
                FitLabel(new Rect(c.x, c.y + L(32f), c.width, L(36f)),
                    Loc.F("FOBOT YARD  {0} EU  (120s)", met), _body);
                FitLabel(new Rect(c.x, c.y + L(70f), c.width, L(16f)),
                    !_loop.HasFobotYard
                        ? Loc.F("Dock a Fobot Yard. {0} does not skip the building.", InputBindings.Hint(GameAction.PayYard))
                        : _loop.FieldReviveReadyIn > 0.5f
                            ? Loc.F("Cooldown {0:F0}s. Bill is by level, EU only.", _loop.FieldReviveReadyIn)
                            : Loc.F("Inspect the yard or press {0}. ICE is not on the bill.", InputBindings.Hint(GameAction.PayYard)), _muted);
                var pay = new Rect(c.x, btnY, W(220f), btnH);
                if (GUI.Button(pay, Loc.F("FOBOT YARD  ·  {0}", InputBindings.Hint(GameAction.PayYard).ToUpperInvariant()), _chipOn))
                    _loop.PayFobotYard();
                Tooltip(pay, HudTips.PayYard());
            }
        }

        private void Update()
        {
            if (_loop == null || !_loop.IsPlaying) return;
            bool cutsceneUp = _loop.TryPeekCutscene(out _);
            if ((_loop.NeedsFieldRevive || _loop.CanPayYard) &&
                !(_loop.Mission != null && _loop.Mission.IsLost) &&
                InputBindings.Down(GameAction.PayYard))
                _loop.PayFobotYard();
            if (!_loop.NeedsFieldRevive &&
                !(_loop.Mission != null && _loop.Mission.IsLost))
                _failLatched = false;

            if (_loop.TryPeekCutscene(out _) &&
                (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)))
            {
                _loop.DismissCutscene();
            }

            if (ReplayRules.IsEndless &&
                _loop.Mission != null && _loop.Mission.IsWon &&
                InputBindings.Down(GameAction.PayYard) && !_loop.IsOutpostOverwhelmed)
            {
                _winDismissed = true;
                _loop.Mission.DismissWinToSandbox();
            }

            // Enter talks to the one selected hero (not while typing, a card is up, or a cut plays).
            if (ReferenceEquals(_chatAgent, null) && !InputBindings.TextEntryActive && _loop.SelectedStructure == null &&
                (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
                 InputBindings.Down(GameAction.Confirm)) &&
                !cutsceneUp && !_loop.TryPeekCutscene(out _))
            {
                var sel = _loop.SelectedAgents;
                if (sel != null && sel.Count == 1 && sel[0] != null) OpenChat(sel[0]);
            }

            if (_loop.FocusedCampus != _lastFocusToast)
            {
                _lastFocusToast = _loop.FocusedCampus;
                Toast(Loc.F("Focus → {0}", ColonyLayout.CampusLabel(_lastFocusToast)), 2.2f);
            }
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "—";
            return s.Length <= max ? s : s.Substring(0, max - 1) + "…";
        }

        private readonly List<(CelestialBodyId body, Vector2 screen, float radius)> _titleAnchors =
            new List<(CelestialBodyId body, Vector2 screen, float radius)>(8);

        /// <summary>
        /// Title is the orrery itself: worlds are labelled where they orbit and picked by clicking
        /// them. Only a wordmark and a small menu sit over the scene, out of the planets' way.
        /// </summary>
        private void DrawTitle()
        {
            const string word = "SOLAR MAJESTY";
            float wx = M + 14f;
            float wy = M + 10f;
            var prevCaps = _caps.normal.textColor;
            var wordColor = GUI.color;
            var wordMatrix = GUI.matrix;
            var wordEnabled = GUI.enabled;
            try
            {
                HudSkin.Band(new Rect(wx - 120f, wy - 20f, 820f, 130f), 0.35f);
                HudSkin.ShadowLabel(new Rect(wx, wy, 700f, 52f), word, _titleWord, 0.55f);
                float ww = _titleWord.CalcSize(new GUIContent(word)).x;
                HudSkin.Pill(new Rect(wx + 1f, wy + 56f, 28f, 3f), Accent);
                HudSkin.RuleH(new Rect(wx + 34f, wy + 57f, Mathf.Max(80f, ww - 34f), 1f), new Color(Gold.r, Gold.g, Gold.b, 0.9f));
                HudSkin.ShadowLabel(new Rect(wx + 1f, wy + 66f, Mathf.Min(W(620f), _sw - wx - M), L(18f)),
                    DemoSettings.FirstHourDemo
                        ? Loc.S("Click Earth to land. You are the Core. Your bots do the work.")
                        : Loc.S("Click a world to land. You are the Core. Your bots do the work."),
                    _tagline, 0.7f);
                _caps.normal.textColor = Gold;
                HudSkin.ShadowLabel(new Rect(wx + 1f, wy + 68f + L(18f), W(400f), L(14f)), ReplayRules.HudTag, _caps, 0.7f);
            }
            catch (ExitGUIException) { throw; }
            catch (System.Exception e) { HudSafety.LogOnce("TitleWordmark:" + e.Message, e); }
            finally
            {
                _caps.normal.textColor = prevCaps;
                GUI.color = wordColor;
                GUI.matrix = wordMatrix;
                GUI.enabled = wordEnabled;
            }

            var savedColor = GUI.color;
            var savedMatrix = GUI.matrix;
            var savedEnabled = GUI.enabled;
            try
            {
                DrawTitlePlanetLabels();
            }
            catch (ExitGUIException) { throw; }
            catch (System.Exception e) { HudSafety.LogOnce("TitlePlanetLabels:" + e.Message, e); }
            finally
            {
                GUI.color = savedColor;
                GUI.matrix = savedMatrix;
                GUI.enabled = savedEnabled;
            }

            if (_confirmNewGame || _titleLoad)
            {
                DrawCampaignDialog(_confirmNewGame);
                DrawToast();
                return;
            }

            float bw = W(240f);
            float bh = L(34f);
            float x = M + 14f;
            float y = _sh - M - 8f - bh;
            if (TitleButton(new Rect(x, y, bw, bh), Loc.S("QUIT"), false, HudTips.Quit()))
                _loop.QuitDemo();
            y -= bh + 6f;
            if (TitleButton(new Rect(x, y, bw, bh), Loc.S("SETTINGS"), false, HudTips.OpenSettings()))
                _loop.OpenSettings();
            y -= bh + 6f;
            if (TitleButton(new Rect(x, y, bw, bh), Loc.S("LOAD"), false, HudTips.LoadGame()))
                _titleLoad = true;
            y -= bh + 6f;
            if (TitleButton(new Rect(x, y, bw, bh), Loc.S("NEW CAMPAIGN"), !DemoSettings.SaveExists, HudTips.NewCampaign()))
            {
                _pendingNewGameBody = CampaignProgress.NewGameBody;
                _pendingUnlockThrough = false;
                _newCampaignSlot = 1;
                _confirmNewGame = true;
            }
            if (DemoSettings.SaveExists)
            {
                y -= L(18f);
                FitLabel(new Rect(x + 2f, y, Mathf.Min(W(420f), _sw - x - M), L(14f)), DemoSettings.ContinueDetail(), _micro);
                y -= bh + 4f;
                if (TitleButton(new Rect(x, y, bw + W(80f), bh), DemoSettings.ContinueButtonLabel(), true, HudTips.Continue()))
                    _loop.ContinueGame();
            }

            DrawToast();
        }

        private bool TitleButton(Rect r, string label, bool primary, HudTip tip)
        {
            Tooltip(r, tip);
            _hitRects.Add(r);
            bool hot = r.Contains(Event.current.mousePosition);
            NoteHover(r, hot);
            var savedColor = GUI.color;
            var savedMatrix = GUI.matrix;
            var savedEnabled = GUI.enabled;
            try
            {
                if (primary)
                {
                    HudSkin.Glow(r, new Color(Accent.r, Accent.g, Accent.b, hot ? 0.55f : 0.35f), 0.6f);
                    HudSkin.DrawPlate(r, HudSkin.Plate.Primary, hot);
                    HudSkin.Pill(new Rect(r.x + 7f, r.y + 9f, 3f, r.height - 18f), new Color(Ink.r, Ink.g, Ink.b, 0.55f));
                }
                else
                {
                    HudSkin.Panel(r, hot ? 0.95f : 0.72f, false);
                    HudSkin.Pill(new Rect(r.x + 7f, r.y + 9f, 3f, r.height - 18f),
                        hot ? Accent : new Color(Gold.r, Gold.g, Gold.b, 0.45f));
                    if (hot) HudSkin.RuleH(new Rect(r.x, r.yMax - 1f, r.width, 1f), Gold);
                }
            }
            catch (ExitGUIException) { throw; }
            catch (System.Exception e) { HudSafety.LogOnce("TitleButton:" + e.Message, e); }
            finally
            {
                GUI.color = savedColor;
                GUI.matrix = savedMatrix;
                GUI.enabled = savedEnabled;
            }
            var style = primary ? _titleButtonOn : _titleButton;
            return GUI.Button(r, FitChipText(style, new[] { label }, r.width), style);
        }

        private void DrawTitlePlanetLabels()
        {
            var view = SolarSystemTitleView.Instance;
            if (view == null) return;
            view.GetBodyAnchors(_titleAnchors);
            var hovered = view.HoveredBody;
            bool shift = ShiftHeld();
            float inv = 1f / Mathf.Max(0.01f, _hudScale);
            foreach (var (body, screen, radius) in _titleAnchors)
            {
                if (!DemoSlice.ShowBody(body)) continue;
                var profile = CelestialBodyCatalog.Get(body);
                if (profile == null) continue;
                Vector2 p = screen * inv;
                float r = radius * inv;
                bool hot = hovered.HasValue && hovered.Value == body;
                bool unlocked = CampaignProgress.IsUnlocked(body);
                bool demoLocked = DemoSettings.FirstHourDemo && body != CelestialBodyId.Earth;

                string tag;
                if (demoLocked) tag = Loc.S("AFTER EARTH");
                else if (!unlocked) tag = CampaignProgress.IsParked(body) ? Loc.S("PARKED") : Loc.S("LOCKED");
                else if (DemoSettings.SaveExists && body == _loop.ActiveBody) tag = Loc.S("YOUR COLONY");
                else tag = "";

                if (hot)
                {
                    if (demoLocked) tag = Loc.S("CLEAR EARTH FIRST  ·  OR SKIP IN SETTINGS");
                    else if (!unlocked)
                        tag = !DevTools.Enabled ? Loc.S("LOCKED") : shift ? Loc.S("CLICK TO UNLOCK") : Loc.S("LOCKED  ·  SHIFT+CLICK");
                    else if (DemoSettings.SaveExists && body == _loop.ActiveBody) tag = Loc.S("CLICK TO CONTINUE");
                    else tag = Loc.S("CLICK TO LAND");
                }

                float y = p.y + r + 6f;
                var nameRect = new Rect(p.x - 90f, y, 180f, L(16f));
                _planetName.normal.textColor = hot ? Color.white
                    : (unlocked && !demoLocked ? TextPrimary : TextMuted);
                HudSkin.ShadowLabel(nameRect, profile.DisplayNameText.ToUpperInvariant(), _planetName, 0.85f);
                if (!string.IsNullOrEmpty(tag))
                {
                    _planetTag.normal.textColor = hot ? Accent : TextMuted;
                    HudSkin.ShadowLabel(new Rect(p.x - 160f, y + L(15f), 320f, L(13f)), tag, _planetTag, 0.85f);
                }
                if (hot)
                    HudSkin.RuleH(new Rect(p.x - 30f, y - 2f, 60f, 1.5f), Accent);
            }
        }

        private float _dialogMeasuredH;

        private void DrawCampaignDialog(bool newCampaign)
        {
            // The dialog hugs what it drew last pass (wrapped copy, slot count, text size).
            float h = Mathf.Max(newCampaign ? 400f : 360f, _dialogMeasuredH);
            h = Mathf.Min(h, _sh - 20f);
            float dw = Mathf.Min(W(460f), _sw - M * 2f);
            var rect = new Rect((_sw - dw) * 0.5f, Mathf.Max(10f, (_sh - h) * 0.5f), dw, h);
            var c = Panel(rect, newCampaign ? Loc.S("New campaign") : Loc.S("Load"));
            HudSkin.CornerTicks(rect, new Color(Gold.r, Gold.g, Gold.b, 0.75f));
            float y = WrapBlock(c, c.y, newCampaign
                ? Loc.F("Drop on {0}. Only the chosen slot is replaced. Other saves stay. Autosave will follow this new colony — save the current one first if you still need it.", _pendingNewGameBody)
                : Loc.S("Load a saved colony. Other slots stay on disk."));
            var slots = SaveSystem.ListSlots();
            float slotH = L(28f);
            for (int i = 0; i < slots.Count; i++)
            {
                var info = slots[i];
                if (newCampaign && info.Slot == SaveSystem.AutosaveSlot) continue;
                bool selected = newCampaign && info.Slot == _newCampaignSlot;
                if (Btn(new Rect(c.x, y, c.width, slotH), SlotButtonLabel(info), selected ? _chipOn : _chipOff))
                {
                    if (newCampaign)
                        _newCampaignSlot = info.Slot;
                    else if (info.Occupied)
                        _loop.LoadFromSlot(info.Slot);
                    else
                        Notify(Loc.S("That slot is empty."), 2.2f);
                }
                y += slotH + 6f;
            }
            y += 6f;
            if (newCampaign && Btn(new Rect(c.x, y, c.width, L(32f)),
                    Loc.F("START IN SLOT {0}", _newCampaignSlot), _chipOn))
            {
                int slot = _newCampaignSlot;
                var body = _pendingNewGameBody;
                bool unlock = _pendingUnlockThrough;
                _confirmNewGame = false;
                if (unlock)
                    _loop.StartNewGameOn(body, slot);
                else
                    _loop.StartNewGame(body, slot);
            }
            if (!newCampaign) y -= 4f;
            float backY = newCampaign ? y + L(32f) + 8f : y;
            if (Btn(new Rect(c.x, backY, c.width, L(28f)), Loc.S("BACK  ·  Esc"), _chipOff, HudTips.Back()))
            {
                _confirmNewGame = false;
                _titleLoad = false;
            }
            _dialogMeasuredH = backY + L(28f) + Pad - rect.y;
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                _confirmNewGame = false;
                _titleLoad = false;
                Event.current.Use();
            }
        }

        private void DrawPause()
        {
            _hitRects.Add(new Rect(0f, 0f, _sw, _sh));
            HudSkin.Vignette(new Rect(0, 0, _sw, _sh), new Color(0.02f, 0.02f, 0.03f), 0.5f);
            if (_pauseSlots)
            {
                DrawPauseSlots();
                return;
            }

            bool iron = ReplayRules.BlocksManualSavesAndReloads;
            float h = Mathf.Min(_sh - 20f, Mathf.Max(iron ? 340f : 400f, _pauseMeasuredH));
            float pw = Mathf.Min(W(440f), _sw - M * 2f);
            var rect = new Rect((_sw - pw) * 0.5f, Mathf.Max(10f, (_sh - h) * 0.5f), pw, h);
            var c = Panel(rect, Loc.S("Paused"));
            HudSkin.CornerTicks(rect, new Color(Gold.r, Gold.g, Gold.b, 0.75f));
            float y = WrapBlock(c, c.y,
                Loc.S("Simulation frozen. Autosave follows this colony. Save keeps a copy in a numbered slot."));
            float bh = L(32f);
            float step = bh + 8f;
            if (Btn(new Rect(c.x, y, c.width, bh), Loc.S("RESUME  ·  Esc"), _chipOn, HudTips.Resume()))
                _loop.ResumePlay();
            y += step;
            if (iron)
            {
                FitLabel(new Rect(c.x, y, c.width, L(18f)), Loc.S("IRONMAN — no manual save or load."), _muted);
                y += L(18f) + 8f;
            }
            else
            {
                float half = (c.width - 8f) * 0.5f;
                if (Btn(new Rect(c.x, y, half, bh), Loc.S("SAVE"), _chipOff, HudTips.SaveGame()))
                {
                    _pauseSlots = true;
                    _pauseSlotsAreLoad = false;
                }
                if (Btn(new Rect(c.x + half + 8f, y, half, bh), Loc.S("LOAD"), _chipOff, HudTips.LoadGame()))
                {
                    _pauseSlots = true;
                    _pauseSlotsAreLoad = true;
                }
                y += step;
            }
            if (Btn(new Rect(c.x, y, c.width, bh), Loc.S("SETTINGS"), _chipOff, HudTips.OpenSettings()))
                _loop.OpenSettings();
            y += step;
            if (Btn(new Rect(c.x, y, c.width, bh), Loc.S("TITLE"), _chipOff, HudTips.ToTitle()))
                _loop.ReturnToTitle();
            y += step;
            if (iron)
            {
                FitLabel(new Rect(c.x, y, c.width, L(18f)), Loc.S("IRONMAN — no abandon, no new seed."), _muted);
                y += L(18f) + 8f;
            }
            else if (Btn(new Rect(c.x, y, c.width, bh), Loc.S("ABANDON BODY  ·  new seed"), _chipOff, HudTips.Abandon()))
            {
                DemoSettings.RequestBootIntoPlay();
                _loop.RestartMission();
            }
            if (!iron) y += step;
            if (Btn(new Rect(c.x, y, c.width, bh), Loc.S("QUIT"), _chipOff, HudTips.Quit()))
                _loop.QuitDemo();
            _pauseMeasuredH = y + bh + Pad - rect.y;
        }

        private float _pauseMeasuredH;

        private void DrawPauseSlots()
        {
            float h = Mathf.Min(_sh - 20f, Mathf.Max(_pauseSlotsAreLoad ? 340f : 300f, _slotsMeasuredH));
            float pw = Mathf.Min(W(480f), _sw - M * 2f);
            var rect = new Rect((_sw - pw) * 0.5f, Mathf.Max(10f, (_sh - h) * 0.5f), pw, h);
            var c = Panel(rect, _pauseSlotsAreLoad ? Loc.S("Load") : Loc.S("Save"));
            HudSkin.CornerTicks(rect, new Color(Gold.r, Gold.g, Gold.b, 0.75f));
            float y = WrapBlock(c, c.y, _pauseSlotsAreLoad
                ? Loc.S("Load a colony. Other slots stay on disk.")
                : Loc.S("Write this colony into a slot. Other slots stay."));
            var slots = SaveSystem.ListSlots();
            float slotH = L(28f);
            for (int i = 0; i < slots.Count; i++)
            {
                var info = slots[i];
                if (!_pauseSlotsAreLoad && info.Slot == SaveSystem.AutosaveSlot) continue;
                if (Btn(new Rect(c.x, y, c.width, slotH), SlotButtonLabel(info), _chipOff))
                {
                    if (_pauseSlotsAreLoad)
                    {
                        if (info.Occupied) _loop.LoadFromSlot(info.Slot);
                        else Notify(Loc.S("That slot is empty."), 2.2f);
                    }
                    else if (_loop.SaveToSlot(info.Slot))
                        _pauseSlots = false;
                }
                y += slotH + 6f;
            }
            y += 6f;
            if (Btn(new Rect(c.x, y, c.width, slotH), Loc.S("BACK  ·  Esc"), _chipOn, HudTips.Back()))
                _pauseSlots = false;
            _slotsMeasuredH = y + slotH + Pad - rect.y;
        }

        private float _slotsMeasuredH;

        private static string SlotButtonLabel(SaveSlotInfo info)
        {
            if (!info.Occupied || string.IsNullOrEmpty(info.Summary))
                return Loc.F("{0}  ·  empty", info.Label);
            string summary = info.Summary;
            if (summary.Length > 52) summary = summary.Substring(0, 51) + "…";
            return $"{info.Label}  ·  {summary}";
        }

        private void DrawSettings()
        {
            // The dimmed backdrop is UI. A wheel notch here scrolls settings, not the map.
            _hitRects.Add(new Rect(0f, 0f, _sw, _sh));
            HudSkin.Vignette(new Rect(0, 0, _sw, _sh), new Color(0.02f, 0.02f, 0.03f), 0.55f);
            float h = Mathf.Min(L(764f), Mathf.Max(460f, _sh - 24f));
            h = Mathf.Min(h, _sh - 20f);
            float sw = Mathf.Min(W(440f), _sw - M * 2f);
            var rect = new Rect((_sw - sw) * 0.5f, Mathf.Max(10f, (_sh - h) * 0.5f), sw, h);
            var panel = Panel(rect, Loc.S("Settings"));
            HudSkin.CornerTicks(rect, new Color(Gold.r, Gold.g, Gold.b, 0.75f));
            HandleRebindKey();

            // Everything above BACK scrolls when the window is shorter than the list.
            float backH = L(32f);
            var view = new Rect(panel.x, panel.y, panel.width, panel.height - backH - 12f);
            bool scrolls = _settingsContentH > view.height;
            var c = new Rect(0f, 0f, view.width - (scrolls ? 12f : 0f), _settingsContentH);
            _tips.BeginClip(view);
            _settingsScroll = GUI.BeginScrollView(view, _settingsScroll, c);
            float y = c.y;

            // Two columns of mix sliders, then full-width UI scale and text size rows.
            float colW = (c.width - 16f) * 0.5f;
            float colX = c.x + colW + 16f;
            SettingsSlider(c.x, y, colW, Loc.S("MASTER"), ref DemoSettings.Master);
            y = SettingsSlider(colX, y, colW, Loc.S("MUSIC"), ref DemoSettings.Music);
            SettingsSlider(c.x, y, colW, Loc.S("SFX"), ref DemoSettings.Sfx);
            y = SettingsSlider(colX, y, colW, Loc.S("VOICES"), ref DemoSettings.Voice);
            SettingsSlider(c.x, y, colW, Loc.S("AMBIENCE"), ref DemoSettings.Ambient);
            y += L(28f);
            y = UiScaleRow(c.x, y, c.width);
            y = TextSizeRow(c.x, y, c.width);
            y += 6f;

            float half2 = (c.width - 8f) * 0.5f;
            if (Chip(new Rect(c.x, y, half2, RowH), Loc.S("INVERT CAMERA PAN"), DemoSettings.InvertPan, HudTips.InvertPan()))
                DemoSettings.InvertPan = !DemoSettings.InvertPan;
            // Perspective camera: zoom out to see the horizon and the sky.
            if (Chip(new Rect(c.x + half2 + 8f, y, half2, RowH), Loc.S("DIORAMA CAMERA"), DemoSettings.DioramaCamera, HudTips.Diorama()))
            {
                DemoSettings.DioramaCamera = !DemoSettings.DioramaCamera;
                DemoSettings.SaveSettings();
            }
            y += RowStep;

            if (Chip(new Rect(c.x, y, half2, RowH),
                    DemoSettings.Fullscreen ? Loc.S("FULLSCREEN") : Loc.S("WINDOWED"), DemoSettings.Fullscreen, HudTips.Fullscreen()))
            {
                DemoSettings.Fullscreen = !DemoSettings.Fullscreen;
                DemoSettings.ApplyDisplay();
                DemoSettings.SaveSettings();
            }
            if (Chip(new Rect(c.x + half2 + 8f, y, half2, RowH), Loc.S("DAY / NIGHT CYCLE"), DemoSettings.DayCycle, HudTips.DayCycle()))
            {
                DemoSettings.DayCycle = !DemoSettings.DayCycle;
                DemoSettings.SaveSettings();
            }
            y += RowStep;

            if (Chip(new Rect(c.x, y, c.width, RowH),
                    Loc.F("RESOLUTION  ·  {0}", DemoSettings.ResolutionLabel),
                    DemoSettings.ResolutionWidth >= 640, HudTips.Resolution()))
            {
                DemoSettings.CycleResolution();
                DemoSettings.SaveSettings();
            }
            y += RowStep;

            var qualityNames = QualitySettings.names;
            string qLabel = Loc.S("QUALITY");
            if (qualityNames != null && qualityNames.Length > 0)
            {
                DemoSettings.QualityIndex = Mathf.Clamp(DemoSettings.QualityIndex, 0, qualityNames.Length - 1);
                qLabel = Loc.F("QUALITY  ·  {0}", qualityNames[DemoSettings.QualityIndex].ToUpperInvariant());
            }
            if (Chip(new Rect(c.x, y, c.width, RowH), qLabel, false, HudTips.Quality()) && qualityNames != null && qualityNames.Length > 0)
            {
                DemoSettings.QualityIndex = (DemoSettings.QualityIndex + 1) % qualityNames.Length;
                DemoSettings.ApplyDisplay();
            }
            y += RowStep;

            int[] caps = { 0, 30, 60, 120, 144 };
            int capIndex = System.Array.IndexOf(caps, DemoSettings.FrameCap);
            if (capIndex < 0) capIndex = 0;
            string capLabel = DemoSettings.FrameCap <= 0 ? Loc.S("UNCAPPED") : Loc.F("{0} FPS", DemoSettings.FrameCap);
            if (Chip(new Rect(c.x, y, c.width, RowH), Loc.F("FRAME CAP  ·  {0}", capLabel), DemoSettings.FrameCap > 0, HudTips.FrameCap()))
            {
                DemoSettings.FrameCap = caps[(capIndex + 1) % caps.Length];
                DemoSettings.ApplyDisplay();
            }
            y += RowStep;

            float visHalf = (c.width - 8f) * 0.5f;
            if (Chip(new Rect(c.x, y, visHalf, RowH), Loc.S("TILT-SHIFT (DIORAMA)"), DemoSettings.TiltShift, HudTips.TiltShift()))
            {
                DemoSettings.TiltShift = !DemoSettings.TiltShift;
                DemoSettings.SaveSettings();
            }
            if (Chip(new Rect(c.x + visHalf + 8f, y, visHalf, RowH), Loc.S("CLOUD SHADOWS"), DemoSettings.CloudShadows, HudTips.CloudShadows()))
            {
                DemoSettings.CloudShadows = !DemoSettings.CloudShadows;
                DemoSettings.SaveSettings();
                CloudShadows.Refresh();
            }
            y += RowStep;

            // Baked voices need nothing running. The LOCAL rows add LLM-written lines and speak them.
            if (Chip(new Rect(c.x, y, c.width, RowH), Loc.S("CHARACTER VOICES  ·  BAKED BARKS + CORE"), DemoSettings.CharacterVoices, HudTips.CharacterVoices()))
            {
                DemoSettings.CharacterVoices = !DemoSettings.CharacterVoices;
                DemoSettings.SaveSettings();
            }
            y += RowStep;

            // Lines come from a local LLM; SPOKEN reads them aloud through a local TTS server.
            if (Chip(new Rect(c.x, y, visHalf, RowH), Loc.S("HERO LINES · LOCAL LLM"), DemoSettings.HeroVoices, HudTips.HeroLines()))
            {
                DemoSettings.HeroVoices = !DemoSettings.HeroVoices;
                DemoSettings.SaveSettings();
            }
            if (Chip(new Rect(c.x + visHalf + 8f, y, visHalf, RowH), Loc.S("SPOKEN · LOCAL TTS"), DemoSettings.HeroSpeech, HudTips.Spoken()))
            {
                DemoSettings.HeroSpeech = !DemoSettings.HeroSpeech;
                DemoSettings.SaveSettings();
            }
            y += RowH + 10f;

            Fill(new Rect(c.x, y, c.width, 1f), Hairline);
            y += 8f;
            FitLabel(new Rect(c.x, y, c.width, L(14f)), Loc.S("ACCESSIBILITY"), _section);
            y += L(18f);

            float halfW = (c.width - 8f) * 0.5f;
            if (Chip(new Rect(c.x, y, halfW, RowH), Loc.S("EDGE SCROLL"), DemoSettings.EdgeScroll, HudTips.EdgeScroll()))
                DemoSettings.EdgeScroll = !DemoSettings.EdgeScroll;
            if (Chip(new Rect(c.x + halfW + 8f, y, halfW, RowH), Loc.S("REDUCE MOTION"), DemoSettings.ReduceMotion, HudTips.ReduceMotion()))
                DemoSettings.ReduceMotion = !DemoSettings.ReduceMotion;
            y += RowStep;

            var cbMode = Accessibility.Mode;
            if (Chip(new Rect(c.x, y, c.width, RowH),
                    Loc.F("COLOUR VISION  ·  {0}", Accessibility.ModeLabel(cbMode).ToUpperInvariant()),
                    cbMode != ColorBlindMode.Off, HudTips.ColourVision()))
            {
                Accessibility.Mode = (ColorBlindMode)(((int)cbMode + 1) % 4);
            }
            y += RowStep;

            y = WrapBlock(c, y, Loc.S("Severity is also shown by icon and prefix, so colour is never the only cue.")) + 6f;

            // Language: every table shipped in Resources/Locale, plus the QA pseudo-language with dev tools.
            var languages = Loc.Available;
            if (languages.Count > 1)
            {
                if (Chip(new Rect(c.x, y, c.width, RowH),
                        $"{Loc.S("LANGUAGE")}  ·  {Loc.NameOf(Loc.Language).ToUpperInvariant()}", Loc.Language != Loc.DefaultLanguage, HudTips.Language()))
                {
                    int at = 0;
                    for (int i = 0; i < languages.Count; i++)
                        if (languages[i].Code == Loc.Language) at = i;
                    Loc.SetLanguage(languages[(at + 1) % languages.Count].Code);
                }
                y += RowStep;
            }

            string tutLabel = _loop.IsTutorialActive ? Loc.S("TUTORIAL  ·  ON") : Loc.S("REPLAY TUTORIAL");
            if (Chip(new Rect(c.x, y, halfW, RowH), tutLabel, _loop.IsTutorialActive, HudTips.Tutorial()))
                _loop.RestartTutorial();
            string logsLabel = ErrorLog.Count > 0 ? Loc.F("OPEN LOGS FOLDER  ·  {0}", ErrorLog.Count) : Loc.S("OPEN LOGS FOLDER");
            if (Chip(new Rect(c.x + halfW + 8f, y, halfW, RowH), logsLabel, ErrorLog.Count > 0, HudTips.Logs()))
                ErrorLog.OpenFolder();
            y += RowStep;

            bool marsLessons = DemoSettings.MarsJexLessons;
            if (!DemoBuild.Active) // Luna/Mars lessons: not in the Earth-only Steam demo
            {
                if (Chip(new Rect(c.x, y, c.width, RowH),
                        marsLessons ? Loc.S("MARS JEX LESSONS  ·  ON") : Loc.S("MARS JEX LESSONS  ·  OFF"),
                        marsLessons, HudTips.MarsLessons()))
                {
                    DemoSettings.MarsJexLessons = !DemoSettings.MarsJexLessons;
                    DemoSettings.SaveSettings();
                }
                y += RowH + 2f;
                y = WrapBlock(c, y, Loc.S("Luna: Jex lectures. Mars: failure asides unless lessons are on.")) + 6f;
            }

            y = DrawControls(c, y);

            Fill(new Rect(c.x, y, c.width, 1f), Hairline);
            y += 8f;
            if (DemoSettings.FirstHourDemo)
            {
                FitLabel(new Rect(c.x, y, c.width, L(14f)), Loc.S("GUIDED START"), _section);
                y += L(18f);
                if (DemoBuild.Active)
                    y = WrapBlock(c, y, DemoEndOffer.SettingsNote); // no way out of the Earth demo
                else
                {
                    if (Chip(new Rect(c.x, y, c.width, RowH), Loc.S("SKIP GUIDED START"), false, HudTips.GuidedStart(true)))
                        _loop.SetFirstHourDemo(false);
                    y += RowH + 4f;
                    y = WrapBlock(c, y,
                        Loc.S("Earth first: learn how bounties sway your robots. Guilds and the outer worlds open when you clear Earth. Skip this if you have played before."));
                }
            }
            else
            {
                FitLabel(new Rect(c.x, y, c.width, L(14f)), Loc.S("REPLAY  ·  MODE / CHALLENGE / STANCE / IRONMAN"), _section);
                y += L(18f);

                float half = (c.width - 8f) * 0.5f;
                if (Chip(new Rect(c.x, y, half, RowH),
                        Loc.F("MODE  ·  {0}", ReplayRules.ModeLabel), ReplayRules.IsEndless, HudTips.Mode()))
                    ReplayRules.CycleMode();
                if (Chip(new Rect(c.x + half + 8f, y, half, RowH),
                        Loc.F("CHAL  ·  {0}", ReplayRules.ChallengeLabel), ReplayRules.Challenge != ChallengeId.None, HudTips.Challenge()))
                    ReplayRules.CycleChallenge();
                y += RowStep;

                if (Chip(new Rect(c.x, y, half, RowH),
                        Loc.F("STANCE  ·  {0}", ReplayRules.StanceLabel), ReplayRules.Stance != DoctrineStance.Balanced, HudTips.Stance()))
                    ReplayRules.CycleStance();
                bool ironmanOn = _loop.RunConfigLocked ? ReplayRules.IronmanRun : ReplayRules.Ironman;
                if (Chip(new Rect(c.x + half + 8f, y, half, RowH),
                        Loc.F("IRON  ·  {0}", ironmanOn ? Loc.S("ON") : Loc.S("OFF")), ironmanOn, HudTips.Ironman()))
                {
                    if (_loop.RunConfigLocked)
                        Notify(Loc.S("Ironman latches at New Game. This run stays as started."), 3.5f);
                    else
                        ReplayRules.CycleIronman();
                }
                y += RowH + 4f;

                y = WrapBlock(c, y, ReplayRules.StanceHint + " " + InputBindings.ExpandKeyTokens(ReplayRules.ChallengeHint));
                y = WrapBlock(c, y, ReplayRules.IronmanHint);
                y = WrapBlock(c, y,
                    Loc.S("Stockpile and fauna apply on New Game / reload. Doctrine hunger, courage, range, and workshop pull apply live. Low Battery ship rules apply when you leave Settings. Ironman latches at New Game or Continue."));
                y += 4f;
                if (Chip(new Rect(c.x, y, c.width, RowH), Loc.S("REPLAY GUIDED START"), false, HudTips.GuidedStart(false)))
                    _loop.SetFirstHourDemo(true);
                y += RowH + 4f;
            }

            _settingsContentH = y + 4f;
            GUI.EndScrollView();
            _tips.EndClip();

            if (Btn(new Rect(panel.x, panel.yMax - backH - 2f, panel.width, backH), Loc.S("BACK  ·  Esc"), _chipOn, HudTips.Back()))
                _loop.CloseSettings();
        }

        /// <summary>
        /// TEXT SIZE: 90 %–150 % in 10 % steps, separate from UI SCALE. − / + rather than a slider so a
        /// controller can step it, and so the row does not resize under a dragging cursor.
        /// </summary>
        private float TextSizeRow(float x, float y, float width)
        {
            float rowH = L(22f);
            var label = new Rect(x, y, W(78f) + 30f, rowH);
            FitLabel(label, Loc.S("TEXT SIZE"), _caps, HudTips.TextSize());
            float btn = Mathf.Max(26f, rowH);
            float right = x + width;
            var plus = new Rect(right - btn, y - 2f, btn, rowH);
            var value = new Rect(plus.x - W(64f) - 4f, y, W(64f), rowH);
            var minus = new Rect(value.x - btn - 4f, y - 2f, btn, rowH);
            float now = HudTextScale.Factor;
            bool canDown = now > HudTextScale.Min + 0.001f;
            bool canUp = now < HudTextScale.Max - 0.001f;
            var prev = GUI.color;
            if (!canDown) GUI.color = new Color(prev.r, prev.g, prev.b, prev.a * 0.45f);
            if (GUI.Button(minus, "−", _chipOff) && canDown)
                DemoSettings.SetTextScale(HudTextScale.Stepped(now, -1));
            GUI.color = prev;
            if (!canUp) GUI.color = new Color(prev.r, prev.g, prev.b, prev.a * 0.45f);
            if (GUI.Button(plus, "+", _chipOff) && canUp)
                DemoSettings.SetTextScale(HudTextScale.Stepped(now, 1));
            GUI.color = prev;
            var prevAlign = _numberSmall.alignment;
            _numberSmall.alignment = TextAnchor.MiddleCenter;
            GUI.Label(value, HudTextScale.Percent(now), _numberSmall);
            _numberSmall.alignment = prevAlign;
            // A preview bar between the label and the buttons, so the change is visible at once.
            var sample = new Rect(label.xMax + 6f, y, Mathf.Max(0f, minus.x - label.xMax - 12f), rowH);
            if (sample.width > 40f) FitLabel(sample, Loc.S("Aa · the quick robot"), _micro);
            Tooltip(new Rect(x, y - 2f, width, rowH + 2f), HudTips.TextSize());
            return y + rowH + 8f;
        }

        /// <summary>
        /// One chip per rebindable action. Legacy input follows the keyboard layout, so this is how
        /// an AZERTY player gets panning back under their left hand.
        /// </summary>
        private float DrawControls(Rect c, float y)
        {
            Fill(new Rect(c.x, y, c.width, 1f), Hairline);
            y += 8f;
            FitLabel(new Rect(c.x, y, c.width, L(14f)), Loc.S("CONTROLS"), _section);
            y += L(18f);

            float half = (c.width - 8f) * 0.5f;
            for (int i = 0; i < RebindableActions.Length; i++)
            {
                GameAction action = RebindableActions[i];
                bool waiting = _rebinding == action;
                string key = waiting ? Loc.S("PRESS A KEY…") : InputBindings.Label(action).ToUpperInvariant();
                var r = new Rect(i % 2 == 0 ? c.x : c.x + half + 8f, y, half, RowH);
                string name = InputBindings.DisplayName(action).ToUpperInvariant();
                if (Chip(r, $"{name}  ·  {key}", waiting, HudTips.Rebind($"{name}  ·  {key}")))
                {
                    _rebinding = waiting ? (GameAction?)null : action;
                    _rebindNote = null;
                }
                if (i % 2 == 1 || i == RebindableActions.Length - 1) y += RowStep;
            }

            if (!string.IsNullOrEmpty(_rebindNote))
            {
                var prevC = _micro.normal.textColor;
                if (_rebindNoteAlarm) _micro.normal.textColor = Alarm;
                FitLabel(new Rect(c.x, y, c.width, L(16f)), _rebindNote, _micro);
                _micro.normal.textColor = prevC;
                y += L(16f) + 4f;
            }
            else
            {
                y = WrapBlock(c, y, _rebinding.HasValue
                    ? Loc.S("Press the new key. Esc cancels.")
                    : Loc.S("Click an action, then press its new key. Esc, number keys, Shift and Ctrl stay fixed."));
            }

            if (Chip(new Rect(c.x, y, c.width, RowH), Loc.S("RESET CONTROLS"), false, HudTips.ResetControls()))
            {
                InputBindings.ResetToDefaults();
                _rebinding = null;
                _rebindNote = Loc.S("Controls reset to defaults.");
                _rebindNoteAlarm = false;
            }
            y += RowH + 8f;

            // Controller: the stick drives the cursor, so the one setting worth exposing is its speed.
            FitLabel(new Rect(c.x, y, c.width, L(14f)),
                GamepadInput.ControllerActive ? Loc.S("CONTROLLER  ·  IN USE") : Loc.S("CONTROLLER"), _section);
            y += L(18f);
            int speed = GamepadInput.CursorSpeed;
            if (Chip(new Rect(c.x, y, c.width, RowH), Loc.F("CURSOR SPEED  ·  {0}", GamepadMap.SpeedName(speed)), false, HudTips.CursorSpeed()))
                GamepadInput.CursorSpeed = (speed + 1) % GamepadMap.SpeedPresets.Length;
            y += RowStep;
            return WrapBlock(c, y, GamepadMap.Summary) + 4f;
        }

        /// <summary>
        /// The key press that ends a rebind. Read from IMGUI so it lands before any button sees it;
        /// a refused key ends the wait with a note rather than leaving the player stuck in it.
        /// </summary>
        private void HandleRebindKey()
        {
            Event e = Event.current;
            if (!_rebinding.HasValue || e == null || e.type != EventType.KeyDown || e.keyCode == KeyCode.None)
                return;

            GameAction action = _rebinding.Value;
            KeyCode key = e.keyCode;
            e.Use();
            _rebinding = null;
            _rebindNoteAlarm = true;
            string keyLabel = InputBindings.KeyLabel(key);

            if (key == KeyCode.Escape)
            {
                _rebindNote = null;
                return;
            }
            if (InputBindings.IsReserved(key))
            {
                _rebindNote = Loc.F("{0} stays fixed (build slots and modifiers).", keyLabel);
                return;
            }
            GameAction? clash = InputBindings.Conflict(action, key);
            if (clash.HasValue)
            {
                _rebindNote = Loc.F("{0} already does {1}.", keyLabel, InputBindings.DisplayName(clash.Value));
                return;
            }
            if (InputBindings.TryRebind(action, key))
            {
                _rebindNote = $"{InputBindings.DisplayName(action)}  ·  {InputBindings.Label(action)}";
                _rebindNoteAlarm = false;
            }
        }

        /// <summary>Wrapped paragraph at its measured height; returns the y below it.</summary>
        private float WrapBlock(Rect c, float y, string text)
        {
            if (string.IsNullOrEmpty(text)) return y;
            float h = _wrap.CalcHeight(new GUIContent(text), c.width);
            GUI.Label(new Rect(c.x, y, c.width, h), text, _wrap);
            return y + h + 6f;
        }

        /// <summary>
        /// 75%–200% in 5% steps. AUTO follows screen height. The number updates while the
        /// slider moves; the panel scale changes when the mouse comes up, so the row
        /// does not slide out from under the cursor.
        /// </summary>
        private float UiScaleRow(float x, float y, float width)
        {
            bool auto = DemoSettings.HudScaleAuto;
            float applied = auto
                ? HudScaleMath.AutoForHeight(Screen.height)
                : DemoSettings.HudScaleExplicit;
            if (!_uiScaleGesture.Dragging)
                _uiScaleGesture.SyncApplied(applied);
            float shown = _uiScaleGesture.Dragging ? _uiScaleGesture.Shown : applied;
            string pct = auto ? Loc.F("A {0:0}%", shown * 100f) : $"{shown * 100f:0}%";

            float rowH = L(18f);
            float labelW = W(78f) + 30f;
            FitLabel(new Rect(x, y, labelW, rowH), Loc.S("UI SCALE"), _caps, HudTips.UiScale());
            float autoW = W(52f);
            float valueW = W(64f);
            float right = x + width;
            float sliderX = x + labelW + 4f;
            float sliderW = Mathf.Max(24f, right - autoW - valueW - 8f - sliderX);
            var sliderRect = new Rect(sliderX, y + rowH * 0.5f - 7f, sliderW, 16f);
            float next = GUI.HorizontalSlider(sliderRect, shown, HudScaleMath.Min, HudScaleMath.Max);
            Tooltip(sliderRect, HudTips.UiScale());
            _uiScaleGesture.Drag(next);
            bool mouseUp = Event.current != null &&
                           Event.current.type == EventType.MouseUp &&
                           Event.current.button == 0;
            if (!mouseUp &&
                Event.current != null &&
                Event.current.type == EventType.Repaint &&
                _uiScaleGesture.Dragging &&
                !Input.GetMouseButton(0))
                mouseUp = true;
            if (_uiScaleGesture.TryCommit(mouseUp, out float commit))
            {
                DemoSettings.SetHudScaleExplicit(commit);
                DemoSettings.SaveSettings();
            }
            GUI.Label(new Rect(right - autoW - valueW - 4f, y, valueW, rowH), pct, _microRight);
            if (Btn(new Rect(right - autoW, y - 2f, autoW, rowH + 4f), Loc.S("AUTO"), auto ? _chipOn : _chipOff, HudTips.UiScaleAuto()))
            {
                _uiScaleGesture.Cancel();
                DemoSettings.ToggleHudScaleAuto(Screen.width, Screen.height);
                DemoSettings.SaveSettings();
            }
            return y + rowH + 10f;
        }

        private float SettingsSlider(
            float x, float y, float width, string label, ref float value,
            float min = 0f, float max = 1f, bool applyAudio = true)
        {
            float rowH = L(18f);
            float labelW = W(90f);
            float valueW = W(40f);
            FitLabel(new Rect(x, y, labelW, rowH), label, _caps, HudTips.Volume(label));
            GUI.Label(new Rect(x + width - valueW, y, valueW, rowH),
                max > 1f ? $"{value:0.00}×" : $"{value * 100f:F0}%", _microRight);
            var slider = new Rect(x + labelW, y + rowH * 0.5f - 7f, Mathf.Max(24f, width - labelW - valueW - 10f), 16f);
            float next = GUI.HorizontalSlider(slider, value, min, max);
            Tooltip(slider, HudTips.Volume(label));
            if (!Mathf.Approximately(next, value))
            {
                value = next;
                if (applyAudio)
                    DemoAudio.ApplyVolumes();
            }
            return y + rowH + 10f;
        }

        private void DrawTutorial()
        {
            if (!_loop.IsTutorialActive) return;

            string line;
            if (DemoSettings.FirstHourDemo)
                line = InputBindings.ExpandKeyTokens(
                    FirstHourTutorial.Beat(_loop.TutorialStep, _loop.FirstHourBuildAlreadyTempting));
            else
            {
                string[] beats =
                {
                    Loc.S("1/6  COMMONS — Colony Commons is already on the claim. It holds the treasury."),
                    Loc.S("2/6  Haul walks out of the Commons and carries each building's EU home."),
                    Loc.S("3/6  Keep the yard clear of mobs — bots build charging stations and solar farms that pay EU."),
                    Loc.S("4/6  Workshop — build Scout / Engineer / Defense. A robot fabricates when it finishes."),
                    Loc.F("5/6  {0}, Build, leave it at 700. Watch the Engineer.", InputBindings.Hint(GameAction.ToolFlag)),
                    Loc.F("6/6  They named a price. Select the flag and press {0} until the chip reads tempted.", InputBindings.Hint(GameAction.BountyUp))
                };
                int step = Mathf.Clamp(_loop.TutorialStep, 0, beats.Length - 1);
                if (_loop.TutorialWantsPriceLesson)
                    step = 5;
                line = beats[step];
            }
            // Top centre, like Majesty 2's advisor line — build/flag catalogs own the space above
            // the console, and the objectives panel owns the top-right corner.
            float barH = DemoSettings.FirstHourDemo ? 60f : 52f;
            HudLayout.Tutorial(_sw, barH, out var rect, out var textRect, out var skipRect);
            // The bar grows to fit the wrapped line (large text, long French) instead of clipping it.
            float needH = _wrap.CalcHeight(new GUIContent(line), textRect.width) + 16f;
            if (needH > barH)
            {
                barH = needH;
                HudLayout.Tutorial(_sw, barH, out rect, out textRect, out skipRect);
            }
            _tutorialBottom = rect.yMax;
            var c = Panel(rect, null);
            HudSkin.RuleH(new Rect(rect.x, rect.y, rect.width, 1f), Accent);
            // Advisor medallion
            var medal = new Rect(c.x, rect.center.y - 15f, 30f, 30f);
            HudSkin.SoftDot(medal.center, 44f, new Color(Accent.r, Accent.g, Accent.b, 0.30f));
            HudSkin.Dot(medal.center, 28f, new Color(0f, 0f, 0f, 0.45f));
            HudSkin.Ring(medal, Gold);
            HudSkin.Icon(HudSkin.Expand(medal, -7f), UiIcons.Get(IconId.Sun), HudSkin.GoldBright);
            var prevText = _wrap.normal.textColor;
            _wrap.normal.textColor = TextPrimary;
            GUI.Label(textRect, line, _wrap);
            _wrap.normal.textColor = prevText;
            if (Btn(skipRect, Loc.S("SKIP"), _chipOff, HudTips.SkipTutorial()))
                _loop.SkipTutorial();
        }

        private void DrawDropManifest()
        {
            if (!_loop.StartsEmpty) return;
            if (_loop.Settlement != null && _loop.Settlement.HasCommons) return;
            if (_loop.TutorialStep > 0) return;
            var catalog = _loop.StarterBuildings;
            if (catalog == null || catalog.Length == 0) return;

            BuildingCategory[] want =
            {
                BuildingCategory.Commons,
                BuildingCategory.Habitat,
                BuildingCategory.EngineerWorkshop
            };

            var rect = new Rect(M, M, W(TopW), L(118f));
            var c = Panel(rect, Loc.S("Drop manifest"), true, HudTips.DropManifest());
            float y = c.y;
            FitLabel(new Rect(c.x, y, c.width, L(13f)), Loc.S("COMMONS first, then HAB and workshops anywhere on open ground."), _micro);
            y += L(16f);
            for (int w = 0; w < want.Length; w++)
            {
                BuildingData found = null;
                for (int i = 0; i < catalog.Length; i++)
                {
                    if (catalog[i] != null && catalog[i].category == want[w])
                    {
                        found = catalog[i];
                        break;
                    }
                }
                if (found == null) continue;
                FitLabel(new Rect(c.x, y, c.width, L(14f)),
                    $"{Loc.S(found.displayName)}  ·  {FormatBuildCost(CostOf(found))}", _micro);
                y += L(15f);
            }
        }
    }
}
