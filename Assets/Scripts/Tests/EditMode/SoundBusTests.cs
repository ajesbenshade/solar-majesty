using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    public class SoundBusTests
    {
        [SetUp]
        public void SetUp()
        {
            SoundBus.ResetAll();
            DemoSettings.Master = 1f;
            DemoSettings.Sfx = 1f;
            DemoSettings.Ambient = 1f;
        }

        [TearDown]
        public void TearDown() => SoundBus.ResetAll();

        [Test]
        public void Volume_RespectsMasterSetting()
        {
            SoundBus.SetLevel(SoundChannel.Sfx, 1f);
            float full = SoundBus.Volume(SoundChannel.Sfx);

            DemoSettings.Master = 0.5f;
            float half = SoundBus.Volume(SoundChannel.Sfx);

            Assert.Less(half, full);
            Assert.AreEqual(full * 0.5f, half, 1e-4f);
        }

        [Test]
        public void Volume_AmbientFollowsTheAmbientSlider()
        {
            SoundBus.SetLevel(SoundChannel.Ambient, 1f);
            DemoSettings.Ambient = 0f;

            Assert.AreEqual(0f, SoundBus.Volume(SoundChannel.Ambient), 1e-4f);
        }

        [Test]
        public void Volume_IsAlwaysNormalised()
        {
            SoundBus.SetLevel(SoundChannel.Sfx, 5f);

            Assert.LessOrEqual(SoundBus.Volume(SoundChannel.Sfx), 1f);
        }

        /// <summary>The Overseer has to be audible over the bed, which is what ducking is for.</summary>
        [Test]
        public void DuckBed_LowersAmbientAndMusic()
        {
            float before = SoundBus.Volume(SoundChannel.Ambient);

            SoundBus.DuckBed(0.6f, 2f);
            SoundBus.Tick(1f);

            Assert.Less(SoundBus.Volume(SoundChannel.Ambient), before);
            Assert.Less(SoundBus.DuckAmount(SoundChannel.Music), 1f);
        }

        [Test]
        public void Duck_RecoversAfterTheHold()
        {
            SoundBus.DuckBed(0.8f, 0.5f);
            SoundBus.Tick(0.5f);
            Assert.Less(SoundBus.DuckAmount(SoundChannel.Ambient), 1f);

            // Hold expires, then the slow release brings it back.
            for (int i = 0; i < 40; i++)
                SoundBus.Tick(0.1f);

            Assert.AreEqual(1f, SoundBus.DuckAmount(SoundChannel.Ambient), 1e-3f);
        }

        /// <summary>A quiet duck must not cancel a loud one that is still holding.</summary>
        [Test]
        public void Duck_DeeperRequestWins()
        {
            SoundBus.DuckFor(SoundChannel.Ambient, 0.9f, 3f);
            SoundBus.Tick(0.5f);
            float deep = SoundBus.DuckAmount(SoundChannel.Ambient);

            SoundBus.DuckFor(SoundChannel.Ambient, 0.1f, 3f);
            SoundBus.Tick(0.5f);

            Assert.LessOrEqual(SoundBus.DuckAmount(SoundChannel.Ambient), deep + 1e-3f);
        }

        [Test]
        public void Tick_ZeroDelta_IsInert()
        {
            SoundBus.DuckBed(0.5f, 1f);
            float before = SoundBus.DuckAmount(SoundChannel.Ambient);

            SoundBus.Tick(0f);

            Assert.AreEqual(before, SoundBus.DuckAmount(SoundChannel.Ambient));
        }

        [Test]
        public void SetLevel_Clamps()
        {
            SoundBus.SetLevel(SoundChannel.Music, -3f);
            Assert.AreEqual(0f, SoundBus.Level(SoundChannel.Music));

            SoundBus.SetLevel(SoundChannel.Music, 9f);
            Assert.AreEqual(1f, SoundBus.Level(SoundChannel.Music));
        }
    }
}
