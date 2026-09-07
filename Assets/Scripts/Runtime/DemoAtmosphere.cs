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
            ConfigureAmbientAndFog(body, cam);
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
            }

            // Fill always sits roughly opposite the body's key so it lifts the shaded faces
            // instead of stacking on the lit ones (Mars key moved to yaw 148 in pass 2).
            go.transform.rotation = Quaternion.Euler(25f, body.SunEuler.y + 175f, 0f);

            var fill = go.GetComponent<Light>();
            if (fill == null) fill = go.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = body.FillColor;
            fill.intensity = body.Id == CelestialBodyId.Mars ? 0.50f
                : body.Id == CelestialBodyId.Earth ? 0.34f : 0.28f;
            fill.shadows = LightShadows.None;
        }

        /// <summary>
        /// Mars haze starts this far past the camera's focal depth so the campus centre stays
        /// clean and only the far ground picks up the concept's pale orange wash.
        /// </summary>
        public const float MarsHazeStartOffset = 4f;
        /// <summary>Metres from haze start to full haze. Frame top at ortho 10 lands at ~15 %.</summary>
        public const float MarsHazeSpan = 92f;

        /// <summary>
        /// View depth from the camera to where its centre ray meets the ground plane. The iso rig
        /// keeps its height while panning, so this is stable across the session.
        /// </summary>
        public static float FocalGroundDepth(Camera cam, float fallback)
        {
            if (cam == null) return fallback;
            Vector3 fwd = cam.transform.forward;
            if (fwd.y > -0.08f) return fallback;
            float depth = cam.transform.position.y / -fwd.y;
            return depth > 2f && depth < 400f ? depth : fallback;
        }

        private static void ConfigureAmbientAndFog(CelestialBodyProfile body, Camera cam)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = body.AmbientSky;
            RenderSettings.ambientEquatorColor = body.AmbientEquator;
            RenderSettings.ambientGroundColor = body.AmbientGround;
            RenderSettings.ambientIntensity = 1f;

            RenderSettings.fog = true;

            if (body.Id == CelestialBodyId.Mars)
            {
                // Dream Loop pass 2: the concept has a readable depth gradient — near yards crisp,
                // far regolith fading toward a pale orange haze. Exp² fog at horizon density gave
                // the frame nothing at all (≈0 at 50 m). Linear fog anchored just past the focal
                // depth gives the far ground ~15 % haze at ortho 10 while hulls at the focus stay
                // white. Start is camera-relative so the wash does not move when the rig pans.
                float depth = FocalGroundDepth(cam, body.FogStart);
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogColor = body.FogColor;
                RenderSettings.fogStartDistance = depth + MarsHazeStartOffset;
                RenderSettings.fogEndDistance = depth + MarsHazeStartOffset + MarsHazeSpan;
                RenderSettings.fogDensity = FogDensityFor(body);
                return;
            }

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
            return Mathf.Clamp(0.9f / horizon, 0.0015f, 0.02f);
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
            EnsureVignette(profile, body);

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
                // Higher key (36°) puts ~1.7× more light on the ground than the 20° sun did, so
                // exposure comes down and the grade goes slightly dusty to match the concept.
                color.contrast.Override(9f);
                color.saturation.Override(-3f);
                color.postExposure.Override(0.12f);
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

        /// <summary>
        /// Soft corner falloff on Mars only. The authored DemoVolumeProfile bake has no Vignette
        /// component, so it is added here; IMGUI HUD draws after post and is unaffected.
        /// </summary>
        private static void EnsureVignette(VolumeProfile profile, CelestialBodyProfile body)
        {
            if (!profile.TryGet(out Vignette vignette))
                vignette = profile.Add<Vignette>(true);
            bool mars = body.Id == CelestialBodyId.Mars;
            vignette.active = true;
            vignette.intensity.Override(mars ? 0.16f : 0f);
            vignette.smoothness.Override(0.55f);
            vignette.color.Override(new Color(0.16f, 0.07f, 0.04f));
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
