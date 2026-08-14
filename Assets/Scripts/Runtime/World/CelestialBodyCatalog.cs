using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Built-in world profiles. Future bodies (Ceres, Titan, …) land here.
    /// Campaign order: Earth → Luna → Mars.
    /// </summary>
    public static class CelestialBodyCatalog
    {
        private static CelestialBodyProfile _earth;
        private static CelestialBodyProfile _luna;
        private static CelestialBodyProfile _mars;
        private static readonly CelestialBodyId[] AllBodies =
        {
            CelestialBodyId.Earth,
            CelestialBodyId.Luna,
            CelestialBodyId.Mars
        };

        public static CelestialBodyId[] All => AllBodies;

        public static CelestialBodyProfile Earth() => _earth ??= BuildEarth();
        public static CelestialBodyProfile Luna() => _luna ??= BuildLuna();
        public static CelestialBodyProfile Mars() => _mars ??= BuildMars();

        public static CelestialBodyProfile Get(CelestialBodyId id)
        {
            switch (id)
            {
                case CelestialBodyId.Earth: return Earth();
                case CelestialBodyId.Mars: return Mars();
                default: return Luna();
            }
        }

        public static CelestialBodyId Next(CelestialBodyId id)
        {
            int n = ((int)id + 1) % AllBodies.Length;
            return AllBodies[n];
        }

        private static CelestialBodyProfile BuildEarth()
        {
            return new CelestialBodyProfile
            {
                Id = CelestialBodyId.Earth,
                DisplayName = "Earth",
                ShortCode = "EARTH",
                Briefing = "Empty drop. Raise the Palace, dock HAB via airlocks, then clear dens and hold pop 8.",
                ArrivalLog = "Earth drop confirmed. Claim disc live. Raise the Palace Keep before any other module.",
                VictoryLog = "Earth secured. Lunar Rocket is on the pad — trajectory to Luna is open.",
                FailLog = "Earth outpost lost. Revive the robots or restart the drop.",
                GroundLight = new Color(0.28f, 0.38f, 0.22f),
                GroundDark = new Color(0.16f, 0.22f, 0.12f),
                Horizon = new Color(0.35f, 0.48f, 0.55f),
                RockColor = new Color(0.32f, 0.30f, 0.28f),
                CraterRim = new Color(0.34f, 0.36f, 0.30f),
                CraterFloor = new Color(0.22f, 0.26f, 0.18f),
                DuneColor = new Color(0.42f, 0.40f, 0.28f),
                SoilNodeColor = new Color(0.40f, 0.36f, 0.22f),
                LairRim = new Color(0.20f, 0.10f, 0.08f),
                LairPit = new Color(0.08f, 0.05f, 0.04f),
                WaterDeep = new Color(0.10f, 0.26f, 0.40f),
                WaterShallow = new Color(0.20f, 0.52f, 0.58f),
                ForestCanopy = new Color(0.12f, 0.34f, 0.14f),
                ForestTrunk = new Color(0.30f, 0.18f, 0.10f),
                SkyTop = new Color(0.22f, 0.42f, 0.72f),
                SkyHorizon = new Color(0.62f, 0.72f, 0.82f),
                SunColor = new Color(1f, 0.96f, 0.88f),
                SunIntensity = 1.25f,
                SunEuler = new Vector3(54f, -22f, 0f),
                GradeFilter = new Color(0.96f, 1f, 0.92f),
                AmbientHum = 72f,
                FillColor = new Color(0.45f, 0.55f, 0.75f),
                AmbientSky = new Color(0.40f, 0.55f, 0.75f),
                AmbientEquator = new Color(0.35f, 0.40f, 0.32f),
                AmbientGround = new Color(0.12f, 0.14f, 0.10f),
                FogColor = new Color(0.55f, 0.62f, 0.68f),
                FogStart = 50f,
                FogEnd = 280f,
                AtmosphereThickness = 1.05f,
                SkyExposure = 1.05f,
                CraterCount = 0,
                RockCount = 36,
                DuneCount = 0,
                LakeCount = 7,
                RiverCount = 4,
                ForestPatchCount = 14,
                ResourceNodeCount = 10,
                LairCount = 3,
                CampusExclusion = 20f,
                MinSpacing = 8f,
                IcePolarBias = 0.25f,
                ResourceWeights = new[] { 4, 3, 3, 1 },
                PopulationGoal = 8,
                SustainHoldSeconds = 25f,
                ResearchRateMultiplier = 1.35f,
                StartRegolith = 110,
                StartWaterIce = 70,
                StartMetals = 340,
                StartPower = 120
            };
        }

        private static CelestialBodyProfile BuildLuna()
        {
            return new CelestialBodyProfile
            {
                Id = CelestialBodyId.Luna,
                DisplayName = "Luna",
                ShortCode = "LUNA",
                Briefing = "Cratered grey. Harder dens, pop 12, research Mars Ship. Same Lego campus rules.",
                ArrivalLog = "Luna insertion complete. Vacuum and dens — sustain pop 12, then stage Mars Ship.",
                VictoryLog = "Luna holds. Mars Ship staged. Next body: Mars.",
                FailLog = "Luna plaza is overrun. Revive or reseed this crater field.",
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
                SunEuler = new Vector3(48f, -35f, 0f),
                GradeFilter = new Color(0.94f, 0.96f, 1f),
                AmbientHum = 48f,
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
                ResourceWeights = new[] { 5, 3, 2, 2 },
                PopulationGoal = 12,
                SustainHoldSeconds = 40f,
                ResearchRateMultiplier = 1f,
                StartRegolith = 90,
                StartWaterIce = 55,
                StartMetals = 300,
                StartPower = 110
            };
        }

        private static CelestialBodyProfile BuildMars()
        {
            return new CelestialBodyProfile
            {
                Id = CelestialBodyId.Mars,
                DisplayName = "Mars",
                ShortCode = "MARS",
                Briefing = "Finale. Dust, dens, pop 16. Hold the red campus until solar conquest completes.",
                ArrivalLog = "Mars descent. This is the spine's last body — dens, sustain pop 16, hold.",
                VictoryLog = "Mars holds. Solar conquest complete. Oversee in sandbox or reseed.",
                FailLog = "Mars outpost overwhelmed. The red plaza is not yours until the party stands.",
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
                SunEuler = new Vector3(36f, -52f, 0f),
                GradeFilter = new Color(1f, 0.88f, 0.72f),
                AmbientHum = 58f,
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
                ResourceWeights = new[] { 3, 5, 2, 2 },
                PopulationGoal = 16,
                SustainHoldSeconds = 50f,
                ResearchRateMultiplier = 0.9f,
                StartRegolith = 100,
                StartWaterIce = 65,
                StartMetals = 320,
                StartPower = 120
            };
        }
    }
}
