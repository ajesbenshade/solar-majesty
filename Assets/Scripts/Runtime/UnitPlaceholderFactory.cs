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
        public static readonly Color MiteTint = new Color(0.72f, 0.48f, 0.18f);
        public static readonly Color LeechTint = new Color(0.18f, 0.78f, 0.82f);
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

            // Hovering probe — white hull, four rotors, cyan sensor, whip antenna.
            var root = new GameObject("Unit_ScoutDrone");
            Cylinder("Pad", root.transform, new Vector3(0f, 0.06f, 0f), new Vector3(0.45f, 0.04f, 0.45f), BlackBand);
            Capsule("Probe", root.transform, new Vector3(0f, 1.35f, 0f), new Vector3(0.32f, 0.7f, 0.32f), WhiteShell);
            Cylinder("Band", root.transform, new Vector3(0f, 1.15f, 0f), new Vector3(0.38f, 0.06f, 0.38f), BlackBand);
            Sphere("Sensor", root.transform, new Vector3(0f, 1.95f, 0.08f), new Vector3(0.28f, 0.28f, 0.28f), WhiteShell);
            Cube("Visor", root.transform, new Vector3(0f, 1.95f, 0.18f), new Vector3(0.18f, 0.08f, 0.06f), ScoutTint);
            Cylinder("Antenna", root.transform, new Vector3(0.1f, 2.45f, 0f), new Vector3(0.04f, 0.4f, 0.04f), Steel);
            Cube("Beacon", root.transform, new Vector3(-0.16f, 1.72f, 0f), new Vector3(0.08f, 0.08f, 0.08f), OrangeAccent);
            for (int i = 0; i < 4; i++)
            {
                float ang = 45f + i * 90f;
                float rad = ang * Mathf.Deg2Rad;
                Vector3 p = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * 0.55f;
                Cylinder("Rotor_" + i, root.transform, p + new Vector3(0f, 0.95f, 0f), new Vector3(0.32f, 0.03f, 0.32f), BlackBand);
            }
            return root;
        }

        public static GameObject BuildEngineer()
        {
            var mesh = UnitMeshCatalog.InstantiateClean(UnitMeshCatalog.LoadEngineer(), "Unit_EngineerBot");
            if (mesh != null) return mesh;

            // Squat builder — orange shell, toolbox, orange service stripe.
            var root = new GameObject("Unit_EngineerBot");
            Capsule("Body", root.transform, new Vector3(0f, 0.95f, 0f), new Vector3(0.95f, 0.9f, 0.95f), EngineerTint);
            Cylinder("Band", root.transform, new Vector3(0f, 0.85f, 0f), new Vector3(1.05f, 0.1f, 1.05f), BlackBand);
            Cube("Toolbox", root.transform, new Vector3(0.78f, 0.85f, 0f), new Vector3(0.5f, 0.38f, 0.42f), BlackBand);
            Cube("Stripe", root.transform, new Vector3(0.78f, 0.95f, 0.22f), new Vector3(0.52f, 0.08f, 0.08f), OrangeAccent);
            Cube("Visor", root.transform, new Vector3(0f, 1.4f, 0.42f), new Vector3(0.55f, 0.16f, 0.1f), new Color(0.25f, 0.85f, 1f));
            Cube("Arm", root.transform, new Vector3(-0.7f, 1.05f, 0.15f), new Vector3(0.35f, 0.18f, 0.18f), Steel);
            return root;
        }

        public static GameObject BuildDefense()
        {
            var mesh = UnitMeshCatalog.InstantiateClean(UnitMeshCatalog.LoadDefense(), "Unit_DefenseMech");
            if (mesh != null) return mesh;

            // Wide combat chassis — red hull, shield plate, shoulder block.
            var root = new GameObject("Unit_DefenseMech");
            Capsule("Body", root.transform, new Vector3(0f, 1.1f, 0f), new Vector3(1.1f, 1.05f, 0.95f), DefenseTint);
            Cylinder("Band", root.transform, new Vector3(0f, 0.95f, 0f), new Vector3(1.2f, 0.1f, 1.05f), BlackBand);
            Cube("Shoulder", root.transform, new Vector3(0.72f, 1.55f, 0f), new Vector3(0.65f, 0.42f, 0.65f), WhiteShell);
            Cube("ShoulderAccent", root.transform, new Vector3(0.72f, 1.7f, 0.28f), new Vector3(0.5f, 0.08f, 0.12f), OrangeAccent);
            Cube("Shield", root.transform, new Vector3(-0.78f, 1.15f, 0.12f), new Vector3(0.18f, 1.15f, 0.9f), Steel);
            Cube("Plating", root.transform, new Vector3(0f, 1.35f, 0.45f), new Vector3(0.7f, 0.35f, 0.12f), BlackBand);
            return root;
        }

        public static GameObject BuildMedic()
        {
            var mesh = UnitMeshCatalog.InstantiateClean(UnitMeshCatalog.LoadForClass(SpecialistClass.Medic), "Unit_Medic");
            if (mesh != null) return mesh;

            var root = new GameObject("Unit_Medic");
            Capsule("Body", root.transform, new Vector3(0f, 1.1f, 0f), new Vector3(0.7f, 1.05f, 0.7f), WhiteShell);
            Cylinder("Band", root.transform, new Vector3(0f, 1.15f, 0f), new Vector3(0.78f, 0.08f, 0.78f), BlackBand);
            Cube("CrossH", root.transform, new Vector3(0f, 1.55f, 0.38f), new Vector3(0.42f, 0.1f, 0.08f), ScoutTint);
            Cube("CrossV", root.transform, new Vector3(0f, 1.55f, 0.38f), new Vector3(0.1f, 0.42f, 0.08f), ScoutTint);
            Sphere("Kit", root.transform, new Vector3(0.42f, 0.95f, 0.05f), new Vector3(0.28f, 0.22f, 0.28f), MedicTint);
            Cube("Beacon", root.transform, new Vector3(-0.22f, 2.05f, 0f), new Vector3(0.1f, 0.1f, 0.1f), OrangeAccent);
            return root;
        }

        public static GameObject BuildHarvester()
        {
            var mesh = UnitMeshCatalog.InstantiateClean(UnitMeshCatalog.LoadHarvester(), "Unit_HarvesterBot");
            if (mesh != null) return mesh;

            var root = new GameObject("Unit_HarvesterBot");
            Capsule("Body", root.transform, new Vector3(0f, 0.88f, 0f), new Vector3(1.05f, 0.78f, 1.05f), HarvesterTint);
            Cylinder("Band", root.transform, new Vector3(0f, 0.78f, 0f), new Vector3(1.15f, 0.1f, 1.15f), BlackBand);
            Cube("Hopper", root.transform, new Vector3(0f, 1.25f, -0.35f), new Vector3(0.7f, 0.42f, 0.55f), Steel);
            Cube("Scoop", root.transform, new Vector3(0f, 0.55f, 0.62f), new Vector3(0.85f, 0.18f, 0.42f), OrangeAccent);
            Cube("Visor", root.transform, new Vector3(0f, 1.22f, 0.48f), new Vector3(0.5f, 0.12f, 0.08f), new Color(0.25f, 0.85f, 1f));
            return root;
        }

        public static GameObject BuildSurveyor()
        {
            var mesh = UnitMeshCatalog.InstantiateClean(UnitMeshCatalog.LoadSurveyor(), "Unit_SurveyorBot");
            if (mesh != null) return mesh;

            var root = new GameObject("Unit_SurveyorBot");
            Capsule("Body", root.transform, new Vector3(0f, 1.2f, 0f), new Vector3(0.55f, 1.12f, 0.55f), SurveyorTint);
            Cylinder("Band", root.transform, new Vector3(0f, 1.35f, 0f), new Vector3(0.62f, 0.08f, 0.62f), BlackBand);
            Sphere("Dish", root.transform, new Vector3(0f, 2.35f, 0f), new Vector3(0.55f, 0.12f, 0.55f), WhiteShell);
            Cylinder("Mast", root.transform, new Vector3(0f, 2.05f, 0f), new Vector3(0.06f, 0.42f, 0.06f), Steel);
            Cube("Lens", root.transform, new Vector3(0f, 1.55f, 0.28f), new Vector3(0.32f, 0.12f, 0.1f), ScoutTint);
            return root;
        }

        public static GameObject BuildDustStalker()
        {
            var mesh = UnitMeshCatalog.InstantiateClean(UnitMeshCatalog.LoadStalker(), "Unit_DustStalker");
            if (mesh != null) return mesh;

            // Low predator — dark carapace, glowing eyes, spine ridges.
            var root = new GameObject("Unit_DustStalker");
            Sphere("Body", root.transform, new Vector3(0f, 0.38f, 0f), new Vector3(1.7f, 0.55f, 1.15f), StalkerTint);
            Sphere("Head", root.transform, new Vector3(0f, 0.48f, 0.7f), new Vector3(0.55f, 0.4f, 0.55f), StalkerTint * 1.15f);
            Cube("Spine", root.transform, new Vector3(0f, 0.58f, -0.35f), new Vector3(0.22f, 0.18f, 0.95f), BlackBand);
            Cube("RidgeA", root.transform, new Vector3(0f, 0.72f, 0.05f), new Vector3(0.12f, 0.28f, 0.18f), BlackBand);
            Cube("RidgeB", root.transform, new Vector3(0f, 0.68f, -0.35f), new Vector3(0.1f, 0.22f, 0.16f), BlackBand);
            Sphere("EyeL", root.transform, new Vector3(-0.16f, 0.55f, 0.92f), new Vector3(0.12f, 0.1f, 0.1f), OrangeAccent);
            Sphere("EyeR", root.transform, new Vector3(0.16f, 0.55f, 0.92f), new Vector3(0.12f, 0.1f, 0.1f), OrangeAccent);
            Cube("LegFL", root.transform, new Vector3(-0.55f, 0.18f, 0.35f), new Vector3(0.12f, 0.35f, 0.12f), BlackBand);
            Cube("LegFR", root.transform, new Vector3(0.55f, 0.18f, 0.35f), new Vector3(0.12f, 0.35f, 0.12f), BlackBand);
            Cube("LegBL", root.transform, new Vector3(-0.5f, 0.18f, -0.4f), new Vector3(0.12f, 0.35f, 0.12f), BlackBand);
            Cube("LegBR", root.transform, new Vector3(0.5f, 0.18f, -0.4f), new Vector3(0.12f, 0.35f, 0.12f), BlackBand);
            return root;
        }

        public static GameObject BuildRegolithMite()
        {
            var mesh = UnitMeshCatalog.InstantiateClean(UnitMeshCatalog.LoadMite(), "Unit_RegolithMite");
            if (mesh != null) return mesh;

            var root = new GameObject("Unit_RegolithMite");
            Sphere("Body", root.transform, new Vector3(0f, 0.28f, 0f), new Vector3(0.85f, 0.42f, 0.7f), MiteTint);
            Cube("Mandible", root.transform, new Vector3(0f, 0.22f, 0.38f), new Vector3(0.28f, 0.12f, 0.22f), BlackBand);
            Sphere("EyeL", root.transform, new Vector3(-0.14f, 0.34f, 0.28f), new Vector3(0.1f, 0.08f, 0.08f), OrangeAccent);
            Sphere("EyeR", root.transform, new Vector3(0.14f, 0.34f, 0.28f), new Vector3(0.1f, 0.08f, 0.08f), OrangeAccent);
            Cube("LegL", root.transform, new Vector3(-0.32f, 0.12f, 0.05f), new Vector3(0.08f, 0.22f, 0.08f), BlackBand);
            Cube("LegR", root.transform, new Vector3(0.32f, 0.12f, 0.05f), new Vector3(0.08f, 0.22f, 0.08f), BlackBand);
            Cube("Plate", root.transform, new Vector3(0f, 0.42f, -0.08f), new Vector3(0.55f, 0.08f, 0.4f), new Color(0.42f, 0.34f, 0.28f));
            return root;
        }

        public static GameObject BuildWattLeech()
        {
            var mesh = UnitMeshCatalog.InstantiateClean(UnitMeshCatalog.LoadLeech(), "Unit_WattLeech");
            if (mesh != null) return mesh;

            var root = new GameObject("Unit_WattLeech");
            Capsule("Body", root.transform, new Vector3(0f, 0.22f, 0f), new Vector3(0.55f, 0.22f, 1.15f), LeechTint);
            Sphere("Core", root.transform, new Vector3(0f, 0.32f, 0.15f), new Vector3(0.28f, 0.28f, 0.28f), new Color(0.55f, 1f, 1f));
            Cube("Spark", root.transform, new Vector3(0f, 0.48f, 0.4f), new Vector3(0.12f, 0.18f, 0.12f), OrangeAccent);
            Cube("Ridge", root.transform, new Vector3(0f, 0.38f, -0.15f), new Vector3(0.18f, 0.1f, 0.7f), new Color(0.72f, 0.9f, 0.98f));
            return root;
        }

        public static GameObject BuildTerraformer()
        {
            var mesh = UnitMeshCatalog.InstantiateClean(UnitMeshCatalog.LoadTerraformer(), "Unit_TerraformerBot");
            if (mesh != null) return mesh;

            var root = new GameObject("Unit_TerraformerBot");
            Capsule("Body", root.transform, new Vector3(0f, 1.0f, 0f), new Vector3(0.85f, 0.9f, 0.85f), WhiteShell);
            Cylinder("Band", root.transform, new Vector3(0f, 0.85f, 0f), new Vector3(0.95f, 0.08f, 0.95f), BlackBand);
            Cylinder("TankL", root.transform, new Vector3(-0.38f, 1.15f, -0.28f), new Vector3(0.28f, 0.42f, 0.28f), Steel);
            Cylinder("TankR", root.transform, new Vector3(0.38f, 1.15f, -0.28f), new Vector3(0.28f, 0.42f, 0.28f), Steel);
            Cube("Boom", root.transform, new Vector3(0f, 1.15f, 0.55f), new Vector3(0.9f, 0.08f, 0.08f), Steel);
            Cube("Nozzle", root.transform, new Vector3(0f, 1.0f, 0.62f), new Vector3(0.12f, 0.18f, 0.12f), OrangeAccent);
            Cube("Visor", root.transform, new Vector3(0f, 1.45f, 0.38f), new Vector3(0.42f, 0.1f, 0.08f), ScoutTint);
            return root;
        }

        public static GameObject BuildCourier()
        {
            var mesh = UnitMeshCatalog.InstantiateClean(UnitMeshCatalog.LoadCourier(), "Unit_CourierBot");
            if (mesh != null) return mesh;

            var root = new GameObject("Unit_CourierBot");
            Cube("Chassis", root.transform, new Vector3(0f, 0.55f, 0f), new Vector3(0.7f, 0.45f, 0.85f), WhiteShell);
            Cube("Crate", root.transform, new Vector3(0f, 1.05f, -0.18f), new Vector3(0.55f, 0.5f, 0.48f), Steel);
            Cube("Stripe", root.transform, new Vector3(0f, 1.05f, -0.44f), new Vector3(0.42f, 0.08f, 0.06f), OrangeAccent);
            Cube("Visor", root.transform, new Vector3(0f, 0.95f, 0.38f), new Vector3(0.36f, 0.1f, 0.08f), ScoutTint);
            return root;
        }

        public static GameObject BuildGeologist()
        {
            var mesh = UnitMeshCatalog.InstantiateClean(UnitMeshCatalog.LoadGeologist(), "Unit_GeologistBot");
            if (mesh != null) return mesh;

            var root = new GameObject("Unit_GeologistBot");
            Cube("Chassis", root.transform, new Vector3(0f, 0.42f, 0f), new Vector3(0.7f, 0.32f, 1.2f), WhiteShell);
            Cube("Belly", root.transform, new Vector3(0f, 0.22f, 0f), new Vector3(0.55f, 0.16f, 1.05f), BlackBand);
            Cube("Crate", root.transform, new Vector3(0f, 0.62f, -0.42f), new Vector3(0.45f, 0.28f, 0.38f), Steel);
            Cube("Stripe", root.transform, new Vector3(0f, 0.52f, 0.48f), new Vector3(0.4f, 0.08f, 0.08f), OrangeAccent);
            Cylinder("Mast", root.transform, new Vector3(0.12f, 0.85f, 0.2f), new Vector3(0.06f, 0.28f, 0.06f), Steel);
            Sphere("Sensor", root.transform, new Vector3(0.12f, 1.18f, 0.2f), new Vector3(0.18f, 0.18f, 0.18f), ScoutTint);
            Cube("Drill", root.transform, new Vector3(-0.12f, 0.55f, 0.7f), new Vector3(0.1f, 0.1f, 0.55f), Steel);
            Cube("Bit", root.transform, new Vector3(-0.12f, 0.5f, 1.02f), new Vector3(0.1f, 0.1f, 0.16f), OrangeAccent);
            for (int i = 0; i < 3; i++)
            {
                float z = -0.45f + i * 0.45f;
                var wl = Cylinder("WheelL_" + i, root.transform, new Vector3(-0.42f, 0.16f, z), new Vector3(0.28f, 0.08f, 0.28f), BlackBand);
                wl.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                var wr = Cylinder("WheelR_" + i, root.transform, new Vector3(0.42f, 0.16f, z), new Vector3(0.28f, 0.08f, 0.28f), BlackBand);
                wr.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            return root;
        }

        public static GameObject BuildSentinel()
        {
            var mesh = UnitMeshCatalog.InstantiateClean(UnitMeshCatalog.LoadSentinel(), "Unit_SentinelMech");
            if (mesh != null) return mesh;

            var root = new GameObject("Unit_SentinelMech");
            Cube("Hull", root.transform, new Vector3(0f, 0.55f, 0f), new Vector3(0.95f, 0.55f, 0.85f), WhiteShell);
            Cube("Skirt", root.transform, new Vector3(0f, 0.22f, 0f), new Vector3(1.05f, 0.18f, 0.95f), BlackBand);
            Cylinder("Turret", root.transform, new Vector3(0f, 1.05f, 0f), new Vector3(0.55f, 0.22f, 0.55f), SentinelTint);
            Cube("Barrel", root.transform, new Vector3(0f, 1.12f, 0.42f), new Vector3(0.12f, 0.12f, 0.55f), Steel);
            Cube("Shield", root.transform, new Vector3(-0.58f, 0.62f, 0.08f), new Vector3(0.08f, 0.7f, 0.7f), Steel);
            Cube("Chevron", root.transform, new Vector3(0f, 0.72f, 0.42f), new Vector3(0.55f, 0.08f, 0.08f), OrangeAccent);
            return root;
        }

        public static GameObject BuildIceWisp()
        {
            var mesh = UnitMeshCatalog.InstantiateClean(UnitMeshCatalog.LoadWisp(), "Unit_IceWisp");
            if (mesh != null) return mesh;

            var root = new GameObject("Unit_IceWisp");
            Sphere("Core", root.transform, new Vector3(0f, 0.55f, 0f), new Vector3(0.32f, 0.32f, 0.32f), ScoutTint);
            Sphere("Halo", root.transform, new Vector3(0f, 0.55f, 0f), new Vector3(0.55f, 0.38f, 0.55f), WispTint);
            Cube("ShardA", root.transform, new Vector3(0.18f, 0.72f, 0.08f), new Vector3(0.08f, 0.28f, 0.06f), WhiteShell);
            Cube("ShardB", root.transform, new Vector3(-0.14f, 0.42f, -0.1f), new Vector3(0.07f, 0.22f, 0.05f), Steel);
            Cube("Spark", root.transform, new Vector3(0.08f, 0.82f, 0.12f), new Vector3(0.08f, 0.08f, 0.08f), OrangeAccent);
            return root;
        }

        public static GameObject BuildRockTick()
        {
            var mesh = UnitMeshCatalog.InstantiateClean(UnitMeshCatalog.LoadTick(), "Unit_RockTick");
            if (mesh != null) return mesh;

            var root = new GameObject("Unit_RockTick");
            Sphere("Body", root.transform, new Vector3(0f, 0.16f, 0f), new Vector3(0.42f, 0.22f, 0.48f), TickTint);
            Cube("Plate", root.transform, new Vector3(0f, 0.26f, -0.02f), new Vector3(0.32f, 0.06f, 0.28f), Steel);
            Cube("PincerL", root.transform, new Vector3(-0.12f, 0.14f, 0.22f), new Vector3(0.05f, 0.05f, 0.14f), OrangeAccent);
            Cube("PincerR", root.transform, new Vector3(0.12f, 0.14f, 0.22f), new Vector3(0.05f, 0.05f, 0.14f), OrangeAccent);
            Cube("LegL", root.transform, new Vector3(-0.18f, 0.08f, 0.05f), new Vector3(0.04f, 0.12f, 0.04f), BlackBand);
            Cube("LegR", root.transform, new Vector3(0.18f, 0.08f, 0.05f), new Vector3(0.04f, 0.12f, 0.04f), BlackBand);
            return root;
        }

        public static GameObject BuildSoilCreeper()
        {
            var mesh = UnitMeshCatalog.InstantiateClean(UnitMeshCatalog.LoadCreeper(), "Unit_SoilCreeper");
            if (mesh != null) return mesh;

            var root = new GameObject("Unit_SoilCreeper");
            Sphere("Body", root.transform, new Vector3(0f, 0.14f, 0f), new Vector3(0.38f, 0.22f, 1.15f), CreeperTint);
            Cube("Ridge", root.transform, new Vector3(0f, 0.24f, -0.08f), new Vector3(0.18f, 0.06f, 0.7f), Steel);
            Cube("Tendril", root.transform, new Vector3(0f, 0.12f, -0.62f), new Vector3(0.08f, 0.08f, 0.28f), OrangeAccent);
            Sphere("Sensor", root.transform, new Vector3(0.08f, 0.22f, 0.48f), new Vector3(0.1f, 0.1f, 0.1f), ScoutTint);
            Cube("Nub", root.transform, new Vector3(-0.08f, 0.22f, 0.42f), new Vector3(0.08f, 0.08f, 0.08f), OrangeAccent);
            return root;
        }

        public static GameObject BuildAshHopper()
        {
            var mesh = UnitMeshCatalog.InstantiateClean(UnitMeshCatalog.LoadHopper(), "Unit_AshHopper");
            if (mesh != null) return mesh;

            var root = new GameObject("Unit_AshHopper");
            Sphere("Body", root.transform, new Vector3(0f, 0.62f, 0f), new Vector3(0.42f, 0.32f, 0.48f), HopperTint);
            Cube("LegFL", root.transform, new Vector3(-0.22f, 0.32f, 0.16f), new Vector3(0.06f, 0.62f, 0.06f), BlackBand);
            Cube("LegFR", root.transform, new Vector3(0.22f, 0.32f, 0.16f), new Vector3(0.06f, 0.62f, 0.06f), BlackBand);
            Cube("LegBL", root.transform, new Vector3(-0.2f, 0.28f, -0.16f), new Vector3(0.06f, 0.52f, 0.06f), BlackBand);
            Cube("LegBR", root.transform, new Vector3(0.2f, 0.28f, -0.16f), new Vector3(0.06f, 0.52f, 0.06f), BlackBand);
            Sphere("EyeL", root.transform, new Vector3(-0.08f, 0.72f, 0.18f), new Vector3(0.1f, 0.1f, 0.1f), ScoutTint);
            Sphere("EyeR", root.transform, new Vector3(0.08f, 0.72f, 0.18f), new Vector3(0.1f, 0.1f, 0.1f), ScoutTint);
            Cube("Knee", root.transform, new Vector3(0.22f, 0.38f, 0.16f), new Vector3(0.08f, 0.08f, 0.08f), OrangeAccent);
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
