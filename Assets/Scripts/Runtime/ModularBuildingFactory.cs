using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Unique bilaterally-symmetric building kits sized to their grid footprints.
    /// Cardinal airlock ports sit on the hull faces so Utility junctions click flush.
    /// Phase 4 HAB / keep / pad / extractors / solar / Defense dispatch to HeroBuildingKits.
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
                    _bodyHull = new Color(0.58f, 0.66f, 0.58f);
                    break;
                case CelestialBodyId.Luna:
                    _bodyHull = new Color(0.68f, 0.70f, 0.72f);
                    break;
                case CelestialBodyId.Mars:
                    _bodyHull = new Color(0.72f, 0.52f, 0.42f);
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
                var airlock = ColonyVisualUtility.SpawnPlusConnector(position, parent, Mathf.Min(worldW, worldD));
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
            AttachCardinalAirlocks(root.transform, worldW * 0.5f, worldD * 0.5f);

            ColonyVisualUtility.EnsureUrpMaterials(root);
            if (!ghost)
                IndustrialArtDressing.SetTintOverlay(root, Color.Lerp(Color.white, _bodyHull, 0.18f));
            ColonyVisualUtility.SnapToGround(root);
            if (ghost)
                StripColliders(root);
            return root;
        }

        private static void BuildCore(Transform root, BuildingCategory cat, float w, float d)
        {
            GameObject uniqueMesh = UniqueMeshPrefab(cat);
            if (uniqueMesh != null)
            {
                var wrap = new GameObject("CoreMesh");
                wrap.transform.SetParent(root, false);
                wrap.transform.localPosition = Vector3.zero;
                wrap.transform.localRotation = Quaternion.identity;
                var core = ColonyVisualUtility.InstantiateOriented(uniqueMesh, root.position, wrap.transform);
                core.name = "Mesh";
                core.transform.localPosition = Vector3.zero;
                FitToFootprint(wrap, w, d);
                return;
            }

            switch (cat)
            {
                case BuildingCategory.Habitat:
                    HeroBuildingKits.BuildHabitat(root, w, d, HeroHull());
                    break;
                case BuildingCategory.Palace:
                    HeroBuildingKits.BuildKeep(root, w, d, HeroHull());
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
                    BuildWorkshop(root, w, d, new Color(0.35f, 0.85f, 1f), tall: false);
                    break;
                case BuildingCategory.EngineerWorkshop:
                    BuildWorkshop(root, w, d, new Color(1f, 0.65f, 0.2f), tall: false);
                    break;
                case BuildingCategory.DefenseWorkshop:
                    BuildWorkshop(root, w, d, new Color(0.95f, 0.35f, 0.35f), tall: true);
                    break;
                case BuildingCategory.MedicWorkshop:
                    BuildWorkshop(root, w, d, new Color(0.45f, 0.95f, 0.55f), tall: false);
                    break;
                case BuildingCategory.HarvesterWorkshop:
                    BuildWorkshop(root, w, d, new Color(0.82f, 0.62f, 0.22f), tall: false);
                    break;
                case BuildingCategory.SurveyorWorkshop:
                    BuildWorkshop(root, w, d, new Color(0.45f, 0.82f, 0.95f), tall: false);
                    break;
                case BuildingCategory.TerraformerWorkshop:
                    BuildWorkshop(root, w, d, new Color(0.42f, 0.82f, 0.38f), tall: false);
                    break;
                case BuildingCategory.CourierWorkshop:
                    BuildWorkshop(root, w, d, new Color(0.95f, 0.72f, 0.28f), tall: false);
                    break;
                case BuildingCategory.GeologistWorkshop:
                    BuildWorkshop(root, w, d, new Color(0.68f, 0.52f, 0.32f), tall: false);
                    break;
                case BuildingCategory.SentinelWorkshop:
                    BuildWorkshop(root, w, d, new Color(0.78f, 0.38f, 0.22f), tall: true);
                    break;
                case BuildingCategory.GuildHall:
                case BuildingCategory.Inn:
                    BuildInn(root, w, d);
                    break;
                case BuildingCategory.ClimateLoom:
                    BuildWonder(root, w, d, new Color(0.38f, 0.82f, 0.48f), 2.8f);
                    break;
                case BuildingCategory.AegisSpire:
                    BuildWonder(root, w, d, new Color(0.45f, 0.72f, 1f), 3.6f);
                    break;
                case BuildingCategory.DeepArchive:
                    BuildWonder(root, w, d, new Color(0.42f, 0.78f, 0.88f), 2.4f);
                    break;
                default:
                    BuildGenericModule(root, w, d, AccentFor(cat));
                    break;
            }
        }

        private static GameObject UniqueMeshPrefab(BuildingCategory cat)
        {
            switch (cat)
            {
                case BuildingCategory.Mining:
                case BuildingCategory.Laboratory:
                    return BuildingVisualCatalog.LoadPrefab(cat);
                default:
                    return null;
            }
        }

        private static void FitToFootprint(GameObject go, float targetX, float targetZ)
        {
            if (go == null) return;
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends == null || rends.Length == 0) return;

            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++)
            {
                if (rends[i] != null)
                    b.Encapsulate(rends[i].bounds);
            }

            // Scale the unrotated wrapper so world XZ matches the footprint even when
            // the FBX child carries Blender's -90° X import rotation.
            float sx = targetX / Mathf.Max(0.05f, b.size.x);
            float sz = targetZ / Mathf.Max(0.05f, b.size.z);
            Vector3 ls = go.transform.localScale;
            go.transform.localScale = new Vector3(ls.x * sx, ls.y * Mathf.Min(sx, sz), ls.z * sz);
        }

        private static void BuildWorkshop(Transform root, float w, float d, Color accent, bool tall)
        {
            float h = tall ? 2.4f : 1.8f;
            Part(root, "Bay", PrimitiveType.Cube,
                new Vector3(0f, h * 0.5f, 0f),
                new Vector3(w, h, d),
                HullColor());
            Part(root, "Door_L", PrimitiveType.Cube,
                new Vector3(-w * 0.22f, 0.85f, d * 0.5f),
                new Vector3(w * 0.28f, 1.5f, 0.12f),
                accent);
            Part(root, "Door_R", PrimitiveType.Cube,
                new Vector3(w * 0.22f, 0.85f, d * 0.5f),
                new Vector3(w * 0.28f, 1.5f, 0.12f),
                accent);
            Part(root, "Stack_L", PrimitiveType.Cylinder,
                new Vector3(-w * 0.32f, h + 0.45f, -d * 0.2f),
                new Vector3(0.4f, 0.55f, 0.4f),
                accent);
            Part(root, "Stack_R", PrimitiveType.Cylinder,
                new Vector3(w * 0.32f, h + 0.45f, -d * 0.2f),
                new Vector3(0.4f, 0.55f, 0.4f),
                accent);
        }

        private static void BuildInn(Transform root, float w, float d)
        {
            Part(root, "Hall", PrimitiveType.Cube,
                new Vector3(0f, 1.15f, 0f),
                new Vector3(w * 0.72f, 2.3f, d),
                HullColor());
            Part(root, "Wing_L", PrimitiveType.Cube,
                new Vector3(-w * 0.38f, 0.85f, 0f),
                new Vector3(w * 0.28f, 1.7f, d * 0.72f),
                AccentFor(BuildingCategory.Inn));
            Part(root, "Wing_R", PrimitiveType.Cube,
                new Vector3(w * 0.38f, 0.85f, 0f),
                new Vector3(w * 0.28f, 1.7f, d * 0.72f),
                AccentFor(BuildingCategory.Inn));
        }

        private static void BuildWonder(Transform root, float w, float d, Color accent, float height)
        {
            Part(root, "Plinth", PrimitiveType.Cube,
                new Vector3(0f, 0.35f, 0f),
                new Vector3(w * 0.92f, 0.7f, d * 0.92f),
                HullColor());
            Part(root, "Tower", PrimitiveType.Cube,
                new Vector3(0f, height * 0.5f + 0.5f, 0f),
                new Vector3(w * 0.42f, height, d * 0.42f),
                accent);
            Part(root, "Cap", PrimitiveType.Cylinder,
                new Vector3(0f, height + 0.85f, 0f),
                new Vector3(w * 0.28f, 0.35f, d * 0.28f),
                new Color(0.95f, 0.42f, 0.08f));
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

        private static void AttachCardinalAirlocks(Transform root, float halfW, float halfD)
        {
            const float bore = 1.25f;
            const float inset = 1.5f;
            const float outset = 1.35f;
            float y = 0.7f;
            DockSleeve(root, "Airlock_N", new Vector3(0f, y, halfD), Vector3.forward, bore, inset, outset);
            DockSleeve(root, "Airlock_S", new Vector3(0f, y, -halfD), Vector3.back, bore, inset, outset);
            DockSleeve(root, "Airlock_E", new Vector3(halfW, y, 0f), Vector3.right, bore, inset, outset);
            DockSleeve(root, "Airlock_W", new Vector3(-halfW, y, 0f), Vector3.left, bore, inset, outset);
        }

        private static void DockSleeve(
            Transform parent,
            string name,
            Vector3 facePos,
            Vector3 outward,
            float bore,
            float inset,
            float outset)
        {
            float length = inset + outset;
            Vector3 center = facePos + outward.normalized * ((outset - inset) * 0.5f);
            bool ns = Mathf.Abs(outward.z) >= Mathf.Abs(outward.x);
            Vector3 scale = ns
                ? new Vector3(bore, bore, length)
                : new Vector3(length, bore, bore);
            Part(parent, name, PrimitiveType.Cube, center, scale, AirlockColor());
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
        private static Color HeroHull() => Color.Lerp(new Color(0.88f, 0.90f, 0.93f), _bodyHull, 0.18f);
        private static Color AirlockColor() => new Color(0.96f, 0.42f, 0.08f);

        private static Color AccentFor(BuildingCategory cat)
        {
            switch (cat)
            {
                case BuildingCategory.Palace: return new Color(0.85f, 0.72f, 0.35f);
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
