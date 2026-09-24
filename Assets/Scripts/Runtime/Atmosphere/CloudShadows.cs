using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SolarMajesty
{
    /// <summary>
    /// Drifting cloud shadows: a generated, tileable noise cookie on the sun. Earth gets
    /// cumulus-sized patches; Mars gets a faint dust veil; airless worlds get none. Cheap (one
    /// cookie sample in the lit shader) and it makes the whole map feel alive.
    /// </summary>
    public sealed class CloudShadows : MonoBehaviour
    {
        private static Texture2D _earthCookie;
        private static Texture2D _marsCookie;

        private UniversalAdditionalLightData _urp;
        private Vector2 _wind;
        private Vector2 _offset;

        private static Light _lastSun;
        private static CelestialBodyProfile _lastBody;

        /// <summary>Re-apply after the setting is toggled.</summary>
        public static void Refresh() => Apply(_lastSun, _lastBody);

        public static void Apply(Light sun, CelestialBodyProfile body)
        {
            if (sun == null) return;
            _lastSun = sun;
            _lastBody = body;
            var drift = sun.GetComponent<CloudShadows>();
            Texture2D cookie = null;
            float size = 0f;
            Vector2 wind = Vector2.zero;
            if (DemoSettings.CloudShadows && body != null)
            {
                if (body.Id == CelestialBodyId.Earth)
                {
                    cookie = _earthCookie != null ? _earthCookie : (_earthCookie = Build("SM_EarthCloudCookie", 1337, 0.50f, 0.70f, 0.50f));
                    size = 96f;
                    wind = new Vector2(1.6f, 0.7f);
                }
                else if (body.Id == CelestialBodyId.Mars)
                {
                    cookie = _marsCookie != null ? _marsCookie : (_marsCookie = Build("SM_MarsDustCookie", 4242, 0.40f, 0.80f, 0.82f));
                    size = 150f;
                    wind = new Vector2(2.4f, -0.9f);
                }
            }

            sun.cookie = cookie;
            var urp = sun.GetComponent<UniversalAdditionalLightData>();
            if (cookie == null)
            {
                if (drift != null) drift.enabled = false;
                return;
            }

            if (urp == null) urp = sun.gameObject.AddComponent<UniversalAdditionalLightData>();
            urp.lightCookieSize = new Vector2(size, size);
            if (drift == null) drift = sun.gameObject.AddComponent<CloudShadows>();
            drift._urp = urp;
            drift._wind = wind;
            drift.enabled = true;
        }

        private void Update()
        {
            if (_urp == null) return;
            // Mission time, so clouds hold still while paused and speed up with fast-forward.
            _offset += _wind * Time.deltaTime;
            Vector2 size = _urp.lightCookieSize;
            if (size.x > 0f) _offset.x = Mathf.Repeat(_offset.x, size.x);
            if (size.y > 0f) _offset.y = Mathf.Repeat(_offset.y, size.y);
            _urp.lightCookieOffset = _offset;
        }

        /// <summary>
        /// Tileable fractal value noise thresholded into soft cloud blobs.
        /// <paramref name="lo"/>/<paramref name="hi"/> set coverage; <paramref name="shade"/> is the
        /// light left under a cloud (1 = no shadow).
        /// </summary>
        private static Texture2D Build(string name, int seed, float lo, float hi, float shade)
        {
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            var px = new Color32[size * size];
            var rng = new System.Random(seed);
            int[] freqs = { 3, 6, 12, 24 };
            float[] weights = { 0.53f, 0.27f, 0.13f, 0.07f };
            var lattices = new float[freqs.Length][];
            for (int o = 0; o < freqs.Length; o++)
            {
                int f = freqs[o];
                lattices[o] = new float[f * f];
                for (int i = 0; i < f * f; i++) lattices[o][i] = (float)rng.NextDouble();
            }

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float n = 0f;
                for (int o = 0; o < freqs.Length; o++)
                    n += weights[o] * Periodic(lattices[o], freqs[o], (float)x / size, (float)y / size);
                float t = Mathf.Clamp01((n - lo) / Mathf.Max(0.001f, hi - lo));
                float cloud = t * t * (3f - 2f * t);
                byte v = (byte)Mathf.RoundToInt(Mathf.Lerp(1f, shade, cloud) * 255f);
                px[y * size + x] = new Color32(v, v, v, 255);
            }
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }

        private static float Periodic(float[] lattice, int f, float u, float v)
        {
            float x = u * f, y = v * f;
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float tx = x - x0, ty = y - y0;
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);
            int xa = ((x0 % f) + f) % f, xb = (xa + 1) % f;
            int ya = ((y0 % f) + f) % f, yb = (ya + 1) % f;
            float a = lattice[ya * f + xa], b = lattice[ya * f + xb];
            float c = lattice[yb * f + xa], d = lattice[yb * f + xb];
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }
    }
}
