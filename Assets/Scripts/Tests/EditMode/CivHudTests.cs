using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// Phase 1 HUD (design doc section 7 "Showing the civ", section 8 item 7): the civ badge,
    /// signature names on unit nameplates, perk deltas next to hire and flag prices, and the
    /// Commons hit points as the civ has them. The price a tooltip shows must be the price the
    /// game charges, for every civ; the neutral civ shows no badge and no delta.
    /// </summary>
    public class CivHudTests
    {
        private readonly List<UnityEngine.Object> _made = new List<UnityEngine.Object>();

        private static CivModifiers None => CivModifiers.For(CivId.None);
        private static CivModifiers Concord => CivModifiers.For(CivId.LumenConcord);
        private static CivModifiers Exchange => CivModifiers.For(CivId.TetherExchange);
        private static CivModifiers Mandate => CivModifiers.For(CivId.BastionMandate);
        private static CivModifiers Pathfinder => CivModifiers.For(CivId.PathfinderAccord);

        [SetUp]
        public void SetUp()
        {
            CivCatalog.UseDatabase(null);
            Loc.SetLanguage(Loc.DefaultLanguage);
        }

        [TearDown]
        public void TearDown()
        {
            CivCatalog.ReloadDatabase();
            foreach (var o in _made)
            {
                if (o != null) UnityEngine.Object.DestroyImmediate(o);
            }
            _made.Clear();
        }

        /// <summary>Every civ the build knows (the neutral one included), plus a null civ.</summary>
        private static IEnumerable<CivModifiers> EveryCiv()
        {
            yield return null;
            foreach (CivId id in Enum.GetValues(typeof(CivId)))
                yield return CivModifiers.For(id);
        }

        private static string Name(CivModifiers civ) => civ != null ? civ.Id.ToString() : "null";

        private static IEnumerable<CivModifiers> Neutral()
        {
            yield return null;
            yield return None;
        }

        private BuildingData Building(BuildingCategory cat, int price)
        {
            var data = ScriptableObject.CreateInstance<BuildingData>();
            _made.Add(data);
            data.category = cat;
            data.displayName = cat.ToString();
            data.buildCost = Wallet.Credits(price);
            data.buildTimeSeconds = 10f;
            return data;
        }

        private static readonly Regex Eu = new Regex(@"(\d[\d,]*) EU", RegexOptions.CultureInvariant);

        /// <summary>Every "N EU" in <paramref name="text"/>, in order.</summary>
        private static List<int> Prices(string text)
        {
            var list = new List<int>();
            foreach (Match m in Eu.Matches(text ?? ""))
                list.Add(int.Parse(m.Groups[1].Value.Replace(",", ""), CultureInfo.InvariantCulture));
            return list;
        }

        // ---- 1. The tooltip price is the charged price, for every civ ---------------------------

        [Test]
        public void HireTooltip_ShowsTheFeeTheGameCharges_ForEveryCivAndClass()
        {
            foreach (var civ in EveryCiv())
            {
                foreach (SpecialistClass cls in Enum.GetValues(typeof(SpecialistClass)))
                {
                    // GameLoop.TryFabricateRobot charges CivRules.HireCost(_civ, cls).
                    int charged = CivRules.HireCost(civ, cls);
                    string line = CivHud.HireLine(civ, cls);
                    var prices = Prices(line);
                    Assert.AreEqual(1, prices.Count, line);
                    Assert.AreEqual(charged, prices[0], $"{Name(civ)} {cls}: {line}");
                    Assert.AreEqual(charged, CivHud.HirePrice(civ, cls));
                }
            }
        }

        [Test]
        public void WorkshopBuildTooltip_CarriesTheHireFee_AndOnlyForWorkshops()
        {
            foreach (var civ in EveryCiv())
            {
                foreach (BuildingCategory cat in Enum.GetValues(typeof(BuildingCategory)))
                {
                    var cls = MajestyEconomy.ClassForWorkshop(cat);
                    string hire = CivHud.WorkshopHireLine(civ, cat);
                    if (!cls.HasValue)
                    {
                        Assert.IsNull(hire, cat.ToString());
                        continue;
                    }
                    var tip = HudTips.BuildEntry("WORKSHOP", null, "200 EU", false, true, null, hire);
                    StringAssert.Contains(hire, tip.Body);
                    Assert.AreEqual(CivRules.HireCost(civ, cls.Value), Prices(hire)[0], $"{Name(civ)} {cat}");
                }
            }
        }

        [Test]
        public void FlagTooltip_ShowsWhatTheTillFinallyPays_ForEveryCivAndFlag()
        {
            var bounties = new List<int> { MajestyEconomy.FlagMinBounty, 450, MajestyEconomy.FlagMaxBounty };
            foreach (FlagType t in Enum.GetValues(typeof(FlagType)))
                bounties.Add(MajestyEconomy.FlagDefaultBounty(t));

            foreach (var civ in EveryCiv())
            {
                foreach (int bounty in bounties)
                {
                    // The economy path: the whole bounty is escrowed, and the civ's share comes back on completion.
                    var res = new ResourceManager();
                    res.Set(ResourceId.Metals, 10000);
                    var eco = new SimpleEconomy(res) { ResupplyEnabled = false, Civ = civ };
                    Assert.IsTrue(eco.TryEscrowBounty(bounty, out int escrow));
                    Assert.AreEqual(bounty, escrow);
                    eco.ReleaseBountyEscrow(escrow);
                    int paid = 10000 - res.Get(ResourceId.Metals);

                    Assert.AreEqual(paid, CivHud.BountyTillPrice(civ, bounty), $"{Name(civ)} {bounty}");
                    var prices = Prices(CivHud.FlagPriceLine(civ, bounty));
                    Assert.AreEqual(bounty, prices[0], "the posted bounty comes first");
                    Assert.AreEqual(paid, prices[prices.Count - 1], $"{Name(civ)} {bounty}: {CivHud.FlagPriceLine(civ, bounty)}");

                    string till = CivHud.BountyTillLine(civ, bounty);
                    if (paid == bounty) Assert.IsNull(till);
                    else Assert.AreEqual(paid, Prices(till)[0]);
                }

                foreach (FlagType t in Enum.GetValues(typeof(FlagType)))
                {
                    int bounty = MajestyEconomy.FlagDefaultBounty(t);
                    var tip = HudTips.FlagKind(t, civ, bounty);
                    StringAssert.StartsWith(HudTips.FlagKind(t).Body, tip.Body, "the flag's own words stay first");
                    StringAssert.Contains(CivHud.FlagPriceLine(civ, bounty), tip.Body);
                    Assert.AreEqual(HudTips.FlagKind(t).Title, tip.Title);
                }
            }
        }

        [Test]
        public void BuildTooltip_ShowsThePlacersPrice_ForEveryCiv_WithTheSurchargeOnTop()
        {
            var buildings = new[]
            {
                Building(BuildingCategory.Watchtower, 150),
                Building(BuildingCategory.Mine, 300),
                Building(BuildingCategory.LandingPad, 200),
                Building(BuildingCategory.Market, 250),
                Building(BuildingCategory.ScoutWorkshop, 200)
            };
            foreach (var civ in EveryCiv())
            {
                foreach (int owned in new[] { 0, 1, 2 })
                {
                    var placer = new BuildingPlacer(new ResourceManager()) { Civ = civ, OwnedCount = cat => owned };
                    foreach (var b in buildings)
                    {
                        int charged = CivHud.SumCost(placer.CostFor(b));
                        // As the build list composes it: the placer's price, then the civ's delta.
                        string cost = CivHud.WithDelta($"{charged} EU", CivHud.BuildingDelta(civ, b));
                        var tip = HudTips.BuildEntry(b.displayName.ToUpperInvariant(), null, cost, false, true, null,
                            CivHud.WorkshopHireLine(civ, b.category));
                        Assert.AreEqual(charged, Prices(tip.Body)[0], $"{Name(civ)} {b.category} x{owned}: {tip.Body}");
                    }
                }
            }
        }

        [Test]
        public void ArmingAndRefabTooltips_ShowTheChargedPrice_ForEveryCiv()
        {
            var shop = Building(BuildingCategory.DefenseWorkshop, 300);
            var other = Building(BuildingCategory.Market, 300);
            foreach (var civ in EveryCiv())
            {
                // GameLoop.TryArmWatchtower and the card chip charge CivRules.WatchtowerArmingCost.
                Assert.AreEqual(CivRules.WatchtowerArmingCost(civ), Prices(CivHud.ArmingPriceText(civ))[0], Name(civ));
                Assert.AreEqual(CivRules.WatchtowerArmingCost(civ), Prices(HudTips.ArmLasers(civ).Body)[0], Name(civ));
                StringAssert.Contains(CivRules.GunRange(civ).ToString("0.#", CultureInfo.InvariantCulture) + " m",
                    HudTips.ArmLasers(civ).Body);
                // GameLoop.TryRefabRookie charges CivRules.RefabMetals.
                Assert.AreEqual(CivRules.RefabMetals(civ, shop), Prices(HudTips.Refab(civ, shop).Body)[0], Name(civ));
                Assert.AreEqual(CivRules.RefabMetals(civ, other), Prices(CivHud.RefabPriceText(civ, other))[0], Name(civ));
            }
        }

        // ---- 2. The neutral civ: no badge, no delta, base names, 140 HP --------------------------

        [Test]
        public void NeutralCiv_HasNoBadge_NoDelta_BaseNames_And140Hp()
        {
            var tower = Building(BuildingCategory.Watchtower, 150);
            var shop = Building(BuildingCategory.ScoutWorkshop, 200);
            foreach (var civ in Neutral())
            {
                string n = Name(civ);
                Assert.IsFalse(CivHud.HasBadge(civ), n);
                Assert.IsNull(CivHud.BadgeText(civ), n);
                Assert.AreEqual(0, CivHud.BadgeVariants(civ).Length, n);
                Assert.IsTrue(CivHud.BadgeTip(civ, 140f, 140f).IsEmpty, n);

                foreach (SpecialistClass cls in Enum.GetValues(typeof(SpecialistClass)))
                {
                    string line = CivHud.HireLine(civ, cls);
                    StringAssert.DoesNotContain("%", line, n);
                    StringAssert.DoesNotContain(",", line.Replace("1,000", ""), n + ": no delta after the price");
                    Assert.AreEqual(CivHud.BaseUnitName(cls), CivHud.UnitName(civ, cls), $"{n} {cls}");
                }
                foreach (FlagType t in Enum.GetValues(typeof(FlagType)))
                {
                    int bounty = MajestyEconomy.FlagDefaultBounty(t);
                    Assert.AreEqual(Loc.F("Bounty {0:N0} EU.", bounty), CivHud.FlagPriceLine(civ, bounty), $"{n} {t}");
                    Assert.IsNull(CivHud.BountyTillLine(civ, bounty), $"{n} {t}");
                    Assert.AreEqual(HudTips.BountyAmount().Body, HudTips.BountyAmount(civ, bounty).Body);
                }
                Assert.AreEqual("", CivHud.BuildingDelta(civ, tower), n);
                Assert.AreEqual("", CivHud.BuildingDelta(civ, shop), n);
                Assert.AreEqual("400 EU", CivHud.ArmingPriceText(civ), n);
                Assert.AreEqual(140, CivHud.CommonsMaxHp(civ), n);
                Assert.AreEqual("Defense Mech", CivHud.UnitName(civ, SpecialistClass.DefenseMech), n);
            }
        }

        [Test]
        public void GuidedStart_PlaysAsNeutral_SoTheHudShowsNoBadge()
        {
            // The HUD reads the live civ (GameLoop.Civ), which the guided Earth start builds from
            // CivChoice.ForNewGame: None whatever card was left picked.
            foreach (var picked in CivCatalog.Playable())
            {
                var live = CivModifiers.For(CivChoice.ForNewGame(picked.id, CelestialBodyId.Earth, guidedStart: true));
                Assert.IsFalse(CivHud.HasBadge(live), picked.id.ToString());
                Assert.IsNull(CivHud.BountyTillLine(live, 400));
                var chosen = CivModifiers.For(CivChoice.ForNewGame(picked.id, CelestialBodyId.Luna, guidedStart: false));
                Assert.IsTrue(CivHud.HasBadge(chosen), picked.id.ToString());
            }
        }

        // ---- 3. Signature names --------------------------------------------------------------------

        [Test]
        public void Nameplates_UseTheSignatureName_AndKeepTheBaseNameWithoutAnEntry()
        {
            Assert.AreEqual("Oathblade", CivHud.UnitName(Concord, SpecialistClass.DefenseMech));
            Assert.AreEqual("Herald", CivHud.UnitName(Concord, SpecialistClass.ScoutDrone));
            Assert.AreEqual("Bulwark", CivHud.UnitName(Mandate, SpecialistClass.DefenseMech));
            Assert.AreEqual("Gate Warden", CivHud.UnitName(Exchange, SpecialistClass.SentinelMech));
            Assert.AreEqual("Cartographer", CivHud.UnitName(Pathfinder, SpecialistClass.SurveyorBot));

            // No Concord entry for the Geologist: today's name, and the caller's fallback is kept.
            Assert.IsFalse(Concord.HasUnitFor(SpecialistClass.GeologistBot));
            Assert.AreEqual("Geologist Bot", CivHud.UnitName(Concord, SpecialistClass.GeologistBot));
            Assert.AreEqual("Geologist Bot", CivHud.UnitName(Concord, SpecialistClass.GeologistBot, "Geologist Bot"));
            Assert.AreEqual("Defense Mech", CivHud.UnitName(Exchange, SpecialistClass.DefenseMech));

            // Every roster entry in the catalog is the name its nameplate shows; nothing is invented.
            foreach (var def in CivCatalog.All)
            {
                var civ = CivModifiers.For(def.id);
                foreach (SpecialistClass cls in Enum.GetValues(typeof(SpecialistClass)))
                {
                    string expected = civ.HasUnitFor(cls) && !string.IsNullOrEmpty(civ.UnitFor(cls).displayName)
                        ? civ.UnitFor(cls).displayName
                        : CivHud.BaseUnitName(cls);
                    Assert.AreEqual(expected, CivHud.UnitName(civ, cls), $"{def.id} {cls}");
                }
            }
        }

        [Test]
        public void BaseUnitNames_AreTheSharedPersonalityNames()
        {
            foreach (SpecialistClass cls in Enum.GetValues(typeof(SpecialistClass)))
            {
                var data = ScriptableObject.CreateInstance<SpecialistData>();
                _made.Add(data);
                data.specialistClass = cls;
                SpecialistPersonality.Apply(data);
                Assert.AreEqual(data.displayName, CivHud.BaseUnitName(cls), cls.ToString());
            }
        }

        // ---- 4. Deltas: right where the price moves, absent where it does not --------------------

        [Test]
        public void Deltas_MatchTheDesignDocNumbers()
        {
            // Hires (doc section 2): Concord 20% cheaper, Mandate 20% dearer.
            Assert.AreEqual("Hire: Herald 120 EU, -20%.", CivHud.HireLine(Concord, SpecialistClass.ScoutDrone));
            Assert.AreEqual("Hire: Oathblade 400 EU, -20%.", CivHud.HireLine(Concord, SpecialistClass.DefenseMech));
            Assert.AreEqual("Hire: Picket 180 EU, +20%.", CivHud.HireLine(Mandate, SpecialistClass.ScoutDrone));
            Assert.AreEqual("Hire: Bulwark 600 EU, +20%.", CivHud.HireLine(Mandate, SpecialistClass.DefenseMech));
            // Unchanged fee: no delta, never "+0%".
            Assert.AreEqual("Hire: Scout Drone 150 EU.", CivHud.HireLine(Exchange, SpecialistClass.ScoutDrone));
            Assert.AreEqual("Hire: Scout Drone 150 EU.", CivHud.HireLine(Pathfinder, SpecialistClass.ScoutDrone));

            // Bounties: the Concord till pays 90% (Explore 400 -> 360, Clear Threat 800 -> 720).
            StringAssert.Contains("360 EU, -10%", CivHud.FlagPriceLine(Concord, 400));
            StringAssert.Contains("720 EU, -10%", CivHud.FlagPriceLine(Concord, 800));
            StringAssert.Contains("360 EU, -10%", CivHud.BountyTillLine(Concord, 400));
            StringAssert.Contains("360 EU, -10%", HudTips.FlagKind(FlagType.Explore, Concord, 400).Body);
            StringAssert.Contains("360 EU, -10%", HudTips.BountyAmount(Concord, 400).Body);
            foreach (var civ in new[] { Exchange, Mandate, Pathfinder })
            {
                Assert.AreEqual("Bounty 400 EU.", CivHud.FlagPriceLine(civ, 400), Name(civ));
                Assert.IsNull(CivHud.BountyTillLine(civ, 400), Name(civ));
            }

            // Outpost buildings: Pathfinder 20% cheaper (Watchtower 150 -> 120, Ore Mine 300 -> 240, Landing Pad 200 -> 160).
            var tower = Building(BuildingCategory.Watchtower, 150);
            var mine = Building(BuildingCategory.Mine, 300);
            var pad = Building(BuildingCategory.LandingPad, 200);
            var market = Building(BuildingCategory.Market, 250);
            var placer = new BuildingPlacer(new ResourceManager()) { Civ = Pathfinder };
            Assert.AreEqual(120, CivHud.SumCost(placer.CostFor(tower)));
            Assert.AreEqual(240, CivHud.SumCost(placer.CostFor(mine)));
            Assert.AreEqual(160, CivHud.SumCost(placer.CostFor(pad)));
            Assert.AreEqual("-20%", CivHud.BuildingDelta(Pathfinder, tower));
            Assert.AreEqual("-20%", CivHud.BuildingDelta(Pathfinder, mine));
            Assert.AreEqual("-20%", CivHud.BuildingDelta(Pathfinder, pad));
            Assert.AreEqual("", CivHud.BuildingDelta(Pathfinder, market), "not an outpost");
            foreach (var civ in new[] { Concord, Exchange, Mandate })
                Assert.AreEqual("", CivHud.BuildingDelta(civ, tower), Name(civ));

            // Laser arming: Mandate 400 -> 250.
            Assert.AreEqual("250 EU, -38%", CivHud.ArmingPriceText(Mandate));
            StringAssert.Contains("21 m", HudTips.ArmLasers(Mandate).Body);
            Assert.AreEqual("400 EU", CivHud.ArmingPriceText(Concord));

            // Re-fab is the hire fee, so it moves with it.
            var defShop = Building(BuildingCategory.DefenseWorkshop, 300);
            Assert.AreEqual("400 EU, -20%", CivHud.RefabPriceText(Concord, defShop));
            Assert.AreEqual("600 EU, +20%", CivHud.RefabPriceText(Mandate, defShop));
        }

        [Test]
        public void Delta_RoundsAndSigns_AndIsEmptyWhenUnchanged()
        {
            Assert.AreEqual("", CivHud.Delta(150, 150));
            Assert.AreEqual("", CivHud.Delta(0, 50), "no base, no percent");
            Assert.AreEqual("-20%", CivHud.Delta(150, 120));
            Assert.AreEqual("+20%", CivHud.Delta(150, 180));
            Assert.AreEqual("-10%", CivHud.Delta(400, 360));
            Assert.AreEqual("-38%", CivHud.Delta(400, 250), "37.5 rounds away from zero");
            Assert.AreEqual("-1 EU", CivHud.Delta(1000, 999), "too small for a percent");
            Assert.AreEqual(-20, CivHud.PercentChange(150, 120));
            Assert.AreEqual(20, CivHud.PercentChange(500, 600));
            Assert.AreEqual("150 EU", CivHud.PriceText(150, 150));
            Assert.AreEqual("120 EU, -20%", CivHud.PriceText(150, 120));
            Assert.AreEqual("1,000 EU", CivHud.PriceText(1000, 1000));
            Assert.AreEqual("120 EU", CivHud.WithDelta("120 EU", ""));
            Assert.AreEqual("120 EU, -20%", CivHud.WithDelta("120 EU", "-20%"));
        }

        [Test]
        public void Deltas_AreShortAndLinesFitATooltip()
        {
            // The tooltip wraps at HudTooltipMath.MaxWidth (300 px); keep each price line short so
            // it never runs past the plate (the Session 31 Mandate clip).
            foreach (var civ in EveryCiv())
            {
                foreach (SpecialistClass cls in Enum.GetValues(typeof(SpecialistClass)))
                {
                    string line = CivHud.HireLine(civ, cls);
                    Assert.LessOrEqual(line.Length, 40, line);
                    Assert.LessOrEqual(CivHud.Delta(MajestyEconomy.HireCost(cls), CivRules.HireCost(civ, cls)).Length, 5, line);
                }
                Assert.LessOrEqual(CivHud.Delta(400, CivHud.BountyTillPrice(civ, 400)).Length, 5);
                Assert.LessOrEqual(CivHud.ArmingPriceText(civ).Length, 16);
            }
        }

        // ---- 5. Commons hit points -------------------------------------------------------------------

        [Test]
        public void CommonsHp_IsTheCivsValue_Mandate200_Pathfinder100_Others140()
        {
            Assert.AreEqual(140, CivHud.CommonsMaxHp(null));
            Assert.AreEqual(140, CivHud.CommonsMaxHp(None));
            Assert.AreEqual(140, CivHud.CommonsMaxHp(Concord));
            Assert.AreEqual(140, CivHud.CommonsMaxHp(Exchange));
            Assert.AreEqual(200, CivHud.CommonsMaxHp(Mandate));
            Assert.AreEqual(100, CivHud.CommonsMaxHp(Pathfinder));

            foreach (var civ in EveryCiv())
            {
                // VillageExpansion configures a Commons with CivRules.CommonsHealth(Civ); the card reads it back.
                var go = new GameObject("Commons");
                _made.Add(go);
                var st = go.AddComponent<ColonyStructure>();
                st.Configure(StructureRole.Core, null, CivRules.CommonsHealth(civ), BuildingCategory.Commons);
                int max = CivHud.CommonsMaxHp(civ);
                Assert.AreEqual(max, Mathf.RoundToInt(st.MaxHealth), Name(civ));
                Assert.AreEqual($"{max}/{max}", CivHud.HpText(st.Health, st.MaxHealth), Name(civ));
                st.RestoreHealth01(0.5f);
                Assert.AreEqual($"{Mathf.CeilToInt(max * 0.5f - 0.001f)}/{max}", CivHud.HpText(st.Health, st.MaxHealth), Name(civ));

                if (CivHud.HasBadge(civ))
                {
                    StringAssert.Contains($"Commons {Mathf.CeilToInt(max * 0.5f - 0.001f)}/{max} HP.",
                        CivHud.BadgeTip(civ, st.Health, st.MaxHealth).Body, Name(civ));
                    StringAssert.Contains($"Commons {max} HP.", CivHud.BadgeTip(civ, 0f, -1f).Body, "no Commons standing");
                }
            }
        }

        [Test]
        public void HpText_RoundsTheCurrentUp_AndClamps()
        {
            Assert.AreEqual("200/200", CivHud.HpText(200f, 200f));
            Assert.AreEqual("1/100", CivHud.HpText(0.4f, 100f), "a standing Commons never reads 0");
            Assert.AreEqual("0/100", CivHud.HpText(0f, 100f));
            Assert.AreEqual("140/140", CivHud.HpText(150f, 140f));
        }

        // ---- 6. Badge ----------------------------------------------------------------------------------

        [Test]
        public void Badge_ShowsTheCivNameAndColour_WithAShortFormForLongNames()
        {
            foreach (var def in CivCatalog.All)
            {
                var civ = CivModifiers.For(def.id);
                if (def.id == CivId.None) continue;
                Assert.IsTrue(CivHud.HasBadge(civ), def.id.ToString());
                Assert.AreEqual(def.displayName.ToUpperInvariant(), CivHud.BadgeText(civ));
                Assert.AreEqual(def.teamSecondary, CivHud.BadgeColour(civ));
                var tip = CivHud.BadgeTip(civ, 0f, -1f);
                Assert.AreEqual(CivHud.BadgeText(civ), tip.Title);
                StringAssert.Contains(def.SummaryText, tip.Body);
            }
            CollectionAssert.AreEqual(new[] { "PATHFINDER ACCORD", "ACCORD" }, CivHud.BadgeVariants(Pathfinder));
            CollectionAssert.AreEqual(new[] { "TETHER EXCHANGE", "EXCHANGE" }, CivHud.BadgeVariants(Exchange));
            CollectionAssert.AreEqual(new[] { "BASTION MANDATE", "MANDATE" }, CivHud.BadgeVariants(Mandate));
        }

        // ---- 7. Player-facing words ---------------------------------------------------------------------

        private static readonly Regex Forbidden = new Regex(
            @"\b(hero|heroes|treasury|purses?|overseer|court|crown|kingdom|conquest|chest|party|inn|habs?|beds|" +
            @"(?<!solar[\s_-]?)majesty)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        [Test]
        public void PlayerFacingText_UsesTheColonyWords()
        {
            var shop = Building(BuildingCategory.ScoutWorkshop, 200);
            var tower = Building(BuildingCategory.Watchtower, 150);
            var texts = new List<string>();
            foreach (var civ in EveryCiv())
            {
                texts.AddRange(CivHud.BadgeVariants(civ));
                var badge = CivHud.BadgeTip(civ, 70f, 140f);
                texts.Add(badge.Title);
                texts.Add(badge.Body);
                foreach (SpecialistClass cls in Enum.GetValues(typeof(SpecialistClass)))
                {
                    texts.Add(CivHud.HireLine(civ, cls));
                    texts.Add(CivHud.UnitName(civ, cls));
                }
                foreach (FlagType t in Enum.GetValues(typeof(FlagType)))
                {
                    int bounty = MajestyEconomy.FlagDefaultBounty(t);
                    texts.Add(HudTips.FlagKind(t, civ, bounty).Body);
                    texts.Add(HudTips.BountyAmount(civ, bounty).Body);
                }
                texts.Add(HudTips.ArmLasers(civ).Body);
                texts.Add(HudTips.Refab(civ, shop).Body);
                texts.Add(HudTips.BuildEntry("WATCHTOWER", null,
                    CivHud.WithDelta("150 EU", CivHud.BuildingDelta(civ, tower)), false, true, null,
                    CivHud.WorkshopHireLine(civ, shop.category)).Body);
            }
            foreach (string text in texts)
            {
                if (string.IsNullOrEmpty(text)) continue;
                Assert.IsFalse(Forbidden.IsMatch(text), "forbidden word in: " + text);
            }
        }
    }
}
