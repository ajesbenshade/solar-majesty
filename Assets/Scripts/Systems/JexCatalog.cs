namespace SolarMajesty
{
    /// <summary>Scripted Jex beats. Catalog only — never a live LLM.</summary>
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
            string.IsNullOrEmpty(line) ? Speaker : Speaker + " — " + line;

        public static string Line(JexBeat beat)
        {
            switch (beat)
            {
                case JexBeat.Drop:
                    return "New rock, same rules. Raise a Commons before anything else.";
                case JexBeat.FirstHab:
                    return "Charging station is up. Worker bots live there. Bounty bots take the flags.";
                case JexBeat.FirstFlag:
                    return "That is a decree, not a leash. Post it. If nobody wants it, that is a you problem.";
                case JexBeat.GreedAsk:
                    return "Post it at 400 first. If Anvil walks past, raise the bounty.";
                case JexBeat.LevyWalk:
                    return "See the crate on wheels? That is Haul walking EU home. Do not click it.";
                case JexBeat.PurseSitting:
                    return "EU is sitting in building batteries. Haul will get around to it. Or a mite will. Gambling!";
                case JexBeat.YardBill:
                    return "Anvil is in the Fobot Yard. Standing it up costs more than the Scout. That is called having favorites.";
                case JexBeat.TankVsWallet:
                    return "That red chip is water, not a coupon. EU will not drink itself. Build a farm or stop collecting idle bots.";
                case JexBeat.Refusal:
                    return "Flag still unpaid. Bots do not work for exposure.";
                case JexBeat.IceCritical:
                    return "EU is running low. Bots first, Anvil second.";
                case JexBeat.PowerShort:
                    return "The till is skinny. Everyone is working slower, including your patience.";
                case JexBeat.PurseStolen:
                    return "A courier lost its EU on the road.";
                case JexBeat.YardUnaffordable:
                    return "Level 6 wreck, not enough EU. Re-fab a rookie or start a bake sale.";
                case JexBeat.EmptyRoster:
                    return "Nobody is standing. You have twenty seconds of optimism left.";
                case JexBeat.MarketPayout:
                    return "Pad paid out. EU is in the till.";
                case JexBeat.MarketBlocked:
                    return "Stall is closed. Nothing to sell yet. Try not dying.";
                case JexBeat.StalkerSiphon:
                    return "Something just drained a battery. That was not a sale.";
                default:
                    return "";
            }
        }

        public static string Say(JexBeat beat) => Format(Line(beat));

        // Localization. Line/Say stay English: baked voice clips are keyed by a hash of the exact
        // English text (VoiceBank.LineKey, Tools/audio/render_voices.py), so the voice lookup must
        // see English. Tools/loc/extract.py collects the Line() literals; translate for display only.

        /// <summary><see cref="Line"/> in the current language, for display.</summary>
        public static string LineText(JexBeat beat) => Loc.S(Line(beat));

        /// <summary><see cref="Say"/> in the current language, for display.</summary>
        public static string SayText(JexBeat beat) => Format(LineText(beat));

        /// <summary>
        /// Display text for an English overseer line that may carry the "Jex — " prefix from
        /// <see cref="Format"/>: the prefix is kept and the line itself is translated, so a
        /// voice-keyed line can travel as English and be translated where it is drawn.
        /// </summary>
        public static string DisplayText(string englishLine)
        {
            if (string.IsNullOrEmpty(englishLine)) return englishLine;
            string prefix = Speaker + " — ";
            if (englishLine.StartsWith(prefix, System.StringComparison.Ordinal))
                return prefix + Loc.S(englishLine.Substring(prefix.Length));
            return Loc.S(englishLine);
        }
    }

    /// <summary>
    /// Luna (and Earth until parked-bodies) default training wheels on.
    /// Mars default off — failure asides only. Optional Mars lessons toggle.
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
