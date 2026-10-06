using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    public class BuildZoneTests
    {
        private static List<BuildZone> Zones() => new List<BuildZone>
        {
            new BuildZone { Kind = BuildZoneKind.TradePost, Center = new Vector3(50, 0, 50), Radius = 7f },
            new BuildZone { Kind = BuildZoneKind.Mine, Center = new Vector3(120, 0, 50), Radius = 7f },
            new BuildZone { Kind = BuildZoneKind.Temple, Center = new Vector3(50, 0, 120), Radius = 7f },
        };

        [Test]
        public void KindFor_MapsPadMineAndTemples_AndLeavesTheRestFree()
        {
            var t = new BuildZoneTuning();
            Assert.AreEqual(BuildZoneKind.TradePost, t.KindFor(BuildingCategory.LandingPad));
            Assert.AreEqual(BuildZoneKind.Mine, t.KindFor(BuildingCategory.Mine));
            Assert.AreEqual(BuildZoneKind.Temple, t.KindFor(BuildingCategory.ClimateLoom));
            Assert.AreEqual(BuildZoneKind.Temple, t.KindFor(BuildingCategory.AegisSpire));
            Assert.AreEqual(BuildZoneKind.Temple, t.KindFor(BuildingCategory.DeepArchive));
            foreach (var free in new[] { BuildingCategory.Habitat, BuildingCategory.Market, BuildingCategory.Inn,
                         BuildingCategory.Blacksmith, BuildingCategory.Watchtower, BuildingCategory.DefenseWorkshop })
                Assert.AreEqual(BuildZoneKind.None, t.KindFor(free), free.ToString());
        }

        [Test]
        public void Allows_OnlyInsideAZoneOfTheRightKind()
        {
            var t = new BuildZoneTuning();
            var zones = Zones();
            Assert.IsTrue(t.Allows(BuildZoneKind.TradePost, new Vector3(52, 0, 49), zones, null));
            Assert.IsFalse(t.Allows(BuildZoneKind.TradePost, new Vector3(70, 0, 50), zones, null), "outside");
            Assert.IsFalse(t.Allows(BuildZoneKind.TradePost, new Vector3(120, 0, 50), zones, null), "that is a mine zone");
            Assert.IsTrue(t.Allows(BuildZoneKind.Mine, new Vector3(120, 0, 52), zones, null));
            Assert.IsTrue(t.Allows(BuildZoneKind.None, new Vector3(0, 0, 0), zones, null), "free categories pass anywhere");
        }

        [Test]
        public void AZoneHoldsOneBuilding()
        {
            var t = new BuildZoneTuning();
            var zones = Zones();
            var taken = new List<Vector3> { new Vector3(50, 0, 51) };
            Assert.IsFalse(t.Allows(BuildZoneKind.TradePost, new Vector3(52, 0, 49), zones, taken));
            Assert.IsTrue(t.Allows(BuildZoneKind.Mine, new Vector3(120, 0, 50), zones, taken), "other kinds unaffected");
        }

        [Test]
        public void NoZones_MeansNothingBoundCanBePlaced()
        {
            var t = new BuildZoneTuning();
            Assert.IsFalse(t.Allows(BuildZoneKind.Mine, new Vector3(1, 0, 1), new List<BuildZone>(), null));
            Assert.IsFalse(t.Allows(BuildZoneKind.Mine, new Vector3(1, 0, 1), null, null));
        }

        [Test]
        public void Disabled_FreesEverything()
        {
            var t = new BuildZoneTuning { enabled = false };
            Assert.AreEqual(BuildZoneKind.None, t.KindFor(BuildingCategory.LandingPad));
            Assert.AreEqual(BuildZoneKind.None, t.KindFor(BuildingCategory.Mine));
        }

        [Test]
        public void MineBands_AreFarFromTheBase_AndTradePostsSpreadOut()
        {
            var t = new BuildZoneTuning();
            foreach (var band in t.mineBands)
                Assert.GreaterOrEqual(band.minMeters, 55f, "mines must be a real walk out so raids matter");
            for (int i = 1; i < t.mineBands.Length; i++)
                Assert.GreaterOrEqual(t.mineBands[i].minMeters, t.mineBands[i - 1].minMeters, "near to far");
            Assert.Less(t.tradePostBands[0].minMeters, t.mineBands[0].minMeters, "pads can sit closer than mines");
            Assert.Greater(t.templeBands.Length, 0);
        }

        [Test]
        public void ReasonText_NamesTheProblem()
        {
            var t = new BuildZoneTuning();
            StringAssert.Contains("ore deposit", t.Reason(BuildZoneKind.Mine, true));
            StringAssert.Contains("taken", t.Reason(BuildZoneKind.TradePost, false));
            StringAssert.Contains("temple", t.Reason(BuildZoneKind.Temple, true));
        }
    }
}
