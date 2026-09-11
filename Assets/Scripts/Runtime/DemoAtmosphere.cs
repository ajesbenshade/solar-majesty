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
        /// <summary>
        /// Play-ortho 10 + 30° iso: focus ~44 m, far ground ~61 m. Catalog FogStart/FogEnd
        /// are authored against this frustum as a haze hint, not an opaque wall.
        /// </summary>
        public const float MarsPlayFocusDepth = 44f;
        public const float MarsPlayFarDepth = 61f;

        private static CelestialBodyProfile _appliedBody;

        public static void Apply(Camera cam, Transform groundParent) =>
            Apply(cam, groundParent, CelestialBodyCatalog.Earth());

        public static void Apply(Camera cam, Transform groundParent, CelestialBodyProfile body)
        {
            if (body == null) body = CelestialBodyCatalog.Earth();
            _appliedBody = body;
            ConfigureSun(body);
            EnsureFillLight(groundParent, body);
            ConfigureAmbientAndFog(body);
            ConfigureCamera(cam, body);
            SyncFog(cam, body);
            EnsureVolume(groundParent, body);
            Debug.Log($"[Atmosphere] {body.DisplayName} sun={body.SunIntensity:0.00} fog={RenderSettings.fogStartDistance:0}/{RenderSettings.fogEndDistance:0}");
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
            // Mars concept wants long readable shadows, not hard black plates: shadowed dirt
            // should hold >= 60 % of lit-dirt luminance (dream-loop round 6 Tier 2 gate).
            sun.shadowStrength = body.Id == CelestialBodyId.Mars
                ? 0.46f
                : body.Id == CelestialBodyId.Luna ? 0.90f
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
            fill.intensity = body.Id == CelestialBodyId.Mars ? 0.50f
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

            // Glossy surfaces mirror the environment. The Procedural skybox keeps a Rayleigh-blue
            // zenith even with an orange tint, which put a cool cast on every white hull; Mars
            // reflects a warm dust cubemap instead (salmon horizon, muted ochre zenith).
            if (body.Id == CelestialBodyId.Mars)
            {
                RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;
                RenderSettings.customReflectionTexture = MarsReflectionCube(body);
                RenderSettings.reflectionIntensity = 0.30f;
                DynamicGI.UpdateEnvironment();
            }
            else
            {
                RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Skybox;
                RenderSettings.customReflectionTexture = null;
                RenderSettings.reflectionIntensity = 1f;
            }

            RenderSettings.fog = true;
            RenderSettings.fogColor = body.FogColor;

            if (body.Id == CelestialBodyId.Mars)
            {
                // Linear so the far ground tints without washing hulls. Catalog FogStart/FogEnd
                // are the play-ortho 10 hint; SyncFog stretches them with live camera depth so
                // zoom-out cannot park FogEnd inside the frame (that was the salmon wall).
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogStartDistance = body.FogStart;
                RenderSettings.fogEndDistance = body.FogEnd;
                RenderSettings.fogDensity = FogDensityFor(body);
                return;
            }

            // Exponential-squared fog holds off until the horizon instead of linearly tinting
            // everything past the start distance. Linear fog starting at 28 m was washing the whole
            // campus into one hue, which is why white hulls rendered orange in the Mars stills.
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = FogDensityFor(body);

            // Kept in sync so anything reading linear fog (or a quality tier that forces it) agrees.
            RenderSettings.fogStartDistance = Mathf.Max(body.FogStart, body.FogEnd * 0.45f);
            RenderSettings.fogEndDistance = body.FogEnd;
        }

        /// <summary>
        /// Stretch Mars Linear fog to the current ortho frustum. Call after zoom / orbit / lift
        /// so the far edge of the *visible* ground stays a haze hint instead of FogColor.
        /// </summary>
        public static void SyncFog(Camera cam) => SyncFog(cam, _appliedBody);

        public static void SyncFog(Camera cam, CelestialBodyProfile body)
        {
            if (cam == null || body == null) return;
            if (body.Id != CelestialBodyId.Mars) return;
            if (!cam.orthographic) return;

            ComputeMarsLinearFog(
                body,
                cam.transform.position.y,
                cam.transform.forward.y,
                cam.transform.up.y,
                cam.orthographicSize,
                out float fogStart,
                out float fogEnd);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;
        }

        /// <summary>
        /// Map catalog FogStart/FogEnd (play-ortho 10) onto a live iso view. Keeps the same
        /// far-edge haze fraction at every zoom: campus/focus stays clear, top of frame is a
        /// hint, never opaque. cameraForwardY is transform.forward.y (negative when looking down).
        /// </summary>
        public static void ComputeMarsLinearFog(
            CelestialBodyProfile body,
            float cameraY,
            float cameraForwardY,
            float cameraUpY,
            float orthoSize,
            out float fogStart,
            out float fogEnd)
        {
            float catalogStart = body != null ? body.FogStart : 48f;
            float catalogEnd = body != null ? body.FogEnd : 95f;
            float sinPitch = Mathf.Max(0.08f, -cameraForwardY);
            float h = Mathf.Max(0.5f, cameraY);
            float ortho = Mathf.Max(0.5f, orthoSize);
            float depthFocus = h / sinPitch;
            float depthFar = (h + cameraUpY * ortho) / sinPitch;

            // Fog factor at the top of the play-ortho frame. Clamp so a stale FogEnd=61 catalog
            // still cannot saturate the live far edge.
            float playHint = Mathf.Clamp(
                (MarsPlayFarDepth - catalogStart) / Mathf.Max(8f, catalogEnd - catalogStart),
                0.12f, 0.40f);
            float startPad = catalogStart - MarsPlayFocusDepth;
            fogStart = depthFocus + startPad;
            float far = Mathf.Max(depthFar, fogStart + 4f);
            fogEnd = fogStart + (far - fogStart) / playHint;
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

        private static Cubemap _marsReflection;

        /// <summary>Small warm dust-sky cubemap: horizon = fog colour, zenith = muted ochre.</summary>
        private static Cubemap MarsReflectionCube(CelestialBodyProfile body)
        {
            if (_marsReflection != null) return _marsReflection;
            const int size = 16;
            var cube = new Cubemap(size, TextureFormat.RGBA32, false) { name = "SM_MarsReflection" };
            Color zenith = new Color(0.62f, 0.40f, 0.24f);
            Color horizon = body.FogColor;
            Color ground = Color.Lerp(body.GroundLight, body.GroundDark, 0.5f);
            var px = new Color[size * size];
            for (int f = 0; f < 6; f++)
            {
                var face = (CubemapFace)f;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    Vector3 dir = face switch
                    {
                        CubemapFace.PositiveX => new Vector3(1f, -v, -u),
                        CubemapFace.NegativeX => new Vector3(-1f, -v, u),
                        CubemapFace.PositiveY => new Vector3(u, 1f, v),
                        CubemapFace.NegativeY => new Vector3(u, -1f, -v),
                        CubemapFace.PositiveZ => new Vector3(u, -v, 1f),
                        _ => new Vector3(-u, -v, -1f)
                    };
                    dir.Normalize();
                    Color c = dir.y >= 0f
                        ? Color.Lerp(horizon, zenith, Mathf.Pow(dir.y, 0.7f))
                        : Color.Lerp(horizon, ground, Mathf.Pow(-dir.y, 0.5f));
                    c.a = 1f;
                    px[y * size + x] = c;
                }
                cube.SetPixels(px, face);
            }
            cube.Apply();
            _marsReflection = cube;
            return cube;
        }

        private static void ConfigureCamera(Camera cam, CelestialBodyProfile body)
        {
            if (cam == null) return;
            // Mars: salmon haze clear (not dark void) so the upper ortho band never goes black.
            cam.backgroundColor = body != null && body.Id == CelestialBodyId.Mars
                ? PlanetaryMapDressing.VoidFillColor(body)
                : PlanetaryMapDressing.VoidFillColor(body);
            cam.farClipPlane = Mathf.Max(cam.farClipPlane, 2000f);
            if (body != null && body.Id == CelestialBodyId.Mars)
                cam.clearFlags = CameraClearFlags.SolidColor;
            else if (cam.clearFlags != CameraClearFlags.Skybox)
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
                color.contrast.Override(11f);
                color.saturation.Override(4f);
                color.postExposure.Override(0.22f);
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
