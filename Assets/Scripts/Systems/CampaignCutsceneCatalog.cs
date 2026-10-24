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

    /// <summary>Nine W2 text beats for Earth, Luna and Mars. The campaign ends on Mars.</summary>
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
            new CampaignCutscene(EarthPrologue, "The Core Wakes", CutsceneKind.Modal,
                "You are the Core. Your bots do the work.",
                "The orange disc is the claim. Raise a Commons on it first.",
                "Horizon can chart the apron. Anvil can weld. You post the flags and pay the bounties.",
                "Earth is the first stop. Luna and Mars come after."),

            new CampaignCutscene(EarthMidCourt, "Commons, Chargers, Charter", CutsceneKind.Modal,
                "The Commons stands. The charging stations fill their batteries. The hall has a charter.",
                "Couriers carry the EU to the till.",
                "A meadow only pays so much. Eight bots and a farm are a start.",
                "Clear the dens, fill the till, and stage the Colony Lifter."),

            new CampaignCutscene(EarthToLuna, "Trajectory Locked — Luna", CutsceneKind.TravelLog,
                "Earth holds. The Colony Lifter is on the pad.",
                "Luna is grey rock and thin margins.",
                "The bots that ride up start over on grey rock.",
                "Next stop: Luna."),

            new CampaignCutscene(LunaArrival, "Hospitality, Itemized", CutsceneKind.Modal,
                "Luna insertion. Grey rock, thin margins.",
                "No supply ships land here. The mines pay the bills.",
                "Ash hoppers will hit the charging stations. Dust ticks will hit the mines.",
                "Raise a Commons, then keep the till ahead of the fees."),

            new CampaignCutscene(LunaMidWarrant, "Ore and Hoppers", CutsceneKind.Modal,
                "The rille ore is paying. Courier bots haul it to the till.",
                "Ash hoppers are on the charging stations. Aegis takes Clear Threat flags.",
                "Pay the bounty and the bots go. Underpay and they wait.",
                "Hold the crater, then stage the next lifter."),

            new CampaignCutscene(LunaToMars, "Trajectory Locked — Mars", CutsceneKind.TravelLog,
                "Luna holds. The Colony Lifter Mk II is staged.",
                "Mars is the last stop of the campaign.",
                "More dens, more wisps, a bigger till to fill.",
                "Next stop: Mars."),

            new CampaignCutscene(MarsArrival, "Red Ground", CutsceneKind.Modal,
                "Mars descent. Red dust and busy dens.",
                "Raise the Commons first. The HUD calls it COMMONS.",
                "This is the big yard. Clear the dens and fill the till.",
                "Wisps raid the solar farms. Creepers chew the furrows. Post flags for both."),

            new CampaignCutscene(MarsMidCrust, "Solar, Wisps, Garden", CutsceneKind.Modal,
                "The solar field is paying. The wisps noticed. Aegis hunts them.",
                "Bloom will weave the crust if you pay for a garden.",
                "A yard that cannot keep its farms running cannot keep its bots.",
                "Hold the yard. Then stage the last lifter."),

            new CampaignCutscene(MarsBeltNamed, "Campaign Complete", CutsceneKind.TravelLog,
                "Mars holds. The Colony Lifter Mk III is on the pad.",
                "Earth, Luna and Mars are running.",
                "Mars is the last stop for now.",
                "The bots keep working. Revisit any world from the title.")
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
