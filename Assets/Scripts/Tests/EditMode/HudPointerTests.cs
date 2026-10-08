using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    public class HudPointerTests
    {
        [Test]
        public void ScreenToGui_MatchesScaleFromOrigin()
        {
            // 4K, UI at 200%. A click at the top-right of the monitor is the top-right of the GUI.
            Vector2 gui = HudPointer.ScreenToGui(new Vector2(3840f, 2160f), 2160f, 2f);
            Assert.AreEqual(1920f, gui.x, 0.01f);
            Assert.AreEqual(0f, gui.y, 0.01f);

            Vector2 mid = HudPointer.ScreenToGui(new Vector2(960f, 540f), 1080f, 1f);
            Assert.AreEqual(960f, mid.x, 0.01f);
            Assert.AreEqual(540f, mid.y, 0.01f);
        }

        [Test]
        public void OverRects_HitsAPanelAndMissesTheMap()
        {
            var panels = new[] { new Rect(16f, 16f, 320f, 400f) };
            Assert.IsTrue(HudPointer.OverRects(panels, new Vector2(40f, 40f)));
            Assert.IsFalse(HudPointer.OverRects(panels, new Vector2(900f, 500f)));
            // A scrollbar sitting just outside the panel still counts as UI.
            Assert.IsTrue(HudPointer.OverRects(panels, new Vector2(340f, 200f)));
        }

        [Test]
        public void Wheel_DoesNotZoomOverUi_OrWhenNotPlaying()
        {
            Assert.IsFalse(HudPointer.WheelZoomsMap(overImgui: true, overEventSystem: false, playing: true));
            Assert.IsFalse(HudPointer.WheelZoomsMap(overImgui: false, overEventSystem: true, playing: true));
            Assert.IsFalse(HudPointer.WheelZoomsMap(overImgui: false, overEventSystem: false, playing: false),
                "Settings, pause, and the title are not the map");
            Assert.IsTrue(HudPointer.WheelZoomsMap(overImgui: false, overEventSystem: false, playing: true));
        }
    }
}
