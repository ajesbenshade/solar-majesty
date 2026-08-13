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
        // Lunar industrial palette — near-black panels, white type, orange accent.
        private static readonly Color PanelBg = new Color(0.05f, 0.053f, 0.058f, 0.90f);
        private static readonly Color PanelSoft = new Color(0.11f, 0.115f, 0.12f, 0.92f);
        private static readonly Color PanelHover = new Color(0.17f, 0.175f, 0.18f, 0.95f);
        private static readonly Color Hairline = new Color(1f, 1f, 1f, 0.09f);
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
        private const float DockH = 52f;
        private const float DockW = 660f;

        private GameLoop _loop;
        private bool _failLatched;
        private bool _winDismissed;
        private bool _deadlineDismissed;
        private bool _techOpen;
        private Vector2 _techScroll;
        private string _toast;
        private float _toastUntil;
        private int _lastFocusToast = -1;
        private float _contentBottom; // top of dock/popup stack — cards sit above this


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
            _lastFocusToast = _loop != null ? _loop.FocusedCampus : -1;
            if (_loop != null && _loop.Economy != null)
            {
                _loop.Economy.ResupplyArrived -= OnResupply;
                _loop.Economy.ResupplyArrived += OnResupply;
            }

            if (_loop != null && _loop.ActiveBody == CelestialBodyId.Earth)
            {
                Toast("Earth tutorial — clear dens, grow to pop goal, research Lunar Rocket (T).", 5.5f);
            }
            else if (_loop != null && _loop.ActiveBody == CelestialBodyId.Luna)
            {
                Toast("Luna — dens + sustain, then research Mars Ship for departure.", 4.5f);
            }
            else if (_loop != null && _loop.ActiveBody == CelestialBodyId.Mars)
            {
                Toast("Mars finale — clear dens, sustain, Mars Ship already unlocks the pad.", 4.5f);
            }
        }

        private void OnResupply()
        {
            Toast("Earth resupply docked at Campus A pad — stockpile topped up.", 4f);
            DemoAudio.PlayRetry();
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
            if (accentTab) Fill(new Rect(r.x, r.y, 2f, 20f), Accent);

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

            DrawCommandPanel(M);
            DrawMissionPanel();
            DrawTechPanel();

            float dockTop = Screen.height - M - DockH;
            _contentBottom = dockTop;
            DrawToolPopups(dockTop);
            DrawBottomDock(dockTop);

            DrawConstructionPanel();
            DrawInspectPanel();
            DrawToast();
            DrawWinBanner();
            DrawFailBanner();

            Vector2 imguiMouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            PointerBlocksWorld = false;
            for (int i = 0; i < _hitRects.Count; i++)
            {
                if (_hitRects[i].Contains(imguiMouse))
                {
                    PointerBlocksWorld = true;
                    break;
                }
            }
        }

        /// <summary>Brand + stockpile + current objective.</summary>
        private void DrawCommandPanel(float top)
        {
            var rect = new Rect(M, top, TopW, 172f);
            var c = Panel(rect, null);

            GUI.Label(new Rect(c.x, c.y, 190f, 22f), "SOLAR MAJESTY", _brand);

            var mission = _loop.Mission;
            string status = "SANDBOX";
            Color pill = PanelSoft;
            Color pillText = TextPrimary;
            if (mission != null)
            {
                switch (mission.State)
                {
                    case MissionState.Won:
                        status = "SECURED";
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

            var pillRect = new Rect(c.xMax - 62f, c.y + 4f, 62f, 16f);
            Fill(pillRect, pill);
            var prevPill = _pill.normal.textColor;
            _pill.normal.textColor = pillText;
            GUI.Label(pillRect, status, _pill);
            _pill.normal.textColor = prevPill;

            float y = c.y + 22f;
            string objective = mission != null ? mission.ObjectiveLabel : "Free sandbox — post bounties.";
            GUI.Label(new Rect(c.x, y, c.width, 14f), objective, _muted);
            y += 14f;

            string code = _loop.BodyProfile != null ? _loop.BodyProfile.ShortCode : "LUNA";
            string seedLine = $"{code} seed {_loop.MoonSeedValue}";
            if (_loop.World != null)
            {
                seedLine +=
                    $"  ·  nodes {_loop.World.Nodes.Count}" +
                    $"  ·  lairs {_loop.World.UnclearedLairCount}/{_loop.World.Lairs.Count}";
            }
            GUI.Label(new Rect(c.x, y, c.width, 13f), seedLine, _micro);
            y += 14f;

            var bodies = CelestialBodyCatalog.All;
            float chipW = (c.width - 3f * (bodies.Length - 1)) / bodies.Length;
            for (int i = 0; i < bodies.Length; i++)
            {
                var id = bodies[i];
                var profile = CelestialBodyCatalog.Get(id);
                bool unlocked = CampaignProgress.IsUnlocked(id);
                var chipRect = new Rect(c.x + i * (chipW + 3f), y, chipW, 20f);
                string label = unlocked ? profile.ShortCode : $"{profile.ShortCode}?";
                if (Chip(chipRect, label, _loop.ActiveBody == id) && unlocked)
                    _loop.SelectBody(id);
            }
            y += 22f;
            Fill(new Rect(c.x, y, c.width, 1f), Hairline);
            y += 6f;

            if (_loop.Resources != null)
            {
                float resW = (c.width - 9f) / 4f;
                ResourceChip(new Rect(c.x, y, resW, 28f), "REGOLITH", _loop.Resources.Get(ResourceId.Regolith));
                ResourceChip(new Rect(c.x + resW + 3f, y, resW, 28f), "ICE", _loop.Resources.Get(ResourceId.WaterIce));
                ResourceChip(new Rect(c.x + (resW + 3f) * 2f, y, resW, 28f), "METALS", _loop.Resources.Get(ResourceId.Metals));
                ResourceChip(new Rect(c.x + (resW + 3f) * 3f, y, resW, 28f), "POWER", _loop.Resources.Get(ResourceId.Power));
                y += 32f;
            }

            var set = _loop.Settlement;
            if (set != null)
            {
                GUI.Label(
                    new Rect(c.x, y, c.width, 14f),
                    $"POP {set.Population}/{set.PopulationGoal}  ·  HOUSING {set.Housing}  ·  VILLAGE {set.VillageHabs}  ·  TAX +{set.LastTax}",
                    _micro);
                y += 16f;
            }

            int partyCount = _loop.Parties != null ? _loop.Parties.Count : 0;
            if (Chip(new Rect(c.x, y, 118f, 22f), "PARTY · P", partyCount > 0))
                _loop.FormPartyAtInn();
            GUI.Label(new Rect(c.x + 124f, y + 4f, c.width - 124f, 16f), "[  disband", _micro);
        }

        private void ResourceChip(Rect r, string label, int amount)
        {
            Fill(r, PanelSoft);
            GUI.Label(new Rect(r.x + 6f, r.y + 1f, r.width - 8f, 11f), label, _micro);
            GUI.Label(new Rect(r.x + 6f, r.y + 12f, r.width - 8f, 15f), amount.ToString(), _value);
        }

        private float DockLeft() => (Screen.width - DockW) * 0.5f;

        private void DrawBottomDock(float top)
        {
            var rect = new Rect(DockLeft(), top, DockW, DockH);
            var c = Panel(rect, null);

            // Flag / Build — toggle popups. Re-click closes (inspect mode).
            if (Chip(new Rect(c.x, c.y + 4f, 96f, 30f), "FLAG · G", _loop.ActiveTool == OverseerTool.Flag))
                _loop.ToggleTool(OverseerTool.Flag);
            if (Chip(new Rect(c.x + 102f, c.y + 4f, 96f, 30f), "BUILD · B", _loop.ActiveTool == OverseerTool.Build))
                _loop.ToggleTool(OverseerTool.Build);
            if (Chip(new Rect(c.x + 204f, c.y + 4f, 96f, 30f), "TECH · T", _techOpen))
                ToggleTechPanel();

            Fill(new Rect(c.x + 312f, c.y + 6f, 1f, c.height - 4f), Hairline);

            GUI.Label(new Rect(c.x + 324f, c.y + 8f, 40f, 22f), "FOCUS", _micro);
            if (Chip(new Rect(c.x + 364f, c.y + 6f, 34f, 26f), "A", _loop.FocusedCampus == 0))
                _loop.FocusCampus(0);
            if (Chip(new Rect(c.x + 402f, c.y + 6f, 34f, 26f), "B", _loop.FocusedCampus == 1))
                _loop.FocusCampus(1);

            float threat = _loop.FocusedLocalThreat;
            GUI.Label(new Rect(c.x + 452f, c.y + 2f, 50f, 14f), "THREAT", _micro);
            Meter(new Rect(c.x + 452f, c.y + 18f, c.width - 452f - 44f, 6f), threat, Color.Lerp(Accent, Alarm, threat));
            GUI.Label(new Rect(c.xMax - 40f, c.y + 8f, 40f, 22f), $"{threat * 100f:F0}%", _microRight);
        }

        public void ToggleTechPanel() => _techOpen = !_techOpen;

        private void DrawTechPanel()
        {
            if (!_techOpen) return;
            var research = _loop.Research;
            if (research == null) return;

            const float panelW = 360f;
            const float panelH = 420f;
            var rect = new Rect(Screen.width - M - panelW, M + 140f, panelW, panelH);
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

                string mark = done ? "DONE" : active ? "…" : can ? "GO" : "—";
                Color markC = done ? Good : active ? Accent : can ? TextPrimary : TextMuted;
                GUI.Label(new Rect(row.x + 6f, row.y + 4f, row.width - 50f, 16f), t.DisplayName, _value);
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

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max) return s ?? "";
            return s.Substring(0, max - 1) + "…";
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
            const float popupH = 236f;
            float left = DockLeft();
            var rect = new Rect(left, dockTop - 8f - popupH, popupW, popupH);
            _contentBottom = rect.y;
            var c = Panel(rect, "Flag orders");

            GUI.Label(new Rect(c.x, c.y, c.width, 13f), "Post a bounty — specialists choose freely.", _micro);
            float y = c.y + 17f;

            y = FlagRow(fp, new Rect(c.x, y, c.width, 24f), "F1  Explore", fp.ExploreFlag);
            y = FlagRow(fp, new Rect(c.x, y, c.width, 24f), "F2  Clear Threat", fp.ClearThreatFlag);
            y = FlagRow(fp, new Rect(c.x, y, c.width, 24f), "F3  Build Here", fp.BuildFlag);
            y = FlagRow(fp, new Rect(c.x, y, c.width, 24f), "F4  Extract", fp.ExtractFlag);
            y = FlagRow(fp, new Rect(c.x, y, c.width, 24f), "F5  Defend", fp.DefendFlag);

            y += 4f;
            GUI.Label(new Rect(c.x, y, 60f, 24f), "BOUNTY", _micro);
            GUI.Label(new Rect(c.x + 58f, y, 70f, 24f), $"${_loop.FlagBounty:F0}", _value);
            if (GUI.Button(new Rect(c.xMax - 58f, y + 2f, 26f, 20f), "−", _chipOff)) fp.NudgeBounty(-15f);
            if (GUI.Button(new Rect(c.xMax - 28f, y + 2f, 26f, 20f), "+", _chipOff)) fp.NudgeBounty(15f);

            y += 26f;
            GUI.Label(new Rect(c.x, y, c.width, 13f), "LMB places · click FLAG again to close", _micro);
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
            float popupH = 58f + count * 28f;
            float left = DockLeft() + 102f;
            var rect = new Rect(left, dockTop - 8f - popupH, popupW, popupH);
            _contentBottom = rect.y;
            var c = Panel(rect, "Build catalog");

            GUI.Label(new Rect(c.x, c.y, c.width, 13f), "Pick a module · LMB on open ground", _micro);
            float y = c.y + 17f;

            for (int i = 0; i < count; i++)
            {
                var b = bp.Catalog[i];
                if (b == null) continue;

                bool on = bp.SelectedIndex == i;
                bool canAfford = _loop.Resources == null || _loop.Resources.CanAfford(b.buildCost);
                var r = new Rect(c.x, y, c.width, 24f);

                if (GUI.Button(r, GUIContent.none, on ? _rowOn : _rowOff))
                {
                    _loop.SetTool(OverseerTool.Build);
                    bp.SelectBuilding(i);
                }

                GUI.Label(new Rect(r.x + 8f, r.y, 18f, r.height), BuildHotkeyLabel(i), on ? _onText : _micro);
                var nameStyle = on ? _onText : _action;
                var prevColor = nameStyle.normal.textColor;
                if (!on && !canAfford) nameStyle.normal.textColor = TextMuted;
                GUI.Label(new Rect(r.x + 26f, r.y, r.width - 110f, r.height), b.displayName, nameStyle);
                nameStyle.normal.textColor = prevColor;

                var costStyle = _microRight;
                var prevCost = costStyle.normal.textColor;
                if (!canAfford) costStyle.normal.textColor = Alarm;
                GUI.Label(new Rect(r.xMax - 92f, r.y, 86f, r.height), FormatBuildCost(b), costStyle);
                costStyle.normal.textColor = prevCost;

                y += 28f;
            }
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

            float h = 118f;
            if (mission.DeadlineEnabled) h += 26f;

            var rect = new Rect(Screen.width - M - 300f, M, 300f, h);
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
            const float cardH = 148f;
            float y0 = _contentBottom - 8f - cardH;
            var rect = new Rect(M, y0, cardW, cardH);
            var c = Panel(rect, null);
            Outline(rect, new Color(0.96f, 0.42f, 0.08f, 0.45f));

            float row = c.y;
            GUI.Label(new Rect(c.x, row, c.width, 16f), st.DisplayName, _value);
            row += 18f;

            string role = st.IsWorkshop ? "Workshop" : st.Role.ToString();
            string worker = st.HasPreferredClass ? ColonyStructure.ClassLabel(st.PreferredClass) : "—";
            GUI.Label(new Rect(c.x, row, c.width, 13f),
                $"{role} · {worker} {st.WorkerCount}/{st.WorkerSlots}", _micro);
            row += 15f;

            Bar(new Rect(c.x, row, c.width, 14f), "HP", st.Health01, HpFill);
            row += 18f;

            string workers = FormatWorkers(st);
            GUI.Label(new Rect(c.x, row, c.width, 13f), workers, _micro);
            row += 16f;

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
            else
            {
                GUI.Label(new Rect(c.x, row, c.width, 22f),
                    $"Locked to {ColonyStructure.ClassLabel(st.PreferredClass)} — flags nearby pull them.", _micro);
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
            {
                var hint = new Rect(M, _contentBottom - 8f - 24f, 340f, 24f);
                _hitRects.Add(hint);
                Fill(hint, PanelBg);
                Outline(hint, Hairline);
                GUI.Label(new Rect(hint.x + 10f, hint.y, hint.width - 16f, hint.height),
                    "Click a specialist or building · Shift+click up to 4 heroes", _micro);
                return;
            }

            int n = selected.Count;
            float avail = Screen.width - M * 2f - (n - 1) * 8f;
            float cardW = Mathf.Clamp(avail / n, 150f, 208f);
            const float cardH = 120f;
            float y = _contentBottom - 8f - cardH;

            for (int i = 0; i < n; i++)
            {
                var a = selected[i];
                if (a == null) continue;

                var rect = new Rect(M + i * (cardW + 8f), y, cardW, cardH);
                var c = Panel(rect, null);
                Outline(rect, new Color(0.96f, 0.42f, 0.08f, 0.45f));
                if (a.IsIncapacitated) Outline(rect, new Color(0.95f, 0.32f, 0.26f, 0.55f));

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
                    $"{ColonyLayout.CampusLabel(campus)} · danger {a.BodyDanger * 100f:F0}%", _micro);
                row += 16f;

                GUI.Label(new Rect(c.x, row, c.width, 14f), FormatAction(a), _action);
                row += 17f;

                Bar(new Rect(c.x, row, c.width, 14f), "HP", a.HealthNormalized, HpFill);
                row += 16f;
                Bar(new Rect(c.x, row, c.width, 14f), "FATIGUE", a.Fatigue, FatigueFill);
                row += 17f;

                GUI.Label(new Rect(c.x, row, c.width, 13f), Truncate(a.LastReason, 30), _micro);
            }
        }

        private static string FormatAction(SpecialistAgent a)
        {
            string action = a.CurrentAction switch
            {
                SpecialistAction.PursueFlag => "Working a flag",
                SpecialistAction.Rest => "Resting at inn",
                SpecialistAction.Flee => "Fleeing to inn",
                SpecialistAction.Hunt => "Hunting fauna",
                SpecialistAction.Wander => a.LastReason != null && a.LastReason.Contains("workshop")
                    ? "At workshop"
                    : "Kingdom vocation",
                _ => "Idle"
            };
            return string.IsNullOrEmpty(a.Status) ? action : $"{action} — {a.Status}";
        }

        private void DrawConstructionPanel()
        {
            if (_loop.Placer == null || _loop.Placer.Orders == null || _loop.Placer.Orders.Count == 0)
                return;

            int rows = Mathf.Min(4, _loop.Placer.Orders.Count);
            float h = 40f + rows * 26f;
            float cardReserve = (_loop.SelectedAgents != null && _loop.SelectedAgents.Count > 0) ||
                                _loop.SelectedStructure != null
                ? 176f : 36f;
            var rect = new Rect(Screen.width - M - 286f, _contentBottom - 8f - cardReserve - 8f - h, 286f, h);
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
            float right = Screen.width - M - 286f - 10f;
            float w = Mathf.Min(380f, Mathf.Max(200f, right - left));
            float x = Mathf.Clamp((Screen.width - w) * 0.5f, left, Mathf.Max(left, right - w));
            var rect = new Rect(x, M, w, 32f);
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

            Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0.02f, 0.05f, 0.03f, 0.55f));

            var rect = new Rect((Screen.width - 460f) * 0.5f, Screen.height * 0.3f, 460f, 160f);
            var c = Panel(rect, null, false);
            Fill(new Rect(rect.x, rect.y, rect.width, 2f), Good);

            var prev = _banner.normal.textColor;
            _banner.normal.textColor = Good;
            bool finale = !CampaignProgress.NextAfter(_loop.ActiveBody).HasValue;
            GUI.Label(new Rect(c.x, c.y, c.width, 26f),
                finale ? "SOLAR CONQUEST COMPLETE" : "OUTPOST SECURED", _banner);
            _banner.normal.textColor = prev;

            GUI.Label(new Rect(c.x, c.y + 32f, c.width, 16f), "Dens cleared · colony sustained · launch ready.", _body);
            GUI.Label(new Rect(c.x, c.y + 52f, c.width, 16f),
                finale
                    ? "Mars holds. Rematch this world, or oversee in sandbox."
                    : "Stage the next departure, or keep overseeing here.",
                _muted);

            if (GUI.Button(new Rect(c.x, c.yMax - 30f, 190f, 28f), "CONTINUE OVERSEEING  ·  Y", _chipOn))
            {
                _winDismissed = true;
                mission.DismissWinToSandbox();
            }

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

            Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0.10f, 0.01f, 0.01f, 0.55f));

            var rect = new Rect((Screen.width - 460f) * 0.5f, Screen.height * 0.32f, 460f, 146f);
            var c = Panel(rect, null, false);
            Fill(new Rect(rect.x, rect.y, rect.width, 2f), Alarm);

            var prev = _banner.normal.textColor;
            _banner.normal.textColor = Alarm;
            GUI.Label(new Rect(c.x, c.y, c.width, 26f),
                deadline ? "MISSION TIME EXPIRED" : "OUTPOST OVERWHELMED", _banner);
            _banner.normal.textColor = prev;

            if (deadline)
            {
                GUI.Label(new Rect(c.x, c.y + 32f, c.width, 16f), "The window to secure the outpost closed.", _body);
                GUI.Label(new Rect(c.x, c.y + 52f, c.width, 16f), "Stakes unmet — restart to try a tighter run.", _muted);
                if (GUI.Button(new Rect(c.x, c.yMax - 30f, 160f, 28f), "RESTART MISSION", _chipOn))
                    _loop.RestartMission();
            }
            else
            {
                GUI.Label(new Rect(c.x, c.y + 32f, c.width, 16f), "Every specialist is incapacitated.", _body);
                GUI.Label(new Rect(c.x, c.y + 52f, c.width, 16f), "Stalkers hold the plaza until the party is revived.", _muted);
                if (GUI.Button(new Rect(c.x, c.yMax - 30f, 160f, 28f), "REVIVE PARTY  ·  Y", _chipOn))
                    _loop.RetryParty();
            }
        }

        private void Update()
        {
            if (_loop == null) return;
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
    }
}
