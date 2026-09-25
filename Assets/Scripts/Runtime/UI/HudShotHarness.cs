#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// HUD review shutter for development players. <c>-smHudShot &lt;dir&gt;</c> boots a body
    /// (<c>-smHudShotBody Mars</c>), stamps the still campus, walks the HUD through its main
    /// states, writes one PNG per state, then quits. Built by <c>HudPreviewBuild</c> under its own
    /// product name so the run never touches the player's saves or settings.
    /// </summary>
    public sealed class HudShotHarness : MonoBehaviour
    {
        private string _dir;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            if (Application.isEditor) return;
            string dir = Arg("-smHudShot");
            if (string.IsNullOrEmpty(dir)) return;

            var body = CelestialBodyId.Mars;
            if (Enum.TryParse(Arg("-smHudShotBody") ?? "", true, out CelestialBodyId parsed))
                body = parsed;

            PlayerPrefs.SetInt(DemoSettings.FirstHourKey, body == CelestialBodyId.Earth ? 1 : 0);
            PlayerPrefs.SetInt(DemoSettings.FullscreenKey, 0);
            PlayerPrefs.SetInt(DemoSettings.TutorialKey, 0);
            PlayerPrefs.Save();
            CampaignProgress.DebugUnlockAll();
            DemoSettings.ClearSave();
            BodySeed.SetBody(body);
            BodySeed.Ensure(body, 0);
            DemoSettings.RequestBootIntoPlay();

            // Launched from a shell the window never takes focus; a paused player never shoots.
            Application.runInBackground = true;
            var go = new GameObject("HudShotHarness");
            DontDestroyOnLoad(go);
            go.AddComponent<HudShotHarness>()._dir = dir;
        }

        private static string Arg(string flag)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }

        private IEnumerator Start()
        {
            Directory.CreateDirectory(_dir);
            GameLoop loop = null;
            float giveUp = Time.realtimeSinceStartup + 60f;
            while ((loop == null || !loop.IsPlaying) && Time.realtimeSinceStartup < giveUp)
            {
                loop = FindFirstObjectByType<GameLoop>();
                yield return null;
            }
            if (loop == null || !loop.IsPlaying)
            {
                Debug.LogError("[HudShot] GameLoop never reached Playing.");
                Application.Quit(1);
                yield break;
            }

            yield return Wait(4f);
            StillCaptureHold.Arm();
            loop.PrepareStillCaptureWorld();
            if (!loop.StampPhase4StillCampus()) loop.StampPhase4StillCampus();
            loop.PrepareStillCaptureWorld();
            StillCaptureHold.Disarm();
            yield return Wait(3f);
            loop.SnapStillCampusCamera((float)Screen.width / Screen.height);
            yield return Wait(2f);

            var hud = FindFirstObjectByType<OverseerHud>();
            yield return Shot("01_tutorial");

            loop.SkipTutorial();
            hud?.Notify("Trade ship landed — caravan gold waits in the Market till for a collector.", 30f);
            loop.Alerts?.Push("hudshot_warn", "Dens stirring east of the pad", AlertSeverity.Warning, Time.unscaledTime, loop.transform.position);
            loop.Alerts?.Push("hudshot_crit", "Habitat hull breach — send a repair crew", AlertSeverity.Critical, Time.unscaledTime, loop.transform.position);
            yield return Wait(0.5f);
            yield return Shot("02_play");
            hud?.ClearToast();

            loop.ToggleTool(OverseerTool.Build);
            yield return Shot("03_build");
            loop.ToggleTool(OverseerTool.Build);

            loop.ToggleTool(OverseerTool.Flag);
            yield return Shot("04_flag");
            loop.ToggleTool(OverseerTool.Flag);

            var agents = loop.Agents;
            for (int i = 0; agents != null && i < agents.Count; i++)
            {
                if (agents[i] == null || !agents[i].IsAlive) continue;
                loop.SelectOnly(agents[i]);
                yield return Shot("05_agent");
                loop.ClearSelection();
                break;
            }

            var structures = FindObjectsByType<ColonyStructure>(FindObjectsSortMode.None);
            ColonyStructure pick = null;
            for (int i = 0; i < structures.Length && pick == null; i++)
                if (structures[i] != null && structures[i].IsWorkshop) pick = structures[i];
            if (pick == null && structures.Length > 0) pick = structures[0];
            if (pick != null)
            {
                loop.SelectStructure(pick);
                yield return Shot("06_building");
                loop.ClearStructureSelection();
            }

            // Conversation (needs -narrator and a running local model): a real two-line exchange.
            if (hud != null && HeroNarrator.Enabled)
            {
                SpecialistAgent hero = FirstHero(loop);
                for (int i = 0; hero == null && i < structures.Length; i++)
                {
                    if (structures[i] == null || !structures[i].IsWorkshop) continue;
                    if (loop.TryFabricateRobot(structures[i], announce: false)) hero = FirstHero(loop);
                }
                if (hero == null) hero = SpawnEngineer(loop);
                if (hero != null)
                {
                    yield return Wait(1f);
                    loop.SelectOnly(hero);
                    loop.GlanceAt(hero.transform.position, force: true);
                    hud.OpenChat(hero);
                    float online = Time.realtimeSinceStartup + 20f;
                    while (!HeroNarrator.ChatOnline && Time.realtimeSinceStartup < online) yield return null;
                    yield return Wait(1.5f);
                    yield return Shot("11_chat_open");

                    yield return Talk(hero, "Hey, how's the work treating you?");
                    yield return Shot("12_chat_reply");
                    HeroNarrator.Say(hero, "Would you clear the den east of the pad if I raised the bounty?");
                    yield return new WaitForEndOfFrame();
                    ScreenCapture.CaptureScreenshot(Path.Combine(_dir, "13_chat_waiting.png"), 1);
                    yield return WaitReply();
                    yield return Shot("14_chat_second");
                    hud.CloseChat();
                    loop.ClearSelection();
                }
                else Debug.LogWarning("[HudShot] no hero to talk to");
            }

            if (hud != null)
            {
                hud.ToggleTechPanel();
                yield return Shot("07_tech");
                hud.ToggleTechPanel();
            }

            loop.TogglePause();
            yield return Shot("08_pause");
            loop.OpenSettings();
            yield return Shot("09_settings");
            loop.CloseSettings();
            loop.ResumePlay();

            loop.ReturnToTitle();
            yield return Wait(3f);
            yield return Shot("10_title");

            Debug.Log($"[HudShot] Wrote stills to {_dir}");
            Application.Quit(0);
        }

        private static SpecialistAgent FirstHero(GameLoop loop)
        {
            var agents = loop.Agents;
            for (int i = 0; agents != null && i < agents.Count; i++)
                if (agents[i] != null && agents[i].IsAlive) return agents[i];
            return null;
        }

        /// <summary>The still campus has no workshops; stand one Engineer up through the loop's own spawner.</summary>
        private static SpecialistAgent SpawnEngineer(GameLoop loop)
        {
            const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
            var dataFor = typeof(GameLoop).GetMethod("DataForClass", Private);
            var spawn = typeof(GameLoop).GetMethod("SpawnOne", Private);
            if (dataFor == null || spawn == null) return null;
            var data = dataFor.Invoke(loop, new object[] { SpecialistClass.EngineerBot });
            if (data == null) return null;
            var pos = ColonyLayout.CampusOrigin + new Vector3(7f, 0f, 5f);
            return spawn.Invoke(loop, new object[] { data, pos, new Color(1f, 0.55f, 0.15f) }) as SpecialistAgent;
        }

        private static IEnumerator Talk(SpecialistAgent hero, string line)
        {
            if (!HeroNarrator.Say(hero, line)) Debug.LogWarning("[HudShot] chat send refused");
            yield return WaitReply();
        }

        private static IEnumerator WaitReply()
        {
            float giveUp = Time.realtimeSinceStartup + 25f;
            while (HeroNarrator.Replying && Time.realtimeSinceStartup < giveUp) yield return null;
        }

        private static IEnumerator Wait(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) yield return null;
        }

        private IEnumerator Shot(string name)
        {
            yield return Wait(0.6f);
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(_dir, name + ".png");
            ScreenCapture.CaptureScreenshot(path, 1);
            yield return Wait(0.4f);
            Debug.Log($"[HudShot] {path}");
        }
    }
}
#endif
