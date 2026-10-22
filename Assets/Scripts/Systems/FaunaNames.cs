namespace SolarMajesty
{
    /// <summary>Player-facing fauna names. Matches the defeat line on each kind.</summary>
    public static class FaunaNames
    {
        /// <summary><see cref="Display"/> in the current language, for on-screen text.</summary>
        public static string DisplayText(FaunaKind kind) => Loc.S(Display(kind));

        /// <summary>
        /// English name. Stays English: it travels as the attacker id into hit/levy notices and
        /// model prompts. Tools/loc/extract.py collects these returns; show <see cref="DisplayText"/>.
        /// </summary>
        public static string Display(FaunaKind kind)
        {
            switch (kind)
            {
                case FaunaKind.Mite: return "Regolith Mite";
                case FaunaKind.Leech: return "Watt Leech";
                case FaunaKind.Wisp: return "Ice Wisp";
                case FaunaKind.Tick: return "Rock Tick";
                case FaunaKind.Creeper: return "Soil Creeper";
                case FaunaKind.Hopper: return "Ash Hopper";
                case FaunaKind.JunkBot: return "Junk Bot";
                default: return "Dust Stalker";
            }
        }
    }
}
