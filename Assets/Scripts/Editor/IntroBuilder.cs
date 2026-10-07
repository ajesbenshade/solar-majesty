#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace SolarMajesty.EditorTools
{
    /// <summary>
    /// Writes <c>Resources/Intro/IntroTimeline</c> and wires a PlayableDirector into the
    /// sandbox scene. The colony is built at runtime inside that one scene, so the intro
    /// is an overlay there rather than a scene at build index 0.
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
            AssetDatabase.Refresh();
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
            if (AssetDatabase.LoadAssetAtPath<TimelineAsset>(IntroAssets.TimelineAssetPath) != null)
                AssetDatabase.DeleteAsset(IntroAssets.TimelineAssetPath);

            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            timeline.name = "IntroTimeline";
            timeline.editorSettings.frameRate = 30d;
            AssetDatabase.CreateAsset(timeline, IntroAssets.TimelineAssetPath);

            var anim = BakeCameraClip();
            AssetDatabase.AddObjectToAsset(anim, timeline);
            var cameraTrack = timeline.CreateTrack<AnimationTrack>(null, IntroAssets.CameraTrack);
            cameraTrack.trackOffset = TrackOffset.ApplyTransformOffsets;
            var cameraClip = cameraTrack.CreateClip(anim);
            cameraClip.start = 0d;
            cameraClip.duration = IntroShot.Duration;
            cameraClip.displayName = "Sweep past Earth";

            var titleTrack = timeline.CreateTrack<ActivationTrack>(null, IntroAssets.TitleTrack);
            var titleClip = titleTrack.CreateDefaultClip();
            titleClip.start = IntroShot.TitleOn;
            titleClip.duration = IntroShot.TitleOff - IntroShot.TitleOn;
            titleClip.displayName = "Solar Majesty";

            var audioTrack = timeline.CreateTrack<AudioTrack>(null, IntroAssets.StingTrack);
            var sting = AssetDatabase.LoadAssetAtPath<AudioClip>(IntroAssets.StingAssetPath);
            if (sting != null)
            {
                var stingClip = audioTrack.CreateClip(sting);
                stingClip.start = IntroShot.TitleOn;
                stingClip.displayName = "Intro Sting";
            }
            else
            {
                Debug.Log("[Intro] No clip at " + IntroAssets.StingAssetPath + ". Audio track left empty.");
            }

            EditorUtility.SetDirty(timeline);
            return timeline;
        }

        private static AnimationClip BakeCameraClip()
        {
            var clip = new AnimationClip { name = "IntroCameraSweep", frameRate = 30f, legacy = false };
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

            SetCurve(clip, typeof(Transform), "m_LocalPosition.x", posX);
            SetCurve(clip, typeof(Transform), "m_LocalPosition.y", posY);
            SetCurve(clip, typeof(Transform), "m_LocalPosition.z", posZ);
            SetCurve(clip, typeof(Transform), "m_LocalRotation.x", rotX);
            SetCurve(clip, typeof(Transform), "m_LocalRotation.y", rotY);
            SetCurve(clip, typeof(Transform), "m_LocalRotation.z", rotZ);
            SetCurve(clip, typeof(Transform), "m_LocalRotation.w", rotW);
            clip.EnsureQuaternionContinuity();

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            return clip;
        }

        private static void AddLinearKey(AnimationCurve curve, float time, float value)
        {
            int index = curve.AddKey(new Keyframe(time, value));
            AnimationUtility.SetKeyLeftTangentMode(curve, index, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(curve, index, AnimationUtility.TangentMode.Linear);
        }

        private static void SetCurve(AnimationClip clip, System.Type type, string property, AnimationCurve curve)
        {
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", type, property), curve);
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
            animator.runtimeAnimatorController = null;
            animator.enabled = false;

            var root = GameObject.Find("Intro");
            if (root == null)
                root = new GameObject("Intro");

            var director = root.GetComponent<PlayableDirector>();
            if (director == null)
                director = root.AddComponent<PlayableDirector>();
            director.playableAsset = timeline;
            director.playOnAwake = false;
            director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
            director.extrapolationMode = DirectorWrapMode.Hold;

            var sequence = root.GetComponent<IntroSequence>();
            if (sequence == null)
                sequence = root.AddComponent<IntroSequence>();

            var source = root.GetComponent<AudioSource>();
            if (source == null)
                source = root.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;

            var title = EnsureTitleSlot(root.transform);
            title.SetActive(false);

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
            earth.position = IntroShot.EarthPosition(ColonyLayout.CameraFocus);
            earth.localScale = Vector3.one * IntroShot.EarthScale;
            earth.gameObject.SetActive(false);

            foreach (var output in timeline.outputs)
            {
                var track = output.sourceObject;
                if (track is AnimationTrack)
                    director.SetGenericBinding(track, animator);
                else if (track is ActivationTrack)
                    director.SetGenericBinding(track, title);
                else if (track is AudioTrack)
                    director.SetGenericBinding(track, source);
            }

            var so = new SerializedObject(sequence);
            so.FindProperty("director").objectReferenceValue = director;
            so.FindProperty("titleRoot").objectReferenceValue = title;
            so.FindProperty("stingSource").objectReferenceValue = source;
            so.FindProperty("spawnEarthGlobe").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Intro] Wired PlayableDirector into " + ScenePath + " (build index stays 0).");
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
            title.transform.SetPositionAndRotation(pose.Position, pose.Rotation);
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
