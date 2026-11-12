using NUnit.Framework;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// Status and defeat copy names the civ's castle. Neutral Commons, the guided tutorial, and a
    /// pre-civ save (v7 and earlier load as None) keep saying Colony Commons.
    /// </summary>
    public class CivHomeCopyTests
    {
        [Test]
        public void NeutralAndOldSaves_SayColonyCommons()
        {
            Assert.AreEqual("Colony Commons", CivHomeCopy.HomeName(null));
            Assert.AreEqual("Colony Commons", CivHomeCopy.HomeName(CivModifiers.For(CivId.None)));
            Assert.AreEqual("Colony Commons", CivHomeCopy.HomeName(CivModifiers.For(CivCatalog.FromSaved(0))));
            Assert.AreEqual("Colony Commons", CivHomeCopy.HomeName(new CivModifiers(null)));

            // The guided Earth start plays as None, including the continue that carries it to Luna.
            var guided = CivModifiers.For(CivChoice.ForNewGame(CivId.LumenConcord, CelestialBodyId.Earth, guidedStart: true));
            Assert.AreEqual("Colony Commons", CivHomeCopy.HomeName(guided));

            const string placed = "Colony Commons is down — build workshops, charging stations and shops anywhere nearby.";
            const string brief = "Colony Commons is down. Build anywhere on open ground.";
            const string fell = "The Colony Commons fell and the till with it. Retry this body, or New Game.";
            Assert.AreEqual(placed, CivHomeCopy.Placed(null));
            Assert.AreEqual(placed, CivHomeCopy.Placed(guided));
            Assert.AreEqual(brief, CivHomeCopy.PlacedBrief(CivModifiers.For(CivId.None)));
            Assert.AreEqual(fell, CivHomeCopy.Fell(CivModifiers.For(CivCatalog.FromSaved(0))));
        }

        [TestCase(CivId.LumenConcord, "Ziggurat Keep")]
        [TestCase(CivId.TetherExchange, "Ring Citadel")]
        [TestCase(CivId.BastionMandate, "Dome Bunker")]
        [TestCase(CivId.PathfinderAccord, "Stilt Arcology")]
        public void NamedCiv_StatusAndDefeat_NameTheCastle(CivId id, string home)
        {
            var civ = CivModifiers.For(id);
            Assert.AreEqual(home, civ.HomeName);
            Assert.AreEqual(home, CivHomeCopy.HomeName(civ));
            Assert.IsFalse(CivHomeCopy.UsesNeutralHome(civ));

            string placed = CivHomeCopy.Placed(civ);
            string brief = CivHomeCopy.PlacedBrief(civ);
            string fell = CivHomeCopy.Fell(civ);
            StringAssert.StartsWith(home + " is down — ", placed);
            StringAssert.Contains("build workshops, charging stations and shops anywhere nearby.", placed);
            StringAssert.StartsWith(home + " is down. ", brief);
            StringAssert.Contains("Build anywhere on open ground.", brief);
            StringAssert.StartsWith("The " + home + " fell", fell);
            StringAssert.Contains("Retry this body, or New Game.", fell);
            StringAssert.DoesNotContain("Colony Commons", placed);
            StringAssert.DoesNotContain("Colony Commons", brief);
            StringAssert.DoesNotContain("Colony Commons", fell);
        }

        [Test]
        public void SkippedGuidedStart_PathfinderHome_IsTheStiltArcology()
        {
            var picked = CivChoice.ForNewGame(CivId.PathfinderAccord, CelestialBodyId.Earth, guidedStart: false);
            var civ = CivModifiers.For(picked);
            string line = CivHomeCopy.Placed(civ);
            StringAssert.Contains("Stilt Arcology is down", line);
            StringAssert.DoesNotContain("Colony Commons", line);
            StringAssert.Contains("Stilt Arcology", CivHomeCopy.Fell(civ));
        }
    }
}
