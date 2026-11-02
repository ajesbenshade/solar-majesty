using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>Where a one-shot plays: flat in the listener's head, or at a spot on the map.</summary>
    public enum SfxSpace
    {
        TwoD = 0,
        Spatial = 1
    }

    /// <summary>
    /// Which sound effects are world-located, and how loud a positioned one is at a given distance.
    ///
    /// Pure so the decisions can be tested without an AudioSource. <c>DemoAudio</c> asks
    /// <see cref="Route"/> before every one-shot, and <c>SpatialAudio</c> sets each voice's level
    /// from <see cref="ComputeRolloff"/> and <see cref="Gain"/> every frame it plays.
    /// </summary>
    public static class SfxRouting
    {
        /// <summary>
        /// Events that happen at a place: robots, fauna, construction, extraction, the launch pad.
        /// Everything else (UI, alerts, research, mission outcome, the launch roar) is about the
        /// colony as a whole and stays 2D even if a caller passes a position.
        /// </summary>
        private static readonly HashSet<string> WorldEvents = new HashSet<string>
        {
            "flag_post", "build_place", "build_complete", "construction_tick",
            "claim", "bite", "robot_hit", "robot_down", "level_up", "credits",
            "robot_spawn", "party_form", "repair", "heal", "equip", "extract",
            "stalker_growl", "stalker_death", "launch_ready",
        };

        public static IEnumerable<string> WorldEventKeys => WorldEvents;

        public static bool IsWorldEvent(string key) => key != null && WorldEvents.Contains(key);

        /// <summary>
        /// 3D only when the event is world-located, a usable position is known and the pooled 3D
        /// player exists. Any missing piece degrades to 2D rather than silence.
        /// </summary>
        public static SfxSpace Route(string key, bool hasPosition, Vector3 position, bool spatialReady)
        {
            if (!IsWorldEvent(key)) return SfxSpace.TwoD;
            if (!hasPosition || !IsFinite(position)) return SfxSpace.TwoD;
            return spatialReady ? SfxSpace.Spatial : SfxSpace.TwoD;
        }

        public static bool IsFinite(Vector3 v) =>
            !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
              float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));

        // ----------------------------------------------------------------------------------------
        // Rolloff relative to what the overseer camera shows
        // ----------------------------------------------------------------------------------------
        //
        // Distance is measured on the ground from the point at screen centre, not from the
        // AudioListener. The listener rides the camera 44 m from the ground (about 63 m in the
        // diorama lens), and an orthographic camera's depth is arbitrary: listener distance put
        // the top edge of the default view ~77 m away and the bottom edge ~22 m, so under the old
        // 6–70 m rolloff the bottom played near full while the top was silent, and the knees moved
        // whenever the lens did.
        // Ground distance from the focus is the same in every direction and in both lenses.

        /// <summary>Gain at the left/right edge of the screen (about -5 dB): still clearly audible.</summary>
        public const float EdgeGain = 0.55f;

        /// <summary>Full volume out to this fraction of the half-width: the middle of the view.</summary>
        public const float FullFraction = 0.5f;

        /// <summary>Off-screen tail never shorter than this, so a fight just past the edge is heard.</summary>
        public const float MinTail = 30f;

        /// <summary>Half the visible height when no camera is known (ColonyLayout.CameraOrthoSize).</summary>
        public const float DefaultHalfHeight = 18f;

        /// <summary>
        /// Ground distances (metres from the point at screen centre) of the three rolloff knees.
        /// Full: the middle of the view plays at the 2D level. Edge: the left/right edge of the
        /// screen, <see cref="EdgeGain"/>. Far: silence, one more half-screen (at least
        /// <see cref="MinTail"/>) past the edge.
        /// </summary>
        public struct Rolloff
        {
            public float Full;
            public float Edge;
            public float Far;
        }

        /// <param name="halfHeight">Half the visible height at the focus, metres: the orthographic
        /// size, which the diorama lens keeps (see <see cref="VisibleHalfHeight"/>).</param>
        /// <param name="aspect">Screen width / height.</param>
        public static Rolloff ComputeRolloff(float halfHeight, float aspect)
        {
            float h = float.IsNaN(halfHeight) || halfHeight <= 0f ? DefaultHalfHeight : halfHeight;
            float a = float.IsNaN(aspect) || aspect <= 0f ? 16f / 9f : Mathf.Clamp(aspect, 1f, 2.4f);
            float halfWidth = h * a;
            return new Rolloff
            {
                Full = halfWidth * FullFraction,
                Edge = halfWidth,
                Far = halfWidth + Mathf.Max(halfWidth, MinTail)
            };
        }

        /// <summary>
        /// Half the visible height at the focus. Orthographic: the ortho size. Perspective: the
        /// frustum at the focus distance, which for the diorama lens equals its zoom value.
        /// </summary>
        public static float VisibleHalfHeight(bool orthographic, float orthoSize, float fieldOfView, float focusDistance)
        {
            if (orthographic) return orthoSize;
            return Mathf.Max(0f, focusDistance) * Mathf.Tan(Mathf.Clamp(fieldOfView, 1f, 179f) * 0.5f * Mathf.Deg2Rad);
        }

        /// <summary>Distance on the ground plane (height ignored).</summary>
        public static float GroundDistance(Vector3 focus, Vector3 position)
        {
            float dx = position.x - focus.x;
            float dz = position.z - focus.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>Piecewise-linear gain for a source <paramref name="groundDistance"/> metres from the focus.</summary>
        public static float Gain(Rolloff r, float groundDistance)
        {
            if (float.IsNaN(groundDistance) || groundDistance <= r.Full) return 1f;
            if (groundDistance <= r.Edge)
                return Mathf.Lerp(1f, EdgeGain, Mathf.InverseLerp(r.Full, r.Edge, groundDistance));
            if (groundDistance < r.Far)
                return Mathf.Lerp(EdgeGain, 0f, Mathf.InverseLerp(r.Edge, r.Far, groundDistance));
            return 0f;
        }
    }
}
