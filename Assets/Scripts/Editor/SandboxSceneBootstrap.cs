#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SolarMajesty.EditorTools
{
    /// <summary>
    /// Opens the sandbox scene when the editor starts with nothing loaded.
    ///
    /// Unity restores the scene setup it saved on quit. When that setup is empty — which the
    /// capture and scene-building tools can leave behind — the project opens on an untitled empty
    /// scene, and pressing Play renders a blank Game view with no error to explain why.
    /// </summary>
    [InitializeOnLoad]
    public static class SandboxSceneBootstrap
    {
        private const string ScenePath = "Assets/Scenes/LunarOutpost_Sandbox.unity";

        /// <summary>Guards against looping if the sandbox scene itself cannot run the game.</summary>
        private static bool _retriedPlay;

        static SandboxSceneBootstrap()
        {
            // CI and build scripts open their own scenes; never race them.
            if (Application.isBatchMode) return;

            EditorApplication.delayCall += OpenIfNothingLoaded;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("Solar Majesty/Open Sandbox Scene %#o", priority = 1)]
        public static void OpenSandbox()
        {
            if (!System.IO.File.Exists(ScenePath))
            {
                Debug.LogError($"[Solar Majesty] Missing {ScenePath}. Use Solar Majesty > Build Demo Scene.");
                return;
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log($"[Solar Majesty] Opened {ScenePath} — press Play.");
        }

        private static void OpenIfNothingLoaded()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!IsUntitledAndEmpty()) return;
            if (!System.IO.File.Exists(ScenePath)) return;

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log($"[Solar Majesty] No scene was open — restored {ScenePath}.");
        }

        /// <summary>True when the only thing loaded is an unsaved, empty scene.</summary>
        private static bool IsUntitledAndEmpty()
        {
            if (SceneManager.sceneCount == 0) return true;
            if (SceneManager.sceneCount > 1) return false;

            Scene scene = SceneManager.GetSceneAt(0);
            return string.IsNullOrEmpty(scene.path) && !scene.isDirty && scene.rootCount == 0;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                _retriedPlay = false;
                return;
            }

            if (state != PlayModeStateChange.ExitingEditMode) return;
            if (Object.FindAnyObjectByType<GameLoop>() != null) return;

            // Playing this scene can only produce an empty Game view. Stop before that happens.
            EditorApplication.isPlaying = false;

            if (_retriedPlay || !System.IO.File.Exists(ScenePath))
            {
                Debug.LogError(
                    $"[Solar Majesty] The open scene has no GameLoop, and {ScenePath} did not supply one.");
                return;
            }

            // Never discard the user's unsaved work to do it.
            if (SceneManager.sceneCount == 1 && SceneManager.GetSceneAt(0).isDirty)
            {
                Debug.LogError(
                    "[Solar Majesty] The open scene has no GameLoop and has unsaved changes. " +
                    $"Save it, then open {ScenePath} (Solar Majesty > Open Sandbox Scene).");
                return;
            }

            _retriedPlay = true;
            EditorApplication.delayCall += () =>
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                Debug.Log($"[Solar Majesty] Opened {ScenePath} — entering Play Mode.");
                EditorApplication.isPlaying = true;
            };
        }
    }
}
#endif
