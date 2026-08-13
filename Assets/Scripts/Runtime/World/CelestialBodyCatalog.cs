using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Built-in world profiles. Future bodies (Ceres, Titan, …) land here.
    /// </summary>
    public static class CelestialBodyCatalog
    {
        private static CelestialBodyProfile _luna;
        private static CelestialBodyProfile _mars;
        private static readonly CelestialBodyId[] AllBodies =
        {
            CelestialBodyId.Luna,
            CelestialBodyId.Mars
        };

        public static CelestialBodyId[] All => AllBodies;

        public static CelestialBodyProfile Luna() => _luna ??= BuildLuna();
        public static CelestialBodyProfile Mars() => _mars ??= BuildMars();

        public static CelestialBodyProfile Get(CelestialBodyId id) =>
            id == CelestialBodyId.Mars ? Mars() : Luna();

        public static CelestialBodyId Next(CelestialBodyId id) =>
            id == CelestialBodyId.Luna ? CelestialBodyId.Mars : CelestialBodyId.Luna;

        private static CelestialBodyProfile BuildLuna()
        {
            return new CelestialBodyProfile
            {
                Id = CelestialBodyId.Luna,
                DisplayName = "Luna",
                ShortCode = "LUNA",
                GroundLight = new Color(0.50f, 0.46f, 0.40f),
                GroundDark = new Color(0.34f, 0.31f, 0.28f),
                Horizon = new Color(0.22f, 0.20f, 0.19f),
                RockColor = new Color(0.28f, 0.26f, 0.24f),
                CraterRim = new Color(0.38f, 0.35f, 0.31f),
                CraterFloor = new Color(0.30f, 0.28f, 0.25f),
                DuneColor = new Color(0.42f, 0.39f, 0.34f),
                SoilNodeColor = new Color(0.52f, 0.48f, 0.40f),
                LairRim = new Color(0.18f, 0.08f, 0.08f),
                LairPit = new Color(0.08f, 0.04f, 0.05f),
                SkyTop = new Color(0.03f, 0.04f, 0.09f),
                SkyHorizon = new Color(0.28f, 0.24f, 0.20f),
                SunColor = new Color(1f, 0.94f, 0.86f),
                SunIntensity = 1.45f,
                FillColor = new Color(0.35f, 0.48f, 0.72f),
                AmbientSky = new Color(0.28f, 0.32f, 0.42f),
                AmbientEquator = new Color(0.22f, 0.20f, 0.18f),
                AmbientGround = new Color(0.12f, 0.10f, 0.09f),
                FogColor = new Color(0.55f, 0.52f, 0.48f),
                FogStart = 70f,
                FogEnd = 340f,
                AtmosphereThickness = 0.15f,
                SkyExposure = 0.55f,
                CraterCount = 96,
                RockCount = 140,
                DuneCount = 0,
                ResourceNodeCount = 24,
                LairCount = 8,
                CampusExclusion = 22f,
                MinSpacing = 9f,
                IcePolarBias = 0.55f,
                ResourceWeights = new[] { 5, 3, 2, 2 }
            };
        }

        private static CelestialBodyProfile BuildMars()
        {
            return new CelestialBodyProfile
            {
                Id = CelestialBodyId.Mars,
                DisplayName = "Mars",
                ShortCode = "MARS",
                GroundLight = new Color(0.62f, 0.32f, 0.18f),
                GroundDark = new Color(0.38f, 0.16f, 0.09f),
                Horizon = new Color(0.28f, 0.12f, 0.07f),
                RockColor = new Color(0.42f, 0.22f, 0.14f),
                CraterRim = new Color(0.48f, 0.24f, 0.14f),
                CraterFloor = new Color(0.32f, 0.14f, 0.08f),
                DuneColor = new Color(0.70f, 0.38f, 0.18f),
                SoilNodeColor = new Color(0.58f, 0.30f, 0.16f),
                LairRim = new Color(0.22f, 0.07f, 0.05f),
                LairPit = new Color(0.10f, 0.03f, 0.02f),
                SkyTop = new Color(0.18f, 0.10f, 0.08f),
                SkyHorizon = new Color(0.72f, 0.42f, 0.22f),
                SunColor = new Color(1f, 0.82f, 0.62f),
                SunIntensity = 1.15f,
                FillColor = new Color(0.55f, 0.32f, 0.22f),
                AmbientSky = new Color(0.42f, 0.22f, 0.16f),
                AmbientEquator = new Color(0.32f, 0.16f, 0.10f),
                AmbientGround = new Color(0.16f, 0.07f, 0.04f),
                FogColor = new Color(0.62f, 0.38f, 0.24f),
                FogStart = 55f,
                FogEnd = 300f,
                AtmosphereThickness = 0.55f,
                SkyExposure = 0.72f,
                CraterCount = 56,
                RockCount = 180,
                DuneCount = 64,
                ResourceNodeCount = 24,
                LairCount = 10,
                CampusExclusion = 22f,
                MinSpacing = 8.5f,
                IcePolarBias = 0.82f,
                ResourceWeights = new[] { 3, 5, 2, 2 }
            };
        }
    }
}
