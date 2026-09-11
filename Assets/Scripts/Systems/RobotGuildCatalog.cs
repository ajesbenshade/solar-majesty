using UnityEngine;

namespace SolarMajesty
{
    public enum RobotGuildId
    {
        Horizon = 0,
        Anvil = 1,
        Aegis = 2,
        Triage = 3
    }

    /// <summary>
    /// Authored robot guild. Presentation + hall identity only —
    /// SpecialistBrain scoring is unchanged. Flags near a hall pull
    /// <see cref="Class"/> through the existing workshop-bonus path.
    /// </summary>
    public sealed class RobotGuildDef
    {
        public RobotGuildId Id;
        public string HallName;
        public string ShortName;
        public SpecialistClass Class;
        public string Motto;
        public string Wants;
        public string Ignores;
        public string CatalogLine;
        public Color Banner;

        public SpecialistClass[] Occupants => new[] { Class };
    }

    /// <summary>
    /// Four basic robot guilds (Horizon / Anvil / Aegis / Triage).
    /// Later callsigns stay on workshops; they are not starter halls.
    /// </summary>
    public static class RobotGuildCatalog
    {
        private static RobotGuildDef[] _starter;

        public static RobotGuildDef[] Starter => _starter ??= BuildStarter();

        public static int StarterCount => Starter.Length;

        public static RobotGuildDef Get(RobotGuildId id)
        {
            var list = Starter;
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i].Id == id)
                    return list[i];
            }

            return list[0];
        }

        public static RobotGuildDef ForClass(SpecialistClass cls)
        {
            var list = Starter;
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i].Class == cls)
                    return list[i];
            }

            return null;
        }

        public static bool IsStarterClass(SpecialistClass cls) => ForClass(cls) != null;

        public static bool TryMatch(BuildingData data, out RobotGuildDef guild)
        {
            guild = null;
            if (data == null) return false;
            if (data.preferredOccupants != null && data.preferredOccupants.Length > 0)
            {
                guild = ForClass(data.preferredOccupants[0]);
                if (guild != null) return true;
            }

            if (!string.IsNullOrEmpty(data.displayName))
            {
                var list = Starter;
                for (int i = 0; i < list.Length; i++)
                {
                    if (string.Equals(list[i].HallName, data.displayName, System.StringComparison.Ordinal))
                    {
                        guild = list[i];
                        return true;
                    }
                }
            }

            return false;
        }

        private static RobotGuildDef[] BuildStarter()
        {
            return new[]
            {
                new RobotGuildDef
                {
                    Id = RobotGuildId.Horizon,
                    HallName = "Horizon Lodge",
                    ShortName = "Horizon",
                    Class = SpecialistClass.ScoutDrone,
                    Motto = "Chart first. Fight never.",
                    Wants = "Explore",
                    Ignores = "fights",
                    CatalogLine = "Scout hall · cheap Explore",
                    Banner = new Color(0.22f, 0.84f, 0.98f)
                },
                new RobotGuildDef
                {
                    Id = RobotGuildId.Anvil,
                    HallName = "Anvil Compact",
                    ShortName = "Anvil",
                    Class = SpecialistClass.EngineerBot,
                    Motto = "Pay holds. Then we weld.",
                    Wants = "Build",
                    Ignores = "cheap flags, dens",
                    CatalogLine = "Engineer hall · greedy Build",
                    Banner = new Color(0.96f, 0.42f, 0.08f)
                },
                new RobotGuildDef
                {
                    Id = RobotGuildId.Aegis,
                    HallName = "Aegis Lodge",
                    ShortName = "Aegis",
                    Class = SpecialistClass.DefenseMech,
                    Motto = "Nothing crosses the rim.",
                    Wants = "Clear Threat / Defend",
                    Ignores = "Explore / Build",
                    CatalogLine = "Defense hall · Clear / Defend",
                    Banner = new Color(0.45f, 0.72f, 1f)
                },
                new RobotGuildDef
                {
                    Id = RobotGuildId.Triage,
                    HallName = "Triage Compact",
                    ShortName = "Triage",
                    Class = SpecialistClass.Medic,
                    Motto = "The wounded come first.",
                    Wants = "Defend",
                    Ignores = "dens",
                    CatalogLine = "Medic hall · Defend, no dens",
                    Banner = new Color(0.52f, 0.76f, 0.86f)
                }
            };
        }
    }
}
