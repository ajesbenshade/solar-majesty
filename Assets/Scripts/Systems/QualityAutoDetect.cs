using System;

namespace SolarMajesty
{
    /// <summary>
    /// Picks the first-launch quality level from the GPU. Only used when the player has never chosen
    /// one; the Settings choice always wins afterwards.
    ///
    /// Integrated GPUs report shared system RAM as video memory (an AMD iGPU here reported 48 GB),
    /// so they are recognised by name first and memory is only trusted for discrete cards.
    /// Levels: 0 Very Low, 1 Low, 2 Medium, 3 High, 4 Very High, 5 Ultra.
    /// </summary>
    public static class QualityAutoDetect
    {
        public const int VeryLow = 0, Low = 1, Medium = 2, High = 3, VeryHigh = 4, Ultra = 5;

        public static int Pick(string gpuName, int gpuMemoryMb, int systemMemoryMb, int levelCount)
        {
            int max = Math.Max(0, levelCount - 1);
            string name = (gpuName ?? "").ToLowerInvariant();

            int pick;
            if (IsSoftware(name)) pick = VeryLow;
            else if (systemMemoryMb > 0 && systemMemoryMb < 6000) pick = Low;
            else if (IsIntegrated(name)) pick = systemMemoryMb >= 12000 ? Medium : Low;
            else if (gpuMemoryMb >= 8000) pick = Ultra;
            else if (gpuMemoryMb >= 6000) pick = VeryHigh;
            else if (gpuMemoryMb >= 4000) pick = High;
            else if (gpuMemoryMb > 0) pick = Medium;
            else pick = High; // unknown: middle of the road

            return Math.Min(pick, max);
        }

        public static bool IsSoftware(string lowerName) =>
            lowerName.Contains("microsoft basic render") || lowerName.Contains("llvmpipe") ||
            lowerName.Contains("swiftshader") || lowerName.Contains("warp");

        public static bool IsIntegrated(string lowerName)
        {
            if (lowerName.Contains("arc")) return false;            // Intel Arc is discrete
            if (lowerName.Contains("intel")) return true;           // UHD / Iris / HD Graphics
            if (lowerName.Contains("radeon(tm) graphics") || lowerName.Contains("radeon graphics")) return true;
            if (lowerName.Contains("vega") && !lowerName.Contains("rx")) return true;
            if (lowerName.Contains("radeon") && lowerName.Contains("780m")) return true;
            if (lowerName.Contains("radeon") && lowerName.Contains("680m")) return true;
            return false;
        }
    }
}
