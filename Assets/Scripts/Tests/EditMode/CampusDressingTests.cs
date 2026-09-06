using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// still5 leftover: unused Commons cardinal showed an orange hull-port ring.
    /// still16 leftover: wrap carbon doors painted the 2×2 as a dark box joint.
    /// Live kits must spawn dock groups off; RefreshTubes enables docked faces only.
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
        public void DensePack_StillFrame_IsTighterThanCampusOrtho10()
        {
            var placer = StampEastChain(out var commons);
            StillCampusDensity.Plan(placer, commons, BuildingPlacer.Cardinal.East);

            Assert.IsTrue(StillCampusDensity.TryCampusAabb(placer, out var min, out var max));
            float ortho = StillCampusDensity.FitStillOrtho(
                min, max, ColonyLayout.DefaultCellSize, StillCampusDensity.GameTabAspect);
            Assert.Less(ortho, ColonyLayout.CampusOrthoSize,
                "still18 dirt was play ortho 10 on a short-wide Game tab");
            Assert.GreaterOrEqual(ortho, StillCampusDensity.StillMinOrtho);
            Assert.LessOrEqual(ortho, StillCampusDensity.StillMaxOrtho);
            Assert.AreEqual(8.41f, ortho, 0.2f,
                "east-HAB dense pack should snap ~8.4, not play 10");
            Assert.Greater(max.x - min.x, 10, "AABB must span pad→HAB");
            Assert.Greater(max.y - min.y, 6, "AABB must span Commons→north yards");
        }

        [Test]
        public void DensePack_LeftoverKits_StampWhenTheyCanFit()
        {
            var placer = StampEastChain(out var commons);
            StillCampusDensity.Plan(placer, commons, BuildingPlacer.Cardinal.East);

            Assert.IsTrue(StillCampusDensity.TryNext(
                placer, commons, BuildingPlacer.Cardinal.East, 4, 4, null, out Vector2Int shop),
                "workshop 4×4 still CanFit after pad+yards");
            Assert.IsTrue(StillCampusDensity.TryNext(
                placer, commons, BuildingPlacer.Cardinal.East, 6, 6, null, out Vector2Int wonder),
                "a 6×6 wonder still CanFit near Commons");

            var leftovers = StillCampusDensity.PlanLeftovers(
                placer, commons, BuildingPlacer.Cardinal.East);
            Assert.IsTrue(leftovers.Workshop, "still19 skip-frame must not drop a CanFit workshop");
            Assert.IsTrue(leftovers.Inn, "still19 skip-frame must not drop a CanFit inn");
            Assert.IsTrue(leftovers.Wonder, "still19 skip-frame must not drop a CanFit wonder");
            Assert.AreEqual("workshop+inn+wonder", leftovers.SkipReason);
            Assert.AreNotEqual("skip-frame", leftovers.SkipReason);
            Assert.Greater(placer.Pieces.Count, 7, "PlanLeftovers occupies leftover footprints");
            Assert.IsFalse(
                RectsOverlap(leftovers.WorkshopOrigin, 4, 4, leftovers.InnOrigin, 4, 4));
            Assert.IsFalse(
                RectsOverlap(leftovers.WorkshopOrigin, 4, 4, leftovers.WonderOrigin, 6, 6));
            Assert.IsFalse(
                RectsOverlap(leftovers.InnOrigin, 4, 4, leftovers.WonderOrigin, 6, 6));

            var log = StillCampusDensity.StampLog.FromPieces(placer, leftovers.SkipReason);
            Assert.IsTrue(log.Workshop);
            Assert.IsTrue(log.Inn);
            Assert.IsTrue(log.Wonder);
            Assert.AreEqual("workshop+inn+wonder", log.Leftover);

            Assert.IsTrue(StillCampusDensity.TryCampusAabb(placer, out var min, out var max));
            float ortho = StillCampusDensity.FitStillOrtho(
                min, max, ColonyLayout.DefaultCellSize, StillCampusDensity.GameTabAspect);
            Assert.LessOrEqual(ortho, StillCampusDensity.StillMaxOrtho);
            Assert.LessOrEqual(ortho, StillCampusDensity.PlayCampusOrthoSize);
            Assert.Greater(ortho, 8.2f, "leftover pack fills more of the still frame than pad-only");
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
        public void DensePack_ExtraHabChain_FillsSouthDirt()
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
            Assert.IsTrue(cues.ExtraHab);
            Assert.IsTrue(cues.ExtraAirlock);
            Assert.AreEqual(airlock, cues.ExtraAirlockOrigin);
            Assert.GreaterOrEqual(cues.PlacedCount, 2);
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
