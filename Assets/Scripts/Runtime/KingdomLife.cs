using UnityEngine;
using UnityEngine.Rendering;

namespace SolarMajesty
{
    /// <summary>
    /// Majesty-style outpost graph: inn, Commons, workshop, guardhouse, patrol ring.
    /// Specialists wander these landmarks when they are not on a bounty.
    /// </summary>
    public static class KingdomLife
    {
        public const float InnArrive = 3.2f;
        public const float InnPartyRadius = 7.5f;
        public const float HuntRange = 3.4f;

        public static Vector3 Commons(int campus) => ColonyLayout.CampusOriginFor(campus);

        public static Vector3 Plaza(int campus) =>
            campus <= 0 ? ColonyLayout.PartySpawn : ColonyLayout.PartySpawnB;

        public static Vector3 Inn(int campus) => ColonyLayout.InnOutpost;

        public static Vector3 InnNear(Vector3 world) => ColonyLayout.InnOutpost;

        /// <summary>
        /// Rest / flee beacon. Campus B robots use the outpost plaza once claimed;
        /// otherwise everyone rallies at the Campus A inn disc.
        /// </summary>
        public static Vector3 RestNear(Vector3 world, bool outpostClaimed)
        {
            if (outpostClaimed && ColonyLayout.NearestCampusIndex(world) == 1)
                return ColonyLayout.PartySpawnB;
            return ColonyLayout.InnOutpost;
        }

        public static bool AtRest(Vector3 world, bool outpostClaimed) =>
            Flat(world, RestNear(world, outpostClaimed)) < InnArrive;

        public static Vector3 Workshop(int campus) =>
            campus <= 0
                ? ColonyLayout.CampusOrigin + new Vector3(10f, 0f, 12f)
                : ColonyLayout.CampusBOrigin + new Vector3(0f, 0f, 9f);

        public static Vector3 Guardhouse(int campus) =>
            campus <= 0
                ? ColonyLayout.CampusOrigin + new Vector3(0f, 0f, 12f)
                : ColonyLayout.CampusBOrigin + new Vector3(0f, 0f, 0f);

        public static Vector3 Lab(int campus) =>
            campus <= 0
                ? ColonyLayout.CampusOrigin + new Vector3(-21f, 0f, 0f)
                : ColonyLayout.CampusBOrigin + new Vector3(-10.5f, 0f, 0f);

        public static Vector3 Pad(int campus) =>
            campus <= 0
                ? ColonyLayout.CampusOrigin + new Vector3(16f, 0f, 0f)
                : ColonyLayout.CampusBOrigin + new Vector3(6f, 0f, -4f);

        public static bool AtInn(Vector3 world) =>
            Flat(world, InnNear(world)) < InnArrive;

        public static bool AtInnParty(Vector3 world) =>
            Flat(world, InnNear(world)) < InnPartyRadius;

        public static Vector3 VocationAnchor(
            SpecialistClass cls,
            Vector3 from,
            Vector3? construction,
            Vector3? resourceNode,
            int salt)
        {
            int campus = ColonyLayout.NearestCampusIndex(from);
            switch (cls)
            {
                case SpecialistClass.EngineerBot:
                    if (construction.HasValue)
                        return construction.Value;
                    return Pick(from, salt, Workshop(campus), Commons(campus), Pad(campus));

                case SpecialistClass.DefenseMech:
                    return PatrolPost(campus, salt);

                case SpecialistClass.Medic:
                    return InnNear(from);

                case SpecialistClass.CourierBot:
                    return Pick(from, salt, Pad(campus), Plaza(campus), Frontier(from, salt));

                case SpecialistClass.TerraformerBot:
                    if (resourceNode.HasValue)
                        return resourceNode.Value;
                    return Pick(from, salt, Pad(campus), Lab(campus), Plaza(campus));

                case SpecialistClass.GeologistBot:
                    if (resourceNode.HasValue)
                        return resourceNode.Value;
                    return Pick(from, salt, Workshop(campus), Pad(campus), Plaza(campus));

                case SpecialistClass.SentinelMech:
                    return PatrolPost(campus, salt);

                default:
                    if (resourceNode.HasValue)
                        return resourceNode.Value;
                    return Pick(from, salt, Lab(campus), Pad(campus), Plaza(1 - campus), Frontier(from, salt));
            }
        }

        public static Vector3 PatrolPost(int campus, int salt)
        {
            Vector3 origin = ColonyLayout.CampusOriginFor(campus);
            float radius = campus <= 0 ? 20f : 14f;
            int count = campus <= 0 ? 6 : 4;
            int idx = Mod(Mathf.FloorToInt(Time.time / 9f) + salt, count);
            float ang = (Mathf.PI * 2f * idx) / count + 0.35f;
            return origin + new Vector3(Mathf.Cos(ang) * radius, 0f, Mathf.Sin(ang) * radius);
        }

        public static void Dress(Transform parent, bool emptyStart = false)
        {
            var root = new GameObject("KingdomLife").transform;
            if (parent != null) root.SetParent(parent, false);

            // Rest / party rally still use InnOutpost coords even without a mesh.
            Marker(root, Inn(0), "Inn_RestBeacon", new Color(0.96f, 0.42f, 0.08f), emptyStart ? 0.55f : 1.15f);
            Marker(root, ColonyLayout.PartySpawnB, "Outpost_RestBeacon",
                new Color(0.25f, 0.85f, 0.92f), 0.5f);

            if (emptyStart) return;

            Marker(root, Guardhouse(0), "Guard_A", new Color(0.72f, 0.16f, 0.14f));
            Marker(root, Workshop(0), "Workshop_A", new Color(0.55f, 0.58f, 0.62f));
            Marker(root, Workshop(1), "Workshop_B", new Color(0.55f, 0.58f, 0.62f));
            Marker(root, Plaza(0), "Plaza_A", new Color(0.82f, 0.84f, 0.86f));

            for (int i = 0; i < 6; i++)
            {
                Vector3 origin = ColonyLayout.CampusOrigin;
                float ang = (Mathf.PI * 2f * i) / 6f + 0.35f;
                Vector3 pos = origin + new Vector3(Mathf.Cos(ang) * 20f, 0f, Mathf.Sin(ang) * 20f);
                Pylon(root, pos, $"Patrol_A_{i}");
            }

            for (int i = 0; i < 4; i++)
            {
                Vector3 origin = ColonyLayout.CampusBOrigin;
                float ang = (Mathf.PI * 2f * i) / 4f;
                Vector3 pos = origin + new Vector3(Mathf.Cos(ang) * 14f, 0f, Mathf.Sin(ang) * 14f);
                Pylon(root, pos, $"Patrol_B_{i}");
            }
        }

        private static Vector3 Frontier(Vector3 from, int salt)
        {
            Vector3 campus = ColonyLayout.CampusOriginFor(ColonyLayout.NearestCampusIndex(from));
            Vector3 dir = from - campus;
            if (dir.sqrMagnitude < 4f)
            {
                float ang = (salt % 8) * 0.785f;
                dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
            }
            dir.y = 0f;
            dir.Normalize();
            Vector3 p = campus + dir * 28f;
            p.x = Mathf.Clamp(p.x, 8f, 370f);
            p.z = Mathf.Clamp(p.z, 8f, 370f);
            return p;
        }

        private static Vector3 Pick(Vector3 from, int salt, params Vector3[] opts)
        {
            if (opts == null || opts.Length == 0) return from;
            int idx = Mod(Mathf.FloorToInt(Time.time / 11f) + salt, opts.Length);
            Vector3 chosen = opts[idx];
            if (Flat(from, chosen) < 1.2f)
                chosen = opts[Mod(idx + 1, opts.Length)];
            return chosen;
        }

        private static void Marker(Transform parent, Vector3 pos, string name, Color accent, float diameter = 1.15f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos + Vector3.up * 0.12f;
            go.transform.localScale = new Vector3(diameter, 0.08f, diameter);
            Object.Destroy(go.GetComponent<Collider>());
            Tint(go, accent, 0.18f);
            ColonyVisualUtility.SnapToGround(go);
        }

        private static void Pylon(Transform parent, Vector3 pos, string name)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos + Vector3.up * 0.55f;
            go.transform.localScale = new Vector3(0.22f, 0.55f, 0.22f);
            Object.Destroy(go.GetComponent<Collider>());
            Tint(go, new Color(0.08f, 0.08f, 0.09f), 0.12f);

            var cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cap.name = "Beacon";
            cap.transform.SetParent(go.transform, false);
            cap.transform.localPosition = new Vector3(0f, 1.05f, 0f);
            cap.transform.localScale = new Vector3(0.55f, 0.22f, 0.55f);
            Object.Destroy(cap.GetComponent<Collider>());
            Tint(cap, new Color(0.95f, 0.42f, 0.08f), 0.4f);
            ColonyVisualUtility.SnapToGround(go);
        }

        private static void Tint(GameObject go, Color c, float smooth)
        {
            var rend = go.GetComponent<Renderer>();
            if (rend == null) return;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                                   ?? Shader.Find("Sprites/Default"))
            {
                name = "SM_Kingdom_" + go.name
            };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smooth);
            if (c.r > 0.8f && c.g < 0.5f && mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", c * 0.6f);
            }
            rend.sharedMaterial = mat;
            rend.shadowCastingMode = ShadowCastingMode.Off;
        }

        private static float Flat(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static int Mod(int v, int m)
        {
            int r = v % m;
            return r < 0 ? r + m : r;
        }
    }
}
