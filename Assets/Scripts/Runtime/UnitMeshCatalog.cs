using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Blender hero unit meshes under Resources/Units (SM_Unit_*.fbx).
    /// Prefab wrappers Unit_* still own SpecialistAgent / DustStalkerAgent components.
    /// </summary>
    public static class UnitMeshCatalog
    {
        public const string ScoutPath = "Units/SM_Unit_ScoutDrone";
        public const string EngineerPath = "Units/SM_Unit_EngineerBot";
        public const string DefensePath = "Units/SM_Unit_DefenseMech";
        public const string MedicPath = "Units/SM_Unit_Medic";
        public const string HarvesterPath = "Units/SM_Unit_HarvesterBot";
        public const string SurveyorPath = "Units/SM_Unit_SurveyorBot";
        public const string TerraformerPath = "Units/SM_Unit_TerraformerBot";
        public const string CourierPath = "Units/SM_Unit_CourierBot";
        public const string GeologistPath = "Units/SM_Unit_GeologistBot";
        public const string SentinelPath = "Units/SM_Unit_SentinelMech";
        public const string StalkerPath = "Units/SM_Unit_DustStalker";
        public const string MitePath = "Units/SM_Unit_RegolithMite";
        public const string LeechPath = "Units/SM_Unit_WattLeech";
        public const string WispPath = "Units/SM_Unit_IceWisp";
        public const string TickPath = "Units/SM_Unit_RockTick";
        public const string CreeperPath = "Units/SM_Unit_SoilCreeper";
        public const string HopperPath = "Units/SM_Unit_AshHopper";

        public static GameObject LoadScout() => Resources.Load<GameObject>(ScoutPath);
        public static GameObject LoadEngineer() => Resources.Load<GameObject>(EngineerPath);
        public static GameObject LoadDefense() => Resources.Load<GameObject>(DefensePath);
        public static GameObject LoadMedic() => Resources.Load<GameObject>(MedicPath);
        public static GameObject LoadHarvester() => Resources.Load<GameObject>(HarvesterPath);
        public static GameObject LoadSurveyor() => Resources.Load<GameObject>(SurveyorPath);
        public static GameObject LoadTerraformer() => Resources.Load<GameObject>(TerraformerPath);
        public static GameObject LoadCourier() => Resources.Load<GameObject>(CourierPath);
        public static GameObject LoadGeologist() => Resources.Load<GameObject>(GeologistPath);
        public static GameObject LoadSentinel() => Resources.Load<GameObject>(SentinelPath);
        public static GameObject LoadStalker() => Resources.Load<GameObject>(StalkerPath);
        public static GameObject LoadMite() => Resources.Load<GameObject>(MitePath);
        public static GameObject LoadLeech() => Resources.Load<GameObject>(LeechPath);
        public static GameObject LoadWisp() => Resources.Load<GameObject>(WispPath);
        public static GameObject LoadTick() => Resources.Load<GameObject>(TickPath);
        public static GameObject LoadCreeper() => Resources.Load<GameObject>(CreeperPath);
        public static GameObject LoadHopper() => Resources.Load<GameObject>(HopperPath);

        public static GameObject LoadForClass(SpecialistClass cls)
        {
            switch (cls)
            {
                case SpecialistClass.ScoutDrone: return LoadScout();
                case SpecialistClass.EngineerBot: return LoadEngineer();
                case SpecialistClass.DefenseMech: return LoadDefense();
                case SpecialistClass.Medic: return LoadMedic();
                case SpecialistClass.HarvesterBot: return LoadHarvester();
                case SpecialistClass.SurveyorBot: return LoadSurveyor();
                case SpecialistClass.TerraformerBot: return LoadTerraformer();
                case SpecialistClass.CourierBot: return LoadCourier();
                case SpecialistClass.GeologistBot: return LoadGeologist();
                case SpecialistClass.SentinelMech: return LoadSentinel();
                default: return LoadScout();
            }
        }

        public static GameObject LoadFauna(FaunaKind kind)
        {
            switch (kind)
            {
                case FaunaKind.Mite: return LoadMite();
                case FaunaKind.Leech: return LoadLeech();
                case FaunaKind.Wisp: return LoadWisp();
                case FaunaKind.Tick: return LoadTick();
                case FaunaKind.Creeper: return LoadCreeper();
                case FaunaKind.Hopper: return LoadHopper();
                default: return LoadStalker();
            }
        }

        /// <summary>Instantiate mesh root, strip imported cameras/lights, remap URP mats.</summary>
        public static GameObject InstantiateClean(GameObject meshPrefab, string name)
        {
            if (meshPrefab == null) return null;
            var go = Object.Instantiate(meshPrefab);
            go.name = name;
            StripImportJunk(go);
            ColonyVisualUtility.EnsureUrpMaterials(go);
            ColonyVisualUtility.SnapToGround(go);
            return go;
        }

        private static void StripImportJunk(GameObject root)
        {
            foreach (var cam in root.GetComponentsInChildren<Camera>(true))
            {
                if (Application.isPlaying) Object.Destroy(cam.gameObject);
                else Object.DestroyImmediate(cam.gameObject);
            }
            foreach (var light in root.GetComponentsInChildren<Light>(true))
            {
                if (Application.isPlaying) Object.Destroy(light.gameObject);
                else Object.DestroyImmediate(light.gameObject);
            }
            foreach (var col in root.GetComponentsInChildren<Collider>(true))
            {
                if (Application.isPlaying) Object.Destroy(col);
                else Object.DestroyImmediate(col);
            }
        }
    }
}
