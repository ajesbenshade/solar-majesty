#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// World review shutter for development players. <c>-smWorldShot &lt;dir&gt;</c> boots a body
    /// (<c>-smWorldShotBody Earth</c>), lets the drop Commons go up, then photographs the campus,
    /// a wide view, a den, a rock formation, points of interest and a mine zone, and quits.
    /// Built by <c>WorldPreviewBuild</c> under its own product name, so it never touches the
    /// player's saves or settings.
    /// </summary>
    public sealed class WorldShotHarness : MonoBehaviour
    {
        private string _dir;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            if (Application.isEditor) return;
            string dir = Arg("-smWorldShot");
            if (string.IsNullOrEmpty(dir)) return;

            var body = CelestialBodyId.Earth;
            if (Enum.TryParse(Arg("-smWorldShotBody") ?? "", true, out CelestialBodyId parsed))
                body = parsed;

            PlayerPrefs.DeleteAll();
            SaveSystem.DeleteAll();
            PlayerPrefs.SetInt(DemoSettings.FirstHourKey, 0);
            PlayerPrefs.SetInt(DemoSettings.FullscreenKey, 0);
            PlayerPrefs.SetInt(DemoSettings.TutorialKey, 1);
            PlayerPrefs.Save();
            CampaignProgress.DebugUnlockAll();
            DemoSettings.ClearSave();
            BodySeed.SetBody(body);
            BodySeed.Ensure(body, 0);
            DemoSettings.RequestBootIntoPlay();

            Application.runInBackground = true;
            var go = new GameObject("WorldShotHarness");
            DontDestroyOnLoad(go);
            go.AddComponent<WorldShotHarness>()._dir = dir;
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
            float giveUp = Time.realtimeSinceStartup + 90f;
            while ((loop == null || !loop.IsPlaying) && Time.realtimeSinceStartup < giveUp)
            {
                loop = FindFirstObjectByType<GameLoop>();
                yield return null;
            }
            if (loop == null || !loop.IsPlaying)
            {
                Debug.LogError("[WorldShot] GameLoop never reached Playing.");
                Application.Quit(1);
                yield break;
            }

            var cam = Camera.main != null ? Camera.main.GetComponent<IsometricCameraController>() : null;
            yield return Wait(6f);
            loop.SkipTutorial();
            var world = loop.World;

            yield return Look(cam, ColonyLayout.CampusOrigin, 18f, "01_campus");
            yield return Look(cam, ColonyLayout.CampusOrigin, 34f, "02_wide");
            yield return Look(cam, ColonyLayout.CampusOrigin + new Vector3(60f, 0f, 60f), 34f, "03_wide_ne");

            if (world != null)
            {
                for (int i = 0; i < world.Lairs.Count && i < 2; i++)
                {
                    var lair = world.Lairs[i];
                    if (lair == null) continue;
                    lair.MarkScouted();
                    yield return Look(cam, lair.WorldPosition, 12f, "04_den_" + i);
                }
                for (int i = 0; i < world.Zones.Count; i++)
                {
                    if (world.Zones[i].Kind != BuildZoneKind.Mine) continue;
                    yield return Look(cam, world.Zones[i].Center, 14f, "05_mine_zone");
                    break;
                }
            }

            yield return LookAtChildren(cam, "RockFormations", 2, 14f, "06_rocks_");
            yield return LookAtChildren(cam, "PointsOfInterest", 5, 12f, "07_poi_");
            yield return LookAtChildren(cam, "Forests", 1, 16f, "08_forest_");

            Debug.Log("[WorldShot] done");
            Application.Quit(0);
        }

        private IEnumerator LookAtChildren(IsometricCameraController cam, string rootName, int max, float ortho, string prefix)
        {
            var root = GameObject.Find(rootName);
            if (root == null) yield break;
            for (int i = 0; i < root.transform.childCount && i < max; i++)
                yield return Look(cam, root.transform.GetChild(i).position, ortho, prefix + i + "_" + root.transform.GetChild(i).name);
        }

        private IEnumerator Look(IsometricCameraController cam, Vector3 at, float ortho, string name)
        {
            var loop = FindFirstObjectByType<GameLoop>();
            for (int i = 0; loop != null && i < 8 && loop.TryPeekCutscene(out _); i++)
                loop.DismissCutscene();
            if (cam != null)
            {
                cam.FocusOn(at, ortho);
                cam.SnapToTarget();
            }
            yield return Wait(1.2f);
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(_dir, name + ".png");
            ScreenCapture.CaptureScreenshot(path, 1);
            yield return Wait(0.5f);
            Debug.Log("[WorldShot] " + path);
        }

        private static IEnumerator Wait(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) yield return null;
        }
    }
}
#endif
