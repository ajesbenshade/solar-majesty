using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// Session 18: all three dens were cleared but an ALPHA STALKER from the kingdom spawn table
    /// (which keeps running after the dens) held the dens gate open, so the win never came.
    /// Runs only when SM_VICTORY_SAVE points at a save copy. When SM_WON_SAVE_OUT is set the won
    /// colony is written there as a save Playtester can Continue from.
    /// </summary>
    public class AlphaRaiderVictoryPlayTests
    {
        [UnityTest]
        [Timeout(300000)]
        public IEnumerator DensCleared_WinsWithAnAlphaRaiderStillAtTheEdge()
        {
            string src = System.Environment.GetEnvironmentVariable("SM_VICTORY_SAVE");
            if (string.IsNullOrEmpty(src) || !File.Exists(src))
                Assert.Ignore("SM_VICTORY_SAVE is not set; this check runs against an explicit save copy.");

            DeveloperPrefsShield.Capture();
            string root = Path.Combine(Path.GetTempPath(), "sm-alpha-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            SaveSystem.DirectoryOverride = root;
            PlaytestTelemetry.DirectoryOverride = Path.Combine(root, "Playtest");
            File.Copy(src, Path.Combine(root, "world0.json"), true);
            File.Copy(src, Path.Combine(root, "slot0.json"), true);
            DemoSettings.SaveExists = true;
            DemoSettings.SetFirstHourDemo(false);
            DemoSettings.RequestBootIntoPlay();
            try
            {
                yield return SceneManager.LoadSceneAsync("LunarOutpost_Sandbox");
                yield return null;
                var loop = Object.FindFirstObjectByType<GameLoop>();
                Assert.IsNotNull(loop);
                loop.ContinueGame();
                for (int i = 0; i < 5; i++) yield return null;

                // Clear every den and its fauna, the way Playtester's heroes did.
                foreach (var lair in loop.World.Lairs)
                    if (lair != null && !lair.IsCleared) lair.ForceClear();
                var stalkers = new List<DustStalkerAgent>(loop.Stalkers);
                foreach (var s in stalkers)
                    if (s != null && s.IsAlive && !loop.IsKingdomSpawn(s)) s.ApplyCombatDamage(1e6f);

                // An alpha raider walks in from the colony edge (kingdom spawn table).
                var alpha = loop.SpawnFaunaAt(FaunaKind.Stalker, ColonyLayout.CampusOrigin + new Vector3(70f, 0f, 70f));
                Assert.IsNotNull(alpha, "alpha spawned");
                alpha.MakeElite(3f, 1.5f, 1.3f, 2f);
                var spawns = (Dictionary<int, List<DustStalkerAgent>>)typeof(GameLoop)
                    .GetField("_kingdomSpawns", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(loop);
                if (!spawns.TryGetValue(999, out var list)) spawns[999] = list = new List<DustStalkerAgent>();
                list.Add(alpha);
                alpha.enabled = false; // keep it standing at the edge for the check

                // The other two gates: launch staged and the treasury hold met.
                var mission = loop.Mission;
                mission.SetLaunchReady(true);
                var hold = typeof(MissionController).GetField("sustainHoldSeconds", BindingFlags.Instance | BindingFlags.NonPublic);
                typeof(MissionController).GetField("_sustainElapsed", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(mission, (float)hold.GetValue(mission) + 1000f);

                float t = 0f;
                while (!mission.IsWon && t < 10f)
                {
                    yield return null;
                    t += Time.unscaledDeltaTime;
                }

                Assert.IsTrue(alpha.IsAlive, "the alpha is still out there");
                Assert.IsTrue(mission.DensCleared, $"the alpha must not hold the dens gate (fauna={mission.StalkersRemaining})");
                Assert.IsTrue(mission.IsWon, "dens cleared + hold + launch = victory");

                string outDir = System.Environment.GetEnvironmentVariable("SM_WON_SAVE_OUT");
                if (!string.IsNullOrEmpty(outDir))
                {
                    Assert.IsTrue(loop.SaveToSlot(0), "won colony written");
                    Directory.CreateDirectory(outDir);
                    File.Copy(SaveSystem.SlotPath(0), Path.Combine(outDir, "slot0.json"), true);
                    File.Copy(SaveSystem.SlotPath(0), Path.Combine(outDir, "world0.json"), true);
                }
            }
            finally
            {
                Time.timeScale = 1f;
                SimSpeed.Set(4);
                DeveloperPrefsShield.Restore();
                SaveSystem.DirectoryOverride = null;
                PlaytestTelemetry.DirectoryOverride = null;
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }
    }
}
