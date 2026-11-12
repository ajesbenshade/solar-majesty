using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// Per-civ meshes for Inn, Guild Hall, HAB, Power and Defense. The drop path follows the
    /// citadel stem. A missing or empty file keeps the shared hero kit or the procedural kit.
    /// The neutral civ and the Commons castle are unchanged.
    /// </summary>
    public class CivBuildingRoutingTests
    {
        private static readonly CivId[] Civs =
        {
            CivId.LumenConcord, CivId.TetherExchange, CivId.BastionMandate, CivId.PathfinderAccord
        };

        /// <summary>A muted accent, the same one the castle tests paint.</summary>
        private static readonly Color Slate = new Color(0.22f, 0.32f, 0.50f);

        private GameObject _root;
        private readonly List<GameObject> _sources = new List<GameObject>();
        private readonly List<Material> _materials = new List<Material>();

        [SetUp]
        public void SetUp()
        {
            CivBuildingLoader.ResetForTests();
            ModularBuildingFactory.BindCiv(null);
            LogAssert.ignoreFailingMessages = true;
            _root = new GameObject("CivBuildingRoutingRoot");
        }

        [TearDown]
        public void TearDown()
        {
            CivBuildingLoader.ResetForTests();
            ModularBuildingFactory.BindCiv(null);
            if (_root != null) Object.DestroyImmediate(_root);
            for (int i = 0; i < _sources.Count; i++)
                if (_sources[i] != null) Object.DestroyImmediate(_sources[i]);
            for (int i = 0; i < _materials.Count; i++)
                if (_materials[i] != null) Object.DestroyImmediate(_materials[i]);
        }

        // ---- the drop convention ----------------------------------------------------------

        [Test]
        public void ResourcePaths_FollowTheCitadelStem()
        {
            Assert.AreEqual("Ziggurat", CivBuildingCatalog.ArchitectureOf(CivId.LumenConcord));
            Assert.AreEqual("Ring", CivBuildingCatalog.ArchitectureOf(CivId.TetherExchange));
            Assert.AreEqual("Dome", CivBuildingCatalog.ArchitectureOf(CivId.BastionMandate));
            Assert.AreEqual("Arcology", CivBuildingCatalog.ArchitectureOf(CivId.PathfinderAccord),
                "Pathfinder Accord uses the Arcology castle stem");
            Assert.IsNull(CivBuildingCatalog.ArchitectureOf(CivId.None));

            Assert.AreEqual("CivBuildings/SM_Ziggurat_Inn", Path(CivId.LumenConcord, BuildingCategory.Inn));
            Assert.AreEqual("CivBuildings/SM_Ziggurat_GuildHall", Path(CivId.LumenConcord, BuildingCategory.GuildHall));
            Assert.AreEqual("CivBuildings/SM_Ziggurat_HAB", Path(CivId.LumenConcord, BuildingCategory.Habitat));
            Assert.AreEqual("CivBuildings/SM_Ziggurat_Power", Path(CivId.LumenConcord, BuildingCategory.Power));
            Assert.AreEqual("CivBuildings/SM_Ziggurat_Defense", Path(CivId.LumenConcord, BuildingCategory.Defense));
            Assert.AreEqual("CivBuildings/SM_Ring_Inn", Path(CivId.TetherExchange, BuildingCategory.Inn));
            Assert.AreEqual("CivBuildings/SM_Dome_GuildHall", Path(CivId.BastionMandate, BuildingCategory.GuildHall));
            Assert.AreEqual("CivBuildings/SM_Arcology_Defense", Path(CivId.PathfinderAccord, BuildingCategory.Defense));
            Assert.AreEqual("Assets/Resources/CivBuildings/SM_Arcology_Inn.fbx",
                CivBuildingCatalog.AssetPath(CivId.PathfinderAccord, BuildingCategory.Inn));

            Assert.IsNull(Path(CivId.None, BuildingCategory.Inn));
            Assert.IsNull(Path(CivId.LumenConcord, BuildingCategory.Commons), "the Commons is the castle");
            Assert.IsNull(Path(CivId.LumenConcord, BuildingCategory.Farm));
            Assert.IsNull(Path(CivId.LumenConcord, BuildingCategory.Market));
            Assert.IsNull(Path(CivId.LumenConcord, BuildingCategory.Watchtower));

            var paths = new HashSet<string>();
            foreach (var civ in Civs)
                foreach (var cat in CivBuildingCatalog.Batch)
                    Assert.IsTrue(paths.Add(Path(civ, cat)), Path(civ, cat));
            Assert.AreEqual(20, paths.Count, "four civs, five buildings");

            // The mesh follows the castle this colony draws, not a second table.
            var ringed = new CivModifiers(new CivDefinition
            {
                id = CivId.LumenConcord,
                castleKey = "castle.ring_citadel"
            });
            Assert.AreEqual("Ring", CivBuildingCatalog.ArchitectureOf(ringed));
            Assert.AreEqual("CivBuildings/SM_Ring_HAB", CivBuildingCatalog.ResourcePath(ringed, BuildingCategory.Habitat));
            var noCastle = new CivModifiers(new CivDefinition { id = CivId.LumenConcord, castleKey = "" });
            Assert.IsNull(CivBuildingCatalog.ArchitectureOf(noCastle));
            Assert.IsNull(CivBuildingCatalog.ResourcePath(noCastle, BuildingCategory.Inn));

            foreach (var cat in CivBuildingCatalog.Batch)
            {
                Assert.AreEqual(4, ColonyLayout.FootprintSide(cat), cat + " is a 4×4 plot");
                Assert.AreEqual(6f, ColonyLayout.FootprintMeters(cat), 0.001f, cat + " is 6 m");
            }

            float playZoom = 6f / (2f * ColonyLayout.CampusOrthoSize);
            Assert.Less(CivBuilding.Lod1Below, playZoom, "play zoom keeps LOD0 on a 6 m building");
            Assert.Greater(CivBuilding.Lod2Below, BuildingLodRules.CullScreenHeight);
            Assert.Less(CivBuilding.Lod2Below, CivBuilding.Lod1Below);
            Assert.Greater(CivCastle.Lod1Below, playZoom, "the castle threshold is sized for the 18 m Commons");
        }

        // ---- missing art -------------------------------------------------------------------

        [Test]
        public void MissingDrop_KeepsTheSharedKit_AndWarnsOncePerPath()
        {
            var warnings = new List<string>();
            Application.logMessageReceived += OnLog;
            try
            {
                ModularBuildingFactory.BindCiv(CivModifiers.For(CivId.LumenConcord));
                for (int pass = 0; pass < 2; pass++)
                    foreach (var cat in CivBuildingCatalog.Batch)
                        AssertShared(Spawn(cat), cat);
            }
            finally
            {
                Application.logMessageReceived -= OnLog;
            }

            Assert.AreEqual(CivBuildingCatalog.Batch.Count, warnings.Count, "one warning per missing file");
            foreach (var cat in CivBuildingCatalog.Batch)
            {
                string path = Path(CivId.LumenConcord, cat);
                Assert.IsTrue(warnings.Exists(w => w.Contains(path)), path);
            }

            void OnLog(string message, string stack, LogType type)
            {
                if (type == LogType.Warning && message.Contains("[CivBuilding]"))
                    warnings.Add(message);
            }
        }

        [Test]
        public void Neutral_Unrouted_AndEmpty_DoNotReplaceTheSharedKit()
        {
            var cube = Cube("SM_Ziggurat_HAB");
            CivBuildingLoader.LoadOverride = _ => cube;

            ModularBuildingFactory.BindCiv(null);
            var neutral = Spawn(BuildingCategory.Habitat);
            Assert.IsNull(neutral.GetComponent<CivBuilding>(), "a null civ is the Commons kit");
            Assert.IsNotNull(Find(neutral.transform, "Dress_HabShell"));

            ModularBuildingFactory.BindCiv(CivModifiers.For(CivId.None));
            var none = Spawn(BuildingCategory.Habitat);
            Assert.IsNull(none.GetComponent<CivBuilding>());
            Assert.IsNotNull(Find(none.transform, "Dress_HabShell"));

            ModularBuildingFactory.BindCiv(CivModifiers.For(CivId.LumenConcord));
            var farm = Spawn(BuildingCategory.Farm);
            Assert.IsNull(farm.GetComponent<CivBuilding>(), "Farm is outside the batch");
            Assert.IsNull(Find(farm.transform, "CivBuilding_Ziggurat_Farm"));

            var empty = Track(new GameObject("SM_Ziggurat_Power"));
            CivBuildingLoader.LoadOverride = path => path.EndsWith("SM_Ziggurat_Power") ? empty : null;
            var power = Spawn(BuildingCategory.Power);
            Assert.IsNull(power.GetComponent<CivBuilding>(), "an empty drop is not a building");
            Assert.IsNotNull(Find(power.transform, "PwrHull"));
            Assert.IsNull(Find(power.transform, "CivBuilding_Ziggurat_Power"));
        }

        [Test]
        public void Commons_StaysOnTheCastlePath()
        {
            var cube = Cube("NotACastle", "Hull");
            CivBuildingLoader.LoadOverride = _ => cube;
            ModularBuildingFactory.BindCiv(CivModifiers.For(CivId.LumenConcord, Slate));
            var go = ModularBuildingFactory.Spawn(BuildingCategory.Commons, Vector3.zero, _root.transform);
            Assert.IsNotNull(go.GetComponent<CivCastle>());
            Assert.IsNull(go.GetComponent<CivBuilding>());
            Assert.IsNotNull(Find(go.transform, "CitadelCrownY"));
        }

        // ---- a dropped mesh ----------------------------------------------------------------

        [Test]
        public void DroppedMesh_ReplacesTheSharedKit_AtAuthoredScale_WithCitadelSlots()
        {
            var source = Track(new GameObject("SM_Ziggurat_Inn"));
            var slots = new[]
            {
                CivCastleLoader.Slot.Trim, CivCastleLoader.Slot.Carbon, CivCastleLoader.Slot.TeamColour,
                CivCastleLoader.Slot.Accent, CivCastleLoader.Slot.CoreEmission
            };
            var originals = new Material[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                originals[i] = Mat(slots[i].ToString());
                var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
                part.name = slots[i].ToString();
                part.transform.SetParent(source.transform, false);
                part.GetComponent<Renderer>().sharedMaterial = originals[i];
            }

            CivBuildingLoader.LoadOverride = path => path == Path(CivId.LumenConcord, BuildingCategory.Inn) ? source : null;
            var civ = CivModifiers.For(CivId.LumenConcord, Slate);
            ModularBuildingFactory.BindCiv(civ);
            var go = Spawn(BuildingCategory.Inn);
            var built = go.GetComponent<CivBuilding>();

            Assert.IsNotNull(built);
            Assert.AreEqual(CivId.LumenConcord, built.Civ);
            Assert.AreEqual(BuildingCategory.Inn, built.Category);
            Assert.AreEqual("Ziggurat", built.Architecture);
            Assert.AreEqual(Path(CivId.LumenConcord, BuildingCategory.Inn), built.ResourcePath);
            Assert.AreEqual("SM_Ziggurat_Inn", built.Model.name);
            Assert.IsNull(Find(go.transform, "CoreMesh"), "the shared hero kit is not also attached");
            Assert.AreEqual(0, go.GetComponentsInChildren<Collider>(true).Length);

            var wrap = Find(go.transform, "CivBuilding_Ziggurat_Inn");
            Assert.IsNotNull(wrap);
            float yawGap = Quaternion.Angle(wrap.localRotation, Quaternion.Euler(0f, CivCastleLoader.ImportYawDegrees, 0f));
            Assert.AreEqual(0f, yawGap, 0.01f, "front faces -Z, same yaw as the castles");
            Assert.AreEqual(1f, wrap.localScale.x, 1e-4f);
            Assert.AreEqual(1f, wrap.localScale.y, 1e-4f);
            Assert.AreEqual(1f, wrap.localScale.z, 1e-4f);
            Assert.AreEqual(1f, built.Model.localScale.x, 1e-4f, "authored scale is kept; the 6 m plot is not refitted");
            Assert.AreEqual(1f, built.Model.localScale.y, 1e-4f);
            Assert.AreEqual(1f, built.Model.localScale.z, 1e-4f);
            float width = built.Model.GetComponentInChildren<Renderer>().bounds.size.x;
            Assert.Greater(width, 0.5f);
            Assert.Less(width, 2f, "a 1 m cube is not stretched to the 6 m plot");

            var spec = CivCastleCatalog.For(CivId.LumenConcord);
            var sheet = Resources.Load<Texture2D>(CivCastleCatalog.TrimTexturePrefix + "BaseColor");
            Assert.IsNotNull(sheet, "shared citadel trim sheet");
            var seen = new HashSet<CivCastleLoader.Slot>();
            foreach (var r in built.Model.GetComponentsInChildren<Renderer>(true))
            {
                var m = r.sharedMaterial;
                Assert.IsNotNull(m, r.name);
                StringAssert.StartsWith(CivCastleLoader.MaterialPrefix, m.name, r.name);
                var slot = CivCastleLoader.SlotOf(m);
                seen.Add(slot);
                AssertColour(CivCastleLoader.SlotColour(slot, spec, civ), CivCastleLoader.BaseColour(m), r.name);
                if (slot == CivCastleLoader.Slot.Trim)
                    Assert.AreSame(sheet, m.GetTexture("_BaseMap"), "Trim reads the shared trim sheet");
                if (slot == CivCastleLoader.Slot.CoreEmission)
                    Assert.IsTrue(m.IsKeywordEnabled("_EMISSION"));
            }
            Assert.AreEqual(slots.Length, seen.Count, "every citadel slot was painted");

            for (int i = 0; i < originals.Length; i++)
            {
                var part = source.transform.Find(slots[i].ToString());
                Assert.AreEqual(slots[i].ToString(), part.GetComponent<Renderer>().sharedMaterial.name,
                    "the imported material is left untouched");
            }

            // A different civ does not pick up this drop.
            ModularBuildingFactory.BindCiv(CivModifiers.For(CivId.TetherExchange));
            var ring = Spawn(BuildingCategory.Inn);
            Assert.IsNull(ring.GetComponent<CivBuilding>());
            Assert.IsNotNull(Find(ring.transform, "CoreMesh"), "the Exchange keeps the shared Inn");

            // The same file painted for the Dome uses the Dome's trim, not the Ziggurat's.
            var domeSource = Cube("SM_Dome_Defense");
            domeSource.GetComponent<Renderer>().sharedMaterial = Mat("Trim");
            CivBuildingLoader.LoadOverride = path =>
                path == Path(CivId.BastionMandate, BuildingCategory.Defense) ? domeSource : null;
            var mandate = CivModifiers.For(CivId.BastionMandate, Slate);
            ModularBuildingFactory.BindCiv(mandate);
            var dome = Spawn(BuildingCategory.Defense);
            var domeMat = dome.GetComponent<CivBuilding>().Model.GetComponentInChildren<Renderer>().sharedMaterial;
            AssertColour(CivCastleCatalog.For(CivId.BastionMandate).TrimTint,
                CivCastleLoader.BaseColour(domeMat), "Dome trim");
            Assert.Greater(Mathf.Abs(spec.TrimTint.r - CivCastleCatalog.For(CivId.BastionMandate).TrimTint.r), 0.05f,
                "Dome trim is not the Ziggurat tint");
            Assert.IsNull(Find(dome.transform, "Dress_S3_DefCore"), "the procedural battery is not under the civ mesh");
        }

        [Test]
        public void DroppedMesh_UsesLodSuffixes_AndTheGhostShowsFullDetail()
        {
            var source = Track(new GameObject("SM_Arcology_GuildHall"));
            AddLod(source.transform, "Hall_LOD0");
            AddLod(source.transform, "Hall_LOD1");
            AddLod(source.transform, "Hall_LOD2");
            AddLod(source.transform, "Lamp");
            CivBuildingLoader.LoadOverride = path =>
                path == Path(CivId.PathfinderAccord, BuildingCategory.GuildHall) ? source : null;

            ModularBuildingFactory.BindCiv(CivModifiers.For(CivId.PathfinderAccord));
            var go = Spawn(BuildingCategory.GuildHall);
            var built = go.GetComponent<CivBuilding>();
            Assert.IsNotNull(built);
            Assert.AreEqual("Arcology", built.Architecture);
            Assert.AreEqual(1, built.Lod0.Length);
            Assert.AreEqual(1, built.Lod1.Length);
            Assert.AreEqual(1, built.Lod2.Length);
            Assert.AreEqual(1, built.Rest.Length, "a mesh with no LOD suffix stays in every level");
            Assert.IsNull(go.GetComponent<LODGroup>(), "the building root does not take a second group");
            Assert.IsNotNull(built.Group);
            Assert.AreSame(built.Model.GetComponent<LODGroup>(), built.Group);
            Assert.AreEqual(3, built.Group.lodCount);
            Assert.AreEqual(1, go.GetComponentsInChildren<LODGroup>(true).Length);
            var lods = built.Group.GetLODs();
            CollectionAssert.Contains(lods[0].renderers, built.Lod0[0]);
            CollectionAssert.Contains(lods[1].renderers, built.Lod1[0]);
            CollectionAssert.Contains(lods[2].renderers, built.Lod2[0]);
            foreach (var lod in lods)
                CollectionAssert.Contains(lod.renderers, built.Rest[0], "the lamp is in every LOD");
            Assert.AreEqual(CivBuilding.Lod1Below, lods[0].screenRelativeTransitionHeight, 1e-4f);
            Assert.AreEqual(CivBuilding.Lod2Below, lods[1].screenRelativeTransitionHeight, 1e-4f);

            var ghost = ModularBuildingFactory.Spawn(
                BuildingCategory.GuildHall, Vector3.zero, _root.transform, ghost: true);
            var ghostBuilt = ghost.GetComponent<CivBuilding>();
            Assert.IsNotNull(ghostBuilt);
            foreach (var r in ghostBuilt.Lod0) Assert.IsTrue(r.enabled, r.name);
            foreach (var r in ghostBuilt.Lod1) Assert.IsFalse(r.enabled, r.name);
            foreach (var r in ghostBuilt.Lod2) Assert.IsFalse(r.enabled, r.name);
            foreach (var r in ghostBuilt.Rest) Assert.IsTrue(r.enabled, r.name);
            Assert.AreEqual(0, ghost.GetComponentsInChildren<Collider>(true).Length);
        }

        // ---- helpers -----------------------------------------------------------------------

        private static string Path(CivId civ, BuildingCategory category) =>
            CivBuildingCatalog.ResourcePath(civ, category);

        private GameObject Spawn(BuildingCategory category) =>
            ModularBuildingFactory.Spawn(category, Vector3.zero, _root.transform);

        private void AssertShared(GameObject go, BuildingCategory category)
        {
            Assert.IsNull(go.GetComponent<CivBuilding>(), category + " has no civ mesh yet");
            switch (category)
            {
                case BuildingCategory.Habitat:
                    Assert.IsNotNull(Find(go.transform, "Dress_HabShell"), "procedural HAB");
                    break;
                case BuildingCategory.Power:
                    Assert.IsNotNull(Find(go.transform, "PwrHull"), "procedural Power");
                    break;
                case BuildingCategory.Defense:
                    Assert.IsNotNull(Find(go.transform, "Dress_S3_DefCore"), "procedural Defense");
                    break;
                case BuildingCategory.Inn:
                case BuildingCategory.GuildHall:
                    Assert.IsNotNull(Find(go.transform, "CoreMesh"), category + " shared hero kit");
                    break;
            }
        }

        private GameObject Cube(string name)
        {
            var go = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            go.name = name;
            return go;
        }

        private void AddLod(Transform parent, string name)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.GetComponent<Renderer>().sharedMaterial = Mat("Trim");
        }

        private Material Mat(string name)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                         ?? Shader.Find("Standard");
            Assert.IsNotNull(shader, "a lit shader to stand in for an FBX slot");
            var mat = new Material(shader) { name = name };
            _materials.Add(mat);
            return mat;
        }

        private GameObject Track(GameObject go)
        {
            _sources.Add(go);
            return go;
        }

        private static Transform Find(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        private static void AssertColour(Color expected, Color actual, string what)
        {
            Assert.AreEqual(expected.r, actual.r, 0.01f, what + " (r)");
            Assert.AreEqual(expected.g, actual.g, 0.01f, what + " (g)");
            Assert.AreEqual(expected.b, actual.b, 0.01f, what + " (b)");
        }
    }
}
