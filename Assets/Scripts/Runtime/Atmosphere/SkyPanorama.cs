using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Rasterises <see cref="SkyPainter"/> into a latitude/longitude texture and shows it through
    /// Skybox/Panoramic while the diorama camera can see the sky. The classic orthographic view
    /// never shows sky, so its skybox (and the reflections baked from it) are left untouched and
    /// restored on uninstall.
    /// </summary>
    public static class SkyPanorama
    {
        public const string ShaderName = "Skybox/Panoramic";

        private static Texture2D _tex;
        private static Material _mat;
        private static CelestialBodyId _builtFor;
        private static Material _savedSky;
        private static bool _installed;
        private static bool _shaderMissing;

        public static bool Installed => _installed;

        /// <summary>Shows this body's sky. False (and nothing changes) if the shader is unavailable.</summary>
        public static bool Install(Camera cam, CelestialBodyProfile body)
        {
            if (body == null || _shaderMissing) return false;
            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                _shaderMissing = true; // warn once; the diorama camera still works, just without sky art
                Debug.LogWarning($"[Sky] {ShaderName} missing — run Solar Majesty → Render → Configure URP For Look Target (adds it to Always Included Shaders).");
                return false;
            }

            if (_tex == null || _builtFor != body.Id)
            {
                if (_tex != null) Object.Destroy(_tex);
                _tex = Build(body);
                _builtFor = body.Id;
            }
            if (_mat == null) _mat = new Material(shader) { name = "SM_SkyPanorama" };
            _mat.SetTexture("_MainTex", _tex);
            if (_mat.HasProperty("_Mapping")) _mat.SetFloat("_Mapping", 1f);      // latitude/longitude
            if (_mat.HasProperty("_ImageType")) _mat.SetFloat("_ImageType", 0f);  // 360°
            if (_mat.HasProperty("_MirrorOnBack")) _mat.SetFloat("_MirrorOnBack", 0f);
            if (_mat.HasProperty("_Layout")) _mat.SetFloat("_Layout", 0f);
            if (_mat.HasProperty("_Rotation")) _mat.SetFloat("_Rotation", 0f);
            if (_mat.HasProperty("_Tint")) _mat.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f, 0.5f));
            if (_mat.HasProperty("_Exposure")) _mat.SetFloat("_Exposure", 1f);

            if (!_installed)
            {
                _savedSky = RenderSettings.skybox;
                _installed = true;
            }
            RenderSettings.skybox = _mat;
            if (cam != null) cam.clearFlags = CameraClearFlags.Skybox;
            return true;
        }

        /// <summary>
        /// Per-frame guard while the diorama camera is live: body hops and void-fill resets
        /// (which force a solid clear on Luna / Mars) must not hide the sky.
        /// </summary>
        public static void EnsureShowing(Camera cam, CelestialBodyProfile body)
        {
            if (body == null || _shaderMissing) return;
            if (!_installed || _mat == null || RenderSettings.skybox != _mat || _builtFor != body.Id)
            {
                Install(cam, body);
                return;
            }
            if (cam != null && cam.clearFlags != CameraClearFlags.Skybox) cam.clearFlags = CameraClearFlags.Skybox;
        }

        /// <summary>Back to the tuned skybox and clear mode for the classic camera.</summary>
        public static void Uninstall(Camera cam, CelestialBodyProfile body)
        {
            if (!_installed) return;
            _installed = false;
            RenderSettings.skybox = _savedSky;
            _savedSky = null;
            if (cam != null && body != null)
                PlanetaryMapDressing.ApplyCameraVoidFill(cam, body, RenderSettings.skybox != null);
        }

        /// <summary>The tuned skybox changed underneath us (body hop): keep it as the restore target.</summary>
        public static void NoteTunedSky(Material sky)
        {
            if (_installed && sky != _mat) _savedSky = sky;
        }

        private static Texture2D Build(CelestialBodyProfile body)
        {
            var planet = SkyPainter.PlanetFor(body);
            int stars = SkyPainter.StarCount(body);
            bool detailed = planet.Kind != SkyPlanetKind.None || stars > 0;
            int w = detailed ? 4096 : 1024;
            int h = w / 2;
            var px = new Color32[w * h];
            bool airless = SkyPainter.IsAirless(body);

            // Gradient depends only on the row.
            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;
                float elevY = Mathf.Cos((1f - v) * Mathf.PI);
                Color32 row = SkyPainter.Gradient(body, elevY);
                int o = y * w;
                for (int x = 0; x < w; x++) px[o + x] = row;
            }

            if (airless) PaintMilkyWay(px, w, h);
            if (stars > 0) PaintStars(px, w, h, stars, (int)body.Id * 7919 + 17, airless ? 1f : 0.35f);
            if (planet.Kind != SkyPlanetKind.None) PaintPlanet(px, w, h, planet, SkyPainter.ToSun(body.SunEuler), airless);

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = $"SM_{body.ShortCode}_SkyPanorama",
                filterMode = FilterMode.Bilinear,
                wrapModeU = TextureWrapMode.Repeat,
                wrapModeV = TextureWrapMode.Clamp
            };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        private static void PaintMilkyWay(Color32[] px, int w, int h)
        {
            // Only the upper hemisphere near the band is worth sampling.
            for (int y = h / 2; y < h; y++)
            {
                float v = (y + 0.5f) / h;
                for (int x = 0; x < w; x++)
                {
                    var dir = SkyPainter.DirFromUV((x + 0.5f) / w, v);
                    float g = SkyPainter.MilkyWay(dir);
                    if (g < 0.02f) continue;
                    int i = y * w + x;
                    px[i] = Add(px[i], new Color(0.55f, 0.58f, 0.70f) * (g * 0.10f));
                }
            }
        }

        private static void PaintStars(Color32[] px, int w, int h, int count, int seed, float brightness)
        {
            var rng = new System.Random(seed);
            for (int i = 0; i < count; i++)
            {
                // Uniform on the sphere, upper hemisphere plus a sliver below the horizon.
                float y = (float)rng.NextDouble() * 1.05f - 0.05f;
                float ang = (float)(rng.NextDouble() * Mathf.PI * 2.0);
                float r = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                var dir = new Vector3(r * Mathf.Cos(ang), y, r * Mathf.Sin(ang));
                Vector2 uv = SkyPainter.UVFromDir(dir);
                int sx = Mathf.Clamp((int)(uv.x * w), 0, w - 1);
                int sy = Mathf.Clamp((int)(uv.y * h), 0, h - 1);
                float mag = Mathf.Pow((float)rng.NextDouble(), 3.2f);
                float warm = (float)rng.NextDouble();
                Color tint = warm < 0.2f ? new Color(1f, 0.82f, 0.66f) : warm > 0.85f ? new Color(0.72f, 0.82f, 1f) : Color.white;
                Color c = tint * (0.25f + 0.95f * mag) * brightness;
                Stamp(px, w, h, sx, sy, c);
                if (mag > 0.55f)
                {
                    Color halo = c * 0.35f;
                    Stamp(px, w, h, sx + 1, sy, halo);
                    Stamp(px, w, h, sx - 1, sy, halo);
                    Stamp(px, w, h, sx, sy + 1, halo);
                    Stamp(px, w, h, sx, sy - 1, halo);
                }
            }
        }

        private static void PaintPlanet(Color32[] px, int w, int h, SkyPlanet p, Vector3 toSun, bool airless)
        {
            SkyPainter.PlanetBasis(p, out Vector3 center, out Vector3 right, out Vector3 up);
            float reach = p.RadiusDeg * 1.2f;
            Vector2 cuv = SkyPainter.UVFromDir(center);
            float cosEl = Mathf.Max(0.2f, Mathf.Cos(p.ElevationDeg * Mathf.Deg2Rad));
            int halfW = Mathf.CeilToInt(reach / 360f / cosEl * w) + 2;
            int halfH = Mathf.CeilToInt(reach / 180f * h) + 2;
            int cx = (int)(cuv.x * w), cy = (int)(cuv.y * h);
            float sinDisc = Mathf.Sin(p.RadiusDeg * Mathf.Deg2Rad);

            for (int y = Mathf.Max(0, cy - halfH); y <= Mathf.Min(h - 1, cy + halfH); y++)
            for (int xi = cx - halfW; xi <= cx + halfW; xi++)
            {
                int x = ((xi % w) + w) % w;
                var dir = SkyPainter.DirFromUV((x + 0.5f) / w, (y + 0.5f) / h);
                if (Vector3.Dot(dir, center) <= 0f) continue;
                float dx = Vector3.Dot(dir, right) / sinDisc;
                float dy = Vector3.Dot(dir, up) / sinDisc;
                if (dx * dx + dy * dy > 1.3f) continue;
                Color c = SkyPainter.ShadePlanet(p, toSun, dx, dy);
                // A daytime sky with air scatters in front of a moon: only the lit part shows.
                if (!airless) c.a *= Mathf.Clamp01(c.maxColorComponent * 2.2f);
                if (c.a <= 0f) continue;
                int i = y * w + x;
                Color under = px[i];
                px[i] = Color.Lerp(under, new Color(c.r, c.g, c.b, 1f), c.a);
            }
        }

        private static void Stamp(Color32[] px, int w, int h, int x, int y, Color c)
        {
            if (y < 0 || y >= h) return;
            x = ((x % w) + w) % w;
            int i = y * w + x;
            px[i] = Add(px[i], c);
        }

        private static Color32 Add(Color32 a, Color b)
        {
            Color s = (Color)a + b;
            s.a = 1f;
            return s;
        }
    }
}
