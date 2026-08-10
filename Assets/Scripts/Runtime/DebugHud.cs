using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Deep score readout for playtests. Hidden by default — toggle with F8.
    /// </summary>
    public class DebugHud : MonoBehaviour
    {
        [SerializeField] private bool show;

        private GameLoop _loop;

        public void Bind(GameLoop loop) => _loop = loop;

        public void SetVisible(bool visible) => show = visible;

        public void ToggleVisible() => show = !show;

        private void OnGUI()
        {
            if (!show) return;
            if (_loop == null) _loop = FindAnyObjectByType<GameLoop>();
            if (_loop == null) return;

            int agentCount = _loop.Agents != null ? _loop.Agents.Count : 0;
            int stalkerCount = _loop.Stalkers != null ? _loop.Stalkers.Count : 0;
            int h = 310 + agentCount * 52 + stalkerCount * 22;
            GUILayout.BeginArea(new Rect(Screen.width - 590, 10, 580, h), GUI.skin.box);
            GUILayout.Label("Solar Majesty — Debug (F8 to hide)");
            GUILayout.Label("Player: flags / buildings only. No unit commands.");
            GUILayout.Space(4);

            GUILayout.Label($"Tool: {_loop.ActiveTool}  Bounty: {_loop.FlagBounty:F0}");
            GUILayout.Label(_loop.Resources?.DebugSummary() ?? "");

            string threatLine = _loop.Threat != null ? _loop.Threat.DebugLine() : "threat=n/a";
            GUILayout.Label($">>> {threatLine}  (global peak)");
            GUILayout.Label(
                $"local A={_loop.LocalThreatAt(ColonyLayout.CampusOrigin):F2}  " +
                $"B={_loop.LocalThreatAt(ColonyLayout.CampusBOrigin):F2}  " +
                $"focus={ColonyLayout.CampusLabel(_loop.FocusedCampus)}");
            GUILayout.Space(6);

            if (_loop.Agents != null && _loop.Agents.Count > 0)
            {
                GUILayout.Label("--- Specialists ---");
                for (int i = 0; i < _loop.Agents.Count; i++)
                {
                    var a = _loop.Agents[i];
                    if (a == null) continue;
                    GUILayout.Label(a.DebugLine());
                }
            }

            if (_loop.Stalkers != null && _loop.Stalkers.Count > 0)
            {
                GUILayout.Space(4);
                GUILayout.Label("--- Dust Stalkers ---");
                for (int i = 0; i < _loop.Stalkers.Count; i++)
                {
                    var s = _loop.Stalkers[i];
                    if (s == null) continue;
                    string ag = s.IsAggro ? "AGGRO" : "wander";
                    GUILayout.Label($"  Stalker {i + 1}: {ag}  HP={s.Health01:P0}");
                }
            }

            GUILayout.Space(6);
            GUILayout.Label("R = force fatigue · Y = revive when overwhelmed");
            GUILayout.EndArea();
        }
    }
}
