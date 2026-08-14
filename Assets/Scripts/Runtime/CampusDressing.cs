using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Phase 4 campus kit: pressurized tube cladding on the square Lego docks,
    /// shield bubbles — visuals only, not a pathing graph.
    /// HAB / keep / pad / extractor / solar field / Defense bunker live in HeroBuildingKits.
    /// Junction turrets sit on airlock hubs (ColonyVisualUtility).
    /// </summary>
    public static class CampusDressing
    {
        private const int MaxProps = 48;
        private const int MaxTubes = 40;
        private const string TubeRootName = "CampusTubeRoot";

        private static int _count;
        private static Shader _lit;

        public static void Reset()
        {
            _count = 0;
            var existing = GameObject.Find(TubeRootName);
            if (existing != null)
            {
                existing.name = TubeRootName + "_old";
                Object.Destroy(existing);
            }
        }

        public static void DressPlaced(BuildingData data, GameObject go, CelestialBodyProfile body)
        {
            if (data == null || go == null) return;

            if (data.category == BuildingCategory.Utility)
                return;

            if (data.category == BuildingCategory.Defense ||
                data.category == BuildingCategory.Palace)
                SpawnShieldBubble(go.transform, data.category == BuildingCategory.Palace);

            if (_count >= MaxProps) return;

            Vector3 origin = go.transform.position;
            float yaw = _count * 1.618f * Mathf.PI;
            Vector3 offset = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw)) * 2.55f;
            Transform parent = go.transform.parent;

            SpawnCrate(origin + offset, body, parent);
            _count++;

            if (body != null && body.Kit == TerrainKit.IceCrust)
            {
                SpawnFrostSlab(origin + offset * 0.4f, body, parent);
                _count++;
            }
            else if (body != null && body.Kit == TerrainKit.AsteroidField)
            {
                SpawnOreChunk(origin - offset * 0.35f, body, parent);
                _count++;
            }

            if (data.category == BuildingCategory.Palace ||
                data.category == BuildingCategory.LandingPad)
            {
                SpawnPylon(origin - offset * 0.55f, body, parent);
                _count++;
            }
        }

        /// <summary>
        /// Corrugated corridor cladding spanning each airlock ↔ module dock.
        /// Dressing only — colliders stripped, NavMesh unchanged.
        /// </summary>
        public static void RefreshTubes(BuildingPlacer placer, IsoGrid grid, Transform parent)
        {
            var existing = GameObject.Find(TubeRootName);
            if (existing != null)
            {
                existing.name = TubeRootName + "_old";
                Object.Destroy(existing);
            }
            if (placer == null || grid == null) return;

            var pieces = placer.Pieces;
            if (pieces == null || pieces.Count == 0) return;

            var root = new GameObject(TubeRootName).transform;
            if (parent != null) root.SetParent(parent, false);

            int spawned = 0;
            for (int a = 0; a < pieces.Count && spawned < MaxTubes; a++)
            {
                var airlock = pieces[a];
                if (!airlock.IsAirlock) continue;
                Vector3 airCenter = PieceCenter(grid, airlock);

                for (int m = 0; m < pieces.Count && spawned < MaxTubes; m++)
                {
                    var module = pieces[m];
                    if (!module.IsModule) continue;
                    for (int f = 0; f < 4; f++)
                    {
                        var face = (BuildingPlacer.Cardinal)f;
                        if (BuildingPlacer.AirlockOriginOnModuleFace(module, face) != airlock.Origin)
                            continue;
                        Vector3 modCenter = PieceCenter(grid, module);
                        SpawnDockTube(root, airCenter, modCenter);
                        spawned++;
                        break;
                    }
                }
            }
        }

        private static Vector3 PieceCenter(IsoGrid grid, BuildingPlacer.CampusPiece piece)
        {
            Vector3 a = grid.CellToWorld(piece.Origin);
            Vector3 b = grid.CellToWorld(piece.Origin + new Vector2Int(piece.Width - 1, piece.Height - 1));
            return (a + b) * 0.5f;
        }

        private static void SpawnDockTube(Transform parent, Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            delta.y = 0f;
            float span = delta.magnitude;
            if (span < 0.4f) return;

            Vector3 dir = delta / span;
            // Cover the gap plus a bite of each hull so the campus reads as one pressurized spine.
            float length = Mathf.Clamp(span * 0.62f, 2.2f, 8.5f);
            Vector3 mid = (from + to) * 0.5f;
            mid.y = 0.92f;

            var tube = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tube.name = "Dress_Tube";
            tube.transform.SetParent(parent, false);
            tube.transform.position = mid;
            tube.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(90f, 0f, 0f);
            tube.transform.localScale = new Vector3(1.05f, length * 0.5f, 1.05f);
            Object.Destroy(tube.GetComponent<Collider>());
            Tint(tube, new Color(0.78f, 0.80f, 0.82f), 0.22f);

            int ribs = Mathf.Clamp(Mathf.RoundToInt(length / 0.85f), 3, 10);
            for (int i = 0; i < ribs; i++)
            {
                float t = (i + 0.5f) / ribs - 0.5f;
                var rib = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                rib.name = "Dress_Rib";
                rib.transform.SetParent(parent, false);
                rib.transform.position = mid + dir * (t * length);
                rib.transform.rotation = tube.transform.rotation;
                rib.transform.localScale = new Vector3(1.18f, 0.07f, 1.18f);
                Object.Destroy(rib.GetComponent<Collider>());
                Tint(rib, new Color(0.12f, 0.12f, 0.13f), 0.18f);
            }
        }

        private static void SpawnShieldBubble(Transform root, bool keep)
        {
            var bubble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bubble.name = "Dress_Shield";
            bubble.transform.SetParent(root, false);
            bubble.transform.localPosition = new Vector3(0f, keep ? 2.4f : 1.6f, 0f);
            bubble.transform.localScale = keep
                ? new Vector3(8.6f, 5.2f, 8.6f)
                : new Vector3(5.4f, 3.2f, 5.4f);
            Object.Destroy(bubble.GetComponent<Collider>());
            var rend = bubble.GetComponent<Renderer>();
            if (rend == null) return;
            var mat = NewLit("SM_DressShield");
            var c = new Color(0.35f, 0.72f, 1f, 0.16f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_Color")) mat.color = c;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.82f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.05f);
            ColonyVisualUtility.ApplyTransparent(mat);
            rend.sharedMaterial = mat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static void SpawnCrate(Vector3 world, CelestialBodyProfile body, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Dress_Crate";
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.position = world + Vector3.up * 0.28f;
            go.transform.localScale = new Vector3(0.55f, 0.42f, 0.7f);
            go.transform.rotation = Quaternion.Euler(0f, world.x * 17f, 0f);
            Object.Destroy(go.GetComponent<Collider>());
            Tint(go, body != null
                ? Color.Lerp(new Color(0.78f, 0.8f, 0.82f), body.RockColor, body.Kit == TerrainKit.AsteroidField ? 0.55f : 0.35f)
                : new Color(0.78f, 0.8f, 0.82f));
            ColonyVisualUtility.SnapToGround(go);
        }

        private static void SpawnFrostSlab(Vector3 world, CelestialBodyProfile body, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Dress_Frost";
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.position = world + Vector3.up * 0.08f;
            go.transform.localScale = new Vector3(1.15f, 0.08f, 0.85f);
            go.transform.rotation = Quaternion.Euler(0f, world.z * 11f, 0f);
            Object.Destroy(go.GetComponent<Collider>());
            Tint(go, Color.Lerp(new Color(0.78f, 0.9f, 0.96f), body != null ? body.GroundLight : Color.cyan, 0.35f));
            ColonyVisualUtility.SnapToGround(go);
        }

        private static void SpawnOreChunk(Vector3 world, CelestialBodyProfile body, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Dress_Ore";
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.position = world + Vector3.up * 0.22f;
            go.transform.localScale = new Vector3(0.48f, 0.32f, 0.42f);
            Object.Destroy(go.GetComponent<Collider>());
            Tint(go, body != null ? Color.Lerp(body.RockColor, new Color(0.55f, 0.42f, 0.28f), 0.4f) : new Color(0.4f, 0.32f, 0.22f));
            ColonyVisualUtility.SnapToGround(go);
        }

        private static void SpawnPylon(Vector3 world, CelestialBodyProfile body, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Dress_Pylon";
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.position = world + Vector3.up * 0.55f;
            go.transform.localScale = new Vector3(0.18f, 0.55f, 0.18f);
            Object.Destroy(go.GetComponent<Collider>());
            Tint(go, body != null && body.Kit == TerrainKit.IceCrust
                ? new Color(0.45f, 0.82f, 0.95f)
                : new Color(0.96f, 0.42f, 0.08f));
            ColonyVisualUtility.SnapToGround(go);

            var cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cap.name = "Beacon";
            cap.transform.SetParent(go.transform, false);
            cap.transform.localPosition = new Vector3(0f, 1.05f, 0f);
            cap.transform.localScale = new Vector3(1.4f, 0.45f, 1.4f);
            Object.Destroy(cap.GetComponent<Collider>());
            Color glow = body != null
                ? Color.Lerp(new Color(0.96f, 0.42f, 0.08f), body.SunColor, 0.25f)
                : new Color(0.96f, 0.42f, 0.08f);
            Tint(cap, glow, 0.4f, glow);
        }

        private static void Tint(GameObject go, Color c, float smooth = 0.28f, Color emission = default)
        {
            var rend = go.GetComponent<Renderer>();
            if (rend == null) return;
            var mat = NewLit(go.name);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            else if (mat.HasProperty("_Color")) mat.color = c;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smooth);
            if (emission.maxColorComponent > 0.01f && mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission);
            }
            rend.sharedMaterial = mat;
        }

        private static Material NewLit(string name)
        {
            if (_lit == null)
                _lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default");
            return new Material(_lit) { name = name };
        }
    }
}
