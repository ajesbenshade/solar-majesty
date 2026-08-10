using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Player-facing overseer HUD (IMGUI): resources, tools, build/flag menus, specialist cards.
    /// DebugHud stays on F8 for deep scores.
    /// </summary>
    public class OverseerHud : MonoBehaviour
    {
        private GameLoop _loop;
        private bool _failLatched;
        private bool _winDismissed;
        private bool _deadlineDismissed;
        private string _toast;
        private float _toastUntil;
        private GUIStyle _titleStyle;
        private GUIStyle _mutedStyle;
        private GUIStyle _chipOn;
        private GUIStyle _chipOff;
        private bool _stylesReady;
        private int _lastFocusToast = -1;

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
        }

        private void OnResupply()
        {
            _toast = "Earth resupply → Campus A pad — shared stockpile topped up.";
            _toastUntil = Time.unscaledTime + 4f;
            DemoAudio.PlayRetry();
        }

        private void EnsureStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;
            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                richText = true,
                fontSize = 15,
                fontStyle = FontStyle.Bold
            };
            _mutedStyle = new GUIStyle(GUI.skin.label)
            {
                richText = true,
                fontSize = 11,
                normal = { textColor = new Color(0.75f, 0.78f, 0.82f) }
            };
            _chipOn = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12
            };
            _chipOff = new GUIStyle(GUI.skin.button) { fontSize = 12 };
        }

        private void OnGUI()
        {
            if (_loop == null) return;
            GUI.skin.label.richText = true;
            EnsureStyles();

            DrawTopBar();
            DrawToolStrip();
            DrawFlagMenu();
            DrawBuildMenu();
            DrawMissionChecklist();
            DrawToast();
            DrawSpecialistCards();
            DrawConstructionLine();
            DrawWinBanner();
            DrawFailBanner();
        }

        private void DrawTopBar()
        {
            GUILayout.BeginArea(new Rect(10, 10, 420, 78), GUI.skin.box);
            GUILayout.Label("SOLAR MAJESTY — Overseer", _titleStyle);

            string res = _loop.Resources != null ? _loop.Resources.DebugSummary() : "resources=n/a";
            GUILayout.Label(res);

            var mission = _loop.Mission;
            if (mission != null)
            {
                string status = mission.State switch
                {
                    MissionState.Won => "WON",
                    MissionState.Lost => "LOST",
                    _ => "ACTIVE"
                };
                GUILayout.Label($"Focus [{status}]: {mission.ObjectiveLabel}", _mutedStyle);
            }

            GUILayout.EndArea();
        }

        private void DrawToolStrip()
        {
            GUILayout.BeginArea(new Rect(10, 96, 460, 62), GUI.skin.box);
            GUILayout.BeginHorizontal();

            ToolChip("Flag (G)", OverseerTool.Flag);
            ToolChip("Build (B)", OverseerTool.Build);
            ToolChip("None (Q)", OverseerTool.None);

            CampusChip("A", 0);
            CampusChip("B", 1);

            GUILayout.FlexibleSpace();
            int focus = _loop.FocusedCampus;
            GUILayout.Label(
                $"{ColonyLayout.CampusLabel(focus)} · local {_loop.FocusedLocalThreat:P0}",
                GUILayout.Width(150));
            GUILayout.EndHorizontal();
            GUILayout.Label(
                $"Threat A {_loop.LocalThreatAt(ColonyLayout.CampusOrigin):P0}  ·  " +
                $"B {_loop.LocalThreatAt(ColonyLayout.CampusBOrigin):P0}  ·  " +
                $"global {_loop.CurrentThreatPressure:P0}  ·  " +
                $"stalkers A {_loop.CountStalkersNearCampus(0)} / B {_loop.CountStalkersNearCampus(1)}  ·  F9 attract B",
                _mutedStyle);
            GUILayout.EndArea();
        }

        private void CampusChip(string label, int campusIndex)
        {
            bool on = _loop.FocusedCampus == campusIndex;
            var prev = GUI.backgroundColor;
            if (on) GUI.backgroundColor = campusIndex == 0
                ? new Color(0.45f, 0.85f, 0.55f)
                : new Color(0.55f, 0.7f, 1f);
            if (GUILayout.Button(label, on ? _chipOn : _chipOff, GUILayout.Height(26), GUILayout.Width(28)))
                _loop.FocusCampus(campusIndex);
            GUI.backgroundColor = prev;
        }

        private void ToolChip(string label, OverseerTool tool)
        {
            bool on = _loop.ActiveTool == tool;
            var style = on ? _chipOn : _chipOff;
            var prev = GUI.backgroundColor;
            if (on) GUI.backgroundColor = new Color(0.35f, 0.75f, 1f);
            if (GUILayout.Button(label, style, GUILayout.Height(26), GUILayout.Width(100)))
                _loop.SetTool(tool);
            GUI.backgroundColor = prev;
        }

        private void DrawFlagMenu()
        {
            if (_loop.ActiveTool != OverseerTool.Flag) return;
            var fp = _loop.FlagInput;
            if (fp == null) return;

            GUILayout.BeginArea(new Rect(10, 166, 280, 210), GUI.skin.box);
            GUILayout.Label("<b>Flag orders</b>");
            GUILayout.Label("Post a bounty — specialists decide.", _mutedStyle);

            FlagButton(fp, "F1  Explore", fp.ExploreFlag);
            FlagButton(fp, "F2  Clear Threat", fp.ClearThreatFlag);
            FlagButton(fp, "F3  Build Here", fp.BuildFlag);
            FlagButton(fp, "F4  Extract", fp.ExtractFlag);
            FlagButton(fp, "F5  Defend", fp.DefendFlag);

            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Bounty  ${_loop.FlagBounty:F0}", GUILayout.Width(110));
            if (GUILayout.Button("−", GUILayout.Width(28), GUILayout.Height(22)))
                fp.NudgeBounty(-15f);
            if (GUILayout.Button("+", GUILayout.Width(28), GUILayout.Height(22)))
                fp.NudgeBounty(15f);
            GUILayout.EndHorizontal();
            GUILayout.Label("LMB places selected flag", _mutedStyle);
            GUILayout.EndArea();
        }

        private void FlagButton(FlagPlacementInput fp, string label, FlagData data)
        {
            if (data == null) return;
            bool on = fp.SelectedFlag == data;
            var prev = GUI.backgroundColor;
            if (on) GUI.backgroundColor = data.bannerColor;
            if (GUILayout.Button(on ? $"▸ {label}" : label, GUILayout.Height(24)))
            {
                _loop.SetTool(OverseerTool.Flag);
                fp.SelectFlag(data);
            }
            GUI.backgroundColor = prev;
        }

        private void DrawBuildMenu()
        {
            if (_loop.ActiveTool != OverseerTool.Build) return;
            var bp = _loop.BuildInput;
            if (bp == null || bp.Catalog == null) return;

            float h = 56f + bp.Catalog.Length * 28f;
            GUILayout.BeginArea(new Rect(10, 166, 320, Mathf.Min(h, 320f)), GUI.skin.box);
            GUILayout.Label("<b>Build catalog</b>");
            GUILayout.Label("Select a module, then LMB on open ground.", _mutedStyle);

            for (int i = 0; i < bp.Catalog.Length; i++)
            {
                var b = bp.Catalog[i];
                if (b == null) continue;

                bool on = bp.SelectedIndex == i;
                bool canAfford = _loop.Resources == null || _loop.Resources.CanAfford(b.buildCost);
                string cost = FormatBuildCost(b);
                string label = $"{i + 1}. {b.displayName}  ·  {cost}";

                var prev = GUI.backgroundColor;
                if (on) GUI.backgroundColor = new Color(1f, 0.65f, 0.2f);
                else if (!canAfford) GUI.backgroundColor = new Color(0.45f, 0.35f, 0.35f);

                if (GUILayout.Button(on ? $"▸ {label}" : label, GUILayout.Height(24)))
                {
                    _loop.SetTool(OverseerTool.Build);
                    bp.SelectBuilding(i);
                }
                GUI.backgroundColor = prev;
            }

            GUILayout.EndArea();
        }

        private static string FormatBuildCost(BuildingData b)
        {
            if (b == null || b.buildCost == null || b.buildCost.Length == 0) return "free";
            var parts = new System.Text.StringBuilder();
            for (int i = 0; i < b.buildCost.Length; i++)
            {
                if (i > 0) parts.Append(" / ");
                parts.Append(b.buildCost[i].amount);
                parts.Append(' ');
                parts.Append(ShortResource(b.buildCost[i].resource));
            }
            return parts.ToString();
        }

        private static string ShortResource(ResourceId id) => id switch
        {
            ResourceId.Regolith => "Reg",
            ResourceId.WaterIce => "Ice",
            ResourceId.Metals => "Met",
            ResourceId.Power => "Pwr",
            _ => id.ToString()
        };

        private void DrawToast()
        {
            if (string.IsNullOrEmpty(_toast) || Time.unscaledTime > _toastUntil)
            {
                _toast = null;
                return;
            }

            float w = 420f;
            GUILayout.BeginArea(new Rect((Screen.width - w) * 0.5f, 12f, w, 36f), GUI.skin.box);
            GUILayout.Label(_toast);
            GUILayout.EndArea();
        }

        private void DrawMissionChecklist()
        {
            var mission = _loop.Mission;
            if (mission == null) return;

            GUILayout.BeginArea(new Rect(Screen.width - 290, 10, 280, 130), GUI.skin.box);
            GUILayout.Label("<b>Mission stakes</b>");
            GUILayout.Label(Check(mission.CombatCleared) +
                            $" Combat W{mission.Wave}: {mission.StalkersRemaining}/{mission.WaveTarget} left");
            GUILayout.Label(Check(mission.HoldComplete) +
                            $" Hold {_loop.FormatHold(mission.HoldElapsed)} / {_loop.FormatHold(mission.HoldRequired)}");
            GUILayout.Label(Check(mission.ColonyComplete) +
                            $" Build {mission.CompletedBuildings}/{mission.BuildingsRequired} complete");
            if (mission.DeadlineEnabled)
            {
                float left = Mathf.Max(0f, mission.MissionDeadline - mission.MissionElapsed);
                GUILayout.Label($"Deadline {_loop.FormatHold(left)} remaining");
            }
            if (mission.MetalsGoal > 0)
                GUILayout.Label($"   (or Metals {mission.MetalsCurrent}/{mission.MetalsGoal})");
            GUILayout.EndArea();
        }

        private void DrawSpecialistCards()
        {
            if (_loop.Agents == null) return;
            float x = 10f;
            float y = Screen.height - 140f;
            float cardW = Mathf.Min(190f, (Screen.width - 40f) / Mathf.Max(1, _loop.Agents.Count) - 8f);
            for (int i = 0; i < _loop.Agents.Count; i++)
            {
                var a = _loop.Agents[i];
                if (a == null) continue;
                int campus = ColonyLayout.NearestCampusIndex(a.transform.position);
                GUILayout.BeginArea(new Rect(x + i * (cardW + 8f), y, cardW, 128f), GUI.skin.box);
                string down = a.IsIncapacitated ? "  [DOWN]" : "";
                GUILayout.Label($"<b>{a.Data?.displayName ?? "Specialist"}{down}</b>");
                GUILayout.Label($"{ColonyLayout.CampusLabel(campus)} · danger {a.BodyDanger:P0}", _mutedStyle);
                GUILayout.Label($"{a.CurrentAction} — {a.Status}");
                DrawBar("HP", a.HealthNormalized, new Color(0.85f, 0.25f, 0.25f));
                DrawBar("Fatigue", a.Fatigue, new Color(0.3f, 0.55f, 0.95f));
                GUILayout.Label(Truncate(a.LastReason, 28), _mutedStyle);
                GUILayout.EndArea();
            }
        }

        private void DrawConstructionLine()
        {
            if (_loop.Placer == null || _loop.Placer.Orders == null || _loop.Placer.Orders.Count == 0)
                return;

            GUILayout.BeginArea(new Rect(Screen.width - 300, Screen.height - 90, 290, 75), GUI.skin.box);
            GUILayout.Label("<b>Construction</b>");
            for (int i = 0; i < _loop.Placer.Orders.Count; i++)
            {
                var o = _loop.Placer.Orders[i];
                if (o == null) continue;
                float p = o.RequiredSeconds > 0f
                    ? Mathf.Clamp01(o.ProgressSeconds / o.RequiredSeconds)
                    : 1f;
                GUILayout.Label($"{o.Data?.displayName ?? "Building"} — {p:P0}");
            }
            GUILayout.EndArea();
        }

        private void DrawWinBanner()
        {
            var mission = _loop.Mission;
            if (mission == null || !mission.IsWon || _winDismissed) return;

            var prev = GUI.color;
            GUI.color = new Color(0.05f, 0.18f, 0.1f, 0.4f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = prev;

            float w = 480f;
            float h = 130f;
            GUILayout.BeginArea(new Rect((Screen.width - w) * 0.5f, Screen.height * 0.28f, w, h), GUI.skin.box);
            GUI.color = new Color(0.45f, 1f, 0.65f);
            GUILayout.Label("<b>OUTPOST SECURED</b>");
            GUI.color = prev;
            GUILayout.Label("Combat cleared · hold survived · construction finished.");
            GUILayout.Label("Keep placing orders — or dismiss and continue the sandbox.");
            if (GUILayout.Button("Continue overseeing", GUILayout.Height(28)))
            {
                _winDismissed = true;
                mission.DismissWinToSandbox();
            }
            GUILayout.EndArea();
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

            var prev = GUI.color;
            GUI.color = new Color(0.15f, 0.02f, 0.02f, 0.45f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = prev;

            float w = 460f;
            float h = 120f;
            GUILayout.BeginArea(new Rect((Screen.width - w) * 0.5f, Screen.height * 0.32f, w, h), GUI.skin.box);
            GUI.color = new Color(1f, 0.35f, 0.3f);
            GUILayout.Label(deadline ? "<b>MISSION TIME EXPIRED</b>" : "<b>OUTPOST OVERWHELMED</b>");
            GUI.color = prev;
            if (deadline)
            {
                GUILayout.Label("The window to secure the outpost closed. Stakes unmet.");
                if (GUILayout.Button("Restart mission", GUILayout.Height(28)))
                    _loop.RestartMission();
            }
            else
            {
                GUILayout.Label("All specialists are incapacitated. Stalkers hold the plaza.");
                GUILayout.Label("Press <b>Y</b> to revive the party — or click below.");
                if (GUILayout.Button("Revive party", GUILayout.Height(28)))
                    _loop.RetryParty();
            }
            GUILayout.EndArea();
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
                _toast = $"Focus → {ColonyLayout.CampusLabel(_lastFocusToast)}";
                _toastUntil = Time.unscaledTime + 2.2f;
            }
        }

        private static string Check(bool done) => done ? "[x]" : "[ ]";

        private static void DrawBar(string label, float t01, Color fill)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(52));
            var r = GUILayoutUtility.GetRect(120, 14);
            GUI.Box(r, GUIContent.none);
            var fillRect = new Rect(r.x + 1, r.y + 1, (r.width - 2) * Mathf.Clamp01(t01), r.height - 2);
            var prev = GUI.color;
            GUI.color = fill;
            GUI.DrawTexture(fillRect, Texture2D.whiteTexture);
            GUI.color = prev;
            GUILayout.Label($"{t01:P0}", GUILayout.Width(40));
            GUILayout.EndHorizontal();
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "-";
            return s.Length <= max ? s : s.Substring(0, max - 1) + "…";
        }
    }
}
