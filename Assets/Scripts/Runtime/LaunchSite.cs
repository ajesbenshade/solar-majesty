using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Departure craft marker on the landing pad once launch tech is ready.
    /// Reuses the pad's parked Colony Lifter when present, otherwise builds one.
    /// </summary>
    public static class LaunchSite
    {
        private static GameObject _craft;
        private static GameObject _beacon;
        private static bool _spawnedReadyFx;

        public static Vector3 PadWorld => ColonyLayout.CampusOrigin + new Vector3(16f, 0f, 0f);

        /// <summary>The craft on the pad (staged, or the parked lifter), for the launch camera beat. May be null.</summary>
        public static Transform Craft
        {
            get
            {
                var craft = _craft != null ? _craft : FindExistingShip(null);
                return craft != null ? craft.transform : null;
            }
        }

        public static void EnsureReady(Transform parent, bool heavyShip)
        {
            if (_craft != null)
            {
                EnsurePadBeacon(parent, _craft.transform.position);
                PulseReady(_craft.transform.position);
                return;
            }

            _craft = FindExistingShip(parent);
            if (_craft == null)
            {
                _craft = new GameObject(heavyShip ? "MarsLifter" : "LunarLifter");
                if (parent != null) _craft.transform.SetParent(parent, false);
                _craft.transform.position = PadWorld;
                // The heavier Mars-bound craft is the same design, a size up.
                _craft.transform.localScale = Vector3.one * (heavyShip ? 1.15f : 1f);
                HeroBuildingKits.BuildColonyLifter(_craft.transform);

                ColonyVisualUtility.EnsureUrpMaterials(_craft);
                ColonyVisualUtility.SnapToGround(_craft);
            }

            _craft.name = heavyShip ? "DepartureCraft_MarsShip" : "DepartureCraft_LunarRocket";
            EnsurePadBeacon(parent, _craft.transform.position);
            PulseReady(_craft.transform.position);
        }

        public static void PlayDeparture(Vector3 from)
        {
            DemoVfx.LaunchPlume(from);
            DemoVfx.ClaimRing(from, new Color(0.96f, 0.42f, 0.08f));
            // The victory stinger already played when the mission was won; departure is the
            // engines. PlayLaunch is 2D so the whole screen rumbles, not just the pad.
            DemoAudio.PlayLaunch();
        }

        public static void ClearSession()
        {
            _craft = null;
            _beacon = null;
            _spawnedReadyFx = false;
        }

        private static void EnsurePadBeacon(Transform parent, Vector3 at)
        {
            if (_beacon != null) return;
            _beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _beacon.name = "LaunchPadBeacon";
            Object.Destroy(_beacon.GetComponent<Collider>());
            if (parent != null)
                _beacon.transform.SetParent(parent, true);
            _beacon.transform.position = new Vector3(at.x, 0.06f, at.z);
            _beacon.transform.localScale = new Vector3(5.4f, 0.04f, 5.4f);
            var rend = _beacon.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                                       ?? Shader.Find("Sprites/Default"));
                Color c = new Color(0.96f, 0.42f, 0.08f, 0.85f);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
                else if (mat.HasProperty("_Color")) mat.color = c;
                rend.sharedMaterial = mat;
            }
        }

        private static void PulseReady(Vector3 at)
        {
            if (_spawnedReadyFx) return;
            _spawnedReadyFx = true;
            DemoVfx.LaunchPlume(at);
            DemoAudio.PlayLaunchReady(at);
            Debug.Log("[Launch] Departure craft staged on the pad.");
        }

        private static GameObject FindExistingShip(Transform parent)
        {
            if (parent != null)
            {
                var rends = parent.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < rends.Length; i++)
                {
                    var t = rends[i];
                    if (t == null) continue;
                    string n = t.name;
                    if (n == HeroBuildingKits.LifterObjectName ||
                        n.IndexOf("DepartureCraft", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return t.gameObject;
                }
            }

            var all = Object.FindObjectsByType<Transform>();
            for (int i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (t == null) continue;
                if (t.name == HeroBuildingKits.LifterObjectName)
                    return t.gameObject;
            }
            return null;
        }
    }
}
