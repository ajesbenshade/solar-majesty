using System;
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

        public event Action Finished;

        private bool _playing;
        private bool _reduceMotion;
        private bool _titleBound;
        private bool _stingPlayed;
        private bool _pushedFog;
        private float _elapsed;
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
        private FogSnapshot _fog;
        private Texture2D _fadeTex;

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
            _overlay = 0f;
            StopDirector();
            RestorePresentation();
            HideProps();
        }

        private void Begin(Camera camera, Vector3 colonyFocus, CelestialBodyProfile body, TimelineAsset timeline)
        {
            if (_playing)
                Abort();

            _cam = camera != null ? camera : Camera.main;
            _focus = colonyFocus;
            _elapsed = 0f;
            _stingPlayed = false;
            _titleBound = false;
            _overlay = 0f;
            _playing = true;
            Playback = ChoosePlayback(timeline != null && !_reduceMotion);

            SnapshotPresentation();
            if (Application.isPlaying)
                ApplyDusk(body);
            EnsureEarth();
            EnsureTitle();
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
                if (!_playing) return;
                Advance(Time.unscaledDeltaTime);
            }
            else if (_overlay > 0f)
            {
                _overlay = Mathf.MoveTowards(_overlay, 0f, Time.unscaledDeltaTime / IntroShot.FadeOutSeconds);
            }

            if (_earth != null && _earth.gameObject.activeSelf && !_reduceMotion)
                _earth.Rotate(0f, 8f * Time.unscaledDeltaTime, 0f, Space.Self);
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
                _overlay = Mathf.Max(_overlay, IntroShot.FadeAlpha(time));

            if (_earth != null)
                _earth.gameObject.SetActive(spawnEarthGlobe);
        }

        private void Finish(bool skipped)
        {
            if (!_playing) return;
            _playing = false;
            StopDirector();
            RestorePresentation();
            HideProps();
            if (!skipped)
                _overlay = 1f;
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
            foreach (var output in timeline.outputs)
            {
                var src = output.sourceObject;
                if (src is AnimationTrack)
                {
                    if (animator != null)
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
            if (_cam != null)
            {
                var animator = _cam.GetComponent<Animator>();
                if (animator != null && animator.runtimeAnimatorController == null)
                    animator.enabled = false;
            }
        }

        private static Animator EnsureAnimator(Camera camera)
        {
            if (camera == null) return null;
            var animator = camera.GetComponent<Animator>();
            if (animator == null)
                animator = camera.gameObject.AddComponent<Animator>();
            return animator;
        }

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
            return titleRoot;
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
            var state = SunPath.Evaluate(body, SunPath.DuskElapsed(body));
            _sun = FindSun();
            if (_sun != null)
            {
                _sun.transform.rotation = Quaternion.Euler(state.Euler);
                _sun.color = state.Color;
                _sun.intensity = body.SunIntensity * state.Intensity;
            }

            _fill = FindFill();
            if (_fill != null)
                _fill.intensity = _fillSnap.Intensity * 0.55f;
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

            var sun = FindSun();
            _haveSun = sun != null;
            if (_haveSun)
            {
                _sunSnap = new LightSnapshot
                {
                    Euler = sun.transform.rotation,
                    Color = sun.color,
                    Intensity = sun.intensity
                };
            }

            var fill = FindFill();
            _haveFill = fill != null;
            if (_haveFill)
            {
                _fillSnap = new LightSnapshot
                {
                    Euler = fill.transform.rotation,
                    Color = fill.color,
                    Intensity = fill.intensity
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

            if (_pushedFog)
            {
                if (_haveSun && _sun != null)
                {
                    _sun.transform.rotation = _sunSnap.Euler;
                    _sun.color = _sunSnap.Color;
                    _sun.intensity = _sunSnap.Intensity;
                }

                if (_haveFill && _fill != null)
                    _fill.intensity = _fillSnap.Intensity;

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
