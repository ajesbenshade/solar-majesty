#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace SolarMajesty.EditorTools
{
    /// <summary>
    /// Writes <c>Resources/Intro/IntroTimeline</c> and wires a PlayableDirector into the
    /// sandbox scene. The colony is built at runtime inside that one scene, so the intro
    /// is an overlay there rather than a scene at build index 0.
    /// Re-running keeps the same timeline asset, track, and clip identities. A camera track
    /// whose name ends with <see cref="IntroAssets.AuthoredCameraSuffix"/> is left untouched.
    /// Batchmode: -executeMethod SolarMajesty.EditorTools.IntroBuilder.BuildAll
    /// </summary>
    public static class IntroBuilder
    {
        private const string ScenePath = "Assets/Scenes/LunarOutpost_Sandbox.unity";

        [MenuItem("Solar Majesty/Intro/Build Intro Timeline", priority = 20)]
        public static void BuildFromMenu()
        {
            BuildAll();
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog(
                    "Solar Majesty",
                    "Intro timeline saved:\n" + IntroAssets.TimelineAssetPath,
                    "OK");
            }
        }

        /// <summary>Unity -batchmode -executeMethod SolarMajesty.EditorTools.IntroBuilder.BuildAll</summary>
        public static void BuildAll()
        {
            EnsureFolder();
            var timeline = BuildTimeline();
            WireScene(timeline);
            AssetDatabase.SaveAssets();
            Debug.Log("[Intro] Built " + IntroAssets.TimelineAssetPath);
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Intro"))
                AssetDatabase.CreateFolder("Assets/Resources", "Intro");
        }

        private static TimelineAsset BuildTimeline()
        {
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(IntroAssets.TimelineAssetPath);
            if (timeline == null)
            {
                timeline = ScriptableObject.CreateInstance<TimelineAsset>();
                timeline.name = "IntroTimeline";
                AssetDatabase.CreateAsset(timeline, IntroAssets.TimelineAssetPath);
            }

            if (System.Math.Abs(timeline.editorSettings.frameRate - 30d) > 0.01d)
                timeline.editorSettings.frameRate = 30d;

            var camera = FindCameraTrack(timeline);
            if (IntroAssets.ShouldRebuildCameraTrack(camera != null, camera != null ? camera.name : null))
            {
                if (camera == null)
                    camera = timeline.CreateTrack<AnimationTrack>(null, IntroAssets.CameraTrack);
                RebuildCameraTrack(timeline, camera);
            }
            else
            {
                Debug.Log("[Intro] Camera track '" + camera.name + "' is authored. Left untouched.");
            }

            var title = FindTrack<ActivationTrack>(timeline, IntroAssets.TitleTrack);
            if (title == null)
                title = timeline.CreateTrack<ActivationTrack>(null, IntroAssets.TitleTrack);
            var titleClip = FirstClip(title) ?? title.CreateDefaultClip();
            PlaceClip(titleClip, IntroShot.TitleOn, IntroShot.TitleOff - IntroShot.TitleOn, "Solar Majesty");

            var audio = FindTrack<AudioTrack>(timeline, IntroAssets.StingTrack);
            if (audio == null)
                audio = timeline.CreateTrack<AudioTrack>(null, IntroAssets.StingTrack);
            var sting = AssetDatabase.LoadAssetAtPath<AudioClip>(IntroAssets.StingAssetPath);
            var stingClip = FirstClip(audio);
            if (sting != null)
            {
                if (stingClip == null)
                    stingClip = audio.CreateClip(sting);
                PlaceClip(stingClip, IntroShot.TitleOn, stingClip.duration, "Intro Sting");
            }
            else if (stingClip == null)
            {
                Debug.Log("[Intro] No clip at " + IntroAssets.StingAssetPath + ". Audio track left empty.");
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(IntroAssets.TitlePrefabPath);
            var targets = RevealTargets(prefab);
            if (targets.Count > 0)
            {
                var reveal = FindTrack<AnimationTrack>(timeline, IntroAssets.RevealTrack);
                if (reveal == null)
                    reveal = timeline.CreateTrack<AnimationTrack>(null, IntroAssets.RevealTrack);
                RebuildRevealTrack(timeline, reveal, targets);
            }

            return timeline;
        }

        private static void RebuildCameraTrack(TimelineAsset timeline, AnimationTrack track)
        {
            if (track.trackOffset != TrackOffset.ApplyTransformOffsets)
                track.trackOffset = TrackOffset.ApplyTransformOffsets;
            if (track.position != Vector3.zero)
                track.position = Vector3.zero;
            if (track.eulerAngles != Vector3.zero)
                track.eulerAngles = Vector3.zero;
            if (track.matchTargetFields != 0)
                track.matchTargetFields = 0;

            var clip = FirstClip(track);
            AnimationPlayableAsset asset = clip != null ? clip.asset as AnimationPlayableAsset : null;
            AnimationClip anim = asset != null ? asset.clip : null;
            if (clip == null || asset == null)
            {
                if (anim == null)
                {
                    anim = new AnimationClip { name = "IntroCameraSweep", frameRate = 30f, legacy = false };
                    AssetDatabase.AddObjectToAsset(anim, timeline);
                }
                clip = track.CreateClip(anim);
                asset = clip.asset as AnimationPlayableAsset;
            }
            else if (anim == null)
            {
                anim = new AnimationClip { name = "IntroCameraSweep", frameRate = 30f, legacy = false };
                AssetDatabase.AddObjectToAsset(anim, timeline);
                asset.clip = anim;
            }

            PlaceClip(clip, 0d, IntroShot.Duration, "Sweep past Earth");
            if (asset != null)
                ForceAbsoluteClip(asset);
            if (anim != null && !CameraClipMatches(anim))
                BakeCameraClip(anim);
        }

        /// <summary>
        /// Baked curves are absolute local poses. removeStartOffset would replay them from the origin.
        /// Track and clip offsets stay at identity, in ApplyTransformOffsets mode.
        /// </summary>
        private static void ForceAbsoluteClip(AnimationPlayableAsset asset)
        {
            if (asset.removeStartOffset)
                asset.removeStartOffset = false;
            if (asset.useTrackMatchFields)
                asset.useTrackMatchFields = false;
            if (asset.matchTargetFields != 0)
                asset.matchTargetFields = 0;
            if (asset.position != Vector3.zero)
                asset.position = Vector3.zero;
            if (asset.eulerAngles != Vector3.zero)
                asset.eulerAngles = Vector3.zero;
        }

        private static bool CameraClipMatches(AnimationClip clip)
        {
            var posX = AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.x"));
            var posY = AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.y"));
            var posZ = AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.z"));
            if (posX == null || posY == null || posZ == null) return false;
            Vector3 focus = ColonyLayout.CameraFocus;
            float[] times = { 0f, 1.25f, IntroShot.CameraSettle, IntroShot.Duration };
            for (int i = 0; i < times.Length; i++)
            {
                var pose = IntroShot.Sample(times[i], focus, false);
                if (Mathf.Abs(posX.Evaluate(times[i]) - pose.Position.x) > 0.02f) return false;
                if (Mathf.Abs(posY.Evaluate(times[i]) - pose.Position.y) > 0.02f) return false;
                if (Mathf.Abs(posZ.Evaluate(times[i]) - pose.Position.z) > 0.02f) return false;
            }
            return true;
        }

        private static void BakeCameraClip(AnimationClip clip)
        {
            clip.frameRate = 30f;
            clip.legacy = false;
            var posX = new AnimationCurve();
            var posY = new AnimationCurve();
            var posZ = new AnimationCurve();
            var rotX = new AnimationCurve();
            var rotY = new AnimationCurve();
            var rotZ = new AnimationCurve();
            var rotW = new AnimationCurve();

            Vector3 focus = ColonyLayout.CameraFocus;
            const float step = 1f / 30f;
            for (float t = 0f; t <= IntroShot.Duration + 0.0001f; t += step)
            {
                float time = Mathf.Min(t, IntroShot.Duration);
                var pose = IntroShot.Sample(time, focus, false);
                AddLinearKey(posX, time, pose.Position.x);
                AddLinearKey(posY, time, pose.Position.y);
                AddLinearKey(posZ, time, pose.Position.z);
                Quaternion q = pose.Rotation;
                AddLinearKey(rotX, time, q.x);
                AddLinearKey(rotY, time, q.y);
                AddLinearKey(rotZ, time, q.z);
                AddLinearKey(rotW, time, q.w);
            }

            SetCurve(clip, "m_LocalPosition.x", posX);
            SetCurve(clip, "m_LocalPosition.y", posY);
            SetCurve(clip, "m_LocalPosition.z", posZ);
            SetCurve(clip, "m_LocalRotation.x", rotX);
            SetCurve(clip, "m_LocalRotation.y", rotY);
            SetCurve(clip, "m_LocalRotation.z", rotZ);
            SetCurve(clip, "m_LocalRotation.w", rotW);
            clip.EnsureQuaternionContinuity();

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }

        private static void RebuildRevealTrack(TimelineAsset timeline, AnimationTrack track, List<RevealTarget> targets)
        {
            if (track.trackOffset != TrackOffset.ApplyTransformOffsets)
                track.trackOffset = TrackOffset.ApplyTransformOffsets;
            if (track.position != Vector3.zero)
                track.position = Vector3.zero;
            if (track.eulerAngles != Vector3.zero)
                track.eulerAngles = Vector3.zero;

            var clip = FirstClip(track);
            AnimationPlayableAsset asset = clip != null ? clip.asset as AnimationPlayableAsset : null;
            AnimationClip anim = asset != null ? asset.clip : null;
            if (clip == null || asset == null)
            {
                if (anim == null)
                {
                    anim = new AnimationClip { name = "IntroTitleReveal", frameRate = 30f, legacy = false };
                    AssetDatabase.AddObjectToAsset(anim, timeline);
                }
                clip = track.CreateClip(anim);
                asset = clip.asset as AnimationPlayableAsset;
            }
            else if (anim == null)
            {
                anim = new AnimationClip { name = "IntroTitleReveal", frameRate = 30f, legacy = false };
                AssetDatabase.AddObjectToAsset(anim, timeline);
                asset.clip = anim;
            }

            PlaceClip(clip, 0d, IntroShot.Duration, "SOLAR, O, MAJESTY");
            if (asset != null)
                ForceAbsoluteClip(asset);
            if (anim != null && !RevealClipMatches(anim, targets))
                BakeRevealClip(anim, targets);
        }

        private static bool RevealClipMatches(AnimationClip clip, List<RevealTarget> targets)
        {
            if (targets.Count == 0) return true;
            var probe = targets[0];
            var curve = AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve(probe.Path, typeof(Transform), "m_LocalScale.x"));
            if (curve == null) return false;
            float mid = IntroShot.TitleOn + probe.Step * IntroTitleMotion.RevealStep + IntroTitleMotion.RevealDuration * 0.5f;
            float done = IntroShot.TitleOn + probe.Step * IntroTitleMotion.RevealStep + IntroTitleMotion.RevealDuration;
            float expectMid = probe.BaseScale.x * IntroTitleMotion.RevealWeight(mid, probe.Step);
            float expectDone = probe.BaseScale.x * IntroTitleMotion.RevealWeight(done, probe.Step);
            if (Mathf.Abs(curve.Evaluate(0f)) > 0.02f) return false;
            if (Mathf.Abs(curve.Evaluate(mid) - expectMid) > 0.05f) return false;
            if (Mathf.Abs(curve.Evaluate(done) - expectDone) > 0.05f) return false;
            return true;
        }

        private static void BakeRevealClip(AnimationClip clip, List<RevealTarget> targets)
        {
            clip.frameRate = 30f;
            clip.legacy = false;
            const float step = 1f / 30f;
            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                var x = new AnimationCurve();
                var y = new AnimationCurve();
                var z = new AnimationCurve();
                for (float t = 0f; t <= IntroShot.Duration + 0.0001f; t += step)
                {
                    float time = Mathf.Min(t, IntroShot.Duration);
                    float weight = IntroTitleMotion.RevealWeight(time, target.Step);
                    AddLinearKey(x, time, target.BaseScale.x * weight);
                    AddLinearKey(y, time, target.BaseScale.y * weight);
                    AddLinearKey(z, time, target.BaseScale.z * weight);
                }
                SetCurve(clip, target.Path, "m_LocalScale.x", x);
                SetCurve(clip, target.Path, "m_LocalScale.y", y);
                SetCurve(clip, target.Path, "m_LocalScale.z", z);
            }

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }

        private struct RevealTarget
        {
            public string Path;
            public int Step;
            public Vector3 BaseScale;
        }

        private static List<RevealTarget> RevealTargets(GameObject prefab)
        {
            var list = new List<RevealTarget>();
            if (prefab == null) return list;
            var root = prefab.transform;
            var solar = FindDeep(root, IntroAssets.WordSolar);
            var emblem = FindDeep(root, IntroAssets.EmblemPlanet);
            var majesty = FindDeep(root, IntroAssets.WordMajesty);
            var trim = FindDeep(root, IntroAssets.TrimRoot);
            if (solar != null)
            {
                int letters = 0;
                for (int i = 0; i < solar.childCount; i++)
                {
                    var child = solar.GetChild(i);
                    if (child == null || child == emblem) continue;
                    list.Add(Target(root, child, IntroTitleMotion.StepSolar));
                    letters++;
                }
                if (letters == 0)
                    list.Add(Target(root, solar, IntroTitleMotion.StepSolar));
            }
            if (emblem != null)
                list.Add(Target(root, emblem, IntroTitleMotion.StepEmblem));
            if (majesty != null)
                list.Add(Target(root, majesty, IntroTitleMotion.StepMajesty));
            if (trim != null)
                list.Add(Target(root, trim, IntroTitleMotion.StepMajesty));
            return list;
        }

        private static RevealTarget Target(Transform root, Transform node, int step) =>
            new RevealTarget { Path = PathOf(root, node), Step = step, BaseScale = node.localScale };

        private static string PathOf(Transform root, Transform node)
        {
            if (node == null || node == root) return "";
            string parent = PathOf(root, node.parent);
            return string.IsNullOrEmpty(parent) ? node.name : parent + "/" + node.name;
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

        private static void AddLinearKey(AnimationCurve curve, float time, float value)
        {
            int index = curve.AddKey(new Keyframe(time, value));
            AnimationUtility.SetKeyLeftTangentMode(curve, index, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(curve, index, AnimationUtility.TangentMode.Linear);
        }

        private static void SetCurve(AnimationClip clip, string property, AnimationCurve curve) =>
            SetCurve(clip, "", property, curve);

        private static void SetCurve(AnimationClip clip, string path, string property, AnimationCurve curve)
        {
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), property), curve);
        }

        private static void PlaceClip(TimelineClip clip, double start, double duration, string displayName)
        {
            if (clip == null) return;
            if (System.Math.Abs(clip.start - start) > 0.0001d)
                clip.start = start;
            if (duration > 0.0001d && System.Math.Abs(clip.duration - duration) > 0.0001d)
                clip.duration = duration;
            if (clip.displayName != displayName)
                clip.displayName = displayName;
        }

        private static T FindTrack<T>(TimelineAsset timeline, string name) where T : TrackAsset
        {
            foreach (var track in timeline.GetOutputTracks())
            {
                if (track is T typed && track.name == name)
                    return typed;
            }
            return null;
        }

        private static AnimationTrack FindCameraTrack(TimelineAsset timeline)
        {
            AnimationTrack generated = null;
            AnimationTrack authoredTrack = null;
            foreach (var track in timeline.GetOutputTracks())
            {
                if (!(track is AnimationTrack anim)) continue;
                if (IntroAssets.IsAuthoredCameraTrack(anim.name))
                    authoredTrack = anim;
                else if (anim.name == IntroAssets.CameraTrack)
                    generated = anim;
            }
            return authoredTrack != null ? authoredTrack : generated;
        }

        private static TimelineClip FirstClip(TrackAsset track)
        {
            if (track == null) return null;
            foreach (var clip in track.GetClips())
                return clip;
            return null;
        }

        private static void WireScene(TimelineAsset timeline)
        {
            if (!System.IO.File.Exists(ScenePath))
            {
                Debug.LogWarning("[Intro] Scene missing (" + ScenePath + "). Timeline asset was still written.");
                return;
            }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var camGo = GameObject.FindGameObjectWithTag("MainCamera");
            if (camGo == null)
            {
                Debug.LogWarning("[Intro] No Main Camera in " + ScenePath + ". Timeline asset was still written.");
                return;
            }

            var animator = camGo.GetComponent<Animator>();
            if (animator == null)
                animator = camGo.AddComponent<Animator>();
            if (animator.runtimeAnimatorController != null)
                animator.runtimeAnimatorController = null;
            if (animator.enabled)
                animator.enabled = false;

            var root = GameObject.Find("Intro");
            if (root == null)
                root = new GameObject("Intro");

            var director = root.GetComponent<PlayableDirector>();
            if (director == null)
                director = root.AddComponent<PlayableDirector>();
            if (director.playableAsset != timeline)
                director.playableAsset = timeline;
            if (director.playOnAwake)
                director.playOnAwake = false;
            if (director.timeUpdateMode != DirectorUpdateMode.UnscaledGameTime)
                director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
            if (director.extrapolationMode != DirectorWrapMode.Hold)
                director.extrapolationMode = DirectorWrapMode.Hold;

            var sequence = root.GetComponent<IntroSequence>();
            if (sequence == null)
                sequence = root.AddComponent<IntroSequence>();

            var source = root.GetComponent<AudioSource>();
            if (source == null)
                source = root.AddComponent<AudioSource>();
            if (source.playOnAwake)
                source.playOnAwake = false;
            if (source.loop)
                source.loop = false;
            if (source.spatialBlend > 0.001f)
                source.spatialBlend = 0f;

            var title = EnsureTitleSlot(root.transform);
            if (title.activeSelf)
                title.SetActive(false);
            IntroSequence.DisableTitleShadows(title);

            Animator titleAnimator = null;
            if (FindTrack<AnimationTrack>(timeline, IntroAssets.RevealTrack) != null)
            {
                titleAnimator = title.GetComponent<Animator>();
                if (titleAnimator == null)
                    titleAnimator = title.AddComponent<Animator>();
                if (titleAnimator.runtimeAnimatorController != null)
                    titleAnimator.runtimeAnimatorController = null;
                if (titleAnimator.enabled)
                    titleAnimator.enabled = false;
            }

            var earth = root.transform.Find("IntroEarth");
            if (earth == null)
            {
                var earthGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                earthGo.name = "IntroEarth";
                earthGo.transform.SetParent(root.transform, false);
                var col = earthGo.GetComponent<Collider>();
                if (col != null)
                    Object.DestroyImmediate(col);
                earth = earthGo.transform;
            }
            Vector3 earthPos = IntroShot.EarthPosition(ColonyLayout.CameraFocus);
            if ((earth.position - earthPos).sqrMagnitude > 0.0001f)
                earth.position = earthPos;
            Vector3 earthScale = Vector3.one * IntroShot.EarthScale;
            if ((earth.localScale - earthScale).sqrMagnitude > 0.0001f)
                earth.localScale = earthScale;
            if (earth.gameObject.activeSelf)
                earth.gameObject.SetActive(false);

            ClearStaleBindings(director, timeline);
            bool authoredCamera = false;
            foreach (var output in timeline.outputs)
            {
                if (output.sourceObject is AnimationTrack anim && IntroAssets.IsAuthoredCameraTrack(anim.name))
                    authoredCamera = true;
            }
            foreach (var output in timeline.outputs)
            {
                var track = output.sourceObject;
                UnityEngine.Object binding = null;
                if (track is AnimationTrack anim)
                {
                    if (anim.name == IntroAssets.RevealTrack)
                        binding = titleAnimator;
                    else if (IntroAssets.IsCameraTrackName(anim.name) && !(authoredCamera && anim.name == IntroAssets.CameraTrack))
                        binding = animator;
                }
                else if (track is ActivationTrack)
                    binding = title;
                else if (track is AudioTrack)
                    binding = source;

                if (binding != null && director.GetGenericBinding(track) != binding)
                    director.SetGenericBinding(track, binding);
            }

            var so = new SerializedObject(sequence);
            var directorProp = so.FindProperty("director");
            var titleProp = so.FindProperty("titleRoot");
            var stingProp = so.FindProperty("stingSource");
            var earthProp = so.FindProperty("spawnEarthGlobe");
            if (directorProp.objectReferenceValue != director)
                directorProp.objectReferenceValue = director;
            if (titleProp.objectReferenceValue != title)
                titleProp.objectReferenceValue = title;
            if (stingProp.objectReferenceValue != source)
                stingProp.objectReferenceValue = source;
            if (!earthProp.boolValue)
                earthProp.boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (scene.isDirty)
                EditorSceneManager.SaveScene(scene);
            Debug.Log("[Intro] Wired PlayableDirector into " + ScenePath + " (build index stays 0).");
        }

        /// <summary>Drops bindings whose track was deleted, so a rebuild cannot accumulate stale keys.</summary>
        private static void ClearStaleBindings(PlayableDirector director, TimelineAsset timeline)
        {
            var live = new HashSet<UnityEngine.Object>();
            foreach (var output in timeline.outputs)
            {
                if (output.sourceObject != null)
                    live.Add(output.sourceObject);
            }

            var so = new SerializedObject(director);
            var bindings = so.FindProperty("m_SceneBindings");
            if (bindings == null || !bindings.isArray) return;
            bool removed = false;
            for (int i = bindings.arraySize - 1; i >= 0; i--)
            {
                var key = bindings.GetArrayElementAtIndex(i).FindPropertyRelative("key").objectReferenceValue;
                if (key == null || !live.Contains(key))
                {
                    bindings.DeleteArrayElementAtIndex(i);
                    removed = true;
                }
            }
            if (removed)
                so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Title slot transform is the same either way: pivot at the centre, readable face on local -Z.
        /// Re-running after <c>SM_Title_SolarMajesty</c> lands swaps the placeholder for that prefab.
        /// </summary>
        private static GameObject EnsureTitleSlot(Transform parent)
        {
            var existing = parent.Find("IntroTitle");
            GameObject title = existing != null ? existing.gameObject : null;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(IntroAssets.TitlePrefabPath);
            bool usePrefab = IntroAssets.UseAuthoredTitle(prefab != null);
            bool currentIsPrefab = title != null
                && prefab != null
                && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(title) == IntroAssets.TitlePrefabPath;

            if (usePrefab && !currentIsPrefab)
            {
                if (title != null)
                    Object.DestroyImmediate(title);
                title = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                if (title == null)
                    title = IntroSequence.CreatePlaceholderTitle(parent);
                else
                {
                    title.name = "IntroTitle";
                    Debug.Log("[Intro] Title slot instanced " + IntroAssets.TitlePrefabPath);
                }
            }
            else if (!usePrefab && !IsPlaceholderTitle(title))
            {
                if (title != null)
                    Object.DestroyImmediate(title);
                title = IntroSequence.CreatePlaceholderTitle(parent);
                Debug.Log("[Intro] Title slot uses the -Z placeholder. Drop the model at " + IntroAssets.TitlePrefabPath + " and run this again.");
            }

            var pose = IntroShot.TitlePose(ColonyLayout.CameraFocus);
            if ((title.transform.position - pose.Position).sqrMagnitude > 0.0001f
                || Quaternion.Dot(title.transform.rotation, pose.Rotation) < 0.9999f)
                title.transform.SetPositionAndRotation(pose.Position, pose.Rotation);
            if ((title.transform.localScale - Vector3.one).sqrMagnitude > 0.0001f)
                title.transform.localScale = Vector3.one;
            return title;
        }

        private static bool IsPlaceholderTitle(GameObject title)
        {
            if (title == null) return false;
            if (PrefabUtility.IsPartOfPrefabInstance(title)) return false;
            return title.transform.Find(IntroAssets.PlaceholderGlyphs) != null;
        }
    }
}
#endif
