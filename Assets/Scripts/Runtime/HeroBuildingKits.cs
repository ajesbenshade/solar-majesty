using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Phase 4 hero silhouettes for HAB / Palace Keep / landing pad / extractors /
    /// solar field / Defense bunker. Dressing on the square Lego grid — no new
    /// pathing, no extra occupancy colliders, no click-to-fire.
    /// </summary>
    public static class HeroBuildingKits
    {
        private static readonly Color White = new Color(0.88f, 0.90f, 0.93f);
        private static readonly Color Carbon = new Color(0.07f, 0.07f, 0.08f);
        private static readonly Color Graphite = new Color(0.16f, 0.17f, 0.19f);
        private static readonly Color Steel = new Color(0.42f, 0.44f, 0.48f);
        private static readonly Color Orange = new Color(0.96f, 0.42f, 0.08f);
        private static readonly Color Yellow = new Color(0.95f, 0.82f, 0.12f);
        private static readonly Color Concrete = new Color(0.40f, 0.41f, 0.43f);
        private static readonly Color Cyan = new Color(0.22f, 0.84f, 0.98f);
        private static readonly Color CyanEmit = new Color(0.20f, 1.15f, 1.65f);
        private static readonly Color Ice = new Color(0.52f, 0.76f, 0.86f);
        private static readonly Color IceEmit = new Color(0.12f, 0.55f, 0.95f);
        private static readonly Color Dust = new Color(0.52f, 0.36f, 0.22f);
        private static readonly Color SolarCell = new Color(0.07f, 0.14f, 0.36f);
        private static readonly Color SolarEmit = new Color(0.12f, 0.48f, 1.55f);

        private static Shader _lit;

        public static bool IsHero(BuildingCategory cat) =>
            cat == BuildingCategory.Habitat ||
            cat == BuildingCategory.Palace ||
            cat == BuildingCategory.LandingPad ||
            cat == BuildingCategory.Farm ||
            cat == BuildingCategory.Mine ||
            cat == BuildingCategory.RegolithCamp ||
            cat == BuildingCategory.Power ||
            cat == BuildingCategory.Defense;

        public static void BuildHabitat(Transform root, float w, float d, Color hull)
        {
            float radius = Mathf.Min(w, d) * 0.36f;
            BuildPressurizedDome(root, "Hab", radius, 2.05f, hull, keep: false);
            // Living-quarters porch lights + roof utility so it reads as a HAB, not a keep.
            Prim(root, "HabHatch", PrimitiveType.Cube,
                new Vector3(0f, 1.15f, radius * 0.92f),
                new Vector3(0.85f, 1.15f, 0.12f), Orange);
            Prim(root, "HabToolbox", PrimitiveType.Cube,
                new Vector3(0.55f, 3.15f, -0.15f),
                new Vector3(0.7f, 0.32f, 0.55f), Graphite);
            Prim(root, "HabAntenna", PrimitiveType.Cylinder,
                new Vector3(-0.45f, 3.55f, 0.2f),
                new Vector3(0.06f, 0.45f, 0.06f), Steel);
        }

        public static void BuildKeep(Transform root, float w, float d, Color hull)
        {
            float radius = Mathf.Min(w, d) * 0.40f;
            BuildPressurizedDome(root, "Keep", radius, 3.2f, hull, keep: true);

            // Comms mast + dish — citadel landmark, taller than a HAB.
            Prim(root, "KeepAntenna", PrimitiveType.Cylinder,
                new Vector3(0f, 6.15f, 0f),
                new Vector3(0.12f, 1.15f, 0.12f), Steel);
            Prim(root, "KeepPack", PrimitiveType.Sphere,
                new Vector3(0.55f, 6.85f, 0f),
                new Vector3(0.85f, 0.22f, 0.85f), Graphite);
            Prim(root, "KeepVisorBeacon", PrimitiveType.Sphere,
                new Vector3(0f, 7.45f, 0f),
                new Vector3(0.28f, 0.28f, 0.28f), Cyan, CyanEmit);

            // Cardinal docking drums (visual) so the keep reads as the tube hub.
            float dockR = radius * 0.98f;
            DockCollar(root, "KeepDock_N", new Vector3(0f, 1.35f, dockR), Vector3.forward);
            DockCollar(root, "KeepDock_S", new Vector3(0f, 1.35f, -dockR), Vector3.back);
            DockCollar(root, "KeepDock_E", new Vector3(dockR, 1.35f, 0f), Vector3.right);
            DockCollar(root, "KeepDock_W", new Vector3(-dockR, 1.35f, 0f), Vector3.left);
        }

        public static void BuildLandingPad(Transform root, float w, float d, Color hull)
        {
            float span = Mathf.Min(w, d);
            float dia = span * 0.92f;

            Prim(root, "Dress_PadDisc", PrimitiveType.Cylinder,
                new Vector3(0f, 0.07f, 0f),
                new Vector3(dia, 0.07f, dia), Concrete);
            Prim(root, "Dress_PadYellow", PrimitiveType.Cylinder,
                new Vector3(0f, 0.12f, 0f),
                new Vector3(dia * 1.04f, 0.025f, dia * 1.04f), Yellow);
            Prim(root, "Dress_PadInner", PrimitiveType.Cylinder,
                new Vector3(0f, 0.13f, 0f),
                new Vector3(dia * 0.62f, 0.02f, dia * 0.62f),
                Color.Lerp(White, hull, 0.15f));
            Prim(root, "Dress_PadTarget", PrimitiveType.Cylinder,
                new Vector3(0f, 0.14f, 0f),
                new Vector3(dia * 0.22f, 0.02f, dia * 0.22f), Orange);

            for (int i = 0; i < 8; i++)
            {
                float ang = i * 45f * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang));
                Prim(root, "Dress_PadRadial_" + i, PrimitiveType.Cube,
                    dir * (dia * 0.32f) + new Vector3(0f, 0.145f, 0f),
                    new Vector3(0.16f, 0.02f, dia * 0.22f),
                    i % 2 == 0 ? Yellow : White,
                    Quaternion.Euler(0f, i * 45f, 0f));
            }

            for (int i = 0; i < 4; i++)
            {
                float ang = i * 90f * Mathf.Deg2Rad;
                Vector3 p = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * (dia * 0.46f);
                Prim(root, "Dress_PadLight_" + i, PrimitiveType.Sphere,
                    p + new Vector3(0f, 0.28f, 0f),
                    new Vector3(0.18f, 0.12f, 0.18f), Cyan, CyanEmit);
            }

            SpawnParkedShip(root);
        }

        public static void BuildWaterExtractor(Transform root, float w, float d, Color hull)
        {
            // Tall ice plant — vertical tanks + pipe tower. Not a greenhouse box.
            Prim(root, "Dress_IceCabin", PrimitiveType.Cube,
                new Vector3(-w * 0.22f, 0.85f, 0.15f),
                new Vector3(w * 0.42f, 1.7f, d * 0.48f), hull);
            Prim(root, "Dress_IceCabinCap", PrimitiveType.Cube,
                new Vector3(-w * 0.22f, 1.78f, 0.15f),
                new Vector3(w * 0.46f, 0.16f, d * 0.52f), Carbon);
            Prim(root, "Dress_IceHatch", PrimitiveType.Cube,
                new Vector3(-w * 0.22f, 0.95f, d * 0.24f),
                new Vector3(0.7f, 1.1f, 0.08f), Orange);

            // Small hydroponics vault so Farm still reads as life-support.
            Prim(root, "Dress_IceVault", PrimitiveType.Cube,
                new Vector3(-w * 0.22f, 1.35f, -d * 0.28f),
                new Vector3(w * 0.32f, 0.55f, d * 0.22f), Ice, IceEmit * 0.35f);

            float[] tankH = { 2.55f, 2.15f, 1.85f };
            float[] tankX = { w * 0.28f, w * 0.28f, w * 0.08f };
            float[] tankZ = { -d * 0.18f, d * 0.22f, d * 0.28f };
            for (int i = 0; i < 3; i++)
            {
                float h = tankH[i];
                Vector3 at = new Vector3(tankX[i], h * 0.5f + 0.15f, tankZ[i]);
                Prim(root, "Dress_IceTank_" + i, PrimitiveType.Cylinder,
                    at, new Vector3(0.72f, h * 0.5f, 0.72f), Steel);
                Prim(root, "Dress_IceBand_" + i, PrimitiveType.Cylinder,
                    at + new Vector3(0f, h * 0.12f, 0f),
                    new Vector3(0.78f, 0.06f, 0.78f), Ice, IceEmit);
                Prim(root, "Dress_IceCap_" + i, PrimitiveType.Cylinder,
                    new Vector3(tankX[i], h + 0.22f, tankZ[i]),
                    new Vector3(0.55f, 0.08f, 0.55f), Carbon);
            }

            Prim(root, "Dress_IceManifold", PrimitiveType.Cylinder,
                new Vector3(w * 0.18f, 4.05f, 0.05f),
                new Vector3(0.12f, 0.95f, 0.12f), Carbon,
                Quaternion.Euler(0f, 0f, 90f));
            Prim(root, "Dress_IceRiser", PrimitiveType.Cylinder,
                new Vector3(w * 0.28f, 2.55f, 0.02f),
                new Vector3(0.14f, 2.35f, 0.14f), Carbon);

            ScaffoldTower(root, "Dress_IceScaf", new Vector3(w * 0.28f, 0f, 0f), 4.4f, 1.15f);
            Prim(root, "Dress_IceCondenser", PrimitiveType.Sphere,
                new Vector3(w * 0.08f, 4.35f, d * 0.28f),
                new Vector3(0.7f, 0.35f, 0.7f), Ice, IceEmit);
        }

        public static void BuildRegolithExtractor(Transform root, float w, float d, Color hull)
        {
            // Low, wide, horizontal — opposite of the ice tower.
            Prim(root, "Dress_RegChassis", PrimitiveType.Cube,
                new Vector3(0f, 0.7f, 0f),
                new Vector3(w * 0.92f, 1.25f, d * 0.62f), Graphite);
            Prim(root, "Dress_RegHull", PrimitiveType.Cube,
                new Vector3(-w * 0.08f, 1.35f, 0f),
                new Vector3(w * 0.55f, 0.7f, d * 0.5f), hull);
            Prim(root, "Dress_RegHopper", PrimitiveType.Cube,
                new Vector3(w * 0.32f, 0.95f, 0f),
                new Vector3(w * 0.28f, 1.55f, d * 0.42f), Dust);
            Prim(root, "Dress_RegScoop", PrimitiveType.Cube,
                new Vector3(w * 0.46f, 0.45f, 0f),
                new Vector3(0.35f, 0.45f, d * 0.38f), Orange);

            for (int i = 0; i < 3; i++)
            {
                float z = -d * 0.22f + i * d * 0.22f;
                Prim(root, "Dress_RegPipe_" + i, PrimitiveType.Cylinder,
                    new Vector3(0.05f, 1.55f, z),
                    new Vector3(0.12f, w * 0.38f, 0.12f),
                    i == 1 ? Yellow : Orange,
                    Quaternion.Euler(0f, 0f, 90f));
            }

            Prim(root, "Dress_RegTank_L", PrimitiveType.Cylinder,
                new Vector3(-w * 0.28f, 0.85f, d * 0.32f),
                new Vector3(0.85f, 0.7f, 0.85f), Dust);
            Prim(root, "Dress_RegTank_R", PrimitiveType.Cylinder,
                new Vector3(w * 0.12f, 0.75f, d * 0.32f),
                new Vector3(1.05f, 0.55f, 1.05f), Dust);

            ScaffoldLow(root, "Dress_RegScaf", new Vector3(-w * 0.28f, 0f, -d * 0.28f), w * 0.7f);
            Prim(root, "Dress_RegBelt", PrimitiveType.Cube,
                new Vector3(w * 0.08f, 0.28f, -d * 0.28f),
                new Vector3(w * 0.7f, 0.16f, 0.35f), Yellow);
        }

        public static void BuildOreExtractor(Transform root, float w, float d, Color hull)
        {
            // Metals mine: twin silos + headframe. Not a HAB, not the ice tower.
            Prim(root, "Dress_OreDeck", PrimitiveType.Cube,
                new Vector3(0f, 0.22f, 0f),
                new Vector3(w * 0.95f, 0.4f, d * 0.9f), Graphite);
            Prim(root, "Dress_OreSilo_L", PrimitiveType.Cylinder,
                new Vector3(-w * 0.26f, 1.55f, 0.1f),
                new Vector3(w * 0.34f, 1.4f, w * 0.34f), Dust);
            Prim(root, "Dress_OreSilo_R", PrimitiveType.Cylinder,
                new Vector3(w * 0.26f, 1.55f, 0.1f),
                new Vector3(w * 0.34f, 1.4f, w * 0.34f), Dust);
            Prim(root, "Dress_OreBand_L", PrimitiveType.Cylinder,
                new Vector3(-w * 0.26f, 2.05f, 0.1f),
                new Vector3(w * 0.38f, 0.07f, w * 0.38f), Orange);
            Prim(root, "Dress_OreBand_R", PrimitiveType.Cylinder,
                new Vector3(w * 0.26f, 2.05f, 0.1f),
                new Vector3(w * 0.38f, 0.07f, w * 0.38f), Orange);
            Prim(root, "Dress_OreHead", PrimitiveType.Cube,
                new Vector3(0f, 3.15f, 0f),
                new Vector3(w * 0.92f, 0.22f, 0.55f), hull);
            Prim(root, "Dress_OreHopper", PrimitiveType.Cube,
                new Vector3(0f, 0.85f, d * 0.28f),
                new Vector3(w * 0.4f, 1.15f, d * 0.32f), Orange);
            Prim(root, "Dress_OrePipe", PrimitiveType.Cylinder,
                new Vector3(0f, 2.55f, 0.1f),
                new Vector3(0.14f, w * 0.28f, 0.14f), Carbon,
                Quaternion.Euler(0f, 0f, 90f));
            ScaffoldLow(root, "Dress_OreScaf", new Vector3(0f, 0f, -d * 0.32f), w * 0.55f);
        }

        public static void BuildSolarField(Transform root, float w, float d, Color hull)
        {
            // Landmark: rows of tilted wafers on the existing Power footprint, not a side overlay.
            Prim(root, "PwrPlinth", PrimitiveType.Cube,
                new Vector3(0f, 0.07f, 0f),
                new Vector3(w * 0.94f, 0.12f, d * 0.94f), Graphite);
            Prim(root, "PwrStripe", PrimitiveType.Cube,
                new Vector3(0f, 0.14f, 0f),
                new Vector3(w * 0.18f, 0.03f, d * 0.92f), Orange);

            const int cols = 4;
            const int rows = 3;
            float cellW = w * 0.18f;
            float cellD = d * 0.20f;
            float pitchX = w * 0.21f;
            float pitchZ = d * 0.22f;
            float originX = -pitchX * (cols - 1) * 0.5f;
            float originZ = -d * 0.08f - pitchZ * (rows - 1) * 0.5f;

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    Vector3 at = new Vector3(originX + c * pitchX, 0.92f, originZ + r * pitchZ);
                    Prim(root, "SolarSteelPylon_" + r + "_" + c, PrimitiveType.Cylinder,
                        new Vector3(at.x, 0.48f, at.z),
                        new Vector3(0.08f, 0.42f, 0.08f), Steel);
                    Prim(root, "SolarArray_" + r + "_" + c, PrimitiveType.Cube,
                        at, new Vector3(cellW, 0.04f, cellD), SolarCell,
                        Quaternion.Euler(-38f, 0f, 0f), SolarEmit);
                    Prim(root, "SolarVisor_" + r + "_" + c, PrimitiveType.Cube,
                        at + new Vector3(0f, 0.22f, cellD * 0.18f),
                        new Vector3(cellW * 0.92f, 0.02f, 0.04f), Cyan,
                        Quaternion.Euler(-38f, 0f, 0f), SolarEmit);
                }

                Prim(root, "SolarVisorBus_" + r, PrimitiveType.Cube,
                    new Vector3(0f, 0.22f, originZ + r * pitchZ),
                    new Vector3(w * 0.78f, 0.03f, 0.06f), Cyan, SolarEmit);
            }

            Prim(root, "PwrInverter", PrimitiveType.Cube,
                new Vector3(0f, 0.62f, d * 0.34f),
                new Vector3(w * 0.32f, 1.05f, d * 0.18f), hull);
            Prim(root, "PwrInverterCap", PrimitiveType.Cube,
                new Vector3(0f, 1.18f, d * 0.34f),
                new Vector3(w * 0.36f, 0.12f, d * 0.22f), Carbon);
            Prim(root, "PwrHatch", PrimitiveType.Cube,
                new Vector3(0f, 0.7f, d * 0.44f),
                new Vector3(0.55f, 0.7f, 0.08f), Orange);
            Prim(root, "SolarVisorBeacon", PrimitiveType.Sphere,
                new Vector3(0f, 1.55f, d * 0.34f),
                new Vector3(0.28f, 0.28f, 0.28f), Cyan, SolarEmit);
        }

        public static void BuildDefenseBattery(Transform root, float w, float d, Color hull)
        {
            // Angular bunker + roof gun — not a HAB/keep dome. Shield bubble stays Week 1 dressing.
            Prim(root, "DefPlinth", PrimitiveType.Cube,
                new Vector3(0f, 0.12f, 0f),
                new Vector3(w * 0.92f, 0.22f, d * 0.92f), Carbon);
            Prim(root, "DefHull", PrimitiveType.Cube,
                new Vector3(0f, 0.95f, 0f),
                new Vector3(w * 0.72f, 1.55f, d * 0.62f), hull);
            Prim(root, "DefBand", PrimitiveType.Cube,
                new Vector3(0f, 0.55f, 0f),
                new Vector3(w * 0.76f, 0.14f, d * 0.66f), Carbon);
            Prim(root, "DefStripe", PrimitiveType.Cube,
                new Vector3(0f, 1.35f, d * 0.32f),
                new Vector3(w * 0.55f, 0.12f, 0.08f), Orange);
            Prim(root, "DefChevron", PrimitiveType.Cube,
                new Vector3(0f, 1.55f, d * 0.32f),
                new Vector3(w * 0.28f, 0.08f, 0.08f), Orange);
            Prim(root, "DefVisor", PrimitiveType.Cube,
                new Vector3(0f, 1.12f, d * 0.32f),
                new Vector3(w * 0.42f, 0.16f, 0.07f), Cyan, CyanEmit);
            Prim(root, "DefHatch", PrimitiveType.Cube,
                new Vector3(0f, 0.7f, d * 0.32f),
                new Vector3(0.7f, 0.85f, 0.08f), Orange);

            for (int i = 0; i < 4; i++)
            {
                float ang = (i * 90f + 45f) * Mathf.Deg2Rad;
                Vector3 p = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * (Mathf.Min(w, d) * 0.38f);
                Prim(root, "DefBollard_" + i, PrimitiveType.Cylinder,
                    p + new Vector3(0f, 0.55f, 0f),
                    new Vector3(0.16f, 0.5f, 0.16f), Carbon);
                Prim(root, "DefVisorEye_" + i, PrimitiveType.Sphere,
                    p + new Vector3(0f, 1.12f, 0f),
                    new Vector3(0.14f, 0.14f, 0.14f), Cyan, CyanEmit);
            }

            BuildJunctionTurret(root, new Vector3(0f, 1.85f, 0.08f), 0f, 1.35f);
        }

        /// <summary>
        /// Dual-barrel gun/sensor pod. Visual only — no fire, no agent, no extra occupancy.
        /// </summary>
        public static void BuildJunctionTurret(
            Transform parent,
            Vector3 localPos,
            float yawDeg = 45f,
            float scale = 1f)
        {
            var pivot = new GameObject("Dress_JunctionTurret");
            pivot.transform.SetParent(parent, false);
            pivot.transform.localPosition = localPos;
            pivot.transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
            pivot.transform.localScale = Vector3.one * scale;
            Transform t = pivot.transform;

            Prim(t, "TurretPlinth", PrimitiveType.Cylinder,
                new Vector3(0f, 0.05f, 0f),
                new Vector3(0.62f, 0.05f, 0.62f), Carbon);
            Prim(t, "TurretAccentRing", PrimitiveType.Cylinder,
                new Vector3(0f, 0.12f, 0f),
                new Vector3(0.70f, 0.03f, 0.70f), Orange);
            Prim(t, "TurretBase", PrimitiveType.Cylinder,
                new Vector3(0f, 0.22f, 0f),
                new Vector3(0.48f, 0.10f, 0.48f), White);
            Prim(t, "TurretBand", PrimitiveType.Cylinder,
                new Vector3(0f, 0.34f, 0f),
                new Vector3(0.22f, 0.08f, 0.22f), Graphite);
            Prim(t, "TurretHead", PrimitiveType.Cube,
                new Vector3(0f, 0.50f, 0.05f),
                new Vector3(0.44f, 0.24f, 0.36f), White);
            Prim(t, "TurretSteelBarrel_L", PrimitiveType.Cylinder,
                new Vector3(-0.11f, 0.50f, 0.40f),
                new Vector3(0.07f, 0.28f, 0.07f), Steel,
                Quaternion.Euler(90f, 0f, 0f));
            Prim(t, "TurretSteelBarrel_R", PrimitiveType.Cylinder,
                new Vector3(0.11f, 0.50f, 0.40f),
                new Vector3(0.07f, 0.28f, 0.07f), Steel,
                Quaternion.Euler(90f, 0f, 0f));
            Prim(t, "TurretLensEye_L", PrimitiveType.Sphere,
                new Vector3(-0.11f, 0.52f, 0.20f),
                new Vector3(0.11f, 0.11f, 0.11f), Cyan, CyanEmit);
            Prim(t, "TurretLensEye_R", PrimitiveType.Sphere,
                new Vector3(0.11f, 0.52f, 0.20f),
                new Vector3(0.11f, 0.11f, 0.11f), Cyan, CyanEmit);
            Prim(t, "TurretStripe", PrimitiveType.Cube,
                new Vector3(0f, 0.64f, 0.04f),
                new Vector3(0.28f, 0.04f, 0.08f), Orange);
        }

        private static void BuildPressurizedDome(
            Transform root, string prefix, float radius, float drumH, Color hull, bool keep)
        {
            float yDrum = drumH * 0.5f + 0.28f;
            Prim(root, prefix + "Plinth", PrimitiveType.Cylinder,
                new Vector3(0f, 0.16f, 0f),
                new Vector3(radius * 2.35f, 0.16f, radius * 2.35f), Carbon);
            Prim(root, prefix + "AccentRing", PrimitiveType.Cylinder,
                new Vector3(0f, 0.28f, 0f),
                new Vector3(radius * 2.2f, 0.05f, radius * 2.2f), Orange);

            OctagonDrum(root, prefix + "Drum_", radius, drumH, yDrum, hull);

            Prim(root, prefix + "Band", PrimitiveType.Cylinder,
                new Vector3(0f, yDrum - drumH * 0.08f, 0f),
                new Vector3(radius * 2.12f, 0.07f, radius * 2.12f), Carbon);
            Prim(root, prefix + "Stripe", PrimitiveType.Cylinder,
                new Vector3(0f, yDrum + drumH * 0.08f, 0f),
                new Vector3(radius * 2.16f, keep ? 0.14f : 0.09f, radius * 2.16f), Orange);
            if (keep)
            {
                Prim(root, prefix + "Stripe2", PrimitiveType.Cylinder,
                    new Vector3(0f, yDrum + drumH * 0.28f, 0f),
                    new Vector3(radius * 2.14f, 0.08f, radius * 2.14f), Orange);
            }

            float domeY = yDrum + drumH * 0.42f;
            Prim(root, prefix + "Dome", PrimitiveType.Sphere,
                new Vector3(0f, domeY, 0f),
                new Vector3(radius * 1.95f, radius * (keep ? 0.95f : 0.82f), radius * 1.95f), hull);
            Prim(root, prefix + "Cap", PrimitiveType.Cylinder,
                new Vector3(0f, domeY + radius * (keep ? 0.42f : 0.32f), 0f),
                new Vector3(radius * 1.15f, 0.07f, radius * 1.15f), White);
            Prim(root, prefix + "BandCupola", PrimitiveType.Cylinder,
                new Vector3(0f, domeY + radius * (keep ? 0.55f : 0.42f), 0f),
                new Vector3(radius * 0.42f, keep ? 0.28f : 0.16f, radius * 0.42f), Carbon);

            int rows = keep ? 2 : 1;
            for (int row = 0; row < rows; row++)
            {
                float wy = yDrum + (row == 0 ? -drumH * 0.12f : drumH * 0.22f);
                for (int i = 0; i < 8; i++)
                {
                    if (!keep && i % 2 == 0) continue; // HAB: diagonal viewports only (cardinals are docks)
                    float ang = i * 45f * Mathf.Deg2Rad;
                    float r = radius * 1.02f;
                    Vector3 pos = new Vector3(Mathf.Sin(ang) * r, wy, Mathf.Cos(ang) * r);
                    Prim(root, prefix + "Visor_" + row + "_" + i, PrimitiveType.Cube,
                        pos,
                        new Vector3(radius * 0.42f, keep ? 0.22f : 0.18f, 0.08f),
                        Cyan, Quaternion.Euler(0f, i * 45f, 0f), CyanEmit);
                }
            }
        }

        private static void OctagonDrum(
            Transform root, string prefix, float radius, float height, float y, Color hull)
        {
            const int sides = 8;
            float chord = 2f * radius * Mathf.Tan(Mathf.PI / sides);
            float thick = 0.22f;
            float r = radius - thick * 0.45f;
            for (int i = 0; i < sides; i++)
            {
                float ang = i * 45f;
                float rad = ang * Mathf.Deg2Rad;
                Vector3 pos = new Vector3(Mathf.Sin(rad) * r, y, Mathf.Cos(rad) * r);
                Prim(root, prefix + i, PrimitiveType.Cube, pos,
                    new Vector3(chord * 1.02f, height, thick), hull,
                    Quaternion.Euler(0f, ang, 0f));
            }
        }

        private static void DockCollar(Transform root, string name, Vector3 pos, Vector3 outward)
        {
            bool ns = Mathf.Abs(outward.z) >= Mathf.Abs(outward.x);
            Quaternion rot = ns
                ? Quaternion.Euler(90f, 0f, 0f)
                : Quaternion.Euler(0f, 0f, 90f);
            Prim(root, name, PrimitiveType.Cylinder, pos,
                new Vector3(0.95f, 0.28f, 0.95f), White, rot);
            Prim(root, name + "Accent", PrimitiveType.Cylinder,
                pos + outward.normalized * 0.32f,
                new Vector3(1.05f, 0.07f, 1.05f), Orange, rot);
        }

        private static void ScaffoldTower(Transform root, string prefix, Vector3 at, float height, float span)
        {
            Vector3[] feet =
            {
                at + new Vector3(-span, 0f, -span),
                at + new Vector3(span, 0f, -span),
                at + new Vector3(-span, 0f, span),
                at + new Vector3(span, 0f, span)
            };
            for (int i = 0; i < 4; i++)
            {
                Prim(root, prefix + "Post_" + i, PrimitiveType.Cube,
                    feet[i] + new Vector3(0f, height * 0.5f, 0f),
                    new Vector3(0.08f, height, 0.08f), Carbon);
            }
            Prim(root, prefix + "Beam", PrimitiveType.Cube,
                at + new Vector3(0f, height * 0.92f, 0f),
                new Vector3(span * 2.1f, 0.08f, span * 2.1f), Yellow);
        }

        private static void ScaffoldLow(Transform root, string prefix, Vector3 at, float width)
        {
            for (int i = 0; i < 3; i++)
            {
                Prim(root, prefix + "Post_" + i, PrimitiveType.Cube,
                    at + new Vector3(-width * 0.4f + i * width * 0.4f, 1.15f, 0f),
                    new Vector3(0.07f, 2.2f, 0.07f), Carbon);
            }
            Prim(root, prefix + "Beam", PrimitiveType.Cube,
                at + new Vector3(0f, 2.2f, 0f),
                new Vector3(width, 0.07f, 0.07f), Yellow);
        }

        private static void SpawnParkedShip(Transform root)
        {
            GameObject prefab = BuildingVisualCatalog.LoadStarship();
            if (prefab != null)
            {
                var ship = ColonyVisualUtility.InstantiateOriented(prefab, root.position, root, 0f);
                ship.name = "Dress_Starship";
                ship.transform.localScale = Vector3.one * ColonyLayout.ShipScale;
                ship.transform.localPosition = Vector3.zero;
                StripColliders(ship);
                ColonyVisualUtility.EnsureUrpMaterials(ship);
                ColonyVisualUtility.SnapToGround(ship, root.position.y + 0.16f);
                return;
            }

            BuildProceduralShip(root);
        }

        private static void BuildProceduralShip(Transform root)
        {
            const float h = 7.2f;
            Prim(root, "Dress_StarshipBody", PrimitiveType.Cylinder,
                new Vector3(0f, h * 0.45f, 0f),
                new Vector3(0.78f, h * 0.42f, 0.78f), White);
            Prim(root, "Dress_StarshipNose", PrimitiveType.Sphere,
                new Vector3(0f, h * 0.92f, 0f),
                new Vector3(0.78f, 1.15f, 0.78f), White);
            Prim(root, "Dress_StarshipHeat", PrimitiveType.Cube,
                new Vector3(0.42f, h * 0.42f, 0f),
                new Vector3(0.08f, h * 0.7f, 0.7f), Carbon);
            Prim(root, "Dress_StarshipBand", PrimitiveType.Cylinder,
                new Vector3(0f, h * 0.38f, 0f),
                new Vector3(0.84f, 0.12f, 0.84f), Carbon);
            Prim(root, "Dress_StarshipStripe", PrimitiveType.Cube,
                new Vector3(-0.4f, h * 0.5f, 0f),
                new Vector3(0.06f, 1.6f, 0.22f), Orange);
            Prim(root, "Dress_StarshipFin_L", PrimitiveType.Cube,
                new Vector3(0f, 0.85f, 0.55f),
                new Vector3(0.08f, 1.1f, 0.55f), Carbon);
            Prim(root, "Dress_StarshipFin_R", PrimitiveType.Cube,
                new Vector3(0f, 0.85f, -0.55f),
                new Vector3(0.08f, 1.1f, 0.55f), Carbon);
            Prim(root, "Dress_StarshipFlap_L", PrimitiveType.Cube,
                new Vector3(0.15f, h * 0.78f, 0.42f),
                new Vector3(0.06f, 0.55f, 0.4f), Carbon);
            Prim(root, "Dress_StarshipFlap_R", PrimitiveType.Cube,
                new Vector3(0.15f, h * 0.78f, -0.42f),
                new Vector3(0.06f, 0.55f, 0.4f), Carbon);
        }

        private static void Prim(
            Transform parent,
            string name,
            PrimitiveType type,
            Vector3 localPos,
            Vector3 localScale,
            Color color,
            Color emission = default)
        {
            Prim(parent, name, type, localPos, localScale, color, Quaternion.identity, emission);
        }

        private static void Prim(
            Transform parent,
            string name,
            PrimitiveType type,
            Vector3 localPos,
            Vector3 localScale,
            Color color,
            Quaternion localRot,
            Color emission = default)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = localScale;
            Object.Destroy(go.GetComponent<Collider>());
            Tint(go, color, emission);
        }

        private static void Tint(GameObject go, Color c, Color emission = default)
        {
            var rend = go.GetComponent<Renderer>();
            if (rend == null) return;
            EnsureLit();
            if (_lit == null) return;
            var mat = new Material(_lit) { name = go.name };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_Color")) mat.color = c;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.38f);
            if (mat.HasProperty("_Metallic"))
                mat.SetFloat("_Metallic", c.maxColorComponent < 0.2f ? 0.4f : 0.08f);
            if (emission.maxColorComponent > 0.01f && mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission);
            }
            rend.sharedMaterial = mat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        private static void StripColliders(GameObject root)
        {
            if (root == null) return;
            var cols = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] != null) Object.Destroy(cols[i]);
            }
        }

        private static void EnsureLit()
        {
            if (_lit != null) return;
            _lit = Shader.Find("Universal Render Pipeline/Lit")
                   ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                   ?? Shader.Find("Sprites/Default");
        }
    }
}
