namespace SolarMajesty
{
    /// <summary>Scripted Grok beats. Catalog only — never a live LLM.</summary>
    public enum GrokBeat
    {
        Drop = 0,
        FirstHab = 1,
        FirstFlag = 2,
        GreedAsk = 3,
        LevyWalk = 4,
        PurseSitting = 5,
        YardBill = 6,
        TankVsWallet = 7,
        Refusal = 8,
        IceCritical = 9,
        PowerShort = 10,
        PurseStolen = 11,
        YardUnaffordable = 12,
        EmptyRoster = 13,
        MarketPayout = 14,
        MarketBlocked = 15,
        StalkerSiphon = 16
    }

    /// <summary>
    /// Grok (SpaceXAI) advisor copy from Docs/MARS_COLONY_MAJESTY_LOOPS.md.
    /// Lessons are training wheels. Failures speak on Mars with wheels off.
    /// Does not replace SpecialistFlavor.
    /// LevyWalk / PurseSitting / PurseStolen wait for levy visibility.
    /// MarketPayout / MarketBlocked wait for pad trickle. Catalogued here so those
    /// slices can call GrokCatalog.Say without inventing copy.
    /// </summary>
    public static class GrokCatalog
    {
        public const string Speaker = "Grok";

        public static bool IsLesson(GrokBeat beat) =>
            beat <= GrokBeat.TankVsWallet || beat == GrokBeat.MarketPayout;

        public static string Format(string line) =>
            string.IsNullOrEmpty(line) ? Speaker : Speaker + " — " + line;

        public static string Line(GrokBeat beat)
        {
            switch (beat)
            {
                case GrokBeat.Drop:
                    return "Welcome to a rock that already has a landlord. Raise a Commons before the Freeholds invoice the crater.";
                case GrokBeat.FirstHab:
                    return "Humans stay indoors. Everything that walks is a robot. Try not to mix them up — it gets philosophical.";
                case GrokBeat.FirstFlag:
                    return "That is a decree, not a leash. Post it. If nobody wants it, that is a you problem.";
                case GrokBeat.GreedAsk:
                    return "Anvil wants 790. He can count. Raise the bounty or enjoy the scenery.";
                case GrokBeat.LevyWalk:
                    return "See the crate on wheels? That is your tax collector. We are not calling it that. Do not click it.";
                case GrokBeat.PurseSitting:
                    return "Credits are napping on the HAB. Haul will get around to it. Or a mite will. Gambling!";
                case GrokBeat.YardBill:
                    return "Anvil is in the Fobot Yard. Standing him up costs more than the Scout. That is called having favorites.";
                case GrokBeat.TankVsWallet:
                    return "That red chip is water, not a coupon. Credits will not drink themselves. Build a farm or stop collecting roommates.";
                case GrokBeat.Refusal:
                    return "Flag still unpaid. The Compact does not work for exposure.";
                case GrokBeat.IceCritical:
                    return "Life support is a suggestion until it is not. The tank is at four. People first, Anvil second.";
                case GrokBeat.PowerShort:
                    return "Grid is skinny. Everyone is working at seventy percent, including your patience.";
                case GrokBeat.PurseStolen:
                    return "Junk-bot ate the levy. Congratulations, you have invented charity.";
                case GrokBeat.YardUnaffordable:
                    return "Level 6 wreck, Compact scrip insufficient. Re-fab a rookie or start a bake sale. Do not pay in ice.";
                case GrokBeat.EmptyRoster:
                    return "Nobody is standing. You have twenty seconds of optimism left.";
                case GrokBeat.MarketPayout:
                    return "Pad paid out. Dock fee already left. The Freeholds send their love. It is itemized.";
                case GrokBeat.MarketBlocked:
                    return "Stall is closed. We do not export lunch until the tank is fat. Try twelve. Try not dying.";
                case GrokBeat.StalkerSiphon:
                    return "Something just drank a unit of ice. That was not a sale.";
                default:
                    return "";
            }
        }

        public static string Say(GrokBeat beat) => Format(Line(beat));
    }

    /// <summary>
    /// Luna (and Earth until parked-bodies) default training wheels on.
    /// Mars default off — failure asides only. Optional Mars lessons toggle.
    /// </summary>
    public static class GrokAdvisor
    {
        public static bool DefaultTrainingWheels(CelestialBodyId body) =>
            body == CelestialBodyId.Luna || body == CelestialBodyId.Earth;

        public static bool TrainingWheels(CelestialBodyId body)
        {
            if (body == CelestialBodyId.Mars)
                return DemoSettings.MarsGrokLessons;
            return DefaultTrainingWheels(body);
        }

        public static bool Allows(CelestialBodyId body, GrokBeat beat)
        {
            if (!GrokCatalog.IsLesson(beat)) return true;
            return TrainingWheels(body);
        }
    }

    /// <summary>Once-per-stretch latches so Grok does not lecture twice.</summary>
    public sealed class GrokSession
    {
        private readonly bool[] _taken = new bool[17];

        public bool TrySpeak(GrokBeat beat, bool wheels, bool condition)
        {
            int i = (int)beat;
            if (i < 0 || i >= _taken.Length) return false;
            if (!condition)
            {
                if (!GrokCatalog.IsLesson(beat))
                    _taken[i] = false;
                return false;
            }

            if (GrokCatalog.IsLesson(beat) && !wheels) return false;
            if (_taken[i]) return false;
            _taken[i] = true;
            return true;
        }
    }
}
