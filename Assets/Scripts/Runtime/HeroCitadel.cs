using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Colony Commons as the AI core (canon: the player is the central AI). v2 (#93 follow-up):
    /// reads as a machine campus, not a castle. Four open lattice relay pylons (thin carbon struts,
    /// ceramic heads, cyan beacons) stand at the corners of a low ceramic panel ring with a portal
    /// gate on the front (-Z) face. The centre is a tapered processing spire of stacked white
    /// ceramic server tiers with radiator fins, a glowing cyan core band and core window, cable
    /// trunks, and a slim comms spire.
    /// Big masses keep their historic names (CommonsTower_*, CommonsKeep, CommonsDome, Cupola*...)
    /// because construction stages, tests and dressing look them up; only their shapes changed.
    /// Everything else goes through <see cref="DetailBatch"/>, a handful of merged meshes.
    /// The play camera sits at yaw 45°, so the -Z and -X faces are what the player sees first.
    /// </summary>
    public static partial class HeroBuildingKits
    {
        // Heights (m above ground).
        private const float CitPlinth = 0.4f;
        private const float CitWallH = 2.1f;              // low panel ring (was 3.2 m curtain wall)
        private const float CitTowerH = 5.6f;             // relay pylon mast height
        private const float CitKeepH = 6.6f;              // top of the third server tier
        private const float CitTier1 = CitKeepH * 0.45f;  // bottom server tier height
        private const float CitTier2Top = CitKeepH * 0.8f; // top of the second tier, above the plinth

        /// <summary>Top of the spire cap, where the crown comms and beacon mount.</summary>
        public const float CitadelCrownY = CitPlinth + CitKeepH + 4.55f;

        public static void BuildCommons(Transform root, float w, float d, Color hull)
        {
            float H = Mathf.Min(w, d) * 0.5f;          // half extent (9 m at 12 cells)
            float T = H - 1.7f;                          // corner pylon centres
            float kw = H * 0.78f, kd = H * 0.66f;        // bottom tier footprint
            float kz = H * 0.08f;                        // spire sits back to leave a front court
            float keepTop = CitPlinth + CitKeepH;
            float y1 = CitPlinth + CitTier1;             // top of tier 1
            float y2 = CitPlinth + CitTier2Top;          // top of tier 2

            // ---------------------------------------------------------------- big masses
            Prim(root, "CommonsPlinth", PrimitiveType.Cube,
                new Vector3(0f, CitPlinth * 0.5f, 0f), new Vector3(H * 2f - 0.6f, CitPlinth, H * 2f - 0.6f), Carbon);
            Prim(root, "CommonsMech", PrimitiveType.Cube,
                new Vector3(0f, CitPlinth + 0.02f, 0f), new Vector3(T * 2f - 1f, 0.04f, T * 2f - 1f), PadDeck);

            // Relay pylons: slim carbon mast, carbon footing, ceramic head. Lattice struts are detail.
            for (int i = 0; i < 4; i++)
            {
                float sx = i % 2 == 0 ? -1f : 1f, sz = i < 2 ? -1f : 1f;
                var c = new Vector3(sx * T, 0f, sz * T);
                Prim(root, "CommonsTower_" + i, PrimitiveType.Cylinder,
                    c + Vector3.up * (CitPlinth + CitTowerH * 0.5f), new Vector3(0.55f, CitTowerH * 0.5f, 0.55f), Carbon);
                Prim(root, "CommonsTowerCrown_" + i, PrimitiveType.Cube,
                    c + Vector3.up * (CitPlinth + CitTowerH + 0.22f), new Vector3(1.3f, 0.45f, 1.3f), hull);
                Prim(root, "CommonsTowerBase_" + i, PrimitiveType.Cylinder,
                    c + Vector3.up * (CitPlinth + 0.25f), new Vector3(1.8f, 0.25f, 1.8f), Carbon);
            }

            // Low ceramic panel ring between the pylons; the front (-Z) run leaves a gate gap.
            float span = T * 2f - 3f;
            float wallY = CitPlinth + CitWallH * 0.5f;
            Prim(root, "CommonsWall_Back", PrimitiveType.Cube, new Vector3(0f, wallY, T), new Vector3(span, CitWallH, 1f), hull);
            Prim(root, "CommonsWall_Left", PrimitiveType.Cube, new Vector3(-T, wallY, 0f), new Vector3(1f, CitWallH, span), hull);
            Prim(root, "CommonsWall_Right", PrimitiveType.Cube, new Vector3(T, wallY, 0f), new Vector3(1f, CitWallH, span), hull);
            float gateHalf = 1.7f;
            float frontSeg = (span - gateHalf * 2f) * 0.5f;
            float frontX = gateHalf + frontSeg * 0.5f;
            Prim(root, "CommonsWall_FrontL", PrimitiveType.Cube, new Vector3(-frontX, wallY, -T), new Vector3(frontSeg, CitWallH, 1f), hull);
            Prim(root, "CommonsWall_FrontR", PrimitiveType.Cube, new Vector3(frontX, wallY, -T), new Vector3(frontSeg, CitWallH, 1f), hull);

            // Portal gate: two ceramic jambs and a lintel carrying the orange stripe.
            for (int s = -1; s <= 1; s += 2)
                Prim(root, "CommonsGatePylon_" + (s < 0 ? "L" : "R"), PrimitiveType.Cube,
                    new Vector3(s * (gateHalf + 0.55f), CitPlinth + 1.6f, -T - 0.2f), new Vector3(1.1f, 3.2f, 1.5f), hull);
            Prim(root, "CommonsGateLintel", PrimitiveType.Cube,
                new Vector3(0f, CitPlinth + 2.9f, -T - 0.2f), new Vector3(gateHalf * 2f + 0.2f, 0.8f, 1.5f), hull);
            Prim(root, "CommonsStripe", PrimitiveType.Cube,
                new Vector3(0f, CitPlinth + 2.9f, -T - 0.98f), new Vector3(gateHalf * 2f + 2.2f, 0.14f, 0.06f), Orange);

            // Processing spire: tier 1 (CommonsKeep), tier 2 (CommonsDome), tier 3 (detail), comms spire.
            Prim(root, "CommonsKeep", PrimitiveType.Cube,
                new Vector3(0f, CitPlinth + CitTier1 * 0.5f, kz), new Vector3(kw, CitTier1, kd), hull);
            Prim(root, "CommonsKeepCornice", PrimitiveType.Cube,
                new Vector3(0f, y1 + 0.08f, kz), new Vector3(kw + 0.3f, 0.16f, kd + 0.3f), Graphite);
            Prim(root, "CommonsDome", PrimitiveType.Cube,
                new Vector3(0f, (y1 + 0.16f + y2) * 0.5f, kz), new Vector3(kw * 0.74f, y2 - y1 - 0.16f, kd * 0.74f), hull);
            Prim(root, "CommonsTierBand", PrimitiveType.Cube,
                new Vector3(0f, y2 + 0.05f, kz), new Vector3(kw * 0.74f + 0.16f, 0.1f, kd * 0.74f + 0.16f), Orange);
            Prim(root, "CommonsRoofDeck", PrimitiveType.Cube,
                new Vector3(0f, keepTop + 0.06f, kz), new Vector3(kw * 0.56f, 0.12f, kd * 0.56f), Graphite);
            Prim(root, "CommonsCupolaLo", PrimitiveType.Cylinder,
                new Vector3(0f, keepTop + 1.26f, kz), new Vector3(1.5f, 1.14f, 1.5f), White);
            Prim(root, "Dress_CommonsCupolaBand", PrimitiveType.Cylinder,
                new Vector3(0f, keepTop + 2.45f, kz), new Vector3(1.58f, 0.05f, 1.58f), Orange);
            Prim(root, "CommonsCupolaHi", PrimitiveType.Cylinder,
                new Vector3(0f, (keepTop + 2.5f + CitadelCrownY) * 0.5f, kz), new Vector3(0.8f, (CitadelCrownY - keepTop - 2.5f) * 0.5f, 0.8f), White);
            Prim(root, "CommonsCupolaCap", PrimitiveType.Cylinder,
                new Vector3(0f, CitadelCrownY, kz), new Vector3(1.0f, 0.04f, 1.0f), Graphite);
            Prim(root, "Dress_CommonsBeacon", PrimitiveType.Sphere,
                new Vector3(0f, CitadelCrownY + 0.2f, kz), new Vector3(0.3f, 0.3f, 0.3f), Orange, new Color(0.9f, 0.35f, 0.05f));

            DetailCitadel(root, H, T, kw, kd, kz, gateHalf, hull);
        }

        private static void DetailCitadel(Transform root, float H, float T, float kw, float kd, float kz, float gateHalf, Color hull)
        {
            var k = new DetailBatch(root, Key("CitadelV2", H, H));
            float wallTop = CitPlinth + CitWallH;
            float keepTop = CitPlinth + CitKeepH;
            float y1 = CitPlinth + CitTier1;
            float y2 = CitPlinth + CitTier2Top;
            float w2 = kw * 0.74f, d2 = kd * 0.74f;   // tier 2
            float w3 = kw * 0.5f, d3 = kd * 0.5f;     // tier 3
            float run = T * 2f - 3f;

            // ---------------------------------------------------------------- foundation
            k.Stage = SFound;
            k.Box(new Vector3(0f, 0.015f, 0f), new Vector3(H * 2f + 0.8f, 0.03f, H * 2f + 0.8f), Concrete);
            // Grand stair up to the gate.
            for (int s = 0; s < 4; s++)
            {
                float y = CitPlinth - 0.1f * (s + 1);
                float z = -H + 0.3f - s * 0.4f;
                k.Box(new Vector3(0f, y * 0.5f + 0.02f, z), new Vector3(gateHalf * 2f + 1.4f + s * 0.3f, y + 0.04f, 0.42f), Concrete);
                k.Box(new Vector3(0f, y + 0.04f, z - 0.17f), new Vector3(gateHalf * 2f + 1.3f + s * 0.3f, 0.012f, 0.05f), Yellow);
            }
            Hazard(k, new Vector3(-gateHalf - 0.6f, CitPlinth + 0.012f, -H + 0.55f), new Vector3(gateHalf + 0.6f, CitPlinth + 0.012f, -H + 0.55f), 0.16f, 0.02f);
            // Bollards along the apron.
            for (int i = -3; i <= 3; i++)
            {
                if (Mathf.Abs(i) < 2) continue;
                var p = new Vector3(i * 1.4f, 0f, -H - 1.1f);
                k.Cyl(p + Vector3.up * 0.3f, 0.2f, 0.6f, Graphite);
                k.Cyl(p + Vector3.up * 0.62f, 0.22f, 0.05f, Yellow);
            }

            // ---------------------------------------------------------------- frame
            k.Stage = SFrame;
            // Flat graphite seam pilasters on the panel ring (no buttresses).
            foreach (var f in WallFaces(T))
            {
                for (float x = -T + 2.6f; x <= T - 2.6f; x += 2.2f)
                {
                    if (f.N.z < -0.5f && Mathf.Abs(x) < gateHalf + 1.1f) continue;
                    FBox(k, f, x, CitPlinth + CitWallH * 0.5f, 0.22f, CitWallH, 0.12f, Graphite);
                }
            }
            // Open lattice relay pylons: four tapering carbon legs, ring braces and X-braces.
            for (int i = 0; i < 4; i++)
            {
                Vector3 c = TowerCentre(i, T);
                const int bays = 3;
                float yb = CitPlinth + 0.5f, yt = CitPlinth + CitTowerH;
                Vector3 Corner(int q, float y)
                {
                    float t = (y - yb) / (yt - yb);
                    float r = Mathf.Lerp(0.75f, 0.45f, t);
                    float qx = q == 0 || q == 3 ? -1f : 1f, qz = q < 2 ? -1f : 1f;
                    return c + new Vector3(qx * r, y, qz * r);
                }
                for (int q = 0; q < 4; q++)
                    k.Beam(Corner(q, yb), Corner(q, yt), 0.12f, Carbon);
                for (int b = 0; b <= bays; b++)
                {
                    float y = Mathf.Lerp(yb, yt, b / (float)bays);
                    for (int q = 0; q < 4; q++)
                        k.Beam(Corner(q, y), Corner((q + 1) % 4, y), 0.07f, Graphite);
                }
                for (int b = 0; b < bays; b++)
                {
                    float ya = Mathf.Lerp(yb, yt, b / (float)bays), yc = Mathf.Lerp(yb, yt, (b + 1) / (float)bays);
                    for (int q = 0; q < 4; q++)
                    {
                        int n = (q + 1) % 4;
                        k.Beam(Corner(q, ya), Corner(n, yc), 0.05f, Carbon);
                        k.Beam(Corner(n, ya), Corner(q, yc), 0.05f, Carbon);
                    }
                }
            }
            // Tier-1 corner pilasters.
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                k.Box(new Vector3(sx * (kw * 0.5f + 0.05f), CitPlinth + CitTier1 * 0.5f, kz + sz * (kd * 0.5f + 0.05f)),
                    new Vector3(0.4f, CitTier1, 0.4f), Graphite);

            // ---------------------------------------------------------------- shell
            k.Stage = SShell;
            // Panel ring cladding, seams, base course and ceramic capping (no walkway, no coping).
            foreach (var f in WallFaces(T))
            {
                bool front = f.N.z < -0.5f;
                for (float x = -T + 1.9f; x <= T - 1.9f; x += 1.1f)
                {
                    if (front && Mathf.Abs(x) < gateHalf + 0.4f) continue;
                    FBox(k, f, x, CitPlinth + 1.15f, 1.02f, 1.1f, 0.04f, Cream2);
                }
                k.Box(f.P(0f, wallTop + 0.05f, -0.5f), new Vector3(run, 0.1f, 1.1f), f.Rot, hull);   // ceramic capping
                FBox(k, f, 0f, CitPlinth + 0.3f, run, 0.4f, 0.08f, Carbon);                          // base course
            }
            // Tier 3 of the spire and its cornice.
            k.Box(new Vector3(0f, (y2 + 0.1f + keepTop) * 0.5f, kz), new Vector3(w3, keepTop - y2 - 0.1f, d3), hull);
            k.Box(new Vector3(0f, keepTop - 0.06f, kz), new Vector3(w3 + 0.2f, 0.12f, d3 + 0.2f), Graphite);
            // Gate armour.
            var gate = new Face(new Vector3(0f, 0f, -T - 0.95f), Vector3.back);
            FBox(k, gate, 0f, CitPlinth + 3.3f, gateHalf * 2f + 2.4f, 0.16f, 0.1f, Graphite);
            FBox(k, gate, -gateHalf - 0.55f, CitPlinth + 1.5f, 0.85f, 2.8f, 0.06f, Cream2);
            FBox(k, gate, gateHalf + 0.55f, CitPlinth + 1.5f, 0.85f, 2.8f, 0.06f, Cream2);

            // ---------------------------------------------------------------- fit-out
            k.Stage = SFit;
            // The gate: blast doors, lit transom, light bar and lamps.
            FBox(k, gate, 0f, CitPlinth + 1.2f, gateHalf * 2f, 2.4f, 0.04f, Carbon);
            FBox(k, gate, -gateHalf * 0.5f, CitPlinth + 1.15f, gateHalf - 0.08f, 2.2f, 0.08f, Graphite);
            FBox(k, gate, gateHalf * 0.5f, CitPlinth + 1.15f, gateHalf - 0.08f, 2.2f, 0.08f, Graphite);
            FBox(k, gate, 0f, CitPlinth + 1.15f, 0.04f, 2.2f, 0.09f, Orange);
            FBox(k, gate, 0f, CitPlinth + 2.65f, gateHalf * 2f - 0.4f, 0.2f, 0.06f, WarmGlass, WarmEmit);
            LightBar(k, gate, 0f, CitPlinth + 3.12f, gateHalf * 2f, CyanEmit);
            WallLamp(k, gate, -gateHalf - 0.55f, CitPlinth + 2.5f, WarmEmit);
            WallLamp(k, gate, gateHalf + 0.55f, CitPlinth + 2.5f, WarmEmit);

            // Panel ring: lamps and a cyan light bar along the top.
            foreach (var f in WallFaces(T))
            {
                bool front = f.N.z < -0.5f;
                for (float x = -T + 3.4f; x <= T - 3.4f; x += 4.4f)
                {
                    if (front && Mathf.Abs(x) < gateHalf + 1.2f) continue;
                    WallLamp(k, f, x, CitPlinth + 1.75f, WarmEmit);
                }
                LightBar(k, f, front ? -(T - 3.1f) * 0.5f - gateHalf * 0.5f : 0f, wallTop - 0.2f, front ? 2.2f : T, CyanEmit);
                if (front) LightBar(k, f, (T - 3.1f) * 0.5f + gateHalf * 0.5f, wallTop - 0.2f, 2.2f, CyanEmit);
            }

            // Inner faces (seen across the court): a few service windows, the back door, a light bar.
            foreach (var f in InnerWallFaces(T))
            {
                bool front = f.N.z > 0.5f;
                for (float x = -T + 2.3f; x <= T - 2.3f; x += 2.3f)
                {
                    if (front && Mathf.Abs(x) < gateHalf + 0.6f) continue;
                    if (!front && f.N.z < -0.5f && Mathf.Abs(x) < 1.2f) continue;   // clear of the back door
                    Window(k, f, x, CitPlinth + 1.2f, 0.6f, 0.45f, Glass, GlassEmit);
                }
                if (!front) Door(k, f, 0f, 1.1f, 1.6f, Graphite, CitPlinth);
                LightBar(k, f, front ? -(T + gateHalf) * 0.5f : 0f, CitPlinth + 1.85f, front ? T - gateHalf - 1.5f : T * 1.2f, CyanEmit);
                if (front) LightBar(k, f, (T + gateHalf) * 0.5f, CitPlinth + 1.85f, T - gateHalf - 1.5f, CyanEmit);
            }

            // Pylon heads: cyan beacon and nav lights.
            for (int i = 0; i < 4; i++)
            {
                Vector3 c = TowerCentre(i, T);
                float top = CitPlinth + CitTowerH + 0.45f;
                k.Box(c + Vector3.up * (top - 0.22f), new Vector3(1.34f, 0.08f, 1.34f), Graphite);
                k.Ball(c + Vector3.up * (top + 0.16f), 0.26f, Cyan, CyanEmit);
                for (int n = 0; n < 4; n++)
                    NavLight(k, c + Dir(n * 90f + 45f) * 0.9f + Vector3.up * (top - 0.1f), n % 2 == 0 ? RedEmit : CyanEmit);
            }

            // Server tiers: rack seams and status lights on tier 1, radiator fins on both tiers' flanks.
            foreach (var f in KeepFaces(kw, kd, kz))
            {
                bool side = Mathf.Abs(f.N.x) > 0.5f;
                float half = side ? kd * 0.5f : kw * 0.5f;
                for (float x = -half + 0.6f; x <= half - 0.6f; x += 0.9f)
                {
                    if (f.N.z < -0.5f && Mathf.Abs(x) < 1.1f) continue;   // keep door
                    FBox(k, f, x, CitPlinth + CitTier1 * 0.5f, 0.05f, CitTier1 - 0.5f, 0.05f, Graphite);
                    FBox(k, f, x + 0.25f, CitPlinth + CitTier1 * 0.72f, 0.12f, 0.06f, 0.04f, Cyan, CyanEmit * 0.5f);
                }
                LightBar(k, f, 0f, y1 - 0.22f, half * 1.6f, CyanEmit);
                if (side)
                {
                    for (int v = -2; v <= 2; v++)
                        k.Box(new Vector3(f.O.x + f.N.x * 0.3f, CitPlinth + CitTier1 * 0.5f, kz + v * kd * 0.18f),
                            new Vector3(0.6f, CitTier1 - 0.6f, 0.06f), Graphite);
                    for (int v = -1; v <= 1; v++)
                        k.Box(new Vector3(f.N.x * (w2 * 0.5f + 0.25f), (y1 + y2) * 0.5f + 0.08f, kz + v * d2 * 0.28f),
                            new Vector3(0.5f, y2 - y1 - 0.6f, 0.06f), Graphite);
                }
            }
            // Glowing cyan core band wrapping tier 2, and the core window on its front face.
            foreach (var f in KeepFaces(w2, d2, kz))
            {
                float half = Mathf.Abs(f.N.x) > 0.5f ? d2 * 0.5f : w2 * 0.5f;
                FBox(k, f, 0f, y2 - 0.45f, half * 2f - 0.1f, 0.22f, 0.05f, Cyan, CyanEmit * 0.7f);
            }
            var tier2Front = new Face(new Vector3(0f, 0f, kz - d2 * 0.5f), Vector3.back);
            float coreY = (y1 + y2) * 0.5f - 0.15f;
            FBox(k, tier2Front, 0f, coreY, 1.3f, 1.5f, 0.06f, Carbon);
            FBox(k, tier2Front, 0f, coreY, 0.42f, 1.3f, 0.09f, Cyan, CyanEmit * 0.6f);
            // Tier 3: vertical cyan data seams.
            foreach (var f in KeepFaces(w3, d3, kz))
                for (int s = -1; s <= 1; s += 2)
                    FBox(k, f, s * 0.45f, (y2 + keepTop) * 0.5f, 0.06f, keepTop - y2 - 0.5f, 0.04f, Cyan, CyanEmit * 0.5f);

            var keepFront = new Face(new Vector3(0f, 0f, kz - kd * 0.5f), Vector3.back);
            Door(k, keepFront, 0f, 1.5f, 2.0f, Graphite, CitPlinth);
            // Cable trunks from the tier-1 corners down to the ground.
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                var cableTop = new Vector3(sx * (kw * 0.5f + 0.05f), y1 - 0.3f, kz + sz * (kd * 0.5f + 0.05f));
                k.Rod(cableTop, new Vector3(cableTop.x + sx * 0.8f, CitPlinth + 0.05f, cableTop.z + sz * 0.8f), 0.24f, Carbon);
            }
            // Tier-1 roof terrace: dish and antenna.
            Dish(k, new Vector3(kw * 0.4f, y1 + 0.16f, kz - kd * 0.38f), 0.7f, 210f, -30f);
            Antenna(k, new Vector3(-kw * 0.42f, y1 + 0.16f, kz - kd * 0.4f), 1.4f);
            // Spire: cyan core ring on the lower segment, crown comms on the cap.
            for (int r = 0; r < 8; r++)
                k.Box(new Vector3(0f, keepTop + 1.4f, kz) + Dir(r * 45f) * 0.76f, new Vector3(0.26f, 0.5f, 0.04f),
                    Quaternion.Euler(0f, r * 45f, 0f), Cyan, CyanEmit);
            for (int i = 0; i < 3; i++)
            {
                Vector3 p = new Vector3(0f, CitadelCrownY + 0.02f, kz) + Dir(i * 120f + 30f) * 0.32f;
                k.Rod(p, p + Vector3.up * (0.9f + i * 0.25f), 0.04f, Steel);
                k.Ball(p + Vector3.up * (0.94f + i * 0.25f), 0.09f, RedLamp, RedEmit);
            }

            // Courtyard: lamp posts, planters, a cargo corner and tanks.
            for (int s = -1; s <= 1; s += 2)
            {
                var post = new Vector3(s * 2.6f, CitPlinth, -T + 1.6f);
                k.Cyl(post + Vector3.up * 0.9f, 0.1f, 1.8f, Graphite);
                k.Box(post + Vector3.up * 1.85f, new Vector3(0.3f, 0.1f, 0.3f), Carbon);
                k.Box(post + Vector3.up * 1.79f, new Vector3(0.24f, 0.03f, 0.24f), Cream, WarmEmit);
                var planter = new Vector3(s * 1.4f, CitPlinth, kz - kd * 0.5f - 0.9f);
                k.Box(planter + Vector3.up * 0.25f, new Vector3(0.9f, 0.5f, 0.9f), Graphite);
                k.Ball(planter + Vector3.up * 0.65f, new Vector3(0.9f, 0.6f, 0.9f), Plant);
            }
            Tank(k, new Vector3(T - 1.6f, CitPlinth, T - 1.6f), 0.9f, 1.6f, White);
            Tank(k, new Vector3(T - 2.7f, CitPlinth, T - 1.6f), 0.9f, 1.6f, White);
            Tank(k, new Vector3(-T + 1.6f, CitPlinth, T - 1.6f), 0.9f, 1.4f, Orange);
            Greebles(k, new Vector3(-T + 2.0f, CitPlinth, -T + 2.0f), 0.8f, 0.8f, 6, 31);
            PipeRun(k, new[]
            {
                new Vector3(T - 1.6f, CitPlinth + 1.3f, T - 1.6f),
                new Vector3(kw * 0.5f + 0.75f, CitPlinth + 1.3f, T - 1.6f),
                new Vector3(kw * 0.5f + 0.75f, CitPlinth + 1.3f, kz + kd * 0.45f)
            }, 0.1f, Steel);

            k.Build();
        }

        private static Vector3 TowerCentre(int i, float T) =>
            new Vector3(i % 2 == 0 ? -T : T, 0f, i < 2 ? -T : T);

        /// <summary>Outer faces of the four curtain walls (front -Z first, as the camera sees it).</summary>
        private static Face[] WallFaces(float T) => new[]
        {
            new Face(new Vector3(0f, 0f, -T - 0.5f), Vector3.back),
            new Face(new Vector3(-T - 0.5f, 0f, 0f), Vector3.left),
            new Face(new Vector3(T + 0.5f, 0f, 0f), Vector3.right),
            new Face(new Vector3(0f, 0f, T + 0.5f), Vector3.forward),
        };

        /// <summary>Courtyard-side faces of the curtain walls.</summary>
        private static Face[] InnerWallFaces(float T) => new[]
        {
            new Face(new Vector3(0f, 0f, -T + 0.5f), Vector3.forward),
            new Face(new Vector3(-T + 0.5f, 0f, 0f), Vector3.right),
            new Face(new Vector3(T - 0.5f, 0f, 0f), Vector3.left),
            new Face(new Vector3(0f, 0f, T - 0.5f), Vector3.back),
        };

        private static Face[] KeepFaces(float kw, float kd, float kz) => new[]
        {
            new Face(new Vector3(0f, 0f, kz - kd * 0.5f), Vector3.back),
            new Face(new Vector3(-kw * 0.5f, 0f, kz), Vector3.left),
            new Face(new Vector3(kw * 0.5f, 0f, kz), Vector3.right),
            new Face(new Vector3(0f, 0f, kz + kd * 0.5f), Vector3.forward),
        };
    }
}
