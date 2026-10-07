using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// A flush footprint does not share cells with a power node, but its deck still
    /// crosses the node's wind turbines. The ghost must go red for every footprint.
    /// </summary>
    public class LandingPadOverlapTests
    {
        private const float Cell = 1.5f;

        [Test]
        public void FlushFootprints_MissPowerCells_ButAreBlockedByTheTurbine()
        {
            var placer = new BuildingPlacer(new ResourceManager());
            placer.BindGrid(Cell, Vector3.zero);
            var power = new Vector2Int(20, 20);
            placer.MarkOccupiedRect(power, 4, 4);
            placer.RegisterPiece(power, 4, 4, BuildingCategory.Power);

            // South edge touches the power node. Cells y=14..19 vs y=20..23.
            var padCell = new Vector2Int(18, 14);
            var pad = Data(BuildingCategory.LandingPad, 6, 6);
            Assert.IsTrue(placer.CanFit(pad, padCell), "cell test misses the rotor");
            Assert.IsFalse(placer.FootprintClear(pad, padCell));
            Assert.AreEqual(BuildingPlacer.BlockedByBuilding, BuildingPlacer.OccupancyBlock(placer, pad, padCell));
            StringAssert.Contains("building", BuildingPlacer.BlockedByBuilding);

            var yard = Data(BuildingCategory.EngineerWorkshop, 4, 4);
            var yardCell = new Vector2Int(20, 16);
            Assert.IsTrue(placer.CanFit(yard, yardCell));
            Assert.IsFalse(placer.FootprintClear(yard, yardCell), "4×4 flush against the rotor");

            var post = Data(BuildingCategory.Watchtower, 1, 1);
            var postCell = new Vector2Int(22, 19);
            Assert.IsTrue(placer.CanFit(post, postCell));
            Assert.IsFalse(placer.FootprintClear(post, postCell), "1×1 on the rotor");

            // One cell of dirt: the rotor no longer reaches.
            Assert.IsTrue(placer.FootprintClear(pad, new Vector2Int(18, 13)));
            Assert.IsTrue(placer.FootprintClear(yard, new Vector2Int(20, 15)));
            Assert.IsTrue(placer.FootprintClear(post, new Vector2Int(22, 18)));

            Object.DestroyImmediate(pad);
            Object.DestroyImmediate(yard);
            Object.DestroyImmediate(post);
        }

        [Test]
        public void PowerNode_WhoseTurbineLandsOnAPad_IsBlocked()
        {
            var placer = new BuildingPlacer(new ResourceManager());
            placer.BindGrid(Cell, Vector3.zero);
            var padCell = new Vector2Int(18, 14);
            placer.MarkOccupiedRect(padCell, 6, 6);
            placer.RegisterPiece(padCell, 6, 6, BuildingCategory.LandingPad);

            var powerCell = new Vector2Int(20, 20);
            var power = Data(BuildingCategory.Power, 4, 4);
            Assert.IsTrue(placer.CanFit(power, powerCell));
            Assert.IsFalse(placer.FootprintClear(power, powerCell));
            Assert.AreEqual("footprint_blocked", FailReason(placer, power, powerCell));

            Object.DestroyImmediate(power);
        }

        [Test]
        public void SharedCells_StillSayBlockedByABuilding_AndAHabitatDoesNotGrowRotors()
        {
            var placer = new BuildingPlacer(new ResourceManager());
            placer.BindGrid(Cell, Vector3.zero);
            var power = new Vector2Int(20, 20);
            placer.MarkOccupiedRect(power, 4, 4);
            placer.RegisterPiece(power, 4, 4, BuildingCategory.Power);

            var pad = Data(BuildingCategory.LandingPad, 6, 6);
            Assert.IsFalse(placer.CanFit(pad, power));
            Assert.AreEqual(BuildingPlacer.BlockedByBuilding, BuildingPlacer.OccupancyBlock(placer, pad, power));

            var hab = new Vector2Int(40, 40);
            placer.MarkOccupiedRect(hab, 4, 4);
            placer.RegisterPiece(hab, 4, 4, BuildingCategory.Habitat);
            var shop = Data(BuildingCategory.EngineerWorkshop, 4, 4);
            var shopCell = new Vector2Int(36, 40);
            Assert.IsTrue(placer.CanFit(shop, shopCell));
            Assert.IsTrue(placer.FootprintClear(shop, shopCell), "flush against a house is not a turbine");
            Assert.IsNull(BuildingPlacer.OccupancyBlock(placer, shop, shopCell));

            // A save that already clips still restores: TryRestore does not use the dress test.
            Assert.IsTrue(placer.TryRestore(pad, new Vector2Int(18, 14), Vector3.zero, 1f, out _));

            Object.DestroyImmediate(pad);
            Object.DestroyImmediate(shop);
        }

        private static string FailReason(BuildingPlacer placer, BuildingData data, Vector2Int cell)
        {
            placer.TryPlace(data, cell, Vector3.zero, out _, out string reason);
            return reason;
        }

        private static BuildingData Data(BuildingCategory cat, int w, int h)
        {
            var data = ScriptableObject.CreateInstance<BuildingData>();
            data.category = cat;
            data.footprintWidth = w;
            data.footprintHeight = h;
            data.displayName = cat.ToString();
            return data;
        }
    }
}
