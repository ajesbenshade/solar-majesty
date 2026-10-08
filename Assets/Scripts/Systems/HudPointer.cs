using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// IMGUI pointer tests in the same space as a scale-from-origin <c>GUI.matrix</c>.
    /// OnGUI records panel rects in that space. The camera reads them from Update
    /// (the previous OnGUI pass) so a wheel notch over a panel does not zoom the map.
    /// </summary>
    public static class HudPointer
    {
        /// <summary>Extra GUI pixels so a scrollbar just outside a panel still counts as UI.</summary>
        public const float HitPad = 6f;

        public static Vector2 ScreenToGui(Vector2 screenMouse, float screenHeight, float scale)
        {
            float s = scale < 0.05f ? 0.05f : scale;
            return new Vector2(screenMouse.x / s, (screenHeight - screenMouse.y) / s);
        }

        public static bool OverRects(IReadOnlyList<Rect> guiRects, Vector2 guiMouse, float pad = HitPad)
        {
            if (guiRects == null) return false;
            for (int i = 0; i < guiRects.Count; i++)
            {
                Rect r = guiRects[i];
                r.x -= pad;
                r.y -= pad;
                r.width += pad * 2f;
                r.height += pad * 2f;
                if (r.Contains(guiMouse)) return true;
            }
            return false;
        }

        /// <summary>
        /// The wheel zooms the map only during play, and only when it is not over IMGUI chrome
        /// or a uGUI object. Settings, pause, and the title are not play, so the wheel cannot zoom.
        /// </summary>
        public static bool WheelZoomsMap(bool overImgui, bool overEventSystem, bool playing)
        {
            if (!playing) return false;
            if (overImgui || overEventSystem) return false;
            return true;
        }
    }
}
