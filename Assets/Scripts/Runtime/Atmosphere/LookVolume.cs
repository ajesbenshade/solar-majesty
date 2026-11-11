using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SolarMajesty
{
    /// <summary>
    /// Writes a <see cref="WorldGrade"/> onto a URP volume profile. The single place that decides
    /// which post-processing parameters the game overrides: the world grade volume and the title
    /// volume both go through <see cref="Apply"/>, so the title (higher priority) replaces every
    /// parameter the world grade set and nothing from Mars leaks onto the orrery.
    /// Profiles are built in code; no baked asset is involved.
    /// </summary>
    public static class LookVolume
    {
        /// <summary>The cost band of the active quality level.</summary>
        public static LookTier CurrentTier =>
            WorldLook.TierFor(QualitySettings.GetQualityLevel(), QualitySettings.count);

        /// <summary>An empty runtime profile; <see cref="Apply"/> fills it.</summary>
        public static VolumeProfile NewProfile(string name)
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = name;
            return profile;
        }

        /// <summary>Tonemapping, white balance, colour adjustments, bloom and vignette for one grade.</summary>
        public static void Apply(VolumeProfile profile, in WorldGrade g, Color colorFilter, LookTier tier)
        {
            if (profile == null) return;

            // Without a tonemapper, HDR values above 1 clip per channel, which turns a lit white
            // hull under a warm sun into flat orange. ACES rolls the highlights off and keeps hue.
            var tonemapping = Ensure<Tonemapping>(profile);
            tonemapping.mode.Override(g.Aces ? TonemappingMode.ACES : TonemappingMode.Neutral);

            // Pulls the body's colour cast out of the neutrals so ceramic hulls read as white
            // against the local ground instead of taking its hue.
            var balance = Ensure<WhiteBalance>(profile);
            balance.temperature.Override(g.Temperature);
            balance.tint.Override(g.Tint);

            var color = Ensure<ColorAdjustments>(profile);
            color.colorFilter.Override(colorFilter);
            color.postExposure.Override(g.PostExposure);
            color.contrast.Override(g.Contrast);
            color.saturation.Override(g.Saturation);

            var bloom = Ensure<Bloom>(profile);
            bloom.threshold.Override(g.BloomThreshold);
            bloom.intensity.Override(g.BloomIntensity);
            bloom.scatter.Override(g.BloomScatter);
            bloom.tint.Override(g.BloomTint);

            var vignette = Ensure<Vignette>(profile);
            vignette.intensity.Override(g.VignetteIntensity);
            vignette.smoothness.Override(g.VignetteSmoothness);
            vignette.color.Override(g.VignetteColor);

            ApplyTier(profile, tier);
        }

        /// <summary>The parts of a profile that depend on the quality tier only (bloom cost).</summary>
        public static void ApplyTier(VolumeProfile profile, LookTier tier)
        {
            if (profile == null || !profile.TryGet(out Bloom bloom)) return;
            bloom.highQualityFiltering.Override(WorldLook.BloomHighQuality(tier));
            // Looked up by name: these two are newer than the rest of the Bloom API and this
            // project cannot afford a compile break if a URP update renames them. Missing = the
            // URP default, which is only a cost difference on the Low / Medium tiers.
            TryOverride(bloom, "maxIterations", WorldLook.BloomIterations(tier));
            TryOverride(bloom, "downscale", WorldLook.BloomQuarterRes(tier) ? 1 : 0); // Half = 0, Quarter = 1
        }

        /// <summary>
        /// Title only: far-field Gaussian depth of field (cheap, no near blur, nothing in front of
        /// the focus ever softens). Off on the Low tier.
        /// </summary>
        public static void ApplyTitleFocus(VolumeProfile profile, LookTier tier)
        {
            if (profile == null) return;
            var dof = Ensure<DepthOfField>(profile);
            bool on = WorldLook.TitleDepthOfField(tier);
            dof.mode.Override(on ? DepthOfFieldMode.Gaussian : DepthOfFieldMode.Off);
            dof.gaussianStart.Override(WorldLook.TitleDofStart);
            dof.gaussianEnd.Override(WorldLook.TitleDofEnd);
            dof.gaussianMaxRadius.Override(WorldLook.TitleDofMaxRadius);
            dof.highQualitySampling.Override(tier == LookTier.Ultra);
        }

        private static T Ensure<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (!profile.TryGet(out T component))
                component = profile.Add<T>(true);
            component.active = true;
            return component;
        }

        /// <summary>Override an int or enum volume parameter by field name; silently skips if absent.</summary>
        private static void TryOverride(VolumeComponent component, string field, int value)
        {
            try
            {
                var f = component.GetType().GetField(field, BindingFlags.Public | BindingFlags.Instance);
                if (f == null || !(f.GetValue(component) is VolumeParameter p)) return;
                if (p is VolumeParameter<int> asInt)
                {
                    asInt.Override(value);
                    return;
                }
                var prop = p.GetType().GetProperty("value");
                if (prop == null || !prop.PropertyType.IsEnum) return;
                prop.SetValue(p, System.Enum.ToObject(prop.PropertyType, value));
                p.overrideState = true;
            }
            catch (System.Exception)
            {
                // Presentation only: keep the URP default rather than fail the frame.
            }
        }
    }
}
