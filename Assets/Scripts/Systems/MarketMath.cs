using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Pad weigh-station math. Surplus ICE (and silent REG) become Compact scrip
    /// only while the tank is above reserve. No FlagType. Does not touch ScoreFlag.
    /// </summary>
    public static class MarketMath
    {
        public const int IceReserveFloor = 12;
        public const int IcePerColonist = 3;
        public const int IceSiphonMax = 1;
        public const int IceToMet = 20;
        public const int RegReserve = 10;
        public const int RegSiphonMax = 2;
        public const int RegToMet = 10;
        public const float IntervalSeconds = 24f;

        public const string GrokPayout =
            "Pad paid out. Dock fee already left. The Freeholds send their love. It is itemized.";
        public const string GrokBlocked =
            "Stall is closed. We do not export lunch until the tank is fat. Try twelve. Try not dying.";

        public static int IceReserve(int population) =>
            Mathf.Max(IceReserveFloor, IcePerColonist * Mathf.Max(0, population));

        public static bool CanExport(int ice, int population) =>
            ice > IceReserve(population);

        /// <summary>ICE taken this tick. Never cuts into the reserve.</summary>
        public static int IceToSiphon(int ice, int population)
        {
            int surplus = ice - IceReserve(population);
            if (surplus <= 0) return 0;
            return Mathf.Min(IceSiphonMax, surplus);
        }

        /// <summary>REG freight while the stall is open. Zero if the tank is not fat.</summary>
        public static int RegToSiphon(int ice, int population, int reg)
        {
            if (!CanExport(ice, population)) return 0;
            int surplus = reg - RegReserve;
            if (surplus <= 0) return 0;
            return Mathf.Min(RegSiphonMax, surplus);
        }

        public static int CreditsFrom(int iceTaken, int regTaken) =>
            iceTaken * IceToMet + regTaken * RegToMet;
    }
}
