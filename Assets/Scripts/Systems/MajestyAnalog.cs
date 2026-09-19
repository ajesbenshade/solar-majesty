namespace SolarMajesty
{
    /// <summary>
    /// Majesty 2 loop → Solar Majesty toy. Player-facing names stay Compact / robot.
    /// Never use Paradox guild or temple names in HUD copy.
    /// </summary>
    public sealed class MajestyAnalogDef
    {
        public string MajestyLoop;
        public string CompactName;
        public string Toy;
    }

    public static class MajestyAnalog
    {
        public static readonly MajestyAnalogDef[] All =
        {
            new MajestyAnalogDef
            {
                MajestyLoop = "Palace",
                CompactName = "Colony Commons",
                Toy = "First civic dock. Levy home chest."
            },
            new MajestyAnalogDef
            {
                MajestyLoop = "Houses / gold",
                CompactName = "HAB purses",
                Toy = "Colonists print CRED. Haul walks it home."
            },
            new MajestyAnalogDef
            {
                MajestyLoop = "Tax collectors",
                CompactName = "Haul (Courier)",
                Toy = "Walks HAB purses to Commons or a far Watchtower."
            },
            new MajestyAnalogDef
            {
                MajestyLoop = "Farms",
                CompactName = "Greenhouse Farm",
                Toy = "Fills the ICE tank (lungs), not the wallet."
            },
            new MajestyAnalogDef
            {
                MajestyLoop = "Marketplace",
                CompactName = "Market Stall",
                Toy = "Heroes buy potions. Surplus ICE/REG siphons to CRED above reserve."
            },
            new MajestyAnalogDef
            {
                MajestyLoop = "Blacksmith",
                CompactName = "Blacksmith",
                Toy = "Lodge arms and armor, paid in CRED."
            },
            new MajestyAnalogDef
            {
                MajestyLoop = "Guardhouse / tower",
                CompactName = "Watchtower",
                Toy = "Posts Aegis or Rim Watch. Levy chest. Arm lasers."
            },
            new MajestyAnalogDef
            {
                MajestyLoop = "Temple resurrect",
                CompactName = "Fobot Yard",
                Toy = "Pay CRED to stand wrecks. Bill scales with level."
            },
            new MajestyAnalogDef
            {
                MajestyLoop = "Temple heal",
                CompactName = "Aid Station",
                Toy = "Hurt robots pay CRED for a patch. Triage clocks in."
            },
            new MajestyAnalogDef
            {
                MajestyLoop = "Inn",
                CompactName = "Waystation Inn",
                Toy = "Rest and fatigue. No shop."
            },
            new MajestyAnalogDef
            {
                MajestyLoop = "Warrior / Ranger / Cleric guilds",
                CompactName = "Aegis / Horizon / Triage halls",
                Toy = "Class kits, pulses, flag pull. Anvil is the dwarf forge hall."
            },
            new MajestyAnalogDef
            {
                MajestyLoop = "Wizard tower / university",
                CompactName = "Lab + TECH tree",
                Toy = "Science ticks. Secret Projects are monuments."
            },
            new MajestyAnalogDef
            {
                MajestyLoop = "Magic tower",
                CompactName = "Defense Battery",
                Toy = "Campus lasers. Watchtower lasers are the rim version."
            },
            new MajestyAnalogDef
            {
                MajestyLoop = "Trading post",
                CompactName = "Landing Pad + Market",
                Toy = "Resupply dock fee. Siphon is the caravan."
            },
            new MajestyAnalogDef
            {
                MajestyLoop = "Dungeons / lairs",
                CompactName = "Dens",
                Toy = "Clear Threat. Explore is the chart decree."
            },
            new MajestyAnalogDef
            {
                MajestyLoop = "Hero recruit",
                CompactName = "Workshops",
                Toy = "Fabricate outdoor robots. Humans stay in HABs."
            },
            new MajestyAnalogDef
            {
                MajestyLoop = "Statues / wonders",
                CompactName = "Climate Loom / Aegis Spire / Deep Archive",
                Toy = "Standing bonus. Unlock from ★ tech."
            },
            new MajestyAnalogDef
            {
                MajestyLoop = "Flags / bounties",
                CompactName = "Decrees",
                Toy = "Eight FlagTypes. Heroes take or refuse. No click-to-move."
            }
        };

        public static MajestyAnalogDef ForCompactName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            for (int i = 0; i < All.Length; i++)
            {
                if (string.Equals(All[i].CompactName, name, System.StringComparison.OrdinalIgnoreCase))
                    return All[i];
            }

            return null;
        }
    }
}
