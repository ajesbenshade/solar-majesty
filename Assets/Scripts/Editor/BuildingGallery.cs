#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SolarMajesty.EditorTools
{
    /// <summary>
    /// Contact sheet of building kits, rendered in edit mode so art passes can be judged without a
    /// player build. One tile per building on Mars ground at the play camera's isometric angle;
    /// with <c>-smStages</c>, one row per building showing each construction stage.
    /// CLI: <c>-executeMethod SolarMajesty.EditorTools.BuildingGallery.Run -smOut path.png [-smStages] [-smBody Mars]</c>
    /// </summary>
    public static class BuildingGallery
    {
        public static readonly BuildingCategory[] CoreSet =
        {
            BuildingCategory.Commons,
            BuildingCategory.Habitat,
            BuildingCategory.EngineerWorkshop,
            BuildingCategory.Power,
            BuildingCategory.LandingPad,
            BuildingCategory.Defense,
            BuildingCategory.Watchtower,
            BuildingCategory.Market
        };

        private const int TileW = 640;
        private const int TileH = 480;

        [MenuItem("Solar Majesty/Render/Building Gallery (core set)", priority = 120)]
        public static void RunMenu() => Render("Docs/ReviewEvidence/building-gallery.png", false, CelestialBodyId.Mars);

        [MenuItem("Solar Majesty/Render/Building Gallery (construction stages)", priority = 121)]
        public static void RunStagesMenu() => Render("Docs/ReviewEvidence/building-stages.png", true, CelestialBodyId.Mars);

        public static void Run()
        {
            string outPath = Arg("-smOut") ?? "Docs/ReviewEvidence/building-gallery.png";
            bool stages = HasArg("-smStages");
            var body = CelestialBodyId.Mars;
            if (Enum.TryParse(Arg("-smBody") ?? "", true, out CelestialBodyId parsed)) body = parsed;
            bool ok = Render(outPath, stages, body);
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        private static bool Render(string outPath, bool stages, CelestialBodyId bodyId)
        {
            string abs = Path.GetFullPath(Path.Combine(Application.dataPath, "..", outPath));
            Directory.CreateDirectory(Path.GetDirectoryName(abs) ?? ".");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var body = CelestialBodyCatalog.Get(bodyId);
            ModularBuildingFactory.BindBody(body);
            IndustrialArtDressing.BindBody(body);
            CampusDressing.Reset();

            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.intensity = body != null ? body.SunIntensity : 1.2f;
            sun.color = new Color(1f, 0.95f, 0.88f);
            sunGo.transform.rotation = Quaternion.Euler(42f, 128f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.60f, 0.62f);
            RenderSettings.ambientEquatorColor = new Color(0.48f, 0.40f, 0.36f);
            RenderSettings.ambientGroundColor = new Color(0.30f, 0.20f, 0.16f);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(20f, 1f, 20f);
            var groundMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            groundMat.SetColor("_BaseColor", bodyId == CelestialBodyId.Mars
                ? new Color(0.62f, 0.30f, 0.17f)
                : new Color(0.45f, 0.44f, 0.42f));
            groundMat.SetFloat("_Smoothness", 0.08f);
            ground.GetComponent<Renderer>().sharedMaterial = groundMat;

            var camGo = new GameObject("GalleryCam");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 400f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.20f, 0.12f, 0.09f);
            cam.allowHDR = true;
            camGo.transform.rotation = Quaternion.Euler(30f, 45f, 0f);

            int stageCount = stages ? ConstructionStages.Count : 1;
            int cols = stages ? stageCount : 4;
            int rows = stages ? CoreSet.Length : Mathf.CeilToInt(CoreSet.Length / 4f);
            var sheet = new Texture2D(cols * TileW, rows * TileH, TextureFormat.RGB24, false);
            var rt = new RenderTexture(TileW, TileH, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            var tile = new Texture2D(TileW, TileH, TextureFormat.RGB24, false);
            cam.targetTexture = rt;

            bool anyLit = false;
            for (int b = 0; b < CoreSet.Length; b++)
            {
                var cat = CoreSet[b];
                int side = ColonyLayout.FootprintSide(cat);
                float cell = ColonyLayout.DefaultCellSize;
                var go = ModularBuildingFactory.Spawn(cat, Vector3.zero, null, side, side, cell);
                var data = ScriptableObject.CreateInstance<BuildingData>();
                data.category = cat;
                CampusDressing.DressPlaced(data, go, body);
                UnityEngine.Object.DestroyImmediate(data);

                Bounds bounds = WorldBounds(go);
                Vector3 focus = new Vector3(bounds.center.x, bounds.min.y + bounds.size.y * 0.35f, bounds.center.z);
                cam.orthographicSize = Mathf.Max(bounds.size.x, bounds.size.z, bounds.size.y * 1.4f) * 0.62f + 1f;
                camGo.transform.position = focus - camGo.transform.forward * 80f;

                for (int s = 0; s < stageCount; s++)
                {
                    if (stages) ConstructionStages.Show(go, ConstructionStages.Snapshot(s));
                    RenderWithoutSrpBatcher(cam);
                    RenderTexture.active = rt;
                    tile.ReadPixels(new Rect(0, 0, TileW, TileH), 0, 0);
                    tile.Apply();
                    RenderTexture.active = null;
                    if (AverageLum(tile) > 10f) anyLit = true;

                    int col = stages ? s : b % cols;
                    int row = stages ? b : b / cols;
                    sheet.SetPixels(col * TileW, (rows - 1 - row) * TileH, TileW, TileH, tile.GetPixels());
                }
                Debug.Log($"[Gallery] {cat}: {CountRenderers(go)} renderers, bounds {bounds.size}");
                UnityEngine.Object.DestroyImmediate(go);
            }

            cam.targetTexture = null;
            sheet.Apply();
            if (anyLit)
            {
                File.WriteAllBytes(abs, sheet.EncodeToPNG());
                Debug.Log($"[Gallery] wrote {abs}");
            }
            else
                Debug.LogWarning("[Gallery] every tile rendered black — not writing the sheet.");

            UnityEngine.Object.DestroyImmediate(sheet);
            UnityEngine.Object.DestroyImmediate(tile);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(camGo);
            UnityEngine.Object.DestroyImmediate(sunGo);
            UnityEngine.Object.DestroyImmediate(ground);
            UnityEngine.Object.DestroyImmediate(groundMat);
            return anyLit;
        }

        private static Bounds WorldBounds(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            var b = new Bounds(go.transform.position, Vector3.one);
            bool first = true;
            foreach (var r in rends)
            {
                if (!r.enabled || r.name.Contains("Footprint")) continue;
                if (first) { b = r.bounds; first = false; }
                else b.Encapsulate(r.bounds);
            }
            return b;
        }

        private static int CountRenderers(GameObject go) => go.GetComponentsInChildren<Renderer>(true).Length;

        private static float AverageLum(Texture2D t)
        {
            var px = t.GetPixels32();
            long sum = 0;
            int n = 0;
            for (int i = 0; i < px.Length; i += 64) { sum += px[i].r + px[i].g + px[i].b; n++; }
            return n > 0 ? sum / (float)(n * 3) : 0f;
        }

        /// <summary>See DemoContentBuilder: the SRP Batcher leaks per-material constants in edit-mode renders.</summary>
        private static void RenderWithoutSrpBatcher(Camera cam)
        {
            var urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline
                as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            bool prior = urp != null && urp.useSRPBatcher;
            if (urp != null) urp.useSRPBatcher = false;
            try { cam.Render(); }
            finally { if (urp != null) urp.useSRPBatcher = prior; }
        }

        private static string Arg(string flag)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == flag) return args[i + 1];
            return null;
        }

        private static bool HasArg(string flag) => Array.IndexOf(Environment.GetCommandLineArgs(), flag) >= 0;
    }
}
#endif
