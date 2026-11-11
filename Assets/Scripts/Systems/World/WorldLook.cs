using UnityEngine;

namespace SolarMajesty
{
    /// <summary>Post-processing cost band. Mirrors the four URP assets in Assets/Settings.</summary>
    public enum LookTier
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Ultra = 3
    }

    /// <summary>
    /// One world's post-processing grade. Plain data: <c>LookVolume</c> writes every field onto a
    /// URP volume profile, so two profiles built from two grades always override the same set of
    /// parameters (a higher-priority volume can then fully replace a lower one).
    /// The colour filter is not here: it stays on <see cref="CelestialBodyProfile.GradeFilter"/>.
    /// </summary>
    public struct WorldGrade
    {
        /// <summary>True = ACES, false = Neutral.</summary>
        public bool Aces;
        public float PostExposure;
        public float Contrast;
        public float Saturation;
        public float Temperature;
        public float Tint;

        /// <summary>Gamma-space threshold, as URP's Bloom takes it (1.1 is about 1.23 linear).</summary>
        public float BloomThreshold;
        public float BloomIntensity;
        public float BloomScatter;
        public Color BloomTint;

        public float VignetteIntensity;
        public float VignetteSmoothness;
        public Color VignetteColor;
    }

    /// <summary>
    /// The look targets, per world: Earth warm-neutral, Luna cool and desaturated with deep
    /// blacks, Mars dusty warm. Pure data and math so EditMode tests can hold the contract.
    /// </summary>
    public static class WorldLook
    {
        /// <summary>
        /// Quality level (index into QualitySettings.names) to cost band. Matches
        /// QualityTiersSetup.LevelAssets: Very Low / Low share the Low pipeline, High / Very High
        /// share High, the last level is Ultra.
        /// </summary>
        public static LookTier TierFor(int qualityLevel, int levelCount)
        {
            if (levelCount <= 1 || qualityLevel >= levelCount - 1) return LookTier.Ultra;
            if (qualityLevel <= 1) return LookTier.Low;
            if (qualityLevel == 2) return LookTier.Medium;
            return LookTier.High;
        }

        public static WorldGrade For(CelestialBodyId id)
        {
            switch (id)
            {
                case CelestialBodyId.Earth:
                    return new WorldGrade
                    {
                        Aces = true,
                        PostExposure = 0.10f,
                        Contrast = 12f,
                        Saturation = 10f,
                        Temperature = 4f,
                        Tint = 0f,
                        // Air scatters: a soft, slightly warm glow on sunlit white and windows.
                        BloomThreshold = 1.10f,
                        BloomIntensity = 0.18f,
                        BloomScatter = 0.62f,
                        BloomTint = new Color(1f, 0.98f, 0.94f),
                        VignetteIntensity = 0.20f,
                        VignetteSmoothness = 0.45f,
                        VignetteColor = new Color(0.04f, 0.05f, 0.07f)
                    };
                case CelestialBodyId.Luna:
                    return new WorldGrade
                    {
                        Aces = true,
                        PostExposure = 0f,
                        Contrast = 18f,
                        Saturation = -14f,
                        Temperature = -6f,
                        Tint = 0f,
                        // No air: glints stay tight and cold; black sky lets them carry a bit more.
                        BloomThreshold = 1.05f,
                        BloomIntensity = 0.24f,
                        BloomScatter = 0.50f,
                        BloomTint = new Color(0.92f, 0.96f, 1f),
                        VignetteIntensity = 0.26f,
                        VignetteSmoothness = 0.40f,
                        VignetteColor = new Color(0.01f, 0.02f, 0.05f)
                    };
                case CelestialBodyId.Mars:
                    return new WorldGrade
                    {
                        Aces = true,
                        PostExposure = 0.22f,
                        Contrast = 12f,
                        Saturation = -2f,
                        // Pulls the orange cast out of the neutrals so cream hulls stay cream.
                        Temperature = -16f,
                        Tint = -6f,
                        // Sunlit cream hulls sit near 1.0-1.4 linear here: a higher threshold keeps
                        // the campus from going milky; wide scatter reads as dust in the air.
                        BloomThreshold = 1.20f,
                        BloomIntensity = 0.14f,
                        BloomScatter = 0.70f,
                        BloomTint = new Color(1f, 0.93f, 0.84f),
                        VignetteIntensity = 0.20f,
                        VignetteSmoothness = 0.45f,
                        VignetteColor = new Color(0.09f, 0.05f, 0.03f)
                    };
                default:
                {
                    // Belt / Europa (parked): the previous shared grade, plus their white balance.
                    var g = new WorldGrade
                    {
                        Aces = true,
                        PostExposure = 0.05f,
                        Contrast = 8f,
                        Saturation = 6f,
                        BloomThreshold = 1.10f,
                        BloomIntensity = 0.20f,
                        BloomScatter = 0.55f,
                        BloomTint = Color.white,
                        VignetteIntensity = 0.22f,
                        VignetteSmoothness = 0.40f,
                        VignetteColor = new Color(0.05f, 0.06f, 0.10f)
                    };
                    if (id == CelestialBodyId.Europa) { g.Temperature = 8f; g.Tint = 2f; }
                    else if (id == CelestialBodyId.Belt) { g.Temperature = 4f; }
                    return g;
                }
            }
        }

        /// <summary>
        /// Title orrery. Everything there is unlit, so Neutral tonemapping (near identity below
        /// the shoulder) keeps the authored textures; only the over-white sun reaches the bloom.
        /// </summary>
        public static WorldGrade Title => new WorldGrade
        {
            Aces = false,
            PostExposure = 0f,
            Contrast = 6f,
            Saturation = 4f,
            Temperature = 0f,
            Tint = 0f,
            BloomThreshold = 1.0f,
            BloomIntensity = 0.35f,
            BloomScatter = 0.70f,
            BloomTint = Color.white,
            VignetteIntensity = 0.30f,
            VignetteSmoothness = 0.50f,
            VignetteColor = new Color(0.01f, 0.01f, 0.03f)
        };

        // ---- per-tier bloom cost ----------------------------------------------------------------

        /// <summary>Bloom pyramid depth. Fewer passes on the cheap tiers.</summary>
        public static int BloomIterations(LookTier tier) =>
            tier == LookTier.Low ? 3 : tier == LookTier.Medium ? 4 : 6;

        /// <summary>Low starts the bloom pyramid at quarter resolution instead of half.</summary>
        public static bool BloomQuarterRes(LookTier tier) => tier == LookTier.Low;

        /// <summary>Bicubic upsampling of the bloom pyramid: Ultra only.</summary>
        public static bool BloomHighQuality(LookTier tier) => tier == LookTier.Ultra;

        // ---- title depth of field ---------------------------------------------------------------

        /// <summary>The title's far-field blur is skipped on the Low tier.</summary>
        public static bool TitleDepthOfField(LookTier tier) => tier != LookTier.Low;

        /// <summary>
        /// Gaussian (far-only) depth of field for the orrery. The camera sits about 20 m from the
        /// sun (20.3 m), so the far side of Mars' orbit is at about 33.8 m: the inner system and
        /// anything on the near side stays sharp, the far arc of the Belt / Jupiter orbits and
        /// the star shell soften.
        /// </summary>
        public const float TitleDofStart = 34f;
        public const float TitleDofEnd = 64f;
        public const float TitleDofMaxRadius = 0.8f;

        // ---- per-world light shaping (read by DemoAtmosphere) -----------------------------------

        /// <summary>
        /// Sun shadow strength. Mars is thick with bounce (shadowed dirt must hold >= 60 % of lit
        /// luminance); Luna has no air to fill a shadow, so it goes near-black.
        /// </summary>
        public static float SunShadowStrength(CelestialBodyId id)
        {
            switch (id)
            {
                case CelestialBodyId.Mars: return 0.46f;
                case CelestialBodyId.Luna: return 0.94f;
                case CelestialBodyId.Earth: return 0.84f;
                default: return 0.72f;
            }
        }

        /// <summary>Shadowless fill light intensity.</summary>
        public static float FillIntensity(CelestialBodyId id)
        {
            switch (id)
            {
                case CelestialBodyId.Mars: return 0.50f;
                case CelestialBodyId.Earth: return 0.34f;
                default: return 0.28f;
            }
        }
    }
}
