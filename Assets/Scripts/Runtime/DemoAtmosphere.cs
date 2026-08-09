using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SolarMajesty
{
    /// <summary>
    /// Phase 3: runtime lunar lighting + fog + ground tint + light URP Volume grade.
    /// </summary>
    public static class DemoAtmosphere
    {
        private static readonly Color SunColor = new Color(1f, 0.94f, 0.86f);
        private static readonly Color FillColor = new Color(0.35f, 0.48f, 0.72f);
        private static readonly Color AmbientSky = new Color(0.28f, 0.32f, 0.42f);
        private static readonly Color AmbientEquator = new Color(0.22f, 0.2f, 0.18f);
        private static readonly Color AmbientGround = new Color(0.12f, 0.1f, 0.09f);
        private static readonly Color FogColor = new Color(0.55f, 0.52f, 0.48f);
        private static readonly Color GroundColor = new Color(0.48f, 0.44f, 0.38f);
        private static readonly Color SkyClear = new Color(0.06f, 0.08f, 0.14f);

        public static void Apply(Camera cam, Transform groundParent)
        {
            ConfigureSun();
            EnsureFillLight(groundParent);
            ConfigureAmbientAndFog();
            TintGround();
            ConfigureCamera(cam);
            EnsureVolume(groundParent);
        }

        private static void ConfigureSun()
        {
            Light sun = null;
            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
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

            sun.color = SunColor;
            sun.intensity = 1.45f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.78f;
            sun.shadowBias = 0.04f;
            sun.shadowNormalBias = 0.6f;
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            sun.name = "Directional Light";
        }

        private static void EnsureFillLight(Transform parent)
        {
            if (GameObject.Find("Fill Light") != null) return;
            var go = new GameObject("Fill Light");
            if (parent != null) go.transform.SetParent(parent, false);
            var fill = go.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = FillColor;
            fill.intensity = 0.28f;
            fill.shadows = LightShadows.None;
            go.transform.rotation = Quaternion.Euler(25f, 140f, 0f);
        }

        private static void ConfigureAmbientAndFog()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = AmbientSky;
            RenderSettings.ambientEquatorColor = AmbientEquator;
            RenderSettings.ambientGroundColor = AmbientGround;
            RenderSettings.ambientIntensity = 1f;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogStartDistance = 28f;
            RenderSettings.fogEndDistance = 95f;
        }

        private static void TintGround()
        {
            var ground = GameObject.Find("GroundPlane");
            if (ground == null) return;
            var rend = ground.GetComponent<Renderer>();
            if (rend == null) return;
            if (rend.material.HasProperty("_BaseColor"))
                rend.material.SetColor("_BaseColor", GroundColor);
            else if (rend.material.HasProperty("_Color"))
                rend.material.color = GroundColor;
        }

        private static void ConfigureCamera(Camera cam)
        {
            if (cam == null) return;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = SkyClear;
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

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "DemoVolumeProfile";

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

            volume.sharedProfile = profile;
        }
    }
}
