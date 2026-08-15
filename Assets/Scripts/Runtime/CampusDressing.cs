using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Phase 4 campus kit: pressurized tube cladding on the square Lego docks,
    /// shield bubbles — visuals only, not a pathing graph.
    /// HAB / Commons / pad / extractor / solar field / Defense bunker live in HeroBuildingKits.
    /// Junction turrets sit on airlock hubs (ColonyVisualUtility).
    /// Airlock hubs are panel-lined square primitives; docks stay Lego.
    /// Tube cladding spans the hub-frame → module-face joint only (flush collars).
    /// </summary>
    public static class CampusDressing
    {
        private const int MaxProps = 96;
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
                data.category == BuildingCategory.Commons)
                SpawnShieldBubble(go.transform, data.category == BuildingCategory.Commons);

            if (data.category == BuildingCategory.Commons ||
                data.category == BuildingCategory.Power ||
                data.category == BuildingCategory.Defense)
                SpawnStatusPip(go.transform, data.category);

            Vector3 origin = go.transform.position;
            Transform parent = go.transform.parent;
            SpawnApron(origin, body, parent, data.category);

            if (_count >= MaxProps) return;

            float yaw = _count * 1.618f * Mathf.PI;
            Vector3 offset = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw)) * 2.55f;
            Vector3 offsetB = new Vector3(Mathf.Cos(yaw + 2.1f), 0f, Mathf.Sin(yaw + 2.1f)) * 2.2f;

            SpawnCrate(origin + offset, body, parent);
            SpawnBarrel(origin + offsetB, body, parent);
            _count += 2;

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

            if (data.category == BuildingCategory.Commons ||
                data.category == BuildingCategory.LandingPad)
            {
                SpawnPylon(origin - offset * 0.55f, body, parent);
                SpawnBollard(origin + offset * 1.15f, body, parent);
                SpawnSpool(origin - offsetB * 0.9f, body, parent);
                _count += 3;
            }

            if (ColonyStructure.IsWorkshopCategory(data.category) ||
                data.category == BuildingCategory.Inn)
            {
                SpawnPallet(origin + offset * 0.72f, body, parent);
                SpawnSpool(origin - offsetB * 0.7f, body, parent);
                _count += 2;
            }

            if (data.category == BuildingCategory.Habitat ||
                data.category == BuildingCategory.Farm ||
                data.category == BuildingCategory.Mine ||
                data.category == BuildingCategory.RegolithCamp ||
                data.category == BuildingCategory.Mining)
            {
                SpawnBollard(origin - offset * 0.95f, body, parent);
                SpawnCone(origin + offsetB * 1.05f, body, parent);
                _count += 2;
            }

            if (data.category == BuildingCategory.Power)
            {
                SpawnSpool(origin + offset * 0.8f, body, parent);
                SpawnCone(origin - offsetB * 0.85f, body, parent);
                _count += 2;
            }

            if (data.category == BuildingCategory.Laboratory)
            {
                SpawnBollard(origin + offset * 0.88f, body, parent);
                SpawnCone(origin - offsetB * 0.92f, body, parent);
                _count += 2;
            }

            // Overview isometric: denser crates / cones on Mars only. Earth meadow stays sparse.
            if (body != null && body.Id == CelestialBodyId.Mars && _count < MaxProps - 3)
            {
                SpawnPallet(origin + offsetB * 1.25f, body, parent);
                SpawnCone(origin - offset * 1.2f, body, parent);
                _count += 2;
                if (data.category == BuildingCategory.Commons ||
                    data.category == BuildingCategory.LandingPad ||
                    data.category == BuildingCategory.Habitat)
                {
                    SpawnCrate(origin - offsetB * 1.15f, body, parent);
                    _count++;
                }
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

            Debug.Log($"[CampusDressing] tubes={spawned} pieces={pieces.Count}");
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
            // Clad the hub-frame → module-face joint only. Do not run tubes through hulls.
            // 2×2 airlock half-extent is one cell; hub orange frame sits 0.92 m from origin.
            float airlockHalf = ColonyLayout.DefaultCellSize;
            const float hubReach = 0.92f;
            const float bite = 0.08f;
            float gap = airlockHalf - hubReach;
            if (gap < 0.2f) return;
            float length = gap + bite * 2f;
            Vector3 mid = from + dir * ((hubReach + airlockHalf) * 0.5f);
            mid.y = 0.85f;
            const float dia = 1.42f;

            var tube = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tube.name = "Dress_Tube";
            tube.transform.SetParent(parent, false);
            tube.transform.position = mid;
            tube.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(90f, 0f, 0f);
            tube.transform.localScale = new Vector3(dia, length * 0.5f, dia);
            Object.Destroy(tube.GetComponent<Collider>());
            Tint(tube, new Color(0.86f, 0.87f, 0.89f), 0.18f);

            int ribs = 3;
            for (int i = 0; i < ribs; i++)
            {
                float t = (i + 0.5f) / ribs - 0.5f;
                var rib = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                rib.name = "Dress_Rib";
                rib.transform.SetParent(parent, false);
                rib.transform.position = mid + dir * (t * length * 0.72f);
                rib.transform.rotation = tube.transform.rotation;
                rib.transform.localScale = new Vector3(dia * 1.14f, 0.07f, dia * 1.14f);
                Object.Destroy(rib.GetComponent<Collider>());
                // Mockup tubes: orange structural rings on a regular cadence, carbon in between.
                bool orangeRing = i % 3 == 1;
                Tint(rib, orangeRing
                    ? new Color(0.96f, 0.42f, 0.08f)
                    : new Color(0.22f, 0.23f, 0.24f), orangeRing ? 0.14f : 0.16f);
            }

            for (int e = -1; e <= 1; e += 2)
            {
                var collar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                collar.name = "Dress_TubeCollar";
                collar.transform.SetParent(parent, false);
                collar.transform.position = mid + dir * (e * (length * 0.5f - 0.04f));
                collar.transform.rotation = tube.transform.rotation;
                collar.transform.localScale = new Vector3(dia * 1.22f, 0.06f, dia * 1.22f);
                Object.Destroy(collar.GetComponent<Collider>());
                Tint(collar, new Color(0.96f, 0.42f, 0.08f), 0.12f);
            }
        }

        private static void SpawnShieldBubble(Transform root, bool commons)
        {
            var bubble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bubble.name = "Dress_Shield";
            bubble.transform.SetParent(root, false);
            bubble.transform.localPosition = new Vector3(0f, commons ? 2.4f : 1.6f, 0f);
            bubble.transform.localScale = commons
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

        /// <summary>
        /// Packed-dust apron under modules so campus reads as flattened paths vs wild regolith.
        /// Visual only — colliders stripped.
        /// </summary>
        private static void SpawnApron(
            Vector3 origin, CelestialBodyProfile body, Transform parent, BuildingCategory cat)
        {
            bool mars = body != null && body.Id == CelestialBodyId.Mars;
            float dia = cat == BuildingCategory.Commons ? 7.2f
                : cat == BuildingCategory.LandingPad ? 6.4f
                : 4.6f;
            if (mars)
                dia *= 1.22f;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Dress_Apron";
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.position = origin + Vector3.up * 0.03f;
            go.transform.localScale = new Vector3(dia, 0.025f, dia);
            Object.Destroy(go.GetComponent<Collider>());
            Color packed = body != null
                ? Color.Lerp(body.GroundDark, body.GroundLight, 0.18f) * 0.82f
                : new Color(0.18f, 0.18f, 0.19f);
            Tint(go, packed, 0.05f);

            if (!mars) return;
            // Overview isometric: raised grey paved slab under Mars campus only.
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = "Dress_ApronSlab";
            if (parent != null) slab.transform.SetParent(parent, true);
            float side = dia * 0.72f;
            if (cat == BuildingCategory.Commons) side = dia * 0.82f;
            slab.transform.position = origin + Vector3.up * 0.045f;
            slab.transform.localScale = new Vector3(side, 0.04f, side);
            Object.Destroy(slab.GetComponent<Collider>());
            Tint(slab, new Color(0.40f, 0.41f, 0.43f), 0.12f);
        }

        /// <summary>
        /// Mockup floating status pip (star/shield language). Dressing only — not selectable.
        /// </summary>
        private static void SpawnStatusPip(Transform root, BuildingCategory cat)
        {
            bool commons = cat == BuildingCategory.Commons;
            var pip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pip.name = commons ? "Dress_StatusStar" : "Dress_StatusShield";
            pip.transform.SetParent(root, false);
            pip.transform.localPosition = new Vector3(0f, commons ? 5.85f : 3.35f, 0f);
            pip.transform.localScale = new Vector3(0.32f, 0.32f, 0.32f);
            Object.Destroy(pip.GetComponent<Collider>());
            Color glow = commons
                ? new Color(0.95f, 0.78f, 0.22f)
                : new Color(0.28f, 0.72f, 1f);
            Tint(pip, glow, 0.62f, glow * 1.6f);

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Dress_StatusRing";
            ring.transform.SetParent(pip.transform, false);
            ring.transform.localPosition = Vector3.zero;
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            ring.transform.localScale = new Vector3(1.55f, 0.08f, 1.55f);
            Object.Destroy(ring.GetComponent<Collider>());
            Tint(ring, glow, 0.45f, glow * 0.8f);
        }

        private static void SpawnCone(Vector3 world, CelestialBodyProfile body, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Dress_Cone";
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.position = world + Vector3.up * 0.28f;
            go.transform.localScale = new Vector3(0.18f, 0.26f, 0.18f);
            Object.Destroy(go.GetComponent<Collider>());
            Tint(go, body != null
                ? Color.Lerp(new Color(0.96f, 0.42f, 0.08f), body.SunColor, 0.1f)
                : new Color(0.96f, 0.42f, 0.08f), 0.22f);
            ColonyVisualUtility.SnapToGround(go);

            var cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cap.name = "Dress_ConeCap";
            cap.transform.SetParent(go.transform, false);
            cap.transform.localPosition = new Vector3(0f, 1.05f, 0f);
            cap.transform.localScale = new Vector3(0.55f, 0.35f, 0.55f);
            Object.Destroy(cap.GetComponent<Collider>());
            Tint(cap, new Color(0.92f, 0.93f, 0.94f));
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

        private static void SpawnBarrel(Vector3 world, CelestialBodyProfile body, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Dress_Barrel";
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.position = world + Vector3.up * 0.38f;
            go.transform.localScale = new Vector3(0.38f, 0.36f, 0.38f);
            Object.Destroy(go.GetComponent<Collider>());
            Tint(go, body != null
                ? Color.Lerp(new Color(0.88f, 0.90f, 0.92f), body.RockColor, 0.22f)
                : new Color(0.88f, 0.90f, 0.92f));
            ColonyVisualUtility.SnapToGround(go);

            var band = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            band.name = "Dress_BarrelBand";
            band.transform.SetParent(go.transform, false);
            band.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            band.transform.localScale = new Vector3(1.12f, 0.12f, 1.12f);
            Object.Destroy(band.GetComponent<Collider>());
            Tint(band, new Color(0.96f, 0.42f, 0.08f));
        }

        private static void SpawnSpool(Vector3 world, CelestialBodyProfile body, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Dress_Spool";
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.position = world + Vector3.up * 0.22f;
            go.transform.localScale = new Vector3(0.72f, 0.16f, 0.72f);
            go.transform.rotation = Quaternion.Euler(90f, world.z * 13f, 0f);
            Object.Destroy(go.GetComponent<Collider>());
            Tint(go, new Color(0.12f, 0.12f, 0.13f), 0.22f);
            ColonyVisualUtility.SnapToGround(go);

            var hub = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            hub.name = "Dress_SpoolHub";
            hub.transform.SetParent(go.transform, false);
            hub.transform.localPosition = Vector3.zero;
            hub.transform.localScale = new Vector3(0.42f, 1.4f, 0.42f);
            Object.Destroy(hub.GetComponent<Collider>());
            Tint(hub, new Color(0.96f, 0.42f, 0.08f));
        }

        private static void SpawnPallet(Vector3 world, CelestialBodyProfile body, Transform parent)
        {
            var deck = GameObject.CreatePrimitive(PrimitiveType.Cube);
            deck.name = "Dress_Pallet";
            if (parent != null) deck.transform.SetParent(parent, true);
            deck.transform.position = world + Vector3.up * 0.08f;
            deck.transform.localScale = new Vector3(1.05f, 0.08f, 0.72f);
            deck.transform.rotation = Quaternion.Euler(0f, world.x * 11f, 0f);
            Object.Destroy(deck.GetComponent<Collider>());
            Tint(deck, new Color(0.18f, 0.18f, 0.19f), 0.18f);

            var crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crate.name = "Dress_PalletCrate";
            crate.transform.SetParent(deck.transform, false);
            crate.transform.localPosition = new Vector3(0f, 2.8f, 0f);
            crate.transform.localScale = new Vector3(0.72f, 4.2f, 0.78f);
            Object.Destroy(crate.GetComponent<Collider>());
            Tint(crate, new Color(0.86f, 0.87f, 0.89f));
            ColonyVisualUtility.SnapToGround(deck);
        }

        private static void SpawnBollard(Vector3 world, CelestialBodyProfile body, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Dress_Bollard";
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.position = world + Vector3.up * 0.38f;
            go.transform.localScale = new Vector3(0.14f, 0.36f, 0.14f);
            Object.Destroy(go.GetComponent<Collider>());
            Tint(go, new Color(0.08f, 0.08f, 0.09f), 0.22f);
            ColonyVisualUtility.SnapToGround(go);

            var lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lamp.name = "Dress_BollardLamp";
            lamp.transform.SetParent(go.transform, false);
            lamp.transform.localPosition = new Vector3(0f, 1.05f, 0f);
            lamp.transform.localScale = new Vector3(1.15f, 0.42f, 1.15f);
            Object.Destroy(lamp.GetComponent<Collider>());
            Color glow = new Color(0.22f, 0.84f, 0.98f);
            Tint(lamp, glow, 0.55f, glow * 1.2f);
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

        /// <summary>
        /// Empty-drop claim: carbon disc, orange/cyan chevrons, bollards.
        /// Visual only — placement still uses the soft claim in BuildingPlacer.
        /// </summary>
        public static GameObject DressClaimDisc(
            Transform parent, Vector3 world, string objectName, Color accent, float diameter)
        {
            var root = new GameObject(objectName);
            if (parent != null) root.transform.SetParent(parent, true);
            root.transform.position = world;

            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "ClaimDisc";
            disc.transform.SetParent(root.transform, false);
            disc.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            disc.transform.localScale = new Vector3(diameter, 0.04f, diameter);
            Object.Destroy(disc.GetComponent<Collider>());
            Tint(disc, new Color(0.10f, 0.11f, 0.12f), 0.18f);

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "ClaimRing";
            ring.transform.SetParent(root.transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.07f, 0f);
            ring.transform.localScale = new Vector3(diameter * 1.06f, 0.02f, diameter * 1.06f);
            Object.Destroy(ring.GetComponent<Collider>());
            Tint(ring, accent, 0.32f, accent * 0.35f);

            var inner = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            inner.name = "ClaimInner";
            inner.transform.SetParent(root.transform, false);
            inner.transform.localPosition = new Vector3(0f, 0.075f, 0f);
            inner.transform.localScale = new Vector3(diameter * 0.42f, 0.018f, diameter * 0.42f);
            Object.Destroy(inner.GetComponent<Collider>());
            Tint(inner, Color.Lerp(new Color(0.88f, 0.90f, 0.93f), accent, 0.15f), 0.22f);

            for (int i = 0; i < 4; i++)
            {
                float ang = i * 90f * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang));
                var chevron = GameObject.CreatePrimitive(PrimitiveType.Cube);
                chevron.name = "ClaimChevron_" + i;
                chevron.transform.SetParent(root.transform, false);
                chevron.transform.localPosition = dir * (diameter * 0.28f) + new Vector3(0f, 0.09f, 0f);
                chevron.transform.localRotation = Quaternion.Euler(0f, i * 90f, 0f);
                chevron.transform.localScale = new Vector3(0.22f, 0.03f, diameter * 0.18f);
                Object.Destroy(chevron.GetComponent<Collider>());
                Tint(chevron, accent, 0.28f);

                var bollard = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                bollard.name = "ClaimBollard_" + i;
                bollard.transform.SetParent(root.transform, false);
                bollard.transform.localPosition = dir * (diameter * 0.48f) + new Vector3(0f, 0.42f, 0f);
                bollard.transform.localScale = new Vector3(0.16f, 0.38f, 0.16f);
                Object.Destroy(bollard.GetComponent<Collider>());
                Tint(bollard, new Color(0.08f, 0.08f, 0.09f), 0.22f);

                var lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                lamp.name = "ClaimLamp_" + i;
                lamp.transform.SetParent(root.transform, false);
                lamp.transform.localPosition = dir * (diameter * 0.48f) + new Vector3(0f, 0.82f, 0f);
                lamp.transform.localScale = new Vector3(0.18f, 0.12f, 0.18f);
                Object.Destroy(lamp.GetComponent<Collider>());
                var glow = Color.Lerp(accent, new Color(0.22f, 0.84f, 0.98f), 0.35f);
                Tint(lamp, glow, 0.55f, glow * 1.4f);
            }

            ColonyVisualUtility.SnapToGround(root);
            return root;
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
