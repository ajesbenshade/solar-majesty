using NUnit.Framework;

namespace SolarMajesty.Tests
{
    public class QualityAutoDetectTests
    {
        [TestCase("NVIDIA GeForce RTX 3080", 10051, 65536, QualityAutoDetect.Ultra)]
        [TestCase("NVIDIA GeForce RTX 3060", 6144, 16384, QualityAutoDetect.VeryHigh)]
        [TestCase("NVIDIA GeForce GTX 1650", 4096, 16384, QualityAutoDetect.High)]
        [TestCase("NVIDIA GeForce GT 1030", 2048, 8192, QualityAutoDetect.Medium)]
        [TestCase("Intel(R) Arc(TM) A770 Graphics", 16000, 32768, QualityAutoDetect.Ultra)]
        public void DiscreteCards_ScaleWithVideoMemory(string gpu, int vram, int ram, int expected)
        {
            Assert.That(QualityAutoDetect.Pick(gpu, vram, ram, 6), Is.EqualTo(expected));
        }

        [TestCase("AMD Radeon(TM) Graphics", 48275, 65536, QualityAutoDetect.Medium)]
        [TestCase("Intel(R) UHD Graphics 620", 4096, 8192, QualityAutoDetect.Low)]
        [TestCase("Intel(R) Iris(R) Xe Graphics", 8000, 16384, QualityAutoDetect.Medium)]
        [TestCase("AMD Radeon 780M", 16000, 32768, QualityAutoDetect.Medium)]
        public void IntegratedGpus_IgnoreSharedMemory(string gpu, int vram, int ram, int expected)
        {
            Assert.That(QualityAutoDetect.Pick(gpu, vram, ram, 6), Is.EqualTo(expected));
        }

        [Test]
        public void LowRam_AndSoftwareRendering_StayLow()
        {
            Assert.That(QualityAutoDetect.Pick("NVIDIA GeForce RTX 3080", 10000, 4096, 6), Is.EqualTo(QualityAutoDetect.Low));
            Assert.That(QualityAutoDetect.Pick("Microsoft Basic Render Driver", 0, 16384, 6), Is.EqualTo(QualityAutoDetect.VeryLow));
        }

        [Test]
        public void NeverExceedsTheLevelsThatExist()
        {
            Assert.That(QualityAutoDetect.Pick("NVIDIA GeForce RTX 4090", 24000, 65536, 3), Is.EqualTo(2));
            Assert.That(QualityAutoDetect.Pick(null, 0, 0, 6), Is.EqualTo(QualityAutoDetect.High));
        }
    }
}
