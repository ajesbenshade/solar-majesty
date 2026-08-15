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
            if (body == null) body = CelestialBodyCatalog.Earth();
            DressGround(grid, body);
            EnsureHorizon(parent, grid, body);
            EnsureSky(body);
            EnsureDustDevils(parent, grid, body);
            EnsureEarthVista(parent, body);
            Debug.Log("[MapDressing] " + body.DisplayName + " ground+sky applied.");
        }

        private static void DressGround(IsoGrid grid, CelestialBodyProfile body)
        {
            var ground = GameObject.Find("GroundPlane");
            if (ground == null) return;
            var rend = ground.GetComponent<Renderer>();
            if (rend == null) return;

            var shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                         ?? Shader.Find("Sprites/Default");
            if (shader == null)
            {
                Debug.LogWarning("[MapDressing] No lit shader — ground stays the default plane.");
                return;
            }

            var mat = new Material(shader) { name = $"SM_Ground_{body.ShortCode}" };
            Texture2D albedo = BuildAlbedo(body.Id == CelestialBodyId.Mars || body.Id == CelestialBodyId.Earth ? 256 : 128, body);
            Texture2D normal = BuildNormal(body.Id == CelestialBodyId.Earth ? 192 : 128);
            albedo.wrapMode = TextureWrapMode.Repeat;
            normal.wrapMode = TextureWrapMode.Repeat;

            float worldW = grid != null ? grid.WorldWidth : 384f;
            float tiles = body.Id == CelestialBodyId.Earth
                ? Mathf.Max(12f, worldW / 22f)
                : Mathf.Max(24f, worldW / 8f);

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
            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", body.Id == CelestialBodyId.Earth ? 0.14f : 0.08f);
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
                // Procedural _SkyTint multiplies the atmosphere. Dark catalog blues read as dusk greybox.
                Color tint = body.Id == CelestialBodyId.Earth
                    ? new Color(0.62f, 0.78f, 1f)
                    : body.Id == CelestialBodyId.Mars
                        ? new Color(0.95f, 0.58f, 0.32f)
                        : body.SkyTop;
                Color ground = body.Id == CelestialBodyId.Earth
                    ? new Color(0.78f, 0.86f, 0.72f)
                    : body.SkyHorizon;
                if (sky.HasProperty("_SkyTint")) sky.SetColor("_SkyTint", tint);
                if (sky.HasProperty("_GroundColor")) sky.SetColor("_GroundColor", ground);
                if (sky.HasProperty("_AtmosphereThickness"))
                    sky.SetFloat("_AtmosphereThickness", body.AtmosphereThickness);
                if (sky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", body.SkyExposure);
                if (sky.HasProperty("_SunSize"))
                    sky.SetFloat("_SunSize", body.Id == CelestialBodyId.Earth ? 0.042f : 0.04f);
                if (sky.HasProperty("_SunSizeConvergence"))
                    sky.SetFloat("_SunSizeConvergence", body.Id == CelestialBodyId.Earth ? 5f : 5f);
                RenderSettings.skybox = sky;
                DynamicGI.UpdateEnvironment();
            }

            if (Camera.main != null)
            {
                Camera.main.clearFlags = shader != null
                    ? CameraClearFlags.Skybox
                    : CameraClearFlags.SolidColor;
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
                    c = Color.Lerp(c, body.DuneColor, dust * 0.34f);
                    c += CraterMark(x, y, size, 0.22f, 0.18f, body.CraterRim, body.CraterFloor);
                    c += CraterMark(x, y, size, 0.68f, 0.71f, body.CraterRim, body.CraterFloor);
                    c += CraterMark(x, y, size, 0.80f, 0.32f, body.CraterRim, body.CraterFloor);
                    float grit = Frac(Mathf.Sin(x * 19.1f + y * 81.3f) * 23421.7f);
                    if (grit > 0.93f)
                        c = Color.Lerp(c, body.RockColor, 0.55f);
                    c = Color.Lerp(c, new Color(0.82f, 0.36f, 0.14f), 0.10f);
                    c = Color.Lerp(c, body.GroundDark, 0.06f); // matte dust
                }
                else if (earthy)
                {
                    float meadow = Mathf.PerlinNoise(x * 0.035f + 3f, y * 0.035f + 1f);
                    float soil = Mathf.PerlinNoise(x * 0.09f + 9f, y * 0.09f);
                    float rows = Mathf.Abs(Mathf.Sin((x * 0.41f + y * 0.07f) * 0.55f));
                    float blade = Frac(Mathf.Sin(x * 41.3f + y * 17.7f) * 9123.4f);
                    float track = Mathf.PerlinNoise(x * 0.018f + 40f, y * 0.22f);
                    c = Color.Lerp(c, body.GroundLight * 1.18f, meadow * 0.55f);
                    if (soil > 0.62f)
                        c = Color.Lerp(c, body.SoilNodeColor, (soil - 0.62f) * 1.15f);
                    if (meadow > 0.58f && soil < 0.55f)
                        c = Color.Lerp(c, body.ForestCanopy * 1.35f, (meadow - 0.58f) * 0.85f);
                    if (rows > 0.72f && meadow > 0.4f)
                        c = Color.Lerp(c, body.DuneColor, 0.18f);
                    if (track > 0.78f && track < 0.86f)
                        c = Color.Lerp(c, body.SoilNodeColor * 0.85f, 0.55f);
                    if (blade > 0.82f)
                        c = Color.Lerp(c, body.GroundLight * 1.25f, 0.22f);
                    float wet = Mathf.PerlinNoise(x * 0.06f + 18f, y * 0.06f + 7f);
                    if (wet > 0.74f)
                        c = Color.Lerp(c, body.WaterDeep, 0.10f);
                    if (h > 0.93f)
                        c = Color.Lerp(c, body.RockColor, 0.40f);
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
                new Vector3(worldW * 0.72f, 0f, worldH * 0.84f),
                new Vector3(worldW * 0.90f, 0f, worldH * 0.58f)
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

            for (int i = 0; i < 7; i++)
            {
                float t = i / 6f;
                var band = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                band.name = "Wisp_" + i;
                band.transform.SetParent(go.transform, false);
                band.transform.localPosition = new Vector3(0f, 1.4f + t * 11.5f, 0f);
                float rad = Mathf.Lerp(2.2f, 0.32f, t);
                band.transform.localScale = new Vector3(rad, 1.05f, rad);
                Object.Destroy(band.GetComponent<Collider>());
                var rend = band.GetComponent<Renderer>();
                if (rend == null) continue;
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                                       ?? Shader.Find("Sprites/Default"));
                var c = new Color(0.78f, 0.44f, 0.22f, 0.26f - t * 0.035f);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
                if (mat.HasProperty("_Color")) mat.color = c;
                ColonyVisualUtility.ApplyTransparent(mat);
                rend.sharedMaterial = mat;
                rend.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        private static void EnsureEarthVista(Transform parent, CelestialBodyProfile body)
        {
            var oldClouds = GameObject.Find("EarthCloudRoot");
            if (oldClouds != null)
            {
                oldClouds.name = "EarthCloudRoot_old";
                Object.Destroy(oldClouds);
            }
            var oldVista = GameObject.Find("EarthVistaRoot");
            if (oldVista != null)
            {
                oldVista.name = "EarthVistaRoot_old";
                Object.Destroy(oldVista);
            }
            if (body == null || body.Id != CelestialBodyId.Earth) return;

            Vector3 campus = ColonyLayout.CampusOrigin;
            var root = new GameObject("EarthVistaRoot").transform;
            if (parent != null) root.SetParent(parent, false);

            // Cumulus in the isometric backdrop (ortho 16) — not parked on the far map edge.
            var clouds = new GameObject("EarthCloudRoot").transform;
            clouds.SetParent(root, false);
            Vector3[] cloudSpots =
            {
                campus + new Vector3(-18f, 20f, 16f),
                campus + new Vector3(22f, 24f, 10f),
                campus + new Vector3(8f, 18f, -18f),
                campus + new Vector3(-10f, 22f, -14f)
            };
            for (int i = 0; i < cloudSpots.Length; i++)
                SpawnCumulus(clouds, cloudSpots[i], 0.62f + i * 0.08f);

            // Grass / trees / a pond just outside the 6-cell claim so the empty drop reads Earth.
            for (int i = 0; i < 28; i++)
            {
                float ang = i * 1.618f * Mathf.PI;
                float rad = 6.4f + (i % 6) * 1.55f;
                Vector3 at = campus + new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);
                SpawnGrassTuft(root, at, body, i);
            }

            Vector3[] treeSpots =
            {
                campus + new Vector3(13.5f, 0f, 8.5f),
                campus + new Vector3(-12.2f, 0f, 10.4f),
                campus + new Vector3(9.8f, 0f, -13.2f),
                campus + new Vector3(-14.5f, 0f, -7.6f),
                campus + new Vector3(16.2f, 0f, -4.2f),
                campus + new Vector3(-8.4f, 0f, 15.1f),
                campus + new Vector3(4.2f, 0f, 16.8f),
                campus + new Vector3(-16.5f, 0f, 2.4f)
            };
            for (int i = 0; i < treeSpots.Length; i++)
                SpawnVistaTree(root, treeSpots[i], body, i);

            SpawnVistaPond(root, campus + new Vector3(14.5f, 0f, 6.2f), body);
        }

        private static void SpawnGrassTuft(Transform parent, Vector3 world, CelestialBodyProfile body, int salt)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Dress_Grass";
            go.transform.SetParent(parent, false);
            go.transform.position = world + Vector3.up * 0.12f;
            go.transform.localScale = new Vector3(0.22f + (salt % 3) * 0.06f, 0.28f, 0.16f);
            go.transform.rotation = Quaternion.Euler(0f, salt * 37f, 8f);
            Object.Destroy(go.GetComponent<Collider>());
            Color blade = Color.Lerp(body.ForestCanopy, body.GroundLight, 0.35f + (salt % 4) * 0.08f);
            PlanetaryWorldGen.Tint(go, blade, 0.12f);
        }

        private static void SpawnVistaTree(Transform parent, Vector3 world, CelestialBodyProfile body, int salt)
        {
            var tree = new GameObject("Dress_Tree");
            tree.transform.SetParent(parent, false);
            tree.transform.position = world;
            float h = 1.55f + (salt % 4) * 0.28f;

            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Trunk";
            trunk.transform.SetParent(tree.transform, false);
            trunk.transform.localPosition = new Vector3(0f, h * 0.28f, 0f);
            trunk.transform.localScale = new Vector3(0.28f, h * 0.28f, 0.28f);
            Object.Destroy(trunk.GetComponent<Collider>());
            PlanetaryWorldGen.Tint(trunk, body.ForestTrunk, 0.08f);

            var canopy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            canopy.name = "Canopy";
            canopy.transform.SetParent(tree.transform, false);
            canopy.transform.localPosition = new Vector3(0.05f, h * 0.78f, -0.04f);
            canopy.transform.localScale = new Vector3(1.85f, 1.15f, 1.75f);
            Object.Destroy(canopy.GetComponent<Collider>());
            PlanetaryWorldGen.Tint(canopy, Color.Lerp(body.ForestCanopy, body.GroundLight, 0.2f), 0.12f);
        }

        private static void SpawnVistaPond(Transform parent, Vector3 world, CelestialBodyProfile body)
        {
            var pond = new GameObject("Dress_Pond");
            pond.transform.SetParent(parent, false);
            pond.transform.position = world;

            var shore = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shore.name = "Shore";
            shore.transform.SetParent(pond.transform, false);
            shore.transform.localPosition = new Vector3(0f, 0.01f, 0f);
            shore.transform.localScale = new Vector3(5.6f, 0.02f, 4.4f);
            Object.Destroy(shore.GetComponent<Collider>());
            PlanetaryWorldGen.Tint(shore, Color.Lerp(body.GroundDark, body.WaterDeep, 0.28f), 0.05f);

            var water = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            water.name = "Water";
            water.transform.SetParent(pond.transform, false);
            water.transform.localPosition = new Vector3(0.15f, 0.025f, -0.1f);
            water.transform.localScale = new Vector3(4.6f, 0.03f, 3.5f);
            Object.Destroy(water.GetComponent<Collider>());
            PlanetaryWorldGen.Tint(water, Color.Lerp(body.WaterDeep, body.WaterShallow, 0.4f), 0.72f);
        }

        private static void SpawnCumulus(Transform parent, Vector3 pos, float scale)
        {
            var go = new GameObject("Dress_Cumulus");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * scale;
            go.AddComponent<CloudDrift>();

            Vector3[] lobes =
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(3.2f, 0.4f, 1.1f),
                new Vector3(-2.6f, 0.2f, -0.8f),
                new Vector3(1.1f, 0.9f, -2.2f)
            };
            float[] rad = { 6.5f, 4.8f, 4.2f, 3.6f };
            for (int i = 0; i < lobes.Length; i++)
            {
                var puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                puff.name = "Puff_" + i;
                puff.transform.SetParent(go.transform, false);
                puff.transform.localPosition = lobes[i];
                puff.transform.localScale = new Vector3(rad[i], rad[i] * 0.38f, rad[i] * 0.72f);
                Object.Destroy(puff.GetComponent<Collider>());
                var rend = puff.GetComponent<Renderer>();
                if (rend == null) continue;
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                                       ?? Shader.Find("Sprites/Default"));
                var c = new Color(0.94f, 0.96f, 0.98f, 0.42f - i * 0.04f);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
                if (mat.HasProperty("_Color")) mat.color = c;
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.08f);
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

    /// <summary>Slow drift for distant Earth cumulus. Not a threat.</summary>
    public class CloudDrift : MonoBehaviour
    {
        [SerializeField] private float orbitMeters = 5.5f;
        [SerializeField] private float degreesPerSecond = 2.4f;
        [SerializeField] private float bobMeters = 0.55f;

        private Vector3 _origin;
        private float _phase;

        private void Awake()
        {
            _origin = transform.position;
            _phase = transform.position.x * 0.07f;
        }

        private void Update()
        {
            float t = Time.time * degreesPerSecond * Mathf.Deg2Rad + _phase;
            Vector3 orbit = new Vector3(Mathf.Cos(t), 0f, Mathf.Sin(t * 0.65f)) * orbitMeters;
            float bob = Mathf.Sin(Time.time * 0.18f + _phase) * bobMeters;
            transform.position = _origin + orbit + new Vector3(0f, bob, 0f);
        }
    }
}
