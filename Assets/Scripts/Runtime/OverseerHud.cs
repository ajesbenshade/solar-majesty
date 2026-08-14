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
        // Dark carbon / gold-orange chrome (Phase 4 visual target). Orange stays the interactive accent.
        private static readonly Color PanelBg = new Color(0.032f, 0.033f, 0.038f, 0.94f);
        private static readonly Color PanelSoft = new Color(0.10f, 0.105f, 0.11f, 0.94f);
        private static readonly Color PanelHover = new Color(0.17f, 0.175f, 0.18f, 0.95f);
        private static readonly Color Hairline = new Color(0.82f, 0.62f, 0.22f, 0.32f);
        private static readonly Color Gold = new Color(0.82f, 0.62f, 0.22f);
        private static readonly Color Accent = new Color(0.96f, 0.42f, 0.08f);
        private static readonly Color Ink = new Color(0.06f, 0.06f, 0.07f);
        private static readonly Color TextPrimary = new Color(0.92f, 0.93f, 0.94f);
        private static readonly Color TextMuted = new Color(0.58f, 0.61f, 0.65f);
        private static readonly Color Track = new Color(1f, 1f, 1f, 0.10f);
        private static readonly Color HpFill = new Color(0.86f, 0.28f, 0.22f);
        private static readonly Color FatigueFill = new Color(0.70f, 0.74f, 0.79f);
        private static readonly Color Alarm = new Color(0.95f, 0.32f, 0.26f);
        private static readonly Color Good = new Color(0.62f, 0.86f, 0.66f);

        private const float M = 14f;      // screen margin
        private const float Pad = 10f;    // panel padding
        private const float TopW = 300f;  // top-left command width
        private const float DockH = 56f;
        private const float DockW = 560f;
        private const float TopStripH = 50f;
        private const float MapSize = 148f;

        private GameLoop _loop;
        private bool _failLatched;
        private bool _winDismissed;
        private bool _deadlineDismissed;
        private bool _techOpen;
        private Vector2 _techScroll;
        private Vector2 _buildScroll;
        private string _toast;
        private float _toastUntil;
        private int _lastFocusToast = -1;
        private float _contentBottom; // top of dock/popup stack — cards sit above this
        private float _sw;
        private float _sh;
        private float _playTop;
        private float _hudScale = 1f;
        private bool _powerAlarmLatched;
        private bool _confirmNewGame;
        private Texture2D _minimapDisc;


        private bool _stylesReady;
        private GUIStyle _brand;
        private GUIStyle _section;
        private GUIStyle _body;
        private GUIStyle _muted;
        private GUIStyle _micro;
        private GUIStyle _microRight;
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

        /// <summary>True when the cursor is over a HUD panel (blocks world select).</summary>
        public bool PointerBlocksWorld { get; private set; }

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

        public void OnSessionPlaying()
        {
            _confirmNewGame = false;
            if (_loop == null || !_loop.StartsEmpty) return;
            var body = _loop.BodyProfile;
            string briefing = body != null && !string.IsNullOrEmpty(body.Briefing)
                ? body.Briefing
                : "Raise the Palace keep first, then dock modules via airlocks.";
            Toast(briefing, 6.5f);
        }

        public void Notify(string message, float seconds) => Toast(message, seconds);

        private void OnResupply()
        {
            string line = _loop?.Economy != null && !string.IsNullOrEmpty(_loop.Economy.LastResupplyLine)
                ? _loop.Economy.LastResupplyLine
                : "Earth resupply docked at the Landing Pad — stockpile topped up.";
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
            if (_loop?.Economy == null) return;
            bool shortOnPower = _loop.Economy.PowerShort;
            if (shortOnPower && !_powerAlarmLatched)
            {
                _powerAlarmLatched = true;
                Toast("Power short — dock a Power Node. Grid draw exceeds generation.", 4f);
            }
            else if (!shortOnPower)
            {
                _powerAlarmLatched = false;
            }
        }

        private void Toast(string message, float seconds)
        {
            _toast = message;
            _toastUntil = Time.unscaledTime + seconds;
        }

        // ---- drawing primitives -------------------------------------------------

        private static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        private static void Fill(Rect r, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        private static void Outline(Rect r, Color c)
        {
            Fill(new Rect(r.x, r.y, r.width, 1f), c);
            Fill(new Rect(r.x, r.yMax - 1f, r.width, 1f), c);
            Fill(new Rect(r.x, r.y, 1f, r.height), c);
            Fill(new Rect(r.xMax - 1f, r.y, 1f, r.height), c);
        }

        /// <summary>Panel chrome (bg, hairline, accent tab, optional header). Returns content rect.</summary>
        private Rect Panel(Rect r, string title, bool accentTab = true)
        {
            _hitRects.Add(r);
            Fill(r, PanelBg);
            Outline(r, Hairline);
            Outline(new Rect(r.x + 1f, r.y + 1f, r.width - 2f, r.height - 2f), new Color(Gold.r, Gold.g, Gold.b, 0.12f));
            if (accentTab) Fill(new Rect(r.x, r.y, 3f, 22f), Accent);

            float y = r.y + Pad;
            if (!string.IsNullOrEmpty(title))
            {
                GUI.Label(new Rect(r.x + Pad, y, r.width - Pad * 2f, 13f), title.ToUpperInvariant(), _section);
                y += 17f;
                Fill(new Rect(r.x + Pad, y, r.width - Pad * 2f, 1f), Hairline);
                y += 7f;
            }
            return new Rect(r.x + Pad, y, r.width - Pad * 2f, r.yMax - y - Pad);
        }

        private void Meter(Rect r, float t01, Color fill)
        {
            Fill(r, Track);
            Fill(new Rect(r.x, r.y, r.width * Mathf.Clamp01(t01), r.height), fill);
        }

        private void Bar(Rect r, string label, float t01, Color fill)
        {
            const float labelW = 46f;
            const float valueW = 34f;
            GUI.Label(new Rect(r.x, r.y, labelW, r.height), label, _micro);
            var track = new Rect(r.x + labelW, r.y + 3f, Mathf.Max(10f, r.width - labelW - valueW), r.height - 6f);
            Meter(track, t01, fill);
            GUI.Label(new Rect(r.xMax - valueW, r.y, valueW, r.height), $"{Mathf.Clamp01(t01) * 100f:F0}%", _microRight);
        }

        private void CheckBox(Rect r, bool done)
        {
            if (done)
            {
                Fill(r, Accent);
                Fill(new Rect(r.x + 3f, r.y + 5f, 5f, 2f), Ink);
                Fill(new Rect(r.x + 5f, r.y + 3f, 2f, 5f), Ink);
            }
            else
            {
                Outline(r, new Color(1f, 1f, 1f, 0.25f));
            }
        }

        private void EnsureStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;

            _brand = Label(17, FontStyle.Bold, TextPrimary, TextAnchor.MiddleLeft);
            _section = Label(11, FontStyle.Bold, Accent, TextAnchor.MiddleLeft);
            _body = Label(12, FontStyle.Normal, TextPrimary, TextAnchor.MiddleLeft);
            _muted = Label(11, FontStyle.Normal, TextMuted, TextAnchor.MiddleLeft);
            _micro = Label(10, FontStyle.Normal, TextMuted, TextAnchor.MiddleLeft);
            _microRight = Label(10, FontStyle.Normal, TextMuted, TextAnchor.MiddleRight);
            _value = Label(14, FontStyle.Bold, TextPrimary, TextAnchor.MiddleLeft);
            _pill = Label(10, FontStyle.Bold, Ink, TextAnchor.MiddleCenter);
            _banner = Label(20, FontStyle.Bold, TextPrimary, TextAnchor.MiddleLeft);
            _action = Label(12, FontStyle.Normal, TextPrimary, TextAnchor.MiddleLeft);
            _wrap = Label(11, FontStyle.Normal, TextMuted, TextAnchor.UpperLeft);
            _wrap.wordWrap = true;
            _onText = Label(11, FontStyle.Bold, Ink, TextAnchor.MiddleLeft);

            _chipOff = Button(PanelSoft, PanelHover, TextPrimary, 11, FontStyle.Normal, TextAnchor.MiddleCenter, 4);
            _chipOn = Button(Accent, Accent, Ink, 11, FontStyle.Bold, TextAnchor.MiddleCenter, 4);
            _rowOff = Button(PanelSoft, PanelHover, TextPrimary, 11, FontStyle.Normal, TextAnchor.MiddleLeft, 8);
            _rowOn = Button(Accent, Accent, Ink, 11, FontStyle.Bold, TextAnchor.MiddleLeft, 8);
        }

        private static GUIStyle Label(int size, FontStyle fs, Color color, TextAnchor anchor) =>
            new GUIStyle
            {
                fontSize = size,
                fontStyle = fs,
                alignment = anchor,
                wordWrap = false,
                clipping = TextClipping.Clip,
                normal = { textColor = color }
            };

        private static GUIStyle Button(
            Color bg, Color hover, Color text, int size, FontStyle fs, TextAnchor anchor, int padLeft) =>
            new GUIStyle
            {
                fontSize = size,
                fontStyle = fs,
                alignment = anchor,
                wordWrap = false,
                clipping = TextClipping.Clip,
                padding = new RectOffset(padLeft, 8, 0, 0),
                normal = { background = Solid(bg), textColor = text },
                hover = { background = Solid(hover), textColor = Color.white },
                active = { background = Solid(hover), textColor = text },
                focused = { background = Solid(bg), textColor = text }
            };

        // ---- frame --------------------------------------------------------------

        private void OnGUI()
        {
            if (_loop == null) return;
            EnsureStyles();
            _hitRects.Clear();

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
                _playTop = DrawTopStrip();
                DrawCommandPanel(_playTop);
                DrawMissionPanel();
                DrawTechPanel();
                DrawDropManifest();
                DrawMinimap();

                float dockTop = _sh - M - DockH;
                _contentBottom = dockTop;
                DrawToolPopups(dockTop);
                DrawBottomDock(dockTop);

                DrawConstructionPanel();
                DrawInspectPanel();
                DrawRoster();
                DrawTutorial();
                DrawToast();
                DrawWinBanner();
                DrawFailBanner();
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
        }

        /// <summary>Brand + stockpile + current objective.</summary>
        private static float CommandPanelHeight()
        {
            int n = CelestialBodyCatalog.All != null ? CelestialBodyCatalog.All.Length : 3;
            int rows = Mathf.Max(1, (n + 2) / 3);
            return 268f + Mathf.Max(0, rows - 1) * 24f;
        }

        private float DrawTopStrip()
        {
            var rect = new Rect(M, M, _sw - M * 2f, TopStripH);
            var c = Panel(rect, null, false);
            Fill(new Rect(rect.x, rect.y, rect.width, 2f), Gold);

            string body = _loop.BodyProfile != null ? _loop.BodyProfile.DisplayName : "Colony";
            GUI.Label(new Rect(c.x, c.y, 280f, 18f), "SOLAR MAJESTY", _brand);
            GUI.Label(new Rect(c.x, c.y + 16f, 280f, 14f),
                $"{body} campaign  ·  {ReplayRules.HudTag}", _micro);

            if (_loop.Resources != null)
            {
                float chipW = 78f;
                float chipsW = chipW * 5f + 12f;
                float x0 = c.x + (c.width - chipsW) * 0.5f;
                int metals = _loop.Resources.Get(ResourceId.Metals);
                int ice = _loop.Resources.Get(ResourceId.WaterIce);
                int reg = _loop.Resources.Get(ResourceId.Regolith);
                int pwr = _loop.Resources.Get(ResourceId.Power);
                int escrow = _loop.Economy != null ? _loop.Economy.EscrowedMetals : 0;
                bool pwrAlarm = pwr < 8 || (_loop.Economy != null && _loop.Economy.PowerDraw > _loop.Economy.PowerGen);
                var set = _loop.Settlement;
                int pop = set != null ? set.Population : 0;
                ResourceChip(new Rect(x0, c.y, chipW, 32f), "REG", reg, reg < 10);
                ResourceChip(new Rect(x0 + chipW + 3f, c.y, chipW, 32f), "ICE", ice, ice < 8);
                ResourceChip(new Rect(x0 + (chipW + 3f) * 2f, c.y, chipW, 32f),
                    escrow > 0 ? $"MET −{escrow}" : "MET", metals, metals < 12);
                ResourceChip(new Rect(x0 + (chipW + 3f) * 3f, c.y, chipW, 32f), "PWR", pwr, pwrAlarm);
                ResourceChip(new Rect(x0 + (chipW + 3f) * 4f, c.y, chipW, 32f),
                    "BEDS", pop, set != null && set.HousingTight);
            }

            int posted = 0;
            var flags = _loop.Flags != null ? _loop.Flags.Flags : null;
            if (flags != null) posted = flags.Count;
            GUI.Label(new Rect(c.xMax - 120f, c.y + 4f, 120f, 28f),
                posted <= 0 ? "Bounties  ·  none" : $"Bounties  ·  {posted}", _microRight);

            return rect.yMax + 6f;
        }

        private void DrawCommandPanel(float top)
        {
            var rect = new Rect(M, top, TopW, CommandPanelHeight());
            var c = Panel(rect, null);

            var mission = _loop.Mission;
            string status = "SANDBOX";
            Color pill = PanelSoft;
            Color pillText = TextPrimary;
            if (mission != null)
            {
                switch (mission.State)
                {
                    case MissionState.Won:
                        status = ReplayRules.IsEndless ? "HOLDING" : "SECURED";
                        pill = Good;
                        pillText = Ink;
                        break;
                    case MissionState.Lost:
                        status = "FAILED";
                        pill = Alarm;
                        pillText = Ink;
                        break;
                    default:
                        status = "ACTIVE";
                        pill = Accent;
                        pillText = Ink;
                        break;
                }
            }

            GUI.Label(new Rect(c.x, c.y, c.width - 70f, 16f), "OVERSEER", _section);
            var pillRect = new Rect(c.xMax - 62f, c.y + 1f, 62f, 16f);
            Fill(pillRect, pill);
            var prevPill = _pill.normal.textColor;
            _pill.normal.textColor = pillText;
            GUI.Label(pillRect, status, _pill);
            _pill.normal.textColor = prevPill;

            float y = c.y + 20f;
            string objective = mission != null ? mission.ObjectiveLabel : "Free sandbox — post bounties.";
            GUI.Label(new Rect(c.x, y, c.width, 14f), objective, _muted);
            y += 14f;

            string code = _loop.BodyProfile != null ? _loop.BodyProfile.ShortCode : "LUNA";
            string seedLine = $"{code} seed {_loop.MoonSeedValue}";
            if (_loop.BodyProfile != null)
            {
                if (_loop.BodyProfile.RadiationDrainPerSecond > 0f)
                    seedLine += "  ·  RAD";
                if (_loop.BodyProfile.MoveSpeedScale > 1.08f)
                    seedLine += "  ·  LOW-G";
                else if (_loop.BodyProfile.MoveSpeedScale < 0.94f)
                    seedLine += "  ·  HEAVY";
            }
            if (_loop.World != null)
            {
                seedLine +=
                    $"  ·  nodes {_loop.World.Nodes.Count}" +
                    $"  ·  lairs {_loop.World.UnclearedLairCount}/{_loop.World.Lairs.Count}";
            }
            GUI.Label(new Rect(c.x, y, c.width, 13f), seedLine, _micro);
            y += 14f;

            var rating = _loop.CurrentRating;
            GUI.Label(new Rect(c.x, y, c.width, 13f), rating.Summary, _micro);
            y += 13f;
            GUI.Label(new Rect(c.x, y, c.width, 13f), rating.Breakdown, _micro);
            y += 14f;

            var bodies = CelestialBodyCatalog.All;
            const int cols = 3;
            float chipW = (c.width - 3f * (cols - 1)) / cols;
            for (int i = 0; i < bodies.Length; i++)
            {
                int row = i / cols;
                int col = i % cols;
                var id = bodies[i];
                var profile = CelestialBodyCatalog.Get(id);
                bool unlocked = CampaignProgress.IsUnlocked(id);
                var chipRect = new Rect(c.x + col * (chipW + 3f), y + row * 22f, chipW, 20f);
                string label = unlocked ? profile.ShortCode : $"{profile.ShortCode}?";
                if (Chip(chipRect, label, _loop.ActiveBody == id) && unlocked)
                    _loop.SelectBody(id);
            }
            int rows = (bodies.Length + cols - 1) / cols;
            y += 22f * rows;
            Fill(new Rect(c.x, y, c.width, 1f), Hairline);
            y += 6f;

            var set = _loop.Settlement;
            if (set != null)
            {
                var prevPop = _micro.normal.textColor;
                _micro.normal.textColor = set.HousingTight ? Alarm : TextMuted;
                string popExtra = set.HasOutpost ? "  ·  OUTPOST" : "";
                if (set.HasGuild) popExtra += "  ·  GUILD";
                if (set.ProductionScale < 0.99f)
                    popExtra += $"  ·  PROD {Mathf.RoundToInt(set.ProductionScale * 100f)}%";
                GUI.Label(
                    new Rect(c.x, y, c.width, 14f),
                    $"POP {set.Population}/{set.PopulationGoal}  ·  BEDS {set.Population}/{set.Housing}  ·  TAX +{set.LastTax} MET{popExtra}",
                    _micro);
                _micro.normal.textColor = prevPop;
                y += 15f;
            }

            var eco = _loop.Economy;
            if (eco != null)
            {
                int gen = eco.PowerGen;
                int draw = eco.PowerDraw;
                var prevPwr = _micro.normal.textColor;
                bool shipHeld = !eco.HasDock;
                _micro.normal.textColor = draw > gen || shipHeld ? Alarm : TextMuted;
                string shipBit = shipHeld
                    ? $"ship {_loop.FormatHold(eco.ResupplySecondsLeft)} (no pad)"
                    : $"ship {_loop.FormatHold(eco.ResupplySecondsLeft)}";
                GUI.Label(
                    new Rect(c.x, y, c.width, 14f),
                    $"PWR {gen} gen / {draw} draw  ·  upkeep {_loop.FormatHold(eco.UpkeepSecondsLeft)}  ·  {shipBit}",
                    _micro);
                _micro.normal.textColor = prevPwr;
                y += 14f;

                string feed = !string.IsNullOrEmpty(eco.LastExtractLine)
                    ? eco.LastExtractLine
                    : (set != null && !string.IsNullOrEmpty(set.LastProductionLine)
                        ? set.LastProductionLine
                        : eco.LastUpkeepLine);
                if (!string.IsNullOrEmpty(feed))
                {
                    GUI.Label(new Rect(c.x, y, c.width, 13f), feed, _micro);
                    y += 14f;
                }
            }

            DrawLogLines(new Rect(c.x, y, c.width, 40f));
            y += 42f;

            int partyCount = _loop.Parties != null ? _loop.Parties.Count : 0;
            if (Chip(new Rect(c.x, y, 108f, 22f), "PARTY · P", partyCount > 0))
                _loop.FormParty();
            GUI.Label(new Rect(c.x + 116f, y + 4f, c.width - 116f, 16f), "select 2+ or inn", _micro);
        }

        private void DrawLogLines(Rect r)
        {
            var log = _loop.Log;
            if (log == null || log.Entries.Count == 0)
            {
                GUI.Label(r, "Overseer log — drop, dens, sustain, launch.", _micro);
                return;
            }

            int n = Mathf.Min(3, log.Entries.Count);
            float row = r.height / 3f;
            for (int i = 0; i < n; i++)
            {
                var e = log.Entries[log.Entries.Count - n + i];
                GUI.Label(new Rect(r.x, r.y + i * row, r.width, row), e.Line, _micro);
            }
        }

        private void ResourceChip(Rect r, string label, int amount, bool alarm = false)
        {
            Color fill = PanelSoft;
            if (alarm)
            {
                float pulse = 0.4f + 0.35f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7f));
                fill = Color.Lerp(PanelSoft, Alarm, pulse);
            }
            Fill(r, fill);
            GUI.Label(new Rect(r.x + 6f, r.y + 1f, r.width - 8f, 11f), label, _micro);
            GUI.Label(new Rect(r.x + 6f, r.y + 12f, r.width - 8f, 15f), amount.ToString(), _value);
        }

        private float DockLeft() => (_sw - DockW) * 0.5f;

        private void DrawBottomDock(float top)
        {
            var rect = new Rect(DockLeft(), top, DockW, DockH);
            var c = Panel(rect, null);

            // Mockup action squares → Overseer verbs only (never unit orders).
            float sq = 44f;
            float x = c.x;
            if (SquareAction(new Rect(x, c.y + 2f, sq, sq), "BLD", "B", _loop.ActiveTool == OverseerTool.Build))
                _loop.ToggleTool(OverseerTool.Build);
            x += sq + 6f;
            if (SquareAction(new Rect(x, c.y + 2f, sq, sq), "FLG", "G", _loop.ActiveTool == OverseerTool.Flag))
                _loop.ToggleTool(OverseerTool.Flag);
            x += sq + 6f;
            if (SquareAction(new Rect(x, c.y + 2f, sq, sq), "TEC", "T", _techOpen))
                ToggleTechPanel();
            x += sq + 6f;
            if (SquareAction(new Rect(x, c.y + 2f, sq, sq), "CAM", "A/B", false))
                _loop.FocusCampus(1 - _loop.FocusedCampus);
            x += sq + 6f;
            if (SquareAction(new Rect(x, c.y + 2f, sq, sq), "MENU", "Esc", false))
                _loop.TogglePause();

            Fill(new Rect(c.x + 268f, c.y + 6f, 1f, c.height - 4f), Hairline);

            float threat = _loop.FocusedLocalThreat;
            GUI.Label(new Rect(c.x + 280f, c.y + 4f, 50f, 14f), "THREAT", _micro);
            Meter(new Rect(c.x + 280f, c.y + 22f, c.width - 280f - 48f, 6f), threat, Color.Lerp(Accent, Alarm, threat));
            GUI.Label(new Rect(c.xMax - 44f, c.y + 10f, 44f, 22f), $"{threat * 100f:F0}%", _microRight);
        }

        private bool SquareAction(Rect r, string glyph, string hotkey, bool on)
        {
            bool hit = GUI.Button(r, GUIContent.none, on ? _chipOn : _chipOff);
            Outline(r, on ? Gold : Hairline);
            var prev = _pill.normal.textColor;
            _pill.normal.textColor = on ? Ink : TextPrimary;
            GUI.Label(new Rect(r.x, r.y + 4f, r.width, 18f), glyph, _pill);
            _pill.normal.textColor = prev;
            GUI.Label(new Rect(r.x, r.yMax - 14f, r.width, 12f), hotkey, _micro);
            return hit;
        }

        public void ToggleTechPanel()
        {
            _techOpen = !_techOpen;
            if (_techOpen)
                _loop?.NotifyTechOpened();
        }

        private void DrawTechPanel()
        {
            if (!_techOpen) return;
            var research = _loop.Research;
            if (research == null) return;

            const float panelW = 360f;
            const float panelH = 480f;
            var rect = new Rect(_sw - M - panelW, M + 140f, panelW, panelH);
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
                string tip = recDef != null
                    ? $"Next: {recDef.DisplayName} — click to start."
                    : "Tree complete — launch tech unlocked.";
                GUI.Label(new Rect(c.x, y, c.width, 14f), tip, _muted);
                y += 20f;
            }

            Fill(new Rect(c.x, y, c.width, 1f), Hairline);
            y += 8f;

            float listH = c.yMax - y;
            var view = new Rect(c.x, y, c.width, listH);
            var content = new Rect(0f, 0f, c.width - 18f, TechCatalog.All.Count * 54f);
            _techScroll = GUI.BeginScrollView(view, _techScroll, content);

            float rowY = 0f;
            var techs = TechCatalog.All;
            for (int i = 0; i < techs.Count; i++)
            {
                var t = techs[i];
                bool done = research.IsUnlocked(t.Id);
                bool can = research.CanSelect(t.Id);
                bool active = research.ActiveTech == t.Id;
                var row = new Rect(0f, rowY, content.width, 50f);

                if (GUI.Button(row, GUIContent.none, active ? _rowOn : _rowOff) && can)
                    research.TrySelect(t.Id);

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

            const float popupW = 300f;
            const float popupH = 340f;
            float left = DockLeft();
            var rect = new Rect(left, dockTop - 8f - popupH, popupW, popupH);
            _contentBottom = rect.y;
            var c = Panel(rect, "Flag orders");

            GUI.Label(new Rect(c.x, c.y, c.width, 13f), FlagBoardLine(), _micro);
            float y = c.y + 17f;

            y = FlagRow(fp, new Rect(c.x, y, c.width, 24f), "F1  Explore", fp.ExploreFlag);
            y = FlagRow(fp, new Rect(c.x, y, c.width, 24f), "F2  Clear Threat", fp.ClearThreatFlag);
            y = FlagRow(fp, new Rect(c.x, y, c.width, 24f), "F3  Build Here", fp.BuildFlag);
            y = FlagRow(fp, new Rect(c.x, y, c.width, 24f), "F4  Extract", fp.ExtractFlag);
            y = FlagRow(fp, new Rect(c.x, y, c.width, 24f), "F5  Defend", fp.DefendFlag);
            y = FlagRow(fp, new Rect(c.x, y, c.width, 24f), "I   Research Site", fp.ResearchSiteFlag);
            y = FlagRow(fp, new Rect(c.x, y, c.width, 24f), "O   Outpost", fp.OutpostFlag);
            y = FlagRow(fp, new Rect(c.x, y, c.width, 24f), "U   Terraform", fp.TerraformFlag);

            y += 4f;
            int metCost = SimpleEconomy.BountyMetalsCost(_loop.FlagBounty);
            bool canPay = fp.CanAffordSelectedBounty();
            GUI.Label(new Rect(c.x, y, 60f, 24f), "BOUNTY", _micro);
            GUI.Label(new Rect(c.x + 58f, y, 70f, 24f), $"${_loop.FlagBounty:F0}", _value);
            if (GUI.Button(new Rect(c.xMax - 58f, y + 2f, 26f, 20f), "−", _chipOff)) fp.NudgeBounty(-15f);
            if (GUI.Button(new Rect(c.xMax - 28f, y + 2f, 26f, 20f), "+", _chipOff)) fp.NudgeBounty(15f);

            y += 22f;
            var prevC = _micro.normal.textColor;
            _micro.normal.textColor = canPay ? TextMuted : Alarm;
            GUI.Label(new Rect(c.x, y, c.width, 13f),
                canPay ? $"escrow {metCost} MET · LMB places · RMB flag refunds" : $"need {metCost} MET — raise stockpile",
                _micro);
            _micro.normal.textColor = prevC;
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

            Fill(new Rect(r.x, r.y + 4f, 3f, r.height - 8f), data.bannerColor);
            GUI.Label(new Rect(r.x + 12f, r.y, r.width - 20f, r.height), label, on ? _onText : _action);
            return r.yMax + 4f;
        }

        private void DrawBuildPopup(float dockTop)
        {
            var bp = _loop.BuildInput;
            if (bp == null || bp.Catalog == null) return;

            int count = bp.Catalog.Length;
            const float popupW = 320f;
            float fullH = count * 28f;
            float popupH = 58f + Mathf.Min(336f, fullH);
            float left = DockLeft() + 102f;
            var rect = new Rect(left, dockTop - 8f - popupH, popupW, popupH);
            _contentBottom = rect.y;
            var c = Panel(rect, "Build catalog");

            GUI.Label(new Rect(c.x, c.y, c.width, 13f), "Pick a module · LMB on open ground", _micro);
            float y = c.y + 17f;

            var view = new Rect(c.x, y, c.width, c.yMax - y);
            var content = new Rect(0f, 0f, c.width - 18f, fullH);
            _buildScroll = GUI.BeginScrollView(view, _buildScroll, content);

            float rowY = 0f;
            for (int i = 0; i < count; i++)
            {
                var b = bp.Catalog[i];
                if (b == null) continue;

                bool on = bp.SelectedIndex == i;
                bool locked = !_loop.IsBuildingUnlocked(b.category);
                bool canAfford = !locked && (_loop.Resources == null || _loop.Resources.CanAfford(b.buildCost));
                var r = new Rect(0f, rowY, content.width, 24f);

                if (GUI.Button(r, GUIContent.none, on ? _rowOn : _rowOff) && !locked)
                {
                    _loop.SetTool(OverseerTool.Build);
                    bp.SelectBuilding(i);
                }

                GUI.Label(new Rect(r.x + 8f, r.y, 18f, r.height), BuildHotkeyLabel(i), on ? _onText : _micro);
                var nameStyle = on ? _onText : _action;
                var prevColor = nameStyle.normal.textColor;
                if (!on && (!canAfford || locked)) nameStyle.normal.textColor = TextMuted;
                GUI.Label(new Rect(r.x + 26f, r.y, r.width - 110f, r.height), b.displayName, nameStyle);
                nameStyle.normal.textColor = prevColor;

                var costStyle = _microRight;
                var prevCost = costStyle.normal.textColor;
                if (locked) costStyle.normal.textColor = TextMuted;
                else if (!canAfford) costStyle.normal.textColor = Alarm;
                GUI.Label(new Rect(r.xMax - 92f, r.y, 86f, r.height),
                    locked ? "NEED TECH" : FormatBuildCost(b), costStyle);
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

        private bool Chip(Rect r, string label, bool on) =>
            GUI.Button(r, label, on ? _chipOn : _chipOff);

        private static string FormatBuildCost(BuildingData b)
        {
            if (b == null || b.buildCost == null || b.buildCost.Length == 0) return "free";
            var parts = new System.Text.StringBuilder();
            for (int i = 0; i < b.buildCost.Length; i++)
            {
                if (i > 0) parts.Append(" · ");
                parts.Append(b.buildCost[i].amount);
                parts.Append(' ');
                parts.Append(ShortResource(b.buildCost[i].resource));
            }
            return parts.ToString();
        }

        private static string ShortResource(ResourceId id) => id switch
        {
            ResourceId.Regolith => "REG",
            ResourceId.WaterIce => "ICE",
            ResourceId.Metals => "MET",
            ResourceId.Power => "PWR",
            _ => id.ToString().ToUpperInvariant()
        };

        private void DrawMissionPanel()
        {
            var mission = _loop.Mission;
            if (mission == null) return;

            var flags = _loop.Flags != null ? _loop.Flags.Flags : null;
            int flagN = flags != null ? Mathf.Min(3, flags.Count) : 0;
            float h = 118f;
            if (mission.DeadlineEnabled) h += 26f;
            h += 18f + flagN * 16f;

            var rect = new Rect(_sw - M - 300f, _playTop > 1f ? _playTop : M, 300f, h);
            var c = Panel(rect, "Conquest gates");
            float y = c.y;

            Stake(new Rect(c.x, y, c.width, 20f), mission.DensCleared,
                "Clear dens",
                mission.LairCount > 0
                    ? $"{mission.UnclearedLairs}/{mission.LairCount} left"
                    : $"{mission.StalkersRemaining} fauna");
            y += 20f;

            var set = _loop.Settlement;
            string sustainVal = set != null
                ? $"pop {mission.PopulationCurrent}/{mission.PopulationGoal} · {_loop.FormatHold(mission.SustainElapsed)}/{_loop.FormatHold(mission.SustainRequired)}"
                : _loop.FormatHold(mission.SustainElapsed);
            Stake(new Rect(c.x, y, c.width, 20f), mission.SustainComplete,
                "Sustain colony",
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

            GUI.Label(new Rect(c.x, y, c.width, 14f), "BOUNTY LOG", _section);
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
                    GUI.Label(new Rect(c.x, y, c.width, 15f),
                        $"{f.Data.displayName}  ${f.CurrentBounty:F0}  ·  {claim}", _micro);
                    y += 15f;
                }
            }
        }

        private void Stake(Rect r, bool done, string label, string value)
        {
            CheckBox(new Rect(r.x, r.y + 4f, 11f, 11f), done);
            GUI.Label(new Rect(r.x + 19f, r.y, r.width - 110f, r.height), label, done ? _muted : _body);
            GUI.Label(new Rect(r.xMax - 90f, r.y, 90f, r.height), value, _microRight);
        }

        private void DrawInspectPanel()
        {
            if (_loop.SelectedStructure != null)
            {
                DrawBuildingCard(_loop.SelectedStructure);
                return;
            }
            DrawSpecialistCards();
        }

        private void DrawBuildingCard(ColonyStructure st)
        {
            const float cardW = 340f;
            float cardH = st.IsGuild ? 172f : 148f;
            float y0 = _contentBottom - 8f - cardH;
            var rect = new Rect(M, y0, cardW, cardH);
            var c = Panel(rect, null);
            Outline(rect, new Color(0.96f, 0.42f, 0.08f, 0.45f));

            float row = c.y;
            GUI.Label(new Rect(c.x, row, c.width, 16f), st.DisplayName, _value);
            row += 18f;

            string role = st.IsWorkshop
                ? (st.RobotFabricated ? "Workshop · robot online" : "Workshop · fabricating…")
                : st.IsGuild
                    ? (st.HasPreferredClass ? st.DisplayName : "Guild Hall · assign a class")
                    : st.IsWonder ? "Secret Project landmark"
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
                    ? $"Colonists indoors — tax + births, no outdoor villagers."
                    : "Empty beds — seed crew arrives with the first HAB.")
                : FormatWorkers(st);
            GUI.Label(new Rect(c.x, row, c.width, 13f), workers, _micro);
            row += 16f;

            if (st.IsResidential)
            {
                GUI.Label(new Rect(c.x, row, c.width, 22f),
                    "Humans stay in HABs. Outdoor work is robots from workshops.", _micro);
            }
            else if (st.IsGuild)
            {
                GUI.Label(new Rect(c.x, row, c.width, 13f),
                    "Assign a class. Flags near this hall pull them (no new robot).", _micro);
                row += 14f;
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
                        ? "Landmark only — bonuses already came from the ★ tech."
                        : st.RobotFabricated
                            ? $"Fabricates {ColonyStructure.ClassLabel(st.PreferredClass)} — flags nearby pull them."
                            : $"Building a {ColonyStructure.ClassLabel(st.PreferredClass)} robot…",
                    _micro);
            }
            row += 26f;

            if (GUI.Button(new Rect(c.x, row, 116f, 24f), "FLAG HERE", _chipOff))
                _loop.PostAttractFlagOnSelected();
            GUI.Label(new Rect(c.x + 124f, row + 4f, c.width - 124f, 20f),
                "Progress via research & conquest gates", _micro);
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

        private void DrawSpecialistCards()
        {
            var selected = _loop.SelectedAgents;
            if (selected == null || selected.Count == 0)
                return;

            int n = selected.Count;
            float avail = _sw - M * 2f - (n - 1) * 8f;
            float cardW = Mathf.Clamp(avail / n, 168f, 230f);
            const float cardH = 162f;
            float y = _contentBottom - 8f - cardH;

            for (int i = 0; i < n; i++)
            {
                var a = selected[i];
                if (a == null) continue;

                var rect = new Rect(M + i * (cardW + 8f), y, cardW, cardH);
                var c = Panel(rect, null);
                Outline(rect, new Color(0.96f, 0.42f, 0.08f, 0.45f));
                if (a.IsIncapacitated) Outline(rect, new Color(0.95f, 0.32f, 0.26f, 0.55f));
                Fill(new Rect(rect.x, rect.y, 3f, rect.height), ClassTint(a.Data != null ? a.Data.specialistClass : SpecialistClass.ScoutDrone));

                float row = c.y;
                GUI.Label(new Rect(c.x, row, c.width - 44f, 16f), a.Data?.displayName ?? "Specialist", _value);
                if (a.IsIncapacitated)
                {
                    var tag = new Rect(c.xMax - 42f, row + 2f, 42f, 13f);
                    Fill(tag, Alarm);
                    var prev = _pill.normal.textColor;
                    _pill.normal.textColor = Ink;
                    GUI.Label(tag, "DOWN", _pill);
                    _pill.normal.textColor = prev;
                }
                row += 18f;

                int campus = ColonyLayout.NearestCampusIndex(a.transform.position);
                GUI.Label(new Rect(c.x, row, c.width, 13f),
                    $"{ColonyLayout.CampusLabel(campus)} · ${a.Credits:F0} · {a.SuitLabel}", _micro);
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
                row += 17f;

                string gene = a.GeneSecondsLeft > 0.5f
                    ? $"gene {a.GeneSecondsLeft:F0}s"
                    : "shop at rest beacon";
                GUI.Label(new Rect(c.x, row, c.width, 13f),
                    Truncate($"{Truncate(a.LastReason, 18)} · {gene}", 36), _micro);
            }
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
                    : "Kingdom vocation",
                _ => "Idle"
            };
            return string.IsNullOrEmpty(a.Status) ? action : $"{action} — {a.Status}";
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
                ? "Bounty escrowed from METALS. Heroes keep $."
                : $"Posted {posted}  ·  claimed {claimed}  ·  heroes keep $";
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
                var hint = new Rect(M, _contentBottom - 8f - 24f, 340f, 24f);
                _hitRects.Add(hint);
                Fill(hint, PanelBg);
                Outline(hint, Hairline);
                GUI.Label(new Rect(hint.x + 10f, hint.y, hint.width - 16f, hint.height),
                    "Build the Palace keep first · dock via airlocks · HABs house colonists", _micro);
                return;
            }

            int rows = Mathf.Min(8, living);
            float h = 28f + rows * 18f;
            var rect = new Rect(M, _contentBottom - 8f - h, 248f, h);
            var c = Panel(rect, "Roster · status");
            float y = c.y;
            int drawn = 0;
            for (int i = 0; i < agents.Count && drawn < rows; i++)
            {
                var a = agents[i];
                if (a == null || !a.IsAlive) continue;
                var cls = a.Data != null ? a.Data.specialistClass : SpecialistClass.ScoutDrone;
                string status = RosterStatus(a);
                var row = new Rect(c.x, y, c.width, 16f);
                Fill(new Rect(row.x, row.y + 3f, 3f, 10f), ClassTint(cls));
                if (GUI.Button(row, GUIContent.none, _rowOff))
                    _loop.SelectOnly(a);
                GUI.Label(new Rect(row.x + 8f, row.y, 52f, 16f), ColonyStructure.ClassLabel(cls), _micro);
                GUI.Label(new Rect(row.x + 62f, row.y, 50f, 16f), status, _body);
                GUI.Label(new Rect(row.x + 114f, row.y, row.width - 114f, 16f),
                    Truncate(a.FlavorLine, 16), _micro);
                y += 18f;
                drawn++;
            }
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
            EnsureMinimapDisc();
            float size = MapSize;
            var rect = new Rect(_sw - M - size, _sh - M - size, size, size);
            _hitRects.Add(rect);
            Fill(rect, PanelBg);
            Outline(rect, Gold);

            var disc = new Rect(rect.x + 8f, rect.y + 8f, size - 16f, size - 16f);
            if (_minimapDisc != null)
            {
                var prev = GUI.color;
                GUI.color = new Color(0.08f, 0.07f, 0.06f, 0.95f);
                GUI.DrawTexture(disc, _minimapDisc);
                GUI.color = prev;
            }

            float worldW = _loop.Grid != null ? _loop.Grid.WorldWidth : 384f;
            float worldH = _loop.Grid != null ? _loop.Grid.WorldHeight : 384f;
            Vector2 MapPoint(Vector3 world)
            {
                float u = Mathf.Clamp01(world.x / worldW);
                float v = Mathf.Clamp01(world.z / worldH);
                return new Vector2(disc.x + u * disc.width, disc.yMax - v * disc.height);
            }

            void Pip(Vector3 world, Color color, float s)
            {
                Vector2 p = MapPoint(world);
                Fill(new Rect(p.x - s * 0.5f, p.y - s * 0.5f, s, s), color);
            }

            Pip(ColonyLayout.CampusOrigin, Accent, 6f);
            Pip(ColonyLayout.CampusBOrigin, new Color(0.35f, 0.85f, 1f), 5f);

            var flags = _loop.Flags != null ? _loop.Flags.Flags : null;
            if (flags != null)
            {
                for (int i = 0; i < flags.Count; i++)
                {
                    var f = flags[i];
                    if (f == null) continue;
                    Color col = f.Data != null ? f.Data.bannerColor : Gold;
                    Pip(f.WorldPosition, col, 4f);
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
                    Pip(s.transform.position, Alarm, 3f);
                }
            }

            if (Camera.main != null)
            {
                var ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                if (Mathf.Abs(ray.direction.y) > 0.01f)
                {
                    float t = -ray.origin.y / ray.direction.y;
                    Pip(ray.origin + ray.direction * t, Color.white, 5f);
                }
            }

            GUI.Label(new Rect(rect.x + 8f, rect.yMax - 18f, rect.width - 16f, 14f), "MAP · click pans", _micro);

            var e = Event.current;
            Vector2 mouse = e.mousePosition;
            mouse.x /= _hudScale;
            mouse.y /= _hudScale;
            if (e.type == EventType.MouseDown && e.button == 0 && disc.Contains(mouse))
            {
                float u = (mouse.x - disc.x) / disc.width;
                float v = 1f - (mouse.y - disc.y) / disc.height;
                var world = new Vector3(u * worldW, 0f, v * worldH);
                _loop.GlanceAt(world, force: true);
                e.Use();
            }
        }

        private void EnsureMinimapDisc()
        {
            if (_minimapDisc != null) return;
            const int s = 64;
            _minimapDisc = new Texture2D(s, s, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear
            };
            var px = new Color[s * s];
            float r = s * 0.5f - 1f;
            Vector2 c = new Vector2(s * 0.5f, s * 0.5f);
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c);
                px[y * s + x] = d <= r ? Color.white : Color.clear;
            }
            _minimapDisc.SetPixels(px);
            _minimapDisc.Apply();
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
            if (string.IsNullOrEmpty(_toast) || Time.unscaledTime > _toastUntil)
            {
                _toast = null;
                return;
            }

            float left = M + TopW + 10f;
            float right = _sw - M - 286f - 10f;
            float w = Mathf.Min(380f, Mathf.Max(200f, right - left));
            float x = Mathf.Clamp((_sw - w) * 0.5f, left, Mathf.Max(left, right - w));
            var rect = new Rect(x, _playTop > 1f ? _playTop : M, w, 32f);
            _hitRects.Add(rect);
            Fill(rect, PanelBg);
            Outline(rect, Hairline);
            Fill(new Rect(rect.x, rect.y, 2f, rect.height), Accent);
            GUI.Label(new Rect(rect.x + 12f, rect.y, rect.width - 20f, rect.height), _toast, _body);
        }

        private void DrawWinBanner()
        {
            var mission = _loop.Mission;
            if (mission == null || !mission.IsWon || _winDismissed) return;

            Fill(new Rect(0, 0, _sw, _sh), new Color(0.02f, 0.05f, 0.03f, 0.55f));

            var rect = new Rect((_sw - 460f) * 0.5f, _sh * 0.26f, 460f, 216f);
            var c = Panel(rect, null, false);
            Fill(new Rect(rect.x, rect.y, rect.width, 2f), Good);

            var prev = _banner.normal.textColor;
            _banner.normal.textColor = Good;
            GUI.Label(new Rect(c.x, c.y, c.width, 26f), mission.WinHeadline, _banner);
            _banner.normal.textColor = prev;

            GUI.Label(new Rect(c.x, c.y + 32f, c.width, 32f), mission.WinDetail, _body);
            GUI.Label(new Rect(c.x, c.y + 64f, c.width, 24f), mission.WinSubline, _muted);
            var rating = _loop.CurrentRating;
            GUI.Label(new Rect(c.x, c.y + 90f, c.width, 16f), rating.Summary, _value);
            GUI.Label(new Rect(c.x, c.y + 108f, c.width, 14f), rating.Breakdown, _micro);

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
            var mission = _loop.Mission;
            bool overwhelmed = _loop.IsOutpostOverwhelmed;
            bool deadline = mission != null && mission.IsLost && mission.WasDeadlineFail && !_deadlineDismissed;
            if (!overwhelmed && !deadline) return;

            if (!_failLatched)
            {
                _failLatched = true;
                DemoAudio.PlayFail();
            }

            Fill(new Rect(0, 0, _sw, _sh), new Color(0.10f, 0.01f, 0.01f, 0.55f));

            var rect = new Rect((_sw - 460f) * 0.5f, _sh * 0.32f, 460f, 168f);
            var c = Panel(rect, null, false);
            Fill(new Rect(rect.x, rect.y, rect.width, 2f), Alarm);

            var prev = _banner.normal.textColor;
            _banner.normal.textColor = Alarm;
            GUI.Label(new Rect(c.x, c.y, c.width, 26f), mission != null ? mission.FailHeadline : "OUTPOST OVERWHELMED", _banner);
            _banner.normal.textColor = prev;

            if (deadline)
            {
                GUI.Label(new Rect(c.x, c.y + 32f, c.width, 32f),
                    mission != null ? mission.FailDetail : "The window to secure the outpost closed.", _body);
                GUI.Label(new Rect(c.x, c.y + 66f, c.width, 16f), "Restart to try a tighter run.", _muted);
                if (GUI.Button(new Rect(c.x, c.yMax - 30f, 160f, 28f), "RESTART MISSION", _chipOn))
                    _loop.RestartMission();
            }
            else
            {
                GUI.Label(new Rect(c.x, c.y + 32f, c.width, 32f),
                    mission != null ? mission.FailDetail : "Every specialist is incapacitated.", _body);
                GUI.Label(new Rect(c.x, c.y + 66f, c.width, 16f), "Stalkers hold the plaza until the party is revived.", _muted);
                if (GUI.Button(new Rect(c.x, c.yMax - 30f, 160f, 28f), "REVIVE PARTY  ·  Y", _chipOn))
                    _loop.RetryParty();
            }
        }

        private void Update()
        {
            if (_loop == null || !_loop.IsPlaying) return;
            if (_loop.IsOutpostOverwhelmed && Input.GetKeyDown(KeyCode.Y))
                _loop.RetryParty();
            if (!_loop.IsOutpostOverwhelmed &&
                !(_loop.Mission != null && _loop.Mission.IsLost && _loop.Mission.WasDeadlineFail))
                _failLatched = false;

            if (_loop.Mission != null && _loop.Mission.IsWon && Input.GetKeyDown(KeyCode.Y) && !_loop.IsOutpostOverwhelmed)
            {
                _winDismissed = true;
                _loop.Mission.DismissWinToSandbox();
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

        private void DrawTitle()
        {
            Fill(new Rect(0, 0, _sw, _sh), new Color(0.02f, 0.03f, 0.04f, 0.86f));
            var rect = new Rect((_sw - 480f) * 0.5f, _sh * 0.10f, 480f, 520f);
            var c = Panel(rect, null, false);
            Fill(new Rect(rect.x, rect.y, rect.width, 3f), Accent);

            GUI.Label(new Rect(c.x, c.y, c.width, 32f), "SOLAR MAJESTY", _banner);
            GUI.Label(new Rect(c.x, c.y + 34f, c.width, 16f), "You are the Overseer. Never command the heroes.", _muted);
            GUI.Label(new Rect(c.x, c.y + 52f, c.width, 16f), "EARTH  →  LUNA  →  MARS  →  BELT  →  EUROPA", _wrap);
            GUI.Label(new Rect(c.x, c.y + 68f, c.width, 14f), ReplayRules.HudTag, _micro);
            GUI.Label(new Rect(c.x, c.y + 86f, c.width, 40f),
                "Raise a Palace, post bounties, let greedy robots choose. Three gates: clear dens, sustain the colony, launch.",
                _wrap);

            if (_confirmNewGame)
            {
                GUI.Label(new Rect(c.x, c.y + 130f, c.width, 48f),
                    "This wipes the continue slot and campaign unlocks, then drops you on Earth.",
                    _wrap);
                if (GUI.Button(new Rect(c.x, c.y + 186f, c.width, 40f), "WIPE AND DROP EARTH", _chipOn))
                {
                    _confirmNewGame = false;
                    _loop.StartNewGame();
                }
                if (GUI.Button(new Rect(c.x, c.y + 234f, c.width, 36f), "BACK", _chipOff))
                    _confirmNewGame = false;
                GUI.Label(new Rect(c.x, c.yMax - 18f, c.width, 16f), "Esc also cancels.", _micro);
                if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
                {
                    _confirmNewGame = false;
                    Event.current.Use();
                }
                return;
            }

            float y = c.y + 130f;
            if (GUI.Button(new Rect(c.x, y, c.width, 40f), "NEW GAME  ·  Earth drop", _chipOn))
            {
                if (DemoSettings.SaveExists)
                    _confirmNewGame = true;
                else
                    _loop.StartNewGame();
            }
            y += 48f;

            GUI.enabled = DemoSettings.SaveExists;
            if (GUI.Button(new Rect(c.x, y, c.width, 40f), DemoSettings.ContinueButtonLabel(), _chipOff))
                _loop.ContinueGame();
            GUI.enabled = true;
            y += 42f;
            GUI.Label(new Rect(c.x, y, c.width, 28f), DemoSettings.ContinueDetail(), _wrap);
            y += 36f;
            if (GUI.Button(new Rect(c.x, y, c.width, 36f), "SETTINGS", _chipOff))
                _loop.OpenSettings();
            y += 44f;
            if (GUI.Button(new Rect(c.x, y, c.width, 36f), "QUIT", _chipOff))
                _loop.QuitDemo();

            GUI.Label(new Rect(c.x, c.yMax - 36f, c.width, 16f),
                "B build  ·  G flag  ·  T tech  ·  Esc pause  ·  RMB cancel flag", _micro);
            GUI.Label(new Rect(c.x, c.yMax - 18f, c.width, 16f),
                "WASD pans this frozen drop. Heroes choose their own work.", _micro);
        }

        private void DrawPause()
        {
            Fill(new Rect(0, 0, _sw, _sh), new Color(0.02f, 0.02f, 0.03f, 0.72f));
            var rect = new Rect((_sw - 420f) * 0.5f, _sh * 0.22f, 420f, 330f);
            var c = Panel(rect, "Paused");
            GUI.Label(new Rect(c.x, c.y, c.width, 28f),
                "Simulation frozen. Autosave keeps this body's campus, stockpile, and research.",
                _wrap);
            if (GUI.Button(new Rect(c.x, c.y + 36f, c.width, 32f), "RESUME  ·  Esc", _chipOn))
                _loop.ResumePlay();
            if (GUI.Button(new Rect(c.x, c.y + 76f, c.width, 32f), "SETTINGS", _chipOff))
                _loop.OpenSettings();
            if (GUI.Button(new Rect(c.x, c.y + 116f, c.width, 32f), "TITLE", _chipOff))
                _loop.ReturnToTitle();
            if (GUI.Button(new Rect(c.x, c.y + 156f, c.width, 32f), "ABANDON BODY  ·  new seed", _chipOff))
            {
                DemoSettings.RequestBootIntoPlay();
                _loop.RestartMission();
            }
            if (GUI.Button(new Rect(c.x, c.y + 196f, c.width, 32f), "QUIT", _chipOff))
                _loop.QuitDemo();
        }

        private void DrawSettings()
        {
            Fill(new Rect(0, 0, _sw, _sh), new Color(0.02f, 0.02f, 0.03f, 0.78f));
            float h = Mathf.Min(640f, Mathf.Max(420f, _sh - 36f));
            var rect = new Rect((_sw - 440f) * 0.5f, Mathf.Max(10f, (_sh - h) * 0.5f), 440f, h);
            var c = Panel(rect, "Settings");
            float y = c.y;

            y = SettingsSlider(c.x, y, c.width, "MASTER", ref DemoSettings.Master);
            y = SettingsSlider(c.x, y, c.width, "SFX", ref DemoSettings.Sfx);
            y = SettingsSlider(c.x, y, c.width, "AMBIENCE", ref DemoSettings.Ambient);
            y = SettingsSlider(c.x, y, c.width, "HUD SCALE", ref DemoSettings.HudScale, 0.85f, 1.25f, applyAudio: false);
            y += 6f;

            if (Chip(new Rect(c.x, y, 200f, 26f), "INVERT CAMERA PAN", DemoSettings.InvertPan))
                DemoSettings.InvertPan = !DemoSettings.InvertPan;
            y += 32f;

            if (Chip(new Rect(c.x, y, 200f, 26f),
                    DemoSettings.Fullscreen ? "FULLSCREEN" : "WINDOWED", DemoSettings.Fullscreen))
            {
                DemoSettings.Fullscreen = !DemoSettings.Fullscreen;
                DemoSettings.ApplyDisplay();
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

            string tutLabel = _loop.IsTutorialActive ? "TUTORIAL  ·  ON" : "REPLAY TUTORIAL";
            if (Chip(new Rect(c.x, y, 220f, 26f), tutLabel, _loop.IsTutorialActive))
                _loop.RestartTutorial();
            y += 36f;

            Fill(new Rect(c.x, y, c.width, 1f), Hairline);
            y += 8f;
            GUI.Label(new Rect(c.x, y, c.width, 14f), "REPLAY  ·  MODE / CHALLENGE / STANCE", _section);
            y += 18f;

            float half = (c.width - 8f) * 0.5f;
            if (Chip(new Rect(c.x, y, half, 26f),
                    $"MODE  ·  {ReplayRules.ModeLabel}", ReplayRules.IsEndless))
                ReplayRules.CycleMode();
            if (Chip(new Rect(c.x + half + 8f, y, half, 26f),
                    $"CHAL  ·  {ReplayRules.ChallengeLabel}", ReplayRules.Challenge != ChallengeId.None))
                ReplayRules.CycleChallenge();
            y += 32f;

            if (Chip(new Rect(c.x, y, c.width, 26f),
                    $"STANCE  ·  {ReplayRules.StanceLabel}", ReplayRules.Stance != DoctrineStance.Balanced))
                ReplayRules.CycleStance();
            y += 30f;

            GUI.Label(new Rect(c.x, y, c.width, 48f),
                ReplayRules.StanceHint + " " + ReplayRules.ChallengeHint,
                _wrap);
            y += 50f;
            GUI.Label(new Rect(c.x, y, c.width, 36f),
                "Stockpile and fauna apply on New Game / reload. Doctrine hunger, courage, range, and workshop pull apply live. Tight Purse ship rules apply when you leave Settings.",
                _wrap);

            if (GUI.Button(new Rect(c.x, c.yMax - 36f, c.width, 32f), "BACK  ·  Esc", _chipOn))
                _loop.CloseSettings();
        }

        private float SettingsSlider(
            float x, float y, float width, string label, ref float value,
            float min = 0f, float max = 1f, bool applyAudio = true)
        {
            GUI.Label(new Rect(x, y, 120f, 18f), label, _micro);
            float next = GUI.HorizontalSlider(new Rect(x + 90f, y + 4f, width - 90f, 16f), value, min, max);
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

            string[] beats =
            {
                "1/6  Palace — B, key 1. Raise the keep on the orange claim.",
                "2/6  Airlock — snap an Airlock Junction onto a Palace face socket.",
                "3/6  HAB — dock housing onto that airlock. Humans live indoors only.",
                "4/6  Workshop — dock Scout / Engineer / Defense. A robot fabricates when it finishes.",
                "5/6  Flag — G, post a bounty (METALS). Robots choose; you never click-to-move.",
                "6/6  Research — T, keep Field Survey ticking toward Lunar Rocket."
            };
            int step = Mathf.Clamp(_loop.TutorialStep, 0, beats.Length - 1);
            float w = 640f;
            var rect = new Rect((_sw - w) * 0.5f, _sh - M - DockH - 78f, w, 64f);
            var c = Panel(rect, null);
            GUI.Label(new Rect(c.x, c.y, c.width - 80f, 40f), beats[step], _wrap);
            if (GUI.Button(new Rect(c.xMax - 72f, c.y + 8f, 72f, 24f), "SKIP", _chipOff))
                _loop.SkipTutorial();
        }

        private void DrawDropManifest()
        {
            if (!_loop.StartsEmpty || _loop.TutorialStep > 3) return;
            var catalog = _loop.StarterBuildings;
            if (catalog == null || catalog.Length == 0) return;

            BuildingCategory[] want =
            {
                BuildingCategory.Palace,
                BuildingCategory.Habitat,
                BuildingCategory.Utility,
                BuildingCategory.EngineerWorkshop
            };

            var rect = new Rect(M, _playTop + CommandPanelHeight() + 8f, TopW, 118f);
            var c = Panel(rect, "Drop manifest");
            float y = c.y;
            GUI.Label(new Rect(c.x, y, c.width, 13f), "Palace → airlock sockets → HAB / workshops. Lego campus only.", _micro);
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
                    $"{found.displayName}  ·  {FormatBuildCost(found)}", _micro);
                y += 15f;
            }
        }
    }
}
