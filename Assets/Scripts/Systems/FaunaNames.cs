namespace SolarMajesty
{
    /// <summary>Player-facing fauna names. Matches the defeat line on each kind.</summary>
    public static class FaunaNames
    {
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
