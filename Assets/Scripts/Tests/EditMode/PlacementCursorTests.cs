using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    public class PlacementCursorTests
    {
        private static float Flat(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        [Test]
        public void SixBySix_CenterStaysUnderTheCursor_CornerAnchorDoesNot()
        {
            var cursor = new Vector3(100f, 0f, 100f);
            const float cs = 1.5f;
            var origin = PlacementCursor.FootprintOrigin(cursor, Vector3.zero, cs, 6, 6);
            var center = PlacementCursor.Center(origin, 6, 6, cs, Vector3.zero);
            Assert.Less(Flat(center, cursor), 0.75f, "half a cell is the snap error");

            // The old rule used the cell under the cursor as the footprint's min corner.
            // A 6×6 pad (landing pad, temples) then sat ~3.5 m away on each axis.
            int oldX = Mathf.FloorToInt(cursor.x / cs);
            int oldZ = Mathf.FloorToInt(cursor.z / cs);
            var old = PlacementCursor.Center(new Vector2Int(oldX, oldZ), 6, 6, cs, Vector3.zero);
            Assert.Greater(Flat(old, cursor), 3f);
        }

        [Test]
        public void EveryFootprint_TracksTheCursor_IncludingAShiftedGrid()
        {
            var cursors = new[]
            {
                new Vector3(10f, 0f, 12f),
                new Vector3(192f, 0f, 192f),
                new Vector3(3.2f, 4f, 8.8f),
            };
            var gridOrigin = new Vector3(10f, 0f, 20f);
            foreach (var cursor in cursors)
            foreach (int n in new[] { 1, 4, 6 })
            {
                var origin = PlacementCursor.FootprintOrigin(cursor, gridOrigin, 1.5f, n, n);
                var center = PlacementCursor.Center(origin, n, n, 1.5f, gridOrigin);
                // One axis can miss by half a cell (0.75 m); the planar miss is the half-diagonal.
                Assert.Less(Flat(center, cursor), 1.07f, $"n={n} at {cursor}");
            }
        }

        [Test]
        public void PadAimedAtATradeZone_CenterLandsInsideTheRing()
        {
            var zone = new BuildZone
            {
                Kind = BuildZoneKind.TradePost,
                Center = new Vector3(80f, 0f, 90f),
                Radius = 7f,
            };
            var zones = new List<BuildZone> { zone };
            var origin = PlacementCursor.FootprintOrigin(zone.Center, Vector3.zero, 1.5f, 6, 6);
            var center = PlacementCursor.Center(origin, 6, 6, 1.5f, Vector3.zero);
            Assert.IsTrue(new BuildZoneTuning().Allows(BuildZoneKind.TradePost, center, zones, null));
            Assert.Less(Flat(center, zone.Center), zone.Radius);
        }

        [Test]
        public void GroundPick_FlatMatchesThePlane_AndIsNotTheRayOrigin()
        {
            var eye = new Vector3(0f, 20f, 0f);
            var ray = new Ray(eye, new Vector3(1f, -1f, 1f));
            Assert.IsTrue(GroundPick.TryHit(ray, (x, z) => 0f, out Vector3 flat));
            Assert.AreEqual(0f, flat.y, 0.001f);
            Assert.Greater(Flat(flat, Vector3.zero), 1f);
            Assert.AreNotEqual(eye, flat);
        }

        [Test]
        public void GroundPick_RaisedGroundPullsTheHitTowardTheCamera()
        {
            var ray = new Ray(new Vector3(0f, 20f, 0f), new Vector3(1f, -1f, 1f));
            Assert.IsTrue(GroundPick.TryHit(ray, (x, z) => 0f, out Vector3 flat));
            Assert.IsTrue(GroundPick.TryHit(ray, (x, z) => 6f, out Vector3 hill));
            Assert.AreEqual(6f, hill.y, 0.05f);
            Assert.Less(Flat(hill, Vector3.zero), Flat(flat, Vector3.zero));
        }

        [Test]
        public void GroundPick_UpwardRayMisses()
        {
            var ray = new Ray(new Vector3(0f, 20f, 0f), Vector3.up);
            Assert.IsFalse(GroundPick.TryHit(ray, (x, z) => 0f, out _));
            Assert.IsFalse(GroundPick.TryHit(ray, null, out _));
        }

        [Test]
        public void TradeZoneReason_NamesTheGoldRing()
        {
            var t = new BuildZoneTuning();
            StringAssert.Contains("ring", t.Reason(BuildZoneKind.TradePost, true));
            StringAssert.Contains("taken", t.Reason(BuildZoneKind.TradePost, false));
        }
    }
}
