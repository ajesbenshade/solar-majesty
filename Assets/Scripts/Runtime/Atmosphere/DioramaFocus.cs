using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SolarMajesty
{
    /// <summary>
    /// Tilt-shift "miniature" focus for the diorama camera: Bokeh depth of field locked on the
    /// ground at screen centre, strongest up close and gone by mid zoom so the vista stays sharp.
    /// Lives on its own volume so the tuned grade volume is untouched. Perspective camera only.
    /// </summary>
    public sealed class DioramaFocus : MonoBehaviour
    {
        /// <summary>Full effect at or below this zoom fraction…</summary>
        public const float FullAt = 0.22f;
        /// <summary>…fading to none at this one.</summary>
        public const float GoneAt = 0.58f;

        private static DioramaFocus _instance;
        private Volume _volume;
        private DepthOfField _dof;
        private IsometricCameraController _rig;
        private GameLoop _loop;

        public static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("DioramaFocusVolume");
            _instance = go.AddComponent<DioramaFocus>();
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 5f;
            volume.weight = 0f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "DioramaFocus_Runtime";
            var dof = profile.Add<DepthOfField>(true);
            dof.mode.Override(DepthOfFieldMode.Bokeh);
            dof.focusDistance.Override(30f);
            dof.focalLength.Override(150f);
            dof.aperture.Override(1.4f);
            volume.sharedProfile = profile;
            _instance._volume = volume;
            _instance._dof = dof;
        }

        /// <summary>Effect strength for a zoom fraction (pure; used by tests).</summary>
        public static float Strength(float zoom01)
        {
            float t = Mathf.Clamp01((zoom01 - FullAt) / (GoneAt - FullAt));
            return 1f - t * t * (3f - 2f * t);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void LateUpdate()
        {
            if (_volume == null || _dof == null) return;
            if (_loop == null) _loop = FindAnyObjectByType<GameLoop>();
            var cam = Camera.main;
            if (_rig == null && cam != null) _rig = cam.GetComponent<IsometricCameraController>();

            bool on = DemoSettings.TiltShift && IsometricCameraController.DioramaActive &&
                      _rig != null && cam != null && !cam.orthographic &&
                      _loop != null && _loop.Screen == DemoScreen.Playing;
            float w = on ? Strength(_rig.Zoom01) : 0f;
            _volume.weight = w;
            if (w <= 0f) return;

            float focus = Mathf.Max(1f, _rig.FocusDistance);
            _dof.focusDistance.Override(focus);
            // Longer lens as the camera backs off keeps the blur band a similar screen size.
            _dof.focalLength.Override(Mathf.Clamp(150f + focus * 1.5f, 1f, 300f));
        }
    }
}
