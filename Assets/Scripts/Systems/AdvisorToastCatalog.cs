// W2 advisor copy — source of truth: Docs/W2_ADVISOR_AND_CHAIN_BEATS.md
// Localization handles only. Decree ids stay FlagDecreeIds consts.

using System.Collections.Generic;

namespace SolarMajesty
{
    public enum AdvisorFireWhen
    {
        Post = 0,
        Claim = 1,
        Complete = 2,
        Travel = 3
    }

    public readonly struct AdvisorToast
    {
        public readonly string Key;
        public readonly string DecreeId;
        public readonly AdvisorFireWhen When;
        public readonly string Line;

        public AdvisorToast(string key, string decreeId, AdvisorFireWhen when, string line)
        {
            Key = key;
            DecreeId = decreeId;
            When = when;
            Line = line;
        }
    }

    /// <summary>
    /// 24 decree toasts + travel framing + complete overlays. Do not invent slugs.
    /// </summary>
    public static class AdvisorToastCatalog
    {
        public const string TravelEarthEmptyDrop = "advisor.travel.earth.empty_drop";
        public const string TravelEarthToLuna = "advisor.travel.earth.to_luna";
        public const string TravelLunaArrival = "advisor.travel.luna.arrival";
        public const string TravelLunaToMars = "advisor.travel.luna.to_mars";
        public const string TravelMarsArrival = "advisor.travel.mars.arrival";
        public const string TravelMarsToBelt = "advisor.travel.mars.to_belt";
        public const string LessonEarthGreedBuild = "advisor.lesson.earth.greed_build";

        public const string CompleteCharterTheHall = "advisor.earth.charter_the_hall.complete";
        public const string CompleteStageTheLunarRocket = "advisor.earth.stage_the_lunar_rocket.complete";
        public const string CompleteCommissionTheMarsShip = "advisor.luna.commission_the_mars_ship.complete";
        public const string CompleteCommissionTheBeltHauler = "advisor.mars.commission_the_belt_hauler.complete";

        private static readonly AdvisorToast[] Primary =
        {
            new AdvisorToast("advisor.earth.survey_the_claim.post", FlagDecreeIds.EarthSurveyTheClaim, AdvisorFireWhen.Post,
                "Chart the apron so the court knows where the orange disc ends and the meadow begins. Fog can wait. Guessing is undignified."),
            new AdvisorToast("advisor.earth.raise_the_commons.post", FlagDecreeIds.EarthRaiseTheCommons, AdvisorFireWhen.Post,
                "Raise the Commons first. A court that meets in the dirt is a picnic, not a reign. Place the civic, then post Build on the live order."),
            new AdvisorToast("advisor.earth.dock_the_first_hab.post", FlagDecreeIds.EarthDockTheFirstHab, AdvisorFireWhen.Post,
                "Dock the first HAB. Humans stay indoors; tax does not collect itself from a meadow. Anvil welds the airlock join — square socket, not a speech."),
            new AdvisorToast("advisor.earth.levy_the_meadow_farm.post", FlagDecreeIds.EarthLevyTheMeadowFarm, AdvisorFireWhen.Post,
                "Levy the meadow farm. ICE is life support, not a beverage program. Creepers will RSVP."),
            new AdvisorToast("advisor.earth.charter_the_hall.post", FlagDecreeIds.EarthCharterTheHall, AdvisorFireWhen.Post,
                "Charter the hall. A court without a guild is stationery. Field Survey, then Hab Ops, then the Charter — dock Horizon Lodge, Anvil Compact, Aegis Lodge, or Triage Compact."),
            new AdvisorToast("advisor.earth.seal_the_near_dens.post", FlagDecreeIds.EarthSealTheNearDens, AdvisorFireWhen.Post,
                "The court does not share the meadow. Three dens. Seal them before the creepers start charging rent."),
            new AdvisorToast("advisor.earth.ward_the_furrows.post", FlagDecreeIds.EarthWardTheFurrows, AdvisorFireWhen.Post,
                "Ward the furrows. Soil creepers chew farms. Aegis and Triage will hold a field cheaper than they will hunt a den. Tempt them accordingly."),
            new AdvisorToast("advisor.earth.stage_the_lunar_rocket.post", FlagDecreeIds.EarthStageTheLunarRocket, AdvisorFireWhen.Post,
                "Stage the Lunar Rocket. Pad labour is a Build on the order. Science is 70 + 40 MET + 15 ICE. A trajectory without a pad is a toast to vacuum."),

            new AdvisorToast("advisor.luna.chart_the_tariff_rille.post", FlagDecreeIds.LunaChartTheTariffRille, AdvisorFireWhen.Post,
                "Chart the rille. Horizon marks the lanes the Freeholds tax. Smugglers use the same lines. So will we."),
            new AdvisorToast("advisor.luna.weigh_the_freehold_ore.post", FlagDecreeIds.LunaWeighTheFreeholdOre, AdvisorFireWhen.Post,
                "They call it a tariff. We call it MET with a receipt. Weigh the Freehold ore. Strip and Core haul the levy."),
            new AdvisorToast("advisor.luna.ward_the_weigh_station.post", FlagDecreeIds.LunaWardTheWeighStation, AdvisorFireWhen.Post,
                "Ticks are stealing from the weigh-station. That is not a den hunt. That is a rim watch with worse lighting."),
            new AdvisorToast("advisor.luna.root_the_hopper_saboteurs.claim", FlagDecreeIds.LunaRootTheHopperSaboteurs, AdvisorFireWhen.Claim,
                "Someone cut the hoppers loose on the HAB. Post a warrant, not a sermon. Clear Threat. Ask later who paid them."),
            new AdvisorToast("advisor.luna.raise_the_crater_commons.post", FlagDecreeIds.LunaRaiseTheCraterCommons, AdvisorFireWhen.Post,
                "Raise a Commons on grey rock. Authority that sits in a crater still has to sit somewhere. Same civic gate as Earth. HUD still says COMMONS."),
            new AdvisorToast("advisor.luna.stake_the_far_rim.post", FlagDecreeIds.LunaStakeTheFarRim, AdvisorFireWhen.Post,
                "Stake the far rim. The cyan disc is Campus B, not a picnic site. Haul claims it. Freight after."),
            new AdvisorToast("advisor.luna.fortify_the_airlocks.post", FlagDecreeIds.LunaFortifyTheAirlocks, AdvisorFireWhen.Post,
                "Fortify the airlocks. Junction turrets are dressing — they look like courage. The labour is still a Build on a Battery or workshop order."),
            new AdvisorToast("advisor.luna.commission_the_mars_ship.post", FlagDecreeIds.LunaCommissionTheMarsShip, AdvisorFireWhen.Post,
                "Commission the Mars Ship. The Compact is not a rumor. It is 100 science, 80 MET, 30 ICE, 20 PWR, and a pad you still have to weld."),

            new AdvisorToast("advisor.mars.survey_the_red_apron.post", FlagDecreeIds.MarsSurveyTheRedApron, AdvisorFireWhen.Post,
                "Survey the red apron. Chart the packed-dust plaza the Compact will actually sit on — white hulls, square docks, a Commons that is not a rumor."),
            new AdvisorToast("advisor.mars.raise_the_compact_seat.post", FlagDecreeIds.MarsRaiseTheCompactSeat, AdvisorFireWhen.Post,
                "Raise the Compact seat. HUD still says COMMONS. The guild will forgive a Sol accent. It will not forgive a dirt court."),
            new AdvisorToast("advisor.mars.string_the_solar_field.post", FlagDecreeIds.MarsStringTheSolarField, AdvisorFireWhen.Post,
                "String the solar field. PWR-1 first. Wisps RSVP themselves. We do not send thank-you notes."),
            new AdvisorToast("advisor.mars.hunt_the_wisps.post", FlagDecreeIds.MarsHuntTheWisps, AdvisorFireWhen.Post,
                "Hunt the wisps. They drink the grid like it is complimentary. It is not. Pair with the ten dens when the plaza gets loud."),
            new AdvisorToast("advisor.mars.ward_the_dust_furrows.post", FlagDecreeIds.MarsWardTheDustFurrows, AdvisorFireWhen.Post,
                "Ward the dust furrows. Compact farms do not get abandoned for a prettier den hunt. Creepers chew; we hold."),
            new AdvisorToast("advisor.mars.stake_campus_b.post", FlagDecreeIds.MarsStakeCampusB, AdvisorFireWhen.Post,
                "Stake Campus B. A Compact forward lodge — not a Freehold weigh-station, not a Sol picnic. Cyan disc. Haul already knows."),
            new AdvisorToast("advisor.mars.weave_the_crust.post", FlagDecreeIds.MarsWeaveTheCrust, AdvisorFireWhen.Post,
                "They want a garden. Pay for a garden. Bloom weaves +0.08 and will invoice you for every grain."),
            new AdvisorToast("advisor.mars.commission_the_belt_hauler.post", FlagDecreeIds.MarsCommissionTheBeltHauler, AdvisorFireWhen.Post,
                "Commission the Belt Hauler — and place the pad. Science without a landing is a Compact joke we do not tell twice. The rocks are next. We are not writing their decrees today.")
        };

        private static readonly Dictionary<string, string> ClaimAsides = new Dictionary<string, string>
        {
            { FlagDecreeIds.EarthSurveyTheClaim, "Horizon took the cheap Explore. Forty credits and a disappearing act — classic." },
            { FlagDecreeIds.EarthRaiseTheCommons, "Anvil took the weld. Try not to look surprised." },
            { FlagDecreeIds.EarthDockTheFirstHab, "Beds on the books. The meadow just became a ledger." },
            { FlagDecreeIds.EarthLevyTheMeadowFarm, "Strip took the levy. Dens can wait — they said so themselves." },
            { FlagDecreeIds.EarthCharterTheHall, "Chart logged the site. The lab can eat the sample; the hall can eat the ego." },
            { FlagDecreeIds.EarthSealTheNearDens, "Aegis hunts. Stay behind the shield and out of the toast." },
            { FlagDecreeIds.EarthWardTheFurrows, "Rim watch on a farm. Very dignified. Very necessary." },
            { FlagDecreeIds.EarthStageTheLunarRocket, "Anvil will take a pad weld if you stop being cute with the bounty." },
            { FlagDecreeIds.LunaChartTheTariffRille, "Chart or Horizon took a tariff lane. Nobody fights a contour line. Good." },
            { FlagDecreeIds.LunaWeighTheFreeholdOre, "The receipt is in the stockpile. Do not lose it. The Freeholds will ask." },
            { FlagDecreeIds.LunaWardTheWeighStation, "Rim took the watch. The levy continues. Poetry later." },
            { FlagDecreeIds.LunaRootTheHopperSaboteurs, "Aegis accepted the warrant. The hoppers did not." },
            { FlagDecreeIds.LunaRaiseTheCraterCommons, "Anvil will weld a crater court if the MET is honest." },
            { FlagDecreeIds.LunaStakeTheFarRim, "Haul took the disc. They already said freight after. Believe them." },
            { FlagDecreeIds.LunaFortifyTheAirlocks, "Anvil fortifies. Aegis will pretend it was their idea." },
            { FlagDecreeIds.LunaCommissionTheMarsShip, "The lab ate the sample. The pad will eat Anvil’s afternoon." },
            { FlagDecreeIds.MarsSurveyTheRedApron, "Horizon priced the dust. Forty credits. They did not stay for the speech." },
            { FlagDecreeIds.MarsRaiseTheCompactSeat, "Anvil welds for the Compact now. Try a guild-proud bounty. They notice." },
            { FlagDecreeIds.MarsStringTheSolarField, "The grid is on the books. The wisps can read." },
            { FlagDecreeIds.MarsHuntTheWisps, "Aegis hunts light that should not have a mouth. Compact work." },
            { FlagDecreeIds.MarsWardTheDustFurrows, "Rim or Aegis on a greenhouse. The Compact keeps its lunch." },
            { FlagDecreeIds.MarsStakeCampusB, "Haul claimed the disc. Freight after. They have a slogan. It works." },
            { FlagDecreeIds.MarsWeaveTheCrust, "Bloom took the slow work. Worth it, they said. Check the yield before you argue." },
            { FlagDecreeIds.MarsCommissionTheBeltHauler, "The lab logged the haul. Anvil still wants a pad. Pay both." }
        };

        private static readonly Dictionary<string, AdvisorToast> Travel = new Dictionary<string, AdvisorToast>
        {
            { TravelEarthEmptyDrop, new AdvisorToast(TravelEarthEmptyDrop, null, AdvisorFireWhen.Travel,
                "The meadow is empty and the court is a rumor. Sol Authority does not hold court in the grass. Raise a Commons before anyone calls this a picnic.") },
            { TravelEarthToLuna, new AdvisorToast(TravelEarthToLuna, null, AdvisorFireWhen.Travel,
                "Earth holds. Lunar Rocket on the pad. Trajectory locked: Luna. The Freeholds already have a receipt prepared.") },
            { TravelLunaArrival, new AdvisorToast(TravelLunaArrival, null, AdvisorFireWhen.Travel,
                "Luna insertion. The Freeholds smile like a weigh-station. Dock fee is four MET. They call it hospitality. We call it a line item.") },
            { TravelLunaToMars, new AdvisorToast(TravelLunaToMars, null, AdvisorFireWhen.Travel,
                "Luna holds. Mars Ship staged. Next body: Mars. Pack guild manners. The Compact does not curtsy.") },
            { TravelMarsArrival, new AdvisorToast(TravelMarsArrival, null, AdvisorFireWhen.Travel,
                "Mars descent. Compact ground. Dust wisps already have opinions about Power. Raise the seat before the dust files a claim of its own.") },
            { TravelMarsToBelt, new AdvisorToast(TravelMarsToBelt, null, AdvisorFireWhen.Travel,
                "Mars holds. Belt Hauler on the pad. Trajectory into the rocks is open. Belt can wait for its own map.") },
            { LessonEarthGreedBuild, new AdvisorToast(LessonEarthGreedBuild, null, AdvisorFireWhen.Post,
                "Anvil will not weld for pocket change. Seventy is a suggestion; tempted is a fact. Raise the bounty until the chip admits it.") }
        };

        private static readonly Dictionary<string, AdvisorToast> Completes = new Dictionary<string, AdvisorToast>
        {
            { CompleteCharterTheHall, new AdvisorToast(CompleteCharterTheHall, FlagDecreeIds.EarthCharterTheHall, AdvisorFireWhen.Complete,
                "Guild Charter signed. Dock Horizon, Anvil, Aegis, or Triage. Flags near CMD-1 dress pull that ego.") },
            { CompleteStageTheLunarRocket, new AdvisorToast(CompleteStageTheLunarRocket, FlagDecreeIds.EarthStageTheLunarRocket, AdvisorFireWhen.Complete,
                "Lunar Rocket is on the pad. The meadow just became a departure lounge.") },
            { CompleteCommissionTheMarsShip, new AdvisorToast(CompleteCommissionTheMarsShip, FlagDecreeIds.LunaCommissionTheMarsShip, AdvisorFireWhen.Complete,
                "Mars Ship staged. The Compact will not send a welcome basket.") },
            { CompleteCommissionTheBeltHauler, new AdvisorToast(CompleteCommissionTheBeltHauler, FlagDecreeIds.MarsCommissionTheBeltHauler, AdvisorFireWhen.Complete,
                "Belt Hauler staged. The rocks are a body. They are not this week’s copy.") }
        };

        public static IReadOnlyList<AdvisorToast> AllPrimary => Primary;

        public static bool TryGetPrimary(string decreeId, out AdvisorToast toast)
        {
            if (!string.IsNullOrEmpty(decreeId))
            {
                for (int i = 0; i < Primary.Length; i++)
                {
                    if (Primary[i].DecreeId == decreeId)
                    {
                        toast = Primary[i];
                        return true;
                    }
                }
            }

            toast = default;
            return false;
        }

        public static bool TryGetClaimAside(string decreeId, out string line)
        {
            if (!string.IsNullOrEmpty(decreeId) && ClaimAsides.TryGetValue(decreeId, out line))
                return true;
            line = null;
            return false;
        }

        public static bool TryGetTravel(string key, out AdvisorToast toast)
        {
            if (!string.IsNullOrEmpty(key) && Travel.TryGetValue(key, out toast))
                return true;
            toast = default;
            return false;
        }

        public static bool TryGetComplete(string key, out AdvisorToast toast)
        {
            if (!string.IsNullOrEmpty(key) && Completes.TryGetValue(key, out toast))
                return true;
            toast = default;
            return false;
        }

        public static string TravelKeyForArrival(CelestialBodyId body)
        {
            switch (body)
            {
                case CelestialBodyId.Earth: return TravelEarthEmptyDrop;
                case CelestialBodyId.Luna: return TravelLunaArrival;
                case CelestialBodyId.Mars: return TravelMarsArrival;
                default: return null;
            }
        }

        public static string TravelKeyForVictory(CelestialBodyId body)
        {
            switch (body)
            {
                case CelestialBodyId.Earth: return TravelEarthToLuna;
                case CelestialBodyId.Luna: return TravelLunaToMars;
                case CelestialBodyId.Mars: return TravelMarsToBelt;
                default: return null;
            }
        }

        public static string CompleteKeyForCraft(CelestialBodyId body)
        {
            switch (body)
            {
                case CelestialBodyId.Earth: return CompleteStageTheLunarRocket;
                case CelestialBodyId.Luna: return CompleteCommissionTheMarsShip;
                case CelestialBodyId.Mars: return CompleteCommissionTheBeltHauler;
                default: return null;
            }
        }
    }
}
