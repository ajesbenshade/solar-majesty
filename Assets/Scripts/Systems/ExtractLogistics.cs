using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Pure haul math for Extract flags. Runtime finds the site; this only scores distance,
    /// matching drop-off, outpost bonus, and saturation. Does not know about scene objects.
    /// </summary>
    public static class ExtractLogistics
    {
        public const float SweetDist = 8f;
        public const float MaxHaul = 36f;
        public const float SaturateWindow = 8f;

        public static bool IsDropOff(BuildingCategory cat)
        {
            switch (cat)
            {
                case BuildingCategory.Mine:
                case BuildingCategory.Mining:
                case BuildingCategory.Farm:
                case BuildingCategory.RegolithCamp:
                case BuildingCategory.Power:
                case BuildingCategory.LandingPad:
                case BuildingCategory.Palace:
                    return true;
                default:
                    return false;
            }
        }

        public static bool Prefers(BuildingCategory cat, ResourceNodeType node)
        {
            switch (node)
            {
                case ResourceNodeType.Metals:
                    return cat == BuildingCategory.Mine || cat == BuildingCategory.Mining;
                case ResourceNodeType.Ice:
                    return cat == BuildingCategory.Farm;
                case ResourceNodeType.Fissile:
                    return cat == BuildingCategory.Power;
                default:
                    return cat == BuildingCategory.RegolithCamp;
            }
        }

        /// <summary>
        /// 1.0 at a matching drop-off within 8 m; ~0.5 at 36 m; ~0.4 with no site.
        /// Same-node extracts inside 8 s apply saturate (down toward 0.62×).
        /// </summary>
        public static float HaulEfficiency(
            float dist,
            bool matching,
            bool hasSite,
            bool outpostLocal,
            float saturate01)
        {
            float saturate = Mathf.Clamp01(saturate01);
            if (!hasSite)
                return Mathf.Lerp(0.45f, 0.35f, saturate);

            float t = dist <= SweetDist
                ? 0f
                : Mathf.Clamp01((dist - SweetDist) / (MaxHaul - SweetDist));
            float haul = matching
                ? Mathf.Lerp(1f, 0.5f, t)
                : Mathf.Lerp(0.72f, 0.4f, t);

            if (outpostLocal && matching)
                haul *= 1.12f;

            haul *= Mathf.Lerp(1f, 0.62f, saturate);
            return Mathf.Clamp(haul, 0.28f, 1.25f);
        }
    }
}
