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
        public const string StalkerPath = "Units/SM_Unit_DustStalker";

        public static GameObject LoadScout() => Resources.Load<GameObject>(ScoutPath);
        public static GameObject LoadEngineer() => Resources.Load<GameObject>(EngineerPath);
        public static GameObject LoadDefense() => Resources.Load<GameObject>(DefensePath);
        public static GameObject LoadStalker() => Resources.Load<GameObject>(StalkerPath);

        public static GameObject LoadForClass(SpecialistClass cls)
        {
            switch (cls)
            {
                case SpecialistClass.ScoutDrone: return LoadScout();
                case SpecialistClass.EngineerBot: return LoadEngineer();
                case SpecialistClass.DefenseMech: return LoadDefense();
                default: return LoadScout();
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
            // DestroyImmediate in editor path — UnitMeshCatalog also strips colliders.
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
