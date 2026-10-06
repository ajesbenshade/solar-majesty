using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Colony Commons as the Majesty 2 castle: a walled citadel. Four corner towers with gun
    /// turrets, armoured curtain walls with a battlement walk, a gatehouse on the front (-Z) face,
    /// a courtyard, and a three-storey keep carrying the command tier and comms spire.
    /// Big masses are named primitives (the construction stages classify them); everything else
    /// goes through <see cref="DetailBatch"/>, a handful of merged meshes.
    /// The play camera sits at yaw 45°, so the -Z and -X faces are what the player sees first.
    /// </summary>
    public static partial class HeroBuildingKits
    {
        // Heights (m above ground).
        private const float CitPlinth = 0.4f;
        private const float CitWallH = 3.2f;
        private const float CitTowerH = 5.6f;
        private const float CitKeepH = 6.6f;
        private const float CitKeepStorey = CitKeepH / 3f;

        /// <summary>Top of the spire cap, where the crown comms and beacon mount.</summary>
        public const float CitadelCrownY = CitPlinth + CitKeepH + 4.55f;

        public static void BuildCommons(Transform root, float w, float d, Color hull)
        {
            float H = Mathf.Min(w, d) * 0.5f;          // half extent (9 m at 12 cells)
            float T = H - 1.7f;                          // corner tower centres
            float kw = H * 0.78f, kd = H * 0.66f;        // keep footprint
            float kz = H * 0.08f;                        // keep sits back to leave a front court
            float keepTop = CitPlinth + CitKeepH;

            // ---------------------------------------------------------------- big masses
            Prim(root, "CommonsPlinth", PrimitiveType.Cube,
                new Vector3(0f, CitPlinth * 0.5f, 0f), new Vector3(H * 2f - 0.6f, CitPlinth, H * 2f - 0.6f), Carbon);
            Prim(root, "CommonsMech", PrimitiveType.Cube,
                new Vector3(0f, CitPlinth + 0.02f, 0f), new Vector3(T * 2f - 1f, 0.04f, T * 2f - 1f), PadDeck);

            for (int i = 0; i < 4; i++)
            {
                float sx = i % 2 == 0 ? -1f : 1f, sz = i < 2 ? -1f : 1f;
                var c = new Vector3(sx * T, 0f, sz * T);
                Prim(root, "CommonsTower_" + i, PrimitiveType.Cylinder,
                    c + Vector3.up * (CitPlinth + CitTowerH * 0.5f), new Vector3(3f, CitTowerH * 0.5f, 3f), hull);
                Prim(root, "CommonsTowerCrown_" + i, PrimitiveType.Cylinder,
                    c + Vector3.up * (CitPlinth + CitTowerH + 0.12f), new Vector3(3.5f, 0.12f, 3.5f), Graphite);
                Prim(root, "CommonsTowerBase_" + i, PrimitiveType.Cylinder,
                    c + Vector3.up * (CitPlinth + 0.35f), new Vector3(3.3f, 0.35f, 3.3f), Carbon);
            }

            // Curtain walls between the towers; the front (-Z) wall leaves a gate gap.
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

            // Gatehouse: two pylons and a lintel over the gate.
            for (int s = -1; s <= 1; s += 2)
                Prim(root, "CommonsGatePylon_" + (s < 0 ? "L" : "R"), PrimitiveType.Cube,
                    new Vector3(s * (gateHalf + 0.55f), CitPlinth + 2.3f, -T - 0.2f), new Vector3(1.3f, 4.6f, 1.7f), hull);
            Prim(root, "CommonsGateLintel", PrimitiveType.Cube,
                new Vector3(0f, CitPlinth + 3.75f, -T - 0.2f), new Vector3(gateHalf * 2f + 0.2f, 1.7f, 1.5f), hull);
            Prim(root, "CommonsStripe", PrimitiveType.Cube,
                new Vector3(0f, CitPlinth + 4.0f, -T - 0.98f), new Vector3(gateHalf * 2f + 2.6f, 0.14f, 0.06f), Orange);

            // Keep: three storeys.
            Prim(root, "CommonsKeep", PrimitiveType.Cube,
                new Vector3(0f, CitPlinth + CitKeepH * 0.5f, kz), new Vector3(kw, CitKeepH, kd), hull);
            Prim(root, "CommonsKeepCornice", PrimitiveType.Cube,
                new Vector3(0f, keepTop + 0.08f, kz), new Vector3(kw + 0.3f, 0.16f, kd + 0.3f), Graphite);

            // Command tier, comms spire and beacon on the keep.
            Prim(root, "CommonsDome", PrimitiveType.Cylinder,
                new Vector3(0f, keepTop + 1.1f, kz), new Vector3(kd * 0.78f, 0.95f, kd * 0.78f), hull);
            Prim(root, "CommonsTierBand", PrimitiveType.Cylinder,
                new Vector3(0f, keepTop + 1.45f, kz), new Vector3(kd * 0.8f, 0.07f, kd * 0.8f), Orange);
            Prim(root, "CommonsRoofDeck", PrimitiveType.Cylinder,
                new Vector3(0f, keepTop + 2.1f, kz), new Vector3(kd * 0.86f, 0.06f, kd * 0.86f), Graphite);
            Prim(root, "CommonsCupolaLo", PrimitiveType.Cylinder,
                new Vector3(0f, keepTop + 3.25f, kz), new Vector3(1.4f, 1.1f, 1.4f), White);
            Prim(root, "Dress_CommonsCupolaBand", PrimitiveType.Cylinder,
                new Vector3(0f, keepTop + 3.9f, kz), new Vector3(1.48f, 0.05f, 1.48f), Orange);
            Prim(root, "CommonsCupolaHi", PrimitiveType.Cylinder,
                new Vector3(0f, keepTop + 4.45f, kz), new Vector3(1.0f, 0.1f, 1.0f), White);
            Prim(root, "CommonsCupolaCap", PrimitiveType.Cylinder,
                new Vector3(0f, CitadelCrownY, kz), new Vector3(1.2f, 0.04f, 1.2f), Graphite);
            Prim(root, "Dress_CommonsBeacon", PrimitiveType.Sphere,
                new Vector3(0f, CitadelCrownY + 0.2f, kz), new Vector3(0.3f, 0.3f, 0.3f), Orange, new Color(0.9f, 0.35f, 0.05f));

            DetailCitadel(root, H, T, kw, kd, kz, gateHalf, hull);
        }

        private static void DetailCitadel(Transform root, float H, float T, float kw, float kd, float kz, float gateHalf, Color hull)
        {
            var k = new DetailBatch(root, Key("Citadel", H, H));
            float wallTop = CitPlinth + CitWallH;
            float keepTop = CitPlinth + CitKeepH;
            float outer = T + 0.5f;          // wall outer face

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
            // Buttresses on the outer wall faces.
            foreach (var f in WallFaces(T))
            {
                for (float x = -T + 2.6f; x <= T - 2.6f; x += 2.2f)
                {
                    if (f.N.z < -0.5f && Mathf.Abs(x) < gateHalf + 1.3f) continue;
                    FBox(k, f, x, CitPlinth + 1.3f, 0.42f, 2.6f, 0.5f, Graphite);
                    FBox(k, f, x, CitPlinth + 0.2f, 0.7f, 0.4f, 0.8f, Carbon);
                }
            }
            // Vertical ribs on the corner towers.
            for (int i = 0; i < 4; i++)
            {
                Vector3 c = TowerCentre(i, T);
                for (int r = 0; r < 8; r++)
                {
                    Vector3 dir = Dir(r * 45f + 22.5f);
                    k.Box(c + dir * 1.52f + Vector3.up * (CitPlinth + CitTowerH * 0.5f), new Vector3(0.14f, CitTowerH - 0.4f, 0.14f),
                        Quaternion.Euler(0f, r * 45f + 22.5f, 0f), Graphite);
                }
            }
            // Keep corner pilasters.
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                k.Box(new Vector3(sx * (kw * 0.5f + 0.05f), CitPlinth + CitKeepH * 0.5f, kz + sz * (kd * 0.5f + 0.05f)),
                    new Vector3(0.4f, CitKeepH, 0.4f), Graphite);

            // ---------------------------------------------------------------- shell
            k.Stage = SShell;
            // Wall cladding panels, base course and the battlement walk with merlons.
            foreach (var f in WallFaces(T))
            {
                bool front = f.N.z < -0.5f;
                for (float x = -T + 1.9f; x <= T - 1.9f; x += 1.1f)
                {
                    if (front && Mathf.Abs(x) < gateHalf + 0.4f) continue;
                    FBox(k, f, x, CitPlinth + 1.75f, 1.02f, 1.6f, 0.04f, Cream2);
                }
                for (float x = -T + 1.75f; x <= T - 1.75f; x += 0.9f)
                {
                    if (front && Mathf.Abs(x) < gateHalf + 0.3f) continue;
                    FBox(k, f, x, wallTop + 0.25f, 0.5f, 0.5f, 0.3f, hull);   // merlon
                }
                FBox(k, f, 0f, CitPlinth + 0.45f, T * 2f - 3f, 0.5f, 0.08f, Carbon);   // base course
                FBox(k, f, 0f, wallTop - 0.05f, T * 2f - 3f, 0.1f, 0.12f, Graphite);   // coping
            }
            // Inner walkway along the wall tops.
            k.Box(new Vector3(0f, wallTop + 0.02f, T - 0.1f), new Vector3(T * 2f - 3f, 0.04f, 1.2f), PadDeck);
            k.Box(new Vector3(-T + 0.1f, wallTop + 0.02f, 0f), new Vector3(1.2f, 0.04f, T * 2f - 3f), PadDeck);
            k.Box(new Vector3(T - 0.1f, wallTop + 0.02f, 0f), new Vector3(1.2f, 0.04f, T * 2f - 3f), PadDeck);
            // Tower merlons and roofs.
            for (int i = 0; i < 4; i++)
            {
                Vector3 c = TowerCentre(i, T);
                float top = CitPlinth + CitTowerH + 0.24f;
                for (int m = 0; m < 10; m++)
                {
                    Vector3 dir = Dir(m * 36f);
                    k.Box(c + dir * 1.55f + Vector3.up * (top + 0.25f), new Vector3(0.5f, 0.5f, 0.32f),
                        Quaternion.Euler(0f, m * 36f, 0f), hull);
                }
                k.Cyl(c + Vector3.up * (top + 0.02f), 2.9f, 0.04f, PadDeck);
            }
            // Keep storey bands and the gatehouse armour.
            for (int s = 1; s < 3; s++)
                k.Box(new Vector3(0f, CitPlinth + s * CitKeepStorey, kz), new Vector3(kw + 0.12f, 0.14f, kd + 0.12f), Carbon);
            var gate = new Face(new Vector3(0f, 0f, -T - 1.05f), Vector3.back);
            FBox(k, gate, 0f, CitPlinth + 4.55f, gateHalf * 2f + 2.8f, 0.3f, 0.1f, Graphite);
            FBox(k, gate, -gateHalf - 0.55f, CitPlinth + 2.3f, 1.0f, 4.2f, 0.06f, Cream2);
            FBox(k, gate, gateHalf + 0.55f, CitPlinth + 2.3f, 1.0f, 4.2f, 0.06f, Cream2);

            // ---------------------------------------------------------------- fit-out
            k.Stage = SFit;
            // The gate: blast doors, lit transom, light bars and lamps.
            FBox(k, gate, 0f, CitPlinth + 1.45f, gateHalf * 2f, 2.9f, 0.04f, Carbon);
            FBox(k, gate, -gateHalf * 0.5f, CitPlinth + 1.4f, gateHalf - 0.08f, 2.7f, 0.08f, Graphite);
            FBox(k, gate, gateHalf * 0.5f, CitPlinth + 1.4f, gateHalf - 0.08f, 2.7f, 0.08f, Graphite);
            FBox(k, gate, 0f, CitPlinth + 1.4f, 0.04f, 2.7f, 0.09f, Orange);
            FBox(k, gate, 0f, CitPlinth + 3.05f, gateHalf * 2f - 0.4f, 0.28f, 0.06f, WarmGlass, WarmEmit);
            LightBar(k, gate, 0f, CitPlinth + 3.4f, gateHalf * 2f, CyanEmit);
            WallLamp(k, gate, -gateHalf - 0.55f, CitPlinth + 3.2f, WarmEmit);
            WallLamp(k, gate, gateHalf + 0.55f, CitPlinth + 3.2f, WarmEmit);
            for (int s = -1; s <= 1; s += 2)
            {
                // Colony banners on the gate pylons.
                FBox(k, gate, s * (gateHalf + 0.55f), CitPlinth + 2.2f, 0.7f, 2.4f, 0.1f, Orange);
                FBox(k, gate, s * (gateHalf + 0.55f), CitPlinth + 2.2f, 0.26f, 2.2f, 0.12f, White);
                FBox(k, gate, s * (gateHalf + 0.55f), CitPlinth + 3.45f, 0.84f, 0.08f, 0.14f, Graphite);
            }

            // Wall slits and lamps.
            foreach (var f in WallFaces(T))
            {
                bool front = f.N.z < -0.5f;
                for (float x = -T + 2.35f; x <= T - 2.35f; x += 1.1f)
                {
                    if (front && Mathf.Abs(x) < gateHalf + 0.9f) continue;
                    FBox(k, f, x, CitPlinth + 2.1f, 0.18f, 0.7f, 0.07f, WarmGlass, WarmEmit * 0.8f);
                }
                for (float x = -T + 3.4f; x <= T - 3.4f; x += 4.4f)
                {
                    if (front && Mathf.Abs(x) < gateHalf + 1.2f) continue;
                    WallLamp(k, f, x, CitPlinth + 2.8f, WarmEmit);
                }
                LightBar(k, f, front ? -(T - 3.1f) * 0.5f - gateHalf * 0.5f : 0f, wallTop - 0.3f, front ? 2.2f : T, CyanEmit);
                if (front) LightBar(k, f, (T - 3.1f) * 0.5f + gateHalf * 0.5f, wallTop - 0.3f, 2.2f, CyanEmit);
            }

            // Inner wall faces (seen across the courtyard): barracks windows, a door, a light bar.
            foreach (var f in InnerWallFaces(T))
            {
                bool front = f.N.z > 0.5f;
                for (float x = -T + 2.3f; x <= T - 2.3f; x += 1.15f)
                {
                    if (front && Mathf.Abs(x) < gateHalf + 0.6f) continue;
                    bool lit = Mathf.RoundToInt(x * 3.7f) % 3 != 0;
                    Window(k, f, x, CitPlinth + 2.05f, 0.5f, 0.6f, lit ? WarmGlass : Glass, lit ? WarmEmit : GlassEmit);
                }
                if (!front) Door(k, f, 0f, 1.1f, 1.6f, Graphite, CitPlinth);
                LightBar(k, f, front ? -(T + gateHalf) * 0.5f : 0f, CitPlinth + 2.75f, front ? T - gateHalf - 1.5f : T * 1.2f, CyanEmit);
                if (front) LightBar(k, f, (T + gateHalf) * 0.5f, CitPlinth + 2.75f, T - gateHalf - 1.5f, CyanEmit);
                FBox(k, f, 0f, CitPlinth + 0.3f, T * 2f - 3f, 0.6f, 0.06f, Carbon);
            }

            // Towers: window bands, nav lights, turrets, banners.
            for (int i = 0; i < 4; i++)
            {
                Vector3 c = TowerCentre(i, T);
                float top = CitPlinth + CitTowerH + 0.26f;
                for (int band = 0; band < 2; band++)
                {
                    float y = CitPlinth + 2.2f + band * 1.9f;
                    for (int r = 0; r < 8; r++)
                    {
                        float a = r * 45f;
                        k.Box(c + Dir(a) * 1.51f + Vector3.up * y, new Vector3(0.5f, 0.5f, 0.05f),
                            Quaternion.Euler(0f, a, 0f), WarmGlass, WarmEmit * 0.8f);
                    }
                    k.Cyl(c + Vector3.up * (y + 0.38f), 3.08f, 0.06f, Carbon);
                }
                k.Cyl(c + Vector3.up * (CitPlinth + CitTowerH - 0.3f), 3.06f, 0.12f, Orange);
                // Turret.
                k.Cyl(c + Vector3.up * (top + 0.2f), 1.1f, 0.4f, Graphite);
                k.Box(c + Vector3.up * (top + 0.62f), new Vector3(0.9f, 0.5f, 0.9f), Quaternion.Euler(0f, 45f + i * 90f, 0f), hull);
                Vector3 aim = Dir(225f + i * 90f);
                for (int g = -1; g <= 1; g += 2)
                {
                    Vector3 side = Vector3.Cross(aim, Vector3.up) * (g * 0.16f);
                    k.Rod(c + side + Vector3.up * (top + 0.65f), c + side + aim * 1.25f + Vector3.up * (top + 0.75f), 0.09f, Carbon);
                }
                k.Ball(c + Vector3.up * (top + 0.92f), 0.18f, Cyan, CyanEmit);
                for (int n = 0; n < 4; n++)
                    NavLight(k, c + Dir(n * 90f + 45f) * 1.6f + Vector3.up * (top + 0.5f), n % 2 == 0 ? RedEmit : CyanEmit);
                // Pennant mast.
                Vector3 mast = c + Dir(45f + i * 90f) * 0.9f + Vector3.up * top;
                k.Rod(mast, mast + Vector3.up * 2.0f, 0.05f, Steel);
                k.Box(mast + Vector3.up * 1.65f + Dir(135f + i * 90f) * 0.35f, new Vector3(0.7f, 0.42f, 0.03f),
                    Quaternion.Euler(0f, 135f + i * 90f, 0f), Orange);
            }

            // Keep: lit windows on every storey, a balcony over the court, roof plant.
            foreach (var f in KeepFaces(kw, kd, kz))
            {
                float half = Mathf.Abs(f.N.x) > 0.5f ? kd * 0.5f : kw * 0.5f;
                int cols = Mathf.Max(2, Mathf.FloorToInt((half * 2f - 0.8f) / 1.2f));
                for (int s = 0; s < 3; s++)
                {
                    float y = CitPlinth + s * CitKeepStorey + CitKeepStorey * 0.55f;
                    for (int c = 0; c < cols; c++)
                    {
                        float x = -half + 0.6f + (c + 0.5f) * (half * 2f - 1.2f) / cols;
                        bool lit = (c + s) % 3 != 0;
                        Window(k, f, x, y, 0.62f, 0.95f, lit ? WarmGlass : Glass, lit ? WarmEmit : GlassEmit);
                    }
                }
                LightBar(k, f, 0f, CitPlinth + CitKeepH - 0.25f, half * 1.6f, CyanEmit);
            }
            var keepFront = new Face(new Vector3(0f, 0f, kz - kd * 0.5f), Vector3.back);
            Door(k, keepFront, 0f, 1.5f, 2.0f, Graphite, CitPlinth);
            FBox(k, keepFront, 0f, CitPlinth + CitKeepStorey * 2f + 0.05f, kw * 0.6f, 0.12f, 0.9f, Graphite);
            Railing(k, keepFront.P(-kw * 0.3f, CitPlinth + CitKeepStorey * 2f + 0.1f, 0.85f),
                keepFront.P(kw * 0.3f, CitPlinth + CitKeepStorey * 2f + 0.1f, 0.85f), 0.5f, Steel);
            // Command tier glazing and roof.
            for (int r = 0; r < 16; r++)
            {
                float a = r * 22.5f;
                k.Box(new Vector3(0f, keepTop + 0.95f, kz) + Dir(a) * (kd * 0.39f + 0.01f), new Vector3(0.55f, 0.6f, 0.04f),
                    Quaternion.Euler(0f, a, 0f), WarmGlass, WarmEmit);
            }
            Railing(k, new Vector3(-kw * 0.45f, keepTop + 0.16f, kz - kd * 0.45f), new Vector3(kw * 0.45f, keepTop + 0.16f, kz - kd * 0.45f), 0.45f, Steel);
            Railing(k, new Vector3(-kw * 0.45f, keepTop + 0.16f, kz + kd * 0.45f), new Vector3(kw * 0.45f, keepTop + 0.16f, kz + kd * 0.45f), 0.45f, Steel);
            RoofUnit(k, new Vector3(-kw * 0.36f, keepTop + 0.16f, kz + kd * 0.3f), 0.8f, 0.6f, 0.5f);
            RoofUnit(k, new Vector3(kw * 0.36f, keepTop + 0.16f, kz + kd * 0.3f), 0.8f, 0.6f, 0.5f);
            Dish(k, new Vector3(kw * 0.36f, keepTop + 0.16f, kz - kd * 0.3f), 0.9f, 210f, -30f);
            Antenna(k, new Vector3(-kw * 0.38f, keepTop + 0.16f, kz - kd * 0.32f), 1.8f);
            // Crown comms on the spire.
            for (int i = 0; i < 3; i++)
            {
                Vector3 p = new Vector3(0f, CitadelCrownY + 0.02f, kz) + Dir(i * 120f + 30f) * 0.38f;
                k.Rod(p, p + Vector3.up * (0.9f + i * 0.25f), 0.04f, Steel);
                k.Ball(p + Vector3.up * (0.94f + i * 0.25f), 0.09f, RedLamp, RedEmit);
            }
            for (int r = 0; r < 8; r++)
                k.Box(new Vector3(0f, CitPlinth + CitKeepH + 3.3f, kz) + Dir(r * 45f) * 0.71f, new Vector3(0.28f, 0.36f, 0.04f),
                    Quaternion.Euler(0f, r * 45f, 0f), Cyan, CyanEmit);

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
                new Vector3(kw * 0.5f + 0.2f, CitPlinth + 1.3f, T - 1.6f),
                new Vector3(kw * 0.5f + 0.2f, CitPlinth + 1.3f, kz + kd * 0.3f)
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
