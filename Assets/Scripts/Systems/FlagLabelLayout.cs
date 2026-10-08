using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Screen-space flag labels. A click on the text is the same pole as a click on the mesh.
    /// Overlaps are pushed apart in a later pass; this type only owns the rectangles.
    /// </summary>
    public static class FlagLabelLayout
    {
        public struct ScreenLabel
        {
            public Vector2 Center;
            public float Width;
            public float Height;
            public int Priority;
            public float OffsetY;
            public bool HideDetail;
        }

        public static Rect Bounds(ScreenLabel label)
        {
            float y = label.Center.y + label.OffsetY;
            float w = label.Width < 1f ? 1f : label.Width;
            float h = label.Height < 1f ? 1f : label.Height;
            return new Rect(label.Center.x - w * 0.5f, y - h * 0.5f, w, h);
        }

        public static bool Hits(ScreenLabel label, Vector2 screenPoint) =>
            Bounds(label).Contains(screenPoint);

        /// <summary>Index of the highest-priority label under the point, or -1.</summary>
        public static int Pick(ScreenLabel[] labels, Vector2 screenPoint)
        {
            if (labels == null) return -1;
            int best = -1;
            int bestPriority = int.MinValue;
            for (int i = 0; i < labels.Length; i++)
            {
                if (!Hits(labels[i], screenPoint)) continue;
                if (labels[i].Priority < bestPriority) continue;
                bestPriority = labels[i].Priority;
                best = i;
            }
            return best;
        }
    }
}
