using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SolarMajesty
{
    /// <summary>
    /// Drives the sun through <see cref="SunPath"/> on mission time: golden hours with long
    /// shadows, a cool blue-hour night where emissive windows and pad lights bloom. Scales the
    /// body's tuned ambient / fill / fog / clear colour instead of replacing them, and returns to
    /// the tuned look exactly while a still is being captured or the setting is off.
    /// Presentation only.
    /// </summary>
    public sealed class SunCycle : MonoBehaviour
    {
        private static SunCycle _instance;

        private CelestialBodyProfile _body;
        private Light _sun;
        private Light _fill;
        private Camera _cam;
        private GameLoop _loop;
        private Bloom _bloom;

        private float _fillBase;
        private Color _ambSky, _ambEquator, _ambGround, _fogBase, _clearBase;
        private float _bloomBase;
        private Material _skyMat;
        private float _skyExposureBase = 1f;

        /// <summary>Last applied state (read by sky / focus effects).</summary>
        public static SunState Current { get; private set; }

        /// <summary>Called by <see cref="DemoAtmosphere.Apply"/> after it sets the body's tuned lighting.</summary>
        public static void Configure(CelestialBodyProfile body, Light sun, Light fill, Camera cam, Volume volume)
        {
            if (_instance == null)
            {
                var go = new GameObject("SunCycle");
                _instance = go.AddComponent<SunCycle>();
            }
            var s = _instance;
            s._body = body;
            s._sun = sun;
            s._fill = fill;
            s._cam = cam;
            s._fillBase = fill != null ? fill.intensity : 0f;
            s._ambSky = RenderSettings.ambientSkyColor;
            s._ambEquator = RenderSettings.ambientEquatorColor;
            s._ambGround = RenderSettings.ambientGroundColor;
            s._fogBase = RenderSettings.fogColor;
            s._clearBase = cam != null ? cam.backgroundColor : Color.black;
            s._bloom = null;
            if (volume != null && volume.profile != null && volume.profile.TryGet(out Bloom bloom))
            {
                s._bloom = bloom;
                s._bloomBase = bloom.intensity.value;
            }
            Current = SunPath.Evaluate(body, 0);
        }

        /// <summary>The tuned clear colour after a camera / void-fill change re-asserted it.</summary>
        public static void RebaseClearColor(Camera cam)
        {
            if (_instance != null && cam != null && cam == _instance._cam)
                _instance._clearBase = cam.backgroundColor;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void LateUpdate()
        {
            if (_body == null || _sun == null) return;
            if (_loop == null) _loop = FindAnyObjectByType<GameLoop>();
            // Title / settings screens own their own lighting.
            if (_loop == null || (_loop.Screen != DemoScreen.Playing && _loop.Screen != DemoScreen.Paused)) return;

            bool live = DemoSettings.DayCycle && !StillCaptureHold.Active && _loop.Mission != null;
            double t = live ? _loop.Mission.MissionElapsed : 0.0;
            var s = SunPath.Evaluate(_body, t);
            Current = s;

            _sun.transform.rotation = Quaternion.Euler(s.Euler);
            _sun.color = s.Color;
            _sun.intensity = _body.SunIntensity * s.Intensity;
            if (_fill != null) _fill.intensity = _fillBase * Mathf.Lerp(1f, 1.35f, s.Night) * Mathf.Lerp(1f, 0.8f, s.Golden);

            // Night cools the ambient; golden hour warms it slightly on worlds with air.
            Color nightTint = Color.Lerp(Color.white, new Color(0.62f, 0.72f, 1f), s.Night);
            Color warm = HasAir(_body) ? Color.Lerp(Color.white, new Color(1f, 0.90f, 0.80f), s.Golden * 0.6f) : Color.white;
            Color tint = nightTint * warm * s.Ambient;
            RenderSettings.ambientSkyColor = _ambSky * tint;
            RenderSettings.ambientEquatorColor = _ambEquator * tint;
            RenderSettings.ambientGroundColor = _ambGround * tint;

            float fogK = Mathf.Lerp(1f, 0.42f, s.Night);
            RenderSettings.fogColor = Opaque(_fogBase * warm * nightTint * fogK);
            if (_cam != null && _cam.clearFlags == CameraClearFlags.SolidColor)
                _cam.backgroundColor = Opaque(_clearBase * warm * nightTint * fogK);

            // Emissive windows and pad rings carry the night: bloom a little harder.
            if (_bloom != null) _bloom.intensity.Override(_bloomBase * (1f + 1.6f * s.Night));

            var sky = RenderSettings.skybox;
            if (sky != null && sky.HasProperty("_Exposure"))
            {
                if (sky != _skyMat)
                {
                    // New sky (body hop, diorama toggle): its authored exposure is the baseline.
                    _skyMat = sky;
                    _skyExposureBase = sky.GetFloat("_Exposure");
                }
                sky.SetFloat("_Exposure", _skyExposureBase * Mathf.Lerp(1f, 0.38f, s.Night));
            }
        }

        private static bool HasAir(CelestialBodyProfile body) =>
            body != null && (body.Id == CelestialBodyId.Earth || body.Id == CelestialBodyId.Mars);

        private static Color Opaque(Color c)
        {
            c.a = 1f;
            return c;
        }
    }
}
