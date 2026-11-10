using UnityEngine;

namespace SolarMajesty
{
    public enum CelestialBodyId
    {
        Earth = 0,
        Luna = 1,
        Mars = 2,
        Belt = 3,
        Europa = 4
    }

    /// <summary>The parts of a world that are hand-authored per Sol world.</summary>
    public enum WorldAspect
    {
        /// <summary>The height field and its features: craters, canyons, islets, ice plates.</summary>
        Terrain,
        /// <summary>Ground material, splat tiles and map dressing.</summary>
        Ground,
        /// <summary>Sky, sun path, lighting grade, clouds and haze.</summary>
        Sky,
        /// <summary>How the fauna look and what the alerts call them.</summary>
        Fauna,
        /// <summary>Music and ambience.</summary>
        Sound,
        /// <summary>Campus dressing, building finish, trees and rocks.</summary>
        Props,
        /// <summary>Decrees, cutscenes, advisor lines and tutorial beats.</summary>
        Story
    }

    public enum TerrainKit
    {
        Standard = 0,
        AsteroidField = 1,
        IceCrust = 2
    }

    /// <summary>
    /// Data-only description of a conquerable world. World gen, dressing, and atmosphere
    /// read this — add a new planet by adding a catalog entry, not a new generator.
    /// </summary>
    [System.Serializable]
    public sealed class CelestialBodyProfile
    {
        /// <summary>
        /// The Sol world this one takes its authored content from, and its place in the campaign
        /// when it is one of the five. A world is identified by <see cref="Key"/>, not by this.
        /// </summary>
        public CelestialBodyId Id;

        [Header("Authored content")]
        [Tooltip("Off: every part of this world comes from the Sol world Id names. On: each part names its own.")]
        public bool MixedContent;
        public CelestialBodyId TerrainFrom;
        public CelestialBodyId GroundFrom;
        public CelestialBodyId SkyFrom;
        public CelestialBodyId FaunaFrom;
        public CelestialBodyId SoundFrom;
        public CelestialBodyId PropsFrom;
        public CelestialBodyId StoryFrom;

        /// <summary>
        /// Which Sol world's hand-authored content one part of this world uses. Code that picks
        /// art, sound or story by world asks this, so a generated world can wear Mars's ground
        /// under Europa's sky.
        /// </summary>
        public CelestialBodyId Authored(WorldAspect aspect)
        {
            if (!MixedContent) return Id;
            switch (aspect)
            {
                case WorldAspect.Terrain: return TerrainFrom;
                case WorldAspect.Ground: return GroundFrom;
                case WorldAspect.Sky: return SkyFrom;
                case WorldAspect.Fauna: return FaunaFrom;
                case WorldAspect.Sound: return SoundFrom;
                case WorldAspect.Props: return PropsFrom;
                case WorldAspect.Story: return StoryFrom;
                default: return Id;
            }
        }

        /// <summary>Take every part from <paramref name="from"/>, then override the ones that differ.</summary>
        public void MixFrom(CelestialBodyId from)
        {
            MixedContent = true;
            TerrainFrom = GroundFrom = SkyFrom = FaunaFrom = SoundFrom = PropsFrom = StoryFrom = from;
        }
        /// <summary>Stable text id ("sol.mars"). Saves and generated worlds name a body by this, not by the enum.</summary>
        public string Key;
        /// <summary>The physical facts architecture answers to.</summary>
        public EnvironmentTraits Traits;
        /// <summary>World seed for a registered world. Zero on the Sol worlds, whose seeds live in prefs.</summary>
        public int Seed;

        /// <summary>A copy to start a new world from. Arrays are shared with the original.</summary>
        public CelestialBodyProfile Copy() => (CelestialBodyProfile)MemberwiseClone();
        public string DisplayName;
        public string ShortCode;
        public string Briefing;
        public string ArrivalLog;
        public string VictoryLog;
        public string FailLog;
        public string EndlessLog;

        // The fields above stay English: DisplayName and ShortCode also name assets and logs, and the
        // catalog is built once. Show the player these instead (Tools/loc/extract.py collects the
        // literals assigned in CelestialBodyCatalog).
        public string DisplayNameText => Loc.S(DisplayName);
        public string BriefingText => Loc.S(Briefing);
        public string ArrivalLogText => Loc.S(ArrivalLog);
        public string VictoryLogText => Loc.S(VictoryLog);
        public string FailLogText => Loc.S(FailLog);
        public string EndlessLogText => Loc.S(EndlessLog);

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
        [UnityEngine.Tooltip("Big boulder outcrops (landmarks).")]
        public int RockFormationCount = 0;
        [UnityEngine.Tooltip("Points of interest (themed per world: ruins, landers, rovers, rigs, geysers...).")]
        public int PoiCount = 0;
        [UnityEngine.Tooltip("Instanced undergrowth: grass, flowers, pebbles, crystals, ice shards...")]
        public GroundCoverLayer[] GroundCover = new GroundCoverLayer[0];
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

        [Header("Body constraints")]
        public TerrainKit Kit = TerrainKit.Standard;
        public TechId LaunchTech = TechId.None;
        public float ExtractYieldScale = 1f;
        public float FarmYieldScale = 1f;
        public float MineYieldScale = 1f;
        public float PowerDrawScale = 1f;
        public int AsteroidIsletCount = 0;
        public int IcePlateCount = 0;

        [Header("Campaign gates")]
        public int PopulationGoal = 12;
        public float SustainHoldSeconds = 40f;
        public float ResearchRateMultiplier = 1f;

        [Header("Logistics")]
        public float ResupplyIntervalScale = 1f;
        public int ResupplyDockFee = 0;
        public int FreightMetals = 0;
        public int FreightIce = 0;
        public int OutpostPowerDraw = 2;

        [Header("Empty-start stockpile")]
        public int StartRegolith = 90;
        public int StartWaterIce = 55;
        public int StartMetals = 280;
        public int StartPower = 100;

        [Header("Ecology / movement")]
        public float MoveSpeedScale = 1f;
        public float RadiationDrainPerSecond = 0f;
        public float RadiationSafeRadius = 22f;
        public float AmbientThreat = 0.12f;
        public float ExpansionThreat = 0.018f;
        public int CampusFaunaCap = 4;
        public float MiteSpawnWeight = 1f;
        public float LeechSpawnWeight = 1f;
        public float TickSpawnWeight = 0f;
        public float WispSpawnWeight = 0f;
        public float CreeperSpawnWeight = 0f;
        public float HopperSpawnWeight = 0f;
        public float FaunaSpeedScale = 1f;
        public float FaunaAggroScale = 1f;
        public int LairStalkerBudget = 2;
        public bool PreferMineMites;
        /// <summary>Alpha 0 keeps factory mite/leech tints.</summary>
        public Color FaunaMiteTint;
        public Color FaunaLeechTint;
        public Color FaunaCreeperTint;
        public Color FaunaHopperTint;
        public Color FaunaWispTint;
        public Color FaunaTickTint;
    }
}
