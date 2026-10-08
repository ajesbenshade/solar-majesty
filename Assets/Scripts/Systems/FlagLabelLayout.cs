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

        /// <summary>
        /// Push overlapping labels apart in screen space. Higher priority stays put.
        /// A push taller than half the label also hides that label's detail line.
        /// </summary>
        public static void Resolve(ScreenLabel[] labels, float gap)
        {
            if (labels == null) return;
            for (int i = 0; i < labels.Length; i++)
            {
                labels[i].OffsetY = 0f;
                labels[i].HideDetail = false;
            }
            if (labels.Length < 2) return;

            for (int pass = 0; pass < 6; pass++)
            {
                bool moved = false;
                for (int i = 0; i < labels.Length; i++)
                {
                    for (int j = i + 1; j < labels.Length; j++)
                    {
                        if (!Overlaps(labels[i], labels[j], gap)) continue;
                        int push = PreferPush(labels, i, j);
                        float need = OverlapHeight(labels[i], labels[j]) + gap;
                        if (need < 4f) need = 4f;
                        float next = labels[push].OffsetY + need;
                        float cap = labels[push].Height * 3f;
                        if (cap < 24f) cap = 24f;
                        if (next > cap) next = cap;
                        if (next > labels[push].OffsetY + 0.5f)
                        {
                            labels[push].OffsetY = next;
                            moved = true;
                        }
                        if (labels[push].OffsetY > labels[push].Height * 0.5f)
                            labels[push].HideDetail = true;
                    }
                }
                if (!moved) break;
            }
        }

        static int PreferPush(ScreenLabel[] labels, int i, int j)
        {
            if (labels[i].Priority != labels[j].Priority)
                return labels[i].Priority < labels[j].Priority ? i : j;
            float yi = labels[i].Center.y + labels[i].OffsetY;
            float yj = labels[j].Center.y + labels[j].OffsetY;
            return yi <= yj ? i : j;
        }

        static bool Overlaps(ScreenLabel a, ScreenLabel b, float gap)
        {
            Rect ra = Bounds(a);
            Rect rb = Bounds(b);
            ra.x -= gap;
            ra.y -= gap;
            ra.width += gap * 2f;
            ra.height += gap * 2f;
            return ra.Overlaps(rb);
        }

        static float OverlapHeight(ScreenLabel a, ScreenLabel b)
        {
            Rect ra = Bounds(a);
            Rect rb = Bounds(b);
            float overlap = Mathf.Min(ra.yMax, rb.yMax) - Mathf.Max(ra.yMin, rb.yMin);
            return overlap < 0f ? 0f : overlap;
        }

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
