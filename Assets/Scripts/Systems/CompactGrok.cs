namespace SolarMajesty
{
    /// <summary>
    /// Scripted Compact asides for the Majesty toys. Not decree toasts. Not an LLM.
    /// </summary>
    public static class CompactGrok
    {
        public static string LevyStolen(int amount, string where) =>
            $"The purse is gone. {amount} CRED walked off {where}. That was not a sale.";

        public static string LevyDelivered(int amount) =>
            $"Haul dumped {amount} CRED at the chest. The walk was the joke.";

        public static string PurseSitting(int amount) =>
            $"{amount} CRED is napping on a HAB. Haul will not teleport it.";

        public static string YardBill(int credits) =>
            $"The yard wants {credits} CRED. Level is the scandal, not the corpse.";

        public static string YardNeedsBuilding() =>
            "Dock a Fobot Yard. Y is not a miracle. It is an invoice.";

        public static string SiphonPaid(int credits, int ice, int reg) =>
            $"Pad paid out +{credits} CRED. −{ice} ICE −{reg} REG. The tank still has lunch.";

        public static string SiphonBlocked(int reserve) =>
            $"Stall is closed. We do not export lunch until the tank is past {reserve}. Try not dying.";

        public static string DenCharted(string who) =>
            $"A den crawled out of the fog. {who} walked it. Clear Threat is now a place, not a rumor.";

        public static string OpenHandsHint() =>
            "Open Hands: they will take a cheap flag. Hunger is past the greed gate. Courage is not.";
    }
}
