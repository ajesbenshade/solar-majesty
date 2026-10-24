namespace SolarMajesty
{
    /// <summary>
    /// Plain-language nudges for the Earth demo. The tutorial bar says what to do; these say it
    /// again, more concretely, when a stranger has gone quiet or pressed a key that did nothing.
    /// </summary>
    public static class FirstHourHints
    {
        // Key names come from the live bindings: {GameAction} tokens are filled by KeyHints.
        public static string PlaceWorkshop => KeyHints.Fill(PlaceWorkshopText);
        public static string PostCheapBuild => KeyHints.Fill(PostCheapBuildText);
        public static string RaiseBuildPrice => KeyHints.Fill(RaiseBuildPriceText);
        public static string PostDefend => KeyHints.Fill(PostDefendText);
        public static string BountyNeedsPole => KeyHints.Fill(BountyNeedsPoleText);

        private const string PlaceWorkshopText =
            "Stuck? Press {ToolBuild}, then {BuildSlot1}, and click open ground near the Commons to place the Engineer workshop.";
        public const string WorkshopRising =
            "The workshop is going up. Its Engineer walks out on its own when it is done. You never order units.";
        public const string WorkshopHireShort =
            "The workshop is done, but hiring its Engineer costs EU. Courier bots are hauling it to the till.";
        private const string PostCheapBuildText =
            "Stuck? Press {ToolFlag}, then {FlagBuild} (Build), and click open ground. Leave the bounty at 400 and watch the Engineer.";
        private const string RaiseBuildPriceText =
            "The Engineer wants more. Click the Build pole to select it, then press {BountyUp} (or + on the flag panel) a few times.";
        private const string PostDefendText =
            "Stuck? Press {ToolFlag}, then {FlagDefend} (Defend), and click on the soil creeper by the yard.";
        private const string BountyNeedsPoleText =
            "{BountyUp} and {BountyDown} change a flag's bounty. Click a flag pole first. Speed is {SpeedDown} and {SpeedUp}.";

        /// <summary>
        /// The nudge for an Earth-demo step after the player has gone quiet, or null when there is
        /// nothing useful to add (tutorial done, unknown step).
        /// </summary>
        public static string IdleHint(int step, bool workshopPlaced, bool awaitingHire)
        {
            if (step < 0 || step >= FirstHourTutorial.Goal) return null;
            if (step == 0)
            {
                if (!workshopPlaced) return PlaceWorkshop;
                return awaitingHire ? WorkshopHireShort : WorkshopRising;
            }
            if (step == 1) return PostCheapBuild;
            if (step == 2) return RaiseBuildPrice;
            return PostDefend;
        }

        /// <summary>
        /// + / − with no pole selected and the flag tool closed changes nothing. Say why instead of
        /// staying silent. Null when the keys did something.
        /// </summary>
        public static string BountyKeyHint(bool poleSelected, bool flagToolOpen) =>
            poleSelected || flagToolOpen ? null : BountyNeedsPole;

        /// <summary>A flag click the till could not cover. The post used to fail silently.</summary>
        public static string PostRefused(string flagName, int cost)
        {
            string name = string.IsNullOrEmpty(flagName) ? "flag" : flagName;
            return $"Not enough EU to post {name} at {cost}. Right-click a flag to cancel it for a refund, or lower the bounty with −.";
        }
    }
}
