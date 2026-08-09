using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// On-screen readout for vertical-slice success criteria (all specialists).
    /// </summary>
    public class DebugHud : MonoBehaviour
    {
        [SerializeField] private bool show = true;

        private GameLoop _loop;

        public void Bind(GameLoop loop) => _loop = loop;

        private void OnGUI()
        {
            if (!show) return;
            if (_loop == null) _loop = FindFirstObjectByType<GameLoop>();
            if (_loop == null) return;

            int agentCount = _loop.Agents != null ? _loop.Agents.Count : 0;
            int h = 280 + agentCount * 52;
            GUILayout.BeginArea(new Rect(10, 10, 560, h), GUI.skin.box);
            GUILayout.Label("Solar Majesty — Phase 1.5 (3 specialists)");
            GUILayout.Label("Player: flags / buildings only. No unit commands.");
            GUILayout.Space(4);

            GUILayout.Label($"Tool: {_loop.ActiveTool}  (Tab / B build · G flag · Q none)");
            GUILayout.Label("Flags: F1 Explore · F2 ClearThreat · F3 Build · LMB · +/- bounty");
            GUILayout.Label($"Bounty: {_loop.FlagBounty:F0}   {_loop.Resources?.DebugSummary()}");
            GUILayout.Space(6);

            if (_loop.Agents != null && _loop.Agents.Count > 0)
            {
                GUILayout.Label("--- Specialists (orb: gray Idle · blue Rest · orange Pursue) ---");
                for (int i = 0; i < _loop.Agents.Count; i++)
                {
                    var a = _loop.Agents[i];
                    if (a == null) continue;
                    GUILayout.Label(a.DebugLine());
                }
            }
            else
            {
                GUILayout.Label("No specialists spawned.");
            }

            GUILayout.Space(6);
            GUILayout.Label("Verify: low far bounty → Idle | high preferred near → walk");
            GUILayout.Label("        R = force fatigue Rest | claim badge on taken flags");
            GUILayout.Label("Emergent: Explore→Scout · Build$high→Engineer · Threat→Defense");
            GUILayout.EndArea();
        }

        private void Update()
        {
            if (_loop == null) return;
            if (Input.GetKeyDown(KeyCode.R))
            {
                _loop.DebugFatigueAll(0.92f);
                Debug.Log("[Debug] Forced ALL specialists fatigue → 0.92 (expect Rest)");
            }
        }
    }
}
