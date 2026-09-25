using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Player-facing overseer console (IMGUI): stockpile, tools, flag/build menus,
    /// mission stakes, specialist cards (selected only), win/fail banners.
    /// DebugHud stays on F8 for deep scores.
    /// </summary>
    public class OverseerHud : MonoBehaviour
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

        private const float M = 16f;      // screen margin
        private const float Pad = 12f;    // panel padding
        private const float TopW = 300f;  // top-left command width
        // Majesty 2-style console: one bottom-centre bar with the treasury crest on top.
        private const float DockH = 100f;
        private const float DockW = 860f;
        private const float CrestW = 240f;
        private const float CrestRise = 30f;
        private const float MapSize = 176f;

        private GameLoop _loop;
        private bool _failLatched;
        private bool _winDismissed;
        private bool _deadlineDismissed;
        private bool _techOpen;
        private bool _buildMinimized;
        private bool _flagMinimized;
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
        private bool _powerAlarmLatched;
        private bool _confirmNewGame;
        private CelestialBodyId _pendingNewGameBody = CelestialBodyId.Earth;
        private float _toastStart;
        private ColonyStructure _cardFor;
        private float _cardMeasuredH;
        private Vector2 _settingsScroll;
        private float _settingsContentH = 700f;
        private string _tooltip;
        private Rect _tooltipAnchor;


        private bool _stylesReady;
        private GUIStyle _brand;
        private GUIStyle _section;
        private GUIStyle _body;
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
            float s = Mathf.Clamp(DemoSettings.HudScale, 0.85f, 1.25f);
            Vector2 imguiMouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y) / s;
            for (int i = 0; i < _hitRects.Count; i++)
            {
                if (_hitRects[i].Contains(imguiMouse))
                    return true;
            }
            return false;
        }

        public bool TitleConfirmOpen => _confirmNewGame;

        /// <summary>Bottom of the right-hand column (objectives, research) in HUD units; the alert feed stacks under it.</summary>
        public float RightColumnBottom { get; private set; } = 96f;

        private readonly List<Rect> _hitRects = new List<Rect>(12);

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
            if (StillCaptureHold.Active) return;
            if (_loop == null || !_loop.StartsEmpty) return;
            // W2 arrival cuts + advisor travel toasts replace the stale Earth briefing.
            if (CampaignCutsceneCatalog.TryGetArrival(_loop.ActiveBody, out _))
                return;
            var body = _loop.BodyProfile;
            string briefing = body != null && !string.IsNullOrEmpty(body.Briefing)
                ? body.Briefing
                : "Colony Commons is down. Dock modules via airlocks.";
            Toast(briefing, 6.5f);
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
                : "Trade ship landed — caravan gold waits in the Market till for a collector.";
            Toast(line, 4f);
            DemoAudio.PlayRetry();
        }

        private void OnResupplyWavedOff()
        {
            string line = _loop?.Economy != null && !string.IsNullOrEmpty(_loop.Economy.LastResupplyLine)
                ? _loop.Economy.LastResupplyLine
                : "Earth ship waved off — no Landing Pad.";
            Toast(line, 4.5f);
        }

        private void OnUpkeep()
        {
            // No grid and no payroll: the upkeep clock has nothing to warn about.
        }

        private void Toast(string message, float seconds)
        {
            if (StillCaptureHold.Active) return;
            if (_toast != message || Time.unscaledTime > _toastUntil)
                _toastStart = Time.unscaledTime;
            _toast = message;
            _toastUntil = Time.unscaledTime + seconds;
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
        private Rect Panel(Rect r, string title, bool accentTab = true)
        {
            _hitRects.Add(r);
            HudSkin.Panel(r);

            float y = r.y + Pad;
            if (!string.IsNullOrEmpty(title))
            {
                float x = r.x + Pad;
                if (accentTab)
                {
                    HudSkin.Pill(new Rect(x, y + 1f, 3f, 11f), Accent);
                    x += 9f;
                }
                GUI.Label(new Rect(x, y - 1f, r.xMax - Pad - x, 15f), title.ToUpperInvariant(), _section);
                y += 17f;
                Fill(new Rect(r.x + Pad, y, r.width - Pad * 2f, 1f), new Color(1f, 1f, 1f, 0.06f));
                HudSkin.RuleH(new Rect(r.x + Pad - 30f, y, 120f, 1f), new Color(Gold.r, Gold.g, Gold.b, 0.8f));
                y += 7f;
            }
            return new Rect(r.x + Pad, y, r.width - Pad * 2f, r.yMax - y - Pad);
        }

        private void Meter(Rect r, float t01, Color fill) => HudSkin.Meter(r, t01, fill);

        /// <summary>
        /// Speed readout plus four clickable notches. Space, comma, and period drive the same state,
        /// but the notches make the feature discoverable to a player who never reads a key list.
        /// </summary>
        private void DrawSpeedControl(Rect r)
        {
            bool paused = SimSpeed.IsPaused;
            var glyph = UiIcons.Get(paused ? IconId.Pause : IconId.Play);
            HudSkin.Icon(new Rect(r.x, r.y + 2f, 10f, 10f), glyph, paused ? Alarm : Gold);
            GUI.Label(new Rect(r.x + 12f, r.y, r.width - 12f, 14f),
                paused ? "HOLD" : SimSpeed.Label,
                paused ? _microAlarm : _microRight);

            int count = SimSpeed.Multipliers.Length;
            float notchW = (r.width - (count - 1) * 3f) / count;
            var row = new Rect(r.x, r.y + 18f, notchW, 5f);

            for (int i = 0; i < count; i++)
            {
                bool lit = !paused && i <= SimSpeed.Index;
                var hit = new Rect(row.x, row.y - 5f, row.width, row.height + 10f);
                bool hot = hit.Contains(Event.current.mousePosition);
                HudSkin.Pill(row, lit ? Accent : new Color(1f, 1f, 1f, hot ? 0.28f : 0.12f));
                if (GUI.Button(hit, GUIContent.none, GUIStyle.none))
                {
                    SimSpeed.Set(i);
                    _loop.ApplySimSpeed();
                }
                row.x += notchW + 3f;
            }
        }

        private void Bar(Rect r, string label, float t01, Color fill)
        {
            const float labelW = 50f;
            const float valueW = 34f;
            GUI.Label(new Rect(r.x, r.y, labelW, r.height), label, _caps);
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

        /// <summary>Queue a hover label; drawn last so it sits above every panel.</summary>
        private void Tooltip(Rect anchor, string text)
        {
            if (!anchor.Contains(Event.current.mousePosition)) return;
            _tooltip = text;
            _tooltipAnchor = anchor;
        }

        private void DrawTooltip()
        {
            if (string.IsNullOrEmpty(_tooltip)) return;
            float w = _tooltipStyle.CalcSize(new GUIContent(_tooltip)).x + 22f;
            var r = new Rect(_tooltipAnchor.center.x - w * 0.5f, _tooltipAnchor.y - 32f, w, 24f);
            r.x = Mathf.Clamp(r.x, M, _sw - M - w);
            HudSkin.Panel(r, 1f, false);
            HudSkin.RuleH(new Rect(r.x + 4f, r.y, r.width - 8f, 1f), Accent);
            GUI.Label(r, _tooltip, _tooltipStyle);
        }

        private void EnsureStyles()
        {
            if (_stylesReady && _chipLabel != null && _titleWord != null) return;
            _stylesReady = true;

            Font display = HudSkin.Display;
            Font numeric = HudSkin.Numeric;
            Font body = HudSkin.Body;

            _brand = Label(display, 17, FontStyle.Bold, TextPrimary, TextAnchor.MiddleLeft);
            _section = Label(display, 11, FontStyle.Bold, Gold, TextAnchor.MiddleLeft);
            _body = Label(body, 12, FontStyle.Normal, TextPrimary, TextAnchor.MiddleLeft);
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
            _chipLabel.clipping = TextClipping.Overflow;
            _titleWord = Label(display, 44, FontStyle.Bold, TextPrimary, TextAnchor.MiddleLeft);
            _titleWord.clipping = TextClipping.Overflow;
            _planetName = Label(display, 12, FontStyle.Bold, TextPrimary, TextAnchor.MiddleCenter);
            _planetName.clipping = TextClipping.Overflow;
            _planetTag = Label(display, 9, FontStyle.Bold, TextMuted, TextAnchor.MiddleCenter);
            _planetTag.clipping = TextClipping.Overflow;
            _treasury = Label(numeric, 25, FontStyle.Bold, HudSkin.GoldBright, TextAnchor.MiddleCenter);
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

        private void OnGUI()
        {
            if (_loop == null) return;
            EnsureStyles();
            GUI.skin = HudSkin.Skin();
            HandleDebugHopKeys();
            _hitRects.Clear();
            _ordersFieldDrawn = false;
            _chatFieldDrawn = false;
            _tooltip = null;

            float s = Mathf.Clamp(DemoSettings.HudScale, 0.85f, 1.25f);
            _hudScale = s;
            var prevMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s, s, 1f));
            _sw = Screen.width / s;
            _sh = Screen.height / s;

            if (_loop.Screen == DemoScreen.Title)
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

                float dockTop = _sh - M - DockH;
                float stackTop = dockTop - CrestRise;
                _contentBottom = stackTop;
                DrawToolPopups(stackTop);
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
                DrawTooltip();
            }

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
            InputBindings.TextEntryActive = orders || chat;
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
        private void ResourceChip(Rect r, string label, int amount, bool alarm = false, string rate = null)
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
            Texture2D tex = ChipIcon(label);
            if (tex != null) HudSkin.Icon(icon, tex, Color.white);
            else HudSkin.Dot(icon.center, 10f, ChipSwatch(label));

            var prevLabel = _chipLabel.normal.textColor;
            _chipLabel.normal.textColor = alarm ? Alarm : TextMuted;
            GUI.Label(new Rect(r.x + 29f, r.y + 2f, r.width - 62f, 12f), label, _chipLabel);
            _chipLabel.normal.textColor = prevLabel;
            if (!string.IsNullOrEmpty(rate))
                GUI.Label(new Rect(r.x + 29f, r.y + 13f, r.width - 62f, 12f), rate, _micro);

            var prevAnchor = _number.alignment;
            _number.alignment = TextAnchor.MiddleRight;
            GUI.Label(new Rect(r.xMax - 44f, r.y, 38f, r.height), amount.ToString(), _number);
            _number.alignment = prevAnchor;
        }

        private static Texture2D ChipIcon(string label)
        {
            if (label.StartsWith("REG")) return UiIcons.Get(IconId.Regolith);
            if (label.StartsWith("ICE")) return UiIcons.Get(IconId.Ice);
            if (label.StartsWith("EU")) return UiIcons.Get(IconId.Coin);
            if (label.StartsWith("PWR")) return UiIcons.Get(IconId.Power);
            if (label.StartsWith("BEDS")) return UiIcons.Get(IconId.Beds);
            if (label.StartsWith("HEROES")) return UiIcons.Get(IconId.Robot);
            if (label.StartsWith("HOUSES")) return UiIcons.Get(IconId.House);
            if (label.StartsWith("TAXMEN")) return UiIcons.Get(IconId.Bag);
            if (label.StartsWith("VILLAGE") || label.StartsWith("HALTED")) return UiIcons.Get(IconId.Hammer);
            return null;
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

        private float DockWidth() => Mathf.Min(DockW, _sw - M * 3f - MapSize - 12f);

        /// <summary>Centred, but never under the minimap on narrow screens.</summary>
        private float DockLeft()
        {
            float w = DockWidth();
            return Mathf.Max(M, Mathf.Min((_sw - w) * 0.5f, _sw - M - MapSize - 12f - w));
        }

        /// <summary>
        /// Bottom-centre console: stockpile on the left, Overseer verbs in the middle, clock and
        /// threat on the right, and the treasury crest — the number this game is about — on top.
        /// </summary>
        private void DrawBottomDock(float top)
        {
            float w = DockWidth();
            var rect = new Rect(DockLeft(), top, w, DockH);
            var c = Panel(rect, null, false);

            DrawStockpile(new Rect(c.x, c.y, 244f, c.height));

            float right = 186f;
            var divider = new Color(Gold.r, Gold.g, Gold.b, 0.45f);
            HudSkin.RuleV(new Rect(c.x + 254f, rect.y + 6f, 1f, rect.height - 12f), divider);
            HudSkin.RuleV(new Rect(c.xMax - right - 10f, rect.y + 6f, 1f, rect.height - 12f), divider);

            // Verbs: build, bounty, research, campus, party, menu. Never unit orders.
            float sq = 48f;
            float gap = 8f;
            float verbsW = sq * 6f + gap * 5f;
            float midL = c.x + 262f;
            float midR = c.xMax - right - 18f;
            float x = midL + Mathf.Max(0f, (midR - midL - verbsW) * 0.5f);
            float vy = c.y + 3f;
            if (SquareAction(new Rect(x, vy, sq, sq), IconId.GlyphBuild, "BUILD", "B", _loop.ActiveTool == OverseerTool.Build))
                _loop.ToggleTool(OverseerTool.Build);
            x += sq + gap;
            if (SquareAction(new Rect(x, vy, sq, sq), IconId.GlyphFlag, "BOUNTY FLAG", "G", _loop.ActiveTool == OverseerTool.Flag))
                _loop.ToggleTool(OverseerTool.Flag);
            x += sq + gap;
            if (SquareAction(new Rect(x, vy, sq, sq), IconId.GlyphTech, "RESEARCH", "T", _techOpen))
                ToggleTechPanel();
            x += sq + gap;
            if (SquareAction(new Rect(x, vy, sq, sq), IconId.Camp, "SWITCH CAMPUS", "A/B", false))
                _loop.FocusCampus(1 - _loop.FocusedCampus);
            x += sq + gap;
            int partyCount = _loop.Parties != null ? _loop.Parties.Count : 0;
            if (SquareAction(new Rect(x, vy, sq, sq), IconId.Party, "FORM PARTY", "P", partyCount > 0))
                _loop.FormParty();
            x += sq + gap;
            if (SquareAction(new Rect(x, vy, sq, sq), IconId.Menu, "MENU", "Esc", false))
                _loop.TogglePause();

            DrawLevyLine(new Rect(midL, vy + sq + 7f, midR - midL, 14f));
            DrawClockAndThreat(new Rect(c.xMax - right, c.y, right, c.height));
            DrawTreasuryCrest(new Rect(rect.x + (w - CrestW) * 0.5f, top - CrestRise, CrestW, HudSkin.CrestSize.y));
        }

        /// <summary>Treasury (CRED is Majesty gold), live income, and what is locked in bounties.</summary>
        private void DrawTreasuryCrest(Rect r)
        {
            _hitRects.Add(r);
            int gold = _loop.Resources != null ? _loop.Resources.Get(ResourceId.Metals) : 0;
            int escrow = _loop.Economy != null ? _loop.Economy.EscrowedMetals : 0;
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
            string rate = perMin >= 1f ? $"+{perMin:F0} EU / MIN" : "NO INCOME YET";
            if (escrow > 0) rate += $"  ·  {escrow:N0} IN BOUNTIES";
            var prev = _treasuryRate.normal.textColor;
            _treasuryRate.normal.textColor = thin ? Alarm : (perMin >= 1f ? Good : TextMuted);
            GUI.Label(new Rect(r.x + 26f, r.y + 32f, r.width - 52f, 13f), thin ? "TREASURY LOW  ·  " + rate : rate, _treasuryRate);
            _treasuryRate.normal.textColor = prev;
        }

        /// <summary>The colony at a glance: heroes, tax houses, collectors on their rounds, dens left.</summary>
        private void DrawStockpile(Rect r)
        {
            GUI.Label(new Rect(r.x, r.y - 2f, r.width, 14f),
                $"{(_loop.BodyProfile != null ? _loop.BodyProfile.DisplayName.ToUpperInvariant() : "COLONY")}  ·  COLONY", _section);

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

            float top = r.y + 17f;
            float cw = (r.width - 6f) * 0.5f;
            float ch = 28f;
            ResourceChip(new Rect(r.x, top, cw, ch), "HEROES", heroes, heroes <= 0);
            ResourceChip(new Rect(r.x + cw + 6f, top, cw, ch), "HOUSES", houses, false,
                solar > 0 ? $"{solar} solar" : null);
            ResourceChip(new Rect(r.x, top + ch + 4f, cw, ch), "TAXMEN", collectors, collectors <= 0 || replacing > 0,
                replacing > 0 ? $"-{replacing} replacing" : (bags > 0 ? $"{bags} carried" : null));
            ResourceChip(new Rect(r.x + cw + 6f, top + ch + 4f, cw, ch), halted ? "HALTED" : "VILLAGE",
                building ? pct : 0, halted, building ? "% built" : "idle");
        }

        /// <summary>Where the gold is on its way home: building tills, collector bags, daily tax.</summary>
        private void DrawLevyLine(Rect r)
        {
            var village = _loop.Village;
            int tills = _loop.SittingLevy;
            int bags = village != null ? village.Collectors.GoldInTransit : 0;
            int collectors = village != null ? village.Collectors.Count : 0;
            int daily = village != null ? village.DailyBuildingTax() : 0;
            string line = $"TILLS {tills:N0}   ·   COLLECTORS {collectors} carrying {bags:N0}   ·   TAX {daily:N0}/day";
            GUI.Label(r, line, _microCenter);
        }

        private void DrawClockAndThreat(Rect r)
        {
            int sol = 1 + Mathf.FloorToInt((_loop.Mission != null ? _loop.Mission.MissionElapsed : 0f) / MajestyEconomy.DaySeconds);
            HudSkin.Icon(new Rect(r.x, r.y, 13f, 13f), UiIcons.Get(IconId.Sun), Gold);
            GUI.Label(new Rect(r.x + 18f, r.y - 1f, r.width - 90f, 15f), $"SOL {sol}", _section);
            GUI.Label(new Rect(r.x, r.y + 15f, r.width - 76f, 12f), ReplayRules.HudTag, _micro);
            DrawSpeedControl(new Rect(r.xMax - 64f, r.y, 62f, 26f));

            int posted = _loop.Flags != null && _loop.Flags.Flags != null ? _loop.Flags.Flags.Count : 0;
            GUI.Label(new Rect(r.x, r.y + 30f, 70f, 13f), "BOUNTIES", _caps);
            GUI.Label(new Rect(r.x + 60f, r.y + 30f, r.width - 62f, 13f),
                posted <= 0 ? "none posted" : $"{posted} posted", _microRight);

            float threat = _loop.FocusedLocalThreat;
            GUI.Label(new Rect(r.x, r.y + 44f, 60f, 13f), "THREAT", _caps);
            var prev = _microRight.normal.textColor;
            _microRight.normal.textColor = threat > 0.6f ? Alarm : threat > 0.3f ? Accent : TextMuted;
            GUI.Label(new Rect(r.xMax - 44f, r.y + 44f, 42f, 13f), $"{threat * 100f:F0}%", _microRight);
            _microRight.normal.textColor = prev;
            DrawSegments(new Rect(r.x, r.y + 59f, r.width - 2f, 4f), threat, 12);
            var rating = _loop.CurrentRating;
            GUI.Label(new Rect(r.x, r.y + 65f, r.width, 12f), Truncate(rating.Summary, 34), _micro);
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
                float y = stackTop - 12f - (n - i) * 18f;
                var rr = new Rect(x, y, w, 18f);
                float a = 0.45f + 0.55f * (i + 1f) / n;
                float textW = Mathf.Min(w, _logLine.CalcSize(new GUIContent(e.Line)).x);
                HudSkin.Band(new Rect(rr.center.x - textW * 0.5f - 60f, rr.y - 1f, textW + 120f, rr.height + 2f), 0.42f * a);
                var prev = _logLine.normal.textColor;
                _logLine.normal.textColor = new Color(TextPrimary.r, TextPrimary.g, TextPrimary.b, a);
                HudSkin.ShadowLabel(rr, e.Line, _logLine, 0.7f);
                _logLine.normal.textColor = prev;
            }
        }

        private bool SquareAction(Rect r, IconId icon, string label, string hotkey, bool on)
        {
            bool hot = r.Contains(Event.current.mousePosition);
            if (on) HudSkin.Glow(r, new Color(Accent.r, Accent.g, Accent.b, 0.45f), 0.6f);
            HudSkin.DrawPlate(r, on ? HudSkin.Plate.Primary : HudSkin.Plate.Quiet, hot);
            bool hit = GUI.Button(r, GUIContent.none, GUIStyle.none);

            Color tint = on ? Ink : hot ? Color.white : new Color(0.88f, 0.86f, 0.82f);
            HudSkin.Icon(new Rect(r.center.x - 12f, r.center.y - 14f, 24f, 24f), UiIcons.Get(icon), tint);
            var prev = _badge.normal.textColor;
            _badge.normal.textColor = on ? new Color(Ink.r, Ink.g, Ink.b, 0.75f) : hot ? Gold : HudSkin.TextFaint;
            GUI.Label(new Rect(r.x + 2f, r.yMax - 14f, r.width - 4f, 12f), hotkey, _badge);
            _badge.normal.textColor = prev;
            Tooltip(r, $"{label}   ·   {hotkey}");
            return hit;
        }

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
        }

        public void ExpandMenu(OverseerTool tool)
        {
            if (tool == OverseerTool.Build) _buildMinimized = false;
            if (tool == OverseerTool.Flag) _flagMinimized = false;
        }

        public void ClearMinimizedMenus()
        {
            _buildMinimized = false;
            _flagMinimized = false;
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

            return false;
        }

        private void DrawTechPanel()
        {
            if (!_techOpen) return;
            var research = _loop.Research;
            if (research == null) return;

            const float panelW = 360f;
            float top = Mathf.Max(M + 140f, RightColumnBottom + 10f);
            float panelH = Mathf.Clamp(_sh - M - MapSize - 24f - top, 220f, 480f);
            var rect = new Rect(_sw - M - panelW, top, panelW, panelH);
            RightColumnBottom = rect.yMax;
            var c = Panel(rect, "Research · T");

            string launch = research.LaunchTechLabel(_loop.ActiveBody);
            GUI.Label(new Rect(c.x, c.y, c.width, 14f),
                $"Labs {research.LabCount} · rate {research.CurrentRate:F1}/s · tip {launch}", _micro);
            float y = c.y + 18f;

            if (research.ActiveTech != TechId.None)
            {
                var active = TechCatalog.Get(research.ActiveTech);
                string name = active != null ? active.DisplayName : research.ActiveTech.ToString();
                float frac = research.ActiveCost > 0f ? research.ActiveProgress / research.ActiveCost : 0f;
                GUI.Label(new Rect(c.x, y, c.width, 14f), $"Active: {name}", _body);
                y += 16f;
                Meter(new Rect(c.x, y, c.width, 7f), frac, Accent);
                y += 12f;
                GUI.Label(new Rect(c.x, y, c.width, 13f),
                    $"{research.ActiveProgress:F0}/{research.ActiveCost:F0} science" +
                    (research.LastEvent == "awaiting_stockpile" ? " · need metals/ice" : ""),
                    _micro);
                y += 18f;
            }
            else
            {
                var rec = research.RecommendedNext();
                var recDef = TechCatalog.Get(rec);
                string bank = research.BankedScience > 0.5f
                    ? $"Science banked {research.BankedScience:F0}. "
                    : "";
                string tip = recDef != null
                    ? $"{bank}Click a tech to start — next pick {recDef.DisplayName}."
                    : $"{bank}Tree complete — launch tech unlocked.";
                GUI.Label(new Rect(c.x, y, c.width, 14f), tip, _muted);
                y += 20f;
            }

            Fill(new Rect(c.x, y, c.width, 1f), Hairline);
            y += 8f;

            float listH = c.yMax - y;
            var view = new Rect(c.x, y, c.width, listH);
            var techs = TechCatalog.All;
            int visibleTechs = 0;
            for (int i = 0; i < techs.Count; i++)
            {
                if (DemoSlice.ShowTech(techs[i].Id))
                    visibleTechs++;
            }
            var content = new Rect(0f, 0f, c.width - 18f, Mathf.Max(54f, visibleTechs * 54f));
            _techScroll = GUI.BeginScrollView(view, _techScroll, content);

            float rowY = 0f;
            for (int i = 0; i < techs.Count; i++)
            {
                var t = techs[i];
                if (!DemoSlice.ShowTech(t.Id)) continue;
                bool done = research.IsUnlocked(t.Id);
                bool can = research.CanSelect(t.Id);
                bool active = research.ActiveTech == t.Id;
                var row = new Rect(0f, rowY, content.width, 50f);

                if (GUI.Button(row, GUIContent.none, active ? _rowOn : _rowOff) && can)
                {
                    if (research.TrySelect(t.Id))
                        _techOpen = false;
                }

                string mark = done ? "DONE" : active ? "…" : can ? (t.SecretProject ? "★" : "GO") : "—";
                Color markC = done ? Good : active ? Accent : can ? TextPrimary : TextMuted;
                string title = t.SecretProject ? $"★ {t.DisplayName}" : t.DisplayName;
                GUI.Label(new Rect(row.x + 6f, row.y + 4f, row.width - 50f, 16f), title, _value);
                var prev = _microRight.normal.textColor;
                _microRight.normal.textColor = markC;
                GUI.Label(new Rect(row.xMax - 44f, row.y + 4f, 40f, 16f), mark, _microRight);
                _microRight.normal.textColor = prev;

                GUI.Label(new Rect(row.x + 6f, row.y + 22f, row.width - 12f, 24f),
                    Truncate(t.Description, 58), _micro);

                if (!done)
                {
                    float p = research.Progress01(t.Id);
                    if (p > 0.01f)
                        Meter(new Rect(row.x + 6f, row.yMax - 6f, row.width - 12f, 3f), p, Accent);
                }

                rowY += 54f;
            }

            GUI.EndScrollView();
        }

        private void DrawToolPopups(float dockTop)
        {
            if (_loop.ActiveTool == OverseerTool.Flag)
                DrawFlagPopup(dockTop);
            else if (_loop.ActiveTool == OverseerTool.Build)
                DrawBuildPopup(dockTop);
        }

        private void DrawFlagPopup(float dockTop)
        {
            var fp = _loop.FlagInput;
            if (fp == null) return;

            if (_flagMinimized)
            {
                DrawMinimizedToolStrip(dockTop, DockLeft(), 300f, FlagStripLabel(fp), "G catalog");
                return;
            }

            const float popupW = 300f;
            const float popupH = 398f;
            float left = DockLeft();
            var rect = new Rect(left, dockTop - 8f - popupH, popupW, popupH);
            _contentBottom = rect.y;
            var c = Panel(rect, "Flag orders");

            GUI.Label(new Rect(c.x, c.y, c.width, 13f), FlagBoardLine(), _micro);
            float y = c.y + 17f;

            y = FlagRow(fp, new Rect(c.x, y, c.width, 24f), "F1  Explore  ·  survey 22 m / 90 s", fp.ExploreFlag);
            y = FlagRow(fp, new Rect(c.x, y, c.width, 24f), "F2  Clear Threat", fp.ClearThreatFlag);
            y = FlagRow(fp, new Rect(c.x, y, c.width, 24f), "F3  Build Here", fp.BuildFlag);
            y = FlagRow(fp, new Rect(c.x, y, c.width, 24f), "F4  Extract", fp.ExtractFlag);
            y = FlagRow(fp, new Rect(c.x, y, c.width, 24f), "F5  Defend  ·  watch 50 s after", fp.DefendFlag);
            y = FlagRow(fp, new Rect(c.x, y, c.width, 24f), "I   Research Site", fp.ResearchSiteFlag);
            y = FlagRow(fp, new Rect(c.x, y, c.width, 24f), "O   Outpost", fp.OutpostFlag);
            y = FlagRow(fp, new Rect(c.x, y, c.width, 24f), "U   Terraform", fp.TerraformFlag);

            y += 4f;
            int metCost = SimpleEconomy.BountyMetalsCost(_loop.FlagBounty);
            bool canPay = fp.CanAffordSelectedBounty();
            GUI.Label(new Rect(c.x, y, 60f, 24f), "BOUNTY", _micro);
            GUI.Label(new Rect(c.x + 58f, y, 70f, 24f), $"${_loop.FlagBounty:F0}", _value);
            if (GUI.Button(new Rect(c.xMax - 58f, y + 2f, 26f, 20f), "−", _chipOff)) fp.NudgeBounty(-MajestyEconomy.FlagBountyStep);
            if (GUI.Button(new Rect(c.xMax - 28f, y + 2f, 26f, 20f), "+", _chipOff)) fp.NudgeBounty(MajestyEconomy.FlagBountyStep);

            y += 22f;
            var prevC = _micro.normal.textColor;
            _micro.normal.textColor = canPay ? TextMuted : Alarm;
            GUI.Label(new Rect(c.x, y, c.width, 13f),
                canPay ? $"escrow {metCost} EU · LMB places · RMB flag refunds" : $"need {metCost} EU — raise stockpile",
                _micro);
            _micro.normal.textColor = prevC;

            y += 18f;
            DrawOrdersField(fp, new Rect(c.x, y, c.width, 40f));
        }

        private const string OrdersControl = "sm_flag_orders";
        private bool _ordersFieldDrawn;

        /// <summary>
        /// Optional written orders for the next flag ("stay away unless level 9+", "no scouts").
        /// Parsed on the spot so the player sees what heroes will understand before posting.
        /// </summary>
        private void DrawOrdersField(FlagPlacementInput fp, Rect r)
        {
            _ordersFieldDrawn = true;
            var field = new Rect(r.x + 52f, r.y, r.width - 52f, 20f);
            GUI.Label(new Rect(r.x, r.y + 2f, 52f, 18f), "ORDERS", _micro);

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
                ? "optional · e.g. \"stay away unless level 9+\""
                : !string.IsNullOrEmpty(read) ? "understood: " + read
                : LayaBridge.Instance != null && LayaBridge.Instance.Online ? "not understood — local AI will try on post"
                : "not understood — heroes will ignore it";
            GUI.Label(new Rect(r.x, r.y + 22f, r.width, 14f), line, _micro);
        }

        private float FlagRow(FlagPlacementInput fp, Rect r, string label, FlagData data)
        {
            if (data == null) return r.y;

            bool on = fp.SelectedFlag == data;
            if (GUI.Button(r, GUIContent.none, on ? _rowOn : _rowOff))
            {
                _loop.SetTool(OverseerTool.Flag);
                fp.SelectFlag(data);
            }

            HudSkin.Dot(new Vector2(r.x + 11f, r.center.y), 8f, data.bannerColor);
            GUI.Label(new Rect(r.x + 22f, r.y, r.width - 28f, r.height), label, on ? _onText : _action);
            return r.yMax + 4f;
        }

        private void DrawBuildPopup(float dockTop)
        {
            var bp = _loop.BuildInput;
            if (bp == null || bp.Catalog == null) return;

            if (_buildMinimized)
            {
                string name = bp.Selected != null ? bp.Selected.displayName : "module";
                DrawMinimizedToolStrip(dockTop, DockLeft() + 102f, 320f,
                    name.ToUpperInvariant() + " · LMB place", "B catalog");
                return;
            }

            var visible = bp.VisibleIndices;
            int count = visible.Count;
            const float popupW = 320f;
            float fullH = count * 28f;
            float popupH = 58f + Mathf.Min(336f, fullH);
            float left = DockLeft() + 102f;
            var rect = new Rect(left, dockTop - 8f - popupH, popupW, popupH);
            _contentBottom = rect.y;
            var c = Panel(rect, "Build catalog");

            GUI.Label(new Rect(c.x, c.y, c.width, 13f),
                DemoSettings.FirstHourDemo
                    ? "1 Engineer workshop · LMB on open ground"
                    : "Pick a building · LMB on open ground",
                _micro);
            float y = c.y + 17f;

            var view = new Rect(c.x, y, c.width, c.yMax - y);
            var content = new Rect(0f, 0f, c.width - 18f, fullH);
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
                var r = new Rect(0f, rowY, content.width, 24f);

                if (GUI.Button(r, GUIContent.none, on ? _rowOn : _rowOff) && !locked)
                {
                    _loop.SetTool(OverseerTool.Build);
                    bp.SelectBuilding(i);
                }

                GUI.Label(new Rect(r.x + 8f, r.y, 18f, r.height), BuildHotkeyLabel(slot), on ? _onText : _micro);
                var nameStyle = on ? _onText : _action;
                var prevColor = nameStyle.normal.textColor;
                if (!on && (!canAfford || locked)) nameStyle.normal.textColor = TextMuted;
                GUI.Label(new Rect(r.x + 26f, r.y, r.width - 110f, r.height),
                    BuildRowLabel(b), nameStyle);
                nameStyle.normal.textColor = prevColor;

                var costStyle = _microRight;
                var prevCost = costStyle.normal.textColor;
                if (locked) costStyle.normal.textColor = TextMuted;
                else if (!canAfford) costStyle.normal.textColor = Alarm;
                GUI.Label(new Rect(r.xMax - 92f, r.y, 86f, r.height),
                    locked ? "NEED TECH" : FormatBuildCost(CostOf(b)), costStyle);
                costStyle.normal.textColor = prevCost;

                rowY += 28f;
            }

            GUI.EndScrollView();
        }

        private static string BuildHotkeyLabel(int index)
        {
            if (index < 9) return (index + 1).ToString();
            if (index == 9) return "0";
            return "·";
        }

        private static string BuildRowLabel(BuildingData b)
        {
            if (b == null) return "module";
            if (!string.IsNullOrEmpty(b.description))
                return $"{b.displayName}  ·  {b.description}";
            switch (b.category)
            {
                case BuildingCategory.Defense: return "Defense Battery  ·  auto-fires 18 m";
                case BuildingCategory.Mining: return "OPS Drop-off  ·  does not grow EU";
                case BuildingCategory.Mine: return "Nuclear Mine";
                case BuildingCategory.Power: return "Solar Farm";
                case BuildingCategory.ClimateLoom:
                case BuildingCategory.AegisSpire:
                case BuildingCategory.DeepArchive:
                    return $"{b.displayName}  ·  bonus while standing";
                default: return b.displayName;
            }
        }

        private static string FlagStripLabel(FlagPlacementInput fp)
        {
            var data = fp != null ? fp.SelectedFlag : null;
            string name = data != null && !string.IsNullOrEmpty(data.displayName)
                ? data.displayName
                : "flag";
            return name.ToUpperInvariant() + " · LMB place";
        }

        private void DrawMinimizedToolStrip(float dockTop, float left, float width, string title, string hint)
        {
            const float h = 36f;
            var rect = new Rect(left, dockTop - 8f - h, width, h);
            _contentBottom = rect.y;
            var c = Panel(rect, null);
            HudSkin.Pill(new Rect(rect.x + 6f, rect.y + 9f, 3f, rect.height - 18f), Accent);
            GUI.Label(new Rect(c.x, c.y, c.width - 88f, c.height), title, _value);
            GUI.Label(new Rect(c.xMax - 86f, c.y + 2f, 86f, c.height - 4f), hint, _microRight);
        }

        private bool Chip(Rect r, string label, bool on) =>
            GUI.Button(r, label, on ? _chipOn : _chipOff);

        private static bool ShiftHeld()
        {
            var e = Event.current;
            if (e != null && e.shift) return true;
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }

        /// <summary>F10 during Playing/Paused even when a HUD button has IMGUI focus.</summary>
        private void HandleDebugHopKeys()
        {
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
            if (cost == null || cost.Length == 0) return "free";
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
            ResourceId.Regolith => "REG",
            ResourceId.WaterIce => "ICE",
            ResourceId.Metals => "EU",
            ResourceId.Power => "PWR",
            _ => id.ToString().ToUpperInvariant()
        };

        private void DrawMissionPanel()
        {
            var mission = _loop.Mission;
            if (mission == null) return;

            var flags = _loop.Flags != null ? _loop.Flags.Flags : null;
            int flagN = flags != null ? Mathf.Min(3, flags.Count) : 0;
            float h = 136f;
            if (mission.DeadlineEnabled) h += 26f;
            h += 18f + flagN * 16f;

            var rect = new Rect(_sw - M - 300f, _playTop > 1f ? _playTop : M, 300f, h);
            RightColumnBottom = rect.yMax;
            var c = Panel(rect, "Objectives");
            float y = c.y;

            Stake(new Rect(c.x, y, c.width, 20f), mission.DensCleared,
                "Clear dens",
                mission.LairCount > 0
                    ? $"{mission.UnclearedLairs}/{mission.LairCount} left"
                    : $"{mission.StalkersRemaining} fauna");
            y += 20f;

            var set = _loop.Settlement;
            string sustainVal = set != null
                ? $"{mission.TreasuryCurrent:N0}/{mission.TreasuryGoal:N0} · {_loop.FormatHold(mission.SustainElapsed)}/{_loop.FormatHold(mission.SustainRequired)}"
                : _loop.FormatHold(mission.SustainElapsed);
            Stake(new Rect(c.x, y, c.width, 20f), mission.SustainComplete,
                "Grow the treasury",
                sustainVal);
            y += 20f;

            string launchNeed = _loop.Research != null
                ? _loop.Research.LaunchTechLabel(_loop.ActiveBody)
                : "craft";
            Stake(new Rect(c.x, y, c.width, 20f), mission.LaunchReady,
                "Launch craft",
                mission.LaunchReady ? "ready on pad" : $"need {launchNeed}");
            y += 22f;

            if (set != null)
            {
                GUI.Label(new Rect(c.x, y, c.width, 14f), set.SustainHint, _micro);
                y += 16f;
            }

            if (mission.DeadlineEnabled)
            {
                float left = Mathf.Max(0f, mission.MissionDeadline - mission.MissionElapsed);
                float frac = mission.MissionDeadline > 0f ? left / mission.MissionDeadline : 0f;
                GUI.Label(new Rect(c.x, y, 120f, 14f), "DEADLINE", _micro);
                GUI.Label(new Rect(c.xMax - 60f, y, 60f, 14f), _loop.FormatHold(left), _microRight);
                Meter(new Rect(c.x, y + 16f, c.width, 5f), frac, frac < 0.25f ? Alarm : Accent);
                y += 26f;
            }

            GUI.Label(new Rect(c.x, y, c.width, 14f), "FLAG LOG", _section);
            y += 16f;
            if (flagN <= 0)
            {
                GUI.Label(new Rect(c.x, y, c.width, 14f), "No flags posted — G to bounty.", _micro);
            }
            else
            {
                for (int i = 0; i < flagN; i++)
                {
                    var f = flags[flags.Count - flagN + i];
                    if (f?.Data == null) continue;
                    string claim = f.ClaimCount > 0
                        ? "claimed"
                        : (string.IsNullOrEmpty(f.InterestLabel) ? "open" : f.InterestLabel);
                    Color col = f.Data != null ? f.Data.bannerColor : Gold;
                    HudSkin.Dot(new Vector2(c.x + 4f, y + 7.5f), 7f, col);
                    string orders = f.Orders != null && f.Orders.HasRules ? $"  ·  {f.Orders.Summary()}" : "";
                    GUI.Label(new Rect(c.x + 12f, y, c.width - 12f, 15f),
                        $"{f.Data.displayName}  ${f.CurrentBounty:F0}  ·  {claim}{orders}", _micro);
                    y += 15f;
                }
            }
        }

        private void Stake(Rect r, bool done, string label, string value)
        {
            CheckBox(new Rect(r.x, r.y + 4f, 11f, 11f), done);
            GUI.Label(new Rect(r.x + 19f, r.y, r.width - 160f, r.height), label, done ? _muted : _body);
            GUI.Label(new Rect(r.xMax - 140f, r.y, 140f, r.height), value, _microRight);
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
            GUI.Label(new Rect(x, y, width, 13f), guild.BenefitBlurb, _micro);
            y += 14f;
            string label;
            if (!researched)
                label = $"Research {guild.BenefitName}";
            else if (dir.IsActive(guild.Id))
                label = $"{guild.BenefitName} {dir.Remaining(guild.Id):F0}s";
            else if (dir.CooldownLeft(guild.Id) > 0.5f)
                label = $"Cooldown {dir.CooldownLeft(guild.Id):F0}s";
            else
                label = $"ACTIVATE {guild.ActivateCost} EU";
            bool can = researched &&
                       dir.CanActivate(guild.Id, _loop.Research, _loop.Resources, true);
            if (Chip(new Rect(x, y, Mathf.Min(220f, width), 22f), label, can) && can)
                _loop.TryActivateGuildBenefit(guild.Id);
            return y + 24f;
        }

        private void DrawBuildingCard(ColonyStructure st)
        {
            const float cardW = 340f;
            bool yard = st.IsFobotYard || st.Category == BuildingCategory.FobotYard;
            bool wreckShop = st.IsWorkshop && st.HasPreferredClass &&
                             _loop.HasWreckFor(st.PreferredClass);
            bool padStall = st.IsLandingPad;
            float cardH = st.IsGuild ? 228f
                : yard || wreckShop || st.IsWatchtower || st.IsAidStation || padStall ? 188f
                : 148f;
            // After the first frame the card hugs what it drew instead of the per-kind guess.
            if (_cardFor == st && _cardMeasuredH > 0f) cardH = _cardMeasuredH;
            float y0 = _contentBottom - 8f - cardH;
            var rect = new Rect(M, y0, cardW, cardH);
            var c = Panel(rect, null);
            HudSkin.RuleH(new Rect(rect.x, rect.y, rect.width, 1f), Accent);

            float row = c.y;
            GUI.Label(new Rect(c.x, row, c.width, 16f), st.DisplayName, _value);
            row += 18f;

            string role = st.IsWorkshop
                ? (RefabLabel(st) ?? (st.RobotFabricated ? "Workshop · robot online" : "Workshop · fabricating…"))
                : st.IsGuild
                    ? (st.HasPreferredClass
                        ? (RobotGuildCatalog.ForClass(st.PreferredClass)?.CatalogLine ?? st.DisplayName)
                        : "Guild Hall · assign a class")
                    : st.IsWonder ? "Secret Project landmark"
                    : st.IsFobotYard || st.Category == BuildingCategory.FobotYard ? "Fobot Yard · wrecks wait"
                    : st.IsWatchtower
                        ? (st.LaserArmed ? "Watchtower · lasers armed" : "Watchtower · guard post")
                    : st.IsAidStation ? "Aid Station · paid patch"
                    : st.IsLandingPad ? "Weigh-station · tank vs wallet"
                    : st.IsResidential ? "Habitat · colonists"
                    : st.Role.ToString();
            string worker = st.IsResidential
                ? "humans"
                : (st.HasPreferredClass ? ColonyStructure.ClassLabel(st.PreferredClass) : "—");
            string beds = st.IsResidential
                ? $" · residents {st.Residents}/{st.ResidentCapacity}"
                : "";
            GUI.Label(new Rect(c.x, row, c.width, 13f),
                st.IsResidential
                    ? $"{role}{beds}"
                    : $"{role} · {worker} {st.WorkerCount}/{st.WorkerSlots}{beds}",
                _micro);
            row += 15f;

            Bar(new Rect(c.x, row, c.width, 14f), "HP", st.Health01, HpFill);
            row += 18f;

            string workers = st.IsResidential
                ? (st.Residents > 0
                    ? (st.LevyPurse > 0
                        ? $"House tax {st.LevyPurse} EU sitting — a tax collector walks it home."
                        : "Colonists indoors — house tax sits until a tax collector walks it home.")
                    : "Empty beds — seed crew arrives with the first HAB.")
                : (st.LevyPurse > 0 && !st.IsTreasuryChest
                    ? $"{FormatWorkers(st)} · till {st.LevyPurse} EU"
                    : FormatWorkers(st));
            GUI.Label(new Rect(c.x, row, c.width, 13f), workers, _micro);
            row += 16f;

            if (st.IsResidential)
            {
                GUI.Label(new Rect(c.x, row, c.width, 22f),
                    st.LevyPurse > 0
                        ? $"House tax {st.LevyPurse} EU — tax collectors carry it to Commons."
                        : "Humans stay in HABs. Outdoor work is robots from workshops.", _micro);
            }
            else if (st.IsFobotYard)
            {
                string wrecks = _loop.WreckSummary();
                GUI.Label(new Rect(c.x, row, c.width, 22f),
                    string.IsNullOrEmpty(wrecks)
                        ? "Wrecks wait here. Pay credits to stand THIS ego up."
                        : wrecks,
                    _micro);
                row += 24f;
                int bill = _loop.FieldReviveMet;
                if (Chip(new Rect(c.x, row, 140f, 22f), bill > 0 ? $"PAY {bill} EU" : "PAY YARD",
                        bill > 0 && _loop.CanPayYard))
                    _loop.PayFobotYard();
                GUI.Label(new Rect(c.x + 148f, row + 4f, c.width - 148f, 16f),
                    "Y  ·  120s", _micro);
            }
            else if (st.IsLandingPad)
            {
                GUI.Label(new Rect(c.x, row, c.width, 22f),
                    "Trade ships land here. Each caravan pays EU into the Market till.", _micro);
            }
            else if (st.IsGuild)
            {
                var guild = st.HasPreferredClass
                    ? RobotGuildCatalog.ForClass(st.PreferredClass)
                    : null;
                if (guild != null)
                {
                    GUI.Label(new Rect(c.x, row, c.width, 13f), guild.Motto, _micro);
                    row += 14f;
                    GUI.Label(new Rect(c.x, row, c.width, 13f),
                        $"Wants {guild.Wants} · ignores {guild.Ignores}", _micro);
                    row += 14f;
                    GUI.Label(new Rect(c.x, row, c.width, 13f),
                        "Flags near this hall pull them (no new robot).", _micro);
                    row += 14f;
                    row = DrawGuildBenefit(c.x, row, c.width, guild);
                }
                else
                {
                    GUI.Label(new Rect(c.x, row, c.width, 13f),
                        "Assign a class. Flags near this hall pull them (no new robot).", _micro);
                    row += 14f;
                }

                if (!st.ClassLocked)
                {
                    if (Chip(new Rect(c.x, row, 54f, 22f), "SCOUT",
                            st.HasPreferredClass && st.PreferredClass == SpecialistClass.ScoutDrone))
                        _loop.SetSelectedWorkplaceClass(SpecialistClass.ScoutDrone);
                    if (Chip(new Rect(c.x + 58f, row, 54f, 22f), "ENG",
                            st.HasPreferredClass && st.PreferredClass == SpecialistClass.EngineerBot))
                        _loop.SetSelectedWorkplaceClass(SpecialistClass.EngineerBot);
                    if (Chip(new Rect(c.x + 116f, row, 54f, 22f), "DEF",
                            st.HasPreferredClass && st.PreferredClass == SpecialistClass.DefenseMech))
                        _loop.SetSelectedWorkplaceClass(SpecialistClass.DefenseMech);
                    if (Chip(new Rect(c.x + 174f, row, 54f, 22f), "MED",
                            st.HasPreferredClass && st.PreferredClass == SpecialistClass.Medic))
                        _loop.SetSelectedWorkplaceClass(SpecialistClass.Medic);
                }
            }
            else if (st.Category == BuildingCategory.FobotYard)
            {
                int met = _loop.FieldReviveMet;
                GUI.Label(new Rect(c.x, row, c.width, 13f),
                    _loop.NeedsFieldRevive
                        ? $"Stand-up bill {met} EU. Cost scales with level."
                        : "No wrecks. Dock this yard before anyone goes down.", _micro);
                var wrecks = _loop.Corpses;
                if (wrecks != null && wrecks.Count > 0)
                {
                    row += 14f;
                    var names = new System.Text.StringBuilder();
                    for (int i = 0; i < wrecks.Count && i < 3; i++)
                    {
                        if (i > 0) names.Append(" · ");
                        names.Append(ColonyStructure.ClassLabel(wrecks[i].Class));
                        names.Append(" L");
                        names.Append(Mathf.Max(1, wrecks[i].Level));
                    }
                    GUI.Label(new Rect(c.x, row, c.width, 13f), names.ToString(), _micro);
                }
                row += 16f;
                bool canPay = _loop.NeedsFieldRevive && _loop.HasFobotYard &&
                              _loop.FieldReviveReadyIn <= 0.5f;
                if (Chip(new Rect(c.x, row, 220f, 22f),
                        canPay ? $"PAY {met} EU" : "YARD IDLE", canPay) && canPay)
                    _loop.RetryParty();
            }
            else if (st.IsWatchtower)
            {
                int guards = st.WorkerCount;
                GUI.Label(new Rect(c.x, row, c.width, 13f),
                    guards > 0
                        ? "Guard posted. Haul can drop EU here when Commons is far."
                        : "Empty post. Aegis / Rim Watch will clock in.", _micro);
                row += 16f;
                if (st.LaserArmed)
                {
                    GUI.Label(new Rect(c.x, row, c.width, 22f), "Lasers armed — 18 m.", _micro);
                }
                else
                {
                    bool can = _loop.Resources != null &&
                               _loop.Resources.Get(ResourceId.Metals) >= OverseerRules.WatchtowerLaserCost;
                    if (Chip(new Rect(c.x, row, 220f, 22f),
                            $"ARM LASERS {OverseerRules.WatchtowerLaserCost} EU", can) && can)
                        _loop.TryArmWatchtower(st);
                }
            }
            else if (st.IsAidStation)
            {
                GUI.Label(new Rect(c.x, row, c.width, 22f),
                    st.WorkerCount > 0
                        ? $"Triage posted. Patch {OverseerRules.AidStationHealCost} EU."
                        : $"Empty bay. Hurt robots pay {OverseerRules.AidStationHealCost} EU.", _micro);
            }
            else if (st.Category == BuildingCategory.Market)
            {
                GUI.Label(new Rect(c.x, row, c.width, 22f),
                    $"Potions + necklace. Pays {MajestyEconomy.MarketDailyTax} EU/day into its till.", _micro);
            }
            else if (st.Category == BuildingCategory.Blacksmith)
            {
                GUI.Label(new Rect(c.x, row, c.width, 22f),
                    "Lodge arms and armor. Heroes buy with their EU.", _micro);
            }
            else if (!st.ClassLocked)
            {
                if (Chip(new Rect(c.x, row, 54f, 22f), "SCOUT",
                        st.HasPreferredClass && st.PreferredClass == SpecialistClass.ScoutDrone))
                    _loop.SetSelectedWorkplaceClass(SpecialistClass.ScoutDrone);
                if (Chip(new Rect(c.x + 58f, row, 54f, 22f), "ENG",
                        st.HasPreferredClass && st.PreferredClass == SpecialistClass.EngineerBot))
                    _loop.SetSelectedWorkplaceClass(SpecialistClass.EngineerBot);
                if (Chip(new Rect(c.x + 116f, row, 54f, 22f), "DEF",
                        st.HasPreferredClass && st.PreferredClass == SpecialistClass.DefenseMech))
                    _loop.SetSelectedWorkplaceClass(SpecialistClass.DefenseMech);
                if (Chip(new Rect(c.x + 174f, row, 54f, 22f), "MED",
                        st.HasPreferredClass && st.PreferredClass == SpecialistClass.Medic))
                    _loop.SetSelectedWorkplaceClass(SpecialistClass.Medic);
            }
            else
            {
                GUI.Label(new Rect(c.x, row, c.width, 22f),
                    st.IsWonder
                        ? "Landmark — bonuses while standing."
                        : st.RobotFabricated
                            ? $"Fabricates {ColonyStructure.ClassLabel(st.PreferredClass)} — flags nearby pull them."
                            : $"Building a {ColonyStructure.ClassLabel(st.PreferredClass)} robot…",
                    _micro);
                var wreckClass = ColonyStructure.RobotClassForWorkshop(st.Category);
                if (wreckClass.HasValue && _loop.HasWreckFor(wreckClass.Value))
                {
                    row += 24f;
                    int met = OverseerRules.RefabMetals(st.SourceData);
                    if (Chip(new Rect(c.x, row, 150f, 22f), $"RE-FAB L1 {met} EU", true))
                        _loop.TryRefabRookie(st);
                    GUI.Label(new Rect(c.x + 158f, row + 4f, c.width - 158f, 16f),
                        "new chassis · wreck gone", _micro);
                }
            }
            row += 26f;

            if (GUI.Button(new Rect(c.x, row, 116f, 24f), "FLAG HERE", _chipOff))
                _loop.PostAttractFlagOnSelected();
            GUI.Label(new Rect(c.x + 124f, row + 4f, c.width - 124f, 20f),
                "Progress via research & conquest gates", _micro);
            _cardFor = st;
            _cardMeasuredH = Mathf.Max(96f, row + 24f + Pad - rect.y);
        }

        private static string FormatWorkers(ColonyStructure st)
        {
            if (st.WorkerCount <= 0) return "No one working here yet.";
            var names = new System.Text.StringBuilder("On duty: ");
            for (int i = 0; i < st.Workers.Count; i++)
            {
                var w = st.Workers[i];
                if (w == null) continue;
                if (i > 0) names.Append(", ");
                names.Append(w.Data != null ? w.Data.displayName : "Specialist");
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
                : "ROBOT";
            return $"RE-FAB {Mathf.FloorToInt(left / 60f)}:{Mathf.FloorToInt(left % 60f):00} {cls}";
        }

        private void DrawSpecialistCards()
        {
            var selected = _loop.SelectedAgents;
            if (selected == null || selected.Count == 0)
                return;

            int n = selected.Count;
            float avail = _sw - M * 2f - (n - 1) * 8f;
            float cardW = Mathf.Clamp(avail / n, 168f, 230f);
            const float cardH = 222f;
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
                GUI.Label(new Rect(c.x, row, c.width - 50f, 16f), $"{a.Record.Name}  L{a.Level}", _value);
                {
                    string tagLabel = a.IsIncapacitated ? "DOWN" : RosterStatus(a);
                    Color tagFill = a.IsIncapacitated ? Alarm
                        : a.CurrentAction == SpecialistAction.PursueFlag ? Accent
                        : a.CurrentAction == SpecialistAction.Rest ? new Color(0.55f, 0.78f, 1f)
                        : Good;
                    var tag = new Rect(c.xMax - 46f, row + 1f, 46f, 14f);
                    HudSkin.Pill(tag, tagFill);
                    var prev = _pill.normal.textColor;
                    _pill.normal.textColor = Ink;
                    GUI.Label(tag, tagLabel, _pill);
                    _pill.normal.textColor = prev;
                }
                row += 18f;

                int campus = ColonyLayout.NearestCampusIndex(a.transform.position);
                GUI.Label(new Rect(c.x, row, c.width, 13f),
                    $"{a.Data?.displayName ?? "Specialist"} · {ColonyLayout.CampusLabel(campus)} · Asks {a.HireMin} EU · Purse {a.Credits:F0}{LevyCarryLine(a)}", _micro);
                row += 16f;

                var prevAct = _action.normal.textColor;
                _action.normal.textColor = ActionTint(a.CurrentAction);
                GUI.Label(new Rect(c.x, row, c.width, 14f), FormatAction(a), _action);
                _action.normal.textColor = prevAct;
                row += 16f;
                if (!string.IsNullOrEmpty(a.Data?.description))
                {
                    GUI.Label(new Rect(c.x, row, c.width, 13f), Truncate(a.Data.description, 42), _micro);
                    row += 14f;
                }

                Bar(new Rect(c.x, row, c.width, 14f), "HP", a.HealthNormalized, HpFill);
                row += 16f;
                Bar(new Rect(c.x, row, c.width, 14f), "FATIGUE", a.Fatigue, FatigueFill);
                row += 16f;
                GUI.Label(new Rect(c.x, row, c.width, 13f),
                    $"MOVE {a.EffectiveMoveSpeed:F1}  ·  WORK {a.EffectiveWorkRate:F2}  ·  status", _micro);
                row += 15f;

                string gene = a.LevyCarried > 0
                    ? $"levy {a.LevyCarried} EU"
                    : a.GeneSecondsLeft > 0.5f
                        ? $"gene {a.GeneSecondsLeft:F0}s"
                        : "shop at rest beacon";
                GUI.Label(new Rect(c.x, row, c.width, 13f),
                    Truncate($"{Truncate(a.LastReason, 14)} · {a.SuitLabel} · {gene}", 42), _micro);
                row += 18f;
                if (GUI.Button(new Rect(c.x, row, c.width, 22f), n == 1 ? "TALK  ·  ENTER" : "TALK", _chipOff))
                {
                    _loop.SelectOnly(a);
                    OpenChat(a);
                }
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
            var rect = new Rect(M, _contentBottom - 8f - h, 440f, h);
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
            GUI.Label(new Rect(c.x + 44f, c.y - 1f, c.width - 130f, 20f), name, _chatTitle);
            GUI.Label(new Rect(c.x + 44f, c.y + 18f, c.width - 130f, 14f),
                $"“{record.Designation}”  ·  {a.Data?.displayName ?? "Specialist"} L{a.Level}  ·  {record.Rank}", _micro);

            string tagLabel = a.IsIncapacitated ? "DOWN" : RosterStatus(a);
            var tag = new Rect(c.xMax - 82f, c.y + 3f, 46f, 14f);
            HudSkin.Pill(tag, a.IsIncapacitated ? Alarm : Good);
            var prevPill = _pill.normal.textColor;
            _pill.normal.textColor = Ink;
            GUI.Label(tag, tagLabel, _pill);
            _pill.normal.textColor = prevPill;
            if (GUI.Button(new Rect(c.xMax - 26f, c.y, 26f, 22f), "×", _chipOff))
            {
                CloseChat();
                return;
            }
            Fill(new Rect(c.x, c.y + 42f, c.width, 1f), new Color(1f, 1f, 1f, 0.06f));

            const float inputH = 30f;
            var view = new Rect(c.x, c.y + 50f, c.width, c.yMax - (c.y + 50f) - inputH - 20f);
            DrawChatLog(a, view, name);

            string status;
            Color statusColor = TextMuted;
            if (!HeroNarrator.Enabled) status = "";
            else if (!HeroNarrator.ChatOnline) status = "Waiting for the local model  ·  Tools/local_ai/start_narrator.sh --no-game";
            else if (HeroNarrator.Replying && !HeroNarrator.ReplyStarted) status = HeroNarrator.ReplyWait > 3f ? $"{name} is taking a while…" : $"{name} is thinking…";
            else if (HeroNarrator.Replying) status = $"{name} is talking…";
            else if (!string.IsNullOrEmpty(HeroNarrator.LastChatError)) { status = HeroNarrator.LastChatError; statusColor = Alarm; }
            else status = $"ENTER send  ·  ESC close  ·  {HeroNarrator.ServerLabel}";
            var prevMicro = _micro.normal.textColor;
            _micro.normal.textColor = statusColor;
            GUI.Label(new Rect(c.x, view.yMax + 3f, c.width, 14f), status, _micro);
            _micro.normal.textColor = prevMicro;

            DrawChatInput(a, new Rect(c.x, c.yMax - inputH, c.width, inputH), name);
        }

        private void DrawChatLog(SpecialistAgent a, Rect view, string name)
        {
            if (!HeroNarrator.Enabled)
            {
                var msg = new Rect(view.x + 20f, view.y + view.height * 0.5f - 40f, view.width - 40f, 36f);
                GUI.Label(msg, "Hero lines are off. Conversations run on a small model on this computer — nothing leaves it.", _wrap);
                if (Chip(new Rect(view.center.x - 110f, msg.yMax + 6f, 220f, 28f), "TURN ON HERO LINES", true))
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
                    $"{name} glances up from the work. Ask about the job, the colony, or what it would take.", _chatHint);
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
            float bh = textH + 27f;
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
                GUI.Label(new Rect(x + 10f, y + 5f, bw - 20f, 12f), player ? "OVERSEER" : heroName.ToUpperInvariant(), who);
                who.normal.textColor = prevColor;
                who.alignment = prevAlign;
                GUI.Label(new Rect(x + 10f, y + 19f, textW, textH), text, _chatText);
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
            var field = new Rect(r.x, r.y, r.width - 76f, r.height);
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
                GUI.Label(new Rect(field.x + 9f, field.y, field.width - 16f, field.height), $"Say something to {name}…", _chatPlaceholder);
            if (_chatFocusPending)
            {
                GUI.FocusControl(ChatControl);
                _chatFocusPending = false;
            }

            bool ready = canSend && !string.IsNullOrWhiteSpace(_chatInput);
            var prev = GUI.color;
            if (!ready) GUI.color = new Color(prev.r, prev.g, prev.b, prev.a * 0.45f);
            if (GUI.Button(new Rect(r.xMax - 70f, r.y, 70f, r.height), "SEND", _chipOn) && ready)
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
            float tw = Mathf.Min(250f, _bubbleText.CalcSize(content).x + 2f);
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
                SpecialistAction.PursueFlag => "Working a flag",
                SpecialistAction.Rest => "Resting / shopping",
                SpecialistAction.Flee => "Fleeing to inn",
                SpecialistAction.Hunt => "Hunting fauna",
                SpecialistAction.Repair => "Repairing module",
                SpecialistAction.Wander => a.LastReason != null && a.LastReason.Contains("workshop")
                    ? "At workshop"
                    : a.LastReason != null && a.LastReason.Contains("levy_home")
                        ? "Walking levy home"
                        : a.LastReason != null && a.LastReason.Contains("levy_collect")
                            ? "Collecting HAB levy"
                            : "Kingdom vocation",
                _ => "Idle"
            };
            return string.IsNullOrEmpty(a.Status) ? action : $"{action} — {a.Status}";
        }

        private static string LevyCarryLine(SpecialistAgent a)
        {
            return a != null && a.LevyCarry > 0 ? $" · levy {a.LevyCarry}" : "";
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
                ? "Bounty escrowed from EU. Heroes keep a purse; colony tithes."
                : $"Posted {posted}  ·  claimed {claimed}  ·  heroes keep EU purse";
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
                const string hintText = "Build anywhere on open ground · houses pay tax · a workshop fabricates a robot";
                var hint = new Rect(M, _contentBottom - 8f - 26f, _micro.CalcSize(new GUIContent(hintText)).x + 46f, 26f);
                _hitRects.Add(hint);
                HudSkin.Panel(hint, 0.9f, false);
                HudSkin.Icon(new Rect(hint.x + 9f, hint.y + 6f, 14f, 14f), UiIcons.Get(IconId.GlyphBuild), Gold);
                GUI.Label(new Rect(hint.x + 30f, hint.y, hint.width - 36f, hint.height), hintText, _micro);
                return;
            }

            int rows = Mathf.Min(8, living);
            float h = 44f + rows * 18f;
            var rect = new Rect(M, _contentBottom - 8f - h, 268f, h);
            var c = Panel(rect, "Roster · status");
            float y = c.y;
            GUI.Label(new Rect(c.x, y, c.width, 14f), ClassReadout(agents), _micro);
            y += 16f;
            int drawn = 0;
            for (int i = 0; i < agents.Count && drawn < rows; i++)
            {
                var a = agents[i];
                if (a == null || !a.IsAlive) continue;
                var cls = a.Data != null ? a.Data.specialistClass : SpecialistClass.ScoutDrone;
                string status = RosterStatus(a);
                var row = new Rect(c.x, y, c.width, 16f);
                if (GUI.Button(row, GUIContent.none, _rowOff))
                    _loop.SelectOnly(a);
                HudSkin.Dot(new Vector2(row.x + 6f, row.center.y), 6f, ClassTint(cls));
                GUI.Label(new Rect(row.x + 14f, row.y, 66f, 16f),
                    $"{ColonyStructure.ClassLabel(cls)} L{a.Level}", _micro);
                GUI.Label(new Rect(row.x + 80f, row.y, 44f, 16f), status, _body);
                GUI.Label(new Rect(row.x + 126f, row.y, row.width - 126f, 16f),
                    Truncate(a.FlavorLine, 14), _micro);
                y += 18f;
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
            string line = $"SCT {sct}  ENG {eng}  DEF {def}  MED {med}";
            return extra > 0 ? line + $"  +{extra}" : line;
        }

        private static string RosterStatus(SpecialistAgent a)
        {
            if (a.IsIncapacitated) return "DOWN";
            switch (a.CurrentAction)
            {
                case SpecialistAction.PursueFlag: return "WORK";
                case SpecialistAction.Rest: return "REST";
                case SpecialistAction.Hunt: return "HUNT";
                case SpecialistAction.Flee: return "FLEE";
                case SpecialistAction.Repair: return "FIX";
                default: return "IDLE";
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
                    Color col = piece.IsAirlock
                        ? Accent
                        : piece.Category == BuildingCategory.Commons
                            ? HudSkin.GoldBright
                            : new Color(0.90f, 0.91f, 0.93f);
                    Pip(mid, col, piece.IsAirlock ? 3f : 4.5f);
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
            HudSkin.ShadowLabel(new Rect(rect.x, rect.yMax - 13f, rect.width, 12f), "MAJESTY COLONY", _capsCenter, 0.9f);

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
            float h = 40f + rows * 26f;
            float mapTop = _sh - M - MapSize;
            var rect = new Rect(_sw - M - 286f, mapTop - 8f - h, 286f, h);
            var c = Panel(rect, "Construction");

            float y = c.y;
            for (int i = 0; i < _loop.Placer.Orders.Count && i < rows; i++)
            {
                var o = _loop.Placer.Orders[i];
                if (o == null) continue;
                float p = o.RequiredSeconds > 0f ? Mathf.Clamp01(o.ProgressSeconds / o.RequiredSeconds) : 1f;
                GUI.Label(new Rect(c.x, y, c.width - 46f, 14f), o.Data?.displayName ?? "Building", _action);
                GUI.Label(new Rect(c.xMax - 44f, y, 44f, 14f), $"{p * 100f:F0}%", _microRight);
                Meter(new Rect(c.x, y + 16f, c.width, 5f), p, Accent);
                y += 26f;
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
            float right = _sw - M - 300f - 10f;
            float w = Mathf.Min(460f, Mathf.Max(220f, right - left));
            float x = Mathf.Clamp((_sw - w) * 0.5f, left, Mathf.Max(left, right - w));
            float textH = Mathf.Clamp(_wrap.CalcHeight(new GUIContent(_toast), w - 48f), 16f, 64f);
            float top = _loop.Screen == DemoScreen.Playing && _loop.IsTutorialActive ? _tutorialBottom + 8f : M;
            var rect = new Rect(x, top + slide, w, textH + 20f);
            _hitRects.Add(rect);

            var prevColor = GUI.color;
            GUI.color = new Color(prevColor.r, prevColor.g, prevColor.b, prevColor.a * fade);
            HudSkin.Panel(rect, 1f, false);
            HudSkin.RuleH(new Rect(rect.x, rect.y, rect.width, 1f), Accent);
            var dot = new Vector2(rect.x + 18f, rect.y + 10f + Mathf.Min(textH, 16f) * 0.5f);
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

            int lines = cut.Body != null ? cut.Body.Length : 0;
            float h = 100f + lines * 28f;
            var rect = new Rect((_sw - 540f) * 0.5f, _sh * 0.22f, 540f, h);
            var c = Panel(rect, null, false);
            HudSkin.ModalChrome(rect, Gold);
            _hitRects.Add(rect);

            var prev = _banner.normal.textColor;
            _banner.normal.textColor = HudSkin.GoldBright;
            GUI.Label(new Rect(c.x, c.y, c.width, 28f), cut.Title, _banner);
            _banner.normal.textColor = prev;

            float y = c.y + 34f;
            if (cut.Body != null)
            {
                for (int i = 0; i < cut.Body.Length; i++)
                {
                    GUI.Label(new Rect(c.x, y, c.width, 26f), cut.Body[i], _body);
                    y += 28f;
                }
            }

            if (GUI.Button(new Rect(c.x, c.yMax - 30f, 180f, 30f), "CONTINUE  ·  SPACE", _chipOn))
                _loop.DismissCutscene();

            if (Event.current.type == EventType.KeyDown &&
                (Event.current.keyCode == KeyCode.Space || Event.current.keyCode == KeyCode.Return))
            {
                _loop.DismissCutscene();
                Event.current.Use();
            }
        }

        private void DrawWinBanner()
        {
            if (StillCaptureHold.Active) return;
            var mission = _loop.Mission;
            if (mission == null || !mission.IsWon || _winDismissed) return;

            HudSkin.Vignette(new Rect(0, 0, _sw, _sh), new Color(0.02f, 0.05f, 0.03f), 0.35f);

            bool travelCut = CampaignCutsceneCatalog.TryGetVictory(_loop.ActiveBody, out _);
            float detailH = travelCut ? 72f : 32f;
            var rect = new Rect((_sw - 500f) * 0.5f, _sh * 0.22f, 500f, travelCut ? 268f : 216f);
            var c = Panel(rect, null, false);
            HudSkin.ModalChrome(rect, Good);

            var prev = _banner.normal.textColor;
            _banner.normal.textColor = Good;
            GUI.Label(new Rect(c.x, c.y, c.width, 26f), mission.WinHeadline, _banner);
            _banner.normal.textColor = prev;

            GUI.Label(new Rect(c.x, c.y + 32f, c.width, detailH), mission.WinDetail, _wrap != null ? _wrap : _body);
            GUI.Label(new Rect(c.x, c.y + 32f + detailH, c.width, 24f), mission.WinSubline, _muted);
            var rating = _loop.CurrentRating;
            float ratingY = c.y + 56f + detailH;
            GUI.Label(new Rect(c.x, ratingY, c.width, 16f), rating.Summary, _value);
            GUI.Label(new Rect(c.x, ratingY + 18f, c.width, 14f), rating.Breakdown, _micro);

            if (GUI.Button(new Rect(c.x, c.yMax - 30f, 190f, 28f), "CONTINUE OVERSEEING  ·  Y", _chipOn))
            {
                _winDismissed = true;
                mission.DismissWinToSandbox();
            }

            if (!ReplayRules.IsEndless)
            {
                var next = CampaignProgress.NextAfter(_loop.ActiveBody);
                if (next.HasValue)
                {
                    string nextName = CelestialBodyCatalog.Get(next.Value).ShortCode;
                    if (GUI.Button(new Rect(c.x + 200f, c.yMax - 30f, 160f, 28f), $"TO {nextName}", _chipOff))
                    {
                        _winDismissed = true;
                        _loop.AdvanceCampaign();
                    }
                }
                else
                {
                    string rematch = _loop.BodyProfile != null ? $"NEW {_loop.BodyProfile.ShortCode}" : "NEW WORLD";
                    if (GUI.Button(new Rect(c.x + 200f, c.yMax - 30f, 160f, 28f), rematch, _chipOff))
                    {
                        _winDismissed = true;
                        _loop.BeginNextConquest();
                    }
                }
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
                DemoAudio.PlayFail();
            }

            HudSkin.Vignette(new Rect(0, 0, _sw, _sh), new Color(0.10f, 0.01f, 0.01f), 0.4f);

            var rect = new Rect((_sw - 460f) * 0.5f, _sh * 0.32f, 460f, 180f);
            var c = Panel(rect, null, false);
            HudSkin.ModalChrome(rect, Alarm);

            var prev = _banner.normal.textColor;
            _banner.normal.textColor = Alarm;
            GUI.Label(new Rect(c.x, c.y, c.width, 26f), mission != null ? mission.FailHeadline : "OUTPOST LOST", _banner);
            _banner.normal.textColor = prev;

            if (deadline || lost)
            {
                GUI.Label(new Rect(c.x, c.y + 32f, c.width, 32f),
                    mission != null ? mission.FailDetail : "The outpost is gone.", _body);
                if (ReplayRules.BlocksManualSavesAndReloads)
                {
                    GUI.Label(new Rect(c.x, c.y + 66f, c.width, 16f),
                        "Ironman — this body is lost. No restart.", _muted);
                    if (GUI.Button(new Rect(c.x, c.yMax - 30f, 140f, 28f), "TITLE", _chipOff))
                        _loop.ReturnToTitle();
                }
                else
                {
                    GUI.Label(new Rect(c.x, c.y + 66f, c.width, 16f),
                        ReplayRules.IsEndless ? "TRY AGAIN — this body is lost." : "Retry this body, or return to title.", _muted);
                    if (GUI.Button(new Rect(c.x, c.yMax - 30f, 160f, 28f), "RESTART MISSION", _chipOn))
                        _loop.RestartMission();
                    if (GUI.Button(new Rect(c.x + 176f, c.yMax - 30f, 140f, 28f), "TITLE", _chipOff))
                        _loop.ReturnToTitle();
                }
            }
            else
            {
                int met = _loop.FieldReviveMet;
                GUI.Label(new Rect(c.x, c.y + 32f, c.width, 36f),
                    $"FOBOT YARD  {met} EU  (120s)", _body);
                GUI.Label(new Rect(c.x, c.y + 70f, c.width, 16f),
                    !_loop.HasFobotYard
                        ? "Dock a Fobot Yard. Y does not skip the building."
                        : _loop.FieldReviveReadyIn > 0.5f
                            ? $"Cooldown {_loop.FieldReviveReadyIn:F0}s. Bill is by level, EU only."
                            : "Inspect the yard or press Y. ICE is not on the bill.", _muted);
                if (GUI.Button(new Rect(c.x, c.yMax - 30f, 220f, 28f), "FOBOT YARD  ·  Y", _chipOn))
                    _loop.PayFobotYard();
            }
        }

        private void Update()
        {
            if (_loop == null || !_loop.IsPlaying) return;
            bool cutsceneUp = _loop.TryPeekCutscene(out _);
            if ((_loop.NeedsFieldRevive || _loop.CanPayYard) &&
                !(_loop.Mission != null && _loop.Mission.IsLost) &&
                Input.GetKeyDown(KeyCode.Y))
                _loop.PayFobotYard();
            if (!_loop.NeedsFieldRevive &&
                !(_loop.Mission != null && _loop.Mission.IsLost))
                _failLatched = false;

            if (_loop.TryPeekCutscene(out _) &&
                (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)))
            {
                _loop.DismissCutscene();
            }

            if (_loop.Mission != null && _loop.Mission.IsWon && Input.GetKeyDown(KeyCode.Y) && !_loop.IsOutpostOverwhelmed)
            {
                _winDismissed = true;
                _loop.Mission.DismissWinToSandbox();
            }

            // Enter talks to the one selected hero (not while typing, a card is up, or a cut plays).
            if (ReferenceEquals(_chatAgent, null) && !InputBindings.TextEntryActive && _loop.SelectedStructure == null &&
                (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) &&
                !cutsceneUp && !_loop.TryPeekCutscene(out _))
            {
                var sel = _loop.SelectedAgents;
                if (sel != null && sel.Count == 1 && sel[0] != null) OpenChat(sel[0]);
            }

            if (_loop.FocusedCampus != _lastFocusToast)
            {
                _lastFocusToast = _loop.FocusedCampus;
                Toast($"Focus → {ColonyLayout.CampusLabel(_lastFocusToast)}", 2.2f);
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
            HudSkin.Band(new Rect(wx - 120f, wy - 20f, 820f, 130f), 0.35f);
            HudSkin.ShadowLabel(new Rect(wx, wy, 700f, 52f), word, _titleWord, 0.55f);
            float ww = _titleWord.CalcSize(new GUIContent(word)).x;
            HudSkin.Pill(new Rect(wx + 1f, wy + 56f, 28f, 3f), Accent);
            HudSkin.RuleH(new Rect(wx + 34f, wy + 57f, Mathf.Max(80f, ww - 34f), 1f), new Color(Gold.r, Gold.g, Gold.b, 0.9f));
            HudSkin.ShadowLabel(new Rect(wx + 1f, wy + 66f, 620f, 18f),
                DemoSettings.FirstHourDemo
                    ? "Click Earth to land. You are the Overseer — heroes choose their own work."
                    : "Click a world to land. You are the Overseer — heroes choose their own work.",
                _tagline, 0.7f);
            var prevCaps = _caps.normal.textColor;
            _caps.normal.textColor = Gold;
            HudSkin.ShadowLabel(new Rect(wx + 1f, wy + 86f, 400f, 14f), ReplayRules.HudTag, _caps, 0.7f);
            _caps.normal.textColor = prevCaps;

            DrawTitlePlanetLabels();

            if (_confirmNewGame)
            {
                DrawTitleConfirm();
                DrawToast();
                return;
            }

            const float bw = 240f;
            const float bh = 34f;
            float x = M + 14f;
            float y = _sh - M - 8f - bh;
            if (TitleButton(new Rect(x, y, bw, bh), "QUIT", false))
                _loop.QuitDemo();
            y -= bh + 6f;
            if (TitleButton(new Rect(x, y, bw, bh), "SETTINGS", false))
                _loop.OpenSettings();
            y -= bh + 6f;
            if (TitleButton(new Rect(x, y, bw, bh), "NEW CAMPAIGN", !DemoSettings.SaveExists))
            {
                var drop = CampaignProgress.NewGameBody;
                if (DemoSettings.SaveExists)
                {
                    _pendingNewGameBody = drop;
                    _confirmNewGame = true;
                }
                else
                    _loop.StartNewGame(drop);
            }
            if (DemoSettings.SaveExists)
            {
                y -= 18f;
                GUI.Label(new Rect(x + 2f, y, 420f, 14f), DemoSettings.ContinueDetail(), _micro);
                y -= bh + 4f;
                if (TitleButton(new Rect(x, y, bw + 80f, bh), DemoSettings.ContinueButtonLabel(), true))
                    _loop.ContinueGame();
            }

            DrawToast();
        }

        private bool TitleButton(Rect r, string label, bool primary)
        {
            _hitRects.Add(r);
            bool hot = r.Contains(Event.current.mousePosition);
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
            var style = primary ? _titleButtonOn : _titleButton;
            return GUI.Button(r, label, style);
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
                if (demoLocked) tag = "FULL CAMPAIGN";
                else if (!unlocked) tag = CampaignProgress.IsParked(body) ? "PARKED" : "LOCKED";
                else if (DemoSettings.SaveExists && body == _loop.ActiveBody) tag = "YOUR COLONY";
                else tag = "";

                if (hot)
                {
                    if (demoLocked) tag = "SETTINGS → FULL CAMPAIGN";
                    else if (!unlocked) tag = shift ? "CLICK TO UNLOCK" : "LOCKED  ·  SHIFT+CLICK";
                    else if (DemoSettings.SaveExists && body == _loop.ActiveBody) tag = "CLICK TO CONTINUE";
                    else tag = "CLICK TO LAND";
                }

                float y = p.y + r + 6f;
                var nameRect = new Rect(p.x - 90f, y, 180f, 16f);
                _planetName.normal.textColor = hot ? Color.white
                    : (unlocked && !demoLocked ? TextPrimary : TextMuted);
                HudSkin.ShadowLabel(nameRect, profile.DisplayName.ToUpperInvariant(), _planetName, 0.85f);
                if (!string.IsNullOrEmpty(tag))
                {
                    _planetTag.normal.textColor = hot ? Accent : TextMuted;
                    HudSkin.ShadowLabel(new Rect(p.x - 110f, y + 15f, 220f, 13f), tag, _planetTag, 0.85f);
                }
                if (hot)
                    HudSkin.RuleH(new Rect(p.x - 30f, y - 2f, 60f, 1.5f), Accent);
            }
        }

        private void DrawTitleConfirm()
        {
            var rect = new Rect(M + 8f, _sh - M - 8f - 150f, 360f, 150f);
            var c = Panel(rect, "New campaign");
            GUI.Label(new Rect(c.x, c.y, c.width, 44f), CampaignProgress.DropConfirmDetail(_pendingNewGameBody), _wrap);
            if (GUI.Button(new Rect(c.x, c.y + 50f, c.width, 30f),
                    CampaignProgress.DropConfirmLabel(_pendingNewGameBody), _chipOn))
            {
                _confirmNewGame = false;
                _loop.StartNewGame(_pendingNewGameBody);
            }
            if (GUI.Button(new Rect(c.x, c.y + 86f, c.width, 24f), "BACK  ·  Esc", _chipOff))
                _confirmNewGame = false;
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                _confirmNewGame = false;
                Event.current.Use();
            }
        }

        private void DrawPause()
        {
            HudSkin.Vignette(new Rect(0, 0, _sw, _sh), new Color(0.02f, 0.02f, 0.03f), 0.5f);
            var rect = new Rect((_sw - 420f) * 0.5f, _sh * 0.22f, 420f, 278f);
            var c = Panel(rect, "Paused");
            HudSkin.CornerTicks(rect, new Color(Gold.r, Gold.g, Gold.b, 0.75f));
            GUI.Label(new Rect(c.x, c.y, c.width, 28f),
                "Simulation frozen. Autosave keeps this body's campus, stockpile, and research.",
                _wrap);
            if (GUI.Button(new Rect(c.x, c.y + 36f, c.width, 32f), "RESUME  ·  Esc", _chipOn))
                _loop.ResumePlay();
            if (GUI.Button(new Rect(c.x, c.y + 76f, c.width, 32f), "SETTINGS", _chipOff))
                _loop.OpenSettings();
            if (GUI.Button(new Rect(c.x, c.y + 116f, c.width, 32f), "TITLE", _chipOff))
                _loop.ReturnToTitle();
            if (ReplayRules.BlocksManualSavesAndReloads)
            {
                GUI.Label(new Rect(c.x, c.y + 162f, c.width, 20f),
                    "IRONMAN — no abandon, no new seed.", _muted);
            }
            else if (GUI.Button(new Rect(c.x, c.y + 156f, c.width, 32f), "ABANDON BODY  ·  new seed", _chipOff))
            {
                DemoSettings.RequestBootIntoPlay();
                _loop.RestartMission();
            }
            if (GUI.Button(new Rect(c.x, c.y + 196f, c.width, 32f), "QUIT", _chipOff))
                _loop.QuitDemo();
        }

        private void DrawSettings()
        {
            HudSkin.Vignette(new Rect(0, 0, _sw, _sh), new Color(0.02f, 0.02f, 0.03f), 0.55f);
            float h = Mathf.Min(764f, Mathf.Max(460f, _sh - 24f));
            var rect = new Rect((_sw - 440f) * 0.5f, Mathf.Max(10f, (_sh - h) * 0.5f), 440f, h);
            var panel = Panel(rect, "Settings");
            HudSkin.CornerTicks(rect, new Color(Gold.r, Gold.g, Gold.b, 0.75f));

            // Everything above BACK scrolls when the window is shorter than the list.
            var view = new Rect(panel.x, panel.y, panel.width, panel.height - 44f);
            bool scrolls = _settingsContentH > view.height;
            var c = new Rect(0f, 0f, view.width - (scrolls ? 12f : 0f), _settingsContentH);
            _settingsScroll = GUI.BeginScrollView(view, _settingsScroll, c);
            float y = c.y;

            y = SettingsSlider(c.x, y, c.width, "MASTER", ref DemoSettings.Master);
            y = SettingsSlider(c.x, y, c.width, "SFX", ref DemoSettings.Sfx);
            y = SettingsSlider(c.x, y, c.width, "AMBIENCE", ref DemoSettings.Ambient);
            y = SettingsSlider(c.x, y, c.width, "HUD SCALE", ref DemoSettings.HudScale, 0.85f, 1.25f, applyAudio: false);
            y += 6f;

            if (Chip(new Rect(c.x, y, 200f, 26f), "INVERT CAMERA PAN", DemoSettings.InvertPan))
                DemoSettings.InvertPan = !DemoSettings.InvertPan;
            // Perspective camera: zoom out to see the horizon and the sky.
            if (Chip(new Rect(c.x + 208f, y, c.width - 208f, 26f), "DIORAMA CAMERA", DemoSettings.DioramaCamera))
            {
                DemoSettings.DioramaCamera = !DemoSettings.DioramaCamera;
                DemoSettings.SaveSettings();
            }
            y += 32f;

            if (Chip(new Rect(c.x, y, 200f, 26f),
                    DemoSettings.Fullscreen ? "FULLSCREEN" : "WINDOWED", DemoSettings.Fullscreen))
            {
                DemoSettings.Fullscreen = !DemoSettings.Fullscreen;
                DemoSettings.ApplyDisplay();
            }
            if (Chip(new Rect(c.x + 208f, y, c.width - 208f, 26f), "DAY / NIGHT CYCLE", DemoSettings.DayCycle))
            {
                DemoSettings.DayCycle = !DemoSettings.DayCycle;
                DemoSettings.SaveSettings();
            }
            y += 32f;

            var qualityNames = QualitySettings.names;
            string qLabel = "QUALITY";
            if (qualityNames != null && qualityNames.Length > 0)
            {
                DemoSettings.QualityIndex = Mathf.Clamp(DemoSettings.QualityIndex, 0, qualityNames.Length - 1);
                qLabel = $"QUALITY  ·  {qualityNames[DemoSettings.QualityIndex].ToUpperInvariant()}";
            }
            if (Chip(new Rect(c.x, y, c.width, 26f), qLabel, false) && qualityNames != null && qualityNames.Length > 0)
            {
                DemoSettings.QualityIndex = (DemoSettings.QualityIndex + 1) % qualityNames.Length;
                DemoSettings.ApplyDisplay();
            }
            y += 32f;

            int[] caps = { 0, 30, 60, 120, 144 };
            int capIndex = System.Array.IndexOf(caps, DemoSettings.FrameCap);
            if (capIndex < 0) capIndex = 0;
            string capLabel = DemoSettings.FrameCap <= 0 ? "UNCAPPED" : $"{DemoSettings.FrameCap} FPS";
            if (Chip(new Rect(c.x, y, c.width, 26f), $"FRAME CAP  ·  {capLabel}", DemoSettings.FrameCap > 0))
            {
                DemoSettings.FrameCap = caps[(capIndex + 1) % caps.Length];
                DemoSettings.ApplyDisplay();
            }
            y += 32f;

            float visHalf = (c.width - 8f) * 0.5f;
            if (Chip(new Rect(c.x, y, visHalf, 26f), "TILT-SHIFT (DIORAMA)", DemoSettings.TiltShift))
            {
                DemoSettings.TiltShift = !DemoSettings.TiltShift;
                DemoSettings.SaveSettings();
            }
            if (Chip(new Rect(c.x + visHalf + 8f, y, visHalf, 26f), "CLOUD SHADOWS", DemoSettings.CloudShadows))
            {
                DemoSettings.CloudShadows = !DemoSettings.CloudShadows;
                DemoSettings.SaveSettings();
                CloudShadows.Refresh();
            }
            y += 32f;

            // Lines come from a local LLM; SPOKEN reads them aloud through a local TTS server.
            if (Chip(new Rect(c.x, y, visHalf, 26f), "HERO LINES · LOCAL LLM", DemoSettings.HeroVoices))
            {
                DemoSettings.HeroVoices = !DemoSettings.HeroVoices;
                DemoSettings.SaveSettings();
            }
            if (Chip(new Rect(c.x + visHalf + 8f, y, visHalf, 26f), "SPOKEN · LOCAL TTS", DemoSettings.HeroSpeech))
            {
                DemoSettings.HeroSpeech = !DemoSettings.HeroSpeech;
                DemoSettings.SaveSettings();
            }
            y += 36f;

            Fill(new Rect(c.x, y, c.width, 1f), Hairline);
            y += 8f;
            GUI.Label(new Rect(c.x, y, c.width, 14f), "ACCESSIBILITY", _section);
            y += 18f;

            float halfW = (c.width - 8f) * 0.5f;
            if (Chip(new Rect(c.x, y, halfW, 26f), "EDGE SCROLL", DemoSettings.EdgeScroll))
                DemoSettings.EdgeScroll = !DemoSettings.EdgeScroll;
            if (Chip(new Rect(c.x + halfW + 8f, y, halfW, 26f), "REDUCE MOTION", DemoSettings.ReduceMotion))
                DemoSettings.ReduceMotion = !DemoSettings.ReduceMotion;
            y += 32f;

            var cbMode = Accessibility.Mode;
            if (Chip(new Rect(c.x, y, c.width, 26f),
                    $"COLOUR VISION  ·  {Accessibility.ModeLabel(cbMode).ToUpperInvariant()}",
                    cbMode != ColorBlindMode.Off))
            {
                Accessibility.Mode = (ColorBlindMode)(((int)cbMode + 1) % 4);
            }
            y += 32f;

            y = WrapBlock(c, y, "Severity is also shown by icon and prefix, so colour is never the only cue.") + 6f;

            string tutLabel = _loop.IsTutorialActive ? "TUTORIAL  ·  ON" : "REPLAY TUTORIAL";
            if (Chip(new Rect(c.x, y, 220f, 26f), tutLabel, _loop.IsTutorialActive))
                _loop.RestartTutorial();
            y += 32f;

            bool marsLessons = DemoSettings.MarsGrokLessons;
            if (Chip(new Rect(c.x, y, c.width, 26f),
                    marsLessons ? "MARS GROK LESSONS  ·  ON" : "MARS GROK LESSONS  ·  OFF",
                    marsLessons))
            {
                DemoSettings.MarsGrokLessons = !DemoSettings.MarsGrokLessons;
                DemoSettings.SaveSettings();
            }
            y += 22f;
            GUI.Label(new Rect(c.x, y, c.width, 16f),
                "Luna: Grok lectures. Mars: failure asides unless lessons are on.", _micro);
            y += 28f;

            Fill(new Rect(c.x, y, c.width, 1f), Hairline);
            y += 8f;
            if (DemoSettings.FirstHourDemo)
            {
                GUI.Label(new Rect(c.x, y, c.width, 14f), "DEMO", _section);
                y += 18f;
                if (Chip(new Rect(c.x, y, c.width, 26f), "FULL CAMPAIGN", false))
                    _loop.SetFirstHourDemo(false);
                y += 30f;
                y = WrapBlock(c, y,
                    "Earth only. Learn how bounties influence the Engineer. Guilds, Belt, and Europa stay hidden until you turn the full campaign on.");
            }
            else
            {
                GUI.Label(new Rect(c.x, y, c.width, 14f), "REPLAY  ·  MODE / CHALLENGE / STANCE / IRONMAN", _section);
                y += 18f;

                float half = (c.width - 8f) * 0.5f;
                if (Chip(new Rect(c.x, y, half, 26f),
                        $"MODE  ·  {ReplayRules.ModeLabel}", ReplayRules.IsEndless))
                    ReplayRules.CycleMode();
                if (Chip(new Rect(c.x + half + 8f, y, half, 26f),
                        $"CHAL  ·  {ReplayRules.ChallengeLabel}", ReplayRules.Challenge != ChallengeId.None))
                    ReplayRules.CycleChallenge();
                y += 32f;

                if (Chip(new Rect(c.x, y, half, 26f),
                        $"STANCE  ·  {ReplayRules.StanceLabel}", ReplayRules.Stance != DoctrineStance.Balanced))
                    ReplayRules.CycleStance();
                bool ironmanOn = _loop.RunConfigLocked ? ReplayRules.IronmanRun : ReplayRules.Ironman;
                if (Chip(new Rect(c.x + half + 8f, y, half, 26f),
                        $"IRON  ·  {(ironmanOn ? "ON" : "OFF")}", ironmanOn))
                {
                    if (_loop.RunConfigLocked)
                        Notify("Ironman latches at New Game. This run stays as started.", 3.5f);
                    else
                        ReplayRules.CycleIronman();
                }
                y += 30f;

                y = WrapBlock(c, y, ReplayRules.StanceHint + " " + ReplayRules.ChallengeHint);
                y = WrapBlock(c, y, ReplayRules.IronmanHint);
                y = WrapBlock(c, y,
                    "Stockpile and fauna apply on New Game / reload. Doctrine hunger, courage, range, and workshop pull apply live. Tight Purse ship rules apply when you leave Settings. Ironman latches at New Game or Continue.");
                y += 4f;
                if (Chip(new Rect(c.x, y, c.width, 26f), "EARTH DEMO", false))
                    _loop.SetFirstHourDemo(true);
                y += 30f;
            }

            _settingsContentH = y + 4f;
            GUI.EndScrollView();

            if (GUI.Button(new Rect(panel.x, panel.yMax - 34f, panel.width, 32f), "BACK  ·  Esc", _chipOn))
                _loop.CloseSettings();
        }

        /// <summary>Wrapped paragraph at its measured height; returns the y below it.</summary>
        private float WrapBlock(Rect c, float y, string text)
        {
            if (string.IsNullOrEmpty(text)) return y;
            float h = _wrap.CalcHeight(new GUIContent(text), c.width);
            GUI.Label(new Rect(c.x, y, c.width, h), text, _wrap);
            return y + h + 6f;
        }

        private float SettingsSlider(
            float x, float y, float width, string label, ref float value,
            float min = 0f, float max = 1f, bool applyAudio = true)
        {
            GUI.Label(new Rect(x, y, 90f, 18f), label, _caps);
            GUI.Label(new Rect(x + width - 40f, y, 40f, 18f),
                max > 1f ? $"{value:0.00}×" : $"{value * 100f:F0}%", _microRight);
            float next = GUI.HorizontalSlider(new Rect(x + 90f, y + 2f, width - 140f, 16f), value, min, max);
            if (!Mathf.Approximately(next, value))
            {
                value = next;
                if (applyAudio)
                    DemoAudio.ApplyVolumes();
            }
            return y + 28f;
        }

        private void DrawTutorial()
        {
            if (!_loop.IsTutorialActive) return;

            string line;
            if (DemoSettings.FirstHourDemo)
                line = FirstHourTutorial.Beat(_loop.TutorialStep, _loop.FirstHourBuildAlreadyTempting);
            else
            {
                string[] beats =
                {
                    "1/6  COMMONS — Colony Commons is already on the claim. It holds the treasury.",
                    "2/6  Tax collectors walk out of the Commons and carry each building's till home.",
                    "3/6  Keep the yard clear of mobs — villagers raise houses and solar farms that pay energy.",
                    "4/6  Workshop — build Scout / Engineer / Defense. A robot fabricates when it finishes.",
                    "5/6  G, Build, leave it at 700. Watch the Engineer.",
                    "6/6  They named a price. Select the flag and press + until the chip reads tempted."
                };
                int step = Mathf.Clamp(_loop.TutorialStep, 0, beats.Length - 1);
                if (_loop.TutorialWantsPriceLesson)
                    step = 5;
                line = beats[step];
            }
            // Top centre, like Majesty 2's advisor line — build/flag catalogs own the space above
            // the console, and the objectives panel owns the top-right corner.
            float left = M;
            float right = _sw - M - 300f - 12f;
            float w = Mathf.Min(720f, right - left);
            float barH = DemoSettings.FirstHourDemo ? 60f : 52f;
            float x = Mathf.Clamp((_sw - w) * 0.5f, left, Mathf.Max(left, right - w));
            var rect = new Rect(x, M, w, barH);
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
            GUI.Label(new Rect(c.x + 42f, c.y, c.width - 128f, barH - 16f), line, _wrap);
            _wrap.normal.textColor = prevText;
            if (GUI.Button(new Rect(c.xMax - 72f, rect.center.y - 13f, 72f, 26f), "SKIP", _chipOff))
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
                BuildingCategory.Utility,
                BuildingCategory.EngineerWorkshop
            };

            var rect = new Rect(M, M, TopW, 118f);
            var c = Panel(rect, "Drop manifest");
            float y = c.y;
            GUI.Label(new Rect(c.x, y, c.width, 13f), "COMMONS → airlock sockets → HAB / workshops. Lego campus only.", _micro);
            y += 16f;
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
                GUI.Label(new Rect(c.x, y, c.width, 14f),
                    $"{found.displayName}  ·  {FormatBuildCost(CostOf(found))}", _micro);
                y += 15f;
            }
        }
    }
}
