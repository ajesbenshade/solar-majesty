using NUnit.Framework;

namespace SolarMajesty.Tests
{
    public class DisplayClampTests
    {
        [Test]
        public void StoredModeLargerThanTheMonitor_ShrinksToTheMonitor()
        {
            int w = 3840, h = 2160;
            Assert.IsTrue(DemoSettings.ClampToDisplay(ref w, ref h, 2560, 1440));
            Assert.AreEqual(2560, w);
            Assert.AreEqual(1440, h);
        }

        [Test]
        public void ModeThatFits_OrUnknownDisplay_IsLeftAlone()
        {
            int w = 1920, h = 1080;
            Assert.IsFalse(DemoSettings.ClampToDisplay(ref w, ref h, 2560, 1440));
            Assert.AreEqual(1920, w);
            w = 3840; h = 2160;
            Assert.IsFalse(DemoSettings.ClampToDisplay(ref w, ref h, 0, 0), "no display size reported");
            Assert.AreEqual(3840, w);
        }
    }
}
