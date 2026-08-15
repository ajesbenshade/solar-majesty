using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Phase 4 hero silhouettes. HAB / Commons / LAB / Power / pad stay sheet-matched.
    /// Guild is CMD-1 civic dress; Mining is OPS-1 annex; Farm / Camp / Mine and wonders
    /// use distinct industrial kits. Dressing on the square Lego grid — no new pathing,
    /// no extra occupancy colliders, no click-to-fire.
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
        private static readonly Color SolarEmit = new Color(0.22f, 0.72f, 2.15f);
        private static readonly Color Glass = new Color(0.48f, 0.72f, 0.82f);
        private static readonly Color GlassEmit = new Color(0.06f, 0.22f, 0.28f);
        private static readonly Color Plant = new Color(0.22f, 0.55f, 0.24f);

        private static Shader _lit;

        public static bool IsHero(BuildingCategory cat) =>
            cat == BuildingCategory.Habitat ||
            cat == BuildingCategory.Commons ||
            cat == BuildingCategory.LandingPad ||
            cat == BuildingCategory.Farm ||
            cat == BuildingCategory.Mine ||
            cat == BuildingCategory.RegolithCamp ||
            cat == BuildingCategory.Power ||
            cat == BuildingCategory.Defense ||
            cat == BuildingCategory.GuildHall ||
            cat == BuildingCategory.Mining ||
            cat == BuildingCategory.Laboratory ||
            cat == BuildingCategory.ClimateLoom ||
            cat == BuildingCategory.AegisSpire ||
            cat == BuildingCategory.DeepArchive ||
            cat == BuildingCategory.Inn ||
            ColonyStructure.IsWorkshopCategory(cat);

        public static void BuildHabitat(Transform root, float w, float d, Color hull)
        {
            // HAB-1 living module: horizontal cylinder on skids (sheet Ø8×L12 → 4×4 / 6 m).
            float length = Mathf.Min(w, d) * 0.92f;
            float radius = length / 3f;
            float z = radius + 0.22f;
            Quaternion alongX = Quaternion.Euler(0f, 0f, 90f);

            Prim(root, "HabShell", PrimitiveType.Cylinder,
                new Vector3(0f, z, 0f),
                new Vector3(radius * 2f, length * 0.36f, radius * 2f), hull, alongX);
            Prim(root, "HabMid", PrimitiveType.Cylinder,
                new Vector3(0f, z, 0f),
                new Vector3(radius * 2.06f, 0.36f, radius * 2.06f), Carbon, alongX);

            for (int s = -1; s <= 1; s += 2)
            {
                float x = s * (length * 0.36f);
                Prim(root, "HabCap_" + s, PrimitiveType.Cylinder,
                    new Vector3(x, z, 0f),
                    new Vector3(radius * 1.98f, 0.39f, radius * 1.98f), Carbon, alongX);
                Prim(root, "HabRing_" + s, PrimitiveType.Cylinder,
                    new Vector3(x + s * 0.38f, z, 0f),
                    new Vector3(radius * 2.1f, 0.05f, radius * 2.1f), Orange, alongX);
                Prim(root, "HabDock_" + s, PrimitiveType.Cylinder,
                    new Vector3(s * (length * 0.50f), z, 0f),
                    new Vector3(1.24f, 0.21f, 1.24f), Graphite, alongX);
                Prim(root, "HabDockAccent_" + s, PrimitiveType.Cylinder,
                    new Vector3(s * (length * 0.52f), z, 0f),
                    new Vector3(1.40f, 0.035f, 1.40f), Orange, alongX);
            }

            Prim(root, "HabFront", PrimitiveType.Cylinder,
                new Vector3(-length * 0.50f, z, 0f),
                new Vector3(1.56f, 0.04f, 1.56f), Steel, alongX);
            Prim(root, "HabFrontSquare", PrimitiveType.Cube,
                new Vector3(-length * 0.54f, z, 0f),
                new Vector3(0.10f, 0.55f, 0.55f), White);
            Prim(root, "HabRearFrame", PrimitiveType.Cube,
                new Vector3(length * 0.50f, z, 0f),
                new Vector3(0.06f, 1.32f, 0.88f), Carbon);
            Prim(root, "HabRearDoor", PrimitiveType.Cube,
                new Vector3(length * 0.48f, z, 0f),
                new Vector3(0.10f, 1.15f, 0.72f), Orange);
            Prim(root, "HabSideFrame", PrimitiveType.Cube,
                new Vector3(0.12f, z, -radius * 1.02f),
                new Vector3(1.05f, 1.35f, 0.08f), Carbon);
            Prim(root, "HabSideDoor", PrimitiveType.Cube,
                new Vector3(0.12f, z, -radius * 0.96f),
                new Vector3(0.85f, 1.15f, 0.12f), Orange);
            Prim(root, "HabToolbox", PrimitiveType.Cube,
                new Vector3(-0.85f, z + radius * 0.82f, 0.05f),
                new Vector3(1.05f, 0.38f, 0.62f), Graphite);
            Prim(root, "HabUtil", PrimitiveType.Cube,
                new Vector3(0.55f, z + radius * 0.78f, -0.08f),
                new Vector3(0.72f, 0.28f, 0.48f), White);
            Prim(root, "HabUtilCap", PrimitiveType.Cube,
                new Vector3(0.55f, z + radius * 0.96f, -0.08f),
                new Vector3(0.55f, 0.10f, 0.36f), Carbon);
            Prim(root, "HabAntenna", PrimitiveType.Cylinder,
                new Vector3(-0.85f, z + radius * 1.15f, 0.2f),
                new Vector3(0.06f, 0.35f, 0.06f), Steel);
            Prim(root, "HabVisor_L", PrimitiveType.Cube,
                new Vector3(-1.35f, z + 0.12f, radius * 0.92f),
                new Vector3(0.55f, 0.22f, 0.06f), Cyan, CyanEmit);
            Prim(root, "HabVisor_R", PrimitiveType.Cube,
                new Vector3(1.15f, z + 0.12f, radius * 0.92f),
                new Vector3(0.55f, 0.22f, 0.06f), Cyan, CyanEmit);

            float[] sx = { -1.85f, -1.85f, 1.85f, 1.85f };
            float[] sz = { -1.15f, 1.15f, -1.15f, 1.15f };
            for (int i = 0; i < 4; i++)
            {
                Prim(root, "HabLeg_" + i, PrimitiveType.Cube,
                    new Vector3(sx[i], 0.42f, sz[i]),
                    new Vector3(0.55f, 0.72f, 0.38f), Carbon);
                Prim(root, "HabPad_" + i, PrimitiveType.Cube,
                    new Vector3(sx[i], 0.10f, sz[i]),
                    new Vector3(0.82f, 0.16f, 0.58f), Graphite);
            }
        }

        public static void BuildCommons(Transform root, float w, float d, Color hull)
        {
            // Command-dome civic citadel (sheet). Player-facing name stays Colony Commons.
            float radius = Mathf.Min(w, d) * 0.38f;
            Prim(root, "CommonsPlinth", PrimitiveType.Cylinder,
                new Vector3(0f, 0.28f, 0f),
                new Vector3(radius * 2.44f, 0.28f, radius * 2.44f), Carbon);
            Prim(root, "CommonsMech", PrimitiveType.Cylinder,
                new Vector3(0f, 0.58f, 0f),
                new Vector3(radius * 2.24f, 0.11f, radius * 2.24f), Graphite);
            for (int i = 0; i < 8; i++)
            {
                float ang = i * 45f * Mathf.Deg2Rad;
                Prim(root, "CommonsLamp_" + i, PrimitiveType.Cube,
                    new Vector3(Mathf.Sin(ang) * radius * 1.14f, 0.52f, Mathf.Cos(ang) * radius * 1.14f),
                    new Vector3(0.14f, 0.08f, 0.10f), Orange);
            }

            Prim(root, "CommonsDrum", PrimitiveType.Cylinder,
                new Vector3(0f, 1.15f, 0f),
                new Vector3(radius * 2f, 0.58f, radius * 2f), hull);
            Prim(root, "CommonsBand", PrimitiveType.Cylinder,
                new Vector3(0f, 1.35f, 0f),
                new Vector3(radius * 2.08f, 0.07f, radius * 2.08f), Carbon);
            Prim(root, "CommonsStripe", PrimitiveType.Cylinder,
                new Vector3(0f, 1.72f, 0f),
                new Vector3(radius * 2.12f, 0.05f, radius * 2.12f), Orange);
            Prim(root, "CommonsDome", PrimitiveType.Sphere,
                new Vector3(0f, 1.85f, 0f),
                new Vector3(radius * 2.04f, radius * 1.47f, radius * 2.04f), hull);
            Prim(root, "CommonsDomeBand", PrimitiveType.Cylinder,
                new Vector3(0f, 3.05f, 0f),
                new Vector3(radius * 1.44f, 0.05f, radius * 1.44f), Carbon);

            Prim(root, "CommonsCupolaLo", PrimitiveType.Cylinder,
                new Vector3(0f, 3.55f, 0f),
                new Vector3(radius * 0.56f, 0.21f, radius * 0.56f), White);
            Prim(root, "CommonsCupolaBand", PrimitiveType.Cylinder,
                new Vector3(0f, 3.72f, 0f),
                new Vector3(radius * 0.60f, 0.04f, radius * 0.60f), Carbon);
            Prim(root, "CommonsCupolaHi", PrimitiveType.Cylinder,
                new Vector3(0f, 3.95f, 0f),
                new Vector3(radius * 0.36f, 0.16f, radius * 0.36f), White);
            Prim(root, "CommonsCupolaCap", PrimitiveType.Cylinder,
                new Vector3(0f, 4.14f, 0f),
                new Vector3(radius * 0.40f, 0.04f, radius * 0.40f), Carbon);
            Prim(root, "CommonsAntenna", PrimitiveType.Cylinder,
                new Vector3(0f, 4.75f, 0f),
                new Vector3(0.09f, 0.58f, 0.09f), Steel);
            Prim(root, "CommonsPack", PrimitiveType.Sphere,
                new Vector3(0.42f, 5.15f, 0f),
                new Vector3(0.56f, 0.16f, 0.56f), Graphite);
            Prim(root, "CommonsVisorBeacon", PrimitiveType.Sphere,
                new Vector3(0f, 5.45f, 0f),
                new Vector3(0.24f, 0.24f, 0.24f), Cyan, CyanEmit);

            for (int i = 0; i < 8; i++)
            {
                if (i % 2 == 0) continue;
                float ang = i * 45f * Mathf.Deg2Rad;
                Prim(root, "CommonsVisor_" + i, PrimitiveType.Cube,
                    new Vector3(Mathf.Sin(ang) * radius * 1.02f, 1.35f, Mathf.Cos(ang) * radius * 1.02f),
                    new Vector3(radius * 0.38f, 0.22f, 0.08f),
                    Cyan, Quaternion.Euler(0f, i * 45f, 0f), CyanEmit);
            }

            // Radial tube stubs are dressing. Square airlocks still attach in the factory.
            for (int i = 0; i < 8; i++)
            {
                float ang = i * 45f * Mathf.Deg2Rad;
                bool cardinal = i % 2 == 0;
                float stubLen = cardinal ? 0.92f : 0.52f;
                float stubR = cardinal ? 0.50f : 0.36f;
                float dist = radius * 0.98f + stubLen * 0.42f;
                Vector3 dir = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang));
                Quaternion rot = Quaternion.LookRotation(dir) * Quaternion.Euler(90f, 0f, 0f);
                Prim(root, "CommonsStub_" + i, PrimitiveType.Cylinder,
                    dir * dist + new Vector3(0f, 1.18f, 0f),
                    new Vector3(stubR * 2f, stubLen * 0.5f, stubR * 2f), White, rot);
                Prim(root, "CommonsStubTip_" + i, PrimitiveType.Cylinder,
                    dir * (dist + stubLen * 0.42f) + new Vector3(0f, 1.18f, 0f),
                    new Vector3(stubR * 2.24f, 0.04f, stubR * 2.24f), Orange, rot);
                if (cardinal)
                {
                    Prim(root, "CommonsStubCollar_" + i, PrimitiveType.Cylinder,
                        dir * (dist - stubLen * 0.18f) + new Vector3(0f, 1.18f, 0f),
                        new Vector3(stubR * 2.12f, 0.05f, stubR * 2.12f), Carbon, rot);
                }
            }
        }

        public static void BuildLandingPad(Transform root, float w, float d, Color hull)
        {
            float span = Mathf.Min(w, d);
            float dia = span * 0.92f;

            Prim(root, "Dress_PadDisc", PrimitiveType.Cylinder,
                new Vector3(0f, 0.08f, 0f),
                new Vector3(dia, 0.08f, dia), Graphite);
            Prim(root, "Dress_PadLip", PrimitiveType.Cylinder,
                new Vector3(0f, 0.06f, 0f),
                new Vector3(dia * 1.04f, 0.05f, dia * 1.04f), Concrete);
            Prim(root, "Dress_PadYellow", PrimitiveType.Cylinder,
                new Vector3(0f, 0.15f, 0f),
                new Vector3(dia * 1.01f, 0.02f, dia * 1.01f), Yellow);
            Prim(root, "Dress_PadRing_0", PrimitiveType.Cylinder,
                new Vector3(0f, 0.17f, 0f),
                new Vector3(dia * 0.84f, 0.02f, dia * 0.84f), Orange);
            Prim(root, "Dress_PadRing_1", PrimitiveType.Cylinder,
                new Vector3(0f, 0.17f, 0f),
                new Vector3(dia * 0.56f, 0.018f, dia * 0.56f), Orange);
            Prim(root, "Dress_PadRing_2", PrimitiveType.Cylinder,
                new Vector3(0f, 0.17f, 0f),
                new Vector3(dia * 0.32f, 0.015f, dia * 0.32f), Orange);
            Prim(root, "Dress_PadInner", PrimitiveType.Cylinder,
                new Vector3(0f, 0.18f, 0f),
                new Vector3(dia * 0.22f, 0.015f, dia * 0.22f), Carbon);
            Prim(root, "Dress_PadH_L", PrimitiveType.Cube,
                new Vector3(-0.42f, 0.20f, 0f),
                new Vector3(0.10f, 0.03f, 0.95f), Orange);
            Prim(root, "Dress_PadH_R", PrimitiveType.Cube,
                new Vector3(0.42f, 0.20f, 0f),
                new Vector3(0.10f, 0.03f, 0.95f), Orange);
            Prim(root, "Dress_PadH_Bar", PrimitiveType.Cube,
                new Vector3(0f, 0.20f, 0f),
                new Vector3(0.84f, 0.03f, 0.12f), Orange);

            for (int i = 0; i < 4; i++)
            {
                float ang = i * 90f * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang));
                Prim(root, "Dress_PadTick_" + i, PrimitiveType.Cube,
                    dir * (dia * 0.44f) + new Vector3(0f, 0.19f, 0f),
                    new Vector3(0.12f, 0.03f, 0.42f),
                    Orange, Quaternion.Euler(0f, i * 90f, 0f));
                Prim(root, "Dress_PadLight_" + i, PrimitiveType.Sphere,
                    dir * (dia * 0.46f) + new Vector3(0f, 0.28f, 0f),
                    new Vector3(0.18f, 0.12f, 0.18f), Cyan, CyanEmit);
                Prim(root, "Dress_PadVent_" + i, PrimitiveType.Cube,
                    new Vector3(Mathf.Sin(ang + 0.4f), 0f, Mathf.Cos(ang + 0.4f)) * (dia * 0.48f)
                        + new Vector3(0f, 0.22f, 0f),
                    new Vector3(0.28f, 0.10f, 0.16f),
                    Carbon, Quaternion.Euler(0f, i * 90f, 0f));
                Prim(root, "Dress_PadInterface_" + i, PrimitiveType.Cube,
                    dir * 0.95f + new Vector3(0f, 0.38f, 0f),
                    new Vector3(0.42f, 0.55f, 0.32f), Graphite);
            }

            SpawnParkedShip(root);
        }

        public static void BuildWaterExtractor(Transform root, float w, float d, Color hull)
        {
            // AG-1 vaulted greenhouse + ice plant. Not a HAB cylinder, not a cabin box.
            Quaternion alongX = Quaternion.Euler(0f, 0f, 90f);
            Prim(root, "Dress_IcePlinth", PrimitiveType.Cube,
                new Vector3(0f, 0.10f, 0f),
                new Vector3(w * 0.94f, 0.16f, d * 0.90f), Graphite);
            Prim(root, "Dress_IceSill", PrimitiveType.Cube,
                new Vector3(-w * 0.08f, 0.28f, 0f),
                new Vector3(w * 0.70f, 0.22f, d * 0.52f), Carbon);
            Prim(root, "Dress_IceHall", PrimitiveType.Cube,
                new Vector3(-w * 0.08f, 0.82f, 0f),
                new Vector3(w * 0.66f, 1.28f, d * 0.46f), hull);
            for (int i = 0; i < 5; i++)
            {
                float x = -w * 0.34f + i * w * 0.13f;
                Prim(root, "Dress_IceArch_" + i, PrimitiveType.Cube,
                    new Vector3(x, 1.42f, 0f),
                    new Vector3(0.08f, 1.05f, d * 0.52f), Carbon);
            }

            float vaultR = Mathf.Min(w, d) * 0.20f;
            Prim(root, "Dress_IceVault", PrimitiveType.Cylinder,
                new Vector3(-w * 0.08f, 1.48f, 0f),
                new Vector3(vaultR * 2f, w * 0.32f, vaultR * 2f), Glass, alongX, GlassEmit);
            Prim(root, "Dress_IceVaultRing_L", PrimitiveType.Cylinder,
                new Vector3(-w * 0.36f, 1.48f, 0f),
                new Vector3(vaultR * 2.08f, 0.04f, vaultR * 2.08f), Orange, alongX);
            Prim(root, "Dress_IceVaultRing_R", PrimitiveType.Cylinder,
                new Vector3(w * 0.18f, 1.48f, 0f),
                new Vector3(vaultR * 2.08f, 0.04f, vaultR * 2.08f), Orange, alongX);

            for (int i = 0; i < 3; i++)
            {
                float x = -w * 0.28f + i * w * 0.16f;
                Prim(root, "Dress_IceTray_" + i, PrimitiveType.Cube,
                    new Vector3(x, 0.42f, 0f),
                    new Vector3(w * 0.14f, 0.10f, d * 0.32f), Plant);
                Prim(root, "Dress_IceGlow_" + i, PrimitiveType.Cube,
                    new Vector3(x, 0.52f, 0f),
                    new Vector3(w * 0.11f, 0.06f, d * 0.24f), Ice, IceEmit * 0.45f);
            }

            Prim(root, "Dress_IceHatch", PrimitiveType.Cube,
                new Vector3(-w * 0.08f, 0.72f, d * 0.24f),
                new Vector3(0.62f, 0.85f, 0.08f), Orange);

            float[] tankH = { 2.35f, 1.85f };
            float[] tankZ = { -d * 0.22f, d * 0.22f };
            for (int i = 0; i < 2; i++)
            {
                float h = tankH[i];
                Vector3 at = new Vector3(w * 0.34f, h * 0.5f + 0.18f, tankZ[i]);
                Prim(root, "Dress_IceTank_" + i, PrimitiveType.Cylinder,
                    at, new Vector3(0.78f, h * 0.5f, 0.78f), Steel);
                Prim(root, "Dress_IceBand_" + i, PrimitiveType.Cylinder,
                    at + new Vector3(0f, h * 0.10f, 0f),
                    new Vector3(0.86f, 0.06f, 0.86f), Ice, IceEmit);
                Prim(root, "Dress_IceCap_" + i, PrimitiveType.Cylinder,
                    new Vector3(w * 0.34f, h + 0.22f, tankZ[i]),
                    new Vector3(0.58f, 0.08f, 0.58f), Carbon);
            }

            Prim(root, "Dress_IceManifold", PrimitiveType.Cylinder,
                new Vector3(w * 0.34f, 2.55f, 0f),
                new Vector3(0.12f, 0.85f, 0.12f), Carbon, Quaternion.Euler(90f, 0f, 0f));
            Prim(root, "Dress_IceRiser", PrimitiveType.Cylinder,
                new Vector3(w * 0.34f, 2.05f, -d * 0.22f),
                new Vector3(0.12f, 1.55f, 0.12f), Carbon);
            ScaffoldTower(root, "Dress_IceScaf", new Vector3(w * 0.34f, 0f, 0f), 3.6f, 0.85f);
            Prim(root, "Dress_IceCondenser", PrimitiveType.Sphere,
                new Vector3(w * 0.22f, 3.55f, d * 0.18f),
                new Vector3(0.62f, 0.32f, 0.62f), Ice, IceEmit);
        }

        public static void BuildRegolithExtractor(Transform root, float w, float d, Color hull)
        {
            // Low horizontal drum plant — cylinder language, not a HAB, not the ice greenhouse.
            Quaternion alongX = Quaternion.Euler(0f, 0f, 90f);
            Prim(root, "Dress_RegPlinth", PrimitiveType.Cube,
                new Vector3(0f, 0.08f, 0f),
                new Vector3(w * 0.94f, 0.14f, d * 0.88f), Graphite);
            Prim(root, "Dress_RegChassis", PrimitiveType.Cylinder,
                new Vector3(-w * 0.06f, 0.72f, 0f),
                new Vector3(d * 0.58f, w * 0.38f, d * 0.58f), Carbon, alongX);
            Prim(root, "Dress_RegHull", PrimitiveType.Cylinder,
                new Vector3(-w * 0.06f, 0.72f, 0f),
                new Vector3(d * 0.48f, w * 0.28f, d * 0.48f), hull, alongX);
            Prim(root, "Dress_RegBand", PrimitiveType.Cylinder,
                new Vector3(-w * 0.06f, 0.72f, 0f),
                new Vector3(d * 0.62f, 0.05f, d * 0.62f), Orange, alongX);
            Prim(root, "Dress_RegHopper", PrimitiveType.Cylinder,
                new Vector3(w * 0.32f, 1.05f, 0f),
                new Vector3(w * 0.32f, 1.15f, w * 0.32f), Dust);
            Prim(root, "Dress_RegHopperBand", PrimitiveType.Cylinder,
                new Vector3(w * 0.32f, 1.35f, 0f),
                new Vector3(w * 0.36f, 0.06f, w * 0.36f), Orange);
            Prim(root, "Dress_RegScoop", PrimitiveType.Cube,
                new Vector3(w * 0.48f, 0.42f, 0f),
                new Vector3(0.38f, 0.42f, d * 0.36f), Orange);

            for (int i = 0; i < 3; i++)
            {
                float z = -d * 0.20f + i * d * 0.20f;
                Prim(root, "Dress_RegPipe_" + i, PrimitiveType.Cylinder,
                    new Vector3(0.02f, 1.28f, z),
                    new Vector3(0.12f, w * 0.36f, 0.12f),
                    i == 1 ? Yellow : Orange, alongX);
            }

            Prim(root, "Dress_RegTank_L", PrimitiveType.Cylinder,
                new Vector3(-w * 0.28f, 0.72f, d * 0.32f),
                new Vector3(0.82f, 0.62f, 0.82f), Dust);
            Prim(root, "Dress_RegTank_R", PrimitiveType.Cylinder,
                new Vector3(w * 0.08f, 0.62f, d * 0.32f),
                new Vector3(1.02f, 0.48f, 1.02f), Dust);

            ScaffoldLow(root, "Dress_RegScaf", new Vector3(-w * 0.28f, 0f, -d * 0.28f), w * 0.7f);
            Prim(root, "Dress_RegBelt", PrimitiveType.Cube,
                new Vector3(w * 0.08f, 0.28f, -d * 0.28f),
                new Vector3(w * 0.7f, 0.16f, 0.35f), Yellow);
        }

        public static void BuildOreExtractor(Transform root, float w, float d, Color hull)
        {
            // Twin silos + A-frame headframe. Not a HAB, not the ice greenhouse.
            Prim(root, "Dress_OreDeck", PrimitiveType.Cube,
                new Vector3(0f, 0.18f, 0f),
                new Vector3(w * 0.95f, 0.32f, d * 0.90f), Graphite);
            Prim(root, "Dress_OreSilo_L", PrimitiveType.Cylinder,
                new Vector3(-w * 0.24f, 1.55f, 0.08f),
                new Vector3(w * 0.36f, 1.45f, w * 0.36f), Dust);
            Prim(root, "Dress_OreSilo_R", PrimitiveType.Cylinder,
                new Vector3(w * 0.24f, 1.55f, 0.08f),
                new Vector3(w * 0.36f, 1.45f, w * 0.36f), Dust);
            Prim(root, "Dress_OreBand_L", PrimitiveType.Cylinder,
                new Vector3(-w * 0.24f, 2.15f, 0.08f),
                new Vector3(w * 0.40f, 0.07f, w * 0.40f), Orange);
            Prim(root, "Dress_OreBand_R", PrimitiveType.Cylinder,
                new Vector3(w * 0.24f, 2.15f, 0.08f),
                new Vector3(w * 0.40f, 0.07f, w * 0.40f), Orange);
            Prim(root, "Dress_OreCap_L", PrimitiveType.Cylinder,
                new Vector3(-w * 0.24f, 3.05f, 0.08f),
                new Vector3(w * 0.28f, 0.08f, w * 0.28f), Carbon);
            Prim(root, "Dress_OreCap_R", PrimitiveType.Cylinder,
                new Vector3(w * 0.24f, 3.05f, 0.08f),
                new Vector3(w * 0.28f, 0.08f, w * 0.28f), Carbon);

            Prim(root, "Dress_OreLeg_L", PrimitiveType.Cube,
                new Vector3(-w * 0.18f, 2.35f, -d * 0.18f),
                new Vector3(0.12f, 3.4f, 0.12f), Carbon,
                Quaternion.Euler(0f, 0f, 16f));
            Prim(root, "Dress_OreLeg_R", PrimitiveType.Cube,
                new Vector3(w * 0.18f, 2.35f, -d * 0.18f),
                new Vector3(0.12f, 3.4f, 0.12f), Carbon,
                Quaternion.Euler(0f, 0f, -16f));
            Prim(root, "Dress_OreHouse", PrimitiveType.Cube,
                new Vector3(0f, 3.55f, -d * 0.12f),
                new Vector3(w * 0.28f, 0.42f, 0.38f), hull);
            Prim(root, "Dress_OreHead", PrimitiveType.Cube,
                new Vector3(0f, 4.05f, -d * 0.12f),
                new Vector3(w * 0.62f, 0.16f, 0.42f), Yellow);
            Prim(root, "Dress_OreWinch", PrimitiveType.Cylinder,
                new Vector3(0f, 3.55f, -d * 0.12f),
                new Vector3(0.42f, 0.22f, 0.42f), Steel,
                Quaternion.Euler(0f, 0f, 90f));

            Prim(root, "Dress_OreHopper", PrimitiveType.Cube,
                new Vector3(0f, 0.78f, d * 0.30f),
                new Vector3(w * 0.38f, 1.05f, d * 0.28f), Orange);
            Prim(root, "Dress_OrePipe", PrimitiveType.Cylinder,
                new Vector3(0f, 2.55f, 0.08f),
                new Vector3(0.14f, w * 0.26f, 0.14f), Carbon,
                Quaternion.Euler(0f, 0f, 90f));
            ScaffoldLow(root, "Dress_OreScaf", new Vector3(0f, 0f, -d * 0.34f), w * 0.55f);
        }

        public static void BuildSolarField(Transform root, float w, float d, Color hull)
        {
            // PWR-1 node + solar field (sheet) on the existing Power footprint.
            Prim(root, "PwrPlinth", PrimitiveType.Cube,
                new Vector3(0f, 0.08f, 0f),
                new Vector3(w * 0.94f, 0.14f, d * 0.94f), Graphite);
            Prim(root, "PwrStripe", PrimitiveType.Cube,
                new Vector3(0f, 0.16f, 0f),
                new Vector3(w * 0.14f, 0.03f, d * 0.92f), Orange);

            float ny = d * 0.22f;
            Prim(root, "PwrHull", PrimitiveType.Cube,
                new Vector3(0f, 0.95f, ny),
                new Vector3(w * 0.42f, 1.65f, d * 0.34f), hull);
            Prim(root, "PwrCap", PrimitiveType.Cube,
                new Vector3(0f, 1.82f, ny),
                new Vector3(w * 0.46f, 0.14f, d * 0.38f), Carbon);
            Prim(root, "PwrChamfer_L", PrimitiveType.Cube,
                new Vector3(-w * 0.18f, 0.72f, ny),
                new Vector3(w * 0.10f, 1.15f, d * 0.28f), Graphite);
            Prim(root, "PwrChamfer_R", PrimitiveType.Cube,
                new Vector3(w * 0.18f, 0.72f, ny),
                new Vector3(w * 0.10f, 1.15f, d * 0.28f), Graphite);
            Prim(root, "PwrRamp", PrimitiveType.Cube,
                new Vector3(0f, 0.22f, ny + d * 0.16f),
                new Vector3(w * 0.22f, 0.28f, d * 0.12f), Concrete);
            Prim(root, "PwrDoorFrame", PrimitiveType.Cube,
                new Vector3(0f, 0.78f, ny + d * 0.16f),
                new Vector3(0.72f, 1.05f, 0.08f), Carbon);
            Prim(root, "PwrHatch", PrimitiveType.Cube,
                new Vector3(0f, 0.78f, ny + d * 0.15f),
                new Vector3(0.52f, 0.85f, 0.06f), Orange);
            Prim(root, "PwrTower", PrimitiveType.Cylinder,
                new Vector3(0f, 2.35f, ny),
                new Vector3(0.64f, 0.42f, 0.64f), White);
            Prim(root, "PwrTowerBand", PrimitiveType.Cylinder,
                new Vector3(0f, 2.42f, ny),
                new Vector3(0.72f, 0.04f, 0.72f), Orange);
            Prim(root, "PwrTowerCap", PrimitiveType.Cylinder,
                new Vector3(0f, 2.82f, ny),
                new Vector3(0.44f, 0.08f, 0.44f), Carbon);
            Prim(root, "PwrVent_0", PrimitiveType.Cube,
                new Vector3(-0.42f, 1.88f, ny - 0.22f),
                new Vector3(0.38f, 0.04f, 0.32f), Carbon);
            Prim(root, "PwrVent_1", PrimitiveType.Cube,
                new Vector3(0.42f, 1.88f, ny - 0.22f),
                new Vector3(0.38f, 0.04f, 0.32f), Carbon);
            Prim(root, "PwrVent_2", PrimitiveType.Cube,
                new Vector3(-0.42f, 1.88f, ny + 0.22f),
                new Vector3(0.38f, 0.04f, 0.32f), Carbon);
            Prim(root, "PwrVent_3", PrimitiveType.Cube,
                new Vector3(0.42f, 1.88f, ny + 0.22f),
                new Vector3(0.38f, 0.04f, 0.32f), Carbon);
            Prim(root, "SolarVisorBeacon", PrimitiveType.Sphere,
                new Vector3(0f, 3.05f, ny),
                new Vector3(0.24f, 0.24f, 0.24f), Cyan, SolarEmit);

            const int cols = 4;
            const int rows = 3;
            float cellW = w * 0.18f;
            float cellD = d * 0.16f;
            float pitchX = w * 0.20f;
            float pitchZ = d * 0.17f;
            float originX = -pitchX * (cols - 1) * 0.5f;
            float originZ = -d * 0.18f - pitchZ * (rows - 1) * 0.5f;
            Quaternion tilt = Quaternion.Euler(-18f, 0f, 0f);

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    Vector3 at = new Vector3(originX + c * pitchX, 0.76f, originZ + r * pitchZ);
                    Prim(root, "SolarSteelPylon_" + r + "_" + c, PrimitiveType.Cylinder,
                        new Vector3(at.x, 0.38f, at.z),
                        new Vector3(0.07f, 0.28f, 0.07f), Steel);
                    Prim(root, "SolarFrame_" + r + "_" + c, PrimitiveType.Cube,
                        new Vector3(at.x, 0.72f, at.z),
                        new Vector3(cellW * 1.08f, 0.05f, cellD * 1.08f), Graphite, tilt);
                    Prim(root, "SolarArray_" + r + "_" + c, PrimitiveType.Cube,
                        at, new Vector3(cellW, 0.03f, cellD), SolarCell, tilt, SolarEmit);
                    Prim(root, "SolarVisor_" + r + "_" + c, PrimitiveType.Cube,
                        at + new Vector3(0f, 0.12f, cellD * 0.12f),
                        new Vector3(cellW * 0.90f, 0.02f, 0.03f), Cyan, tilt, SolarEmit);
                }

                Prim(root, "SolarVisorBus_" + r, PrimitiveType.Cube,
                    new Vector3(0f, 0.20f, originZ + r * pitchZ),
                    new Vector3(w * 0.72f, 0.03f, 0.05f), Cyan, SolarEmit);
            }

            float arrZ = originZ + pitchZ;
            Prim(root, "SolarBracket_0", PrimitiveType.Cube,
                new Vector3(-pitchX * 1.55f, 0.68f, arrZ - pitchZ * 1.15f),
                new Vector3(0.12f, 0.08f, 0.12f), Orange);
            Prim(root, "SolarBracket_1", PrimitiveType.Cube,
                new Vector3(pitchX * 1.55f, 0.68f, arrZ - pitchZ * 1.15f),
                new Vector3(0.12f, 0.08f, 0.12f), Orange);
            Prim(root, "SolarBracket_2", PrimitiveType.Cube,
                new Vector3(-pitchX * 1.55f, 0.68f, arrZ + pitchZ * 1.15f),
                new Vector3(0.12f, 0.08f, 0.12f), Orange);
            Prim(root, "SolarBracket_3", PrimitiveType.Cube,
                new Vector3(pitchX * 1.55f, 0.68f, arrZ + pitchZ * 1.15f),
                new Vector3(0.12f, 0.08f, 0.12f), Orange);
        }

        public static void BuildDefenseBattery(Transform root, float w, float d, Color hull)
        {
            // Angular bunker + roof gun — not a HAB/Commons dome. Shield bubble stays Week 1 dressing.
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

        public static void BuildWorkshop(Transform root, float w, float d, Color accent, bool tall)
        {
            // Hangar bay — not a colored greybox cube. Cardinal airlocks still attach in factory.
            float h = tall ? 2.55f : 2.05f;
            Prim(root, "ShopPlinth", PrimitiveType.Cube,
                new Vector3(0f, 0.10f, 0f),
                new Vector3(w * 0.94f, 0.18f, d * 0.94f), Carbon);
            Prim(root, "ShopApron", PrimitiveType.Cube,
                new Vector3(0f, 0.16f, d * 0.32f),
                new Vector3(w * 0.72f, 0.08f, d * 0.28f), Concrete);
            Prim(root, "ShopHull", PrimitiveType.Cube,
                new Vector3(0f, h * 0.5f + 0.12f, -d * 0.08f),
                new Vector3(w * 0.78f, h, d * 0.68f), White);
            Prim(root, "ShopCap", PrimitiveType.Cube,
                new Vector3(0f, h + 0.18f, -d * 0.08f),
                new Vector3(w * 0.84f, 0.14f, d * 0.74f), Carbon);
            Prim(root, "ShopStripe", PrimitiveType.Cube,
                new Vector3(0f, h * 0.62f, d * 0.26f),
                new Vector3(w * 0.55f, 0.10f, 0.08f), Orange);
            Prim(root, "ShopVisor", PrimitiveType.Cube,
                new Vector3(0f, h * 0.78f, d * 0.26f),
                new Vector3(w * 0.38f, 0.16f, 0.07f), Cyan, CyanEmit);
            Prim(root, "ShopDoor_L", PrimitiveType.Cube,
                new Vector3(-w * 0.16f, 0.95f, d * 0.26f),
                new Vector3(w * 0.22f, 1.55f, 0.10f), accent);
            Prim(root, "ShopDoor_R", PrimitiveType.Cube,
                new Vector3(w * 0.16f, 0.95f, d * 0.26f),
                new Vector3(w * 0.22f, 1.55f, 0.10f), accent);
            Prim(root, "ShopStack_L", PrimitiveType.Cylinder,
                new Vector3(-w * 0.22f, h + 0.55f, -d * 0.18f),
                new Vector3(0.28f, 0.42f, 0.28f), Graphite);
            Prim(root, "ShopStack_R", PrimitiveType.Cylinder,
                new Vector3(w * 0.22f, h + 0.55f, -d * 0.18f),
                new Vector3(0.28f, 0.42f, 0.28f), Graphite);
            Prim(root, "ShopCranePost", PrimitiveType.Cube,
                new Vector3(-w * 0.38f, 1.35f, d * 0.18f),
                new Vector3(0.10f, 2.4f, 0.10f), Carbon);
            Prim(root, "ShopCraneBeam", PrimitiveType.Cube,
                new Vector3(-w * 0.12f, 2.52f, d * 0.18f),
                new Vector3(w * 0.52f, 0.08f, 0.10f), Yellow);
            Prim(root, "ShopBeacon", PrimitiveType.Sphere,
                new Vector3(0f, h + 0.72f, -d * 0.08f),
                new Vector3(0.22f, 0.22f, 0.22f), Cyan, CyanEmit);
            Prim(root, "ShopHatch", PrimitiveType.Cube,
                new Vector3(0f, 0.72f, d * 0.27f),
                new Vector3(0.62f, 0.85f, 0.08f), Orange);
            Prim(root, "ShopTrack_L", PrimitiveType.Cube,
                new Vector3(-w * 0.30f, 1.05f, d * 0.27f),
                new Vector3(0.06f, 1.85f, 0.06f), Carbon);
            Prim(root, "ShopTrack_R", PrimitiveType.Cube,
                new Vector3(w * 0.30f, 1.05f, d * 0.27f),
                new Vector3(0.06f, 1.85f, 0.06f), Carbon);
            Prim(root, "ShopBayLight_L", PrimitiveType.Sphere,
                new Vector3(-w * 0.22f, h * 0.92f, d * 0.22f),
                new Vector3(0.16f, 0.16f, 0.16f), accent, CyanEmit * 0.45f);
            Prim(root, "ShopBayLight_R", PrimitiveType.Sphere,
                new Vector3(w * 0.22f, h * 0.92f, d * 0.22f),
                new Vector3(0.16f, 0.16f, 0.16f), accent, CyanEmit * 0.45f);
            for (int i = 0; i < 3; i++)
            {
                Prim(root, "ShopChevron_" + i, PrimitiveType.Cube,
                    new Vector3(0f, 0.20f, d * 0.38f - i * 0.22f),
                    new Vector3(0.55f - i * 0.08f, 0.03f, 0.10f), Yellow);
            }
        }

        public static void BuildInn(Transform root, float w, float d)
        {
            // Rest hall with porch lantern — not a three-box grey hall.
            Prim(root, "InnPlinth", PrimitiveType.Cube,
                new Vector3(0f, 0.10f, 0f),
                new Vector3(w * 0.92f, 0.18f, d * 0.92f), Carbon);
            Prim(root, "InnPorch", PrimitiveType.Cube,
                new Vector3(0f, 0.22f, d * 0.36f),
                new Vector3(w * 0.48f, 0.12f, d * 0.22f), Concrete);
            Prim(root, "InnHall", PrimitiveType.Cube,
                new Vector3(0f, 1.15f, -d * 0.04f),
                new Vector3(w * 0.58f, 2.1f, d * 0.62f), White);
            Prim(root, "InnCap", PrimitiveType.Cube,
                new Vector3(0f, 2.28f, -d * 0.04f),
                new Vector3(w * 0.64f, 0.14f, d * 0.68f), Carbon);
            Prim(root, "InnStripe", PrimitiveType.Cube,
                new Vector3(0f, 1.55f, d * 0.27f),
                new Vector3(w * 0.42f, 0.10f, 0.08f), Orange);
            Prim(root, "InnVisor", PrimitiveType.Cube,
                new Vector3(0f, 1.22f, d * 0.27f),
                new Vector3(w * 0.32f, 0.18f, 0.07f), Cyan, CyanEmit);
            Prim(root, "InnHatch", PrimitiveType.Cube,
                new Vector3(0f, 0.85f, d * 0.27f),
                new Vector3(0.62f, 1.05f, 0.08f), Orange);
            Prim(root, "InnWing_L", PrimitiveType.Cube,
                new Vector3(-w * 0.32f, 0.82f, -d * 0.06f),
                new Vector3(w * 0.22f, 1.4f, d * 0.42f), Graphite);
            Prim(root, "InnWing_R", PrimitiveType.Cube,
                new Vector3(w * 0.32f, 0.82f, -d * 0.06f),
                new Vector3(w * 0.22f, 1.4f, d * 0.42f), Graphite);
            Prim(root, "InnLanternPost", PrimitiveType.Cylinder,
                new Vector3(w * 0.18f, 1.05f, d * 0.42f),
                new Vector3(0.08f, 0.85f, 0.08f), Steel);
            Prim(root, "InnLantern", PrimitiveType.Sphere,
                new Vector3(w * 0.18f, 1.85f, d * 0.42f),
                new Vector3(0.22f, 0.22f, 0.22f), Orange, new Color(1.4f, 0.55f, 0.12f));
            Prim(root, "InnBeacon", PrimitiveType.Sphere,
                new Vector3(0f, 2.72f, -d * 0.04f),
                new Vector3(0.20f, 0.20f, 0.20f), Cyan, CyanEmit);
            Prim(root, "InnCanopy", PrimitiveType.Cube,
                new Vector3(0f, 1.55f, d * 0.38f),
                new Vector3(w * 0.42f, 0.06f, d * 0.18f), Carbon);
            Prim(root, "InnLanternPost_L", PrimitiveType.Cylinder,
                new Vector3(-w * 0.18f, 1.05f, d * 0.42f),
                new Vector3(0.08f, 0.85f, 0.08f), Steel);
            Prim(root, "InnLantern_L", PrimitiveType.Sphere,
                new Vector3(-w * 0.18f, 1.85f, d * 0.42f),
                new Vector3(0.22f, 0.22f, 0.22f), Orange, new Color(1.4f, 0.55f, 0.12f));
            Prim(root, "InnBench", PrimitiveType.Cube,
                new Vector3(0f, 0.42f, d * 0.40f),
                new Vector3(w * 0.28f, 0.12f, 0.18f), Graphite);
        }

        public static void BuildGuildHall(Transform root, float w, float d, Color hull)
        {
            // CMD-1 civic hall (sheet) + guild banner. Not a Commons dome, not a HAB cylinder.
            Prim(root, "GuildPlinth", PrimitiveType.Cube,
                new Vector3(0f, 0.14f, 0f),
                new Vector3(w * 0.96f, 0.26f, d * 0.92f), Carbon);
            Prim(root, "GuildMech", PrimitiveType.Cube,
                new Vector3(0f, 0.42f, 0f),
                new Vector3(w * 0.88f, 0.32f, d * 0.78f), Graphite);
            Prim(root, "GuildHullLo", PrimitiveType.Cube,
                new Vector3(0f, 1.05f, -d * 0.04f),
                new Vector3(w * 0.78f, 1.15f, d * 0.62f), hull);
            Prim(root, "GuildHullHi", PrimitiveType.Cube,
                new Vector3(0f, 1.95f, -d * 0.06f),
                new Vector3(w * 0.62f, 0.85f, d * 0.50f), hull);
            Prim(root, "GuildCap", PrimitiveType.Cube,
                new Vector3(0f, 2.42f, -d * 0.06f),
                new Vector3(w * 0.68f, 0.12f, d * 0.56f), Carbon);
            Prim(root, "GuildCol_L", PrimitiveType.Cube,
                new Vector3(-w * 0.16f, 1.15f, d * 0.28f),
                new Vector3(0.14f, 1.85f, 0.12f), Orange);
            Prim(root, "GuildCol_R", PrimitiveType.Cube,
                new Vector3(w * 0.16f, 1.15f, d * 0.28f),
                new Vector3(0.14f, 1.85f, 0.12f), Orange);
            Prim(root, "GuildDoorFrame", PrimitiveType.Cube,
                new Vector3(0f, 0.95f, d * 0.30f),
                new Vector3(0.72f, 1.15f, 0.10f), Carbon);
            Prim(root, "GuildHatch", PrimitiveType.Cube,
                new Vector3(0f, 0.95f, d * 0.32f),
                new Vector3(0.52f, 0.95f, 0.08f), Orange);
            Prim(root, "GuildSteps", PrimitiveType.Cube,
                new Vector3(0f, 0.22f, d * 0.42f),
                new Vector3(w * 0.36f, 0.16f, d * 0.18f), Concrete);
            Prim(root, "GuildStep2", PrimitiveType.Cube,
                new Vector3(0f, 0.36f, d * 0.36f),
                new Vector3(w * 0.30f, 0.12f, d * 0.12f), Graphite);
            Prim(root, "GuildVisor", PrimitiveType.Cube,
                new Vector3(0f, 1.58f, d * 0.28f),
                new Vector3(w * 0.28f, 0.16f, 0.07f), Cyan, CyanEmit);
            Prim(root, "GuildSensor", PrimitiveType.Sphere,
                new Vector3(0f, 2.72f, -d * 0.06f),
                new Vector3(0.55f, 0.28f, 0.55f), White);
            Prim(root, "GuildSensorBand", PrimitiveType.Cylinder,
                new Vector3(0f, 2.62f, -d * 0.06f),
                new Vector3(0.62f, 0.04f, 0.62f), Carbon);
            Prim(root, "GuildAnt_L", PrimitiveType.Cylinder,
                new Vector3(-0.45f, 3.15f, -d * 0.12f),
                new Vector3(0.05f, 0.55f, 0.05f), Steel);
            Prim(root, "GuildAnt_R", PrimitiveType.Cylinder,
                new Vector3(0.38f, 3.05f, 0.08f),
                new Vector3(0.04f, 0.42f, 0.04f), Steel);
            Prim(root, "GuildPort_E", PrimitiveType.Cube,
                new Vector3(w * 0.40f, 0.55f, 0f),
                new Vector3(0.18f, 0.55f, 0.55f), White);
            Prim(root, "GuildPortRing_E", PrimitiveType.Cube,
                new Vector3(w * 0.48f, 0.55f, 0f),
                new Vector3(0.06f, 0.62f, 0.62f), Orange);
            Prim(root, "GuildPort_W", PrimitiveType.Cube,
                new Vector3(-w * 0.40f, 0.55f, 0f),
                new Vector3(0.18f, 0.55f, 0.55f), White);
            Prim(root, "GuildPortRing_W", PrimitiveType.Cube,
                new Vector3(-w * 0.48f, 0.55f, 0f),
                new Vector3(0.06f, 0.62f, 0.62f), Orange);
            Prim(root, "GuildMast", PrimitiveType.Cylinder,
                new Vector3(w * 0.22f, 3.35f, -d * 0.18f),
                new Vector3(0.08f, 0.85f, 0.08f), Steel);
            Prim(root, "GuildBanner", PrimitiveType.Cube,
                new Vector3(w * 0.22f + 0.28f, 3.55f, -d * 0.18f),
                new Vector3(0.52f, 0.38f, 0.05f), Orange);
            Prim(root, "GuildBeacon", PrimitiveType.Sphere,
                new Vector3(w * 0.22f, 4.25f, -d * 0.18f),
                new Vector3(0.18f, 0.18f, 0.18f), Cyan, CyanEmit);
        }

        public static void BuildOpsUnit(Transform root, float w, float d, Color hull)
        {
            // OPS-1 operations annex (sheet). Low elongated prism — not Commons, not Guild/CMD.
            Prim(root, "OpsPlinth", PrimitiveType.Cube,
                new Vector3(0f, 0.10f, 0f),
                new Vector3(w * 0.94f, 0.18f, d * 0.88f), Carbon);
            Prim(root, "OpsHull", PrimitiveType.Cube,
                new Vector3(0f, 0.72f, 0f),
                new Vector3(w * 0.82f, 1.12f, d * 0.62f), hull);
            Prim(root, "OpsCap", PrimitiveType.Cube,
                new Vector3(0f, 1.32f, 0f),
                new Vector3(w * 0.88f, 0.12f, d * 0.68f), Carbon);
            Prim(root, "OpsCorner_0", PrimitiveType.Cylinder,
                new Vector3(-w * 0.38f, 0.72f, -d * 0.28f),
                new Vector3(0.42f, 1.12f, 0.42f), hull);
            Prim(root, "OpsCorner_1", PrimitiveType.Cylinder,
                new Vector3(w * 0.38f, 0.72f, -d * 0.28f),
                new Vector3(0.42f, 1.12f, 0.42f), hull);
            Prim(root, "OpsCorner_2", PrimitiveType.Cylinder,
                new Vector3(-w * 0.38f, 0.72f, d * 0.28f),
                new Vector3(0.42f, 1.12f, 0.42f), hull);
            Prim(root, "OpsCorner_3", PrimitiveType.Cylinder,
                new Vector3(w * 0.38f, 0.72f, d * 0.28f),
                new Vector3(0.42f, 1.12f, 0.42f), hull);
            Prim(root, "OpsVisorFrame", PrimitiveType.Cube,
                new Vector3(0f, 1.05f, d * 0.32f),
                new Vector3(w * 0.62f, 0.28f, 0.08f), Carbon);
            Prim(root, "OpsVisor", PrimitiveType.Cube,
                new Vector3(0f, 1.05f, d * 0.34f),
                new Vector3(w * 0.55f, 0.18f, 0.06f), Cyan, CyanEmit);
            Prim(root, "OpsSteps", PrimitiveType.Cube,
                new Vector3(0f, 0.18f, d * 0.42f),
                new Vector3(w * 0.22f, 0.12f, d * 0.14f), Concrete);
            Prim(root, "OpsHatch", PrimitiveType.Cube,
                new Vector3(0f, 0.52f, d * 0.32f),
                new Vector3(0.42f, 0.52f, 0.08f), Orange);
            Prim(root, "OpsVent", PrimitiveType.Cylinder,
                new Vector3(0.35f, 1.48f, -0.12f),
                new Vector3(0.42f, 0.08f, 0.42f), Graphite);
            Prim(root, "OpsAnt_L", PrimitiveType.Cylinder,
                new Vector3(-0.55f, 1.85f, 0.15f),
                new Vector3(0.05f, 0.42f, 0.05f), Steel);
            Prim(root, "OpsAnt_R", PrimitiveType.Cylinder,
                new Vector3(0.48f, 1.72f, -0.22f),
                new Vector3(0.04f, 0.32f, 0.04f), Steel);
            Prim(root, "OpsStripe", PrimitiveType.Cube,
                new Vector3(0f, 0.48f, d * 0.32f),
                new Vector3(w * 0.72f, 0.08f, 0.06f), Orange);
            Prim(root, "OpsBeacon", PrimitiveType.Sphere,
                new Vector3(-0.35f, 1.62f, 0.18f),
                new Vector3(0.16f, 0.16f, 0.16f), Cyan, CyanEmit);
        }

        public static void BuildLaboratory(Transform root, float w, float d, Color hull)
        {
            // LAB-1 isolated cylinder (sheet Ø4.5×L8.7 → 4×4 / 6 m). Not a HAB, not a box.
            float length = Mathf.Min(w, d) * 0.90f;
            float radius = length / 3.86f;
            float z = radius + 0.20f;
            Quaternion alongX = Quaternion.Euler(0f, 0f, 90f);

            Prim(root, "LabShell", PrimitiveType.Cylinder,
                new Vector3(0f, z, 0f),
                new Vector3(radius * 2f, length * 0.39f, radius * 2f), hull, alongX);
            Prim(root, "LabBelly", PrimitiveType.Cube,
                new Vector3(0f, z - radius * 0.42f, 0f),
                new Vector3(length * 0.72f, radius * 0.55f, radius * 1.35f), Graphite);
            Prim(root, "LabMid", PrimitiveType.Cylinder,
                new Vector3(0f, z, 0f),
                new Vector3(radius * 2.08f, 0.28f, radius * 2.08f), Carbon, alongX);

            for (int s = -1; s <= 1; s += 2)
            {
                float x = s * (length * 0.34f);
                Prim(root, "LabCap_" + s, PrimitiveType.Cylinder,
                    new Vector3(x, z, 0f),
                    new Vector3(radius * 2.02f, 0.21f, radius * 2.02f), Carbon, alongX);
                Prim(root, "LabRing_" + s, PrimitiveType.Cylinder,
                    new Vector3(x + s * 0.28f, z, 0f),
                    new Vector3(radius * 2.16f, 0.04f, radius * 2.16f), Orange, alongX);
                Prim(root, "LabStripe_" + s, PrimitiveType.Cube,
                    new Vector3(x, z + radius * 0.35f, -radius * 0.15f),
                    new Vector3(0.10f, 0.85f, 0.12f), Orange);
                Prim(root, "LabDock_" + s, PrimitiveType.Cylinder,
                    new Vector3(s * (length * 0.48f), z, 0f),
                    new Vector3(1.04f, 0.19f, 1.04f), White, alongX);
                Prim(root, "LabFlange_" + s, PrimitiveType.Cylinder,
                    new Vector3(s * (length * 0.52f), z, 0f),
                    new Vector3(1.20f, 0.035f, 1.20f), Carbon, alongX);
            }

            Prim(root, "LabFront", PrimitiveType.Cylinder,
                new Vector3(-length * 0.50f, z, 0f),
                new Vector3(1.16f, 0.04f, 1.16f), Steel, alongX);
            Prim(root, "LabFrontSquare", PrimitiveType.Cube,
                new Vector3(-length * 0.54f, z, 0f),
                new Vector3(0.08f, 0.42f, 0.42f), White);
            Prim(root, "LabHatchFrame", PrimitiveType.Cube,
                new Vector3(0.15f, z, radius * 0.98f),
                new Vector3(0.72f, 0.72f, 0.08f), Carbon);
            Prim(root, "LabHatch", PrimitiveType.Cube,
                new Vector3(0.15f, z, radius * 0.94f),
                new Vector3(0.55f, 0.55f, 0.08f), Orange);
            Prim(root, "LabBay", PrimitiveType.Cube,
                new Vector3(0.85f, z + 0.15f, -radius * 0.55f),
                new Vector3(1.15f, 0.72f, 0.55f), Steel);
            for (int i = 0; i < 3; i++)
            {
                Prim(root, "LabSample_" + i, PrimitiveType.Cylinder,
                    new Vector3(0.55f + i * 0.22f, z + 0.55f, -radius * 0.55f),
                    new Vector3(0.14f, 0.16f, 0.14f), Ice, IceEmit);
            }
            Prim(root, "LabGrille", PrimitiveType.Cube,
                new Vector3(-0.35f, z + radius * 0.92f, 0.05f),
                new Vector3(0.85f, 0.08f, 0.42f), Carbon);
            Prim(root, "LabPipe", PrimitiveType.Cylinder,
                new Vector3(0.05f, 0.32f, 0f),
                new Vector3(0.10f, length * 0.28f, 0.10f), Steel, alongX);
            Prim(root, "LabMast", PrimitiveType.Cylinder,
                new Vector3(1.05f, z + radius + 0.55f, 0.12f),
                new Vector3(0.07f, 0.48f, 0.07f), Steel);
            Prim(root, "LabDish", PrimitiveType.Sphere,
                new Vector3(1.05f, z + radius + 1.05f, 0.12f),
                new Vector3(0.64f, 0.14f, 0.64f), White);
            Prim(root, "LabDishRing", PrimitiveType.Cylinder,
                new Vector3(1.05f, z + radius + 1.05f, 0.12f),
                new Vector3(0.68f, 0.02f, 0.68f), Orange);
            Prim(root, "LabLens", PrimitiveType.Sphere,
                new Vector3(1.05f, z + radius + 1.08f, 0.28f),
                new Vector3(0.14f, 0.14f, 0.14f), Cyan, CyanEmit);
            Prim(root, "LabSkid_L", PrimitiveType.Cube,
                new Vector3(0f, 0.10f, -1.05f),
                new Vector3(length * 0.62f, 0.18f, 0.32f), Carbon);
            Prim(root, "LabSkid_R", PrimitiveType.Cube,
                new Vector3(0f, 0.10f, 1.05f),
                new Vector3(length * 0.62f, 0.18f, 0.32f), Carbon);
        }

        public static void BuildClimateLoom(Transform root, float w, float d, Color hull)
        {
            // Weather lattice + cooling towers. Not a white cabin, not a HAB.
            Prim(root, "LoomPlinth", PrimitiveType.Cube,
                new Vector3(0f, 0.14f, 0f),
                new Vector3(w * 0.94f, 0.26f, d * 0.94f), Graphite);
            Prim(root, "LoomBunker", PrimitiveType.Cube,
                new Vector3(-w * 0.32f, 0.72f, -d * 0.30f),
                new Vector3(w * 0.28f, 1.15f, d * 0.28f), Carbon);
            Prim(root, "LoomBunkerCap", PrimitiveType.Cube,
                new Vector3(-w * 0.32f, 1.35f, -d * 0.30f),
                new Vector3(w * 0.32f, 0.10f, d * 0.32f), hull);
            Prim(root, "LoomHatch", PrimitiveType.Cube,
                new Vector3(-w * 0.32f, 0.72f, -d * 0.16f),
                new Vector3(0.55f, 0.72f, 0.08f), Orange);
            for (int i = 0; i < 4; i++)
            {
                float x = -w * 0.32f + i * w * 0.22f;
                Prim(root, "LoomPost_" + i, PrimitiveType.Cube,
                    new Vector3(x, 2.35f, d * 0.22f),
                    new Vector3(0.12f, 4.2f, 0.12f), Carbon);
                Prim(root, "LoomBrace_" + i, PrimitiveType.Cube,
                    new Vector3(x, 2.55f, d * 0.08f),
                    new Vector3(0.08f, 0.08f, d * 0.28f), Steel);
            }
            Prim(root, "LoomBoom", PrimitiveType.Cube,
                new Vector3(0.02f, 4.45f, d * 0.22f),
                new Vector3(w * 0.82f, 0.14f, 0.22f), Yellow);
            for (int i = 0; i < 5; i++)
            {
                float x = -w * 0.34f + i * w * 0.17f;
                Prim(root, "LoomNozzle_" + i, PrimitiveType.Cylinder,
                    new Vector3(x, 3.95f, d * 0.22f),
                    new Vector3(0.14f, 0.28f, 0.14f), Ice, IceEmit);
            }
            Prim(root, "LoomTower_L", PrimitiveType.Cylinder,
                new Vector3(w * 0.28f, 1.85f, -d * 0.22f),
                new Vector3(1.15f, 1.75f, 1.15f), Ice, IceEmit * 0.35f);
            Prim(root, "LoomTower_R", PrimitiveType.Cylinder,
                new Vector3(w * 0.28f, 1.35f, d * 0.08f),
                new Vector3(0.92f, 1.25f, 0.92f), Steel);
            Prim(root, "LoomBand", PrimitiveType.Cylinder,
                new Vector3(w * 0.28f, 2.35f, -d * 0.22f),
                new Vector3(1.28f, 0.08f, 1.28f), Orange);
            Prim(root, "LoomFlare", PrimitiveType.Cylinder,
                new Vector3(w * 0.28f, 3.55f, -d * 0.22f),
                new Vector3(0.55f, 0.22f, 0.55f), Carbon);
            ScaffoldTower(root, "LoomScaf", new Vector3(w * 0.18f, 0f, d * 0.32f), 4.8f, 0.95f);
            Prim(root, "LoomCondenser", PrimitiveType.Sphere,
                new Vector3(0.05f, 4.85f, d * 0.22f),
                new Vector3(0.55f, 0.28f, 0.55f), Ice, IceEmit);
            Prim(root, "LoomBeacon", PrimitiveType.Sphere,
                new Vector3(-w * 0.32f, 1.62f, -d * 0.30f),
                new Vector3(0.18f, 0.18f, 0.18f), Cyan, CyanEmit);
        }

        public static void BuildAegisSpire(Transform root, float w, float d, Color hull)
        {
            // Tapered shield monument + rings. Not a Commons citadel, not stacked boxes.
            Prim(root, "SpirePlinth", PrimitiveType.Cube,
                new Vector3(0f, 0.16f, 0f),
                new Vector3(w * 0.88f, 0.28f, d * 0.88f), Carbon);
            for (int i = 0; i < 4; i++)
            {
                float ang = (i * 90f + 45f) * Mathf.Deg2Rad;
                Vector3 p = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * (Mathf.Min(w, d) * 0.38f);
                Prim(root, "SpireButtress_" + i, PrimitiveType.Cube,
                    p + new Vector3(0f, 1.05f, 0f),
                    new Vector3(0.28f, 1.85f, 0.28f), Graphite,
                    Quaternion.Euler(0f, i * 90f + 45f, 0f));
                Prim(root, "SpireEmitter_" + i, PrimitiveType.Cylinder,
                    p + new Vector3(0f, 2.05f, 0f),
                    new Vector3(0.18f, 0.12f, 0.18f), Cyan, CyanEmit);
            }
            Prim(root, "SpireBase", PrimitiveType.Cube,
                new Vector3(0f, 1.25f, 0f),
                new Vector3(w * 0.36f, 2.15f, d * 0.36f), hull);
            Prim(root, "SpireMid", PrimitiveType.Cube,
                new Vector3(0f, 3.55f, 0f),
                new Vector3(w * 0.22f, 2.45f, d * 0.22f), hull);
            Prim(root, "SpireNeedle", PrimitiveType.Cylinder,
                new Vector3(0f, 5.85f, 0f),
                new Vector3(0.16f, 1.35f, 0.16f), Steel);
            Prim(root, "SpireChevron_0", PrimitiveType.Cube,
                new Vector3(0f, 1.85f, d * 0.19f),
                new Vector3(w * 0.18f, 0.12f, 0.08f), Orange);
            Prim(root, "SpireChevron_1", PrimitiveType.Cube,
                new Vector3(0f, 3.15f, d * 0.12f),
                new Vector3(w * 0.12f, 0.10f, 0.08f), Orange);
            Prim(root, "SpireChevron_2", PrimitiveType.Cube,
                new Vector3(0f, 4.45f, d * 0.12f),
                new Vector3(w * 0.08f, 0.08f, 0.08f), Orange);
            Prim(root, "SpireRing_0", PrimitiveType.Cylinder,
                new Vector3(0f, 2.25f, 0f),
                new Vector3(w * 0.78f, 0.05f, w * 0.78f), Cyan, CyanEmit * 0.45f);
            Prim(root, "SpireRing_1", PrimitiveType.Cylinder,
                new Vector3(0f, 3.85f, 0f),
                new Vector3(w * 0.52f, 0.05f, w * 0.52f), Cyan, CyanEmit * 0.45f);
            Prim(root, "SpireRing_2", PrimitiveType.Cylinder,
                new Vector3(0f, 5.25f, 0f),
                new Vector3(w * 0.32f, 0.04f, w * 0.32f), Cyan, CyanEmit * 0.45f);
            Prim(root, "SpireBeacon", PrimitiveType.Sphere,
                new Vector3(0f, 7.25f, 0f),
                new Vector3(0.28f, 0.28f, 0.28f), Cyan, CyanEmit);
        }

        public static void BuildDeepArchive(Transform root, float w, float d, Color hull)
        {
            // Buried data silos + blast door. Low vault, not a loom or spire.
            Quaternion alongX = Quaternion.Euler(0f, 0f, 90f);
            Prim(root, "ArchPlinth", PrimitiveType.Cube,
                new Vector3(0f, 0.10f, 0f),
                new Vector3(w * 0.94f, 0.18f, d * 0.94f), Carbon);
            Prim(root, "ArchLid", PrimitiveType.Cube,
                new Vector3(-w * 0.06f, 1.55f, -d * 0.08f),
                new Vector3(w * 0.62f, 0.14f, d * 0.58f), Graphite);
            Prim(root, "ArchCap", PrimitiveType.Cube,
                new Vector3(-w * 0.06f, 1.68f, -d * 0.08f),
                new Vector3(w * 0.52f, 0.08f, d * 0.48f), Carbon);

            float[] siloZ = { -d * 0.22f, 0.02f, d * 0.26f };
            float[] siloY = { 0.62f, 0.72f, 0.55f };
            for (int i = 0; i < 3; i++)
            {
                Prim(root, "ArchSilo_" + i, PrimitiveType.Cylinder,
                    new Vector3(-w * 0.08f, siloY[i], siloZ[i]),
                    new Vector3(0.72f, w * 0.32f, 0.72f), i == 1 ? hull : Steel, alongX);
                Prim(root, "ArchSiloBand_" + i, PrimitiveType.Cylinder,
                    new Vector3(-w * 0.08f, siloY[i], siloZ[i]),
                    new Vector3(0.80f, 0.05f, 0.80f), Orange, alongX);
            }

            Prim(root, "ArchDoorFrame", PrimitiveType.Cube,
                new Vector3(-w * 0.08f, 0.85f, d * 0.42f),
                new Vector3(w * 0.42f, 1.35f, 0.12f), Carbon);
            Prim(root, "ArchHatch", PrimitiveType.Cube,
                new Vector3(-w * 0.08f, 0.78f, d * 0.46f),
                new Vector3(w * 0.28f, 1.05f, 0.08f), Orange);
            Prim(root, "ArchStripe", PrimitiveType.Cube,
                new Vector3(-w * 0.08f, 0.42f, d * 0.44f),
                new Vector3(w * 0.48f, 0.10f, 0.08f), Orange);
            Prim(root, "ArchStack", PrimitiveType.Cube,
                new Vector3(w * 0.34f, 0.95f, -d * 0.22f),
                new Vector3(w * 0.18f, 1.65f, d * 0.28f), Steel);
            Prim(root, "ArchDish_0", PrimitiveType.Sphere,
                new Vector3(w * 0.34f, 2.05f, -d * 0.22f),
                new Vector3(0.92f, 0.16f, 0.92f), White);
            Prim(root, "ArchDish_1", PrimitiveType.Sphere,
                new Vector3(w * 0.22f, 1.85f, d * 0.18f),
                new Vector3(0.62f, 0.12f, 0.62f), Graphite);
            Prim(root, "ArchMast", PrimitiveType.Cylinder,
                new Vector3(-w * 0.06f, 2.25f, -d * 0.08f),
                new Vector3(0.08f, 0.55f, 0.08f), Steel);
            Prim(root, "ArchBeacon", PrimitiveType.Sphere,
                new Vector3(-w * 0.06f, 2.85f, -d * 0.08f),
                new Vector3(0.18f, 0.18f, 0.18f), Cyan, CyanEmit);
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
            const float h = 7.4f;
            Prim(root, "Dress_StarshipSkirt", PrimitiveType.Cylinder,
                new Vector3(0f, 0.55f, 0f),
                new Vector3(1.36f, 0.28f, 1.36f), Carbon);
            Prim(root, "Dress_StarshipBody", PrimitiveType.Cylinder,
                new Vector3(0f, 3.15f, 0f),
                new Vector3(1.04f, 2.6f, 1.04f), White);
            Prim(root, "Dress_StarshipHeat", PrimitiveType.Cube,
                new Vector3(0f, 2.85f, -0.42f),
                new Vector3(0.85f, 4.4f, 0.18f), Carbon);
            Prim(root, "Dress_StarshipBand", PrimitiveType.Cylinder,
                new Vector3(0f, 1.85f, 0f),
                new Vector3(1.12f, 0.05f, 1.12f), Carbon);
            Prim(root, "Dress_StarshipStripe", PrimitiveType.Cylinder,
                new Vector3(0f, 3.55f, 0f),
                new Vector3(1.12f, 0.05f, 1.12f), Orange);
            Prim(root, "Dress_StarshipBandHi", PrimitiveType.Cylinder,
                new Vector3(0f, 5.05f, 0f),
                new Vector3(1.12f, 0.05f, 1.12f), Orange);
            Prim(root, "Dress_StarshipNose", PrimitiveType.Sphere,
                new Vector3(0f, h * 0.90f, 0f),
                new Vector3(1.04f, 1.35f, 1.04f), White);
            Prim(root, "Dress_StarshipFin_L", PrimitiveType.Cube,
                new Vector3(0.08f, 1.05f, 0.58f),
                new Vector3(0.12f, 1.25f, 0.55f), Carbon);
            Prim(root, "Dress_StarshipFin_R", PrimitiveType.Cube,
                new Vector3(0.08f, 1.05f, -0.58f),
                new Vector3(0.12f, 1.25f, 0.55f), Carbon);
            Prim(root, "Dress_StarshipFlap_L", PrimitiveType.Cube,
                new Vector3(0.12f, 5.55f, 0.42f),
                new Vector3(0.08f, 0.72f, 0.38f), Carbon);
            Prim(root, "Dress_StarshipFlap_R", PrimitiveType.Cube,
                new Vector3(0.12f, 5.55f, -0.42f),
                new Vector3(0.08f, 0.72f, 0.38f), Carbon);
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
