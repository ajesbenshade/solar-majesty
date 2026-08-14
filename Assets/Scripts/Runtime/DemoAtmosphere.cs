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
            Apply(cam, groundParent, CelestialBodyCatalog.Luna());

        public static void Apply(Camera cam, Transform groundParent, CelestialBodyProfile body)
        {
            if (body == null) body = CelestialBodyCatalog.Luna();
            ConfigureSun(body);
            EnsureFillLight(groundParent, body);
            ConfigureAmbientAndFog(body);
            ConfigureCamera(cam, body);
            EnsureVolume(groundParent);
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
            sun.shadowStrength = 0.78f;
            sun.shadowBias = 0.04f;
            sun.shadowNormalBias = 0.6f;
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
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
            fill.intensity = 0.28f;
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
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = body.FogColor;
            RenderSettings.fogStartDistance = body.FogStart;
            RenderSettings.fogEndDistance = body.FogEnd;
        }

        private static void ConfigureCamera(Camera cam, CelestialBodyProfile body)
        {
            if (cam == null) return;
            // PlanetaryMapDressing may switch to Skybox after Apply; keep a body-tinted fallback.
            if (cam.clearFlags != CameraClearFlags.Skybox)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = body.SkyTop;
            }
            cam.farClipPlane = Mathf.Max(cam.farClipPlane, 200f);

            var additional = cam.GetComponent<UniversalAdditionalCameraData>();
            if (additional == null) additional = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            additional.renderPostProcessing = true;
        }

        private static void EnsureVolume(Transform parent)
        {
            if (GameObject.Find("DemoVolume") != null) return;

            var go = new GameObject("DemoVolume");
            if (parent != null) go.transform.SetParent(parent, false);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1f;

            var authored = Resources.Load<VolumeProfile>("Atmosphere/DemoVolumeProfile");
            if (authored != null)
            {
                volume.sharedProfile = authored;
                return;
            }

            volume.sharedProfile = BuildRuntimeProfile();
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
