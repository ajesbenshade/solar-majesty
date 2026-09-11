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
            EnsureMarsVista(parent, body);
            Debug.Log("[MapDressing] " + body.DisplayName + " ground+sky applied.");
        }

        private static void DressGround(IsoGrid grid, CelestialBodyProfile body)
        {
            var ground = GameObject.Find("GroundPlane");
            if (ground == null) return;
            var rend = ground.GetComponent<Renderer>();
            if (rend == null) return;

            // Procedural ground reads the displaced mesh's slope and vertex exposure, so it shows
            // rock on ridges and dust on flats instead of one tiled tint across the whole map.
            var ground0 = Shader.Find("SolarMajesty/PlanetGround");
            if (ground0 != null)
            {
                var groundMat = new Material(ground0) { name = $"SM_Ground_{body.ShortCode}" };
                groundMat.SetColor("_BaseColor", body.GroundLight);
                groundMat.SetColor("_DarkColor", body.GroundDark);
                groundMat.SetColor("_RockColor", body.RockColor);
                groundMat.SetFloat("_MacroScale", body.Id == CelestialBodyId.Earth ? 34f : 46f);
                groundMat.SetFloat("_MacroStrength", 0.38f);
                groundMat.SetFloat("_DetailScale", 2.4f);
                groundMat.SetFloat("_DetailStrength",
                    body.Id == CelestialBodyId.Europa ? 0.12f
                    : body.Id == CelestialBodyId.Mars ? 0.40f
                    : body.Id == CelestialBodyId.Luna ? 0.38f
                    : 0.24f);
                groundMat.SetFloat("_Smoothness", body.Id == CelestialBodyId.Europa ? 0.30f : 0.06f);
                BindAuthoredGroundDetail(groundMat, body);
                BindTerrainBake(groundMat, ground, body);

                rend.sharedMaterial = groundMat;
                rend.shadowCastingMode = ShadowCastingMode.Off;
                rend.receiveShadows = true;
                return;
            }

            var shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                         ?? Shader.Find("Sprites/Default");
            if (shader == null)
            {
                Debug.LogWarning("[MapDressing] No lit shader — ground stays the default plane.");
                return;
            }

            var mat = new Material(shader) { name = $"SM_Ground_{body.ShortCode}" };
            Texture2D albedo = null;
            Texture2D normal = null;
            bool authored = false;
            if (body.Id == CelestialBodyId.Earth)
            {
                albedo = EnvironmentMeshCatalog.LoadEarthAlbedo();
                normal = EnvironmentMeshCatalog.LoadEarthNormal();
                authored = albedo != null;
            }
            else if (body.Id == CelestialBodyId.Mars)
            {
                albedo = EnvironmentMeshCatalog.LoadMarsAlbedo();
                normal = EnvironmentMeshCatalog.LoadMarsNormal();
                authored = albedo != null;
            }

            if (!authored)
            {
                albedo = BuildAlbedo(body.Id == CelestialBodyId.Mars || body.Id == CelestialBodyId.Earth ? 256 : 128, body);
                normal = BuildNormal(body.Id == CelestialBodyId.Earth ? 192 : 128);
            }
            else if (normal == null)
            {
                normal = BuildNormal(body.Id == CelestialBodyId.Earth ? 192 : 128);
            }

            albedo.wrapMode = TextureWrapMode.Repeat;
            normal.wrapMode = TextureWrapMode.Repeat;
            if (authored)
            {
                albedo.filterMode = FilterMode.Bilinear;
                normal.filterMode = FilterMode.Bilinear;
            }

            float worldW = grid != null ? grid.WorldWidth : 384f;
            // Dense tiles so authored meadow/regolith detail matches water foam scale.
            // Slightly non-square UV scale breaks iso-aligned checker banding.
            Vector2 tileScale;
            if (body.Id == CelestialBodyId.Earth)
            {
                float t = Mathf.Max(22f, worldW / 14f);
                tileScale = new Vector2(t * 1.07f, t * 0.91f);
            }
            else if (body.Id == CelestialBodyId.Mars)
            {
                float t = Mathf.Max(20f, worldW / 16f);
                tileScale = new Vector2(t * 0.94f, t * 1.08f);
            }
            else
            {
                float t = Mathf.Max(24f, worldW / 8f);
                tileScale = new Vector2(t, t);
            }

            if (mat.HasProperty("_BaseMap"))
            {
                mat.SetTexture("_BaseMap", albedo);
                mat.SetTextureScale("_BaseMap", tileScale);
            }
            if (mat.HasProperty("_BumpMap"))
            {
                mat.SetTexture("_BumpMap", normal);
                mat.SetTextureScale("_BumpMap", tileScale);
                mat.EnableKeyword("_NORMALMAP");
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", body.Id == CelestialBodyId.Earth ? 0.14f : 0.08f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);

            rend.sharedMaterial = mat;
            rend.shadowCastingMode = ShadowCastingMode.Off;
            rend.receiveShadows = true;
            if (authored)
                Debug.Log($"[MapDressing] Authored ground textures for {body.ShortCode} tiles={tileScale.x:F1}x{tileScale.y:F1}");
        }

        /// <summary>
        /// PlanetGround keeps the body color lock; authored tiles add grit only.
        /// Missing textures leave _DetailTexAmount at 0 (shader default).
        /// </summary>
        private static void BindAuthoredGroundDetail(Material groundMat, CelestialBodyProfile body)
        {
            if (groundMat == null || body == null) return;
            Texture2D albedo = null;
            Texture2D normal = null;
            if (body.Id == CelestialBodyId.Earth)
            {
                albedo = EnvironmentMeshCatalog.LoadEarthAlbedo();
                normal = EnvironmentMeshCatalog.LoadEarthNormal();
            }
            else if (body.Id == CelestialBodyId.Mars)
            {
                albedo = EnvironmentMeshCatalog.LoadMarsAlbedo();
                normal = EnvironmentMeshCatalog.LoadMarsNormal();
            }

            if (albedo == null) return;
            albedo.wrapMode = TextureWrapMode.Repeat;
            albedo.filterMode = FilterMode.Bilinear;
            if (groundMat.HasProperty("_DetailAlbedo"))
                groundMat.SetTexture("_DetailAlbedo", albedo);
            if (normal != null)
            {
                normal.wrapMode = TextureWrapMode.Repeat;
                normal.filterMode = FilterMode.Bilinear;
                if (groundMat.HasProperty("_DetailNormal"))
                    groundMat.SetTexture("_DetailNormal", normal);
            }

            if (groundMat.HasProperty("_DetailTexScale"))
                groundMat.SetFloat("_DetailTexScale", body.Id == CelestialBodyId.Mars ? 8.5f : 6.5f);
            // TDB splat albedo dominates; authored Mars grit stays as a secondary multiply.
            if (groundMat.HasProperty("_DetailTexAmount"))
                groundMat.SetFloat("_DetailTexAmount", body.Id == CelestialBodyId.Mars ? 0.32f : 0.40f);
        }

        /// <summary>
        /// Bind Terrain Data Baker-style maps (height / world normal / splat / mask) produced
        /// by TerrainDataBake. Missing holder leaves _SplatAmount at 0 (mesh-only grade).
        /// </summary>
        private static void BindTerrainBake(Material groundMat, GameObject ground, CelestialBodyProfile body)
        {
            if (groundMat == null || ground == null) return;
            var holder = ground.GetComponent<TerrainBakeHolder>();
            var bake = holder != null ? holder.Bake : null;
            if (bake == null || bake.SplatMap == null) return;

            if (groundMat.HasProperty("_HeightMap")) groundMat.SetTexture("_HeightMap", bake.HeightMap);
            if (groundMat.HasProperty("_NormalMap")) groundMat.SetTexture("_NormalMap", bake.NormalMap);
            if (groundMat.HasProperty("_SplatMap")) groundMat.SetTexture("_SplatMap", bake.SplatMap);
            if (groundMat.HasProperty("_MaskMap")) groundMat.SetTexture("_MaskMap", bake.MaskMap);
            if (groundMat.HasProperty("_BakeSize"))
                groundMat.SetVector("_BakeSize", new Vector4(bake.WorldWidth, bake.WorldHeight, 0f, 0f));
            if (groundMat.HasProperty("_BakeNormalAmount")) groundMat.SetFloat("_BakeNormalAmount", 0.88f);
            if (groundMat.HasProperty("_SplatAmount")) groundMat.SetFloat("_SplatAmount", 0.90f);

            Color grass;
            Color wet;
            CelestialBodyId id = body != null ? body.Id : CelestialBodyId.Mars;
            switch (id)
            {
                case CelestialBodyId.Earth:
                    grass = body.ForestCanopy;
                    wet = Color.Lerp(body.WaterShallow, body.GroundDark, 0.35f);
                    break;
                case CelestialBodyId.Europa:
                    grass = body.GroundLight;
                    wet = body.WaterShallow;
                    break;
                case CelestialBodyId.Luna:
                    grass = Color.Lerp(body.GroundLight, body.RockColor, 0.4f);
                    wet = body.CraterFloor;
                    break;
                default:
                    grass = Color.Lerp(body.GroundDark, body.RockColor, 0.45f);
                    wet = body.CraterFloor;
                    break;
            }
            if (groundMat.HasProperty("_GrassColor")) groundMat.SetColor("_GrassColor", grass);
            if (groundMat.HasProperty("_WetColor")) groundMat.SetColor("_WetColor", wet);

            BindSplatAlbedoTiles(groundMat, id);
        }

        /// <summary>
        /// TDB Sand/Grass/Snow as world-XZ splat albedo. Mars desaturates then colorizes
        /// so Grass grit reads as rock, not a lawn. Missing kit leaves _SplatAlbedoAmount 0.
        /// </summary>
        private static void BindSplatAlbedoTiles(Material groundMat, CelestialBodyId id)
        {
            if (groundMat == null) return;
            var layers = TerrainSplatLayers.Load();
            if (layers == null || !layers.HasAny) return;

            void BindLayer(string prop, Texture2D tex)
            {
                if (tex == null || !groundMat.HasProperty(prop)) return;
                tex.wrapMode = TextureWrapMode.Repeat;
                tex.filterMode = FilterMode.Bilinear;
                groundMat.SetTexture(prop, tex);
            }

            BindLayer("_SplatAlbedo0", layers.sand);
            BindLayer("_SplatAlbedo1", layers.grass);
            BindLayer("_SplatAlbedo2", layers.snow);
            BindLayer("_SplatAlbedo3", layers.sand);

            if (groundMat.HasProperty("_SplatAlbedoAmount"))
                groundMat.SetFloat("_SplatAlbedoAmount", 1f);
            if (groundMat.HasProperty("_SplatTexScale"))
                groundMat.SetFloat("_SplatTexScale", id == CelestialBodyId.Mars ? 5.8f : 7.5f);
            if (groundMat.HasProperty("_SplatAmount"))
                groundMat.SetFloat("_SplatAmount", 0.92f);
            if (groundMat.HasProperty("_MacroStrength"))
                groundMat.SetFloat("_MacroStrength", id == CelestialBodyId.Mars ? 0.16f : 0.28f);

            Vector4 desat;
            switch (id)
            {
                case CelestialBodyId.Earth:
                    desat = new Vector4(0f, 0f, 0f, 0.12f);
                    break;
                case CelestialBodyId.Europa:
                    desat = new Vector4(0.35f, 0.55f, 0.05f, 0.20f);
                    break;
                default:
                    // Mars / Luna / Belt: kill TDB Earth chroma, keep luma grain.
                    desat = new Vector4(1f, 1f, 1f, 0.95f);
                    break;
            }
            if (groundMat.HasProperty("_SplatDesat"))
                groundMat.SetVector("_SplatDesat", desat);

            // r15 still: TDB Sand * GroundLight read peach (186/112/54). Rust lock
            // ~150/68/30 so white hulls stay white against dirt, not washed sand.
            if (id == CelestialBodyId.Mars)
            {
                if (groundMat.HasProperty("_BaseColor"))
                    groundMat.SetColor("_BaseColor", new Color(0.58f, 0.26f, 0.11f, 1f));
                if (groundMat.HasProperty("_DarkColor"))
                    groundMat.SetColor("_DarkColor", new Color(0.36f, 0.14f, 0.06f, 1f));
                if (groundMat.HasProperty("_RockColor"))
                    groundMat.SetColor("_RockColor", new Color(0.42f, 0.20f, 0.10f, 1f));
                if (groundMat.HasProperty("_SlopeStart"))
                    groundMat.SetFloat("_SlopeStart", 0.18f);
                if (groundMat.HasProperty("_SlopeEnd"))
                    groundMat.SetFloat("_SlopeEnd", 0.58f);
                if (groundMat.HasProperty("_BakeNormalAmount"))
                    groundMat.SetFloat("_BakeNormalAmount", 1f);
                if (groundMat.HasProperty("_MacroStrength"))
                    groundMat.SetFloat("_MacroStrength", 0.22f);
            }
            else if (id == CelestialBodyId.Luna)
            {
                // Highland anorthosite dust, shadowed mare bowls, pale ejecta rims.
                if (groundMat.HasProperty("_BaseColor"))
                    groundMat.SetColor("_BaseColor", new Color(0.62f, 0.58f, 0.52f, 1f));
                if (groundMat.HasProperty("_DarkColor"))
                    groundMat.SetColor("_DarkColor", new Color(0.18f, 0.17f, 0.16f, 1f));
                if (groundMat.HasProperty("_RockColor"))
                    groundMat.SetColor("_RockColor", new Color(0.38f, 0.36f, 0.34f, 1f));
                if (groundMat.HasProperty("_GrassColor"))
                    groundMat.SetColor("_GrassColor", new Color(0.72f, 0.68f, 0.62f, 1f));
                if (groundMat.HasProperty("_WetColor"))
                    groundMat.SetColor("_WetColor", new Color(0.14f, 0.13f, 0.12f, 1f));
                if (groundMat.HasProperty("_SlopeStart"))
                    groundMat.SetFloat("_SlopeStart", 0.16f);
                if (groundMat.HasProperty("_SlopeEnd"))
                    groundMat.SetFloat("_SlopeEnd", 0.52f);
                if (groundMat.HasProperty("_BakeNormalAmount"))
                    groundMat.SetFloat("_BakeNormalAmount", 1f);
                if (groundMat.HasProperty("_MacroStrength"))
                    groundMat.SetFloat("_MacroStrength", 0.18f);
                if (groundMat.HasProperty("_SplatTexScale"))
                    groundMat.SetFloat("_SplatTexScale", 6.4f);
            }
        }

        /// <summary>
        /// Flat void fill when ortho zoom puts camera-local view corners below y=0.
        /// Those rays never hit GroundPlane/HorizonSkirt (origins already under the world) and
        /// would otherwise show Skybox/Procedural default mustard ground.
        /// </summary>
        public static Color VoidFillColor(CelestialBodyProfile body)
        {
            if (body == null) body = CelestialBodyCatalog.Earth();
            if (body.Id == CelestialBodyId.Earth)
                return Color.Lerp(body.GroundDark, body.GroundLight, 0.18f);
            // Mars Game-tab was a hard black sky cut — use salmon haze as the miss/clear
            // fill so the top of the ortho frame reads atmosphere, not void.
            if (body.Id == CelestialBodyId.Mars)
                return Color.Lerp(body.FogColor, body.SkyHorizon, 0.45f);
            if (body.Id == CelestialBodyId.Luna)
                return Color.Lerp(body.GroundDark, body.SkyTop, 0.35f);
            return Color.Lerp(body.GroundDark, body.GroundLight, 0.12f);
        }

        private static void EnsureHorizon(Transform parent, IsoGrid grid, CelestialBodyProfile body)
        {
            var existing = GameObject.Find("HorizonSkirt");
            if (existing != null) Object.Destroy(existing);
            var existingDeep = GameObject.Find("HorizonDeepFloor");
            if (existingDeep != null) Object.Destroy(existingDeep);

            float worldW = grid != null ? grid.WorldWidth : 384f;
            float worldH = grid != null ? grid.WorldHeight : 384f;
            float mapSpan = Mathf.Max(worldW, worldH);
            // Plane is 10×10; must cover map + max-ortho frustum even when camera pans to a corner.
            // Deep floor sits far below so rays that start under y=0 still hit geometry.
            float diameter = Mathf.Max(mapSpan * 10f, IsometricCameraController.MaxOrthoSize * 40f);
            Color skirtColor = VoidFillColor(body);
            Vector3 center = new Vector3(worldW * 0.5f, 0f, worldH * 0.5f);

            // Near skirt just under GroundPlane (catches rays that miss the map horizontally).
            SpawnHorizonPlane("HorizonSkirt", parent, center + Vector3.down * 0.35f, diameter, skirtColor);
            // Deep floor: ortho zoom > ~camY/up.y puts bottom-row ray origins under the world;
            // a plane at y=-80 is still in front of those rays (forward.y < 0).
            SpawnHorizonPlane("HorizonDeepFloor", parent, center + Vector3.down * 80f, diameter * 1.6f, skirtColor);
        }

        private static void SpawnHorizonPlane(
            string name, Transform parent, Vector3 worldPos, float worldSize, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.name = name;
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            // Unity plane mesh is 10×10 on XZ.
            float s = Mathf.Max(1f, worldSize / 10f);
            go.transform.localScale = new Vector3(s, 1f, s);
            Object.Destroy(go.GetComponent<Collider>());
            PlanetaryWorldGen.Tint(go, color, 0.04f, ShadowCastingMode.Off);
            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.receiveShadows = false;
                rend.enabled = true;
                // Huge scaled planes can get bad bounds; force always-draw.
                rend.forceRenderingOff = false;
                var mf = go.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                    rend.localBounds = new Bounds(Vector3.zero, Vector3.one * 100f);
            }
        }

        private static void EnsureSky(CelestialBodyProfile body)
        {
            Color voidFill = VoidFillColor(body);
            var shader = Shader.Find("Skybox/Procedural");
            if (shader != null)
            {
                var sky = new Material(shader) { name = $"SM_{body.ShortCode}_Sky" };
                // Procedural _SkyTint multiplies the atmosphere. Dark catalog blues read as dusk greybox.
                Color tint = body.Id == CelestialBodyId.Earth
                    ? new Color(0.62f, 0.78f, 1f)
                    : body.Id == CelestialBodyId.Mars
                        ? new Color(0.95f, 0.58f, 0.32f)
                        : body.Id == CelestialBodyId.Luna
                            ? new Color(0.06f, 0.06f, 0.08f)
                            : body.SkyTop;
                // Must match terrain void — default Procedural ground is mustard/olive yellow.
                if (sky.HasProperty("_SkyTint")) sky.SetColor("_SkyTint", tint);
                sky.SetColor("_GroundColor", voidFill);
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

            ApplyCameraVoidFill(Camera.main, body, shader != null);
        }

        /// <summary>
        /// Clear color must match ground void. SkyTop blue is wrong when rays miss all geometry.
        /// </summary>
        public static void ApplyCameraVoidFill(Camera cam, CelestialBodyProfile body, bool hasSkybox)
        {
            if (cam == null) return;
            Color voidFill = VoidFillColor(body);
            cam.backgroundColor = voidFill;
            // Mars ortho overseer: procedural skybox often reads black in the upper
            // third. Solid haze fill matches the dream-loop concept sky band.
            if (body != null && (body.Id == CelestialBodyId.Mars || body.Id == CelestialBodyId.Luna))
                cam.clearFlags = CameraClearFlags.SolidColor;
            else
                cam.clearFlags = hasSkybox ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
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
            {
                if (WorldHasWater(treeSpots[i], 1.2f)) continue;
                SpawnVistaTree(root, treeSpots[i], body, i);
            }

            Vector3 pondAt = campus + new Vector3(14.5f, 0f, 6.2f);
            SpawnVistaPond(root, pondAt, body);
        }

        private static bool WorldHasWater(Vector3 world, float margin)
        {
            var gen = Object.FindFirstObjectByType<PlanetaryWorldGen>();
            return gen != null && gen.IsOverWater(world, margin);
        }

        /// <summary>
        /// Empty Mars drop: boulder scatter on the baked grade. Mesa / canyon / crater
        /// come from TerrainDataBake — no sphere ridges or crater/dune props.
        /// </summary>
        private static void EnsureMarsVista(Transform parent, CelestialBodyProfile body)
        {
            var old = GameObject.Find("MarsVistaRoot");
            if (old != null)
            {
                old.name = "MarsVistaRoot_old";
                Object.Destroy(old);
            }
            if (body == null || body.Id != CelestialBodyId.Mars) return;

            Vector3 campus = ColonyLayout.CampusOrigin;
            var root = new GameObject("MarsVistaRoot").transform;
            if (parent != null) root.SetParent(parent, false);

            for (int i = 0; i < 24; i++)
            {
                float ang = i * 1.618f * Mathf.PI;
                float rad = 7.4f + (i % 5) * 1.65f;
                Vector3 at = campus + new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);
                SpawnVistaBoulder(root, at, body, i, 0.48f + (i % 4) * 0.16f);
            }

            // Pebble scatter between the yards (concept dirt is grainy, not a smooth field).
            for (int i = 0; i < 30; i++)
            {
                float ang = i * 2.399f + 0.9f;
                float rad = 4.5f + (i % 7) * 1.15f;
                Vector3 at = campus + new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);
                // Odd salt => polyhedral Boulder_B; Boulder_A is a rubble pile on a square patch.
                SpawnVistaBoulder(root, at, body, 201 + i * 2, 0.16f + (i % 3) * 0.07f);
            }
            // Extra grain on the camera-side half (world -z), which fills the lower-right
            // dirt of the Game-tab still; the concept shows ~40+ pebbles there.
            for (int i = 0; i < 90; i++)
            {
                float ang = Mathf.PI + (i + 0.5f) / 90f * Mathf.PI + Mathf.Sin(i * 1.7f) * 0.08f;
                float rad = 5.0f + (i % 11) * 0.85f;
                Vector3 at = campus + new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);
                SpawnVistaBoulder(root, at, body, 301 + i * 2, 0.14f + (i % 4) * 0.05f);
            }

            Vector3[] outcrops =
            {
                campus + new Vector3(12.8f, 0f, 7.6f),
                campus + new Vector3(-13.4f, 0f, 9.2f),
                campus + new Vector3(10.6f, 0f, -12.4f),
                campus + new Vector3(-11.8f, 0f, -8.6f),
                campus + new Vector3(16.4f, 0f, -3.2f),
                campus + new Vector3(-15.6f, 0f, 2.8f)
            };
            for (int i = 0; i < outcrops.Length; i++)
            {
                // Outcrops stay boulder-sized: at 1.15–1.6 m the blocky rock FBX read as beige cubes.
                SpawnVistaBoulder(root, outcrops[i], body, i + 40, 0.58f + (i % 3) * 0.11f);
                SpawnVistaBoulder(root, outcrops[i] + new Vector3(0.85f, 0f, -0.55f), body, i + 60, 0.40f);
                SpawnVistaBoulder(root, outcrops[i] + new Vector3(-0.7f, 0f, 0.7f), body, i + 80, 0.32f);
            }

            // Mesa / canyon / crater live in TerrainDataBake.Height — no sphere ridges,
            // no crater/dune props sitting in the bowls they would double.
        }

        private static float SampleBakeY(float wx, float wz)
        {
            var ground = GameObject.Find("GroundPlane");
            var holder = ground != null ? ground.GetComponent<TerrainBakeHolder>() : null;
            if (holder != null && holder.Bake != null)
                return holder.Bake.SampleHeight(wx, wz);
            return 0f;
        }

        private static void SpawnVistaBoulder(
            Transform parent, Vector3 world, CelestialBodyProfile body, int salt, float scale)
        {
            float gy = SampleBakeY(world.x, world.z);
            var prefab = EnvironmentMeshCatalog.LoadRock(salt);
            var mesh = EnvironmentMeshCatalog.InstantiateClean(prefab, "Dress_MarsBoulder");
            if (mesh != null)
            {
                mesh.transform.SetParent(parent, false);
                mesh.transform.position = new Vector3(world.x, gy, world.z);
                float s = scale / EnvironmentMeshCatalog.RockNativeSize;
                mesh.transform.localScale = Vector3.one * s * (0.9f + (salt % 3) * 0.08f);
                // Keep FBX import axis, yaw only — Euler(tip,yaw,tip) stood the flat base upright.
                Quaternion importRot = prefab != null ? prefab.transform.rotation : mesh.transform.rotation;
                ColonyVisualUtility.SetYawKeepingImport(mesh.transform, importRot, salt * 37f);
                ColonyVisualUtility.SeatFlatOnGround(mesh);
                Color c = Color.Lerp(body.RockColor, body.GroundDark, 0.22f + (salt % 4) * 0.08f);
                PlanetaryWorldGen.Tint(mesh, c, 0.08f, ShadowCastingMode.On);
                ColonyVisualUtility.SnapToGround(mesh, gy);
                return;
            }

            var go = GameObject.CreatePrimitive(salt % 3 == 0 ? PrimitiveType.Sphere : PrimitiveType.Capsule);
            go.name = "Dress_MarsBoulder";
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(world.x, gy + 0.18f * scale, world.z);
            go.transform.localScale = new Vector3(
                scale * (0.85f + (salt % 3) * 0.12f),
                scale * (0.42f + (salt % 2) * 0.18f),
                scale * (0.72f + (salt % 4) * 0.1f));
            go.transform.rotation = Quaternion.Euler(12f * (salt % 5), salt * 37f, 8f * (salt % 3));
            Object.Destroy(go.GetComponent<Collider>());
            Color tint = Color.Lerp(body.RockColor, body.GroundDark, 0.22f + (salt % 4) * 0.08f);
            PlanetaryWorldGen.Tint(go, tint, 0.08f, ShadowCastingMode.On);
            ColonyVisualUtility.SnapToGround(go, gy);
        }

        private static void SpawnGrassTuft(Transform parent, Vector3 world, CelestialBodyProfile body, int salt)
        {
            var grassPrefab = body != null && body.Id == CelestialBodyId.Earth
                ? EnvironmentMeshCatalog.LoadEarthGrass(salt)
                : null;
            if (grassPrefab != null)
            {
                var clump = EnvironmentMeshCatalog.InstantiateVendorNature(
                    grassPrefab, "Dress_Grass", 0.35f + (salt % 4) * 0.08f);
                if (clump != null)
                {
                    clump.transform.SetParent(parent, false);
                    clump.transform.position = world;
                    clump.transform.rotation = Quaternion.Euler(0f, salt * 37f, 0f);
                    ColonyVisualUtility.SnapToGround(clump);
                    if (salt % 5 == 0)
                    {
                        var flowerPrefab = EnvironmentMeshCatalog.LoadEarthFlower(salt);
                        if (flowerPrefab != null)
                        {
                            var bloom = EnvironmentMeshCatalog.InstantiateVendorNature(
                                flowerPrefab, "Dress_Poppy", 0.22f);
                            if (bloom != null)
                            {
                                bloom.transform.SetParent(parent, false);
                                bloom.transform.position = world + new Vector3(0.18f, 0f, 0.12f);
                                ColonyVisualUtility.SnapToGround(bloom);
                            }
                        }
                    }
                    return;
                }
            }

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
            float h = 1.55f + (salt % 4) * 0.28f;
            var prefab = EnvironmentMeshCatalog.LoadTree(salt, body != null ? body.Id : CelestialBodyId.Earth);
            GameObject mesh = null;
            if (prefab != null)
            {
                mesh = EnvironmentMeshCatalog.IsVendorNature(prefab)
                    ? EnvironmentMeshCatalog.InstantiateVendorNature(prefab, "Dress_Tree", h * 1.6f)
                    : EnvironmentMeshCatalog.InstantiateClean(prefab, "Dress_Tree");
            }
            if (mesh != null)
            {
                mesh.transform.SetParent(parent, false);
                mesh.transform.position = world;
                if (EnvironmentMeshCatalog.IsVendorNature(prefab))
                    mesh.transform.rotation = Quaternion.Euler(0f, salt * 41f, 0f);
                else
                {
                    // Keep import orientation (FBX -90 X) then yaw — identity rotation flattens trees.
                    ColonyVisualUtility.SetYawKeepingImport(
                        mesh.transform, mesh.transform.rotation, salt * 41f);
                    float s = h / EnvironmentMeshCatalog.TreeNativeHeight;
                    mesh.transform.localScale = Vector3.one * s;
                }
                ColonyVisualUtility.SnapToGround(mesh);
                if (salt == 0)
                    Debug.Log("[MapDressing] Earth vista trees using " + prefab.name);
                return;
            }
            if (salt == 0)
                Debug.LogWarning("[MapDressing] Earth tree prefab missing — primitive tree fallback");

            var tree = new GameObject("Dress_Tree");
            tree.transform.SetParent(parent, false);
            tree.transform.position = world;

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

            // Overlapping elliptical discs — soft shoreline, same language as lakes/rivers.
            var worldGen = Object.FindFirstObjectByType<PlanetaryWorldGen>();
            for (int i = 0; i < 3; i++)
            {
                var water = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                water.name = i == 0 ? "Water" : $"Water_{i}";
                water.transform.SetParent(pond.transform, false);
                water.transform.localPosition = new Vector3(
                    0.15f + (i - 1) * 0.55f,
                    0.02f,
                    -0.1f + (i % 2) * 0.35f);
                water.transform.localRotation = Quaternion.Euler(0f, 18f + i * 40f, 0f);
                float sx = 4.2f - i * 0.55f;
                float sz = 3.2f - i * 0.35f;
                water.transform.localScale = new Vector3(sx, 0.035f, sz);
                Object.Destroy(water.GetComponent<Collider>());
                StylizedWaterVisual.Apply(
                    water,
                    body.WaterDeep,
                    Color.Lerp(body.WaterDeep, body.WaterShallow, 0.35f + i * 0.1f));
                worldGen?.RegisterExternalWater(water.transform, sx * 0.5f, sz * 0.5f);
            }
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
