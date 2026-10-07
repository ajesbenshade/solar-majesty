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
    /// Contact sheets of building kits, rendered in edit mode so art passes can be judged without a
    /// player build. Each tile is one building on the world's ground at the play camera's isometric
    /// angle, under that world's in-game sun, fill, fog and colour grade (DemoAtmosphere).
    /// <para>CLI: <c>-executeMethod SolarMajesty.EditorTools.BuildingGallery.Run -smOut path.png</c> plus:</para>
    /// <list type="bullet">
    /// <item><c>-smCats core|all|rest|wonders|Cat,Cat</c> — which buildings (default core).</item>
    /// <item><c>-smBody Mars|Earth|Luna|Belt|Europa|all</c> — with <c>all</c>, one sheet per world
    /// (<c>path_Earth.png</c>, …).</item>
    /// <item><c>-smWorlds</c> — one sheet, one row per world, one column per building.</item>
    /// <item><c>-smStages</c> — one row per building, one column per construction stage.</item>
    /// <item><c>-smTile 640x480</c> — tile size; <c>-smCols N</c> — columns for the plain sheet.</item>
    /// <item><c>-smYaw 45</c> — camera yaw (the play camera orbits; 45 is its default).</item>
    /// <item><c>-smNoAtmos</c> — flat studio light instead of the world's grade.</item>
    /// <item><c>-smAnchors</c> — overlay each kit's KitAnchors: yellow body box, magenta roofs,
    /// green doorway lanes, cyan vents. A kit with no anchors gets a red cross on the ground.</item>
    /// </list>
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

        public static readonly BuildingCategory[] Wonders =
        {
            BuildingCategory.ClimateLoom,
            BuildingCategory.AegisSpire,
            BuildingCategory.DeepArchive
        };

        public static readonly CelestialBodyId[] Worlds =
        {
            CelestialBodyId.Earth,
            CelestialBodyId.Luna,
            CelestialBodyId.Mars,
            CelestialBodyId.Belt,
            CelestialBodyId.Europa
        };

        /// <summary>Every category a player or villager can build (Utility is retired).</summary>
        public static BuildingCategory[] AllBuildable()
        {
            var list = new List<BuildingCategory>();
            foreach (BuildingCategory c in Enum.GetValues(typeof(BuildingCategory)))
                if (c != BuildingCategory.Utility) list.Add(c);
            return list.ToArray();
        }

        private struct Options
        {
            public BuildingCategory[] Cats;
            public int TileW, TileH, Cols;
            public float Yaw;
            public bool Atmos;
            public bool Anchors;
        }

        [MenuItem("Solar Majesty/Render/Building Gallery (core set)", priority = 120)]
        public static void RunMenu() =>
            RenderSheet("Docs/ReviewEvidence/building-gallery.png", CelestialBodyId.Mars, Defaults(CoreSet));

        [MenuItem("Solar Majesty/Render/Building Gallery (every building, every world)", priority = 121)]
        public static void RunAllMenu()
        {
            foreach (var w in Worlds)
                RenderSheet($"Docs/ReviewEvidence/building-gallery_{w}.png", w, Defaults(AllBuildable()));
        }

        [MenuItem("Solar Majesty/Render/Building Gallery (construction stages)", priority = 122)]
        public static void RunStagesMenu() =>
            RenderStages("Docs/ReviewEvidence/building-stages.png", CelestialBodyId.Mars, Defaults(CoreSet));

        private static Options Defaults(BuildingCategory[] cats) => new Options
        {
            Cats = cats, TileW = 640, TileH = 480, Cols = 4, Yaw = 45f, Atmos = true
        };

        public static void Run()
        {
            string outPath = Arg("-smOut") ?? "Docs/ReviewEvidence/building-gallery.png";
            var opt = Defaults(ParseCats(Arg("-smCats")));
            string tile = Arg("-smTile");
            if (!string.IsNullOrEmpty(tile))
            {
                var parts = tile.ToLowerInvariant().Split('x');
                if (parts.Length == 2 && int.TryParse(parts[0], out int tw) && int.TryParse(parts[1], out int th))
                {
                    opt.TileW = Mathf.Clamp(tw, 64, 2048);
                    opt.TileH = Mathf.Clamp(th, 64, 2048);
                }
            }
            if (int.TryParse(Arg("-smCols") ?? "", out int cols)) opt.Cols = Mathf.Max(1, cols);
            else opt.Cols = opt.Cats.Length > 12 ? 6 : 4;
            if (float.TryParse(Arg("-smYaw") ?? "", out float yaw)) opt.Yaw = yaw;
            opt.Atmos = !HasArg("-smNoAtmos");
            opt.Anchors = HasArg("-smAnchors");

            string bodyArg = Arg("-smBody") ?? "Mars";
            bool allWorlds = bodyArg.Equals("all", StringComparison.OrdinalIgnoreCase);
            var body = CelestialBodyId.Mars;
            if (!allWorlds && Enum.TryParse(bodyArg, true, out CelestialBodyId parsed)) body = parsed;

            bool ok = true;
            if (HasArg("-smWorlds"))
                ok = RenderWorlds(outPath, opt);
            else if (HasArg("-smStages"))
            {
                if (allWorlds)
                    foreach (var w in Worlds) ok &= RenderStages(Suffixed(outPath, w), w, opt);
                else
                    ok = RenderStages(outPath, body, opt);
            }
            else if (allWorlds)
                foreach (var w in Worlds) ok &= RenderSheet(Suffixed(outPath, w), w, opt);
            else
                ok = RenderSheet(outPath, body, opt);

            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        private static BuildingCategory[] ParseCats(string arg)
        {
            if (string.IsNullOrEmpty(arg) || arg.Equals("core", StringComparison.OrdinalIgnoreCase)) return CoreSet;
            if (arg.Equals("all", StringComparison.OrdinalIgnoreCase)) return AllBuildable();
            if (arg.Equals("wonders", StringComparison.OrdinalIgnoreCase)) return Wonders;
            if (arg.Equals("rest", StringComparison.OrdinalIgnoreCase))
            {
                var rest = new List<BuildingCategory>(AllBuildable());
                rest.RemoveAll(c => Array.IndexOf(CoreSet, c) >= 0);
                return rest.ToArray();
            }
            var list = new List<BuildingCategory>();
            foreach (var name in arg.Split(','))
                if (Enum.TryParse(name.Trim(), true, out BuildingCategory c)) list.Add(c);
                else Debug.LogWarning($"[Gallery] unknown category '{name}'");
            return list.Count > 0 ? list.ToArray() : CoreSet;
        }

        private static string Suffixed(string path, CelestialBodyId w)
        {
            string ext = Path.GetExtension(path);
            return path.Substring(0, path.Length - ext.Length) + "_" + w + (string.IsNullOrEmpty(ext) ? ".png" : ext);
        }

        // ── Sheets ───────────────────────────────────────────────────────────────

        private static bool RenderSheet(string outPath, CelestialBodyId bodyId, Options opt)
        {
            int cols = Mathf.Min(opt.Cols, opt.Cats.Length);
            int rows = Mathf.CeilToInt(opt.Cats.Length / (float)cols);
            using (var studio = new Studio(bodyId, opt))
            {
                var sheet = new Sheet(cols, rows, opt.TileW, opt.TileH);
                for (int b = 0; b < opt.Cats.Length; b++)
                {
                    var go = studio.Spawn(opt.Cats[b]);
                    studio.Frame(go);
                    sheet.Put(b % cols, b / cols, studio.Shoot());
                    Log(opt.Cats[b], bodyId, go);
                    UnityEngine.Object.DestroyImmediate(go);
                }
                return sheet.Write(outPath);
            }
        }

        /// <summary>
        /// Stalker dens in their three looks: active, charted (survey beacon), cleared.
        /// CLI: -executeMethod SolarMajesty.EditorTools.BuildingGallery.RunDens -smOut path.png [-smBody Earth]
        /// </summary>
        public static void RunDens()
        {
            string outPath = Arg("-smOut") ?? "Docs/ReviewEvidence/dens.png";
            var opt = Defaults(new[] { BuildingCategory.Commons });
            opt.TileW = 900; opt.TileH = 700;
            string bodyArg = Arg("-smBody") ?? "Earth";
            var body = Enum.TryParse(bodyArg, true, out CelestialBodyId parsed) ? parsed : CelestialBodyId.Earth;
            bool ok;
            using (var studio = new Studio(body, opt))
            {
                var profile = CelestialBodyCatalog.Get(body);
                var sheet = new Sheet(3, 1, opt.TileW, opt.TileH);
                for (int look = 0; look < 3; look++)
                {
                    var go = new GameObject("Den");
                    var lair = go.AddComponent<StalkerLair>();
                    lair.Configure(null, 2, 8f, profile?.LairRim, profile?.LairPit);
                    if (look >= 1) lair.MarkScouted();
                    if (look == 2) lair.ForceClear();
                    if (look == 0) lair.RestoreChart(false, true);
                    var beacon = go.transform.Find("SurveyBeacon");
                    if (look == 0 && beacon != null) beacon.gameObject.SetActive(false);
                    studio.Frame(go);
                    sheet.Put(look, 0, studio.Shoot());
                    UnityEngine.Object.DestroyImmediate(go);
                }
                ok = sheet.Write(outPath);
            }
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        private static bool RenderStages(string outPath, CelestialBodyId bodyId, Options opt)
        {
            int stageCount = ConstructionStages.Count;
            using (var studio = new Studio(bodyId, opt))
            {
                var sheet = new Sheet(stageCount, opt.Cats.Length, opt.TileW, opt.TileH);
                for (int b = 0; b < opt.Cats.Length; b++)
                {
                    var go = studio.Spawn(opt.Cats[b]);
                    studio.Frame(go);
                    for (int s = 0; s < stageCount; s++)
                    {
                        ConstructionStages.Show(go, ConstructionStages.Snapshot(s));
                        sheet.Put(s, b, studio.Shoot());
                    }
                    Log(opt.Cats[b], bodyId, go);
                    UnityEngine.Object.DestroyImmediate(go);
                }
                return sheet.Write(outPath);
            }
        }

        private static bool RenderWorlds(string outPath, Options opt)
        {
            var sheet = new Sheet(opt.Cats.Length, Worlds.Length, opt.TileW, opt.TileH);
            for (int r = 0; r < Worlds.Length; r++)
            {
                using (var studio = new Studio(Worlds[r], opt))
                {
                    for (int b = 0; b < opt.Cats.Length; b++)
                    {
                        var go = studio.Spawn(opt.Cats[b]);
                        studio.Frame(go);
                        sheet.Put(b, r, studio.Shoot());
                        Log(opt.Cats[b], Worlds[r], go);
                        UnityEngine.Object.DestroyImmediate(go);
                    }
                }
            }
            return sheet.Write(outPath);
        }

        private static void Log(BuildingCategory cat, CelestialBodyId body, GameObject go)
        {
            int rends = go.GetComponentsInChildren<Renderer>(true).Length;
            var mats = new HashSet<Material>();
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials) if (m != null) mats.Add(m);
            Debug.Log($"[Gallery] {body}/{cat}: {rends} renderers, {mats.Count} materials, bounds {WorldBounds(go).size}");
        }

        // ── Studio: scene, light, ground, camera ────────────────────────────────

        private sealed class Studio : IDisposable
        {
            private readonly CelestialBodyProfile _body;
            private readonly Options _opt;
            private readonly Camera _cam;
            private readonly RenderTexture _rt;
            private readonly Texture2D _tile;
            private readonly Material _groundMat;

            public Studio(CelestialBodyId bodyId, Options opt)
            {
                _opt = opt;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                _body = CelestialBodyCatalog.Get(bodyId);
                ModularBuildingFactory.BindBody(_body);
                IndustrialArtDressing.BindBody(_body);
                CampusDressing.Reset();

                var sunGo = new GameObject("Directional Light");
                var sun = sunGo.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.shadows = LightShadows.Soft;
                sun.intensity = _body != null ? _body.SunIntensity : 1.2f;
                sun.color = new Color(1f, 0.95f, 0.88f);
                sunGo.transform.rotation = Quaternion.Euler(42f, 128f, 0f);
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.62f, 0.60f, 0.62f);
                RenderSettings.ambientEquatorColor = new Color(0.48f, 0.40f, 0.36f);
                RenderSettings.ambientGroundColor = new Color(0.30f, 0.20f, 0.16f);

                var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "Ground";
                ground.transform.localScale = new Vector3(20f, 1f, 20f);
                UnityEngine.Object.DestroyImmediate(ground.GetComponent<Collider>());
                _groundMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                Color g = _body != null ? Color.Lerp(_body.GroundDark, _body.GroundLight, 0.55f) : new Color(0.45f, 0.44f, 0.42f);
                _groundMat.SetColor("_BaseColor", g);
                _groundMat.SetFloat("_Smoothness", 0.08f);
                ground.GetComponent<Renderer>().sharedMaterial = _groundMat;

                var camGo = new GameObject("GalleryCam");
                _cam = camGo.AddComponent<Camera>();
                _cam.orthographic = true;
                _cam.nearClipPlane = 0.3f;
                _cam.farClipPlane = 400f;
                _cam.clearFlags = CameraClearFlags.SolidColor;
                _cam.backgroundColor = _body != null ? PlanetaryMapDressing.VoidFillColor(_body) : new Color(0.2f, 0.12f, 0.09f);
                _cam.allowHDR = true;
                camGo.transform.rotation = Quaternion.Euler(30f, opt.Yaw, 0f);

                if (opt.Atmos && _body != null)
                {
                    try
                    {
                        DemoAtmosphere.Apply(_cam, null, _body);
                        // Studio framing is tight; world fog tuned for the play camera would wash tiles.
                        RenderSettings.fog = false;
                        _cam.farClipPlane = 400f;
                        _cam.clearFlags = CameraClearFlags.SolidColor;
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning("[Gallery] atmosphere failed, studio light instead: " + e.Message);
                    }
                }

                _rt = new RenderTexture(opt.TileW, opt.TileH, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
                _tile = new Texture2D(opt.TileW, opt.TileH, TextureFormat.RGB24, false);
                _cam.targetTexture = _rt;
            }

            public GameObject Spawn(BuildingCategory cat)
            {
                int side = ColonyLayout.FootprintSide(cat);
                var go = ModularBuildingFactory.Spawn(cat, Vector3.zero, null, side, side, ColonyLayout.DefaultCellSize);
                var data = ScriptableObject.CreateInstance<BuildingData>();
                data.category = cat;
                try { CampusDressing.DressPlaced(data, go, _body); }
                catch (Exception e) { Debug.LogWarning($"[Gallery] DressPlaced {cat}: {e.Message}"); }
                UnityEngine.Object.DestroyImmediate(data);
                if (_opt.Anchors) AnchorOverlay.Draw(go);
                return go;
            }

            public void Frame(GameObject go)
            {
                Bounds bounds = WorldBounds(go);
                Vector3 focus = new Vector3(bounds.center.x, bounds.min.y + bounds.size.y * 0.35f, bounds.center.z);
                float aspect = _opt.TileW / (float)_opt.TileH;
                _cam.orthographicSize = Mathf.Max(bounds.size.x / aspect * 1.2f, bounds.size.z / aspect * 1.2f,
                    bounds.size.y * 0.9f, Mathf.Max(bounds.size.x, bounds.size.z) * 0.5f) * 0.62f + 1f;
                _cam.transform.position = focus - _cam.transform.forward * 80f;
            }

            public Color[] Shoot()
            {
                RenderWithoutSrpBatcher(_cam);
                RenderTexture.active = _rt;
                _tile.ReadPixels(new Rect(0, 0, _opt.TileW, _opt.TileH), 0, 0);
                _tile.Apply();
                RenderTexture.active = null;
                return _tile.GetPixels();
            }

            public void Dispose()
            {
                _cam.targetTexture = null;
                _rt.Release();
                UnityEngine.Object.DestroyImmediate(_rt);
                UnityEngine.Object.DestroyImmediate(_tile);
                UnityEngine.Object.DestroyImmediate(_groundMat);
            }
        }

        private sealed class Sheet
        {
            private readonly Texture2D _tex;
            private readonly int _rows, _tw, _th;
            private bool _anyLit;

            public Sheet(int cols, int rows, int tw, int th)
            {
                _rows = rows; _tw = tw; _th = th;
                // Worlds mode opens a new scene per row, which unloads unreferenced assets.
                _tex = new Texture2D(cols * tw, rows * th, TextureFormat.RGB24, false) { hideFlags = HideFlags.HideAndDontSave };
                var fill = new Color[cols * tw * rows * th];
                for (int i = 0; i < fill.Length; i++) fill[i] = new Color(0.08f, 0.08f, 0.09f);
                _tex.SetPixels(fill);
            }

            public void Put(int col, int row, Color[] px)
            {
                if (!_anyLit)
                {
                    float sum = 0f; int n = 0;
                    for (int i = 0; i < px.Length; i += 97) { sum += px[i].r + px[i].g + px[i].b; n++; }
                    if (n > 0 && sum / (n * 3f) > 0.04f) _anyLit = true;
                }
                _tex.SetPixels(col * _tw, (_rows - 1 - row) * _th, _tw, _th, px);
            }

            public bool Write(string outPath)
            {
                _tex.Apply();
                string abs = Path.GetFullPath(Path.IsPathRooted(outPath)
                    ? outPath
                    : Path.Combine(Application.dataPath, "..", outPath));
                Directory.CreateDirectory(Path.GetDirectoryName(abs) ?? ".");
                bool ok = _anyLit;
                if (ok)
                {
                    File.WriteAllBytes(abs, _tex.EncodeToPNG());
                    Debug.Log($"[Gallery] wrote {abs}");
                }
                else
                    Debug.LogWarning("[Gallery] every tile rendered black — not writing the sheet.");
                UnityEngine.Object.DestroyImmediate(_tex);
                return ok;
            }
        }

        /// <summary>Debug overlay of a kit's recorded anchors (see <see cref="KitAnchors"/>).</summary>
        private static class AnchorOverlay
        {
            private static readonly Dictionary<Color, Material> Mats = new Dictionary<Color, Material>();

            public static void Draw(GameObject go)
            {
                var root = go.transform;
                var shape = KitAnchors.Of(go);
                var holder = new GameObject("AnchorOverlay").transform;
                holder.SetParent(root, false);
                if (shape == null)
                {
                    float top = WorldBounds(go).max.y - root.position.y + 0.3f;
                    Bar(holder, new Vector3(0, top, 0), new Vector3(4f, 0.12f, 0.3f), Quaternion.Euler(0, 45, 0), Color.red);
                    Bar(holder, new Vector3(0, top, 0), new Vector3(4f, 0.12f, 0.3f), Quaternion.Euler(0, -45, 0), Color.red);
                    return;
                }
                if (shape.HasBody) WireBox(holder, shape.Body.center, shape.Body.size, Quaternion.identity, Color.yellow, 0.04f);
                foreach (var r in shape.Roofs)
                {
                    var rot = Quaternion.Euler(r.Tilt, r.Yaw, 0f);
                    switch (r.Kind)
                    {
                        case RoofKind.Flat:
                        case RoofKind.Slope:
                            WireBox(holder, r.Center, new Vector3(r.Size.x, 0.02f, r.Size.z), rot, Color.magenta, 0.07f);
                            break;
                        case RoofKind.Dome:
                            WireBox(holder, r.Center + Vector3.up * r.Size.y * 0.5f, r.Size, Quaternion.identity, Color.magenta, 0.05f);
                            break;
                        case RoofKind.Vault:
                            WireBox(holder, r.Center + Vector3.up * r.Size.y * 0.5f, r.Size, Quaternion.Euler(0, r.Yaw, 0), Color.magenta, 0.05f);
                            break;
                        case RoofKind.Cylinder:
                            WireBox(holder, r.Center, new Vector3(r.Size.x, r.Size.x, r.Size.z), Quaternion.Euler(0, r.Yaw, 0), Color.magenta, 0.05f);
                            break;
                    }
                }
                foreach (var d in shape.Doors)
                {
                    Vector3 n = d.Normal.sqrMagnitude > 1e-4f ? d.Normal.normalized : Vector3.back;
                    var rot = Quaternion.LookRotation(n, Vector3.up);
                    Bar(holder, d.Position + n * 1.1f + Vector3.up * 0.03f, new Vector3(d.Width, 0.05f, 2.2f), rot, Color.green);
                    Bar(holder, d.Position + Vector3.up * d.Height, new Vector3(d.Width, 0.06f, 0.06f), rot, Color.green);
                }
                foreach (var e in shape.Emitters)
                    Bar(holder, e.Position, Vector3.one * 0.3f, Quaternion.identity, Color.cyan);
            }

            private static void WireBox(Transform parent, Vector3 c, Vector3 size, Quaternion rot, Color col, float t)
            {
                Vector3 h = size * 0.5f;
                for (int i = 0; i < 12; i++)
                {
                    int axis = i / 4;
                    float sa = (i & 1) == 0 ? -1 : 1, sb = (i & 2) == 0 ? -1 : 1;
                    Vector3 off, sc;
                    if (axis == 0) { off = new Vector3(0, sa * h.y, sb * h.z); sc = new Vector3(size.x, t, t); }
                    else if (axis == 1) { off = new Vector3(sa * h.x, 0, sb * h.z); sc = new Vector3(t, size.y, t); }
                    else { off = new Vector3(sa * h.x, sb * h.y, 0); sc = new Vector3(t, t, size.z); }
                    Bar(parent, c + rot * off, sc, rot, col);
                }
            }

            private static void Bar(Transform parent, Vector3 localPos, Vector3 size, Quaternion rot, Color col)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
                UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
                g.name = "Anchor";
                g.transform.SetParent(parent, false);
                g.transform.localPosition = localPos;
                g.transform.localRotation = rot;
                g.transform.localScale = new Vector3(Mathf.Max(size.x, 0.01f), Mathf.Max(size.y, 0.01f), Mathf.Max(size.z, 0.01f));
                if (!Mats.TryGetValue(col, out var m) || m == null)
                {
                    m = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { hideFlags = HideFlags.DontUnloadUnusedAsset };
                    m.SetColor("_BaseColor", col);
                    Mats[col] = m;
                }
                var r = g.GetComponent<Renderer>();
                r.sharedMaterial = m;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        private static Bounds WorldBounds(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            var b = new Bounds(go.transform.position, Vector3.one);
            bool first = true;
            foreach (var r in rends)
            {
                if (!r.enabled || r.name.Contains("Footprint") || r.name.Contains("SelectRing") || r.name == "Anchor") continue;
                if (first) { b = r.bounds; first = false; }
                else b.Encapsulate(r.bounds);
            }
            return b;
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
