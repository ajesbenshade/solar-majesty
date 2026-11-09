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

        public static CelestialBodyId Body { get; private set; } = CelestialBodyId.Earth;
        public static int Current { get; private set; } = 10007;

        public static void Ensure(
            CelestialBodyId body,
            int inspectorOverride = 0,
            bool randomizeIfMissing = false)
        {
            Body = body;
            GamePrefs.SetInt(BodyPrefsKey, (int)body);

            if (inspectorOverride != 0)
            {
                Current = inspectorOverride;
                return;
            }

            string key = SeedKey(body);
            if (GamePrefs.HasKey(key))
            {
                Current = GamePrefs.GetInt(key, 10007);
                return;
            }

            Current = randomizeIfMissing
                ? UnityEngine.Random.Range(1, int.MaxValue / 4)
                : DefaultSeed(body);
            PersistSeed();
        }

        /// <summary>Load last selected body from prefs (Earth if unset).</summary>
        public static CelestialBodyId LoadSavedBody() =>
            (CelestialBodyId)GamePrefs.GetInt(BodyPrefsKey, (int)CelestialBodyId.Earth);

        /// <summary>Point the in-memory body and seed at prefs. Does not write.</summary>
        public static void ReloadFromPrefs()
        {
            Body = LoadSavedBody();
            string key = SeedKey(Body);
            Current = GamePrefs.HasKey(key) ? GamePrefs.GetInt(key, DefaultSeed(Body)) : DefaultSeed(Body);
        }

        public static void SetBody(CelestialBodyId body)
        {
            Body = body;
            GamePrefs.SetInt(BodyPrefsKey, (int)body);
            GamePrefs.SetString(KeyPrefsKey, ""); // a Sol world: no registered world is selected
            GamePrefs.Save();
        }

        private const string KeyPrefsKey = "SM_BodyKey";

        /// <summary>
        /// Select a world registered with <see cref="CelestialBodyCatalog.Register"/> for the next
        /// boot. The body pref follows the Sol world it borrows authored content from.
        /// </summary>
        public static void SetWorld(CelestialBodyProfile world)
        {
            Body = world.Id;
            GamePrefs.SetInt(BodyPrefsKey, (int)world.Id);
            GamePrefs.SetString(KeyPrefsKey, world.Key ?? "");
            GamePrefs.Save();
        }

        /// <summary>The registered world chosen for this boot, or empty for a Sol world.</summary>
        public static string LoadSavedKey() => GamePrefs.GetString(KeyPrefsKey, "");

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
            GamePrefs.SetInt(SeedKey(Body), Current);
            GamePrefs.Save();
        }

        private static string SeedKey(CelestialBodyId body) => SeedPrefix + body;

        private static int DefaultSeed(CelestialBodyId body)
        {
            switch (body)
            {
                case CelestialBodyId.Earth: return 9001;
                case CelestialBodyId.Mars: return 20011;
                case CelestialBodyId.Belt: return 30013;
                case CelestialBodyId.Europa: return 40017;
                default: return 10007;
            }
        }
    }
}
