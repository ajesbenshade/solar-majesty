// Shared Earth→Luna→Mars decree ids for Narrative + LP.
// Maps onto existing FlagType. Not a quest graph.
// See Docs/FLAG_TYPE_MAP_EARTH_LUNA_MARS.md.

using System.Collections.Generic;

namespace SolarMajesty
{
    /// <summary>
    /// One authored decree title. <see cref="FlagManager"/> still posts <see cref="FlagData"/> by type.
    /// </summary>
    public readonly struct FlagDecree
    {
        public readonly string Id;
        public readonly CelestialBodyId Body;
        public readonly FlagType Type;
        public readonly string Title;
        public readonly string Intent;

        public FlagDecree(
            string id,
            CelestialBodyId body,
            FlagType type,
            string title,
            string intent)
        {
            Id = id;
            Body = body;
            Type = type;
            Title = title;
            Intent = intent;
        }
    }

    /// <summary>
    /// Stable string ids Narrative can write advisor lines against.
    /// Convention: {body}.{typetoken}.{slug} — see TypeToken.
    /// </summary>
    public static class FlagDecreeIds
    {
        public const string EarthSurveyTheClaim = "earth.explore.survey_the_claim";
        public const string EarthRaiseTheCommons = "earth.build.raise_the_commons";
        public const string EarthDockTheFirstHab = "earth.build.dock_the_first_hab";
        public const string EarthLevyTheMeadowFarm = "earth.extract.levy_the_meadow_farm";
        public const string EarthWardTheFurrows = "earth.defendarea.ward_the_furrows";
        public const string EarthSealTheNearDens = "earth.clearthreat.seal_the_near_dens";
        public const string EarthCharterTheHall = "earth.researchsite.charter_the_hall";
        public const string EarthStageTheLunarRocket = "earth.build.stage_the_lunar_rocket";

        public const string LunaChartTheTariffRille = "luna.explore.chart_the_tariff_rille";
        public const string LunaWeighTheFreeholdOre = "luna.extract.weigh_the_freehold_ore";
        public const string LunaWardTheWeighStation = "luna.defendarea.ward_the_weigh_station";
        public const string LunaRootTheHopperSaboteurs = "luna.clearthreat.root_the_hopper_saboteurs";
        public const string LunaRaiseTheCraterCommons = "luna.build.raise_the_crater_commons";
        public const string LunaStakeTheFarRim = "luna.establishoutpost.stake_the_far_rim";
        public const string LunaCommissionTheMarsShip = "luna.researchsite.commission_the_mars_ship";
        public const string LunaFortifyTheAirlocks = "luna.build.fortify_the_airlocks";

        public const string MarsSurveyTheRedApron = "mars.explore.survey_the_red_apron";
        public const string MarsRaiseTheCompactSeat = "mars.build.raise_the_compact_seat";
        public const string MarsStringTheSolarField = "mars.build.string_the_solar_field";
        public const string MarsWardTheDustFurrows = "mars.defendarea.ward_the_dust_furrows";
        public const string MarsHuntTheWisps = "mars.clearthreat.hunt_the_wisps";
        public const string MarsStakeCampusB = "mars.establishoutpost.stake_campus_b";
        public const string MarsWeaveTheCrust = "mars.terraform.weave_the_crust";
        public const string MarsCommissionTheBeltHauler = "mars.researchsite.commission_the_belt_hauler";

        private static readonly FlagDecree[] Table =
        {
            new FlagDecree(EarthSurveyTheClaim, CelestialBodyId.Earth, FlagType.Explore,
                "Survey the Claim", "Horizon maps the meadow so the court knows the apron."),
            new FlagDecree(EarthRaiseTheCommons, CelestialBodyId.Earth, FlagType.Build,
                "Raise the Commons", "First civic landmark; tutorial court opens."),
            new FlagDecree(EarthDockTheFirstHab, CelestialBodyId.Earth, FlagType.Build,
                "Dock the First HAB", "Beds and tax. Humans stay indoors."),
            new FlagDecree(EarthLevyTheMeadowFarm, CelestialBodyId.Earth, FlagType.Extract,
                "Levy the Meadow Farm", "ICE for life support. Creepers follow the harvest."),
            new FlagDecree(EarthWardTheFurrows, CelestialBodyId.Earth, FlagType.DefendArea,
                "Ward the Furrows", "Hold farm pests without a hunt."),
            new FlagDecree(EarthSealTheNearDens, CelestialBodyId.Earth, FlagType.ClearThreat,
                "Seal the Near Dens", "Combat gate — three dens."),
            new FlagDecree(EarthCharterTheHall, CelestialBodyId.Earth, FlagType.ResearchSite,
                "Charter the Hall", "Science toward Guild Charter, then assign a class."),
            new FlagDecree(EarthStageTheLunarRocket, CelestialBodyId.Earth, FlagType.Build,
                "Stage the Lunar Rocket", "Pad labour. Pair with Lunar Rocket research."),

            new FlagDecree(LunaChartTheTariffRille, CelestialBodyId.Luna, FlagType.Explore,
                "Chart the Tariff Rille", "Mark the crater lanes the Freeholds tax."),
            new FlagDecree(LunaWeighTheFreeholdOre, CelestialBodyId.Luna, FlagType.Extract,
                "Weigh the Freehold Ore", "Haul the levy. They call it a tariff."),
            new FlagDecree(LunaWardTheWeighStation, CelestialBodyId.Luna, FlagType.DefendArea,
                "Ward the Weigh-Station", "Ticks steal from mines. Rim watch."),
            new FlagDecree(LunaRootTheHopperSaboteurs, CelestialBodyId.Luna, FlagType.ClearThreat,
                "Root the Hopper Saboteurs", "Ash hoppers on the HAB — sabotage flavor."),
            new FlagDecree(LunaRaiseTheCraterCommons, CelestialBodyId.Luna, FlagType.Build,
                "Raise the Crater Commons", "Authority plaza on grey rock."),
            new FlagDecree(LunaStakeTheFarRim, CelestialBodyId.Luna, FlagType.EstablishOutpost,
                "Stake the Far Rim", "Courier claims Campus B."),
            new FlagDecree(LunaCommissionTheMarsShip, CelestialBodyId.Luna, FlagType.ResearchSite,
                "Commission the Mars Ship", "Science toward Mars Ship."),
            new FlagDecree(LunaFortifyTheAirlocks, CelestialBodyId.Luna, FlagType.Build,
                "Fortify the Airlocks", "Defense Battery / workshop labour."),

            new FlagDecree(MarsSurveyTheRedApron, CelestialBodyId.Mars, FlagType.Explore,
                "Survey the Red Apron", "Chart the packed-dust plaza."),
            new FlagDecree(MarsRaiseTheCompactSeat, CelestialBodyId.Mars, FlagType.Build,
                "Raise the Compact Seat", "Colony Commons as Ares civic."),
            new FlagDecree(MarsStringTheSolarField, CelestialBodyId.Mars, FlagType.Build,
                "String the Solar Field", "PWR-1 landmark labour. Wisps will come."),
            new FlagDecree(MarsWardTheDustFurrows, CelestialBodyId.Mars, FlagType.DefendArea,
                "Ward the Dust Furrows", "Creepers chew Compact farms."),
            new FlagDecree(MarsHuntTheWisps, CelestialBodyId.Mars, FlagType.ClearThreat,
                "Hunt the Wisps", "Drain on Power. Pair with dens."),
            new FlagDecree(MarsStakeCampusB, CelestialBodyId.Mars, FlagType.EstablishOutpost,
                "Stake Campus B", "Cyan disc — Compact forward lodge."),
            new FlagDecree(MarsWeaveTheCrust, CelestialBodyId.Mars, FlagType.Terraform,
                "Weave the Crust", "Bloom slow farm-yield bump."),
            new FlagDecree(MarsCommissionTheBeltHauler, CelestialBodyId.Mars, FlagType.ResearchSite,
                "Commission the Belt Hauler", "Science toward Belt Hauler; still need a pad.")
        };

        public static IReadOnlyList<FlagDecree> All => Table;

        /// <summary>Id path token for a mechanical type. Kept stable for Narrative scripts.</summary>
        public static string TypeToken(FlagType type)
        {
            switch (type)
            {
                case FlagType.Explore: return "explore";
                case FlagType.ClearThreat: return "clearthreat";
                case FlagType.Build: return "build";
                case FlagType.Extract: return "extract";
                case FlagType.DefendArea: return "defendarea";
                case FlagType.ResearchSite: return "researchsite";
                case FlagType.EstablishOutpost: return "establishoutpost";
                case FlagType.Terraform: return "terraform";
                default: return "flag";
            }
        }

        public static string BodyToken(CelestialBodyId body)
        {
            switch (body)
            {
                case CelestialBodyId.Earth: return "earth";
                case CelestialBodyId.Luna: return "luna";
                case CelestialBodyId.Mars: return "mars";
                case CelestialBodyId.Belt: return "belt";
                case CelestialBodyId.Europa: return "europa";
                default: return "body";
            }
        }

        public static bool TryGet(string id, out FlagDecree decree)
        {
            if (!string.IsNullOrEmpty(id))
            {
                for (int i = 0; i < Table.Length; i++)
                {
                    if (Table[i].Id == id)
                    {
                        decree = Table[i];
                        return true;
                    }
                }
            }

            decree = default;
            return false;
        }

        /// <summary>
        /// Title + body match until a decree id is stamped on <see cref="FlagHandle"/>.
        /// Case-insensitive; trims. Does not invent slugs.
        /// </summary>
        public static bool TryMatchTitle(string title, CelestialBodyId body, out FlagDecree decree)
        {
            if (!string.IsNullOrEmpty(title))
            {
                for (int i = 0; i < Table.Length; i++)
                {
                    if (Table[i].Body != body) continue;
                    if (string.Equals(Table[i].Title, title.Trim(), System.StringComparison.OrdinalIgnoreCase))
                    {
                        decree = Table[i];
                        return true;
                    }
                }
            }

            decree = default;
            return false;
        }

        /// <summary>
        /// Posted-flag hook: title + body first, then the unique type on that body.
        /// Ambiguous types (multiple Builds) stay unmatched unless the title hits.
        /// </summary>
        public static bool TryMatchPosted(FlagType type, string title, CelestialBodyId body, out FlagDecree decree)
        {
            if (TryMatchTitle(title, body, out decree))
                return true;

            int hit = -1;
            int count = 0;
            for (int i = 0; i < Table.Length; i++)
            {
                if (Table[i].Body != body || Table[i].Type != type) continue;
                hit = i;
                count++;
                if (count > 1) break;
            }

            if (count == 1)
            {
                decree = Table[hit];
                return true;
            }

            decree = default;
            return false;
        }

        public static List<FlagDecree> ForBody(CelestialBodyId body)
        {
            var list = new List<FlagDecree>(8);
            for (int i = 0; i < Table.Length; i++)
            {
                if (Table[i].Body == body)
                    list.Add(Table[i]);
            }

            return list;
        }
    }
}
