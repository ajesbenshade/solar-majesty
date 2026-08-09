using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Player-facing overseer HUD (IMGUI). DebugHud stays on F8 for deep scores.
    /// </summary>
    public class OverseerHud : MonoBehaviour
    {
        private GameLoop _loop;
        private bool _failLatched;
        private bool _winDismissed;
        private bool _deadlineDismissed;
        private string _toast;
        private float _toastUntil;

        public void Bind(GameLoop loop)
        {
            _loop = loop;
            _failLatched = false;
            _winDismissed = false;
            _deadlineDismissed = false;
            _toast = null;
            if (_loop != null && _loop.Economy != null)
            {
                _loop.Economy.ResupplyArrived -= OnResupply;
                _loop.Economy.ResupplyArrived += OnResupply;
            }
        }

        private void OnResupply()
        {
            _toast = "Earth resupply arrived — stockpile topped up.";
            _toastUntil = Time.unscaledTime + 4f;
            DemoAudio.PlayRetry();
        }

        private void OnGUI()
        {
            if (_loop == null) return;
            GUI.skin.label.richText = true;

            DrawTopBar();
            DrawMissionChecklist();
            DrawToast();
            DrawSpecialistCards();
            DrawConstructionLine();
            DrawWinBanner();
            DrawFailBanner();
        }

        private void DrawTopBar()
        {
            GUILayout.BeginArea(new Rect(10, 10, 560, 110), GUI.skin.box);
            GUILayout.Label("<b>SOLAR MAJESTY — Overseer</b>");
            string res = _loop.Resources != null ? _loop.Resources.DebugSummary() : "resources=n/a";
            GUILayout.Label(res);

            string flagName = "?";
            if (_loop.GetComponent<FlagPlacementInput>() is FlagPlacementInput fp && fp.SelectedFlag != null)
                flagName = fp.SelectedFlag.displayName;

            GUILayout.Label(
                $"Tool: {_loop.ActiveTool}   Flag: {flagName}   Bounty: ${_loop.FlagBounty:F0}   " +
                $"Threat: {_loop.CurrentThreatPressure:P0}");

            var mission = _loop.Mission;
            if (mission != null)
            {
                string status = mission.State switch
                {
                    MissionState.Won => "WON",
                    MissionState.Lost => "LOST",
                    _ => "ACTIVE"
                };
                GUILayout.Label($"Focus [{status}]: {mission.ObjectiveLabel}");
            }

            GUILayout.Label("G flag · B build · F1–F5 flags · +/- bounty · LMB · 1–7 buildings · F8 debug · R fatigue · Y revive");
            GUILayout.EndArea();
        }

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
            float y = 130f;
            for (int i = 0; i < _loop.Agents.Count; i++)
            {
                var a = _loop.Agents[i];
                if (a == null) continue;
                GUILayout.BeginArea(new Rect(x + i * 210f, y, 200f, 118f), GUI.skin.box);
                string down = a.IsIncapacitated ? "  [DOWN]" : "";
                GUILayout.Label($"<b>{a.Data?.displayName ?? "Specialist"}{down}</b>");
                GUILayout.Label($"{a.CurrentAction} — {a.Status}");
                DrawBar("HP", a.HealthNormalized, new Color(0.85f, 0.25f, 0.25f));
                DrawBar("Fatigue", a.Fatigue, new Color(0.3f, 0.55f, 0.95f));
                GUILayout.Label(Truncate(a.LastReason, 36));
                GUILayout.EndArea();
            }
        }

        private void DrawConstructionLine()
        {
            if (_loop.Placer == null || _loop.Placer.Orders == null || _loop.Placer.Orders.Count == 0)
                return;

            GUILayout.BeginArea(new Rect(10, Screen.height - 70, 480, 55), GUI.skin.box);
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
