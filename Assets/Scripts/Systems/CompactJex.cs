namespace SolarMajesty
{
    /// <summary>
    /// Scripted Jex asides for the Majesty toys. Not decree toasts. Not an LLM.
    ///
    /// Localization: templated lines are formatted with <c>Loc.F</c> here (no baked clip can match
    /// them anyway). Fixed lines (<c>=&gt; "..."</c>) are voice-keyed by their exact English text, so they
    /// return English; Tools/audio/render_voices.py scrapes them and Tools/loc/extract.py collects
    /// them, and the display path translates them with <c>Loc.S</c> after the voice lookup.
    /// </summary>
    public static class CompactJex
    {
        public static string LevyStolen(int amount, string where) =>
            Loc.F("The battery is drained. {0} EU walked off {1}. That was not a sale.", amount, where);

        /// <summary>
        /// One line for a burst of purse thefts. Templated (the numbers change), so it is
        /// text-only: VoiceBank.TryGetLine will not find a baked clip.
        /// </summary>
        public static string PestsLifted(int amount, int buildings)
        {
            int n = buildings < 1 ? 1 : buildings;
            return n == 1
                ? Loc.F("Pests drained {0} EU from {1} building battery.", amount, n)
                : Loc.F("Pests drained {0} EU from {1} building batteries.", amount, n);
        }

        public static string LevyDelivered(int amount) =>
            Loc.F("Courier hauled {0} EU to the till.", amount);

        public static string PurseSitting(int amount) =>
            Loc.F("{0} EU is sitting in building batteries. Couriers will not teleport it.", amount);

        public static string YardBill(int credits) =>
            Loc.F("The yard wants {0} EU. Level is the scandal, not the corpse.", credits);

        public static string YardNeedsBuilding() =>
            "Dock a Fobot Yard. Y is not a miracle. It is an invoice.";

        public static string SiphonPaid(int credits, int ice, int reg) =>
            Loc.F("Pad paid out +{0} EU.", credits);

        public static string SiphonBlocked(int reserve) =>
            "Stall is closed. Nothing to sell yet. Try not dying.";

        public static string DenCharted(string who) =>
            Loc.F("A den crawled out of the fog. {0} walked it. Clear Threat is now a place, not a rumor.", who);

        public static string OpenHandsHint() =>
            "Open Hands: they will take a cheap flag. Hunger is past the greed gate. Courage is not.";
    }
}
