namespace SolarMajesty
{
    /// <summary>
    /// Plain-language nudges for the Earth demo. The tutorial bar says what to do; these say it
    /// again, more concretely, when a stranger has gone quiet or pressed a key that did nothing.
    /// </summary>
    public static class FirstHourHints
    {
        public const string PlaceWorkshop =
            "Stuck? Press B, then 1, and click open ground near the Commons to place the Engineer workshop.";
        public const string WorkshopRising =
            "The workshop is going up. Its Engineer walks out on his own when it is done. You never order units.";
        public const string WorkshopHireShort =
            "The workshop is done, but hiring its Engineer costs EU. Tax collectors are bringing it to the Commons.";
        public const string PostCheapBuild =
            "Stuck? Press G, then F3 (Build), and click open ground. Leave the bounty at 700 and watch the Engineer.";
        public const string RaiseBuildPrice =
            "The Engineer wants more. Click the Build pole to select it, then press + (or + on the flag panel) a few times.";
        public const string PostDefend =
            "Stuck? Press G, then F5 (Defend), and click on the soil creeper by the yard.";
        public const string BountyNeedsPole =
            "+ and − change a flag's bounty. Click a flag pole first. Speed is , and .";

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

        /// <summary>A flag click the treasury could not cover. The post used to fail silently.</summary>
        public static string PostRefused(string flagName, int cost)
        {
            string name = string.IsNullOrEmpty(flagName) ? "flag" : flagName;
            return $"Not enough EU to post {name} at {cost}. Right-click a flag to cancel it for a refund, or lower the bounty with −.";
        }
    }
}
