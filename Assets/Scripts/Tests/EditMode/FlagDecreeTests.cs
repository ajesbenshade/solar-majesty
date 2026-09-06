using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace SolarMajesty.Tests
{
    public class FlagDecreeTests
    {
        [Test]
        public void Catalog_HasTwentyFourUniqueIds()
        {
            var seen = new HashSet<string>();
            var all = FlagDecreeIds.All;

            Assert.AreEqual(24, all.Count);
            for (int i = 0; i < all.Count; i++)
            {
                Assert.IsTrue(seen.Add(all[i].Id), "duplicate id: " + all[i].Id);
                Assert.IsFalse(string.IsNullOrEmpty(all[i].Title));
                Assert.IsFalse(string.IsNullOrEmpty(all[i].Intent));
            }
        }

        [Test]
        public void Catalog_EightDecreesPerW2Body_AndNoneForBeltEuropa()
        {
            Assert.AreEqual(8, FlagDecreeIds.ForBody(CelestialBodyId.Earth).Count);
            Assert.AreEqual(8, FlagDecreeIds.ForBody(CelestialBodyId.Luna).Count);
            Assert.AreEqual(8, FlagDecreeIds.ForBody(CelestialBodyId.Mars).Count);
            Assert.AreEqual(0, FlagDecreeIds.ForBody(CelestialBodyId.Belt).Count);
            Assert.AreEqual(0, FlagDecreeIds.ForBody(CelestialBodyId.Europa).Count);
        }

        [Test]
        public void Catalog_IdsMatchBodyAndTypeTokens()
        {
            var all = FlagDecreeIds.All;
            for (int i = 0; i < all.Count; i++)
            {
                var d = all[i];
                string expectedPrefix = FlagDecreeIds.BodyToken(d.Body) + "." + FlagDecreeIds.TypeToken(d.Type) + ".";
                Assert.IsTrue(d.Id.StartsWith(expectedPrefix, StringComparison.Ordinal),
                    d.Id + " should start with " + expectedPrefix);
            }
        }

        [Test]
        public void TryGet_FindsAKnownDecree()
        {
            Assert.IsTrue(FlagDecreeIds.TryGet(FlagDecreeIds.LunaRootTheHopperSaboteurs, out var d));
            Assert.AreEqual(CelestialBodyId.Luna, d.Body);
            Assert.AreEqual(FlagType.ClearThreat, d.Type);
            Assert.AreEqual("Root the Hopper Saboteurs", d.Title);
        }

        [Test]
        public void TryGet_RejectsUnknownAndEmpty()
        {
            Assert.IsFalse(FlagDecreeIds.TryGet("belt.explore.not_in_w2", out _));
            Assert.IsFalse(FlagDecreeIds.TryGet("", out _));
            Assert.IsFalse(FlagDecreeIds.TryGet(null, out _));
        }

        [Test]
        public void TypeToken_CoversEveryFlagType()
        {
            foreach (FlagType type in Enum.GetValues(typeof(FlagType)))
            {
                string token = FlagDecreeIds.TypeToken(type);
                Assert.IsFalse(string.IsNullOrEmpty(token));
                Assert.AreNotEqual("flag", token, "missing TypeToken for " + type);
            }
        }
    }
}
