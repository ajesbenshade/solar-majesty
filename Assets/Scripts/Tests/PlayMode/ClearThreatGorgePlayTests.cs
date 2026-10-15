using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// Earth-scene check against a copy of a developed save. The suite skips it unless
    /// SM_GORGE_SAVE points at that copy, so a normal PlayMode run never touches it.
    /// </summary>
    public class ClearThreatGorgePlayTests
    {
        private string _saveRoot;
        private float _fastStep;
        private int _logBucket = -1;

        [UnityTest]
        [Timeout(420000)]
        public IEnumerator CopiedSave_BreaksTheFarDenOnce_AndADownedHeroRecoversAtTheInn()
        {
            string src = System.Environment.GetEnvironmentVariable("SM_GORGE_SAVE");
            if (string.IsNullOrEmpty(src) || !File.Exists(src))
                Assert.Ignore("SM_GORGE_SAVE is not set; this check runs against an explicit save copy.");

            DeveloperPrefsShield.Capture();
            _fastStep = SimSpeed.Multipliers[SimSpeed.Multipliers.Length - 1];
            _saveRoot = Path.Combine(Path.GetTempPath(), "sm-gorge-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveRoot);
            SaveSystem.DirectoryOverride = _saveRoot;
            PlaytestTelemetry.DirectoryOverride = Path.Combine(_saveRoot, "Playtest");
            File.Copy(src, Path.Combine(_saveRoot, "world0.json"), true);
            File.Copy(src, Path.Combine(_saveRoot, "slot0.json"), true);
            DemoSettings.SaveExists = true;
            DemoSettings.SetFirstHourDemo(false);
            DemoSettings.RequestBootIntoPlay();

            string outDir = System.Environment.GetEnvironmentVariable("SM_E2E_OUT");
            if (string.IsNullOrEmpty(outDir)) outDir = _saveRoot;
            Directory.CreateDirectory(outDir);
            var log = new StringBuilder();
            var pays = new System.Collections.Generic.List<int>();
            void OnLog(string condition, string stack, LogType type)
            {
                if (condition != null && condition.StartsWith("[Credits]") && condition.Contains("Clear Threat"))
                {
                    pays.Add(ParseGross(condition));
                    log.AppendLine(condition);
                }
            }
            Application.logMessageReceived += OnLog;

            try
            {
                yield return SceneManager.LoadSceneAsync("LunarOutpost_Sandbox");
                yield return null;
                var loop = Object.FindFirstObjectByType<GameLoop>();
                Assert.IsNotNull(loop, "Earth scene has a game loop");
                loop.ContinueGame();
                yield return null;
                PushFast();

                var lairs = Object.FindObjectsByType<StalkerLair>(FindObjectsSortMode.None);
                StalkerLair den = null;
                float best = float.MaxValue;
                var want = new Vector3(129f, 0f, 302f);
                for (int i = 0; i < lairs.Length; i++)
                {
                    if (lairs[i] == null || lairs[i].IsCleared) continue;
                    float d = Flat(lairs[i].transform.position, want);
                    log.AppendLine($"lair {lairs[i].transform.position.x:F0},{lairs[i].transform.position.z:F0} d={d:F0} hp={lairs[i].StructureHp:F0}");
                    if (d < best)
                    {
                        best = d;
                        den = lairs[i];
                    }
                }
                Assert.IsNotNull(den, "the save has a den near the 20,800 flag");
                Assert.Less(best, 30f, "the far den should be the one at about (129, 302)");

                float game = 0f;
                float realStart = Time.realtimeSinceStartup;
                bool shotHit = false;
                while (game < 900f && Time.realtimeSinceStartup - realStart < 240f && !den.IsCleared)
                {
                    PushFast();
                    if (!shotHit && den.StructureHp < den.StructureMaxHp - 0.5f)
                    {
                        shotHit = true;
                        Shot(Path.Combine(outDir, "mound-hit.png"), den.transform.position);
                        log.AppendLine($"HIT hp={den.StructureHp:F1} at game {game:F0}s");
                        log.AppendLine(HeroLine());
                    }
                    int bucket = Mathf.FloorToInt(game / 15f);
                    if (bucket != _logBucket)
                    {
                        _logBucket = bucket;
                        log.AppendLine($"t={game:F0} hp={den.StructureHp:F0} {HeroLine()}");
                    }
                    yield return null;
                    game += Time.deltaTime;
                }

                Shot(Path.Combine(outDir, "mound-end.png"), den.transform.position);
                log.AppendLine($"END cleared={den.IsCleared} hp={den.StructureHp:F1} pays={string.Join(",", pays)}");
                log.AppendLine(HeroLine());
                File.WriteAllText(Path.Combine(outDir, "e2e-log.txt"), log.ToString());

                Assert.IsTrue(den.IsCleared, "a claimed mech has to break the mound. " + log);
                Assert.AreEqual(1, pays.FindAll(v => v == 20800).Count, "the stacked 20,800 pays once. " + log);

                var heroes = Object.FindObjectsByType<SpecialistAgent>(FindObjectsSortMode.None);
                // A mech that stood up inside the scrap window can roll a scrap on this down, so skip it.
                var stoodUpAt = typeof(SpecialistAgent).GetField("_stoodUpAt",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                SpecialistAgent flee = null;
                for (int i = 0; i < heroes.Length; i++)
                {
                    if (heroes[i] != null && heroes[i].IsAlive && !heroes[i].IsUntargetable
                        && (float)stoodUpAt.GetValue(heroes[i]) <= Time.time - OverseerRules.ScrapWindow)
                    {
                        flee = heroes[i];
                        break;
                    }
                }
                Assert.IsNotNull(flee, "a living hero has to be able to flee home");
                float hp = flee.HealthNormalized;
                flee.ApplyDamage(5f, false);
                Assert.IsFalse(flee.IsScrapped, "the scripted down has to test recovery, not scrapping");
                Assert.IsTrue(flee.IsIncapacitated, "the scripted hit has to drop the hero");

                float recoverGame = 0f;
                float recoverStart = Time.realtimeSinceStartup;
                bool healed = false;
                while (recoverGame < 240f && Time.realtimeSinceStartup - recoverStart < 120f)
                {
                    PushFast();
                    Assert.IsTrue(flee != null && !flee.IsScrapped, "the recovering hero was destroyed or scrapped");
                    float door = Flat(flee.transform.position, ColonyLayout.InnOutpost);
                    if (!flee.IsIncapacitated && door < KingdomLife.RefugeArrive && flee.HealthNormalized > hp * 0.5f)
                    {
                        healed = true;
                        break;
                    }
                    yield return null;
                    recoverGame += Time.deltaTime;
                }
                Assert.IsTrue(flee != null && !flee.IsScrapped, "the recovering hero was destroyed or scrapped");
                Shot(Path.Combine(outDir, "inn-recover.png"), flee.transform.position);
                log.AppendLine($"FLEE pos={flee.transform.position.x:F1},{flee.transform.position.z:F1} hp={flee.HealthNormalized:F2} action={flee.CurrentAction} down={flee.IsIncapacitated}");
                File.WriteAllText(Path.Combine(outDir, "e2e-log.txt"), log.ToString());
                Assert.IsTrue(healed, "a fleeing hero has to reach the inn and recover. " + log);
            }
            finally
            {
                Application.logMessageReceived -= OnLog;
                SimSpeed.Multipliers[SimSpeed.Multipliers.Length - 1] = _fastStep;
                SimSpeed.Set(4);
                DeveloperPrefsShield.Restore();
                SaveSystem.DirectoryOverride = null;
                PlaytestTelemetry.DirectoryOverride = null;
                if (!string.IsNullOrEmpty(_saveRoot) && Directory.Exists(_saveRoot))
                    Directory.Delete(_saveRoot, true);
            }
        }


        private static void Shot(string path, Vector3 look)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                File.WriteAllText(path + ".txt", "no camera");
                return;
            }
            Vector3 prevPos = cam.transform.position;
            Quaternion prevRot = cam.transform.rotation;
            var prev = cam.targetTexture;
            var rt = new RenderTexture(1280, 720, 24);
            try
            {
                cam.transform.position = look + new Vector3(-16f, 24f, -16f);
                cam.transform.LookAt(look);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.Destroy(tex);
            }
            finally
            {
                cam.targetTexture = prev;
                cam.transform.SetPositionAndRotation(prevPos, prevRot);
                RenderTexture.active = null;
                Object.Destroy(rt);
            }
        }

        private static void PushFast()
        {
            int last = SimSpeed.Multipliers.Length - 1;
            if (SimSpeed.Index != last)
                SimSpeed.Set(last);
            Time.timeScale = SimSpeed.TimeScale;
        }

        private static int ParseGross(string line)
        {
            // Claim and status lines can mention Clear Threat without being a payout.
            // Only "Bounty N EU" counts; anything else is skipped, and a short line
            // must not throw when the marker sits at the end.
            if (string.IsNullOrEmpty(line)) return 0;
            const string mark = "Bounty ";
            int at = line.IndexOf(mark);
            if (at < 0) return 0;
            int start = at + mark.Length;
            if (start < 0 || start > line.Length) return 0;
            int eu = line.IndexOf(" EU", start);
            if (eu < start) return 0;
            int len = eu - start;
            if (len < 0 || start > line.Length - len) return 0;
            string text = line.Substring(start, len);
            text = text.Replace(",", "").Replace(".", "").Replace(" ", "")
                .Replace("\u00A0", "").Replace("\u202F", "");
            if (text.Length == 0 || !int.TryParse(text, out int gross)) return 0;
            return gross;
        }

        private static string HeroLine()
        {
            var heroes = Object.FindObjectsByType<SpecialistAgent>(FindObjectsSortMode.None);
            var bits = new List<string>();
            for (int i = 0; i < heroes.Length; i++)
            {
                var h = heroes[i];
                if (h == null) continue;
                bits.Add($"{h.name}@{h.transform.position.x:F0},{h.transform.position.z:F0}:{h.CurrentAction}");
            }
            return string.Join(" ", bits);
        }

        private static float Flat(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
