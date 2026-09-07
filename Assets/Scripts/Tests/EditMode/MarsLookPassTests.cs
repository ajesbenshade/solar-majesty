using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// Dream Loop pass 2 (spaced overseer concept). Locks the look deltas that a Game-tab still
    /// is judged on: key light from screen upper-left with down-right shadows, pale haze that
    /// starts past the campus, gritty pebbled regolith, footprint-sized yards, and the concept
    /// silhouettes (geodesic Commons, canvas awnings, spherical extractor tanks, comms mast).
    /// None of this stamps Phase 4 EXIT — GD still shoots the still.
    /// </summary>
    public class MarsLookPassTests
    {
        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("MarsLookPassTestRoot");
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                Object.DestroyImmediate(_root);
        }

        [Test]
        public void Mars_KeyLight_IsHighFromScreenLeft_ShadowsFallDownRight()
        {
            var mars = CelestialBodyCatalog.Mars();
            Assert.That(mars.SunEuler.x, Is.InRange(30f, 42f),
                "concept shadows are ~1.4× object height, not a 20° raking sun");
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(148f, mars.SunEuler.y)), Is.LessThan(15f),
                "with the iso rig at yaw 45, sun yaw ~148 throws shadows down-right and lights -X faces");
        }

        [Test]
        public void Mars_Haze_IsPaleAndAnchoredPastTheCampus()
        {
            var mars = CelestialBodyCatalog.Mars();
            Assert.That(mars.FogColor.r, Is.GreaterThan(0.8f), "haze reads pale dust, not saturated orange");
            Assert.That(mars.FogColor.g, Is.GreaterThan(0.5f));
            Assert.That(mars.FogStart, Is.GreaterThanOrEqualTo(30f));
            Assert.That(DemoAtmosphere.MarsHazeStartOffset, Is.GreaterThan(0f),
                "haze must start past the focal depth so hulls at the focus stay white");
            Assert.That(DemoAtmosphere.MarsHazeSpan, Is.GreaterThanOrEqualTo(60f),
                "frame top at ortho 10 should sit near 15 % haze, not a wall of fog");
        }

        [Test]
        public void FocalGroundDepth_FallsBackWithoutCamera()
        {
            Assert.AreEqual(34f, DemoAtmosphere.FocalGroundDepth(null, 34f));
        }

        [Test]
        public void FocalGroundDepth_UsesCameraHeightAndPitch()
        {
            var camGo = new GameObject("Cam");
            camGo.transform.SetParent(_root.transform);
            var cam = camGo.AddComponent<Camera>();
            camGo.transform.position = new Vector3(0f, 20f, 0f);
            camGo.transform.rotation = Quaternion.Euler(30f, 45f, 0f);
            float depth = DemoAtmosphere.FocalGroundDepth(cam, 1f);
            Assert.That(depth, Is.EqualTo(40f).Within(0.5f), "20 m up at 30° pitch → 40 m to the ground");
        }

        [Test]
        public void Mars_Ground_HasShaderPebbles_EarthDoesNot()
        {
            Assert.That(PlanetaryMapDressing.PebbleDensityFor(CelestialBodyCatalog.Mars()), Is.GreaterThan(0.2f));
            Assert.AreEqual(0f, PlanetaryMapDressing.PebbleDensityFor(CelestialBodyCatalog.Earth()));
            Assert.That(PlanetaryMapDressing.MarsPebbleCount, Is.InRange(48, 120),
                "enough stones to read as a field, few enough for the SRP batcher");
        }

        [Test]
        public void HullSkirt_IsStrongestOnMars()
        {
            float mars = IndustrialArtDressing.SkirtScaleFor(CelestialBodyCatalog.Mars());
            float earth = IndustrialArtDressing.SkirtScaleFor(CelestialBodyCatalog.Earth());
            Assert.AreEqual(1f, mars);
            Assert.That(earth, Is.LessThan(mars), "meadow hulls do not take a red dust sill");
            Assert.That(IndustrialArtDressing.HullSkirtHeight, Is.InRange(0.5f, 1.5f));
        }

        [Test]
        public void Yards_ScaleWithFootprint_AndWrapTheModule()
        {
            float hab = CampusDressing.YardDiameter(BuildingCategory.Habitat, 6f, mars: true);
            float pad = CampusDressing.YardDiameter(BuildingCategory.LandingPad, 9f, mars: true);
            Assert.That(hab, Is.GreaterThan(6f), "yard must extend past the hull, not hide inside it");
            Assert.That(hab, Is.LessThan(12f), "spaced campus — yards do not merge into one plaza");
            Assert.That(pad, Is.GreaterThan(hab));
        }

        [Test]
        public void Commons_HasGeodesicLatticeMesh()
        {
            var commons = ModularBuildingFactory.Spawn(
                BuildingCategory.Commons, Vector3.zero, _root.transform);
            var lattice = commons.transform.Find(HeroBuildingKits.GeodesicLatticeName);
            Assert.IsNotNull(lattice, "concept Commons is a geodesic dome, not a smooth sphere");
            var mf = lattice.GetComponent<MeshFilter>();
            Assert.IsNotNull(mf);
            Assert.IsNotNull(mf.sharedMesh);
            Assert.That(mf.sharedMesh.vertexCount, Is.GreaterThan(24 * 20),
                "at least the upper-hemisphere struts of a frequency-2 icosphere");
            Assert.IsNotNull(commons.transform.Find("CommonsCupolaStripe"), "orange cupola collar");
        }

        [Test]
        public void Workshop_AndInn_HaveCanvasAwnings()
        {
            var shop = ModularBuildingFactory.Spawn(
                BuildingCategory.EngineerWorkshop, Vector3.zero, _root.transform);
            var inn = ModularBuildingFactory.Spawn(
                BuildingCategory.Inn, new Vector3(20f, 0f, 0f), _root.transform);
            Assert.IsNotNull(shop.transform.Find("ShopCanvasAwning"));
            Assert.IsNotNull(shop.transform.Find("ShopAwningSteelPole_0"));
            Assert.IsNotNull(inn.transform.Find("InnCanvasAwning"));
            Assert.IsNull(inn.transform.Find("InnCanopy"), "carbon slab canopy replaced by canvas");
        }

        [Test]
        public void RegolithExtractor_HasSphericalTanksAndColumn()
        {
            var camp = ModularBuildingFactory.Spawn(
                BuildingCategory.RegolithCamp, Vector3.zero, _root.transform);
            Assert.IsNotNull(camp.transform.Find("RegSphereTank_L"));
            Assert.IsNotNull(camp.transform.Find("RegSphereTank_R"));
            Assert.IsNotNull(camp.transform.Find("RegColumnTank"));
            Assert.IsNull(camp.transform.Find("Dress_RegTank_L"), "dust drums replaced by pressure spheres");
        }

        [Test]
        public void SolarField_HasLatticeCommsMast()
        {
            var pwr = ModularBuildingFactory.Spawn(
                BuildingCategory.Power, Vector3.zero, _root.transform);
            Assert.IsNotNull(pwr.transform.Find("PwrMastSteelStrut_0"));
            Assert.IsNotNull(pwr.transform.Find("PwrMastSteelStrut_2"));
            Assert.IsNotNull(pwr.transform.Find("PwrMastBeacon"));
        }

        [Test]
        public void StillOrtho_PullsBackForWideCampus_NeverInsidePlayOrtho()
        {
            // Capture-sized campus (pad + solar + Commons + HABs ≈ 20×20 cells) on a 1.53 Game tab.
            float wide = StillCampusDensity.FitStillOrtho(
                new Vector2Int(0, 0), new Vector2Int(20, 20), StillCampusDensity.DefaultCellSize, 1.53f);
            Assert.AreEqual(StillCampusDensity.ConceptStillOrthoMax, wide,
                "the Capture had hulls on every edge — a wide campus must pull back to the concept ceiling");

            // Commons + airlock + one HAB stays at play ortho: no zoom-in past 10.
            float small = StillCampusDensity.FitStillOrtho(
                new Vector2Int(0, 0), new Vector2Int(6, 10), StillCampusDensity.DefaultCellSize, 2.4f);
            Assert.AreEqual(StillCampusDensity.PlayCampusOrthoSize, small);
            Assert.That(StillCampusDensity.ConceptCampusFill, Is.InRange(0.45f, 0.65f));
        }

        [Test]
        public void DockBore_IsAboutThirtyPercentOfHabDiameter()
        {
            // HAB shell radius for the 6 m footprint = 6 × 0.92 / 3.
            float habDia = 2f * (6f * 0.92f / 3f);
            float ratio = ColonyVisualUtility.DockBore / habDia;
            Assert.That(ratio, Is.InRange(0.26f, 0.34f),
                "Capture tubes were ~40 % of the HAB; concept corridors are ~30 %");
        }

        [Test]
        public void ShieldCue_IsFlatGroundRing_NotGlossyBubble()
        {
            var defense = ModularBuildingFactory.Spawn(
                BuildingCategory.Defense, Vector3.zero, _root.transform);
            var data = ScriptableObject.CreateInstance<BuildingData>();
            data.category = BuildingCategory.Defense;
            data.footprintWidth = 4;
            data.footprintHeight = 4;
            CampusDressing.DressPlaced(data, defense, CelestialBodyCatalog.Mars());
            var shield = defense.transform.Find("Dress_Shield");
            Assert.IsNotNull(shield);
            Assert.That(shield.localScale.y, Is.LessThan(0.05f), "coverage cue lies flat on the ground");
            var mat = shield.GetComponent<Renderer>().sharedMaterial;
            Assert.That(mat.GetColor("_BaseColor").a, Is.LessThan(0.2f));
            Assert.That(mat.GetFloat("_Smoothness"), Is.LessThan(0.1f), "no specular flare under the key");
            Object.DestroyImmediate(data);
        }

        [Test]
        public void GeodesicLattice_KeepsOnlyUpperStruts()
        {
            var go = new GameObject("LatticeHost");
            go.transform.SetParent(_root.transform);
            HeroBuildingKits.BuildGeodesicLattice(go.transform, "Lattice",
                Vector3.zero, Vector3.one, minUnitY: -0.06f, strutWidth: 0.05f);
            var mf = go.transform.Find("Lattice").GetComponent<MeshFilter>();
            Bounds b = mf.sharedMesh.bounds;
            Assert.That(b.min.y, Is.GreaterThan(-0.25f), "no struts below the drum line");
            Assert.That(b.max.y, Is.GreaterThan(0.95f), "apex hub present under the cupola");
        }
    }
}
