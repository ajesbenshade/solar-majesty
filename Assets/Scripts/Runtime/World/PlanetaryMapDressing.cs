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
            EnsureDustDevils(parent, grid, body);
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
            Texture2D albedo = BuildAlbedo(body.Id == CelestialBodyId.Mars ? 256 : 128, body);
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
            bool ice = body.Kit == TerrainKit.IceCrust;
            bool belt = body.Kit == TerrainKit.AsteroidField;
            bool mars = body.Id == CelestialBodyId.Mars;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.11f, y * 0.11f);
                float n2 = Mathf.PerlinNoise(x * 0.37f + 20f, y * 0.37f + 8f);
                float h = Frac(Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f);
                Color c = Color.Lerp(body.GroundDark, body.GroundLight, n * 0.65f + n2 * 0.35f);
                if (mars)
                {
                    float dust = Mathf.PerlinNoise(x * 0.045f + 2f, y * 0.045f);
                    c = Color.Lerp(c, body.DuneColor, dust * 0.28f);
                    c += CraterMark(x, y, size, 0.22f, 0.18f, body.CraterRim, body.CraterFloor);
                    c += CraterMark(x, y, size, 0.68f, 0.71f, body.CraterRim, body.CraterFloor);
                    c += CraterMark(x, y, size, 0.80f, 0.32f, body.CraterRim, body.CraterFloor);
                    float grit = Frac(Mathf.Sin(x * 19.1f + y * 81.3f) * 23421.7f);
                    if (grit > 0.93f)
                        c = Color.Lerp(c, body.RockColor, 0.55f);
                    c = Color.Lerp(c, body.GroundDark, 0.08f); // matte dust
                }
                else if (earthy)
                {
                    float meadow = Mathf.PerlinNoise(x * 0.07f + 3f, y * 0.07f + 1f);
                    float soil = Mathf.PerlinNoise(x * 0.22f + 9f, y * 0.22f);
                    c = Color.Lerp(c, body.GroundLight * 1.08f, meadow * 0.35f);
                    if (soil > 0.72f)
                        c = Color.Lerp(c, body.SoilNodeColor, 0.22f);
                }
                else if (ice)
                {
                    float crack = Mathf.PerlinNoise(x * 0.19f, y * 0.04f);
                    if (crack > 0.62f)
                        c = Color.Lerp(c, body.WaterDeep, (crack - 0.62f) * 1.4f);
                    c = Color.Lerp(c, body.WaterShallow, n2 * 0.18f);
                }
                else if (belt)
                {
                    float speck = Frac(Mathf.Sin(x * 19.7f + y * 91.3f) * 23421.7f);
                    if (speck > 0.92f)
                        c = Color.Lerp(c, body.RockColor * 1.4f, 0.55f);
                    if (n < 0.28f)
                        c = Color.Lerp(c, Color.black, 0.35f);
                }
                c *= 0.92f + h * 0.16f;
                pixels[y * size + x] = c;
            }
            tex.SetPixels(pixels);
            tex.Apply(true);
            return tex;
        }

        private static Color CraterMark(
            int x, int y, int size, float cx, float cy, Color rim, Color floor)
        {
            float u = x / (float)size;
            float v = y / (float)size;
            float dx = u - cx;
            float dy = v - cy;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            float r = 0.11f;
            if (d > r) return Color.clear;
            if (d < r * 0.55f)
                return (floor - Color.white * 0.08f) * 0.35f - new Color(0.08f, 0.04f, 0.02f, 0f);
            return (rim - Color.white * 0.04f) * 0.28f;
        }

        private static void EnsureDustDevils(Transform parent, IsoGrid grid, CelestialBodyProfile body)
        {
            var old = GameObject.Find("DustDevilRoot");
            if (old != null)
            {
                old.name = "DustDevilRoot_old";
                Object.Destroy(old);
            }
            if (body == null || body.Id != CelestialBodyId.Mars) return;

            float worldW = grid != null ? grid.WorldWidth : 384f;
            float worldH = grid != null ? grid.WorldHeight : 384f;
            var root = new GameObject("DustDevilRoot").transform;
            if (parent != null) root.SetParent(parent, false);

            Vector3[] spots =
            {
                new Vector3(worldW * 0.18f, 0f, worldH * 0.78f),
                new Vector3(worldW * 0.82f, 0f, worldH * 0.22f),
                new Vector3(worldW * 0.72f, 0f, worldH * 0.84f)
            };
            for (int i = 0; i < spots.Length; i++)
            {
                if (Vector3.Distance(spots[i], ColonyLayout.CampusOrigin) < 28f)
                    continue;
                SpawnDustDevil(root, spots[i], 1f + i * 0.18f);
            }
        }

        private static void SpawnDustDevil(Transform parent, Vector3 pos, float scale)
        {
            var go = new GameObject("Dress_DustDevil");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * scale;
            go.AddComponent<DustDevilSpin>();

            for (int i = 0; i < 5; i++)
            {
                float t = i / 4f;
                var band = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                band.name = "Wisp_" + i;
                band.transform.SetParent(go.transform, false);
                band.transform.localPosition = new Vector3(0f, 1.2f + t * 7.5f, 0f);
                float rad = Mathf.Lerp(1.8f, 0.35f, t);
                band.transform.localScale = new Vector3(rad, 0.85f, rad);
                Object.Destroy(band.GetComponent<Collider>());
                var rend = band.GetComponent<Renderer>();
                if (rend == null) continue;
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                                       ?? Shader.Find("Sprites/Default"));
                var c = new Color(0.72f, 0.42f, 0.22f, 0.22f - t * 0.04f);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
                if (mat.HasProperty("_Color")) mat.color = c;
                ColonyVisualUtility.ApplyTransparent(mat);
                rend.sharedMaterial = mat;
                rend.shadowCastingMode = ShadowCastingMode.Off;
            }
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

    /// <summary>Slow spin for distant Mars dust-devil dressing. Not a threat.</summary>
    public class DustDevilSpin : MonoBehaviour
    {
        [SerializeField] private float degreesPerSecond = 38f;

        private void Update()
        {
            transform.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f);
        }
    }
}
