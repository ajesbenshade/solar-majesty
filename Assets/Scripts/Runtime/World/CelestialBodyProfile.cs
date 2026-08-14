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
        public string Briefing;
        public string ArrivalLog;
        public string VictoryLog;
        public string FailLog;

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
        public Vector3 SunEuler = new Vector3(48f, -35f, 0f);
        public Color GradeFilter = Color.white;
        public float AmbientHum = 55f;
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
        public int LakeCount = 0;
        public int RiverCount = 0;
        public int ForestPatchCount = 0;
        public int ResourceNodeCount = 12;
        public int LairCount = 4;
        public float CampusExclusion = 18f;
        public float MinSpacing = 7f;
        public float IcePolarBias = 0.55f;

        [Header("Earth hydrology / vegetation")]
        public Color WaterDeep = new Color(0.12f, 0.28f, 0.42f);
        public Color WaterShallow = new Color(0.22f, 0.48f, 0.55f);
        public Color ForestCanopy = new Color(0.14f, 0.32f, 0.12f);
        public Color ForestTrunk = new Color(0.28f, 0.18f, 0.10f);

        /// <summary>Relative weights for Regolith, Metals, Ice, Fissile (must be length 4).</summary>
        public int[] ResourceWeights = { 5, 3, 2, 2 };

        [Header("Campaign gates")]
        public int PopulationGoal = 12;
        public float SustainHoldSeconds = 40f;
        public float ResearchRateMultiplier = 1f;

        [Header("Empty-start stockpile")]
        public int StartRegolith = 90;
        public int StartWaterIce = 55;
        public int StartMetals = 280;
        public int StartPower = 100;
    }
}
