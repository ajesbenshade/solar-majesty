using System.Collections.Generic;

namespace SolarMajesty
{
    /// <summary>
    /// Pure rules behind the LODGroup on every placed building (Runtime/BuildingLod.cs).
    ///
    /// Buildings are procedural kits: a few big shells plus dozens of small parts (lamps, vents,
    /// antennas, world-dressing pivots). LOD1 keeps the shells and drops the small parts; below a
    /// tiny screen height the whole building culls. The camera is orthographic, where Unity's
    /// screen-relative height is <c>size / (2 * orthographicSize)</c>, times the quality level's
    /// LOD bias. The thresholds are chosen against the real zoom range (see the constants) so
    /// the iso camera keeps LOD0 at normal play zoom and never culls a building anywhere in its
    /// zoom range; LOD1 mostly earns its keep on low quality levels and toward the horizon in the
    /// perspective diorama view.
    /// </summary>
    public static class BuildingLodRules
    {
        /// <summary>A part whose largest side is under this fraction of the building's is detail.</summary>
        public const float DetailFraction = 0.06f;

        /// <summary>
        /// Per-world dressing (the <c>Dress_Arch</c> child) is secondary to the kit's own shape, so
        /// it drops out at a larger size. Its static parts are merged into building-sized meshes
        /// and stay; what goes is the small moving and blinking pieces.
        /// </summary>
        public const float DressingDetailFraction = 0.15f;

        /// <summary>
        /// LOD0 → LOD1. The smallest footprint is 4 cells × 1.5 m = 6 m; at the default play zoom
        /// (ortho 18) that is 6 / 36 = 0.167 of the screen at LOD bias 1, and 0.05 on the Very Low
        /// level (bias 0.3), still above this. On a 1080p screen a building is ~43 px tall here,
        /// so a 6 % part is under 3 px when it pops out.
        /// </summary>
        public const float DetailScreenHeight = 0.04f;

        /// <summary>
        /// LOD1 → culled. At max zoom-out (ortho 52) the smallest building is 6 / 104 = 0.058 at
        /// bias 1 and 0.017 at Very Low, so the iso camera never culls one. In the perspective
        /// diorama view only far buildings on low quality levels drop out.
        /// </summary>
        public const float CullScreenHeight = 0.01f;

        /// <summary>
        /// Name fragments of overlays that must never be put in a LOD: selection rings and proxies,
        /// status orbs and pips, labels, footprints, VFX, placement ghosts. The same list
        /// IndustrialArtDressing and ConstructionStages skip.
        /// </summary>
        private static readonly string[] OverlayTokens =
            { "SelectRing", "SelectProxy", "StatusOrb", "StatusPip", "Label", "Footprint", "Vfx", "Ghost" };

        /// <summary>True for an object name that belongs to a UI/selection overlay, not the building.</summary>
        public static bool IsOverlay(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            for (int i = 0; i < OverlayTokens.Length; i++)
            {
                if (name.Contains(OverlayTokens[i])) return true;
            }
            return false;
        }

        /// <summary>True for the per-world dressing holder and its children (<c>Dress_Arch*</c>).</summary>
        public static bool IsDressing(string name) =>
            !string.IsNullOrEmpty(name) && name.StartsWith("Dress_Arch");

        /// <summary>Is a part of <paramref name="extent"/> metres (largest side) detail on this building?</summary>
        public static bool IsDetail(float extent, float buildingSize, bool dressing)
        {
            // Empty or broken bounds are not worth a draw call at distance.
            if (!(extent > 0f)) return true;
            if (!(buildingSize > 0f)) return false;
            float fraction = dressing ? DressingDetailFraction : DetailFraction;
            return extent < buildingSize * fraction;
        }

        /// <summary>
        /// Which renderers LOD1 keeps, by index. <paramref name="extents"/> is each renderer's
        /// largest world-bounds side; <paramref name="dressing"/> (optional) marks Dress_Arch parts;
        /// <paramref name="buildingSize"/> is the largest side of the whole building (0 = use the
        /// largest part). The largest part is always kept, so LOD1 is never empty.
        /// </summary>
        public static List<int> SelectLod1(IList<float> extents, IList<bool> dressing, float buildingSize)
        {
            var keep = new List<int>();
            if (extents == null || extents.Count == 0) return keep;

            int largest = 0;
            for (int i = 1; i < extents.Count; i++)
            {
                if (extents[i] > extents[largest]) largest = i;
            }
            float size = buildingSize > 0f ? buildingSize : extents[largest];

            for (int i = 0; i < extents.Count; i++)
            {
                bool dress = dressing != null && i < dressing.Count && dressing[i];
                if (i == largest || !IsDetail(extents[i], size, dress))
                    keep.Add(i);
            }
            return keep;
        }

        /// <summary>
        /// Unity's screen-relative height for an orthographic camera, including the quality
        /// level's LOD bias. Used by tests to hold the thresholds against the camera's zoom range.
        /// </summary>
        public static float OrthoScreenHeight(float size, float orthoSize, float lodBias)
        {
            if (orthoSize <= 0f) return 1f;
            return size * 0.5f / orthoSize * lodBias;
        }
    }
}
