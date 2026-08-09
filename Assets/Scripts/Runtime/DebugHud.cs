using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// On-screen readout for vertical-slice success criteria (specialists + threat).
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
            int stalkerCount = _loop.Stalkers != null ? _loop.Stalkers.Count : 0;
            int h = 310 + agentCount * 52 + stalkerCount * 22;
            GUILayout.BeginArea(new Rect(10, 10, 580, h), GUI.skin.box);
            GUILayout.Label("Solar Majesty — Phase 1.6 (threat pressure)");
            GUILayout.Label("Player: flags / buildings only. No unit commands.");
            GUILayout.Space(4);

            GUILayout.Label($"Tool: {_loop.ActiveTool}  (Tab / B build · G flag · Q none)");
            GUILayout.Label("Flags: F1 Explore · F2 ClearThreat · F3 Build · LMB · +/- bounty");
            GUILayout.Label($"Bounty: {_loop.FlagBounty:F0}   {_loop.Resources?.DebugSummary()}");

            // Threat pressure — what brains receive as bodyDanger
            string threatLine = _loop.Threat != null ? _loop.Threat.DebugLine() : "threat=n/a";
            GUILayout.Label($">>> {threatLine}  (pushed as bodyDanger)");
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
                GUILayout.Label("--- Dust Stalkers (dark red blobs) ---");
                for (int i = 0; i < _loop.Stalkers.Count; i++)
                {
                    var s = _loop.Stalkers[i];
                    if (s == null) continue;
                    string ag = s.IsAggro ? "AGGRO" : "wander";
                    GUILayout.Label($"  Stalker {i + 1}: {ag}  HP={s.Health01:P0}");
                }
            }
            else
            {
                GUILayout.Space(4);
                GUILayout.Label("--- No living Dust Stalkers (pressure should drop to ambient) ---");
            }

            GUILayout.Space(6);
            GUILayout.Label("Threat tests: stalker near party → high danger → Defense takes ClearThreat");
            GUILayout.Label("  Engineer (low courage) more Rest/Idle under pressure");
            GUILayout.Label("  F2 ClearThreat on stalker + Defense work → stalker dies → danger falls");
            GUILayout.Label("  R = force fatigue Rest on all");
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
