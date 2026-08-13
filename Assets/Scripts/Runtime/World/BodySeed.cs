using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Per-body conquest seed. Same body+seed → same craters/nodes/lairs.
    /// Advanced on RestartMission so each conquest of that world is new.
    /// </summary>
    public static class BodySeed
    {
        private const string BodyPrefsKey = "SM_CelestialBody";
        private const string SeedPrefix = "SM_BodySeed_";

        public static CelestialBodyId Body { get; private set; } = CelestialBodyId.Luna;
        public static int Current { get; private set; } = 10007;

        public static void Ensure(
            CelestialBodyId body,
            int inspectorOverride = 0,
            bool randomizeIfMissing = false)
        {
            Body = body;
            PlayerPrefs.SetInt(BodyPrefsKey, (int)body);

            if (inspectorOverride != 0)
            {
                Current = inspectorOverride;
                return;
            }

            string key = SeedKey(body);
            if (PlayerPrefs.HasKey(key))
            {
                Current = PlayerPrefs.GetInt(key, 10007);
                return;
            }

            Current = randomizeIfMissing
                ? UnityEngine.Random.Range(1, int.MaxValue / 4)
                : DefaultSeed(body);
            PersistSeed();
        }

        /// <summary>Load last selected body from prefs (Luna if unset).</summary>
        public static CelestialBodyId LoadSavedBody() =>
            (CelestialBodyId)PlayerPrefs.GetInt(BodyPrefsKey, (int)CelestialBodyId.Luna);

        public static void SetBody(CelestialBodyId body)
        {
            Body = body;
            PlayerPrefs.SetInt(BodyPrefsKey, (int)body);
            PlayerPrefs.Save();
        }

        public static void AdvanceForNextConquest()
        {
            unchecked
            {
                Current = Current * 1103515245 + 12345;
                if (Current == 0) Current = DefaultSeed(Body);
            }
            PersistSeed();
            Debug.Log($"[BodySeed] Next {Body} seed = {Current}");
        }

        public static void SetAndPersist(int seed)
        {
            Current = seed == 0 ? DefaultSeed(Body) : seed;
            PersistSeed();
        }

        private static void PersistSeed()
        {
            PlayerPrefs.SetInt(SeedKey(Body), Current);
            PlayerPrefs.Save();
        }

        private static string SeedKey(CelestialBodyId body) => SeedPrefix + body;

        private static int DefaultSeed(CelestialBodyId body) =>
            body == CelestialBodyId.Mars ? 20011 : 10007;
    }
}
