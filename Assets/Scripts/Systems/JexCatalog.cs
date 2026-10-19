namespace SolarMajesty
{
    /// <summary>Scripted Jex beats. Catalog only ΓÇö never a live LLM.</summary>
    public enum JexBeat
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
    /// Jex (SpaceXAI) advisor copy from Docs/MARS_COLONY_MAJESTY_LOOPS.md.
    /// Lessons are training wheels. Failures speak on Mars with wheels off.
    /// Does not replace SpecialistFlavor.
    /// LevyWalk / PurseSitting / PurseStolen wait for levy visibility.
    /// MarketPayout / MarketBlocked wait for pad trickle. Catalogued here so those
    /// slices can call JexCatalog.Say without inventing copy.
    /// </summary>
    public static class JexCatalog
    {
        public const string Speaker = "Jex";

        public static bool IsLesson(JexBeat beat) =>
            beat <= JexBeat.TankVsWallet || beat == JexBeat.MarketPayout;

        public static string Format(string line) =>
            string.IsNullOrEmpty(line) ? Speaker : Speaker + " ΓÇö " + line;

        public static string Line(JexBeat beat)
        {
            switch (beat)
            {
                case JexBeat.Drop:
                    return "Welcome to a rock that already has a landlord. Raise a Commons before the Freeholds invoice the crater.";
                case JexBeat.FirstHab:
                    return "Worker bots run the yard. Contract units take bounties. Same mind, different jobs.";
                case JexBeat.FirstFlag:
                    return "That is a decree, not a leash. Post it. If nobody wants it, that is a you problem.";
                case JexBeat.GreedAsk:
                    return "Anvil wants 790. He can count. Raise the bounty or enjoy the scenery.";
                case JexBeat.LevyWalk:
                    return "See the crate on wheels? That is Haul walking EU home. Do not click it.";
                case JexBeat.PurseSitting:
                    return "EU is sitting in building batteries. Haul will get around to it. Or a mite will. Gambling!";
                case JexBeat.YardBill:
                    return "Anvil is in the Fobot Yard. Standing him up costs more than the Scout. That is called having favorites.";
                case JexBeat.TankVsWallet:
                    return "That red chip is water, not a coupon. EU will not drink itself. Build a farm or stop collecting idle bots.";
                case JexBeat.Refusal:
                    return "Flag still unpaid. The Compact does not work for exposure.";
                case JexBeat.IceCritical:
                    return "Life support is a suggestion until it is not. The tank is at four. Bots first, Anvil second.";
                case JexBeat.PowerShort:
                    return "Grid is skinny. Everyone is working at seventy percent, including your patience.";
                case JexBeat.PurseStolen:
                    return "Haul lost the levy on the road.";
                case JexBeat.YardUnaffordable:
                    return "Level 6 wreck, Compact EU insufficient. Re-fab a rookie or start a bake sale. Do not pay in ice.";
                case JexBeat.EmptyRoster:
                    return "Nobody is standing. You have twenty seconds of optimism left.";
                case JexBeat.MarketPayout:
                    return "Pad paid out. Dock fee already left. The Freeholds send their love. It is itemized.";
                case JexBeat.MarketBlocked:
                    return "Stall is closed. We do not export lunch until the tank is fat. Try twelve. Try not dying.";
                case JexBeat.StalkerSiphon:
                    return "Something just drank a unit of ice. That was not a sale.";
                default:
                    return "";
            }
        }

        public static string Say(JexBeat beat) => Format(Line(beat));
    }

    /// <summary>
    /// Luna (and Earth until parked-bodies) default training wheels on.
    /// Mars default off ΓÇö failure asides only. Optional Mars lessons toggle.
    /// </summary>
    public static class JexAdvisor
    {
        public static bool DefaultTrainingWheels(CelestialBodyId body) =>
            body == CelestialBodyId.Luna || body == CelestialBodyId.Earth;

        public static bool TrainingWheels(CelestialBodyId body)
        {
            if (body == CelestialBodyId.Mars)
                return DemoSettings.MarsJexLessons;
            return DefaultTrainingWheels(body);
        }

        public static bool Allows(CelestialBodyId body, JexBeat beat)
        {
            if (!JexCatalog.IsLesson(beat)) return true;
            return TrainingWheels(body);
        }
    }

    /// <summary>Once-per-stretch latches so Jex does not lecture twice.</summary>
    public sealed class JexSession
    {
        private readonly bool[] _taken = new bool[17];

        public bool TrySpeak(JexBeat beat, bool wheels, bool condition)
        {
            int i = (int)beat;
            if (i < 0 || i >= _taken.Length) return false;
            if (!condition)
            {
                if (!JexCatalog.IsLesson(beat))
                    _taken[i] = false;
                return false;
            }

            if (JexCatalog.IsLesson(beat) && !wheels) return false;
            if (_taken[i]) return false;
            _taken[i] = true;
            return true;
        }
    }
}
