#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SolarMajesty.EditorTools
{
    /// <summary>
    /// Polls for Temp/PlayDemo.flag — when present, opens sandbox and enters Play Mode.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayDemoOnLoad
    {
        private const string FlagPath = "Temp/PlayDemo.flag";
        private const string ScenePath = "Assets/Scenes/LunarOutpost_Sandbox.unity";
        private static bool _armed = true;

        static PlayDemoOnLoad()
        {
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            if (!_armed || EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (!File.Exists(FlagPath))
                return;

            _armed = false;
            try { File.Delete(FlagPath); } catch { /* ignore */ }

            if (!File.Exists(ScenePath))
            {
                Debug.LogError("[Solar Majesty] Missing demo scene: " + ScenePath);
                _armed = true;
                return;
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
            Debug.Log("[Solar Majesty] Auto Play — LunarOutpost_Sandbox (use Game tab).");
            // Re-arm after leaving play mode so future flags work.
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        private static void OnPlayMode(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.playModeStateChanged -= OnPlayMode;
                _armed = true;
            }
        }
    }
}
#endif
