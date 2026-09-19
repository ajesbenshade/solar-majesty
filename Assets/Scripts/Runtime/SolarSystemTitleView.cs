using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SolarMajesty
{
    /// <summary>
    /// Click target on a title-screen planet or moon. Menu navigation only.
    /// </summary>
    public sealed class SolarSystemPickTarget : MonoBehaviour
    {
        public CelestialBodyId Body;
        public string Caption;
        public float SpinDegPerSec;
        public float PickMul = 1.28f;
    }

    /// <summary>
    /// Procedural orrery for the title screen. Steals the play camera while shown,
    /// animates with unscaled time (title timescale is 0), and raycasts sphere
    /// colliders for body picks. Does not post flags or move specialists.
    /// </summary>
    [DefaultExecutionOrder(40)]
    public sealed class SolarSystemTitleView : MonoBehaviour
    {
        public static SolarSystemTitleView Instance { get; private set; }

        public string HoverCaption { get; private set; }
        public CelestialBodyId? HoveredBody { get; private set; }
        public bool IsShown => _shown;

        private GameLoop _loop;
        private Camera _cam;
        private Transform _rig;
        private Transform _pivot;
        private bool _built;
        private bool _shown;
        private bool _stolen;
        private SolarSystemPickTarget _hover;

        private readonly List<Light> _disabledLights = new List<Light>(8);
        private readonly List<Material> _mats = new List<Material>(24);
        private readonly List<SolarSystemPickTarget> _picks = new List<SolarSystemPickTarget>(8);
        private readonly List<Transform> _spinners = new List<Transform>(12);
        private readonly List<float> _spinRates = new List<float>(12);
        private bool _camPost;

        private bool _fog;
        private FogMode _fogMode;
        private Color _fogColor;
        private float _fogStart;
        private float _fogEnd;
        private float _fogDensity;
        private AmbientMode _ambientMode;
        private Color _ambientSky;
        private Color _ambientEquator;
        private Color _ambientGround;
        private Color _ambientFlat;
        private Material _skybox;
        private float _ambientIntensity;

        private bool _camOrtho;
        private float _camSize;
        private float _camNear;
        private float _camFar;
        private float _camFov;
        private CameraClearFlags _camClear;
        private Color _camBg;
        private Vector3 _camPos;
        private Quaternion _camRot;

        public static SolarSystemTitleView Ensure(GameLoop loop, Camera cam)
        {
            if (loop == null) return null;
            var view = loop.GetComponent<SolarSystemTitleView>();
            if (view == null) view = loop.gameObject.AddComponent<SolarSystemTitleView>();
            view._loop = loop;
            view._cam = cam;
            Instance = view;
            return view;
        }

        private void OnEnable() => Instance = this;

        private void OnDisable()
        {
            if (Instance == this) Instance = null;
            if (_shown) Hide();
        }

        private void OnDestroy()
        {
            if (_shown) Hide();
            for (int i = 0; i < _mats.Count; i++)
            {
                if (_mats[i] != null) Destroy(_mats[i]);
            }
            _mats.Clear();
        }

        public void Show()
        {
            if (_shown) return;
            if (_cam == null && Camera.main != null) _cam = Camera.main;
            if (_cam == null) return;
            if (!_built) Build();
            if (_rig != null) _rig.gameObject.SetActive(true);
            if (!_stolen) StealCamera();
            DimWorldLights(true);
            ApplySpaceLighting();
            _shown = true;
        }

        public void Hide()
        {
            if (!_shown) return;
            if (_rig != null) _rig.gameObject.SetActive(false);
            if (_stolen) RestoreCamera();
            DimWorldLights(false);
            RestoreSpaceLighting();
            _shown = false;
            HoverCaption = null;
            HoveredBody = null;
            _hover = null;
        }

        private void Update()
        {
            if (!_shown || _pivot == null) return;
            float dt = Time.unscaledDeltaTime;
            if (!DemoSettings.ReduceMotion)
            {
                _pivot.Rotate(0f, 7.2f * dt, 0f, Space.Self);
                for (int i = 0; i < _spinners.Count; i++)
                {
                    if (_spinners[i] != null)
                        _spinners[i].Rotate(0f, _spinRates[i] * dt, 0f, Space.Self);
                }
            }

            TickHoverAndClick();
        }

        private void TickHoverAndClick()
        {
            if (_loop != null && _loop.Screen != DemoScreen.Title) return;
            if (_loop != null && _loop.TitleConfirmOpen) return;
            if (_cam == null) return;

            bool overHud = _loop != null && _loop.TitlePointerBlocksWorld;
            SolarSystemPickTarget next = overHud ? null : PickUnderCursor();

            if (next != _hover)
            {
                SetHighlight(_hover, false);
                _hover = next;
                SetHighlight(_hover, true);
            }

            if (_hover != null)
            {
                HoveredBody = _hover.Body;
                bool locked = !CampaignProgress.IsUnlocked(_hover.Body);
                HoverCaption = locked
                    ? $"{_hover.Caption}  ·  locked"
                    : _hover.Caption;
            }
            else
            {
                HoveredBody = null;
                HoverCaption = null;
            }

            if (overHud || _hover == null) return;
            if (!Input.GetMouseButtonDown(0)) return;
            bool cheat = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            _loop?.PlayBodyFromTitle(_hover.Body, cheat);
        }

        private SolarSystemPickTarget PickUnderCursor()
        {
            Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
            SolarSystemPickTarget best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < _picks.Count; i++)
            {
                var pick = _picks[i];
                if (pick == null) continue;
                float radius = 0.5f * pick.transform.lossyScale.x * Mathf.Max(1.05f, pick.PickMul);
                if (!SolarSystemTitlePick.RayHitsSphere(
                        ray.origin, ray.direction, pick.transform.position, radius, out float dist))
                    continue;
                if (dist >= bestDist) continue;
                bestDist = dist;
                best = pick;
            }
            return best;
        }

        private void SetHighlight(SolarSystemPickTarget target, bool on)
        {
            if (target == null) return;
            float s = on ? 1.12f : 1f;
            target.transform.localScale = Vector3.one * ScaleFor(target.Body) * s;
        }

        private static float ScaleFor(CelestialBodyId id)
        {
            switch (id)
            {
                case CelestialBodyId.Earth: return SolarSystemOrbits.EarthScale;
                case CelestialBodyId.Luna: return SolarSystemOrbits.LunaScale;
                case CelestialBodyId.Mars: return SolarSystemOrbits.MarsScale;
                case CelestialBodyId.Belt: return SolarSystemOrbits.BeltHubScale;
                case CelestialBodyId.Europa: return SolarSystemOrbits.EuropaScale;
                default: return 1f;
            }
        }

        private void Build()
        {
            var rig = new GameObject("SM_SolarTitle");
            rig.hideFlags = HideFlags.HideAndDontSave;
            rig.transform.SetParent(null, true);
            rig.transform.position = new Vector3(0f, SolarSystemOrbits.TitleAltitude, 0f);
            rig.transform.rotation = Quaternion.identity;
            _rig = rig.transform;

            _pivot = new GameObject("Pivot").transform;
            _pivot.SetParent(_rig, false);

            SpawnStars(_rig);
            SpawnSun(_pivot);
            var earthAnchor = Anchor(_pivot, "EarthAnchor",
                SolarSystemOrbits.OnOrbit(SolarSystemOrbits.EarthOrbit, 0.55f));
            var earth = SpawnBody(earthAnchor, "Earth", CelestialBodyId.Earth, "Earth",
                Vector3.zero, SolarSystemOrbits.EarthScale, Color.white, "Earth", 22f);
            SpawnClouds(earth.transform);
            SpawnBody(earthAnchor, "Luna", CelestialBodyId.Luna, "Luna",
                SolarSystemOrbits.OnOrbit(SolarSystemOrbits.LunaOrbit, 1.1f),
                SolarSystemOrbits.LunaScale, Color.white, "Luna", -8f);
            SpawnBody(_pivot, "Mars", CelestialBodyId.Mars, "Mars",
                SolarSystemOrbits.OnOrbit(SolarSystemOrbits.MarsOrbit, 2.05f),
                SolarSystemOrbits.MarsScale, Color.white, "Mars", 18f);
            SpawnBelt(_pivot);
            var jupiterAnchor = Anchor(_pivot, "JupiterAnchor",
                SolarSystemOrbits.OnOrbit(SolarSystemOrbits.JupiterOrbit, 4.15f));
            var jupiter = SpawnGlobe(jupiterAnchor, "Jupiter",
                Vector3.zero, SolarSystemOrbits.JupiterScale, Color.white, "Jupiter");
            RegisterSpin(jupiter.transform, 32f);
            SpawnBody(jupiterAnchor, "Europa", CelestialBodyId.Europa, "Europa",
                SolarSystemOrbits.OnOrbit(SolarSystemOrbits.EuropaOrbit, 0.4f),
                SolarSystemOrbits.EuropaScale, Color.white, "Europa", 11f);

            Ring(_pivot, SolarSystemOrbits.EarthOrbit, new Color(0.55f, 0.70f, 0.90f, 0.55f));
            Ring(_pivot, SolarSystemOrbits.MarsOrbit, new Color(0.80f, 0.45f, 0.28f, 0.50f));
            Ring(_pivot, SolarSystemOrbits.BeltOrbit, new Color(0.72f, 0.68f, 0.55f, 0.42f));
            Ring(_pivot, SolarSystemOrbits.JupiterOrbit, new Color(0.86f, 0.70f, 0.42f, 0.40f));

            _built = true;
        }

        private void SpawnSun(Transform parent)
        {
            var sun = SpawnGlobe(parent, "Sun", Vector3.zero, SolarSystemOrbits.SunScale,
                new Color(1.15f, 1.05f, 0.85f), "Sun");
            RegisterSpin(sun.transform, 6f);
        }

        private void SpawnClouds(Transform earth)
        {
            var clouds = SpawnGlobe(earth, "EarthClouds", Vector3.zero, 1.035f,
                new Color(1f, 1f, 1f, 0.55f), "EarthClouds", additive: true);
            RegisterSpin(clouds.transform, 8f);
        }

        private void SpawnBelt(Transform parent)
        {
            var hub = SpawnBody(parent, "Belt", CelestialBodyId.Belt, "Main Belt",
                SolarSystemOrbits.OnOrbit(SolarSystemOrbits.BeltOrbit, 3.35f),
                SolarSystemOrbits.BeltHubScale, Color.white, "Ceres", 9f);
            hub.GetComponent<SolarSystemPickTarget>().PickMul = 3.6f;
            var rng = new System.Random(11);
            for (int i = 0; i < 14; i++)
            {
                float a = i / 14f * Mathf.PI * 2f + 0.07f * i;
                float r = 0.85f + (float)rng.NextDouble() * 0.9f;
                SpawnGlobe(hub.transform, "Rock_" + i,
                    new Vector3(Mathf.Cos(a) * r, ((i % 5) - 2) * 0.10f, Mathf.Sin(a) * r),
                    0.16f + (i % 3) * 0.05f,
                    Color.Lerp(new Color(0.58f, 0.52f, 0.44f), new Color(0.78f, 0.70f, 0.58f), i / 14f),
                    "Ceres");
            }
        }

        private static Transform Anchor(Transform parent, string name, Vector3 local)
        {
            var go = new GameObject(name);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localScale = Vector3.one;
            return go.transform;
        }

        private GameObject SpawnBody(
            Transform parent, string name, CelestialBodyId id, string caption,
            Vector3 local, float scale, Color color, string map, float spinDeg)
        {
            var go = SpawnGlobe(parent, name, local, scale, color, map);
            var pick = go.AddComponent<SolarSystemPickTarget>();
            pick.Body = id;
            pick.Caption = caption;
            pick.SpinDegPerSec = spinDeg;
            pick.PickMul = 1.28f;
            _picks.Add(pick);
            RegisterSpin(go.transform, spinDeg);
            return go;
        }

        private GameObject SpawnGlobe(
            Transform parent, string name, Vector3 local, float scale, Color color,
            string map = null, bool additive = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.hideFlags = HideFlags.HideAndDontSave;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localScale = Vector3.one * scale;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            var rend = go.GetComponent<MeshRenderer>();
            rend.sharedMaterial = MakeGlobe(color, map, additive);
            rend.shadowCastingMode = ShadowCastingMode.Off;
            rend.receiveShadows = false;
            return go;
        }

        private void RegisterSpin(Transform t, float degPerSec)
        {
            _spinners.Add(t);
            _spinRates.Add(degPerSec);
        }

        private void SpawnStars(Transform parent)
        {
            var stars = new GameObject("Stars").transform;
            stars.SetParent(parent, false);
            var rng = new System.Random(203);
            for (int i = 0; i < 70; i++)
            {
                Vector3 dir = new Vector3(
                    (float)rng.NextDouble() * 2f - 1f,
                    (float)rng.NextDouble() * 1.2f - 0.15f,
                    (float)rng.NextDouble() * 2f - 1f).normalized;
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "Star_" + i;
                go.hideFlags = HideFlags.HideAndDontSave;
                go.transform.SetParent(stars, false);
                go.transform.localPosition = dir * (34f + (i % 7) * 1.1f);
                go.transform.localScale = Vector3.one * (0.04f + (i % 5) * 0.012f);
                var col = go.GetComponent<Collider>();
                if (col != null) Object.DestroyImmediate(col);
                var rend = go.GetComponent<MeshRenderer>();
                rend.shadowCastingMode = ShadowCastingMode.Off;
                rend.receiveShadows = false;
                rend.sharedMaterial = MakeGlobe(new Color(0.85f, 0.90f, 1f), null);
            }
        }

        private void Ring(Transform parent, float radius, Color color)
        {
            var go = new GameObject("Orbit_" + radius.ToString("0.0"));
            go.hideFlags = HideFlags.HideAndDontSave;
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = 72;
            lr.startWidth = 0.07f;
            lr.endWidth = 0.07f;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            for (int i = 0; i < 72; i++)
            {
                float a = i / 72f * Mathf.PI * 2f;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
            }
            lr.sharedMaterial = MakeGlobe(color, null);
            lr.startColor = color;
            lr.endColor = color;
        }

        private Material MakeGlobe(Color color, string map, bool additive = false)
        {
            var sh = Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Sprites/Default")
                     ?? Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(sh) { name = "SM_OrreryUnlit", hideFlags = HideFlags.HideAndDontSave };
            Texture2D tex = null;
            if (!string.IsNullOrEmpty(map))
                tex = Resources.Load<Texture2D>("World/Orrery/" + map);
            Color tint = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
            if (mat.HasProperty("_Color")) mat.color = tint;
            if (tex != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            }
            if (sh != null && sh.name.IndexOf("Unlit", System.StringComparison.Ordinal) < 0 &&
                mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", tint);
                if (tex != null && mat.HasProperty("_EmissionMap"))
                    mat.SetTexture("_EmissionMap", tex);
            }
            if (additive)
            {
                ColonyVisualUtility.ApplyTransparent(mat);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            }
            _mats.Add(mat);
            return mat;
        }

        private void StealCamera()
        {
            if (_cam == null) return;
            _camOrtho = _cam.orthographic;
            _camSize = _cam.orthographicSize;
            _camNear = _cam.nearClipPlane;
            _camFar = _cam.farClipPlane;
            _camFov = _cam.fieldOfView;
            _camClear = _cam.clearFlags;
            _camBg = _cam.backgroundColor;
            _camPos = _cam.transform.position;
            _camRot = _cam.transform.rotation;

            Vector3 origin = new Vector3(0f, SolarSystemOrbits.TitleAltitude, 0f);
            _cam.orthographic = false;
            _cam.fieldOfView = 48f;
            _cam.nearClipPlane = 0.4f;
            _cam.farClipPlane = 90f;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.012f, 0.014f, 0.03f);
            _cam.transform.position = origin + SolarSystemOrbits.CameraLocal;
            _cam.transform.LookAt(origin + SolarSystemOrbits.LookLocal, Vector3.up);
            var additional = _cam.GetComponent<UniversalAdditionalCameraData>();
            if (additional != null)
            {
                _camPost = additional.renderPostProcessing;
                additional.renderPostProcessing = false;
            }
            _stolen = true;
        }

        private void RestoreCamera()
        {
            if (_cam == null)
            {
                _stolen = false;
                return;
            }
            _cam.orthographic = _camOrtho;
            _cam.orthographicSize = _camSize;
            _cam.nearClipPlane = _camNear;
            _cam.farClipPlane = _camFar;
            _cam.fieldOfView = _camFov;
            _cam.clearFlags = _camClear;
            _cam.backgroundColor = _camBg;
            _cam.transform.SetPositionAndRotation(_camPos, _camRot);
            var additional = _cam.GetComponent<UniversalAdditionalCameraData>();
            if (additional != null)
                additional.renderPostProcessing = _camPost;
            _stolen = false;
        }

        private void DimWorldLights(bool dim)
        {
            if (dim)
            {
                _disabledLights.Clear();
                var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
                for (int i = 0; i < lights.Length; i++)
                {
                    var light = lights[i];
                    if (light == null || !light.enabled) continue;
                    if (light.type != LightType.Directional) continue;
                    light.enabled = false;
                    _disabledLights.Add(light);
                }
            }
            else
            {
                for (int i = 0; i < _disabledLights.Count; i++)
                {
                    if (_disabledLights[i] != null)
                        _disabledLights[i].enabled = true;
                }
                _disabledLights.Clear();
            }
        }

        private void ApplySpaceLighting()
        {
            _fog = RenderSettings.fog;
            _fogMode = RenderSettings.fogMode;
            _fogColor = RenderSettings.fogColor;
            _fogStart = RenderSettings.fogStartDistance;
            _fogEnd = RenderSettings.fogEndDistance;
            _fogDensity = RenderSettings.fogDensity;
            _ambientMode = RenderSettings.ambientMode;
            _ambientSky = RenderSettings.ambientSkyColor;
            _ambientEquator = RenderSettings.ambientEquatorColor;
            _ambientGround = RenderSettings.ambientGroundColor;
            _ambientFlat = RenderSettings.ambientLight;
            _ambientIntensity = RenderSettings.ambientIntensity;
            _skybox = RenderSettings.skybox;

            RenderSettings.fog = false;
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.07f, 0.08f, 0.12f);
            RenderSettings.ambientIntensity = 0.35f;
        }

        private void RestoreSpaceLighting()
        {
            RenderSettings.fog = _fog;
            RenderSettings.fogMode = _fogMode;
            RenderSettings.fogColor = _fogColor;
            RenderSettings.fogStartDistance = _fogStart;
            RenderSettings.fogEndDistance = _fogEnd;
            RenderSettings.fogDensity = _fogDensity;
            RenderSettings.ambientMode = _ambientMode;
            RenderSettings.ambientSkyColor = _ambientSky;
            RenderSettings.ambientEquatorColor = _ambientEquator;
            RenderSettings.ambientGroundColor = _ambientGround;
            RenderSettings.ambientLight = _ambientFlat;
            RenderSettings.ambientIntensity = _ambientIntensity;
            RenderSettings.skybox = _skybox;
        }
    }
}
