using System;
using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Draws Inn, Guild Hall, HAB, Power or Defense as the colony's civ variant when that FBX
    /// is in <see cref="CivBuildingCatalog"/>. Places it 1:1 (the 6 m plot is baked in, the same
    /// way the castles bake their own plot), turns it so the front faces -Z, and paints the
    /// citadel slots (Trim, Carbon, TeamColour, Accent, CoreEmission) with the shared trim sheet.
    ///
    /// Returns false when the civ is neutral, the building is outside the batch, or the file is
    /// missing or empty. The caller then builds the shared hero kit or the procedural kit.
    /// </summary>
    public static class CivBuildingLoader
    {
        private static readonly HashSet<string> Warned = new HashSet<string>();

        /// <summary>
        /// Test seam. When set, used instead of Resources. Production leaves it null;
        /// play-mode startup clears it.
        /// </summary>
        internal static Func<string, GameObject> LoadOverride;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => ResetForTests();

        internal static void ResetForTests()
        {
            LoadOverride = null;
            Warned.Clear();
        }

        /// <summary>
        /// Build <paramref name="category"/> for <paramref name="civ"/> under <paramref name="root"/>.
        /// False = use the shared or procedural building.
        /// </summary>
        public static bool TryBuild(Transform root, BuildingCategory category, CivModifiers civ)
        {
            if (root == null || civ == null || civ.Id == CivId.None) return false;
            string path = CivBuildingCatalog.ResourcePath(civ, category);
            if (string.IsNullOrEmpty(path)) return false;

            GameObject prefab = Load(path);
            if (prefab == null)
            {
                WarnOnce(path, "[CivBuilding] " + path + " missing; shared " + category + ".");
                return false;
            }

            string architecture = CivBuildingCatalog.ArchitectureOf(civ);
            string stem = CivBuildingCatalog.Stem(category);
            var wrap = new GameObject("CivBuilding_" + architecture + "_" + stem);
            wrap.transform.SetParent(root, false);
            wrap.transform.localPosition = Vector3.zero;
            wrap.transform.localRotation = Quaternion.Euler(0f, CivCastleLoader.ImportYawDegrees, 0f);
            wrap.transform.localScale = Vector3.one;

            var model = Object.Instantiate(prefab, wrap.transform, false);
            model.name = prefab.name;
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = prefab.transform.localRotation;
            model.transform.localScale = prefab.transform.localScale;
            Sanitize(model);

            if (!HasMesh(model))
            {
                ColonyVisualUtility.DestroyNow(wrap);
                WarnOnce(path + ":empty", "[CivBuilding] " + path + " has no mesh; shared " + category + ".");
                return false;
            }

            var l0 = new List<Renderer>();
            var l1 = new List<Renderer>();
            var l2 = new List<Renderer>();
            var rest = new List<Renderer>();
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
                string n = r.name;
                if (n.EndsWith("_LOD0")) l0.Add(r);
                else if (n.EndsWith("_LOD1")) l1.Add(r);
                else if (n.EndsWith("_LOD2")) l2.Add(r);
                else rest.Add(r);
            }

            LODGroup group = null;
            if (l0.Count > 0)
            {
                group = model.GetComponent<LODGroup>();
                foreach (var g in model.GetComponentsInChildren<LODGroup>(true))
                    if (g != group) ColonyVisualUtility.DestroyNow(g);
                if (group == null) group = model.AddComponent<LODGroup>();
            }
            else
            {
                foreach (var g in model.GetComponentsInChildren<LODGroup>(true))
                    ColonyVisualUtility.DestroyNow(g);
            }

            var built = root.gameObject.AddComponent<CivBuilding>();
            built.Civ = civ.Id;
            built.Category = category;
            built.Architecture = architecture;
            built.ResourcePath = path;
            built.Model = model.transform;
            built.Lod0 = l0.ToArray();
            built.Lod1 = l1.ToArray();
            built.Lod2 = l2.ToArray();
            built.Rest = rest.ToArray();
            built.Group = group;
            if (group != null) built.ConfigureLods();

            CivCastleLoader.ApplySlots(model, CivCastleCatalog.ForKey(civ.CastleKey), civ);
            return true;
        }

        private static bool HasMesh(GameObject model)
        {
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                if (r is MeshRenderer || r is SkinnedMeshRenderer) return true;
            return false;
        }

        private static GameObject Load(string path)
        {
            if (LoadOverride != null) return LoadOverride(path);
            return BuildingVisualCatalog.LoadByPath(path);
        }

        private static void Sanitize(GameObject go)
        {
            if (go == null) return;
            foreach (var cam in go.GetComponentsInChildren<Camera>(true))
            {
                if (cam == null || cam.gameObject == go) continue;
                cam.enabled = false;
                ColonyVisualUtility.DestroyNow(cam.gameObject);
            }
            foreach (var light in go.GetComponentsInChildren<Light>(true))
            {
                if (light == null || light.gameObject == go) continue;
                light.enabled = false;
                ColonyVisualUtility.DestroyNow(light.gameObject);
            }
            foreach (var col in go.GetComponentsInChildren<Collider>(true))
                ColonyVisualUtility.DestroyNow(col);
        }

        private static void WarnOnce(string key, string message)
        {
            if (Warned.Add(key)) Debug.LogWarning(message);
        }
    }
}
