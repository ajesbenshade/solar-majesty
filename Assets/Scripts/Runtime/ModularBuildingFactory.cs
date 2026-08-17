using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Unique bilaterally-symmetric building kits sized to their grid footprints.
    /// Cardinal airlock ports sit on the hull faces so Utility junctions click flush.
    /// Phase 4 hero FBX (SM_Hero_*) is preferred; HeroBuildingKits is the procedural fallback.
    /// </summary>
    public static class ModularBuildingFactory
    {
        private static Material _hull;
        private static Color _bodyHull = new Color(0.62f, 0.66f, 0.72f);

        /// <summary>Greybox hull tint per body. Hero kits lerp a white shell toward this grade.</summary>
        public static void BindBody(CelestialBodyProfile body)
        {
            if (body == null)
            {
                _bodyHull = new Color(0.62f, 0.66f, 0.72f);
                return;
            }

            switch (body.Id)
            {
                case CelestialBodyId.Earth:
                    _bodyHull = new Color(0.74f, 0.78f, 0.74f);
                    break;
                case CelestialBodyId.Luna:
                    _bodyHull = new Color(0.68f, 0.70f, 0.72f);
                    break;
                case CelestialBodyId.Mars:
                    // Sheet is white/black/orange. Do not lerp Mars dirt into the hull.
                    _bodyHull = new Color(0.98f, 0.98f, 0.99f);
                    break;
                case CelestialBodyId.Belt:
                    _bodyHull = new Color(0.42f, 0.40f, 0.38f);
                    break;
                case CelestialBodyId.Europa:
                    _bodyHull = new Color(0.62f, 0.78f, 0.86f);
                    break;
                default:
                    _bodyHull = new Color(0.62f, 0.66f, 0.72f);
                    break;
            }
        }

        /// <summary>Hulls fill the footprint so faces meet the 2×2 airlock cell.</summary>
        private const float HullFill = 1f;

        public static GameObject Spawn(
            BuildingCategory category,
            Vector3 position,
            Transform parent,
            bool ghost = false)
        {
            int side = ColonyLayout.FootprintSide(category);
            return Spawn(category, position, parent, side, side, ColonyLayout.DefaultCellSize, ghost);
        }

        public static GameObject Spawn(
            BuildingCategory category,
            Vector3 position,
            Transform parent,
            int footprintW,
            int footprintH,
            float cellSize,
            bool ghost = false)
        {
            cellSize = cellSize > 0.1f ? cellSize : ColonyLayout.DefaultCellSize;
            footprintW = Mathf.Max(1, footprintW);
            footprintH = Mathf.Max(1, footprintH);
            float worldW = footprintW * cellSize;
            float worldD = footprintH * cellSize;

            if (category == BuildingCategory.Utility)
            {
                var airlock = ColonyVisualUtility.SpawnPlusConnector(
                    position, parent, Mathf.Min(worldW, worldD), showAllArms: ghost);
                airlock.name = ghost ? $"Ghost_{category}" : "AirlockJunction";
                if (ghost)
                    StripColliders(airlock);
                return airlock;
            }

            var root = new GameObject(ghost ? $"Ghost_{category}" : $"Mod_{category}");
            if (parent != null)
                root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, Quaternion.identity);

            if (!HeroBuildingKits.IsHero(category))
                BuildDeck(root.transform, worldW, worldD);
            BuildCore(root.transform, category, worldW * HullFill, worldD * HullFill);
            AttachCardinalAirlocks(root.transform, category, worldW * 0.5f, worldD * 0.5f, ghost);
            if (!ghost)
                HideLiveDockDress(root.transform);

            ColonyVisualUtility.EnsureUrpMaterials(root);
            // Do not SetTintOverlay here — MPB _BaseColor replaces orange/cyan/carbon
            // and flattens hero kits into greybox hulls. Body grade lives in atmosphere.
            ColonyVisualUtility.SnapToGround(root);
            if (ghost)
                StripColliders(root);
            return root;
        }

        private static void BuildCore(Transform root, BuildingCategory cat, float w, float d)
        {
            if (TryAttachHeroMesh(root, cat, w, d))
                return;

            switch (cat)
            {
                case BuildingCategory.Habitat:
                    HeroBuildingKits.BuildHabitat(root, w, d, HeroHull());
                    break;
                case BuildingCategory.Commons:
                    HeroBuildingKits.BuildCommons(root, w, d, HeroHull());
                    break;
                case BuildingCategory.LandingPad:
                    HeroBuildingKits.BuildLandingPad(root, w, d, HeroHull());
                    break;
                case BuildingCategory.Farm:
                    HeroBuildingKits.BuildWaterExtractor(root, w, d, HeroHull());
                    break;
                case BuildingCategory.Mine:
                    HeroBuildingKits.BuildOreExtractor(root, w, d, HeroHull());
                    break;
                case BuildingCategory.RegolithCamp:
                    HeroBuildingKits.BuildRegolithExtractor(root, w, d, HeroHull());
                    break;
                case BuildingCategory.Power:
                    HeroBuildingKits.BuildSolarField(root, w, d, HeroHull());
                    break;
                case BuildingCategory.Defense:
                    HeroBuildingKits.BuildDefenseBattery(root, w, d, HeroHull());
                    break;
                case BuildingCategory.ScoutWorkshop:
                    HeroBuildingKits.BuildWorkshop(root, w, d, new Color(0.35f, 0.85f, 1f), tall: false);
                    break;
                case BuildingCategory.EngineerWorkshop:
                    HeroBuildingKits.BuildWorkshop(root, w, d, new Color(1f, 0.65f, 0.2f), tall: false);
                    break;
                case BuildingCategory.DefenseWorkshop:
                    HeroBuildingKits.BuildWorkshop(root, w, d, new Color(0.95f, 0.35f, 0.35f), tall: true);
                    break;
                case BuildingCategory.MedicWorkshop:
                    HeroBuildingKits.BuildWorkshop(root, w, d, new Color(0.45f, 0.95f, 0.55f), tall: false);
                    break;
                case BuildingCategory.HarvesterWorkshop:
                    HeroBuildingKits.BuildWorkshop(root, w, d, new Color(0.82f, 0.62f, 0.22f), tall: false);
                    break;
                case BuildingCategory.SurveyorWorkshop:
                    HeroBuildingKits.BuildWorkshop(root, w, d, new Color(0.45f, 0.82f, 0.95f), tall: false);
                    break;
                case BuildingCategory.TerraformerWorkshop:
                    HeroBuildingKits.BuildWorkshop(root, w, d, new Color(0.42f, 0.82f, 0.38f), tall: false);
                    break;
                case BuildingCategory.CourierWorkshop:
                    HeroBuildingKits.BuildWorkshop(root, w, d, new Color(0.95f, 0.72f, 0.28f), tall: false);
                    break;
                case BuildingCategory.GeologistWorkshop:
                    HeroBuildingKits.BuildWorkshop(root, w, d, new Color(0.68f, 0.52f, 0.32f), tall: false);
                    break;
                case BuildingCategory.SentinelWorkshop:
                    HeroBuildingKits.BuildWorkshop(root, w, d, new Color(0.78f, 0.38f, 0.22f), tall: true);
                    break;
                case BuildingCategory.GuildHall:
                    HeroBuildingKits.BuildGuildHall(root, w, d, HeroHull());
                    break;
                case BuildingCategory.Mining:
                    HeroBuildingKits.BuildOpsUnit(root, w, d, HeroHull());
                    break;
                case BuildingCategory.Inn:
                    HeroBuildingKits.BuildInn(root, w, d);
                    break;
                case BuildingCategory.Laboratory:
                    HeroBuildingKits.BuildLaboratory(root, w, d, HeroHull());
                    break;
                case BuildingCategory.ClimateLoom:
                    HeroBuildingKits.BuildClimateLoom(root, w, d, HeroHull());
                    break;
                case BuildingCategory.AegisSpire:
                    HeroBuildingKits.BuildAegisSpire(root, w, d, HeroHull());
                    break;
                case BuildingCategory.DeepArchive:
                    HeroBuildingKits.BuildDeepArchive(root, w, d, HeroHull());
                    break;
                default:
                    BuildGenericModule(root, w, d, AccentFor(cat));
                    break;
            }
        }

        private static readonly HashSet<BuildingCategory> AttachedLogged = new HashSet<BuildingCategory>();

        private static bool TryAttachHeroMesh(Transform root, BuildingCategory cat, float w, float d)
        {
            GameObject uniqueMesh = UniqueMeshPrefab(cat);
            if (uniqueMesh == null) return false;

            var wrap = new GameObject("CoreMesh");
            wrap.transform.SetParent(root, false);
            wrap.transform.localPosition = Vector3.zero;
            wrap.transform.localRotation = Quaternion.identity;
            var core = ColonyVisualUtility.InstantiateOriented(uniqueMesh, root.position, wrap.transform);
            if (core == null)
            {
                Object.Destroy(wrap);
                return false;
            }

            core.name = "Mesh";
            core.transform.localPosition = Vector3.zero;
            SanitizeImportedMesh(wrap);
            var rends = wrap.GetComponentsInChildren<Renderer>(true);
            if (rends == null || rends.Length == 0)
            {
                Object.Destroy(wrap);
                Debug.LogWarning("[HeroKit] " + cat + " FBX had no renderers — procedural fallback.");
                return false;
            }

            FitToFootprint(wrap, w, d);
            if (AttachedLogged.Add(cat))
                Debug.Log("[HeroKit] Attached " + uniqueMesh.name + " for " + cat + ".");
            return true;
        }

        private static GameObject UniqueMeshPrefab(BuildingCategory cat)
        {
            // Joined Commons FBX bakes eight radial stubs. Unused ones read as orange
            // ribbed modules on empty faces and cannot be toggled per dock.
            if (cat == BuildingCategory.Commons)
                return null;

            GameObject hero = BuildingVisualCatalog.LoadHeroKit(cat);
            if (hero != null)
                return hero;
            switch (cat)
            {
                case BuildingCategory.Mining:
                    return BuildingVisualCatalog.LoadPrefab(cat);
                default:
                    return null;
            }
        }

        private static void SanitizeImportedMesh(GameObject go)
        {
            if (go == null) return;
            foreach (var cam in go.GetComponentsInChildren<Camera>(true))
            {
                if (cam == null || cam.gameObject == go) continue;
                cam.enabled = false;
                Object.Destroy(cam.gameObject);
            }
            foreach (var light in go.GetComponentsInChildren<Light>(true))
            {
                if (light == null || light.gameObject == go) continue;
                light.enabled = false;
                Object.Destroy(light.gameObject);
            }
            foreach (var col in go.GetComponentsInChildren<Collider>(true))
                Object.Destroy(col);
        }

        private static void FitToFootprint(GameObject go, float targetX, float targetZ)
        {
            if (go == null) return;
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends == null || rends.Length == 0) return;

            Bounds b = default;
            bool any = false;
            for (int i = 0; i < rends.Length; i++)
            {
                var r = rends[i];
                if (r == null || r is ParticleSystemRenderer) continue;
                if (!any)
                {
                    b = r.bounds;
                    any = true;
                }
                else b.Encapsulate(r.bounds);
            }
            if (!any) return;

            // Uniform scale from the tighter axis so cylinder HAB / LAB keep L/D
            // instead of being inflated to a square. Clamp bad import bounds.
            float sx = targetX / Mathf.Max(0.05f, b.size.x);
            float sz = targetZ / Mathf.Max(0.05f, b.size.z);
            float uniform = Mathf.Clamp(Mathf.Min(sx, sz), 0.2f, 6f);
            Vector3 ls = go.transform.localScale;
            go.transform.localScale = new Vector3(ls.x * uniform, ls.y * uniform, ls.z * uniform);
        }

        private static void BuildGenericModule(Transform root, float w, float d, Color accent)
        {
            Part(root, "Hull", PrimitiveType.Cube,
                new Vector3(0f, 1.0f, 0f),
                new Vector3(w, 2.0f, d),
                HullColor());
            Part(root, "Cap_L", PrimitiveType.Cube,
                new Vector3(-w * 0.32f, 2.15f, 0f),
                new Vector3(w * 0.22f, 0.4f, d * 0.8f),
                accent);
            Part(root, "Cap_R", PrimitiveType.Cube,
                new Vector3(w * 0.32f, 2.15f, 0f),
                new Vector3(w * 0.22f, 0.4f, d * 0.8f),
                accent);
        }

        private static void BuildDeck(Transform root, float w, float d)
        {
            Part(root, "Deck", PrimitiveType.Cube,
                new Vector3(0f, 0.08f, 0f),
                new Vector3(w, 0.16f, d),
                new Color(0.82f, 0.48f, 0.18f));
        }

        private static void HideLiveDockDress(Transform root)
        {
            var ts = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < ts.Length; i++)
            {
                Transform t = ts[i];
                if (t == null || t == root) continue;
                string n = t.name;
                if (n.StartsWith("CommonsStub"))
                    t.gameObject.SetActive(false);
            }
        }

        private static void AttachCardinalAirlocks(
            Transform root, BuildingCategory cat, float halfW, float halfD, bool ghost)
        {
            // Round hulls (Commons drum = 0.38 × span) sit inside the square footprint.
            // Sleeves must start on that hull and end at the Lego face or the isometric
            // shot reads as a missed orange port. Live sleeves start hidden; RefreshTubes
            // enables only faces that actually dock.
            float y = ColonyVisualUtility.DockY;
            float bore = ColonyVisualUtility.DockBore;
            float hullR = RoundHullRadius(cat, halfW, halfD);
            PlaceDockSleeve(root, "DockSleeve_N", Vector3.forward, halfD, y, bore, hullR, ghost);
            PlaceDockSleeve(root, "DockSleeve_S", Vector3.back, halfD, y, bore, hullR, ghost);
            PlaceDockSleeve(root, "DockSleeve_E", Vector3.right, halfW, y, bore, hullR, ghost);
            PlaceDockSleeve(root, "DockSleeve_W", Vector3.left, halfW, y, bore, hullR, ghost);
        }

        /// <summary>Commons drum radius. Box kits return 0 so the sleeve sits on the wall.</summary>
        private static float RoundHullRadius(BuildingCategory cat, float halfW, float halfD)
        {
            if (cat == BuildingCategory.Commons)
                return Mathf.Min(halfW, halfD) * 2f * 0.38f;
            return 0f;
        }

        private static void PlaceDockSleeve(
            Transform root,
            string name,
            Vector3 outward,
            float face,
            float y,
            float bore,
            float hullR,
            bool ghost)
        {
            Vector3 dir = outward.normalized;
            float inset = hullR > 0.2f
                ? Mathf.Max(0.2f, face - hullR + 0.08f)
                : 0.36f;
            const float outset = 0.06f;
            Vector3 facePos = dir * face + new Vector3(0f, y, 0f);
            DockSleeve(root, name, facePos, dir, bore, inset, outset, hullR > 0.2f, ghost);
        }

        private static void DockSleeve(
            Transform parent,
            string name,
            Vector3 facePos,
            Vector3 outward,
            float bore,
            float inset,
            float outset,
            bool hullHasPort,
            bool ghost)
        {
            var group = new GameObject(name);
            group.transform.SetParent(parent, false);
            group.transform.localPosition = Vector3.zero;
            group.transform.localRotation = Quaternion.identity;

            float length = inset + outset;
            Vector3 dir = outward.normalized;
            Vector3 center = facePos + dir * ((outset - inset) * 0.5f);
            Color carbon = new Color(0.16f, 0.17f, 0.19f);
            Quaternion along = Quaternion.LookRotation(dir) * Quaternion.Euler(90f, 0f, 0f);
            var tube = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tube.name = name + "_Tube";
            tube.transform.SetParent(group.transform, false);
            tube.transform.localPosition = center;
            tube.transform.localRotation = along;
            tube.transform.localScale = new Vector3(bore, length * 0.5f, bore);
            Object.Destroy(tube.GetComponent<Collider>());
            ApplyColor(tube, new Color(0.98f, 0.98f, 0.99f));

            Vector3 hullEnd = facePos - dir * inset;
            DressCyl(group.transform, name + "_Lip", hullEnd + dir * 0.05f, along,
                new Vector3(bore * 1.04f, 0.035f, bore * 1.04f), carbon);
            // Round kits already wear CommonsPort rings. Box walls need the orange here.
            if (!hullHasPort)
            {
                DressCyl(group.transform, name + "_Collar", hullEnd + dir * 0.02f, along,
                    new Vector3(bore * 1.16f, 0.045f, bore * 1.16f), AirlockColor());
            }

            if (!ghost)
                group.SetActive(false);
        }

        private static void DressCyl(
            Transform parent, string name, Vector3 localPos, Quaternion rot, Vector3 localScale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = rot;
            go.transform.localScale = localScale;
            Object.Destroy(go.GetComponent<Collider>());
            ApplyColor(go, color);
        }

        private static void Part(
            Transform parent,
            string name,
            PrimitiveType type,
            Vector3 localPos,
            Vector3 localScale,
            Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = localScale;
            ApplyColor(go, color);
        }

        private static void ApplyColor(GameObject go, Color color)
        {
            var rend = go.GetComponent<Renderer>();
            if (rend == null) return;
            EnsureMaterials();
            var mat = new Material(_hull);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color"))
                mat.color = color;
            rend.sharedMaterial = mat;
        }

        private static void EnsureMaterials()
        {
            if (_hull != null) return;
            var sh = Shader.Find("Universal Render Pipeline/Lit")
                     ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                     ?? Shader.Find("Standard");
            if (sh == null) return;
            _hull = new Material(sh) { name = "SM_ModHull" };
        }

        private static Color HullColor() => _bodyHull;
        private static Color HeroHull()
        {
            Color sheet = new Color(0.98f, 0.98f, 0.99f);
            // Mars BindBody is already sheet-white. Other bodies may lean a little.
            float t = _bodyHull.g > 0.9f ? 0f : 0.16f;
            return Color.Lerp(sheet, _bodyHull, t);
        }
        private static Color AirlockColor() => new Color(0.96f, 0.42f, 0.08f);

        private static Color AccentFor(BuildingCategory cat)
        {
            switch (cat)
            {
                case BuildingCategory.Commons: return new Color(0.85f, 0.72f, 0.35f);
                case BuildingCategory.Farm: return new Color(0.35f, 0.8f, 0.4f);
                case BuildingCategory.Mine: return new Color(0.75f, 0.55f, 0.3f);
                case BuildingCategory.RegolithCamp: return new Color(0.7f, 0.6f, 0.45f);
                case BuildingCategory.Inn: return new Color(0.92f, 0.62f, 0.28f);
                case BuildingCategory.Laboratory: return new Color(0.45f, 0.7f, 1f);
                case BuildingCategory.Power: return new Color(1f, 0.85f, 0.25f);
                case BuildingCategory.GuildHall: return new Color(0.92f, 0.78f, 0.28f);
                case BuildingCategory.HarvesterWorkshop: return new Color(0.82f, 0.62f, 0.22f);
                case BuildingCategory.SurveyorWorkshop: return new Color(0.45f, 0.82f, 0.95f);
                case BuildingCategory.TerraformerWorkshop: return new Color(0.42f, 0.82f, 0.38f);
                case BuildingCategory.CourierWorkshop: return new Color(0.95f, 0.72f, 0.28f);
                case BuildingCategory.GeologistWorkshop: return new Color(0.68f, 0.52f, 0.32f);
                case BuildingCategory.SentinelWorkshop: return new Color(0.78f, 0.38f, 0.22f);
                case BuildingCategory.ClimateLoom: return new Color(0.38f, 0.82f, 0.48f);
                case BuildingCategory.AegisSpire: return new Color(0.45f, 0.72f, 1f);
                case BuildingCategory.DeepArchive: return new Color(0.42f, 0.78f, 0.88f);
                default: return new Color(0.55f, 0.7f, 0.85f);
            }
        }

        private static void StripColliders(GameObject root)
        {
            if (root == null) return;
            foreach (var col in root.GetComponentsInChildren<Collider>())
                Object.Destroy(col);
        }
    }
}
