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
                "Chart the apron so we know where the orange disc ends and the meadow begins. Fog can wait. Guessing costs EU."),
            new AdvisorToast("advisor.earth.raise_the_commons.post", FlagDecreeIds.EarthRaiseTheCommons, AdvisorFireWhen.Post,
                "Raise the Commons first. Everything else hangs off it. Place it, then post Build on the live order."),
            new AdvisorToast("advisor.earth.dock_the_first_hab.post", FlagDecreeIds.EarthDockTheFirstHab, AdvisorFireWhen.Post,
                "Place the first charging station. Bots charge there and it fills a battery with EU. Couriers carry it to the till."),
            new AdvisorToast("advisor.earth.levy_the_meadow_farm.post", FlagDecreeIds.EarthLevyTheMeadowFarm, AdvisorFireWhen.Post,
                "Run the meadow farm. It pays EU. Creepers will show up for it."),
            new AdvisorToast("advisor.earth.charter_the_hall.post", FlagDecreeIds.EarthCharterTheHall, AdvisorFireWhen.Post,
                "Charter the hall. Field Survey, then Charger Ops, then the Charter. Then dock Horizon Lodge, Anvil Lodge, Aegis Lodge, or Triage Lodge."),
            new AdvisorToast("advisor.earth.seal_the_near_dens.post", FlagDecreeIds.EarthSealTheNearDens, AdvisorFireWhen.Post,
                "Three dens near the yard. Seal them before the creepers move in."),
            new AdvisorToast("advisor.earth.ward_the_furrows.post", FlagDecreeIds.EarthWardTheFurrows, AdvisorFireWhen.Post,
                "Ward the furrows. Soil creepers chew farms. Aegis and Triage will hold a field cheaper than they will hunt a den. Tempt them accordingly."),
            new AdvisorToast("advisor.earth.stage_the_lunar_rocket.post", FlagDecreeIds.EarthStageTheLunarRocket, AdvisorFireWhen.Post,
                "Stage the Colony Lifter. Pad labour is a Build on the order. Research is 70 science + 550 EU. No pad, no launch."),

            new AdvisorToast("advisor.luna.chart_the_tariff_rille.post", FlagDecreeIds.LunaChartTheTariffRille, AdvisorFireWhen.Post,
                "Chart the rille. Horizon marks the ore lanes. We will use them."),
            new AdvisorToast("advisor.luna.weigh_the_freehold_ore.post", FlagDecreeIds.LunaWeighTheFreeholdOre, AdvisorFireWhen.Post,
                "Mine the rille ore. It pays EU into the mine's battery. Courier bots haul it to the till."),
            new AdvisorToast("advisor.luna.ward_the_weigh_station.post", FlagDecreeIds.LunaWardTheWeighStation, AdvisorFireWhen.Post,
                "Ticks are stealing from the mines. That is not a den hunt. Post Defend and hold the spot."),
            new AdvisorToast("advisor.luna.root_the_hopper_saboteurs.claim", FlagDecreeIds.LunaRootTheHopperSaboteurs, AdvisorFireWhen.Claim,
                "Ash hoppers are on the charging stations. Post Clear Threat. Ask questions later."),
            new AdvisorToast("advisor.luna.raise_the_crater_commons.post", FlagDecreeIds.LunaRaiseTheCraterCommons, AdvisorFireWhen.Post,
                "Raise a Commons on grey rock. Same gate as Earth. The HUD calls it COMMONS."),
            new AdvisorToast("advisor.luna.stake_the_far_rim.post", FlagDecreeIds.LunaStakeTheFarRim, AdvisorFireWhen.Post,
                "Stake the far rim. Yard B is the cyan dot on the minimap. A courier claims it. Freight after."),
            new AdvisorToast("advisor.luna.fortify_the_perimeter.post", FlagDecreeIds.LunaFortifyThePerimeter, AdvisorFireWhen.Post,
                "Fortify the perimeter. Laser turrets look brave, but the labour is still a Build on a turret or workshop order."),
            new AdvisorToast("advisor.luna.commission_the_mars_ship.post", FlagDecreeIds.LunaCommissionTheMarsShip, AdvisorFireWhen.Post,
                "Commission the Colony Lifter Mk II. It is 100 science and 1,100 EU, plus a pad you still have to weld."),

            new AdvisorToast("advisor.mars.survey_the_red_apron.post", FlagDecreeIds.MarsSurveyTheRedApron, AdvisorFireWhen.Post,
                "Survey the red apron. Chart the packed dust where the Commons will sit."),
            new AdvisorToast("advisor.mars.raise_the_compact_seat.post", FlagDecreeIds.MarsRaiseTheCompactSeat, AdvisorFireWhen.Post,
                "Raise the Mars Commons. The HUD calls it COMMONS. Nothing else works until it stands."),
            new AdvisorToast("advisor.mars.string_the_solar_field.post", FlagDecreeIds.MarsStringTheSolarField, AdvisorFireWhen.Post,
                "Lay out the solar field. It pays EU. Wisps will come for it."),
            new AdvisorToast("advisor.mars.hunt_the_wisps.post", FlagDecreeIds.MarsHuntTheWisps, AdvisorFireWhen.Post,
                "Hunt the wisps. They raid the solar farms. Pair it with the dens when the yard gets loud."),
            new AdvisorToast("advisor.mars.ward_the_dust_furrows.post", FlagDecreeIds.MarsWardTheDustFurrows, AdvisorFireWhen.Post,
                "Ward the dust furrows. Do not drop the farms for a prettier den hunt. Creepers chew; we hold."),
            new AdvisorToast("advisor.mars.stake_campus_b.post", FlagDecreeIds.MarsStakeCampusB, AdvisorFireWhen.Post,
                "Stake Yard B, the cyan dot on the minimap. A courier claims it. They already know the way."),
            new AdvisorToast("advisor.mars.weave_the_crust.post", FlagDecreeIds.MarsWeaveTheCrust, AdvisorFireWhen.Post,
                "They want a garden. Pay for a garden. Bloom weaves +0.08 and will invoice you for every grain."),
            new AdvisorToast("advisor.mars.commission_the_belt_hauler.post", FlagDecreeIds.MarsCommissionTheBeltHauler, AdvisorFireWhen.Post,
                "Commission the Colony Lifter Mk III, and place the pad. Research without a pad goes nowhere. This is the last launch of the campaign.")
        };

        private static readonly Dictionary<string, string> ClaimAsides = new Dictionary<string, string>
        {
            { FlagDecreeIds.EarthSurveyTheClaim, "Horizon took the Explore and vanished into the fog. Classic." },
            { FlagDecreeIds.EarthRaiseTheCommons, "Anvil took the weld. Try not to look surprised." },
            { FlagDecreeIds.EarthDockTheFirstHab, "Charging station on the books. Its battery starts filling." },
            { FlagDecreeIds.EarthLevyTheMeadowFarm, "Strip took the farm. Dens can wait — they said so themselves." },
            { FlagDecreeIds.EarthCharterTheHall, "Chart logged the site. The lab can eat the sample; the hall can eat the ego." },
            { FlagDecreeIds.EarthSealTheNearDens, "Aegis hunts. Stay behind the shield and out of the toast." },
            { FlagDecreeIds.EarthWardTheFurrows, "Rim watch on a farm. Very dignified. Very necessary." },
            { FlagDecreeIds.EarthStageTheLunarRocket, "Anvil will take a pad weld if you stop being cute with the bounty." },
            { FlagDecreeIds.LunaChartTheTariffRille, "Chart or Horizon took a tariff lane. Nobody fights a contour line. Good." },
            { FlagDecreeIds.LunaWeighTheFreeholdOre, "The ore is on the books. Couriers will haul the EU." },
            { FlagDecreeIds.LunaWardTheWeighStation, "Rim took the watch. The mines keep paying." },
            { FlagDecreeIds.LunaRootTheHopperSaboteurs, "Aegis accepted the warrant. The hoppers did not." },
            { FlagDecreeIds.LunaRaiseTheCraterCommons, "Anvil will weld a crater Commons if the EU is honest." },
            { FlagDecreeIds.LunaStakeTheFarRim, "A courier took Yard B. They already said freight after. Believe them." },
            { FlagDecreeIds.LunaFortifyThePerimeter, "Anvil fortifies. Aegis will pretend it was their idea." },
            { FlagDecreeIds.LunaCommissionTheMarsShip, "The lab ate the sample. The pad will eat Anvil’s afternoon." },
            { FlagDecreeIds.MarsSurveyTheRedApron, "Horizon took the dust. They did not stay for the speech." },
            { FlagDecreeIds.MarsRaiseTheCompactSeat, "Anvil took the weld. Try a guild-proud bounty. They notice." },
            { FlagDecreeIds.MarsStringTheSolarField, "The solar field is on the books. The wisps noticed." },
            { FlagDecreeIds.MarsHuntTheWisps, "Aegis hunts light that should not have a mouth. Good work." },
            { FlagDecreeIds.MarsWardTheDustFurrows, "Rim or Aegis on a greenhouse. The farm keeps paying." },
            { FlagDecreeIds.MarsStakeCampusB, "A courier claimed Yard B. Freight after. They have a slogan. It works." },
            { FlagDecreeIds.MarsWeaveTheCrust, "Bloom took the slow work. Worth it, they said. Check the yield before you argue." },
            { FlagDecreeIds.MarsCommissionTheBeltHauler, "The lab logged the haul. Anvil still wants a pad. Pay both." }
        };

        private static readonly Dictionary<string, AdvisorToast> Travel = new Dictionary<string, AdvisorToast>
        {
            { TravelEarthEmptyDrop, new AdvisorToast(TravelEarthEmptyDrop, null, AdvisorFireWhen.Travel,
                "The meadow is empty. Raise a Commons. Nothing works until it stands.") },
            { TravelEarthToLuna, new AdvisorToast(TravelEarthToLuna, null, AdvisorFireWhen.Travel,
                "Earth holds. The Colony Lifter is on the pad. Next stop: Luna.") },
            { TravelLunaArrival, new AdvisorToast(TravelLunaArrival, null, AdvisorFireWhen.Travel,
                "Luna insertion. Grey rock, thin margins. Raise a Commons first.") },
            { TravelLunaToMars, new AdvisorToast(TravelLunaToMars, null, AdvisorFireWhen.Travel,
                "Luna holds. The Colony Lifter Mk II is staged. Next stop: Mars.") },
            { TravelMarsArrival, new AdvisorToast(TravelMarsArrival, null, AdvisorFireWhen.Travel,
                "Mars descent. Dust wisps raid the solar farms. Raise the Commons first.") },
            { TravelMarsToBelt, new AdvisorToast(TravelMarsToBelt, null, AdvisorFireWhen.Travel,
                "Mars holds. The Colony Lifter Mk III is on the pad. Earth, Luna and Mars are done.") },
            { LessonEarthGreedBuild, new AdvisorToast(LessonEarthGreedBuild, null, AdvisorFireWhen.Post,
                "Anvil will not weld for pocket change. Seven hundred is a suggestion; tempted is a fact. Raise the bounty until the chip says so.") }
        };

        private static readonly Dictionary<string, AdvisorToast> Completes = new Dictionary<string, AdvisorToast>
        {
            { CompleteCharterTheHall, new AdvisorToast(CompleteCharterTheHall, FlagDecreeIds.EarthCharterTheHall, AdvisorFireWhen.Complete,
                "Guild Charter signed. Dock Horizon, Anvil, Aegis, or Triage. Flags near a hall pull that class.") },
            { CompleteStageTheLunarRocket, new AdvisorToast(CompleteStageTheLunarRocket, FlagDecreeIds.EarthStageTheLunarRocket, AdvisorFireWhen.Complete,
                "The Colony Lifter is on the pad. The meadow just became a departure lounge.") },
            { CompleteCommissionTheMarsShip, new AdvisorToast(CompleteCommissionTheMarsShip, FlagDecreeIds.LunaCommissionTheMarsShip, AdvisorFireWhen.Complete,
                "The Colony Lifter Mk II is staged. Mars will not send a welcome basket.") },
            { CompleteCommissionTheBeltHauler, new AdvisorToast(CompleteCommissionTheBeltHauler, FlagDecreeIds.MarsCommissionTheBeltHauler, AdvisorFireWhen.Complete,
                "The Colony Lifter Mk III is staged. That closes the campaign.") }
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
