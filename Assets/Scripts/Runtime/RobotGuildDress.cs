using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Banner / column tint so the four starter halls read apart on CMD-1 dress.
    /// Visual only — does not change pull, fabricate, or SpecialistBrain.
    /// </summary>
    public static class RobotGuildDress
    {
        public static void Apply(GameObject go, BuildingData data)
        {
            if (go == null || data == null || data.category != BuildingCategory.GuildHall)
                return;

            Color banner = new Color(0.96f, 0.42f, 0.08f);
            if (RobotGuildCatalog.TryMatch(data, out var guild) && guild != null)
                banner = guild.Banner;
            HeroBuildingKits.PaintGuildAccents(go.transform, banner);
        }
    }
}
