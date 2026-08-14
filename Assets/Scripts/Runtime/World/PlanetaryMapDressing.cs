using UnityEngine;
using UnityEngine.Rendering;

namespace SolarMajesty
{
    /// <summary>
    /// Runtime ground material, horizon skirt, and sky driven by a <see cref="CelestialBodyProfile"/>.
    /// Visual only — does not affect nav or gameplay.
    /// </summary>
    public static class PlanetaryMapDressing
    {
        public static void Apply(Transform parent, IsoGrid grid, CelestialBodyProfile body)
        {
            if (body == null) body = CelestialBodyCatalog.Luna();
            DressGround(grid, body);
            EnsureHorizon(parent, grid, body);
            EnsureSky(body);
        }

        private static void DressGround(IsoGrid grid, CelestialBodyProfile body)
        {
            var ground = GameObject.Find("GroundPlane");
            if (ground == null) return;
            var rend = ground.GetComponent<Renderer>();
            if (rend == null) return;

            var shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Universal Render Pipeline/Simple Lit");
            if (shader == null) return;

            var mat = new Material(shader) { name = $"SM_Ground_{body.ShortCode}" };
            Texture2D albedo = BuildAlbedo(128, body);
            Texture2D normal = BuildNormal(128);
            albedo.wrapMode = TextureWrapMode.Repeat;
            normal.wrapMode = TextureWrapMode.Repeat;

            float worldW = grid != null ? grid.WorldWidth : 384f;
            float tiles = Mathf.Max(24f, worldW / 8f);

            if (mat.HasProperty("_BaseMap"))
            {
                mat.SetTexture("_BaseMap", albedo);
                mat.SetTextureScale("_BaseMap", new Vector2(tiles, tiles));
            }
            if (mat.HasProperty("_BumpMap"))
            {
                mat.SetTexture("_BumpMap", normal);
                mat.SetTextureScale("_BumpMap", new Vector2(tiles, tiles));
                mat.EnableKeyword("_NORMALMAP");
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.08f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);

            rend.sharedMaterial = mat;
            rend.shadowCastingMode = ShadowCastingMode.Off;
            rend.receiveShadows = true;
        }

        private static void EnsureHorizon(Transform parent, IsoGrid grid, CelestialBodyProfile body)
        {
            var existing = GameObject.Find("HorizonSkirt");
            if (existing != null) Object.Destroy(existing);

            float worldW = grid != null ? grid.WorldWidth : 384f;
            float worldH = grid != null ? grid.WorldHeight : 384f;
            float scale = Mathf.Max(worldW, worldH) / 10f * 1.35f;

            var skirt = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            skirt.name = "HorizonSkirt";
            if (parent != null) skirt.transform.SetParent(parent, false);
            skirt.transform.position = new Vector3(worldW * 0.5f, -0.15f, worldH * 0.5f);
            skirt.transform.localScale = new Vector3(scale, 0.05f, scale);
            Object.Destroy(skirt.GetComponent<Collider>());
            PlanetaryWorldGen.Tint(skirt, body.Horizon, 0.05f, ShadowCastingMode.Off);
        }

        private static void EnsureSky(CelestialBodyProfile body)
        {
            var shader = Shader.Find("Skybox/Procedural");
            if (shader != null)
            {
                var sky = new Material(shader) { name = $"SM_{body.ShortCode}_Sky" };
                if (sky.HasProperty("_SkyTint")) sky.SetColor("_SkyTint", body.SkyTop);
                if (sky.HasProperty("_GroundColor")) sky.SetColor("_GroundColor", body.SkyHorizon);
                if (sky.HasProperty("_AtmosphereThickness"))
                    sky.SetFloat("_AtmosphereThickness", body.AtmosphereThickness);
                if (sky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", body.SkyExposure);
                RenderSettings.skybox = sky;
                DynamicGI.UpdateEnvironment();
            }

            if (Camera.main != null)
            {
                Camera.main.clearFlags = CameraClearFlags.Skybox;
                Camera.main.backgroundColor = body.SkyTop;
            }
        }

        private static Texture2D BuildAlbedo(int size, CelestialBodyProfile body)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = $"SM_{body.ShortCode}_Albedo",
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color[size * size];
            bool earthy = body.Id == CelestialBodyId.Earth;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.11f, y * 0.11f);
                float n2 = Mathf.PerlinNoise(x * 0.37f + 20f, y * 0.37f + 8f);
                float h = Frac(Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f);
                Color c = Color.Lerp(body.GroundDark, body.GroundLight, n * 0.65f + n2 * 0.35f);
                if (earthy)
                {
                    float meadow = Mathf.PerlinNoise(x * 0.07f + 3f, y * 0.07f + 1f);
                    float soil = Mathf.PerlinNoise(x * 0.22f + 9f, y * 0.22f);
                    c = Color.Lerp(c, body.GroundLight * 1.08f, meadow * 0.35f);
                    if (soil > 0.72f)
                        c = Color.Lerp(c, body.SoilNodeColor, 0.22f);
                }
                c *= 0.92f + h * 0.16f;
                pixels[y * size + x] = c;
            }
            tex.SetPixels(pixels);
            tex.Apply(true);
            return tex;
        }

        private static Texture2D BuildNormal(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "SM_GroundNormal",
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color[size * size];
            const float strength = 0.55f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float hL = Height(x - 1, y);
                float hR = Height(x + 1, y);
                float hD = Height(x, y - 1);
                float hU = Height(x, y + 1);
                Vector3 n = new Vector3((hL - hR) * strength, (hD - hU) * strength, 1f).normalized;
                pixels[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
            }
            tex.SetPixels(pixels);
            tex.Apply(true);
            return tex;

            float Height(int px, int py)
            {
                px = (px + size) % size;
                py = (py + size) % size;
                return Mathf.PerlinNoise(px * 0.15f, py * 0.15f);
            }
        }

        private static float Frac(float v) => v - Mathf.Floor(v);
    }
}
