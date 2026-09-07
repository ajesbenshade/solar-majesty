using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SolarMajesty
{
    /// <summary>
    /// Runtime lighting + fog + URP Volume grade, driven by a celestial body profile.
    /// Ground albedo is owned by <see cref="PlanetaryMapDressing"/> — do not tint it here.
    /// </summary>
    public static class DemoAtmosphere
    {
        public static void Apply(Camera cam, Transform groundParent) =>
            Apply(cam, groundParent, CelestialBodyCatalog.Earth());

        public static void Apply(Camera cam, Transform groundParent, CelestialBodyProfile body)
        {
            if (body == null) body = CelestialBodyCatalog.Earth();
            ConfigureSun(body);
            EnsureFillLight(groundParent, body);
            ConfigureAmbientAndFog(body);
            ConfigureCamera(cam, body);
            EnsureVolume(groundParent, body);
            Debug.Log($"[Atmosphere] {body.DisplayName} sun={body.SunIntensity:0.00} fog={body.FogStart:0}/{body.FogEnd:0}");
        }

        private static void ConfigureSun(CelestialBodyProfile body)
        {
            Light sun = null;
            var lights = Object.FindObjectsByType<Light>();
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null && lights[i].type == LightType.Directional && lights[i].name != "Fill Light")
                {
                    sun = lights[i];
                    break;
                }
            }

            if (sun == null)
            {
                var go = new GameObject("Directional Light");
                sun = go.AddComponent<Light>();
                sun.type = LightType.Directional;
            }

            sun.color = body.SunColor;
            sun.intensity = body.SunIntensity;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = body.Id == CelestialBodyId.Mars || body.Id == CelestialBodyId.Luna
                ? 0.90f
                : body.Id == CelestialBodyId.Earth ? 0.84f : 0.72f;
            sun.shadowBias = 0.04f;
            sun.shadowNormalBias = 0.55f;
            sun.shadowNearPlane = 0.2f;
            sun.transform.rotation = Quaternion.Euler(body.SunEuler);
            sun.name = "Directional Light";
        }

        private static void EnsureFillLight(Transform parent, CelestialBodyProfile body)
        {
            var existing = GameObject.Find("Fill Light");
            GameObject go = existing;
            if (go == null)
            {
                go = new GameObject("Fill Light");
                if (parent != null) go.transform.SetParent(parent, false);
                go.AddComponent<Light>();
                go.transform.rotation = Quaternion.Euler(25f, 140f, 0f);
            }

            var fill = go.GetComponent<Light>();
            if (fill == null) fill = go.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = body.FillColor;
            fill.intensity = body.Id == CelestialBodyId.Mars ? 0.40f
                : body.Id == CelestialBodyId.Earth ? 0.34f : 0.28f;
            fill.shadows = LightShadows.None;
        }

        private static void ConfigureAmbientAndFog(CelestialBodyProfile body)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = body.AmbientSky;
            RenderSettings.ambientEquatorColor = body.AmbientEquator;
            RenderSettings.ambientGroundColor = body.AmbientGround;
            RenderSettings.ambientIntensity = 1f;

            RenderSettings.fog = true;

            // Exponential-squared fog holds off until the horizon instead of linearly tinting
            // everything past the start distance. Linear fog starting at 28 m was washing the whole
            // campus into one hue, which is why white hulls rendered orange in the Mars stills.
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = body.FogColor;
            RenderSettings.fogDensity = FogDensityFor(body);

            // Kept in sync so anything reading linear fog (or a quality tier that forces it) agrees.
            RenderSettings.fogStartDistance = Mathf.Max(body.FogStart, body.FogEnd * 0.45f);
            RenderSettings.fogEndDistance = body.FogEnd;
        }

        /// <summary>
        /// Density chosen so fog reaches roughly half strength at the body's FogEnd, keeping the
        /// campus itself unfogged while the far vista still recedes.
        /// </summary>
        private static float FogDensityFor(CelestialBodyProfile body)
        {
            float horizon = Mathf.Max(60f, body.FogEnd);
            float reach = body.Id == CelestialBodyId.Mars ? 1.18f : 0.9f;
            return Mathf.Clamp(reach / horizon, 0.0015f, 0.02f);
        }

        private static void ConfigureCamera(Camera cam, CelestialBodyProfile body)
        {
            if (cam == null) return;
            // Void fill must match terrain — SkyTop blue/`Default-Skybox` mustard show when
            // ortho zoom puts ray origins under the ground plane (see IsometricCameraController).
            cam.backgroundColor = PlanetaryMapDressing.VoidFillColor(body);
            cam.farClipPlane = Mathf.Max(cam.farClipPlane, 2000f);
            if (cam.clearFlags != CameraClearFlags.Skybox)
                cam.clearFlags = CameraClearFlags.SolidColor;

            var additional = cam.GetComponent<UniversalAdditionalCameraData>();
            if (additional == null) additional = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            additional.renderPostProcessing = true;
        }

        private static void EnsureVolume(Transform parent, CelestialBodyProfile body)
        {
            var existing = GameObject.Find("DemoVolume");
            Volume volume;
            if (existing == null)
            {
                var go = new GameObject("DemoVolume");
                if (parent != null) go.transform.SetParent(parent, false);
                volume = go.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 1f;
                var authored = Resources.Load<VolumeProfile>("Atmosphere/DemoVolumeProfile");
                volume.sharedProfile = authored != null ? authored : BuildRuntimeProfile();
            }
            else
            {
                volume = existing.GetComponent<Volume>();
            }

            GradeVolume(volume, body);
        }

        private static void GradeVolume(Volume volume, CelestialBodyProfile body)
        {
            if (volume == null || body == null) return;
            var profile = volume.profile;
            if (profile == null) return;

            EnsureTonemapping(profile);
            EnsureWhiteBalance(profile, body);

            if (!profile.TryGet(out ColorAdjustments color))
                color = profile.Add<ColorAdjustments>(true);
            color.colorFilter.Override(body.GradeFilter);
            if (body.Id == CelestialBodyId.Earth)
            {
                color.contrast.Override(12f);
                color.saturation.Override(14f);
                color.postExposure.Override(0.10f);
            }
            else if (body.Id == CelestialBodyId.Mars)
            {
                // Locked concept is dusty and filmic, not the Capture's clipped white/cyan
                // highlights against near-black shadows.
                color.contrast.Override(6f);
                color.saturation.Override(-2f);
                color.postExposure.Override(0.04f);

                if (!profile.TryGet(out Bloom bloom))
                    bloom = profile.Add<Bloom>(true);
                bloom.active = true;
                bloom.threshold.Override(1.15f);
                bloom.intensity.Override(0.10f);
                bloom.scatter.Override(0.46f);

                if (!profile.TryGet(out Vignette vignette))
                    vignette = profile.Add<Vignette>(true);
                vignette.active = true;
                vignette.intensity.Override(0.14f);
                vignette.smoothness.Override(0.34f);
            }
            else
            {
                color.contrast.Override(8f);
                color.saturation.Override(6f);
                color.postExposure.Override(0.05f);
            }
        }

        /// <summary>
        /// Without a tonemapper, HDR values above 1 clip per channel, which turns a lit white hull
        /// under a warm sun into flat orange. ACES rolls the highlights off and keeps hue.
        /// </summary>
        private static void EnsureTonemapping(VolumeProfile profile)
        {
            if (!profile.TryGet(out Tonemapping tonemapping))
                tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.active = true;
            tonemapping.mode.Override(TonemappingMode.ACES);
        }

        /// <summary>
        /// Pulls the body's colour cast out of the neutrals so ceramic hulls read as white against
        /// the local ground instead of taking its hue. This is the difference between "on Mars"
        /// and "everything is orange".
        /// </summary>
        private static void EnsureWhiteBalance(VolumeProfile profile, CelestialBodyProfile body)
        {
            if (!profile.TryGet(out WhiteBalance balance))
                balance = profile.Add<WhiteBalance>(true);
            balance.active = true;

            switch (body.Id)
            {
                case CelestialBodyId.Mars:
                    balance.temperature.Override(-16f);
                    balance.tint.Override(-6f);
                    break;
                case CelestialBodyId.Europa:
                    balance.temperature.Override(8f);
                    balance.tint.Override(2f);
                    break;
                case CelestialBodyId.Belt:
                    balance.temperature.Override(4f);
                    balance.tint.Override(0f);
                    break;
                default:
                    balance.temperature.Override(0f);
                    balance.tint.Override(0f);
                    break;
            }
        }

        private static VolumeProfile BuildRuntimeProfile()
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "DemoVolumeProfile_Runtime";

            var color = profile.Add<ColorAdjustments>(true);
            color.contrast.Override(8f);
            color.saturation.Override(6f);
            color.postExposure.Override(0.05f);
            color.colorFilter.Override(new Color(1f, 0.97f, 0.93f));

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.22f);
            bloom.scatter.Override(0.55f);

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.28f);
            vignette.smoothness.Override(0.4f);
            vignette.color.Override(new Color(0.05f, 0.06f, 0.1f));

            return profile;
        }
    }
}
