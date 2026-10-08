using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace SolarMajesty
{
    /// <summary>
    /// Five-second launch intro. Plays <c>Resources/Intro/IntroTimeline</c> on a
    /// <see cref="PlayableDirector"/> when that Timeline exists (camera track, title
    /// activation track, sting audio track). Otherwise sweeps the camera in code,
    /// shows the same placeholder title, and plays <c>Resources/Intro/IntroSting</c> if present.
    /// Real-time: the director uses unscaled time, and the colony clock stays paused.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class IntroSequence : MonoBehaviour
    {
        [SerializeField] private PlayableDirector director;
        [SerializeField] private GameObject titleRoot;
        [SerializeField] private AudioSource stingSource;
        [SerializeField] private bool spawnEarthGlobe = true;

        public bool IsPlaying => _playing;
        public IntroPlayback Playback { get; private set; }
        public GameObject TitleRoot => titleRoot;
        public float Elapsed => _elapsed;
        public float OverlayAlpha => _overlay;

        public event System.Action Finished;

        /// <summary>Skip waits one frame so the click cannot land on a title button. A finished shot does not.</summary>
        public bool DeferTitleHandoff => _skipped;

        private bool _playing;
        private bool _skipped;
        private bool _reduceMotion;
        private bool _titleBound;
        private bool _stingPlayed;
        private bool _pushedFog;
        private bool _tintedDusk;
        private bool _crossfading;
        private float _elapsed;
        private float _fadeClock;
        private float _overlay;
        private Vector3 _focus;
        private Camera _cam;
        private Light _sun;
        private Light _fill;
        private Transform _earth;
        private Material _earthMat;
        private CameraSnapshot _camera;
        private LightSnapshot _sunSnap;
        private LightSnapshot _fillSnap;
        private bool _haveSun;
        private bool _haveFill;
        private bool _haveAmbient;
        private Light _forcedSun;
        private Light _forcedFill;
        private FogSnapshot _fog;
        private IntroAmbientSample _ambient;
        private Texture2D _fadeTex;
        private TitlePart[] _parts = System.Array.Empty<TitlePart>();
        private Renderer[] _gold = System.Array.Empty<Renderer>();
        private Vector4[] _goldSt = System.Array.Empty<Vector4>();
        private MaterialPropertyBlock _glintBlock;
        private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");

        public static IntroPlayback ChoosePlayback(bool timelinePresent) =>
            timelinePresent ? IntroPlayback.Timeline : IntroPlayback.Fallback;

        public static TimelineAsset LoadTimeline() =>
            Resources.Load<TimelineAsset>(IntroAssets.TimelineResource);

        public static AudioClip LoadSting() =>
            Resources.Load<AudioClip>(IntroAssets.StingResource);

        public static IntroSequence Ensure()
        {
            var existing = FindAnyObjectByType<IntroSequence>();
            if (existing != null) return existing;
            var go = new GameObject("Intro");
            return go.AddComponent<IntroSequence>();
        }

        private void Awake()
        {
            if (_playing) return;
            HideProps();
        }

        /// <summary>Code path used when no Timeline asset is available. Edit-mode tests call this.</summary>
        public void PlayFallback(Camera camera, Vector3 colonyFocus, CelestialBodyProfile body, bool reduceMotion = false)
        {
            _reduceMotion = reduceMotion;
            Begin(camera, colonyFocus, body, null);
        }

        public void Play(Camera camera, Vector3 colonyFocus, CelestialBodyProfile body, bool reduceMotion)
        {
            _reduceMotion = reduceMotion;
            Begin(camera, colonyFocus, body, reduceMotion ? null : LoadTimeline());
        }

        public void Advance(float unscaledDelta)
        {
            if (!_playing) return;
            _elapsed += Mathf.Max(0f, unscaledDelta);
            ApplyClock(_elapsed);
            if (_elapsed >= IntroShot.Duration)
                Finish(skipped: false);
        }

        /// <summary>Keeps the crossfade moving after the shot has handed off. One step per frame, no hold at black.</summary>
        public void AdvanceCrossfade(float unscaledDelta)
        {
            if (_playing || !_crossfading) return;
            _fadeClock += Mathf.Max(0f, unscaledDelta);
            _overlay = IntroShot.CrossfadeAlpha(_fadeClock);
            if (_fadeClock >= IntroShot.CrossfadeSeconds * 2f)
            {
                _overlay = 0f;
                _crossfading = false;
            }
        }

        /// <summary>Edit-mode tests pass their own lights so the open scene's sun is left alone.</summary>
        internal void SetLightsForTests(Light sun, Light fill)
        {
            _forcedSun = sun;
            _forcedFill = fill;
        }

        /// <summary>Test hook. Armed skip uses the same rule as a real key, click, or pad button.</summary>
        public void NotifyInput(IntroInputSample sample)
        {
            if (!_playing) return;
            if (_elapsed < IntroShot.SkipArmSeconds) return;
            if (IntroSkip.ShouldSkip(sample))
                Skip();
        }

        public void Skip()
        {
            if (!_playing) return;
            _overlay = 0f;
            Finish(skipped: true);
        }

        /// <summary>Stop without telling the boot flow. Used when the title is opened some other way.</summary>
        public void Abort()
        {
            if (!_playing && _overlay <= 0f) return;
            _playing = false;
            _crossfading = false;
            _overlay = 0f;
            StopDirector();
            RestorePresentation();
            HideProps();
            RestoreTitleScales();
            ClearGlint();
        }

        private void Begin(Camera camera, Vector3 colonyFocus, CelestialBodyProfile body, TimelineAsset timeline)
        {
            if (_playing)
                Abort();

            _cam = camera != null ? camera : Camera.main;
            _focus = colonyFocus;
            _elapsed = 0f;
            _fadeClock = 0f;
            _stingPlayed = false;
            _titleBound = false;
            _skipped = false;
            _crossfading = false;
            _overlay = 0f;
            _playing = true;
            Playback = ChoosePlayback(timeline != null && !_reduceMotion);

            SnapshotPresentation();
            if (Application.isPlaying || _forcedSun != null || _forcedFill != null)
                ApplyDusk(body);
            EnsureEarth();
            EnsureTitle();
            CacheTitleMotion();
            ApplyTitleMotion(0f);
            if (titleRoot != null)
                titleRoot.SetActive(false);
            ApplyPose(IntroShot.Sample(0f, _focus, _reduceMotion));

            if (Playback == IntroPlayback.Timeline)
                StartDirector(timeline);
        }

        private void Update()
        {
            if (_playing)
            {
                if (Application.isPlaying)
                    NotifyInput(IntroSkip.Sample());
                if (_playing)
                    Advance(Time.unscaledDeltaTime);
            }

            // Same frame the shot ends: step past the peak so OnGUI never paints a held black frame.
            if (!_playing)
                AdvanceCrossfade(Time.unscaledDeltaTime);

            if (_earth != null && _earth.gameObject.activeSelf && !_reduceMotion)
                _earth.Rotate(0f, 8f * Time.unscaledDeltaTime, 0f, Space.Self);
        }

        private void LateUpdate()
        {
            // The animator applies the reveal clip between Update and LateUpdate.
            // Re-apply so the code path wins, and a missing clip still reveals.
            if (_playing)
                ApplyTitleMotion(_elapsed);
        }

        private void OnGUI()
        {
            if (_overlay <= 0.004f) return;
            if (_fadeTex == null)
            {
                _fadeTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                _fadeTex.SetPixel(0, 0, Color.white);
                _fadeTex.Apply();
            }

            Color prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, _overlay);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _fadeTex);
            GUI.color = prev;
        }

        private void ApplyClock(float time)
        {
            if (Playback == IntroPlayback.Fallback)
                ApplyPose(IntroShot.Sample(time, _focus, _reduceMotion));
            else if (_cam != null)
                _cam.fieldOfView = IntroShot.Sample(time, _focus, false).FieldOfView;

            bool showTitle = Playback == IntroPlayback.Fallback || !_titleBound;
            if (showTitle && titleRoot != null)
                titleRoot.SetActive(IntroShot.TitleVisible(time));

            if (!_stingPlayed && time >= IntroShot.TitleOn && Playback == IntroPlayback.Fallback)
            {
                _stingPlayed = true;
                PlaySting();
            }

            if (!_reduceMotion)
            {
                _fadeClock = Mathf.Max(0f, time - IntroShot.FadeStart);
                _overlay = IntroShot.CrossfadeAlpha(_fadeClock);
            }

            ApplyTitleMotion(time);

            if (_earth != null)
                _earth.gameObject.SetActive(spawnEarthGlobe);
        }

        private void Finish(bool skipped)
        {
            if (!_playing) return;
            _playing = false;
            _skipped = skipped;
            StopDirector();
            RestorePresentation();
            HideProps();
            RestoreTitleScales();
            ClearGlint();
            if (skipped || _reduceMotion)
            {
                _overlay = 0f;
                _crossfading = false;
            }
            else
            {
                _crossfading = true;
                _fadeClock = Mathf.Max(0f, _elapsed - IntroShot.FadeStart);
                _overlay = IntroShot.CrossfadeAlpha(_fadeClock);
            }
            Finished?.Invoke();
        }

        private void StartDirector(TimelineAsset timeline)
        {
            if (director == null)
                director = GetComponent<PlayableDirector>();
            if (director == null)
                director = gameObject.AddComponent<PlayableDirector>();

            director.playableAsset = timeline;
            director.enabled = true;
            director.playOnAwake = false;
            director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
            director.extrapolationMode = DirectorWrapMode.Hold;
            Bind(timeline);
            director.time = 0d;
            director.Play();
        }

        private void Bind(TimelineAsset timeline)
        {
            var animator = EnsureAnimator(_cam);
            bool authoredCamera = false;
            foreach (var output in timeline.outputs)
            {
                if (output.sourceObject is AnimationTrack candidate && IntroAssets.IsAuthoredCameraTrack(candidate.name))
                    authoredCamera = true;
            }
            foreach (var output in timeline.outputs)
            {
                var src = output.sourceObject;
                if (src is AnimationTrack animTrack)
                {
                    if (animTrack.name == IntroAssets.RevealTrack)
                    {
                        var title = EnsureTitle();
                        var titleAnimator = EnsureAnimator(title);
                        if (titleAnimator != null)
                        {
                            titleAnimator.enabled = true;
                            director.SetGenericBinding(src, titleAnimator);
                        }
                    }
                    else if (IntroAssets.IsCameraTrackName(animTrack.name)
                             && !(authoredCamera && animTrack.name == IntroAssets.CameraTrack)
                             && animator != null)
                    {
                        animator.enabled = true;
                        director.SetGenericBinding(src, animator);
                    }
                }
                else if (src is ActivationTrack)
                {
                    var title = EnsureTitle();
                    if (title != null)
                    {
                        director.SetGenericBinding(src, title);
                        _titleBound = true;
                    }
                }
                else if (src is AudioTrack)
                {
                    var source = EnsureSource();
                    if (source != null)
                        director.SetGenericBinding(src, source);
                }
            }
        }

        private void StopDirector()
        {
            if (director == null) return;
            if (director.state == PlayState.Playing)
                director.Stop();
            director.enabled = false;
            DisableLooseAnimator(_cam != null ? _cam.gameObject : null);
            DisableLooseAnimator(titleRoot);
        }

        private static void DisableLooseAnimator(GameObject go)
        {
            if (go == null) return;
            var animator = go.GetComponent<Animator>();
            if (animator != null && animator.runtimeAnimatorController == null)
                animator.enabled = false;
        }

        private static Animator EnsureAnimator(GameObject go)
        {
            if (go == null) return null;
            var animator = go.GetComponent<Animator>();
            if (animator == null)
                animator = go.AddComponent<Animator>();
            animator.runtimeAnimatorController = null;
            return animator;
        }

        private static Animator EnsureAnimator(Camera camera) =>
            camera == null ? null : EnsureAnimator(camera.gameObject);

        private void ApplyPose(IntroShot.Pose pose)
        {
            if (_cam == null) return;
            _cam.orthographic = false;
            _cam.fieldOfView = pose.FieldOfView;
            _cam.nearClipPlane = 0.3f;
            _cam.farClipPlane = Mathf.Max(_cam.farClipPlane, 2000f);
            _cam.transform.SetPositionAndRotation(pose.Position, pose.Rotation);
        }

        private GameObject EnsureTitle()
        {
            bool created = false;
            if (titleRoot == null)
            {
                var child = transform.Find("IntroTitle");
                if (child != null) titleRoot = child.gameObject;
            }

            // Scene wiring (placeholder or the authored prefab instance) wins.
            // The player build does not load SM_Title_SolarMajesty; a missing slot gets the placeholder.
            if (titleRoot == null)
            {
                titleRoot = CreatePlaceholderTitle(transform);
                var pose = IntroShot.TitlePose(_focus);
                titleRoot.transform.SetPositionAndRotation(pose.Position, pose.Rotation);
                titleRoot.transform.localScale = Vector3.one;
                created = true;
            }

            if (created)
                titleRoot.SetActive(false);
            DressPlaceholder(titleRoot);
            DisableTitleShadows(titleRoot);
            return titleRoot;
        }

        /// <summary>Instance override only. The artist's prefab asset keeps its own shadow flags.</summary>
        public static void DisableTitleShadows(GameObject title)
        {
            if (title == null) return;
            var renderers = title.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null) continue;
                if (renderer.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off)
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (renderer.receiveShadows)
                    renderer.receiveShadows = false;
            }
        }

        private void CacheTitleMotion()
        {
            RestoreTitleScales();
            ClearGlint();
            _parts = System.Array.Empty<TitlePart>();
            _gold = System.Array.Empty<Renderer>();
            _goldSt = System.Array.Empty<Vector4>();
            if (titleRoot == null) return;

            var solar = FindDeep(titleRoot.transform, IntroAssets.WordSolar);
            var emblem = FindDeep(titleRoot.transform, IntroAssets.EmblemPlanet);
            var majesty = FindDeep(titleRoot.transform, IntroAssets.WordMajesty);
            var trim = FindDeep(titleRoot.transform, IntroAssets.TrimRoot);

            var parts = new System.Collections.Generic.List<TitlePart>(16);
            if (solar != null)
            {
                int letters = 0;
                for (int i = 0; i < solar.childCount; i++)
                {
                    var child = solar.GetChild(i);
                    if (child == null || child == emblem) continue;
                    parts.Add(Part(child, IntroTitleMotion.StepSolar));
                    letters++;
                }
                if (letters == 0)
                    parts.Add(Part(solar, IntroTitleMotion.StepSolar));
            }
            if (emblem != null)
                parts.Add(Part(emblem, IntroTitleMotion.StepEmblem));
            if (majesty != null)
                parts.Add(Part(majesty, IntroTitleMotion.StepMajesty));
            if (trim != null)
                parts.Add(Part(trim, IntroTitleMotion.StepMajesty));
            _parts = parts.ToArray();

            var renderers = titleRoot.GetComponentsInChildren<Renderer>(true);
            var gold = new System.Collections.Generic.List<Renderer>(renderers.Length);
            var sts = new System.Collections.Generic.List<Vector4>(renderers.Length);
            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                var mat = renderer != null ? renderer.sharedMaterial : null;
                if (mat == null || mat.name == null) continue;
                if (!mat.name.StartsWith(IntroAssets.GoldMaterialName)) continue;
                if (!mat.HasProperty(BaseMapStId)) continue;
                gold.Add(renderer);
                sts.Add(mat.GetVector(BaseMapStId));
            }
            _gold = gold.ToArray();
            _goldSt = sts.ToArray();
        }

        private static TitlePart Part(Transform transform, int step) =>
            new TitlePart { Transform = transform, BaseScale = transform.localScale, Step = step };

        private void ApplyTitleMotion(float time)
        {
            for (int i = 0; i < _parts.Length; i++)
            {
                var part = _parts[i];
                if (part.Transform == null) continue;
                float weight = IntroTitleMotion.RevealWeight(time, part.Step);
                part.Transform.localScale = part.BaseScale * weight;
            }

            float glintStart = IntroShot.TitleOn + IntroTitleMotion.GlintLead;
            if (time < glintStart || _gold.Length == 0) return;
            if (_glintBlock == null)
                _glintBlock = new MaterialPropertyBlock();
            for (int i = 0; i < _gold.Length; i++)
            {
                if (_gold[i] == null) continue;
                _gold[i].GetPropertyBlock(_glintBlock);
                _glintBlock.SetVector(BaseMapStId, IntroTitleMotion.WithGlint(_goldSt[i], time));
                _gold[i].SetPropertyBlock(_glintBlock);
            }
        }

        private void RestoreTitleScales()
        {
            for (int i = 0; i < _parts.Length; i++)
            {
                var part = _parts[i];
                if (part.Transform != null)
                    part.Transform.localScale = part.BaseScale;
            }
        }

        private void ClearGlint()
        {
            for (int i = 0; i < _gold.Length; i++)
            {
                if (_gold[i] != null)
                    _gold[i].SetPropertyBlock(null);
            }
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name)) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        private static void DressPlaceholder(GameObject title)
        {
            if (title == null) return;
            var glyphs = title.transform.Find(IntroAssets.PlaceholderGlyphs);
            if (glyphs == null) return;
            var text = glyphs.GetComponent<TextMesh>();
            if (text != null && text.font == null)
                text.font = BuiltinFont();
            ApplyFontMaterial(glyphs.GetComponent<MeshRenderer>(), text != null ? text.font : null);
        }

        /// <summary>
        /// Stand-in title. Root pivot is the centre. Glyphs are spun so their readable side
        /// is the root's -Z, matching <c>SM_Title_SolarMajesty</c>.
        /// </summary>
        public static GameObject CreatePlaceholderTitle(Transform parent)
        {
            var root = new GameObject("IntroTitle");
            if (parent != null)
                root.transform.SetParent(parent, false);

            var glyphs = new GameObject(IntroAssets.PlaceholderGlyphs);
            glyphs.transform.SetParent(root.transform, false);
            glyphs.transform.localPosition = Vector3.zero;
            glyphs.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            glyphs.transform.localScale = Vector3.one;

            var text = glyphs.AddComponent<TextMesh>();
            text.text = "Solar Majesty";
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 72;
            text.characterSize = 0.16f;
            text.fontStyle = FontStyle.Bold;
            text.color = new Color(0.96f, 0.88f, 0.62f, 1f);
            text.font = BuiltinFont();
            ApplyFontMaterial(glyphs.GetComponent<MeshRenderer>(), text.font);
            return root;
        }

        private static Font BuiltinFont()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return font;
        }

        private static void ApplyFontMaterial(MeshRenderer renderer, Font font)
        {
            if (renderer == null || font == null) return;
            if (renderer.sharedMaterial != null && renderer.sharedMaterial.name == "SM_IntroTitle")
                return;
            var shader = Shader.Find("Sprites/Default")
                         ?? Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("UI/Default");
            if (shader == null) return;
            var mat = new Material(shader) { name = "SM_IntroTitle" };
            var tex = font.material != null ? font.material.mainTexture : null;
            if (tex != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color")) mat.color = Color.white;
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void EnsureEarth()
        {
            if (!spawnEarthGlobe)
            {
                if (_earth != null) _earth.gameObject.SetActive(false);
                return;
            }

            if (_earth == null)
            {
                var child = transform.Find("IntroEarth");
                if (child != null) _earth = child;
            }

            if (_earth == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "IntroEarth";
                go.transform.SetParent(transform, false);
                var col = go.GetComponent<Collider>();
                if (col != null)
                {
                    if (Application.isPlaying) Destroy(col);
                    else DestroyImmediate(col);
                }
                _earth = go.transform;
            }

            _earth.position = IntroShot.EarthPosition(_focus);
            _earth.localScale = Vector3.one * IntroShot.EarthScale;
            _earth.gameObject.SetActive(true);

            var rend = _earth.GetComponent<MeshRenderer>();
            if (rend == null) return;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
            if (rend.sharedMaterial != null && rend.sharedMaterial.name == "SM_IntroEarth")
                return;

            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Sprites/Default")
                         ?? Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return;
            var mat = new Material(shader) { name = "SM_IntroEarth" };
            var tex = Resources.Load<Texture2D>("World/Orrery/Earth");
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color")) mat.color = Color.white;
            if (tex != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            }
            rend.sharedMaterial = mat;
            _earthMat = mat;
        }

        private AudioSource EnsureSource()
        {
            if (stingSource == null)
                stingSource = GetComponent<AudioSource>();
            if (stingSource == null)
                stingSource = gameObject.AddComponent<AudioSource>();
            stingSource.playOnAwake = false;
            stingSource.loop = false;
            stingSource.spatialBlend = 0f;
            stingSource.volume = Mathf.Clamp01(DemoSettings.Master * DemoSettings.Music);
            return stingSource;
        }

        private void PlaySting()
        {
            var clip = LoadSting();
            if (clip == null) return;
            var source = EnsureSource();
            if (!Application.isPlaying) return;
            source.PlayOneShot(clip, source.volume);
        }

        private void ApplyDusk(CelestialBodyProfile body)
        {
            if (body == null) body = CelestialBodyCatalog.Earth();
            _sun = _forcedSun != null ? _forcedSun : FindSun();
            if (_sun != null && _haveSun)
            {
                var tinted = IntroDusk.TintSun(new IntroLightSample
                {
                    Rotation = _sunSnap.Euler,
                    Color = _sunSnap.Color,
                    Intensity = _sunSnap.Intensity
                }, body);
                _sun.transform.rotation = tinted.Rotation;
                _sun.color = tinted.Color;
                _sun.intensity = tinted.Intensity;
            }

            _fill = _forcedFill != null ? _forcedFill : FindFill();
            if (_fill != null && _haveFill)
            {
                var tinted = IntroDusk.TintFill(new IntroLightSample
                {
                    Rotation = _fillSnap.Euler,
                    Color = _fillSnap.Color,
                    Intensity = _fillSnap.Intensity
                });
                _fill.color = tinted.Color;
                _fill.intensity = tinted.Intensity;
            }

            if (_haveAmbient)
            {
                var tinted = IntroDusk.TintAmbient(_ambient);
                RenderSettings.ambientSkyColor = tinted.Sky;
                RenderSettings.ambientEquatorColor = tinted.Equator;
                RenderSettings.ambientGroundColor = tinted.Ground;
                RenderSettings.ambientLight = tinted.Flat;
                RenderSettings.ambientIntensity = tinted.Intensity;
            }

            _tintedDusk = true;
        }

        private Light FindSun()
        {
            var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                var light = lights[i];
                if (light == null || light.type != LightType.Directional) continue;
                if (light.name == "Fill Light") continue;
                return light;
            }
            return null;
        }

        private Light FindFill()
        {
            var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                var light = lights[i];
                if (light != null && light.name == "Fill Light")
                    return light;
            }
            return null;
        }

        private void SnapshotPresentation()
        {
            if (_cam != null)
            {
                _camera = new CameraSnapshot
                {
                    Valid = true,
                    Ortho = _cam.orthographic,
                    OrthoSize = _cam.orthographicSize,
                    Fov = _cam.fieldOfView,
                    Near = _cam.nearClipPlane,
                    Far = _cam.farClipPlane,
                    Clear = _cam.clearFlags,
                    Background = _cam.backgroundColor,
                    Position = _cam.transform.position,
                    Rotation = _cam.transform.rotation
                };
            }

            var sun = _forcedSun != null ? _forcedSun : (Application.isPlaying ? FindSun() : null);
            _haveSun = sun != null;
            if (_haveSun)
            {
                _sun = sun;
                _sunSnap = new LightSnapshot
                {
                    Euler = sun.transform.rotation,
                    Color = sun.color,
                    Intensity = sun.intensity
                };
            }

            var fill = _forcedFill != null ? _forcedFill : (Application.isPlaying ? FindFill() : null);
            _haveFill = fill != null;
            if (_haveFill)
            {
                _fill = fill;
                _fillSnap = new LightSnapshot
                {
                    Euler = fill.transform.rotation,
                    Color = fill.color,
                    Intensity = fill.intensity
                };
            }

            bool tintAmbient = Application.isPlaying || _forcedSun != null || _forcedFill != null;
            _haveAmbient = tintAmbient;
            if (_haveAmbient)
            {
                _ambient = new IntroAmbientSample
                {
                    Mode = RenderSettings.ambientMode,
                    Sky = RenderSettings.ambientSkyColor,
                    Equator = RenderSettings.ambientEquatorColor,
                    Ground = RenderSettings.ambientGroundColor,
                    Flat = RenderSettings.ambientLight,
                    Intensity = RenderSettings.ambientIntensity
                };
            }

            _fog = new FogSnapshot
            {
                Enabled = RenderSettings.fog,
                Color = RenderSettings.fogColor,
                Start = RenderSettings.fogStartDistance,
                End = RenderSettings.fogEndDistance
            };
            // The stand-in Earth sits just past the play-mode fog start. Push the haze
            // out for the sweep, then put the body's fog back before the title steals it.
            // Edit-mode tests must not rewrite the open scene's fog.
            _pushedFog = Application.isPlaying;
            if (_pushedFog)
            {
                RenderSettings.fogStartDistance = Mathf.Max(_fog.Start, 70f);
                RenderSettings.fogEndDistance = Mathf.Max(_fog.End, 360f);
            }
        }

        private void RestorePresentation()
        {
            if (_cam != null && _camera.Valid)
            {
                _cam.orthographic = _camera.Ortho;
                _cam.orthographicSize = _camera.OrthoSize;
                _cam.fieldOfView = _camera.Fov;
                _cam.nearClipPlane = _camera.Near;
                _cam.farClipPlane = _camera.Far;
                _cam.clearFlags = _camera.Clear;
                _cam.backgroundColor = _camera.Background;
                _cam.transform.SetPositionAndRotation(_camera.Position, _camera.Rotation);
            }

            if (_tintedDusk)
            {
                if (_haveSun && _sun != null)
                {
                    _sun.transform.rotation = _sunSnap.Euler;
                    _sun.color = _sunSnap.Color;
                    _sun.intensity = _sunSnap.Intensity;
                }

                if (_haveFill && _fill != null)
                {
                    _fill.transform.rotation = _fillSnap.Euler;
                    _fill.color = _fillSnap.Color;
                    _fill.intensity = _fillSnap.Intensity;
                }

                if (_haveAmbient)
                {
                    RenderSettings.ambientMode = _ambient.Mode;
                    RenderSettings.ambientSkyColor = _ambient.Sky;
                    RenderSettings.ambientEquatorColor = _ambient.Equator;
                    RenderSettings.ambientGroundColor = _ambient.Ground;
                    RenderSettings.ambientLight = _ambient.Flat;
                    RenderSettings.ambientIntensity = _ambient.Intensity;
                }

                _tintedDusk = false;
            }

            if (_pushedFog)
            {
                RenderSettings.fog = _fog.Enabled;
                RenderSettings.fogColor = _fog.Color;
                RenderSettings.fogStartDistance = _fog.Start;
                RenderSettings.fogEndDistance = _fog.End;
                _pushedFog = false;
            }
        }

        private void HideProps()
        {
            if (_earth != null)
                _earth.gameObject.SetActive(false);
            else
            {
                var child = transform.Find("IntroEarth");
                if (child != null) child.gameObject.SetActive(false);
            }

            if (titleRoot != null)
                titleRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_tintedDusk || _pushedFog)
                RestorePresentation();
            DestroyOwned(_fadeTex);
            DestroyOwned(_earthMat);
            if (titleRoot == null) return;
            var glyphs = titleRoot.transform.Find(IntroAssets.PlaceholderGlyphs);
            var rend = glyphs != null ? glyphs.GetComponent<MeshRenderer>() : null;
            if (rend != null && rend.sharedMaterial != null && rend.sharedMaterial.name == "SM_IntroTitle")
                DestroyOwned(rend.sharedMaterial);
        }

        private void DestroyOwned(UnityEngine.Object owned)
        {
            if (owned == null) return;
            if (Application.isPlaying) Destroy(owned);
            else DestroyImmediate(owned);
        }

        private struct CameraSnapshot
        {
            public bool Valid;
            public bool Ortho;
            public float OrthoSize;
            public float Fov;
            public float Near;
            public float Far;
            public CameraClearFlags Clear;
            public Color Background;
            public Vector3 Position;
            public Quaternion Rotation;
        }

        private struct TitlePart
        {
            public Transform Transform;
            public Vector3 BaseScale;
            public int Step;
        }

        private struct LightSnapshot
        {
            public Quaternion Euler;
            public Color Color;
            public float Intensity;
        }

        private struct FogSnapshot
        {
            public bool Enabled;
            public Color Color;
            public float Start;
            public float End;
        }
    }
}
