using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Unit builders: prefer Blender SM_Unit_* FBX meshes, else industrial primitive fallbacks.
    /// Prefabs under Resources/Units wrap these for Play Mode.
    /// </summary>
    public static class UnitPlaceholderFactory
    {
        public static readonly Color ScoutTint = new Color(0.35f, 0.85f, 1f);
        public static readonly Color EngineerTint = new Color(1f, 0.55f, 0.15f);
        public static readonly Color DefenseTint = new Color(0.85f, 0.22f, 0.22f);
        public static readonly Color MedicTint = new Color(0.92f, 0.96f, 1f);
        public static readonly Color HarvesterTint = new Color(0.82f, 0.62f, 0.18f);
        public static readonly Color SurveyorTint = new Color(0.45f, 0.82f, 0.95f);
        public static readonly Color StalkerTint = new Color(0.42f, 0.07f, 0.1f);
        public static readonly Color MiteTint = new Color(0.62f, 0.48f, 0.32f);
        public static readonly Color LeechTint = new Color(0.88f, 0.90f, 0.92f);
        public static readonly Color BeltMiteTint = new Color(0.38f, 0.32f, 0.28f);
        public static readonly Color EuropaLeechTint = new Color(0.32f, 0.82f, 0.95f);
        public static readonly Color GeologistTint = new Color(0.68f, 0.52f, 0.32f);
        public static readonly Color SentinelTint = new Color(0.78f, 0.38f, 0.22f);
        public static readonly Color WispTint = new Color(0.62f, 0.88f, 0.96f);
        public static readonly Color TickTint = new Color(0.32f, 0.28f, 0.24f);
        public static readonly Color CreeperTint = new Color(0.38f, 0.48f, 0.20f);
        public static readonly Color HopperTint = new Color(0.52f, 0.50f, 0.46f);

        private static readonly Color WhiteShell = new Color(0.86f, 0.88f, 0.9f);
        private static readonly Color BlackBand = new Color(0.06f, 0.06f, 0.07f);
        private static readonly Color OrangeAccent = new Color(0.95f, 0.42f, 0.08f);
        private static readonly Color Steel = new Color(0.48f, 0.5f, 0.53f);

        public static GameObject BuildScout()
        {
            var mesh = UnitMeshCatalog.InstantiateClean(UnitMeshCatalog.LoadScout(), "Unit_ScoutDrone");
            if (mesh != null) return mesh;

            // Imagine LO-SCT-1 fuselage + hover rotors (not a Surveyor tripod).
            var root = new GameObject("Unit_ScoutDrone");
            Cylinder("Pad", root.transform, new Vector3(0f, 0.06f, 0f), new Vector3(0.48f, 0.05f, 0.48f), BlackBand);
            Capsule("Probe", root.transform, new Vector3(0f, 1.45f, 0f), new Vector3(0.36f, 0.78f, 0.36f), WhiteShell);
            Cylinder("BandLo", root.transform, new Vector3(0f, 1.12f, 0f), new Vector3(0.40f, 0.05f, 0.40f), BlackBand);
            Cylinder("BandMid", root.transform, new Vector3(0f, 1.48f, 0f), new Vector3(0.40f, 0.05f, 0.40f), BlackBand);
            Cylinder("Collar", root.transform, new Vector3(0f, 2.05f, 0.04f), new Vector3(0.32f, 0.06f, 0.32f), OrangeAccent);
            Cube("Head", root.transform, new Vector3(0f, 2.28f, 0.08f), new Vector3(0.28f, 0.24f, 0.32f), WhiteShell);
            Cube("Lens", root.transform, new Vector3(0f, 2.28f, 0.26f), new Vector3(0.16f, 0.14f, 0.06f), ScoutTint);
            Cylinder("Antenna", root.transform, new Vector3(0.08f, 2.72f, -0.06f), new Vector3(0.04f, 0.42f, 0.04f), Steel);
            Cube("Beacon", root.transform, new Vector3(0.16f, 1.88f, 0f), new Vector3(0.08f, 0.08f, 0.08f), OrangeAccent);
            for (int i = 0; i < 4; i++)
            {
                float ang = 45f + i * 90f;
                float rad = ang * Mathf.Deg2Rad;
                Vector3 p = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * 0.58f;
                Cylinder("Rotor_" + i, root.transform, p + new Vector3(0f, 0.98f, 0f), new Vector3(0.36f, 0.03f, 0.36f), BlackBand);
            }
            return root;
        }

        public static GameObject BuildEngineer()
        {
            var mesh = UnitMeshCatalog.InstantiateClean(UnitMeshCatalog.LoadEngineer(), "Unit_EngineerBot");
            if (mesh != null) return mesh;

            // Imagine v2 builder — white hull, crate pack, cyan visor, orange docks.
            var root = new GameObject("Unit_EngineerBot");
            Capsule("Body", root.transform, new Vector3(0f, 1.05f, 0.04f), new Vector3(1.05f, 0.95f, 0.92f), WhiteShell);
            Cylinder("Band", root.transform, new Vector3(0f, 0.82f, 0.04f), new Vector3(1.12f, 0.08f, 1.02f), BlackBand);
            Cube("Pack", root.transform, new Vector3(0f, 1.12f, -0.38f), new Vector3(0.48f, 0.58f, 0.28f), Steel);
            Cube("PackStripe", root.transform, new Vector3(0f, 1.12f, -0.54f), new Vector3(0.08f, 0.36f, 0.04f), OrangeAccent);
            Cube("Toolbox", root.transform, new Vector3(0.52f, 0.78f, 0.08f), new Vector3(0.28f, 0.22f, 0.22f), BlackBand);
            Cube("Visor", root.transform, new Vector3(0f, 1.58f, 0.28f), new Vector3(0.42f, 0.12f, 0.08f), ScoutTint);
            Cube("Dock", root.transform, new Vector3(0f, 1.12f, 0.42f), new Vector3(0.28f, 0.12f, 0.06f), OrangeAccent);
            Cube("BootL", root.transform, new Vector3(-0.18f, 0.08f, 0.12f), new Vector3(0.22f, 0.10f, 0.32f), BlackBand);
            Cube("BootR", root.transform, new Vector3(0.18f, 0.08f, 0.12f), new Vector3(0.22f, 0.10f, 0.32f), BlackBand);
            Cube("ArmL", root.transform, new Vector3(-0.62f, 1.12f, 0.12f), new Vector3(0.16f, 0.42f, 0.16f), Steel);
            Cube("ArmR", root.transform, new Vector3(0.62f, 1.12f, 0.12f), new Vector3(0.16f, 0.42f, 0.16f), Steel);
            Cube("StripeL", root.transform, new Vector3(-0.72f, 1.18f, 0.12f), new Vector3(0.04f, 0.22f, 0.12f), OrangeAccent);
            return root;
        }

        public static GameObject BuildDefense()
        {
            var mesh = UnitMeshCatalog.InstantiateClean(UnitMeshCatalog.LoadDefense(), "Unit_DefenseMech");
            if (mesh != null) return mesh;

            // Imagine Guardian — continuous treads, red viewport, massive shoulder pods.
            var root = new GameObject("Unit_DefenseMech");
            Cube("Belly", root.transform, new Vector3(0f, 0.42f, 0.02f), new Vector3(1.12f, 0.38f, 1.32f), BlackBand);
            Cube("Hull", root.transform, new Vector3(0f, 1.08f, -0.04f), new Vector3(1.18f, 0.78f, 1.22f), WhiteShell);
            Cube("Slope", root.transform, new Vector3(0f, 0.98f, 0.52f), new Vector3(0.95f, 0.58f, 0.32f), WhiteShell);
            Cube("Face", root.transform, new Vector3(0f, 1.05f, 0.68f), new Vector3(0.72f, 0.48f, 0.10f), BlackBand);
            Cube("Visor", root.transform, new Vector3(0f, 1.08f, 0.74f), new Vector3(0.48f, 0.28f, 0.06f), DefenseTint);
            Cube("Emblem", root.transform, new Vector3(0f, 1.42f, 0.62f), new Vector3(0.16f, 0.14f, 0.08f), OrangeAccent);
            Cube("ShoulderL", root.transform, new Vector3(-0.92f, 1.18f, 0.02f), new Vector3(0.58f, 0.72f, 0.88f), WhiteShell);
            Cube("ShoulderR", root.transform, new Vector3(0.92f, 1.18f, 0.02f), new Vector3(0.58f, 0.72f, 0.88f), WhiteShell);
            Cube("PortL", root.transform, new Vector3(-0.92f, 1.18f, 0.46f), new Vector3(0.28f, 0.32f, 0.06f), DefenseTint);
            Cube("PortR", root.transform, new Vector3(0.92f, 1.18f, 0.46f), new Vector3(0.28f, 0.32f, 0.06f), DefenseTint);
            Cube("HazL", root.transform, new Vector3(-0.92f, 1.55f, 0.18f), new Vector3(0.38f, 0.06f, 0.10f), OrangeAccent);
            Cube("HazR", root.transform, new Vector3(0.92f, 1.55f, 0.18f), new Vector3(0.38f, 0.06f, 0.10f), OrangeAccent);
            Cube("Turret", root.transform, new Vector3(0f, 1.68f, -0.06f), new Vector3(0.36f, 0.18f, 0.32f), BlackBand);
            Cube("TreadL", root.transform, new Vector3(-0.72f, 0.18f, 0.02f), new Vector3(0.42f, 0.28f, 1.58f), BlackBand);
            Cube("TreadR", root.transform, new Vector3(0.72f, 0.18f, 0.02f), new Vector3(0.42f, 0.28f, 1.58f), BlackBand);
            return root;
        }

        public static GameObject BuildMedic()
        {
            var mesh = UnitMeshCatalog.InstantiateClean(UnitMeshCatalog.LoadForClass(SpecialistClass.Medic), "Unit_Medic");
            if (mesh != null) return mesh;

            // LO-MED-1 hover capsule — white top / black belly, no rotors.
            var root = new GameObject("Unit_Medic");
            Cylinder("Pad", root.transform, new Vector3(0f, 0.04f, 0f), new Vector3(0.52f, 0.04f, 0.52f), BlackBand);
            Sphere("Hull", root.transform, new Vector3(0f, 0.72f, 0.04f), new Vector3(0.86f, 0.68f, 1.68f), WhiteShell);
            Sphere("Belly", root.transform, new Vector3(0f, 0.50f, 0.04f), new Vector3(0.74f, 0.32f, 1.46f), BlackBand);
            Cube("StripeL", root.transform, new Vector3(-0.30f, 0.52f, 0.72f), new Vector3(0.10f, 0.08f, 0.22f), OrangeAccent);
            Cube("StripeR", root.transform, new Vector3(0.30f, 0.52f, 0.72f), new Vector3(0.10f, 0.08f, 0.22f), OrangeAccent);
            Cube("Visor", root.transform, new Vector3(0f, 0.74f, 0.82f), new Vector3(0.48f, 0.08f, 0.04f), ScoutTint);
            Cube("CrossH", root.transform, new Vector3(0f, 1.08f, 0.06f), new Vector3(0.46f, 0.05f, 0.08f), ScoutTint);
            Cube("CrossV", root.transform, new Vector3(0f, 1.08f, 0.06f), new Vector3(0.08f, 0.05f, 0.46f), ScoutTint);
            Cylinder("IvPole", root.transform, new Vector3(-0.22f, 1.18f, -0.72f), new Vector3(0.04f, 0.30f, 0.04f), Steel);
            Sphere("IvBag", root.transform, new Vector3(-0.22f, 1.50f, -0.72f), new Vector3(0.12f, 0.16f, 0.10f), ScoutTint);
            for (int i = 0; i < 4; i++)
            {
                float x = i < 2 ? -0.38f : 0.38f;
                float z = i % 2 == 0 ? 0.52f : -0.52f;
                Cylinder("Thruster_" + i, root.transform, new Vector3(x, 0.18f, z), new Vector3(0.26f, 0.04f, 0.26f), ScoutTint);
            }
            return root;
        }

        public static GameObject BuildHarvester()
        {
            var mesh = UnitMeshCatalog.InstantiateClean(UnitMeshCatalog.LoadHarvester(), "Unit_HarvesterBot");
            if (mesh != null) return mesh;

            // LO-HAR-1 tracked hopper — orange blade, rear hopper, side arm.
            var root = new GameObject("Unit_HarvesterBot");
            Cube("Chassis", root.transform, new Vector3(0f, 0.42f, 0.02f), new Vector3(1.08f, 0.36f, 1.32f), BlackBand);
            Cube("Cab", root.transform, new Vector3(0f, 0.98f, 0.22f), new Vector3(0.82f, 0.62f, 0.72f), WhiteShell);
            Cube("Visor", root.transform, new Vector3(0f, 1.08f, 0.60f), new Vector3(0.58f, 0.16f, 0.05f), ScoutTint);
            Cube("Hopper", root.transform, new Vector3(0f, 1.05f, -0.62f), new Vector3(0.88f, 0.62f, 0.52f), Steel);
            Cube("HopLip", root.transform, new Vector3(0f, 1.38f, -0.62f), new Vector3(0.82f, 0.08f, 0.46f), OrangeAccent);
            Cube("Blade", root.transform, new Vector3(0f, 0.48f, 0.98f), new Vector3(1.18f, 0.58f, 0.12f), OrangeAccent);
            Cube("Arm", root.transform, new Vector3(-0.68f, 1.02f, 0.08f), new Vector3(0.10f, 0.10f, 0.42f), BlackBand);
            Cube("Bucket", root.transform, new Vector3(-0.68f, 0.42f, 0.58f), new Vector3(0.16f, 0.12f, 0.22f), OrangeAccent);
            Cube("TrackL", root.transform, new Vector3(-0.62f, 0.18f, 0.02f), new Vector3(0.28f, 0.28f, 1.42f), BlackBand);
            Cube("TrackR", root.transform, new Vector3(0.62f, 0.18f, 0.02f), new Vector3(0.28f, 0.28f, 1.42f), BlackBand);
            return root;
        }

        public static GameObject BuildSurveyor()
        {
            var mesh = UnitMeshCatalog.InstantiateClean(UnitMeshCatalog.LoadSurveyor(), "Unit_SurveyorBot");
            if (mesh != null) return mesh;

            // LO-SRV-1 tripod mast — not a Scout hover, not a six-wheel rover.
            var root = new GameObject("Unit_SurveyorBot");
            Cylinder("Body", root.transform, new Vector3(0f, 0.92f, 0f), new Vector3(0.60f, 0.29f, 0.60f), WhiteShell);
            Cylinder("Band", root.transform, new Vector3(0f, 0.78f, 0f), new Vector3(0.68f, 0.04f, 0.68f), BlackBand);
            Cylinder("Mast", root.transform, new Vector3(0f, 1.82f, 0f), new Vector3(0.08f, 0.64f, 0.08f), Steel);
            Sphere("Dish", root.transform, new Vector3(0f, 2.48f, 0f), new Vector3(0.84f, 0.14f, 0.84f), WhiteShell);
            Cube("Lens", root.transform, new Vector3(0f, 0.98f, 0.32f), new Vector3(0.16f, 0.12f, 0.06f), ScoutTint);
            Sphere("Cluster", root.transform, new Vector3(0f, 2.55f, 0.08f), new Vector3(0.16f, 0.16f, 0.16f), ScoutTint);
            Cube("FootA", root.transform, new Vector3(0.78f, 0.04f, 0.28f), new Vector3(0.22f, 0.06f, 0.22f), BlackBand);
            Cube("FootB", root.transform, new Vector3(-0.62f, 0.04f, 0.52f), new Vector3(0.22f, 0.06f, 0.22f), BlackBand);
            Cube("FootC", root.transform, new Vector3(-0.14f, 0.04f, -0.80f), new Vector3(0.22f, 0.06f, 0.22f), BlackBand);
            Cube("LegA", root.transform, new Vector3(0.38f, 0.42f, 0.14f), new Vector3(0.08f, 0.55f, 0.08f), Steel);
            Cube("LegB", root.transform, new Vector3(-0.30f, 0.42f, 0.26f), new Vector3(0.08f, 0.55f, 0.08f), Steel);
            Cube("LegC", root.transform, new Vector3(-0.08f, 0.42f, -0.40f), new Vector3(0.08f, 0.55f, 0.08f), Steel);
            return root;
        }

        public static GameObject BuildDustStalker()
        {
            var mesh = UnitMeshCatalog.InstantiateClean(UnitMeshCatalog.LoadStalker(), "Unit_DustStalker");
            if (mesh != null) return mesh;

            // Imagine predator — four eyes, wrapping bone plates, serrated spine, 3-toe feet.
            var root = new GameObject("Unit_DustStalker");
            Sphere("Body", root.transform, new Vector3(0f, 0.50f, 0.06f), new Vector3(1.22f, 0.78f, 2.65f), StalkerTint);
            Sphere("Head", root.transform, new Vector3(0f, 0.58f, 1.32f), new Vector3(0.52f, 0.48f, 0.82f), StalkerTint * 1.15f);
            Cube("Plate", root.transform, new Vector3(0f, 0.82f, 0.12f), new Vector3(0.72f, 0.18f, 1.15f), WhiteShell);
            Cube("Spine", root.transform, new Vector3(0f, 1.12f, 0.08f), new Vector3(0.14f, 0.62f, 1.65f), BlackBand);
            Cube("Tail", root.transform, new Vector3(0f, 0.40f, -1.55f), new Vector3(0.32f, 0.24f, 1.05f), StalkerTint);
            Sphere("EyeL", root.transform, new Vector3(-0.14f, 0.70f, 1.50f), new Vector3(0.12f, 0.10f, 0.10f), OrangeAccent);
            Sphere("EyeR", root.transform, new Vector3(0.14f, 0.70f, 1.50f), new Vector3(0.12f, 0.10f, 0.10f), OrangeAccent);
            Sphere("EyeLo", root.transform, new Vector3(-0.22f, 0.62f, 1.42f), new Vector3(0.09f, 0.08f, 0.08f), OrangeAccent);
            Sphere("EyeRo", root.transform, new Vector3(0.22f, 0.62f, 1.42f), new Vector3(0.09f, 0.08f, 0.08f), OrangeAccent);
            Cube("BracerL", root.transform, new Vector3(-0.55f, 0.18f, 0.55f), new Vector3(0.18f, 0.14f, 0.18f), WhiteShell);
            Cube("BracerR", root.transform, new Vector3(0.55f, 0.18f, 0.55f), new Vector3(0.18f, 0.14f, 0.18f), WhiteShell);
            Cube("LegFL", root.transform, new Vector3(-0.55f, 0.22f, 0.55f), new Vector3(0.14f, 0.42f, 0.14f), BlackBand);
            Cube("LegFR", root.transform, new Vector3(0.55f, 0.22f, 0.55f), new Vector3(0.14f, 0.42f, 0.14f), BlackBand);
            Cube("LegBL", root.transform, new Vector3(-0.50f, 0.22f, -0.50f), new Vector3(0.14f, 0.42f, 0.14f), BlackBand);
            Cube("LegBR", root.transform, new Vector3(0.50f, 0.22f, -0.50f), new Vector3(0.14f, 0.42f, 0.14f), BlackBand);
            return root;
        }

        public static GameObject BuildRegolithMite()
        {
            var mesh = UnitMeshCatalog.InstantiateClean(UnitMeshCatalog.LoadMite(), "Unit_RegolithMite");
            if (mesh != null) return mesh;

            var root = new GameObject("Unit_RegolithMite");
            Sphere("Body", root.transform, new Vector3(0f, 0.20f, 0.02f), new Vector3(0.46f, 0.34f, 0.82f), MiteTint);
            Cube("Plate", root.transform, new Vector3(0f, 0.36f, 0f), new Vector3(0.42f, 0.08f, 0.55f), new Color(0.14f, 0.14f, 0.16f));
            Cube("Mandible", root.transform, new Vector3(0f, 0.16f, 0.42f), new Vector3(0.16f, 0.08f, 0.14f), BlackBand);
            Sphere("Eye", root.transform, new Vector3(0f, 0.24f, 0.44f), new Vector3(0.09f, 0.09f, 0.09f), ScoutTint);
            Cube("LegFL", root.transform, new Vector3(-0.22f, 0.08f, 0.22f), new Vector3(0.16f, 0.08f, 0.08f), BlackBand);
            Cube("LegFR", root.transform, new Vector3(0.22f, 0.08f, 0.22f), new Vector3(0.16f, 0.08f, 0.08f), BlackBand);
            Cube("LegML", root.transform, new Vector3(-0.24f, 0.08f, 0f), new Vector3(0.16f, 0.08f, 0.08f), BlackBand);
            Cube("LegMR", root.transform, new Vector3(0.24f, 0.08f, 0f), new Vector3(0.16f, 0.08f, 0.08f), BlackBand);
            Cube("LegBL", root.transform, new Vector3(-0.20f, 0.08f, -0.22f), new Vector3(0.16f, 0.08f, 0.08f), BlackBand);
            Cube("LegBR", root.transform, new Vector3(0.20f, 0.08f, -0.22f), new Vector3(0.16f, 0.08f, 0.08f), BlackBand);
            return root;
        }

        public static GameObject BuildWattLeech()
        {
            var mesh = UnitMeshCatalog.InstantiateClean(UnitMeshCatalog.LoadLeech(), "Unit_WattLeech");
            if (mesh != null) return mesh;

            var root = new GameObject("Unit_WattLeech");
            Sphere("Body", root.transform, new Vector3(0f, 0.16f, 0f), new Vector3(0.92f, 0.28f, 1.50f), LeechTint);
            Cube("Groove", root.transform, new Vector3(0f, 0.28f, 0.04f), new Vector3(0.10f, 0.05f, 1.22f), ScoutTint);
            Cube("MandibleL", root.transform, new Vector3(-0.10f, 0.12f, 0.78f), new Vector3(0.08f, 0.08f, 0.18f), WhiteShell);
            Cube("MandibleR", root.transform, new Vector3(0.10f, 0.12f, 0.78f), new Vector3(0.08f, 0.08f, 0.18f), WhiteShell);
            Sphere("NubL", root.transform, new Vector3(-0.12f, 0.22f, 0.62f), new Vector3(0.07f, 0.07f, 0.07f), OrangeAccent);
            Sphere("NubR", root.transform, new Vector3(0.12f, 0.22f, 0.62f), new Vector3(0.07f, 0.07f, 0.07f), OrangeAccent);
            Cube("FinFL", root.transform, new Vector3(-0.48f, 0.14f, 0.32f), new Vector3(0.28f, 0.04f, 0.18f), WhiteShell);
            Cube("FinFR", root.transform, new Vector3(0.48f, 0.14f, 0.32f), new Vector3(0.28f, 0.04f, 0.18f), WhiteShell);
            Cube("DiscL", root.transform, new Vector3(-0.38f, 0.08f, 0f), new Vector3(0.14f, 0.04f, 0.14f), BlackBand);
            Cube("DiscR", root.transform, new Vector3(0.38f, 0.08f, 0f), new Vector3(0.14f, 0.04f, 0.14f), BlackBand);
            return root;
        }

        public static GameObject BuildTerraformer()
        {
            var mesh = UnitMeshCatalog.InstantiateClean(UnitMeshCatalog.LoadTerraformer(), "Unit_TerraformerBot");
            if (mesh != null) return mesh;

            // LO-TRF-1 tracked dozer — orange blade + rear rake, not a hopper scoop.
            var root = new GameObject("Unit_TerraformerBot");
            Cube("Chassis", root.transform, new Vector3(0f, 0.72f, 0.02f), new Vector3(1.18f, 0.48f, 1.68f), WhiteShell);
            Cube("Belly", root.transform, new Vector3(0f, 0.40f, 0.02f), new Vector3(1.28f, 0.16f, 1.78f), BlackBand);
            Cube("Cab", root.transform, new Vector3(0f, 1.28f, 0.38f), new Vector3(0.78f, 0.52f, 0.58f), WhiteShell);
            Cube("Blade", root.transform, new Vector3(0f, 0.58f, 1.22f), new Vector3(1.55f, 0.78f, 0.12f), OrangeAccent);
            Cube("Rake", root.transform, new Vector3(0f, 0.42f, -1.12f), new Vector3(2.05f, 0.10f, 0.10f), OrangeAccent);
            Cube("TineL", root.transform, new Vector3(-0.72f, 0.22f, -1.18f), new Vector3(0.06f, 0.28f, 0.06f), OrangeAccent);
            Cube("TineC", root.transform, new Vector3(0f, 0.22f, -1.18f), new Vector3(0.06f, 0.28f, 0.06f), OrangeAccent);
            Cube("TineR", root.transform, new Vector3(0.72f, 0.22f, -1.18f), new Vector3(0.06f, 0.28f, 0.06f), OrangeAccent);
            Cube("Tanks", root.transform, new Vector3(0f, 1.22f, -0.48f), new Vector3(0.92f, 0.42f, 0.72f), WhiteShell);
            Cube("Beacon", root.transform, new Vector3(0f, 1.62f, 0.28f), new Vector3(0.10f, 0.10f, 0.10f), OrangeAccent);
            Cube("Visor", root.transform, new Vector3(0f, 1.38f, 0.68f), new Vector3(0.62f, 0.16f, 0.05f), ScoutTint);
            Cube("TrackL", root.transform, new Vector3(-0.68f, 0.20f, 0.02f), new Vector3(0.32f, 0.32f, 1.52f), BlackBand);
            Cube("TrackR", root.transform, new Vector3(0.68f, 0.20f, 0.02f), new Vector3(0.32f, 0.32f, 1.52f), BlackBand);
            return root;
        }

        public static GameObject BuildCourier()
        {
            var mesh = UnitMeshCatalog.InstantiateClean(UnitMeshCatalog.LoadCourier(), "Unit_CourierBot");
            if (mesh != null) return mesh;

            // LO-COU-1 six-wheel hauler — white crate, orange corners, no drill.
            var root = new GameObject("Unit_CourierBot");
            Cube("Chassis", root.transform, new Vector3(0f, 0.50f, 0.02f), new Vector3(0.78f, 0.32f, 1.58f), WhiteShell);
            Cube("Belly", root.transform, new Vector3(0f, 0.26f, 0.02f), new Vector3(0.70f, 0.16f, 1.48f), BlackBand);
            Cube("Crate", root.transform, new Vector3(0f, 1.00f, -0.28f), new Vector3(0.80f, 0.78f, 0.92f), WhiteShell);
            Cube("Corner", root.transform, new Vector3(0.38f, 1.28f, -0.70f), new Vector3(0.10f, 0.10f, 0.10f), OrangeAccent);
            Cube("Cab", root.transform, new Vector3(0f, 0.82f, 0.68f), new Vector3(0.64f, 0.44f, 0.42f), WhiteShell);
            Cube("Grille", root.transform, new Vector3(0f, 0.62f, 0.90f), new Vector3(0.22f, 0.18f, 0.04f), BlackBand);
            Cube("Visor", root.transform, new Vector3(0f, 0.92f, 0.90f), new Vector3(0.50f, 0.10f, 0.04f), ScoutTint);
            Cube("Beacon", root.transform, new Vector3(0f, 1.12f, 0.58f), new Vector3(0.10f, 0.10f, 0.10f), OrangeAccent);
            Cylinder("Antenna", root.transform, new Vector3(0.22f, 1.38f, 0.58f), new Vector3(0.04f, 0.40f, 0.04f), Steel);
            for (int i = 0; i < 3; i++)
            {
                float z = -0.58f + i * 0.60f;
                var wl = Cylinder("WheelL_" + i, root.transform, new Vector3(-0.46f, 0.18f, z), new Vector3(0.32f, 0.10f, 0.32f), BlackBand);
                wl.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                var wr = Cylinder("WheelR_" + i, root.transform, new Vector3(0.46f, 0.18f, z), new Vector3(0.32f, 0.10f, 0.32f), BlackBand);
                wr.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            return root;
        }

        public static GameObject BuildGeologist()
        {
            var mesh = UnitMeshCatalog.InstantiateClean(UnitMeshCatalog.LoadGeologist(), "Unit_GeologistBot");
            if (mesh != null) return mesh;

            // LO-GEO-1 drill rover — vertical bit through orange collar, small crate.
            var root = new GameObject("Unit_GeologistBot");
            Cube("Chassis", root.transform, new Vector3(0f, 0.44f, 0.04f), new Vector3(0.72f, 0.28f, 1.38f), WhiteShell);
            Cube("Belly", root.transform, new Vector3(0f, 0.24f, 0.04f), new Vector3(0.62f, 0.14f, 1.22f), BlackBand);
            Cube("Crate", root.transform, new Vector3(0f, 0.62f, -0.58f), new Vector3(0.46f, 0.26f, 0.36f), Steel);
            Cube("StripeL", root.transform, new Vector3(-0.12f, 0.52f, 0.78f), new Vector3(0.05f, 0.22f, 0.04f), OrangeAccent);
            Cube("StripeR", root.transform, new Vector3(0.12f, 0.52f, 0.78f), new Vector3(0.05f, 0.22f, 0.04f), OrangeAccent);
            Cylinder("Mast", root.transform, new Vector3(-0.16f, 0.92f, 0.18f), new Vector3(0.06f, 0.32f, 0.06f), Steel);
            Sphere("Sensor", root.transform, new Vector3(-0.16f, 1.26f, 0.18f), new Vector3(0.16f, 0.16f, 0.16f), ScoutTint);
            Cube("DrillArm", root.transform, new Vector3(0f, 0.92f, 0.72f), new Vector3(0.10f, 0.36f, 0.12f), Steel);
            Cylinder("Collar", root.transform, new Vector3(0f, 0.68f, 0.72f), new Vector3(0.22f, 0.08f, 0.22f), OrangeAccent);
            Cylinder("Bit", root.transform, new Vector3(0f, 0.38f, 0.72f), new Vector3(0.08f, 0.22f, 0.08f), Steel);
            Cylinder("Vial", root.transform, new Vector3(-0.12f, 0.90f, -0.50f), new Vector3(0.06f, 0.08f, 0.06f), ScoutTint);
            for (int i = 0; i < 3; i++)
            {
                float z = -0.58f + i * 0.62f;
                var wl = Cylinder("WheelL_" + i, root.transform, new Vector3(-0.44f, 0.16f, z), new Vector3(0.28f, 0.08f, 0.28f), BlackBand);
                wl.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                var wr = Cylinder("WheelR_" + i, root.transform, new Vector3(0.44f, 0.16f, z), new Vector3(0.28f, 0.08f, 0.28f), BlackBand);
                wr.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            return root;
        }

        public static GameObject BuildSentinel()
        {
            var mesh = UnitMeshCatalog.InstantiateClean(UnitMeshCatalog.LoadSentinel(), "Unit_SentinelMech");
            if (mesh != null) return mesh;

            // LO-SEN-1 tracked turret — continuous treads, cyan visor, no red viewport.
            var root = new GameObject("Unit_SentinelMech");
            Cube("Hull", root.transform, new Vector3(0f, 0.72f, 0.02f), new Vector3(1.12f, 0.48f, 1.28f), WhiteShell);
            Cube("Skirt", root.transform, new Vector3(0f, 0.38f, 0.02f), new Vector3(1.22f, 0.18f, 1.38f), BlackBand);
            Cylinder("Turret", root.transform, new Vector3(0f, 1.28f, 0.08f), new Vector3(0.48f, 0.12f, 0.38f), WhiteShell);
            Cube("BarrelL", root.transform, new Vector3(-0.12f, 1.28f, 0.52f), new Vector3(0.10f, 0.10f, 0.72f), BlackBand);
            Cube("BarrelR", root.transform, new Vector3(0.12f, 1.28f, 0.52f), new Vector3(0.10f, 0.10f, 0.72f), BlackBand);
            Cube("LensL", root.transform, new Vector3(-0.12f, 1.28f, 0.90f), new Vector3(0.08f, 0.08f, 0.08f), ScoutTint);
            Cube("LensR", root.transform, new Vector3(0.12f, 1.28f, 0.90f), new Vector3(0.08f, 0.08f, 0.08f), ScoutTint);
            Cube("ChevronL", root.transform, new Vector3(-0.16f, 1.00f, 0.18f), new Vector3(0.42f, 0.05f, 0.08f), OrangeAccent);
            Cube("ChevronR", root.transform, new Vector3(0.16f, 1.00f, 0.18f), new Vector3(0.42f, 0.05f, 0.08f), OrangeAccent);
            Cube("Visor", root.transform, new Vector3(0f, 0.78f, 0.66f), new Vector3(0.72f, 0.08f, 0.05f), ScoutTint);
            Cube("TrackL", root.transform, new Vector3(-0.62f, 0.18f, 0.02f), new Vector3(0.30f, 0.28f, 1.42f), BlackBand);
            Cube("TrackR", root.transform, new Vector3(0.62f, 0.18f, 0.02f), new Vector3(0.30f, 0.28f, 1.42f), BlackBand);
            return root;
        }

        public static GameObject BuildIceWisp()
        {
            var mesh = UnitMeshCatalog.InstantiateClean(UnitMeshCatalog.LoadWisp(), "Unit_IceWisp");
            if (mesh != null) return mesh;

            var root = new GameObject("Unit_IceWisp");
            Cylinder("Pad", root.transform, new Vector3(0f, 0.03f, 0f), new Vector3(0.48f, 0.03f, 0.48f), ScoutTint);
            Sphere("Core", root.transform, new Vector3(0f, 1.00f, 0f), new Vector3(0.24f, 0.24f, 0.24f), ScoutTint);
            Cylinder("Hub", root.transform, new Vector3(0f, 1.00f, 0f), new Vector3(0.44f, 0.08f, 0.44f), BlackBand);
            Cube("ShardA", root.transform, new Vector3(0f, 1.00f, 0.52f), new Vector3(0.08f, 0.08f, 0.62f), WispTint);
            Cube("ShardB", root.transform, new Vector3(0.48f, 1.00f, 0.18f), new Vector3(0.62f, 0.08f, 0.08f), WispTint);
            Cube("ShardC", root.transform, new Vector3(0.32f, 1.00f, -0.40f), new Vector3(0.08f, 0.08f, 0.52f), WispTint);
            Cube("ShardD", root.transform, new Vector3(-0.32f, 1.00f, -0.40f), new Vector3(0.08f, 0.08f, 0.52f), WispTint);
            Cube("ShardE", root.transform, new Vector3(-0.48f, 1.00f, 0.18f), new Vector3(0.62f, 0.08f, 0.08f), WispTint);
            Cube("ShardF", root.transform, new Vector3(0.22f, 1.00f, 0.42f), new Vector3(0.08f, 0.08f, 0.42f), WispTint);
            Cube("ShardG", root.transform, new Vector3(-0.22f, 1.00f, 0.42f), new Vector3(0.08f, 0.08f, 0.42f), WispTint);
            Sphere("Nub", root.transform, new Vector3(0.18f, 1.06f, 0.28f), new Vector3(0.07f, 0.07f, 0.07f), OrangeAccent);
            return root;
        }

        public static GameObject BuildRockTick()
        {
            var mesh = UnitMeshCatalog.InstantiateClean(UnitMeshCatalog.LoadTick(), "Unit_RockTick");
            if (mesh != null) return mesh;

            var root = new GameObject("Unit_RockTick");
            Sphere("Body", root.transform, new Vector3(0f, 0.22f, 0f), new Vector3(0.82f, 0.28f, 0.46f), TickTint);
            Cube("Spike", root.transform, new Vector3(0f, 0.48f, -0.04f), new Vector3(0.12f, 0.28f, 0.12f), Steel);
            Cube("PincerL", root.transform, new Vector3(-0.18f, 0.2f, 0.42f), new Vector3(0.08f, 0.08f, 0.22f), BlackBand);
            Cube("TipL", root.transform, new Vector3(-0.18f, 0.2f, 0.58f), new Vector3(0.08f, 0.08f, 0.12f), OrangeAccent);
            Cube("PincerR", root.transform, new Vector3(0.18f, 0.2f, 0.42f), new Vector3(0.08f, 0.08f, 0.22f), BlackBand);
            Cube("TipR", root.transform, new Vector3(0.18f, 0.2f, 0.58f), new Vector3(0.08f, 0.08f, 0.12f), OrangeAccent);
            Cube("LegL", root.transform, new Vector3(-0.52f, 0.1f, 0.05f), new Vector3(0.32f, 0.08f, 0.08f), BlackBand);
            Cube("LegR", root.transform, new Vector3(0.52f, 0.1f, 0.05f), new Vector3(0.32f, 0.08f, 0.08f), BlackBand);
            return root;
        }

        public static GameObject BuildSoilCreeper()
        {
            var mesh = UnitMeshCatalog.InstantiateClean(UnitMeshCatalog.LoadCreeper(), "Unit_SoilCreeper");
            if (mesh != null) return mesh;

            var root = new GameObject("Unit_SoilCreeper");
            Sphere("SegA", root.transform, new Vector3(0f, 0.18f, 0.78f), new Vector3(0.32f, 0.24f, 0.32f), Steel);
            Sphere("SegOlive", root.transform, new Vector3(0f, 0.18f, 0.42f), new Vector3(0.3f, 0.22f, 0.3f), CreeperTint);
            Sphere("SegC", root.transform, new Vector3(0f, 0.16f, 0.06f), new Vector3(0.28f, 0.2f, 0.28f), Steel);
            Sphere("SegD", root.transform, new Vector3(0f, 0.14f, -0.32f), new Vector3(0.26f, 0.18f, 0.26f), Steel);
            Sphere("SegE", root.transform, new Vector3(0f, 0.14f, -0.68f), new Vector3(0.24f, 0.16f, 0.24f), Steel);
            Cube("Plate", root.transform, new Vector3(0f, 0.32f, 0.12f), new Vector3(0.22f, 0.06f, 1.35f), Steel);
            Cube("CerciL", root.transform, new Vector3(-0.06f, 0.12f, -0.95f), new Vector3(0.06f, 0.06f, 0.22f), OrangeAccent);
            Cube("CerciR", root.transform, new Vector3(0.06f, 0.12f, -0.95f), new Vector3(0.06f, 0.06f, 0.22f), OrangeAccent);
            Sphere("EyeL", root.transform, new Vector3(-0.08f, 0.24f, 1.05f), new Vector3(0.08f, 0.08f, 0.08f), ScoutTint);
            Sphere("EyeR", root.transform, new Vector3(0.08f, 0.24f, 1.05f), new Vector3(0.08f, 0.08f, 0.08f), ScoutTint);
            return root;
        }

        public static GameObject BuildAshHopper()
        {
            var mesh = UnitMeshCatalog.InstantiateClean(UnitMeshCatalog.LoadHopper(), "Unit_AshHopper");
            if (mesh != null) return mesh;

            var root = new GameObject("Unit_AshHopper");
            Sphere("Body", root.transform, new Vector3(0f, 1.42f, 0.08f), new Vector3(0.46f, 0.42f, 0.60f), HopperTint);
            Cube("LegFL", root.transform, new Vector3(-0.42f, 0.72f, 0.42f), new Vector3(0.06f, 1.35f, 0.06f), BlackBand);
            Cube("LegFR", root.transform, new Vector3(0.42f, 0.72f, 0.42f), new Vector3(0.06f, 1.35f, 0.06f), BlackBand);
            Cube("LegML", root.transform, new Vector3(-0.58f, 0.70f, 0.04f), new Vector3(0.06f, 1.28f, 0.06f), BlackBand);
            Cube("LegMR", root.transform, new Vector3(0.58f, 0.70f, 0.04f), new Vector3(0.06f, 1.28f, 0.06f), BlackBand);
            Cube("LegBL", root.transform, new Vector3(-0.44f, 0.66f, -0.38f), new Vector3(0.06f, 1.22f, 0.06f), BlackBand);
            Cube("LegBR", root.transform, new Vector3(0.44f, 0.66f, -0.36f), new Vector3(0.06f, 1.22f, 0.06f), BlackBand);
            Sphere("EyeL", root.transform, new Vector3(-0.1f, 1.48f, 0.28f), new Vector3(0.1f, 0.1f, 0.1f), ScoutTint);
            Sphere("EyeR", root.transform, new Vector3(0.1f, 1.48f, 0.28f), new Vector3(0.1f, 0.1f, 0.1f), ScoutTint);
            Cube("Knee", root.transform, new Vector3(0.48f, 0.62f, 0.04f), new Vector3(0.1f, 0.1f, 0.1f), OrangeAccent);
            Cube("Face", root.transform, new Vector3(0f, 1.36f, 0.32f), new Vector3(0.20f, 0.06f, 0.04f), OrangeAccent);
            return root;
        }

        public static GameObject BuildFauna(FaunaKind kind)
        {
            switch (kind)
            {
                case FaunaKind.Mite: return BuildRegolithMite();
                case FaunaKind.Leech: return BuildWattLeech();
                case FaunaKind.Wisp: return BuildIceWisp();
                case FaunaKind.Tick: return BuildRockTick();
                case FaunaKind.Creeper: return BuildSoilCreeper();
                case FaunaKind.Hopper: return BuildAshHopper();
                default: return BuildDustStalker();
            }
        }

        public static GameObject BuildForClass(SpecialistClass cls)
        {
            switch (cls)
            {
                case SpecialistClass.ScoutDrone: return BuildScout();
                case SpecialistClass.EngineerBot: return BuildEngineer();
                case SpecialistClass.DefenseMech: return BuildDefense();
                case SpecialistClass.Medic: return BuildMedic();
                case SpecialistClass.HarvesterBot: return BuildHarvester();
                case SpecialistClass.SurveyorBot: return BuildSurveyor();
                case SpecialistClass.TerraformerBot: return BuildTerraformer();
                case SpecialistClass.CourierBot: return BuildCourier();
                case SpecialistClass.GeologistBot: return BuildGeologist();
                case SpecialistClass.SentinelMech: return BuildSentinel();
                default: return BuildScout();
            }
        }

        private static void StripCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col == null) return;
            if (Application.isPlaying) Object.Destroy(col);
            else Object.DestroyImmediate(col);
        }

        private static GameObject Capsule(string name, Transform parent, Vector3 localPos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            StripCollider(go);
            Tint(go, color);
            return go;
        }

        private static GameObject Sphere(string name, Transform parent, Vector3 localPos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            StripCollider(go);
            Tint(go, color);
            return go;
        }

        private static GameObject Cube(string name, Transform parent, Vector3 localPos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            StripCollider(go);
            Tint(go, color);
            return go;
        }

        private static GameObject Cylinder(string name, Transform parent, Vector3 localPos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            StripCollider(go);
            Tint(go, color);
            return go;
        }

        private static void Tint(GameObject go, Color color)
        {
            var rend = go.GetComponent<Renderer>();
            if (rend == null) return;

            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) lit = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (lit != null)
            {
                var mat = new Material(lit) { name = go.name + "_Mat" };
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                if (mat.HasProperty("_Color")) mat.color = color;
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.42f);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.15f);
                rend.sharedMaterial = mat;
                return;
            }

            var fallback = rend.material;
            if (fallback.HasProperty("_BaseColor"))
                fallback.SetColor("_BaseColor", color);
            else if (fallback.HasProperty("_Color"))
                fallback.color = color;
        }
    }
}
