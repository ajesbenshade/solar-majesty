using UnityEngine;

namespace SolarMajesty
{
    public enum CelestialBodyId
    {
        Earth = 0,
        Luna = 1,
        Mars = 2
    }

    /// <summary>
    /// Data-only description of a conquerable world. World gen, dressing, and atmosphere
    /// read this — add a new planet by adding a catalog entry, not a new generator.
    /// </summary>
    public sealed class CelestialBodyProfile
    {
        public CelestialBodyId Id;
        public string DisplayName;
        public string ShortCode;

        [Header("Surface")]
        public Color GroundLight;
        public Color GroundDark;
        public Color Horizon;
        public Color RockColor;
        public Color CraterRim;
        public Color CraterFloor;
        public Color DuneColor;
        public Color SoilNodeColor;
        public Color LairRim;
        public Color LairPit;

        [Header("Sky / light")]
        public Color SkyTop;
        public Color SkyHorizon;
        public Color SunColor;
        public float SunIntensity = 1.45f;
        public Color FillColor;
        public Color AmbientSky;
        public Color AmbientEquator;
        public Color AmbientGround;
        public Color FogColor;
        public float FogStart = 28f;
        public float FogEnd = 95f;
        public float AtmosphereThickness = 0.15f;
        public float SkyExposure = 0.55f;

        [Header("Layout")]
        public int CraterCount = 28;
        public int RockCount = 40;
        public int DuneCount = 0;
        public int ResourceNodeCount = 12;
        public int LairCount = 4;
        public float CampusExclusion = 18f;
        public float MinSpacing = 7f;
        public float IcePolarBias = 0.55f;

        /// <summary>Relative weights for Regolith, Metals, Ice, Fissile (must be length 4).</summary>
        public int[] ResourceWeights = { 5, 3, 2, 2 };

        [Header("Campaign gates")]
        public int PopulationGoal = 12;
        public float SustainHoldSeconds = 40f;
        public float ResearchRateMultiplier = 1f;
    }
}
