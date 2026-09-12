using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// still5 leftover: unused Commons cardinal showed an orange hull-port ring.
    /// still16 leftover: wrap carbon doors painted the 2×2 as a dark box joint.
    /// Live kits must spawn dock groups off; RefreshTubes enables docked faces only.
    /// CaptureStill / RefreshTubes must not stamp a between-yard tube web.
    /// The hub stays a smaller white paneled square; orange only on docked collars.
    /// </summary>
    public class CampusDressingTests
    {
        private GameObject _root;
        private IsoGrid _grid;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("CampusDressingTestRoot");
            var gridGo = new GameObject("IsoGrid");
            gridGo.transform.SetParent(_root.transform);
            _grid = gridGo.AddComponent<IsoGrid>();
            _grid.Resize(64, 64);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                Object.DestroyImmediate(_root);
        }

        [Test]
        public void DockDressNames_CoverHullDrumPortsAndLegacyStubs()
        {
            Assert.IsTrue(CampusDressing.IsDockDressName("CommonsPort_N"));
            Assert.IsTrue(CampusDressing.IsDockDressName("CommonsPort_E_Ring"));
            Assert.IsTrue(CampusDressing.IsDockDressName("HabPort_S"));
            Assert.IsTrue(CampusDressing.IsDockDressName("LabPort_W"));
            Assert.IsTrue(CampusDressing.IsDockDressName("PwrPort_N"));
            Assert.IsTrue(CampusDressing.IsDockDressName("DockSleeve_E"));
            Assert.IsTrue(CampusDressing.IsDockDressName("Dress_TubeArm_W"));
            Assert.IsTrue(CampusDressing.IsDockDressName("CommonsStub_N"));
            Assert.IsTrue(CampusDressing.IsDockDressName("DrumPort_E"));
            Assert.IsFalse(CampusDressing.IsDockDressName("CommonsDrum"));
            Assert.IsFalse(CampusDressing.IsDockDressName("GuildPort_E"));
            Assert.IsFalse(CampusDressing.IsDockDressName("Dress_AirlockHub"));
        }

        [Test]
        public void LiveCommons_HullPortsStartInactive()
        {
            var commons = ModularBuildingFactory.Spawn(
                BuildingCategory.Commons, Vector3.zero, _root.transform);
            Assert.IsNotNull(FindChild(commons.transform, "CommonsPort_N"));
            Assert.IsFalse(IsRootActive(commons.transform, "CommonsPort_N"));
            Assert.IsFalse(IsRootActive(commons.transform, "CommonsPort_E"));
            Assert.IsFalse(IsRootActive(commons.transform, "CommonsPort_S"));
            Assert.IsFalse(IsRootActive(commons.transform, "CommonsPort_W"));
            Assert.IsFalse(IsRootActive(commons.transform, "DockSleeve_N"));
        }

        [Test]
        public void LiveCommons_GeodesicLattice_NoCyanWaistVisors()
        {
            var commons = ModularBuildingFactory.Spawn(
                BuildingCategory.Commons, Vector3.zero, _root.transform);
            Assert.IsNotNull(FindChild(commons.transform, "Dress_CommonsGeo_0"),
                "dream-loop: geodesic facets must dress the dome");
            Assert.IsNotNull(FindChild(commons.transform, "CommonsStripe"),
                "orange equatorial band");
            Assert.IsNotNull(FindChild(commons.transform, "Dress_CommonsCupolaBand"),
                "orange cupola band");
            Assert.IsNull(FindChild(commons.transform, "CommonsVisor_1"),
                "cyan waist visors washed the sheet white — removed");
            Assert.IsNull(FindChild(commons.transform, "CommonsVisor_3"));
            Color stripe = Albedo(FindChild(commons.transform, "CommonsStripe"));
            Assert.Greater(stripe.r, 0.85f);
            Assert.Less(stripe.g, 0.55f);
        }

        [Test]
        public void LiveHab_ThickCarbonMidBand_AndOrangeRimHatches()
        {
            var hab = ModularBuildingFactory.Spawn(
                BuildingCategory.Habitat, Vector3.zero, _root.transform);
            Transform mid = FindChild(hab.transform, "HabCarbonBand");
            Assert.IsNotNull(mid);
            // Unity cylinder height = 2 * scale.y; band must cover ~24% of HAB length.
            float length = 6f * 0.92f;
            float midLen = mid.localScale.y * 2f;
            Assert.Greater(midLen / length, 0.20f, "mid-band must read thick at ortho 10");
            Assert.Less(midLen / length, 0.32f);
            Assert.Less(Albedo(mid).grayscale, 0.20f, "mid-band stays near-black");
            Assert.IsNotNull(FindChild(hab.transform, "HabCarbonBandCore"),
                "darker core ring so the mid-band reads vs white hull");
            Assert.IsNull(FindChild(hab.transform, "HabMid"),
                "old HabMid name retired — was easy to confuse with orange trim");
            Assert.IsNotNull(FindChild(hab.transform, "HabFrontRim"));
            Assert.IsNotNull(FindChild(hab.transform, "HabRearRim"));
            Color rim = Albedo(FindChild(hab.transform, "HabFrontRim"));
            Assert.Greater(rim.r, 0.85f);
            Assert.Less(rim.g, 0.55f);
        }

        [Test]
        public void SnapToGroundKeepingDockAxis_PreservesSharedDockY()
        {
            var airlock = ModularBuildingFactory.Spawn(
                BuildingCategory.Utility, Vector3.zero, _root.transform);
            Transform tube = FindChild(airlock.transform, "Dress_TubeArm_N_Tube");
            Assert.IsNotNull(tube);
            float before = tube.position.y;
            // Force a ground seat that would otherwise slide docks off DockY.
            airlock.transform.position += new Vector3(0f, 0.35f, 0f);
            ColonyVisualUtility.SnapToGroundKeepingDockAxis(airlock);
            Assert.AreEqual(before, tube.position.y, 0.04f,
                "airlock arms must stay on ColonyVisualUtility.DockY after seating");
            Assert.AreEqual(ColonyVisualUtility.DockY, tube.position.y, 0.08f);
        }

        [Test]
        public void MarsCatalog_SalmonHorizonHaze()
        {
            var mars = CelestialBodyCatalog.Get(CelestialBodyId.Mars);
            // Pale dusty haze, concept far-ground edge ~RGB 222/140/80 (dream-loop round 6).
            Assert.AreEqual(0.98f, mars.FogColor.r, 0.03f);
            Assert.AreEqual(0.61f, mars.FogColor.g, 0.03f);
            Assert.AreEqual(0.40f, mars.FogColor.b, 0.03f);
            // Play-ortho 10: start past campus/focus (~44 m), FogEnd well past the 61 m far
            // edge so the top of frame is a salmon hint (~28 %), not an opaque wall.
            Assert.Greater(mars.FogStart, 30f);
            Assert.Greater(mars.FogEnd, 80f);
            Assert.Less(mars.FogEnd, 160f);
            float playFarHint = (DemoAtmosphere.MarsPlayFarDepth - mars.FogStart)
                / (mars.FogEnd - mars.FogStart);
            Assert.Less(playFarHint, 0.45f);
            Assert.Greater(playFarHint, 0.10f);
            // Dusty brown-orange dirt, not blood-red: concept lit dirt G/R ~0.45.
            Assert.Greater(mars.GroundLight.g / mars.GroundLight.r, 0.55f);
        }

        [Test]
        public void MarsFog_ZoomedOutKeepsFarGroundAHint()
        {
            var mars = CelestialBodyCatalog.Get(CelestialBodyId.Mars);
            float pitch = 30f * Mathf.Deg2Rad;
            float sinP = Mathf.Sin(pitch);
            float cosP = Mathf.Cos(pitch);
            float ortho = IsometricCameraController.MaxOrthoSize;
            float camY = ortho * cosP + 1.5f;
            DemoAtmosphere.ComputeMarsLinearFog(
                mars, camY, -sinP, cosP, ortho, out float start, out float end);
            float depthFar = (camY + cosP * ortho) / sinP;
            float depthFocus = camY / sinP;
            float farHint = Mathf.InverseLerp(start, end, depthFar);
            float focusHint = Mathf.InverseLerp(start, end, depthFocus);
            Assert.Less(farHint, 0.45f, "zoomed-out far ground must stay a haze hint, not FogColor");
            Assert.Greater(farHint, 0.10f, "zoomed-out far ground must still recede");
            Assert.Less(focusHint, 0.08f, "zoomed-out focus must stay near-clear");
            Assert.Greater(end, depthFar + 20f);
        }

        [Test]
        public void FaunaHide_DoesNotUseCampusCream()
        {
            Assert.IsTrue(IndustrialArtDressing.IsFaunaAgentName("RegolithMite"));
            Assert.IsTrue(IndustrialArtDressing.IsFaunaAgentName("WattLeech"));
            Assert.IsTrue(IndustrialArtDressing.IsFaunaAgentName("DustStalker"));
            Assert.IsTrue(IndustrialArtDressing.IsFaunaAgentName("AshHopper"));
            Assert.IsTrue(IndustrialArtDressing.IsFaunaAgentName("DustHopper"));
            Assert.IsTrue(IndustrialArtDressing.IsFaunaAgentName("DustWisp"));
            Assert.IsFalse(IndustrialArtDressing.IsFaunaAgentName("ColonyCommons"));
            Assert.IsFalse(IndustrialArtDressing.IsFaunaAgentName("Visual"));

            GameObject mite = MakeFaunaStub("RegolithMite", new Vector3(0.45f, 0.9f, 0.4f));
            AddAccentToken(mite.transform.Find("Visual"), "SM_Cyan");
            IndustrialArtDressing.Apply(mite);
            var hideMat = mite.transform.Find("Visual").GetComponent<Renderer>().sharedMaterial;
            Assert.IsNotNull(hideMat);
            Assert.IsFalse(hideMat.name.Contains("WhiteHull"), hideMat.name);
            Assert.IsTrue(hideMat.name.Contains("MiteHide"), hideMat.name);
            Transform cyan = FindChild(mite.transform, "SM_CyanToken");
            Assert.IsNotNull(cyan);
            Assert.IsTrue(cyan.GetComponent<Renderer>().sharedMaterial.name.Contains("Cyan"),
                cyan.GetComponent<Renderer>().sharedMaterial.name);

            GameObject hopper = MakeFaunaStub("DustHopper", new Vector3(0.4f, 1.1f, 0.35f));
            IndustrialArtDressing.Apply(hopper);
            string hopperMat = hopper.transform.Find("Visual").GetComponent<Renderer>().sharedMaterial.name;
            Assert.IsTrue(hopperMat.Contains("HopperHide"), hopperMat);
            Assert.IsFalse(hopperMat.Contains("MiteHide"), hopperMat);

            GameObject wisp = MakeFaunaStub("DustWisp", Vector3.one * 0.6f);
            IndustrialArtDressing.Apply(wisp);
            string wispMat = wisp.transform.Find("Visual").GetComponent<Renderer>().sharedMaterial.name;
            Assert.IsTrue(wispMat.Contains("WispHide"), wispMat);
            Assert.IsFalse(wispMat.Contains("MiteHide"), wispMat);
        }

        [Test]
        public void FaunaMotion_HopperHops_LeechHovers_MiteScuttles()
        {
            Assert.AreEqual(LocomotionKind.Hop, UnitMotion.KindFor(FaunaKind.Hopper));
            Assert.AreEqual(LocomotionKind.Hover, UnitMotion.KindFor(FaunaKind.Leech));
            Assert.AreEqual(LocomotionKind.Hover, UnitMotion.KindFor(FaunaKind.Wisp));
            Assert.AreEqual(LocomotionKind.Scuttle, UnitMotion.KindFor(FaunaKind.Mite));
            Assert.AreEqual(LocomotionKind.Scuttle, UnitMotion.KindFor(FaunaKind.Stalker));
            Assert.AreEqual(6, DustStalkerAgent.LegCountFor(FaunaKind.Hopper));
            Assert.AreEqual(6, DustStalkerAgent.LegCountFor(FaunaKind.Mite));
            Assert.AreEqual(6, DustStalkerAgent.LegCountFor(FaunaKind.Stalker));
            Assert.AreEqual(8, DustStalkerAgent.LegCountFor(FaunaKind.Tick));
            Assert.AreEqual(0, DustStalkerAgent.LegCountFor(FaunaKind.Leech));
            Assert.AreEqual(0, DustStalkerAgent.LegCountFor(FaunaKind.Wisp));
        }

        [Test]
        public void FaunaHasArt_KeepsProceduralLegs()
        {
            GameObject mite = MakeFaunaStub("RegolithMite", new Vector3(0.45f, 0.9f, 0.4f));
            InitFauna(mite, FaunaKind.Mite);
            Assert.IsTrue(IndustrialArtDressing.HasArt(mite));
            Assert.IsNotNull(mite.GetComponent<ProceduralLegs>(), "mite IK legs even when HasArt");
            Assert.AreEqual(6, mite.GetComponent<ProceduralLegs>().LegCount);

            GameObject hopper = MakeFaunaStub("AshHopper", new Vector3(0.4f, 1.2f, 0.35f));
            InitFauna(hopper, FaunaKind.Hopper);
            Assert.IsTrue(IndustrialArtDressing.HasArt(hopper));
            Assert.IsNotNull(hopper.GetComponent<ProceduralLegs>());
            Assert.AreEqual(6, hopper.GetComponent<ProceduralLegs>().LegCount);

            GameObject stalker = MakeFaunaStub("DustStalker", new Vector3(0.5f, 1.1f, 0.7f));
            InitFauna(stalker, FaunaKind.Stalker);
            Assert.IsTrue(IndustrialArtDressing.HasArt(stalker));
            Assert.IsNotNull(stalker.GetComponent<ProceduralLegs>());
            Assert.AreEqual(6, stalker.GetComponent<ProceduralLegs>().LegCount);

            GameObject leech = MakeFaunaStub("WattLeech", new Vector3(0.35f, 1.0f, 0.25f));
            InitFauna(leech, FaunaKind.Leech);
            Assert.IsNull(leech.GetComponent<ProceduralLegs>(), "leech hovers — no IK legs");
        }

        [Test]
        public void FaunaMite_PaintAddsCyanOrangeDress()
        {
            GameObject mite = MakeFaunaStub("RegolithMite", new Vector3(0.45f, 0.9f, 0.4f));
            InitFauna(mite, FaunaKind.Mite);
            Transform eye = FindChild(mite.transform, "Dress_EyeL");
            Transform nub = FindChild(mite.transform, "Dress_NubL");
            Assert.IsNotNull(eye, "cyan eye dress");
            Assert.IsNotNull(nub, "orange nub dress");
            Color eyeC = Albedo(eye);
            Assert.Greater(eyeC.b, 0.70f, "eye stays cyan");
            Assert.Greater(eyeC.b, eyeC.r);
            Color nubC = Albedo(nub);
            Assert.Greater(nubC.r, 0.80f, "nub stays safety orange");
            Assert.Less(nubC.b, 0.25f);
        }

        [Test]
        public void FaunaAlignHead_LongestAxisOnPlusZ()
        {
            var go = new GameObject("RegolithMite");
            go.transform.SetParent(_root.transform, false);
            var vis = GameObject.CreatePrimitive(PrimitiveType.Cube);
            vis.name = "Visual";
            vis.transform.SetParent(go.transform, false);
            vis.transform.localRotation = Quaternion.identity;
            vis.transform.localScale = new Vector3(2f, 0.3f, 0.4f);
            var col = vis.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            FaunaDressing.AlignHead(go);

            Bounds b = vis.GetComponent<Renderer>().bounds;
            Assert.Greater(b.size.z, b.size.x * 1.2f,
                "longest horizontal mesh axis must map to parent +Z");
        }

        [Test]
        public void GhostCommons_ShowsAllCardinalPorts()
        {
            var ghost = ModularBuildingFactory.Spawn(
                BuildingCategory.Commons, Vector3.zero, _root.transform, ghost: true);
            Assert.IsTrue(IsRootActive(ghost.transform, "CommonsPort_N"));
            Assert.IsTrue(IsRootActive(ghost.transform, "CommonsPort_E"));
            Assert.IsTrue(IsRootActive(ghost.transform, "DockSleeve_N"));
        }

        [Test]
        public void RefreshTubes_EnablesOnlyDockedCommonsNorth()
        {
            var buildings = new GameObject("Buildings");
            buildings.transform.SetParent(_root.transform);
            Vector3 o = new Vector3(48f, 0f, 48f);
            var commons = ModularBuildingFactory.Spawn(
                BuildingCategory.Commons, o, buildings.transform);
            commons.name = "Bld_ColonyCommons_drop";
            var airlock = ModularBuildingFactory.Spawn(
                BuildingCategory.Utility, o + new Vector3(0f, 0f, 6f), buildings.transform);
            var hab = ModularBuildingFactory.Spawn(
                BuildingCategory.Habitat, o + new Vector3(0f, 0f, 10.5f), buildings.transform);
            hab.name = "Mod_Habitat";

            var placer = new BuildingPlacer(new ResourceManager());
            Vector2Int c0 = OriginFromCenter(o, 6);
            Vector2Int a0 = OriginFromCenter(o + new Vector3(0f, 0f, 6f), 2);
            Vector2Int h0 = OriginFromCenter(o + new Vector3(0f, 0f, 10.5f), 4);
            placer.RegisterPiece(c0, 6, 6, BuildingCategory.Commons);
            placer.RegisterPiece(a0, 2, 2, BuildingCategory.Utility);
            placer.RegisterPiece(h0, 4, 4, BuildingCategory.Habitat);

            CampusDressing.RefreshTubes(placer, _grid, buildings.transform);

            Assert.IsTrue(IsRootActive(commons.transform, "CommonsPort_N"),
                "docked Commons north must show one orange collar");
            Assert.IsFalse(IsRootActive(commons.transform, "CommonsPort_E"),
                "still5 unused east ring must stay off");
            Assert.IsFalse(IsRootActive(commons.transform, "CommonsPort_S"));
            Assert.IsFalse(IsRootActive(commons.transform, "CommonsPort_W"));
            Assert.IsTrue(IsRootActive(airlock.transform, "Dress_TubeArm_N"));
            Assert.IsTrue(IsRootActive(airlock.transform, "Dress_TubeArm_S"));
            Assert.IsFalse(IsRootActive(airlock.transform, "Dress_TubeArm_E"));
            Assert.IsFalse(IsRootActive(airlock.transform, "Dress_TubeArm_W"));
            Assert.IsNotNull(FindChild(airlock.transform, "Dress_AirlockHub"));
            Assert.IsNull(GameObject.Find("CampusTubeRoot"),
                "no fourth CampusTubeRoot stacking collars in the HAB gap");
            Assert.IsNull(GameObject.Find(CampusDressing.TubeRunRootName),
                "RefreshTubes must not stamp a between-yard CampusDress_TubeRuns web");
            AssertAirlockReadsAsWhiteHub(airlock.transform);
            Assert.IsTrue(IsRootActive(hab.transform, "HabPort_S"),
                "HAB south hull port is the module-side orange collar");
            Assert.IsFalse(IsRootActive(hab.transform, "HabPort_N"));
            Assert.IsFalse(IsRootActive(hab.transform, "HabPort_E"));
            Assert.IsFalse(IsRootActive(hab.transform, "HabPort_W"));
            AssertDockedArmIsWhiteTubePlusCollar(airlock.transform, "Dress_TubeArm_S");
            AssertDockedArmIsWhiteTubePlusCollar(airlock.transform, "Dress_TubeArm_N");
        }

        [Test]
        public void RefreshTubes_FindsVillageRingAirlock()
        {
            var buildings = new GameObject("Buildings");
            buildings.transform.SetParent(_root.transform);
            var village = new GameObject("VillageRing");
            village.transform.SetParent(_root.transform);
            Vector3 o = new Vector3(48f, 0f, 48f);
            var commons = ModularBuildingFactory.Spawn(
                BuildingCategory.Commons, o, buildings.transform);
            commons.name = "Bld_ColonyCommons_drop";
            var airlock = ModularBuildingFactory.Spawn(
                BuildingCategory.Utility, o + new Vector3(0f, 0f, 6f), village.transform);
            airlock.name = "VillageAirlock";
            var hab = ModularBuildingFactory.Spawn(
                BuildingCategory.Habitat, o + new Vector3(0f, 0f, 10.5f), village.transform);
            hab.name = "VillageHAB_0";

            var placer = new BuildingPlacer(new ResourceManager());
            Vector2Int c0 = OriginFromCenter(o, 6);
            Vector2Int a0 = OriginFromCenter(o + new Vector3(0f, 0f, 6f), 2);
            Vector2Int h0 = OriginFromCenter(o + new Vector3(0f, 0f, 10.5f), 4);
            placer.RegisterPiece(c0, 6, 6, BuildingCategory.Commons);
            placer.RegisterPiece(a0, 2, 2, BuildingCategory.Utility);
            placer.RegisterPiece(h0, 4, 4, BuildingCategory.Habitat);

            CampusDressing.RefreshTubes(placer, _grid, _root.transform);

            Assert.IsTrue(IsRootActive(airlock.transform, "Dress_TubeArm_N"),
                "VillageRing airlock must get docked north collar");
            Assert.IsTrue(IsRootActive(airlock.transform, "Dress_TubeArm_S"),
                "VillageRing airlock must get docked south collar");
            Assert.IsTrue(IsRootActive(commons.transform, "CommonsPort_N"));
            Assert.IsTrue(IsRootActive(hab.transform, "HabPort_S"));
        }

        [Test]
        public void AirlockHub_IsSmallWhiteSquare_NoWrapDoors()
        {
            var airlock = ModularBuildingFactory.Spawn(
                BuildingCategory.Utility, Vector3.zero, _root.transform);
            AssertAirlockReadsAsWhiteHub(airlock.transform);
            Assert.IsFalse(IsRootActive(airlock.transform, "Dress_TubeArm_N"),
                "live unused north arm must start hidden");
            Assert.IsFalse(IsRootActive(airlock.transform, "Dress_TubeArm_E"));
            Assert.IsFalse(IsRootActive(airlock.transform, "Dress_TubeArm_S"));
            Assert.IsFalse(IsRootActive(airlock.transform, "Dress_TubeArm_W"));
        }

        [Test]
        public void GhostAirlock_ShowsAllArms_EachWithOneOrangeCollar()
        {
            var ghost = ModularBuildingFactory.Spawn(
                BuildingCategory.Utility, Vector3.zero, _root.transform, ghost: true);
            AssertAirlockReadsAsWhiteHub(ghost.transform);
            string[] arms = { "Dress_TubeArm_N", "Dress_TubeArm_E", "Dress_TubeArm_S", "Dress_TubeArm_W" };
            for (int i = 0; i < arms.Length; i++)
            {
                Assert.IsTrue(IsRootActive(ghost.transform, arms[i]), arms[i] + " ghost arm");
                AssertDockedArmIsWhiteTubePlusCollar(ghost.transform, arms[i]);
            }
        }

        [Test]
        public void CardinalExpansion_PlacesHabBeyondAirlock_NotOnCommons()
        {
            var commons = new BuildingPlacer.CampusPiece(
                new Vector2Int(10, 10), 6, 6, BuildingCategory.Commons);
            var faces = new[]
            {
                BuildingPlacer.Cardinal.East,
                BuildingPlacer.Cardinal.West,
                BuildingPlacer.Cardinal.North,
                BuildingPlacer.Cardinal.South
            };

            for (int i = 0; i < faces.Length; i++)
            {
                var face = faces[i];
                BuildingPlacer.CardinalExpansionOrigins(
                    commons, face, 4, 4, out Vector2Int airlock, out Vector2Int hab);

                Assert.IsFalse(
                    RectsOverlap(hab, 4, 4, commons.Origin, 6, 6),
                    $"{face} HAB {hab} must not sit on Commons {commons.Origin}");
                Assert.IsFalse(
                    RectsOverlap(hab, 4, 4, airlock, 2, 2),
                    $"{face} HAB {hab} must not sit on airlock {airlock}");
                Assert.AreEqual(
                    BuildingPlacer.AirlockOriginOnModuleFace(commons, face),
                    airlock);
                Assert.AreEqual(
                    BuildingPlacer.ModuleOriginOnAirlockFace(
                        new BuildingPlacer.CampusPiece(airlock, 2, 2, BuildingCategory.Utility),
                        4, 4, face),
                    hab,
                    $"{face} HAB must continue in the same cardinal as the Commons dock");
            }
        }

        [Test]
        public void OppositeFaceHab_OverlapsCommons_AndFailsCanFit()
        {
            var placer = new BuildingPlacer(new ResourceManager());
            var commonsOrigin = new Vector2Int(10, 10);
            placer.MarkCampusRect(commonsOrigin, 6, 6);
            placer.RegisterPiece(commonsOrigin, 6, 6, BuildingCategory.Commons);
            var commons = placer.Pieces[0];

            var faces = new[]
            {
                BuildingPlacer.Cardinal.East,
                BuildingPlacer.Cardinal.West,
                BuildingPlacer.Cardinal.North,
                BuildingPlacer.Cardinal.South
            };

            int outwardFits = 0;
            for (int i = 0; i < faces.Length; i++)
            {
                var face = faces[i];
                Vector2Int aCell = BuildingPlacer.AirlockOriginOnModuleFace(commons, face);
                Assert.IsTrue(placer.CanFitRect(aCell, 2, 2), $"{face} airlock {aCell} must be free");

                BuildingPlacer.CardinalExpansionOrigins(
                    commons, face, 4, 4, out Vector2Int _, out Vector2Int outwardHab);
                Assert.IsTrue(
                    placer.CanFitRect(outwardHab, 4, 4),
                    $"{face} outward HAB {outwardHab} must CanFit on an empty Commons drop");
                outwardFits++;

                var inward = Opposite(face);
                Vector2Int inwardHab = BuildingPlacer.ModuleOriginOnAirlockFace(
                    new BuildingPlacer.CampusPiece(aCell, 2, 2, BuildingCategory.Utility),
                    4, 4, inward);
                Assert.IsFalse(
                    placer.CanFitRect(inwardHab, 4, 4),
                    $"{face} Opposite HAB {inwardHab} is the still-stamp miss (overlaps Commons)");
                Assert.IsTrue(
                    placer.TryFirstOccupiedCell(inwardHab, 4, 4, out Vector2Int blocked));
                Assert.IsTrue(
                    placer.TryGetPieceAt(blocked, out var piece) &&
                    piece.Category == BuildingCategory.Commons,
                    $"{face} inward miss must report Commons overlap, got {blocked}");
            }

            Assert.AreEqual(4, outwardFits);
        }

        [Test]
        public void EmptyCommonsDrop_EastChainFitsAndDoesNotOverlap()
        {
            var placer = new BuildingPlacer(new ResourceManager());
            var commonsOrigin = new Vector2Int(10, 10);
            placer.MarkCampusRect(commonsOrigin, 6, 6);
            placer.RegisterPiece(commonsOrigin, 6, 6, BuildingCategory.Commons);

            BuildingPlacer.CardinalExpansionOrigins(
                placer.Pieces[0],
                BuildingPlacer.Cardinal.East,
                4, 4,
                out Vector2Int airlock,
                out Vector2Int hab);

            Assert.AreEqual(new Vector2Int(16, 12), airlock);
            Assert.AreEqual(new Vector2Int(18, 11), hab);
            Assert.IsTrue(placer.CanFitRect(airlock, 2, 2));
            Assert.IsTrue(placer.CanFitRect(hab, 4, 4));

            placer.MarkCampusRect(airlock, 2, 2);
            placer.RegisterPiece(airlock, 2, 2, BuildingCategory.Utility);
            placer.MarkCampusRect(hab, 4, 4);
            placer.RegisterPiece(hab, 4, 4, BuildingCategory.Habitat);

            Assert.AreEqual(3, placer.Pieces.Count);
            Assert.IsTrue(placer.HasCommonsModule);
            Assert.IsFalse(placer.CanFitRect(airlock, 2, 2));
            Assert.IsFalse(placer.CanFitRect(hab, 4, 4));
        }

        [Test]
        public void DensePack_EastHab_PlacesPadPowerWaterRegolith_WithoutOverlap()
        {
            var placer = StampEastChain(out var commons);

            Vector2Int westPad = StillCampusDensity.FlushOrigin(
                commons, BuildingPlacer.Cardinal.West, 6, 6);
            Assert.AreEqual(new Vector2Int(4, 10), westPad);
            Assert.IsTrue(placer.CanFitRect(westPad, 6, 6), "west pad must CanFit beside east HAB");

            var plan = StillCampusDensity.Plan(placer, commons, BuildingPlacer.Cardinal.East);

            Assert.IsTrue(plan.Pad, "pad");
            Assert.IsTrue(plan.Power, "pwr");
            Assert.IsTrue(plan.Water, "water");
            Assert.IsTrue(plan.Regolith, "regolith");
            Assert.AreEqual(4, plan.PlacedCount);
            Assert.AreEqual(westPad, plan.PadOrigin, "HAB east → pad west (E/W/N pack)");
            Assert.AreEqual(
                StillCampusDensity.FlushOrigin(commons, BuildingPlacer.Cardinal.North, 4, 4),
                plan.PowerOrigin,
                "PWR-1 north of Commons");
            Assert.AreEqual(new Vector2Int(16, 16), plan.WaterOrigin, "water NE corner");
            Assert.AreEqual(new Vector2Int(6, 16), plan.RegolithOrigin, "regolith NW corner");

            var log = StillCampusDensity.StampLog.FromPieces(placer);
            Assert.IsTrue(log.Commons && log.Airlock && log.Hab);
            Assert.IsTrue(log.Pad && log.Power && log.Water && log.Regolith);
            Assert.AreEqual(
                "commons=True airlock=True hab=True pad=True pwr=True water=True regolith=True " +
                "workshop=False inn=False wonder=False leftover=none extraHab=False extraSolar=False defense=False",
                log.ToString());

            Assert.IsFalse(
                RectsOverlap(plan.PadOrigin, 6, 6, commons.Origin, 6, 6));
            Assert.IsFalse(
                RectsOverlap(plan.PowerOrigin, 4, 4, plan.PadOrigin, 6, 6));
            Assert.IsFalse(
                RectsOverlap(plan.WaterOrigin, 4, 4, plan.PowerOrigin, 4, 4));
            Assert.IsFalse(
                RectsOverlap(plan.RegolithOrigin, 4, 4, plan.WaterOrigin, 4, 4));
            Assert.IsFalse(
                RectsOverlap(plan.WaterOrigin, 4, 4, new Vector2Int(18, 11), 4, 4),
                "water must not sit on east HAB");
            Assert.AreEqual(7, placer.Pieces.Count);
        }

        [Test]
        public void DensePack_ForwardYards_CanFitNearCommons_ButExtraRuleWouldReject()
        {
            var placer = StampEastChain(out var commons);
            Vector2Int westPad = StillCampusDensity.FlushOrigin(
                commons, BuildingPlacer.Cardinal.West, 6, 6);

            Assert.IsTrue(placer.CanFitRect(westPad, 6, 6));
            Assert.IsFalse(
                placer.IsValidModuleDock(westPad, 6, 6),
                "pad is a forward yard, not an airlock dock");
            Assert.IsFalse(
                placer.OverlapsOutpostClaim(westPad, 6, 6),
                "Campus B claim is ~30 m off — still pack must not require it");
        }

        [Test]
        public void DensePack_SkipsHabFace_ThenTakesNorthWhenWestBlocked()
        {
            var placer = StampEastChain(out var commons);

            Vector2Int westPad = StillCampusDensity.FlushOrigin(
                commons, BuildingPlacer.Cardinal.West, 6, 6);
            placer.MarkCampusRect(westPad, 6, 6);
            placer.RegisterPiece(westPad, 6, 6, BuildingCategory.Defense);

            Assert.IsTrue(StillCampusDensity.TryNext(
                placer, commons, BuildingPlacer.Cardinal.East, 6, 6, null, out Vector2Int picked));
            Assert.AreNotEqual(westPad, picked);
            Assert.AreEqual(
                StillCampusDensity.FlushOrigin(commons, BuildingPlacer.Cardinal.North, 6, 6),
                picked,
                "west blocked → north pad (South last)");
        }

        [Test]
        public void DensePack_StillFrame_UsesPlayCampusOrtho10()
        {
            var placer = StampEastChain(out var commons);
            StillCampusDensity.Plan(placer, commons, BuildingPlacer.Cardinal.East);

            Assert.IsTrue(StillCampusDensity.TryCampusAabb(placer, out var min, out var max));
            float ortho = StillCampusDensity.FitStillOrtho(
                placer, ColonyLayout.DefaultCellSize, StillCampusDensity.GameTabAspect);
            Assert.AreEqual(
                ColonyLayout.CampusOrthoSize, ortho,
                "spaced overseer still uses play ortho 10 — do not zoom-to-pack AABB");
            Assert.AreEqual(StillCampusDensity.PlayCampusOrthoSize, ortho);
            Assert.Greater(max.x - min.x, 10, "AABB must span pad→HAB");
            Assert.Greater(max.y - min.y, 6, "AABB must span Commons→north yards");
        }

        [Test]
        public void DensePack_LeftoverKits_DoNotStampWhenTheyCanFit()
        {
            var placer = StampEastChain(out var commons);
            StillCampusDensity.Plan(placer, commons, BuildingPlacer.Cardinal.East);

            Assert.IsTrue(StillCampusDensity.TryNext(
                placer, commons, BuildingPlacer.Cardinal.East, 4, 4, null, out _),
                "workshop 4×4 still CanFit after pad+yards");
            Assert.IsTrue(StillCampusDensity.TryNext(
                placer, commons, BuildingPlacer.Cardinal.East, 6, 6, null, out _),
                "a 6×6 wonder still CanFit near Commons");

            var leftovers = StillCampusDensity.PlanLeftovers(
                placer, commons, BuildingPlacer.Cardinal.East);
            Assert.IsFalse(leftovers.Inn, "Aaron 2026-09-07: leftover Inn is not a still density gate");
            Assert.IsFalse(leftovers.Wonder, "Aaron 2026-09-07: leftover wonder is not a still density gate");
            Assert.IsFalse(leftovers.Workshop, "Aaron 2026-09-07: leftover hangar is not a still density gate");
            Assert.AreEqual("spaced", leftovers.SkipReason);
            Assert.AreEqual(7, placer.Pieces.Count, "PlanLeftovers must not occupy leftover footprints");

            var log = StillCampusDensity.StampLog.FromPieces(placer, leftovers.SkipReason);
            Assert.IsFalse(log.Workshop);
            Assert.IsFalse(log.Inn);
            Assert.IsFalse(log.Wonder);
            Assert.AreEqual("spaced", log.Leftover);

            Assert.IsTrue(StillCampusDensity.TryCampusAabb(placer, out _, out _));
            float ortho = StillCampusDensity.FitStillOrtho(
                placer, ColonyLayout.DefaultCellSize, StillCampusDensity.GameTabAspect);
            Assert.AreEqual(StillCampusDensity.PlayCampusOrthoSize, ortho);
            Assert.GreaterOrEqual(ortho, StillCampusDensity.StillMinOrtho);
        }

        [Test]
        public void LandmarkGap_IsNotFlushToCommons()
        {
            var placer = StampEastChain(out var commons);
            Assert.IsTrue(StillCampusDensity.TryNext(
                placer, commons, BuildingPlacer.Cardinal.East, 6, 6, null,
                out Vector2Int spaced, StillCampusDensity.LandmarkGapCells));
            Vector2Int flush = StillCampusDensity.FlushOrigin(
                commons, BuildingPlacer.Cardinal.West, 6, 6, 0);
            Assert.AreNotEqual(flush, spaced, "pad must sit off the Commons apron");
            Vector2Int gapped = StillCampusDensity.FlushOrigin(
                commons, BuildingPlacer.Cardinal.West, 6, 6, StillCampusDensity.LandmarkGapCells);
            Assert.AreEqual(gapped, spaced);
        }

        [Test]
        public void DensePack_Leftovers_AfterWorkshop_SkipInnAndWonder()
        {
            var placer = StampEastChain(out var commons);
            StillCampusDensity.Plan(placer, commons, BuildingPlacer.Cardinal.East);
            Assert.IsTrue(StillCampusDensity.TryDockOrNext(
                placer, commons, BuildingPlacer.Cardinal.East, 4, 4, null, out Vector2Int shop));
            placer.MarkCampusRect(shop, 4, 4);
            placer.RegisterPiece(shop, 4, 4, BuildingCategory.EngineerWorkshop);

            var leftovers = StillCampusDensity.PlanLeftovers(
                placer, commons, BuildingPlacer.Cardinal.East);
            Assert.IsTrue(leftovers.Workshop, "existing hangar stays on the leftover label");
            Assert.IsFalse(leftovers.Inn, "Aaron 2026-09-07: do not pack leftover Inn");
            Assert.IsFalse(leftovers.Wonder, "Aaron 2026-09-07: do not pack leftover wonder");
            Assert.AreEqual("spaced+workshop", leftovers.SkipReason);
        }

        [Test]
        public void DensePack_Still20Order_SkipsLeftoverDensityPressure()
        {
            var placer = StampStill20Order(out _, out var leftovers, out var cues);

            Assert.IsFalse(leftovers.Inn, "Aaron 2026-09-07: leftover Inn is not a still density gate");
            Assert.IsFalse(leftovers.Wonder, "Aaron 2026-09-07: leftover wonder is not a still density gate");
            Assert.IsTrue(leftovers.SkipReason.StartsWith("spaced"), leftovers.SkipReason);
            Assert.IsFalse(cues.ExtraSolar, "do not pack a second solar bank on the still");
            Assert.IsFalse(cues.Defense, "do not pack Defense Battery leftover on the still");
            Assert.AreEqual(0, cues.HabSocketCount);
            Assert.AreEqual(1, StillCampusDensity.CountCategory(placer, BuildingCategory.Power));
            Assert.AreEqual(0, StillCampusDensity.CountCategory(placer, BuildingCategory.Defense));

            var log = StillCampusDensity.StampLog.FromPieces(placer, leftovers.SkipReason);
            Assert.IsFalse(log.Inn);
            Assert.IsFalse(log.Wonder);
            Assert.IsFalse(log.ExtraSolar);
            Assert.IsFalse(log.Defense);

            Assert.IsTrue(StillCampusDensity.TryCampusAabb(placer, out _, out _));
            float ortho = StillCampusDensity.FitStillOrtho(
                placer, ColonyLayout.DefaultCellSize, StillCampusDensity.GameTabAspect);
            Assert.AreEqual(StillCampusDensity.PlayCampusOrthoSize, ortho);
            Assert.GreaterOrEqual(ortho, StillCampusDensity.StillMinOrtho);
        }

        [Test]
        public void DensePack_WorkshopPrefersAirlockDock_BeforeYardsFillIt()
        {
            var placer = StampEastChain(out var commons);
            Assert.IsTrue(StillCampusDensity.TryDockOnAirlock(
                placer, commons, 4, 4, null, out Vector2Int dock));
            Assert.AreEqual(new Vector2Int(15, 14), dock,
                "east airlock north face is free before pad/water occupy it");

            Assert.IsTrue(StillCampusDensity.TryDockOrNext(
                placer, commons, BuildingPlacer.Cardinal.East, 4, 4, null, out Vector2Int picked));
            Assert.AreEqual(dock, picked);
        }

        [Test]
        public void DensePack_ExtraHabChain_CanFitSouth_ButPlanCuesSkips()
        {
            var placer = StampEastChain(out var commons);
            Assert.IsTrue(StillCampusDensity.TryExtraHabChain(
                placer, commons, BuildingPlacer.Cardinal.East, null,
                out Vector2Int airlock, out Vector2Int hab));
            Assert.AreEqual(
                BuildingPlacer.AirlockOriginOnModuleFace(commons, BuildingPlacer.Cardinal.South),
                airlock);
            Assert.IsFalse(RectsOverlap(hab, 4, 4, commons.Origin, 6, 6));
            Assert.IsFalse(RectsOverlap(hab, 4, 4, airlock, 2, 2));
            Assert.IsTrue(placer.CanFitRect(airlock, 2, 2));
            Assert.IsTrue(placer.CanFitRect(hab, 4, 4));

            var cues = StillCampusDensity.PlanCues(
                placer, commons, BuildingPlacer.Cardinal.East);
            Assert.IsFalse(cues.ExtraHab, "Aaron 2026-09-07: extra HAB is leftover density, not a still gate");
            Assert.IsFalse(cues.ExtraAirlock);
            Assert.AreEqual(0, cues.PlacedCount);
        }

        [Test]
        public void StillHabSocket_SpawnsFoundationAndCrane()
        {
            var go = CampusDressing.SpawnStillHabSocket(Vector3.zero, _root.transform);
            Assert.IsNotNull(go);
            Assert.AreEqual("Site_StillHabSocket", go.name);
            Assert.IsNotNull(FindChild(go.transform, "SocketDisc"));
            Assert.IsNotNull(FindChild(go.transform, "SocketCraneMast"));
            Assert.IsNotNull(FindChild(go.transform, "SocketClad_0"));
            Assert.Greater(Albedo(FindChild(go.transform, "SocketDisc")).grayscale, 0.88f);
        }

        [Test]
        public void DensePack_InteriorDirt_MayStayEmpty_StillUsesPlayOrtho()
        {
            var placer = StampEastChain(out var commons);
            Vector2Int pad = StillCampusDensity.FlushOrigin(
                commons, BuildingPlacer.Cardinal.West, 6, 6);
            placer.MarkCampusRect(pad, 6, 6);
            placer.RegisterPiece(pad, 6, 6, BuildingCategory.LandingPad);
            var extractor = new Vector2Int(pad.x, pad.y + 6 + 4);
            placer.MarkCampusRect(extractor, 4, 4);
            placer.RegisterPiece(extractor, 4, 4, BuildingCategory.Farm);

            var cues = StillCampusDensity.PlanCues(placer, commons, BuildingPlacer.Cardinal.East);
            Assert.AreEqual(0, cues.HabSocketCount, "PlanCues must not fill interior dirt");
            Assert.IsFalse(cues.HabSocket);
            Assert.AreEqual(
                StillCampusDensity.PlayCampusOrthoSize,
                StillCampusDensity.FitStillOrtho(
                    placer, ColonyLayout.DefaultCellSize, StillCampusDensity.GameTabAspect));
        }

        [Test]
        public void DensePack_Still21Order_SkipsLeftoversWithoutInteriorFill()
        {
            var placer = StampStill20Order(out _, out var leftovers, out var cues);
            Assert.IsFalse(leftovers.Inn, "do not pack leftover Inn");
            Assert.IsFalse(leftovers.Wonder, "do not pack leftover wonder");
            Assert.IsTrue(leftovers.SkipReason.StartsWith("spaced"), leftovers.SkipReason);
            Assert.AreEqual(0, cues.HabSocketCount, "do not force-fill interior sockets");
            Assert.AreEqual(
                StillCampusDensity.PlayCampusOrthoSize,
                StillCampusDensity.FitStillOrtho(
                    placer, ColonyLayout.DefaultCellSize, StillCampusDensity.GameTabAspect));
            Assert.AreEqual(10f, StillCampusDensity.PlayCampusOrthoSize);
        }

        [Test]
        public void RefreshTubes_SpacedCampus_EnablesDockedArmsWithoutTubeRuns()
        {
            var buildings = new GameObject("Buildings");
            buildings.transform.SetParent(_root.transform);
            Vector3 o = new Vector3(48f, 0f, 48f);
            var commons = ModularBuildingFactory.Spawn(
                BuildingCategory.Commons, o, buildings.transform);
            commons.name = "Bld_ColonyCommons_drop";
            var airlock = ModularBuildingFactory.Spawn(
                BuildingCategory.Utility, o + new Vector3(0f, 0f, 6f), buildings.transform);
            var hab = ModularBuildingFactory.Spawn(
                BuildingCategory.Habitat, o + new Vector3(0f, 0f, 10.5f), buildings.transform);
            hab.name = "Mod_Habitat";

            var placer = new BuildingPlacer(new ResourceManager());
            Vector2Int c0 = OriginFromCenter(o, 6);
            Vector2Int a0 = OriginFromCenter(o + new Vector3(0f, 0f, 6f), 2);
            Vector2Int h0 = OriginFromCenter(o + new Vector3(0f, 0f, 10.5f), 4);
            placer.RegisterPiece(c0, 6, 6, BuildingCategory.Commons);
            placer.RegisterPiece(a0, 2, 2, BuildingCategory.Utility);
            placer.RegisterPiece(h0, 4, 4, BuildingCategory.Habitat);

            var commonsPiece = placer.Pieces[0];
            Vector2Int pad = StillCampusDensity.FlushOrigin(
                commonsPiece, BuildingPlacer.Cardinal.West, 6, 6);
            placer.MarkCampusRect(pad, 6, 6);
            placer.RegisterPiece(pad, 6, 6, BuildingCategory.LandingPad);
            Vector3 padWorld = FootprintCenter(pad, 6);
            var padGo = ModularBuildingFactory.Spawn(
                BuildingCategory.LandingPad, padWorld, buildings.transform);
            padGo.name = "Bld_LandingPad_still";

            new GameObject(CampusDressing.TubeRunRootName).transform.SetParent(buildings.transform);

            // West pad is a cardinal neighbor that still21 used to web with a tube run.
            Assert.IsTrue(StillCampusDensity.TryCardinalNeighbor(
                placer.Pieces[0], placer.Pieces[placer.Pieces.Count - 1],
                8, out _, out bool eastWest));
            Assert.IsTrue(eastWest, "west pad is an E/W neighbor of Commons");
            Assert.IsFalse(StillCampusDensity.AreAirlockLinked(
                placer, placer.Pieces[0], placer.Pieces[placer.Pieces.Count - 1]));

            CampusDressing.RefreshTubes(placer, _grid, buildings.transform);

            Assert.IsTrue(IsRootActive(commons.transform, "CommonsPort_N"),
                "docked Commons north must stay enabled on a still campus");
            Assert.IsTrue(IsRootActive(airlock.transform, "Dress_TubeArm_N"));
            Assert.IsTrue(IsRootActive(airlock.transform, "Dress_TubeArm_S"));
            Assert.IsFalse(IsRootActive(commons.transform, "CommonsPort_E"),
                "unused Commons east ring stays off");
            Assert.IsNull(GameObject.Find("CampusTubeRoot"),
                "must not revive CampusTubeRoot in the HAB gap");
            Assert.IsNull(GameObject.Find(CampusDressing.TubeRunRootName),
                "RefreshTubes must not stamp a pressurized tube web between yards");
        }

        private Vector3 FootprintCenter(Vector2Int origin, int side)
        {
            Vector3 a = _grid.CellToWorld(origin);
            Vector3 b = _grid.CellToWorld(origin + new Vector2Int(side - 1, side - 1));
            return (a + b) * 0.5f;
        }

        [Test]
        public void InferHabFace_ReadsEastChain()
        {
            var placer = StampEastChain(out var commons);
            Assert.AreEqual(
                BuildingPlacer.Cardinal.East,
                StillCampusDensity.InferHabFace(placer, commons));
            Assert.AreEqual(
                BuildingPlacer.Cardinal.West,
                StillCampusDensity.Opposite(BuildingPlacer.Cardinal.East));
        }

        [Test]
        public void StillCampusDensity_GridConstants_MatchColonyLayout()
        {
            Assert.AreEqual(ColonyLayout.DefaultCellSize, StillCampusDensity.DefaultCellSize);
            Assert.AreEqual(ColonyLayout.CampusOrthoSize, StillCampusDensity.PlayCampusOrthoSize);
            Assert.AreEqual(1.5f, StillCampusDensity.DefaultCellSize);
            Assert.AreEqual(10f, StillCampusDensity.PlayCampusOrthoSize);
            Assert.AreEqual(0, StillCampusDensity.StillFrameInsetCells);
            Assert.AreEqual(6, StillCampusDensity.MaxInteriorHabSockets);
            Assert.AreEqual(
                StillCampusDensity.PlayCampusOrthoSize, StillCampusDensity.StillMinOrtho,
                "FitStillOrtho floor must not crop inside play ortho");
        }

        [Test]
        public void FitStillOrtho_EmptyPlacer_FallsBackToPlayCampusOrtho()
        {
            Assert.AreEqual(
                StillCampusDensity.PlayCampusOrthoSize,
                StillCampusDensity.FitStillOrtho(
                    null, StillCampusDensity.DefaultCellSize, StillCampusDensity.GameTabAspect));
        }

        private static BuildingPlacer StampEastChain(out BuildingPlacer.CampusPiece commons)
        {
            var placer = new BuildingPlacer(new ResourceManager());
            var commonsOrigin = new Vector2Int(10, 10);
            placer.MarkCampusRect(commonsOrigin, 6, 6);
            placer.RegisterPiece(commonsOrigin, 6, 6, BuildingCategory.Commons);
            commons = placer.Pieces[0];

            BuildingPlacer.CardinalExpansionOrigins(
                commons,
                BuildingPlacer.Cardinal.East,
                4, 4,
                out Vector2Int airlock,
                out Vector2Int hab);
            placer.MarkCampusRect(airlock, 2, 2);
            placer.RegisterPiece(airlock, 2, 2, BuildingCategory.Utility);
            placer.MarkCampusRect(hab, 4, 4);
            placer.RegisterPiece(hab, 4, 4, BuildingCategory.Habitat);
            return placer;
        }

        /// <summary>
        /// still20 shutter order: extra HAB + workshop, then pad/yards, leftovers, cues.
        /// </summary>
        private static BuildingPlacer StampStill20Order(
            out BuildingPlacer.CampusPiece commons,
            out StillCampusDensity.LeftoverPlan leftovers,
            out StillCampusDensity.CuePlan cues)
        {
            var placer = StampEastChain(out commons);
            Assert.IsTrue(StillCampusDensity.TryExtraHabChain(
                placer, commons, BuildingPlacer.Cardinal.East, null,
                out Vector2Int extraAirlock, out Vector2Int extraHab));
            placer.MarkCampusRect(extraAirlock, 2, 2);
            placer.RegisterPiece(extraAirlock, 2, 2, BuildingCategory.Utility);
            placer.MarkCampusRect(extraHab, 4, 4);
            placer.RegisterPiece(extraHab, 4, 4, BuildingCategory.Habitat);

            if (StillCampusDensity.TryDockOnAirlock(
                    placer, commons, 4, 4, null, out Vector2Int shop))
            {
                placer.MarkCampusRect(shop, 4, 4);
                placer.RegisterPiece(shop, 4, 4, BuildingCategory.EngineerWorkshop);
            }

            StillCampusDensity.Plan(placer, commons, BuildingPlacer.Cardinal.East);
            leftovers = StillCampusDensity.PlanLeftovers(
                placer, commons, BuildingPlacer.Cardinal.East);
            cues = StillCampusDensity.PlanCues(placer, commons, BuildingPlacer.Cardinal.East);
            return placer;
        }

        private static BuildingPlacer.Cardinal Opposite(BuildingPlacer.Cardinal face)
        {
            switch (face)
            {
                case BuildingPlacer.Cardinal.East: return BuildingPlacer.Cardinal.West;
                case BuildingPlacer.Cardinal.West: return BuildingPlacer.Cardinal.East;
                case BuildingPlacer.Cardinal.North: return BuildingPlacer.Cardinal.South;
                default: return BuildingPlacer.Cardinal.North;
            }
        }

        private static bool RectsOverlap(
            Vector2Int a, int aw, int ah, Vector2Int b, int bw, int bh)
        {
            return a.x < b.x + bw && a.x + aw > b.x && a.y < b.y + bh && a.y + ah > b.y;
        }

        private Vector2Int OriginFromCenter(Vector3 center, int side)
        {
            float cs = _grid.CellSize;
            int span = Mathf.Max(1, side);
            Vector3 corner = center - new Vector3((span - 1) * 0.5f * cs, 0f, (span - 1) * 0.5f * cs);
            return _grid.WorldToCell(corner);
        }

        private static Transform FindChild(Transform root, string name)
        {
            var ts = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < ts.Length; i++)
            {
                if (ts[i] != null && ts[i].name == name)
                    return ts[i];
            }
            return null;
        }

        private GameObject MakeFaunaStub(string name, Vector3 visualScale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            var vis = GameObject.CreatePrimitive(PrimitiveType.Cube);
            vis.name = "Visual";
            vis.transform.SetParent(go.transform, false);
            vis.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            vis.transform.localScale = visualScale;
            var col = vis.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader) { name = "SM_White" };
            vis.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        private static void AddAccentToken(Transform visual, string token)
        {
            var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eye.name = token + "Token";
            eye.transform.SetParent(visual, false);
            eye.transform.localScale = Vector3.one * 0.15f;
            var col = eye.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader) { name = token };
            eye.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static void InitFauna(GameObject go, FaunaKind kind)
        {
            IndustrialArtDressing.Apply(go);
            var agent = go.AddComponent<DustStalkerAgent>();
            agent.Initialize(null, null, Vector3.zero);
            agent.SetKind(kind);
        }

        private static bool IsRootActive(Transform root, string name)
        {
            Transform t = FindChild(root, name);
            return t != null && t.gameObject.activeSelf;
        }

        /// <summary>
        /// still16 fail: wrap carbon doors + carbon roof made the 2×2 a dark box.
        /// Hub must stay a cell-safe white square; unused faces stay clean plates.
        /// </summary>
        private static void AssertAirlockReadsAsWhiteHub(Transform airlock)
        {
            Transform hub = FindChild(airlock, "Dress_AirlockHub");
            Assert.IsNotNull(hub, "Dress_AirlockHub");
            Vector3 scale = hub.localScale;
            Assert.AreEqual(ColonyVisualUtility.AirlockHubSide, scale.x, 0.02f);
            Assert.AreEqual(ColonyVisualUtility.AirlockHubSide, scale.z, 0.02f);
            Assert.Less(scale.x, ColonyLayout.DefaultCellSize * 2f,
                "hub must stay smaller than the 2×2 cell so the white tube reads");
            Assert.Greater(Albedo(hub).grayscale, 0.88f, "hub hull must be sheet-white");
            Assert.IsNotNull(FindChild(airlock, "Dress_HubPanel_0"));
            Assert.Greater(Albedo(FindChild(airlock, "Dress_HubPanel_0")).grayscale, 0.88f);
            Assert.IsNotNull(FindChild(airlock, "Dress_HubSeamH_0"),
                "carbon panel seams must stay — still18 cube-ish miss");
            Assert.Less(Albedo(FindChild(airlock, "Dress_HubSeamH_0")).grayscale, 0.35f);
            Assert.Greater(Albedo(FindChild(airlock, "Dress_HubRoof")).grayscale, 0.88f,
                "carbon roof was the still16 dark lid — roof stays white");
            Assert.IsNull(FindChild(airlock, "Dress_HubDoor_0"),
                "wrap doors painted the hub as a dark box");
            Assert.IsNull(FindChild(airlock, "Dress_HubDoor_1"));
            Assert.IsNotNull(FindChild(airlock, "Dress_HubInset_0"),
                "small inset hatch is panel language, not a wrap door");
            Assert.Less(FindChild(airlock, "Dress_HubInset_0").localScale.x
                        + FindChild(airlock, "Dress_HubInset_0").localScale.z, 0.70f,
                "inset hatch must stay much smaller than a wrap door");
        }

        private static void AssertDockedArmIsWhiteTubePlusCollar(Transform airlock, string arm)
        {
            Transform group = FindChild(airlock, arm);
            Assert.IsNotNull(group, arm);
            Transform tube = FindChild(group, arm + "_Tube");
            Transform collar = FindChild(group, arm + "_Collar");
            Transform lip = FindChild(group, arm + "_Lip");
            Assert.IsNotNull(tube, arm + " white tube");
            Assert.IsNotNull(collar, arm + " orange collar");
            Assert.IsNotNull(lip, arm + " white hub lip");
            Transform hubCollar = FindChild(group, arm + "_HubCollar");
            Assert.IsNotNull(hubCollar, arm + " hub-face collar (still18 cube-ish miss)");
            Transform faceFrame = FindChild(group, arm + "_FaceFrame");
            Assert.IsNotNull(faceFrame, arm + " square orange face frame (still19 cube-ish miss)");
            Assert.Greater(Albedo(tube).grayscale, 0.88f, arm + " tube must be white");
            Assert.Greater(Albedo(lip).grayscale, 0.88f, arm + " lip must be white");
            Color collarC = Albedo(collar);
            Assert.Greater(collarC.r, 0.85f, arm + " collar stays safety orange");
            Assert.Less(collarC.g, 0.55f);
            Assert.Less(collarC.b, 0.25f);
            Color hubC = Albedo(hubCollar);
            Assert.Greater(hubC.r, 0.85f, arm + " hub collar stays safety orange");
            Assert.Less(hubC.g, 0.55f);
            Color frameC = Albedo(faceFrame);
            Assert.Greater(frameC.r, 0.85f, arm + " face frame stays safety orange");
            Assert.Less(frameC.g, 0.55f);
            float tubeLen = tube.localScale.y * 2f;
            Assert.Greater(tubeLen, 0.39f, arm + " stub must read as a short white tube");
            float face = ColonyLayout.DefaultCellSize;
            Vector3 collarFlat = collar.localPosition;
            collarFlat.y = 0f;
            Assert.Greater(collarFlat.magnitude, face * 0.85f,
                arm + " orange collar sits at the Lego face, not as a hub-gasket dark ring");
            Vector3 hubFlat = hubCollar.localPosition;
            hubFlat.y = 0f;
            Assert.Less(hubFlat.magnitude, ColonyVisualUtility.AirlockHubSide * 0.72f,
                arm + " hub collar sits on the white square, readable at Game-tab distance");
            Assert.Greater(collar.localScale.y, 0.06f, arm + " Lego-face collar must be thicker than a sliver");
            Assert.Greater(hubCollar.localScale.x, ColonyVisualUtility.DockBore * 1.25f,
                arm + " hub collar must read wider than the tube");
        }

        private static Color Albedo(Transform t)
        {
            Assert.IsNotNull(t);
            var rend = t.GetComponent<Renderer>();
            Assert.IsNotNull(rend, t.name + " renderer");
            var mat = rend.sharedMaterial;
            Assert.IsNotNull(mat, t.name + " material");
            if (mat.HasProperty("_BaseColor"))
                return mat.GetColor("_BaseColor");
            return mat.color;
        }
    }
}
