using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Departure craft marker on the landing pad once launch tech is ready.
    /// Reuses the Phase 4 parked pad ship (Dress_Starship) when present.
    /// </summary>
    public static class LaunchSite
    {
        private static GameObject _craft;
        private static GameObject _beacon;
        private static bool _spawnedReadyFx;

        public static Vector3 PadWorld => ColonyLayout.CampusOrigin + new Vector3(16f, 0f, 0f);

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
                Vector3 pad = PadWorld;
                GameObject prefab = BuildingVisualCatalog.LoadStarship();
                if (prefab != null)
                {
                    _craft = ColonyVisualUtility.InstantiateOriented(prefab, pad, parent, 0f);
                    float scale = heavyShip ? ColonyLayout.ShipScale * 1.15f : ColonyLayout.ShipScale;
                    _craft.transform.localScale = Vector3.one * scale;
                }
                else
                {
                    _craft = new GameObject(heavyShip ? "MarsShip" : "LunarRocket");
                    if (parent != null) _craft.transform.SetParent(parent, false);
                    _craft.transform.position = pad;
                    BuildStack(_craft.transform, heavyShip);
                }

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
            DemoAudio.PlayVictory();
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
            DemoAudio.PlayClaim();
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
                    if (n.IndexOf("Starship", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n.IndexOf("DepartureCraft", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return t.gameObject;
                }
            }

            var all = Object.FindObjectsByType<Transform>();
            for (int i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (t == null) continue;
                if (t.name.IndexOf("Starship", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return t.gameObject;
            }
            return null;
        }

        private static void BuildStack(Transform root, bool heavy)
        {
            float h = heavy ? 4.2f : 3.2f;
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Fuselage";
            body.transform.SetParent(root, false);
            body.transform.localPosition = new Vector3(0f, h * 0.45f, 0f);
            body.transform.localScale = new Vector3(heavy ? 1.1f : 0.85f, h * 0.5f, heavy ? 1.1f : 0.85f);
            Object.Destroy(body.GetComponent<Collider>());
            Tint(body, new Color(0.86f, 0.88f, 0.9f));

            var nose = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            nose.name = "Nose";
            nose.transform.SetParent(root, false);
            nose.transform.localPosition = new Vector3(0f, h * 0.95f, 0f);
            nose.transform.localScale = Vector3.one * (heavy ? 0.95f : 0.75f);
            Object.Destroy(nose.GetComponent<Collider>());
            Tint(nose, new Color(0.96f, 0.42f, 0.08f));

            var fin = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fin.name = "Fin";
            fin.transform.SetParent(root, false);
            fin.transform.localPosition = new Vector3(0.55f, 0.55f, 0f);
            fin.transform.localScale = new Vector3(0.12f, 0.9f, 0.55f);
            Object.Destroy(fin.GetComponent<Collider>());
            Tint(fin, new Color(0.08f, 0.08f, 0.09f));
        }

        private static void Tint(GameObject go, Color c)
        {
            var rend = go.GetComponent<Renderer>();
            if (rend == null) return;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                                   ?? Shader.Find("Sprites/Default"));
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            else if (mat.HasProperty("_Color")) mat.color = c;
            rend.sharedMaterial = mat;
        }
    }
}
