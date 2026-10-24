using NUnit.Framework;

namespace SolarMajesty.Tests
{
    public class DevToolsTests
    {
        [TearDown]
        public void TearDown() => DevTools.Override(null);

        [Test]
        public void ReleaseBuild_HasNoCheats()
        {
            Assert.That(DevTools.Resolve(false, false, new[] { "SolarMajesty.exe" }), Is.False);
            Assert.That(DevTools.Resolve(false, false, null), Is.False);
        }

        [Test]
        public void EditorAndDevelopmentBuilds_KeepThem()
        {
            Assert.That(DevTools.Resolve(true, false, null), Is.True);
            Assert.That(DevTools.Resolve(false, true, null), Is.True);
        }

        [Test]
        public void DevtoolsArg_TurnsThemOnInRelease()
        {
            Assert.That(DevTools.Resolve(false, false, new[] { "SolarMajesty.exe", "-DevTools" }), Is.True);
        }
    }
}
