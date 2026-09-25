using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace SolarMajesty.Tests
{
    /// <summary>Spoken hero lines: casting, the TTS request, WAV decoding and the robot filter.</summary>
    public class HeroSpeechTests
    {
        [Test]
        public void Casting_IsStablePerHeroAndOnlyTheMedicIsHuman()
        {
            var a = HeroSpeech.VoiceFor(SpecialistClass.DefenseMech, 42, 0.5f);
            var b = HeroSpeech.VoiceFor(SpecialistClass.DefenseMech, 42, 0.5f);
            Assert.AreEqual(a.Voice, b.Voice, "same hero, same voice");
            Assert.Greater(a.Robot, 0.5f, "mechs get the heavy radio voice");
            Assert.AreEqual(0f, HeroSpeech.VoiceFor(SpecialistClass.Medic, 7, 0.5f).Robot, "the Medic is human");
            Assert.AreNotEqual(HeroSpeech.VoiceFor(SpecialistClass.EngineerBot, 0, 0.5f).Voice,
                HeroSpeech.VoiceFor(SpecialistClass.EngineerBot, 1, 0.5f).Voice, "two voices per class");
            Assert.Greater(HeroSpeech.VoiceFor(SpecialistClass.EngineerBot, 0, 0.95f).Speed,
                HeroSpeech.VoiceFor(SpecialistClass.EngineerBot, 0, 0.05f).Speed, "workaholics talk faster");
            foreach (SpecialistClass cls in Enum.GetValues(typeof(SpecialistClass)))
                StringAssert.IsMatch("^[ab][fm]_", HeroSpeech.VoiceFor(cls, 3, 0.5f).Voice, $"{cls} has a Kokoro voice");
        }

        [Test]
        public void Request_IsOpenAISpeechJson()
        {
            var v = new HeroVoiceSpec { Voice = "am_onyx", Speed = 1.05f };
            var root = (Dictionary<string, object>)LocalJson.Parse(HeroSpeech.BuildRequest("Pay me \"first\".", v));
            Assert.AreEqual("Pay me \"first\".", root["input"]);
            Assert.AreEqual("am_onyx", root["voice"]);
            Assert.AreEqual(1.05, (double)root["speed"], 1e-6);
            Assert.AreEqual("wav", root["response_format"]);
        }

        private static byte[] Wav(short format, short bits, short channels, int rate, byte[] data, int dataSizeField, bool extensible = false)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write("RIFF".ToCharArray()); w.Write(0); w.Write("WAVE".ToCharArray());
            w.Write("fmt ".ToCharArray());
            w.Write(extensible ? 40 : 16);
            w.Write(extensible ? (short)-2 : format); // 0xFFFE
            w.Write(channels); w.Write(rate); w.Write(rate * channels * bits / 8);
            w.Write((short)(channels * bits / 8)); w.Write(bits);
            if (extensible) { w.Write((short)22); w.Write(bits); w.Write(0); w.Write(format); w.Write(new byte[14]); }
            w.Write("LIST".ToCharArray()); w.Write(4); w.Write("INFO".ToCharArray()); // chunk to skip
            w.Write("data".ToCharArray()); w.Write(dataSizeField); w.Write(data);
            return ms.ToArray();
        }

        [Test]
        public void Wav_DecodesPcm16Float32ExtensibleAndStreamingSizes()
        {
            var pcm = new byte[] { 0x00, 0x40, 0x00, 0xC0 }; // +0.5, -0.5
            Assert.IsTrue(HeroSpeech.TryDecodeWav(Wav(1, 16, 1, 24000, pcm, 4), out var s, out int ch, out int rate));
            Assert.AreEqual(1, ch); Assert.AreEqual(24000, rate);
            Assert.AreEqual(new[] { 0.5f, -0.5f }, s);

            Assert.IsTrue(HeroSpeech.TryDecodeWav(Wav(1, 16, 1, 24000, pcm, 0), out s, out _, out _), "streaming size 0");
            Assert.AreEqual(2, s.Length);
            Assert.IsTrue(HeroSpeech.TryDecodeWav(Wav(1, 16, 1, 24000, pcm, -1), out s, out _, out _), "streaming size 0xFFFFFFFF");
            Assert.AreEqual(2, s.Length);

            var f32 = new byte[8];
            BitConverter.GetBytes(0.25f).CopyTo(f32, 0);
            BitConverter.GetBytes(-1f).CopyTo(f32, 4);
            Assert.IsTrue(HeroSpeech.TryDecodeWav(Wav(3, 32, 2, 22050, f32, 8, extensible: true), out s, out ch, out rate));
            Assert.AreEqual(2, ch); Assert.AreEqual(22050, rate);
            Assert.AreEqual(new[] { 0.25f, -1f }, s);

            Assert.IsFalse(HeroSpeech.TryDecodeWav(new byte[10], out _, out _, out _));
            Assert.IsFalse(HeroSpeech.TryDecodeWav(Wav(1, 8, 1, 8000, pcm, 4), out _, out _, out _), "8-bit unsupported");
        }

        [Test]
        public void Robot_KeepsLengthNoClippingAndZeroIsUntouched()
        {
            var tone = new float[24000];
            for (int i = 0; i < tone.Length; i++) tone[i] = 0.9f * (float)Math.Sin(i * 2 * Math.PI * 440 / 24000);
            var clean = (float[])tone.Clone();
            HeroSpeech.ApplyRobot(clean, 1, 24000, 0f);
            Assert.AreEqual(tone, clean, "strength 0 is a no-op");

            var bot = (float[])tone.Clone();
            HeroSpeech.ApplyRobot(bot, 1, 24000, 0.8f);
            Assert.AreEqual(tone.Length, bot.Length);
            float peak = 0f, energy = 0f;
            foreach (var x in bot) { peak = Math.Max(peak, Math.Abs(x)); energy += x * x; }
            Assert.LessOrEqual(peak, 1f, "no clipping");
            Assert.Greater(energy / bot.Length, 0.01f, "still audible");
            Assert.AreNotEqual(tone, bot, "the filter did something");
        }
    }
}
