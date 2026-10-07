using System.Collections.Generic;
using NUnit.Framework;

namespace SolarMajesty.Tests
{
    public class OrbitalPowersTests
    {
        private static bool NoTech(TechId _) => false;
        private static bool AllTech(TechId _) => true;

        [Test]
        public void RangeBands_FollowMajestySpellRanges()
        {
            var t = new OrbitalTuning(); // 2.5 Majesty units per metre
            Assert.AreEqual(1f, t.RangeMultiplier(0f));
            Assert.AreEqual(1f, t.RangeMultiplier(10f), "25 units");
            Assert.AreEqual(3f, t.RangeMultiplier(15f), "37.5 units");
            Assert.AreEqual(5f, t.RangeMultiplier(30f), "75 units");
            Assert.AreEqual(7f, t.RangeMultiplier(50f), "125 units");
            Assert.AreEqual(15f, t.RangeMultiplier(500f));
            Assert.AreEqual(15f, t.RangeMultiplier(float.PositiveInfinity), "no uplink at all");
        }

        [Test]
        public void Cost_IsBaseTimesRange_AndFreePowersStayFree()
        {
            var d = new OrbitalDirector(new OrbitalTuning());
            Assert.AreEqual(250, d.CostAt(OrbitalPowerId.KineticLance, 5f));
            Assert.AreEqual(750, d.CostAt(OrbitalPowerId.KineticLance, 15f));
            Assert.AreEqual(0, d.CostAt(OrbitalPowerId.TillAudit, 500f));
        }

        [Test]
        public void Tiers_UnlockByResearch()
        {
            var d = new OrbitalDirector(new OrbitalTuning());
            Assert.AreEqual(OrbitalCastCheck.Locked, d.Check(OrbitalPowerId.KineticLance, NoTech, 10000, 0f));
            bool UplinkOnly(TechId id) => id == TechId.OrbitalUplink;
            Assert.AreEqual(OrbitalCastCheck.Ok, d.Check(OrbitalPowerId.KineticLance, UplinkOnly, 10000, 0f));
            Assert.AreEqual(OrbitalCastCheck.Locked, d.Check(OrbitalPowerId.OrbitalBarrage, UplinkOnly, 10000, 0f));
            Assert.AreEqual(OrbitalCastCheck.Ok, d.Check(OrbitalPowerId.OrbitalBarrage, AllTech, 10000, 0f));
        }

        [Test]
        public void RequireResearchOff_UnlocksEverything()
        {
            var d = new OrbitalDirector(new OrbitalTuning { requireResearch = false });
            Assert.AreEqual(OrbitalCastCheck.Ok, d.Check(OrbitalPowerId.OrbitalBarrage, NoTech, 10000, 0f));
        }

        [Test]
        public void Check_TooPoorAtRange_ButAffordableNearTheUplink()
        {
            var d = new OrbitalDirector(new OrbitalTuning());
            Assert.AreEqual(OrbitalCastCheck.Ok, d.Check(OrbitalPowerId.KineticLance, AllTech, 300, 5f));
            Assert.AreEqual(OrbitalCastCheck.TooPoor, d.Check(OrbitalPowerId.KineticLance, AllTech, 300, 30f));
        }

        [Test]
        public void Cooldown_BlocksThenClears()
        {
            var d = new OrbitalDirector(new OrbitalTuning());
            d.StartCooldown(OrbitalPowerId.OrbitalBarrage);
            Assert.AreEqual(OrbitalCastCheck.Cooling, d.Check(OrbitalPowerId.OrbitalBarrage, AllTech, 10000, 0f));
            d.Tick(19f);
            Assert.AreEqual(OrbitalCastCheck.Cooling, d.Check(OrbitalPowerId.OrbitalBarrage, AllTech, 10000, 0f));
            d.Tick(1f);
            Assert.AreEqual(OrbitalCastCheck.Ok, d.Check(OrbitalPowerId.OrbitalBarrage, AllTech, 10000, 0f));
            Assert.AreEqual(OrbitalCastCheck.Ok, d.Check(OrbitalPowerId.KineticLance, AllTech, 10000, 0f), "cooldowns are per power");
        }

        [Test]
        public void Cooldowns_SurviveSaveAndLoad()
        {
            var a = new OrbitalDirector(new OrbitalTuning());
            a.StartCooldown(OrbitalPowerId.TillAudit);
            a.Tick(30f);
            var b = new OrbitalDirector(new OrbitalTuning());
            b.RestoreCooldowns(a.CaptureCooldowns());
            Assert.AreEqual(150f, b.ReadyIn(OrbitalPowerId.TillAudit), 1e-3f);
            b.RestoreCooldowns(null);
            Assert.AreEqual(0f, b.ReadyIn(OrbitalPowerId.TillAudit), "old saves: all ready");
        }

        [Test]
        public void Disabled_BlocksEverything()
        {
            var d = new OrbitalDirector(new OrbitalTuning { enabled = false });
            Assert.AreEqual(OrbitalCastCheck.Disabled, d.Check(OrbitalPowerId.KineticLance, AllTech, 10000, 0f));
        }

        [Test]
        public void Defaults_CoverEveryPower_WithTechTiers()
        {
            var t = new OrbitalTuning();
            var seen = new HashSet<OrbitalPowerId>();
            foreach (var p in t.powers)
            {
                Assert.IsTrue(seen.Add(p.id), $"duplicate {p.id}");
                Assert.IsNotNull(TechCatalog.Get(OrbitalTuning.TechFor(p.tier)), $"{p.id} tech exists");
            }
            Assert.AreEqual(System.Enum.GetValues(typeof(OrbitalPowerId)).Length, seen.Count);
        }

        [Test]
        public void Lance_KillsAStalker_ButNotAnAlpha()
        {
            var t = new OrbitalTuning();
            t.TryGet(OrbitalPowerId.KineticLance, out var lance);
            Assert.GreaterOrEqual(lance.magnitude, 28f, "stalker HP");
            Assert.Less(lance.magnitude, 28f * new KingdomThreatTuning().eliteHealthMul, "alpha needs more than one lance");
        }
    }
}
