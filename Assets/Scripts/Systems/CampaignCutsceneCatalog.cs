// W2 campaign-stakes cutscenes — source of truth: Docs/W2_CAMPAIGN_STAKES.md
// cut.* keys are localization handles. Decree ids stay FlagDecreeIds consts.

using System.Collections.Generic;

namespace SolarMajesty
{
    public enum CutsceneKind
    {
        Modal = 0,
        TravelLog = 1
    }

    public readonly struct CampaignCutscene
    {
        public readonly string Id;
        public readonly string Title;
        public readonly string[] Body;
        public readonly CutsceneKind Kind;

        public CampaignCutscene(string id, string title, CutsceneKind kind, params string[] body)
        {
            Id = id;
            Title = title;
            Kind = kind;
            Body = body ?? System.Array.Empty<string>();
        }

        /// <summary>Single travel-log / VictoryLog paragraph. Does not invent prose.</summary>
        public string LogParagraph
        {
            get
            {
                if (Body == null || Body.Length == 0) return Title ?? "";
                var parts = new List<string>(Body.Length);
                for (int i = 0; i < Body.Length; i++)
                {
                    if (!string.IsNullOrEmpty(Body[i]))
                        parts.Add(Body[i].Trim());
                }
                return parts.Count == 0 ? (Title ?? "") : string.Join(" ", parts.ToArray());
            }
        }
    }

    /// <summary>Nine W2 text beats. Belt / Europa stay catalog-only.</summary>
    public static class CampaignCutsceneCatalog
    {
        public const string EarthPrologue = "cut.earth.prologue";
        public const string EarthMidCourt = "cut.earth.mid_court";
        public const string EarthToLuna = "cut.earth.to_luna";
        public const string LunaArrival = "cut.luna.arrival";
        public const string LunaMidWarrant = "cut.luna.mid_warrant";
        public const string LunaToMars = "cut.luna.to_mars";
        public const string MarsArrival = "cut.mars.arrival";
        public const string MarsMidCrust = "cut.mars.mid_crust";
        public const string MarsBeltNamed = "cut.mars.belt_named";

        private static readonly CampaignCutscene[] Table =
        {
            new CampaignCutscene(EarthPrologue, "The Meadow Is Not a Reign", CutsceneKind.Modal,
                "The cradle is a closing book. Sol Authority still stamps decrees; the meadow does not stamp them back.",
                "A claim disc in the grass is a picnic. Raise a Commons before anyone files us as scenery.",
                "Horizon can chart the apron. Anvil can weld. Neither makes the green infinite.",
                "We stay here, we become tenants of a farm. We leave, we remain a crown."),

            new CampaignCutscene(EarthMidCourt, "Court, Beds, Charter", CutsceneKind.Modal,
                "The Commons stands. The HAB is a ledger. The hall has a charter.",
                "That is a court. It is not a solar power.",
                "ICE and REG have a ceiling on this meadow. Eight souls and a farm do not impress anyone who already owns a lane.",
                "Sol Authority’s legitimacy is expansion. Farms are how we eat. They are not why we rule."),

            new CampaignCutscene(EarthToLuna, "Trajectory Locked — Luna", CutsceneKind.TravelLog,
                "Earth holds. The Lunar Rocket is on the pad, not in a toast.",
                "The Freeholds already have a receipt prepared. Four MET at the dock. They call it hospitality.",
                "We call it the first bill of a longer reign.",
                "Do not wave at the trajectory. They will invoice the wave."),

            new CampaignCutscene(LunaArrival, "Hospitality, Itemized", CutsceneKind.Modal,
                "Luna insertion. The Freeholds smile like a weigh-station.",
                "Dock fee is four MET. Ice and ore have owners. The lanes have owners.",
                "Ash hoppers will test the HAB. Dust ticks will test the mines. Neither is the real tax.",
                "The real tax is staying. Stay, and you pay forever."),

            new CampaignCutscene(LunaMidWarrant, "Receipt and Warrant", CutsceneKind.Modal,
                "They call it a tariff. Strip and Core hauled the receipt. Do not lose it.",
                "Someone cut the hoppers loose on the HAB. Aegis takes warrants, not sermons.",
                "The lanes were spoken for before we arrived. Buy them with MET, or leave them behind.",
                "A crater Commons that pays rent is a nicer picnic. We did not come for nicer."),

            new CampaignCutscene(LunaToMars, "Trajectory Locked — Mars", CutsceneKind.TravelLog,
                "Luna holds. Mars Ship staged. Pack guild manners.",
                "The Compact does not curtsy. It charters.",
                "We have paid the Freehold tithe long enough to learn the lesson: ice and ore without a seat is someone else’s spine.",
                "The red campus is where a crown becomes industrial — or becomes a story about a meadow."),

            new CampaignCutscene(MarsArrival, "Compact Ground", CutsceneKind.Modal,
                "Mars descent. Compact ground. Dust already filing opinions about Power.",
                "Raise the seat — HUD still says COMMONS — before the dust files a claim of its own.",
                "This is the industrial spine. Not a second Earth court. Not a Freehold weigh-station with worse lighting.",
                "Wisps drink grids. Creepers chew furrows. The Compact expects both handled without a Sol accent."),

            new CampaignCutscene(MarsMidCrust, "Grid, Wisps, Garden", CutsceneKind.Modal,
                "The solar field is on the books. The wisps can read. Aegis hunts light that should not have a mouth.",
                "Bloom will weave the crust if you pay for a garden. The Compact creed is not a picnic blanket.",
                "A stronghold that cannot keep Power or lunch is a failed picnic with better dust.",
                "Hold the campus. Then we name the rocks. We do not write their decrees today."),

            new CampaignCutscene(MarsBeltNamed, "The Rocks Have a Name", CutsceneKind.TravelLog,
                "Mars holds. Belt Hauler on the pad. Trajectory into the rocks is open.",
                "Belt is the next freight frontier. It is a name. It is not this week’s map.",
                "Europa can wait in the catalog. We are not writing their decrees.",
                "The crown is a solar power, or it was a meadow with a rocket. Choose which log we keep.")
        };

        public static IReadOnlyList<CampaignCutscene> All => Table;

        public static bool TryGet(string id, out CampaignCutscene cut)
        {
            if (!string.IsNullOrEmpty(id))
            {
                for (int i = 0; i < Table.Length; i++)
                {
                    if (Table[i].Id == id)
                    {
                        cut = Table[i];
                        return true;
                    }
                }
            }

            cut = default;
            return false;
        }

        public static string ArrivalKey(CelestialBodyId body)
        {
            switch (body)
            {
                case CelestialBodyId.Earth: return EarthPrologue;
                case CelestialBodyId.Luna: return LunaArrival;
                case CelestialBodyId.Mars: return MarsArrival;
                default: return null;
            }
        }

        public static string VictoryKey(CelestialBodyId body)
        {
            switch (body)
            {
                case CelestialBodyId.Earth: return EarthToLuna;
                case CelestialBodyId.Luna: return LunaToMars;
                case CelestialBodyId.Mars: return MarsBeltNamed;
                default: return null;
            }
        }

        public static bool TryGetArrival(CelestialBodyId body, out CampaignCutscene cut) =>
            TryGet(ArrivalKey(body), out cut);

        public static bool TryGetVictory(CelestialBodyId body, out CampaignCutscene cut) =>
            TryGet(VictoryKey(body), out cut);
    }
}
