#if UNITY_EDITOR
using System.IO;
using UnityEngine;

namespace SolarMajesty.EditorTools
{
    /// <summary>
    /// Shared first-hour handoff notes for Windows and macOS player zips.
    /// Friends get this file only — not DEMO.md.
    /// </summary>
    public static class PlaytestHandoff
    {
        public const string FileName = "PLAYTEST.txt";

        public static string SessionNotes =>
            "What to try (Earth, 10–20 minutes)\n" +
            "----------------------------------\n" +
            "Title is a solar-system orrery. Click Earth. Meadow drop, no starter robots.\n" +
            "Colony Commons, one airlock, and a HAB are already on the claim.\n" +
            "Press B, then 1, and dock the Engineer workshop on the open airlock face.\n" +
            "Wait until the robot is standing. You never click-to-move units.\n" +
            "Press G, choose Build, leave the bounty at $70. Watch the Engineer respond.\n" +
            "If ignored, select the flag and press + until tempted. If accepted, continue.\n" +
            "A soil creeper shows up. Press G, F5 Defend, and post it on the bug.\n" +
            "If the pole says ignored, dock a Defense workshop (key 2) and post F5 again.\n" +
            "SKIP dismisses the tutorial bar. Esc → Title → Continue brings the campus back.\n" +
            "Stop whenever you get bored. That moment is the useful data.\n" +
            "\n" +
            "Controls\n" +
            "--------\n" +
            "Esc          Pause (Resume / Settings / Title / Quit)\n" +
            "WASD         Pan camera     Q / E zoom out / in (mouse does not pan or zoom)\n" +
            "B            Build catalog  G flag catalog     Tab cycle     T research\n" +
            "1-9 / 0      Pick a building while Build is open\n" +
            "F1 Explore   F2 Clear Threat   F3 Build   F4 Extract   F5 Defend\n" +
            "LMB          Place / inspect     RMB on a flag: cancel + refund CRED\n" +
            "+ / -        Raise / lower bounty\n" +
            "P            Form a party (max 4)     [ disband\n" +
            "\n" +
            "Telemetry (please zip this folder back)\n" +
            "---------------------------------------\n" +
            "macOS:   ~/Library/Application Support/SolarMajesty/Solar Majesty/Playtest/\n" +
            "Windows: %USERPROFILE%\\AppData\\LocalLow\\SolarMajesty\\Solar Majesty\\Playtest/\n" +
            "One JSON-lines file per session (session-*.jsonl). Nothing leaves the machine on its own.\n" +
            "\n" +
            "Notes\n" +
            "-----\n" +
            "- Wallet chip is CRED (not MET). Flags escrow CRED. ICE is life support; PWR is the grid.\n" +
            "- Continue restores campus + stockpile + research + open flags + fauna + specialist HP.\n" +
            "- If the Engineer ignores a cheap Build, raise its bounty. If already accepted, continue.\n" +
            "- This build is Earth only. Dens, sustain, and launch stay on the HUD. You do not have to finish Earth.\n" +
            "- Settings → Full campaign shows the other worlds. Leave it off for this session.\n" +
            "- Greybox / blockout art. Please note crashes, unreadable UI, and \"I didn't know what to do\".\n";

        public static string WindowsReadme =>
            "Solar Majesty — Windows playtest\n" +
            "================================\n" +
            "Early overseer-loop demo (not a finished game). No Unity install needed.\n" +
            "\n" +
            "How to run\n" +
            "----------\n" +
            "1. Unzip the whole folder. Do not run the .exe from inside the zip.\n" +
            "2. Double-click SolarMajesty.exe. Keep it next to SolarMajesty_Data.\n" +
            "3. Windows SmartScreen may warn (unsigned build). More info → Run anyway.\n" +
            "4. Alt+Enter toggles fullscreen.\n" +
            "\n" +
            SessionNotes;

        public static string MacReadme =>
            "Solar Majesty — macOS playtest\n" +
            "==============================\n" +
            "Early overseer-loop demo (not a finished game). No Unity install needed.\n" +
            "\n" +
            "How to run\n" +
            "----------\n" +
            "1. Unzip the folder. Open SolarMajesty.app (right-click → Open if Gatekeeper warns).\n" +
            "2. Keep the .app next to any _Data / plugins that shipped with it.\n" +
            "\n" +
            SessionNotes;

        public static void WriteWindows(string directory)
        {
            Write(directory, WindowsReadme);
        }

        public static void WriteMac(string directory)
        {
            Write(directory, MacReadme);
        }

        private static void Write(string directory, string text)
        {
            if (string.IsNullOrEmpty(directory)) return;
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, FileName), text.Replace("\n", "\r\n"));
            Debug.Log("[Playtest] Wrote " + Path.Combine(directory, FileName));
        }
    }
}
#endif
