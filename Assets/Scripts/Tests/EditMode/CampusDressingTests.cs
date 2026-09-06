using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// still5 leftover: unused Commons cardinal showed an orange hull-port ring.
    /// Live kits must spawn those groups off; RefreshTubes enables docked faces only.
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
    }
}
