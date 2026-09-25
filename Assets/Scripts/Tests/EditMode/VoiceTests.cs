using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// Character voices: line keys shared with the Python bake, the voice bank, who gets to speak,
    /// WAV decoding for live lines, and that the shipped bank covers every scripted line.
    /// </summary>
    public class VoiceTests
    {
        [Test]
        public void LineKey_MatchesThePythonBake()
        {
            // Values printed by Tools/audio/voice_common.line_key; if these drift, baked lines go silent.
            Assert.AreEqual("15466bc2", VoiceBank.LineKey(
                "Welcome to a rock that already has a landlord. Raise a Commons before the Freeholds invoice the crater."));
            Assert.AreEqual("815b811e", VoiceBank.LineKey("Tax collector killed on the road"));
        }

        [Test]
        public void LineKey_IgnoresSpeakerPrefixCaseAndPunctuation()
        {
            string line = GrokCatalog.Line(GrokBeat.FirstFlag);
            Assert.AreEqual(VoiceBank.LineKey(line), VoiceBank.LineKey(GrokCatalog.Say(GrokBeat.FirstFlag)));
            Assert.AreEqual(VoiceBank.LineKey("Nope. Nope. Inn!"), VoiceBank.LineKey("  nope nope — INN "));
            Assert.AreEqual("nope nope inn", VoiceBank.NormalizeLine("Grok — Nope... Nope, inn?"));
            Assert.AreNotEqual(VoiceBank.LineKey("Level 6"), VoiceBank.LineKey("Level 7"));
        }

        private const string Manifest = @"{""version"":1,""clips"":[
 {""speaker"":""hero.ScoutDrone"",""cue"":""hurt"",""take"":0,""text"":""Ow!"",""clip"":""a0"",""seconds"":0.5},
 {""speaker"":""hero.ScoutDrone"",""cue"":""hurt"",""take"":1,""text"":""Hey!"",""clip"":""a1"",""seconds"":0.5},
 {""speaker"":""hero.ScoutDrone"",""cue"":""hurt"",""take"":2,""text"":""Rude!"",""clip"":""a2"",""seconds"":0.5},
 {""speaker"":""hero.ScoutDrone"",""cue"":""levelup"",""take"":0,""text"":""Faster!"",""clip"":""b0"",""seconds"":0.7},
 {""speaker"":""overseer"",""cue"":""line"",""key"":""815b811e"",""text"":""Tax collector killed on the road"",""clip"":""c0"",""seconds"":2.1},
 {""speaker"":""overseer"",""cue"":""not_a_cue"",""clip"":""x""},
 {""cue"":""hurt"",""clip"":""no_speaker""}
]}";

        [Test]
        public void Bank_ParsesBarksAndLinesAndSkipsJunk()
        {
            var bank = VoiceBank.Parse(Manifest);
            Assert.AreEqual(5, bank.Count);
            Assert.AreEqual(1, bank.LineCount);
            Assert.IsTrue(bank.HasBark("hero.ScoutDrone", VoiceCue.Hurt));
            Assert.IsTrue(bank.HasBark(VoiceBank.SpeakerFor(SpecialistClass.ScoutDrone), VoiceCue.LevelUp));
            Assert.IsFalse(bank.HasBark("hero.ScoutDrone", VoiceCue.Flee));

            Assert.IsTrue(bank.TryGetLine("Tax collector killed on the road.", out var line));
            Assert.AreEqual("c0", line.Clip);
            Assert.IsFalse(bank.TryGetLine("Haul deposited 40 EU at Commons.", out _));

            Assert.AreEqual(0, VoiceBank.Parse("not json").Count);
            Assert.AreEqual(0, VoiceBank.Parse(null).Count);
        }

        [Test]
        public void Bank_NeverPicksTheSameTakeTwiceInARow()
        {
            var bank = VoiceBank.Parse(Manifest);
            var rng = new System.Random(3);
            string last = null;
            for (int i = 0; i < 200; i++)
            {
                Assert.IsTrue(bank.TryPickBark("hero.ScoutDrone", VoiceCue.Hurt, rng, out var info));
                Assert.AreNotEqual(last, info.Clip);
                last = info.Clip;
            }
            // A single take is still allowed to repeat.
            Assert.IsTrue(bank.TryPickBark("hero.ScoutDrone", VoiceCue.LevelUp, rng, out var a));
            Assert.IsTrue(bank.TryPickBark("hero.ScoutDrone", VoiceCue.LevelUp, rng, out var b));
            Assert.AreEqual(a.Clip, b.Clip);
        }

        [Test]
        public void CueKeys_RoundTrip()
        {
            foreach (VoiceCue cue in (VoiceCue[])Enum.GetValues(typeof(VoiceCue)))
            {
                Assert.IsTrue(VoiceBank.TryParseCue(VoiceBank.CueKey(cue), out VoiceCue back), cue.ToString());
                Assert.AreEqual(cue, back);
            }
            Assert.AreEqual("levelup", VoiceBank.CueKey(VoiceCue.LevelUp));
            Assert.AreEqual("alert_critical", VoiceBank.CueKey(VoiceCue.AlertCritical));
        }

        [Test]
        public void Director_SelectedHeroChattersOthersOnlyForBigMoments()
        {
            var d = new VoiceDirector();
            Assert.IsFalse(d.TryStartHero(1, VoiceCue.Wander, selected: false, now: 0f, seconds: 1f));
            Assert.IsTrue(d.TryStartHero(1, VoiceCue.Wander, selected: true, now: 0f, seconds: 1f));
            Assert.IsTrue(d.TryStartHero(2, VoiceCue.Claim, selected: false, now: 1f, seconds: 1f));
        }

        [Test]
        public void Director_LimitsOverlapAndSpacing()
        {
            var d = new VoiceDirector { MaxHeroVoices = 2, HeroGap = 0.5f };
            Assert.IsTrue(d.TryStartHero(1, VoiceCue.Claim, false, 0f, 3f));
            Assert.IsFalse(d.TryStartHero(2, VoiceCue.Claim, false, 0.1f, 3f), "gap between lines starting");
            Assert.IsTrue(d.TryStartHero(2, VoiceCue.Claim, false, 0.6f, 3f));
            Assert.IsFalse(d.TryStartHero(3, VoiceCue.Claim, false, 1.2f, 3f), "two voices already talking");
            Assert.IsTrue(d.TryStartHero(3, VoiceCue.Claim, false, 3.1f, 1f));
            Assert.IsFalse(d.TryStartHero(1, VoiceCue.Flee, false, 3.7f, 1f), "per-hero cooldown");
        }

        [Test]
        public void Director_HurtIsRateLimitedAcrossTheColony()
        {
            var d = new VoiceDirector { HeroGap = 0f };
            Assert.IsTrue(d.TryStartHero(1, VoiceCue.Hurt, false, 0f, 0.4f));
            Assert.IsFalse(d.TryStartHero(2, VoiceCue.Hurt, false, 1f, 0.4f));
            Assert.IsTrue(d.TryStartHero(2, VoiceCue.Hurt, false, d.HurtGap + 0.01f, 0.4f));
        }

        [Test]
        public void Director_OverseerOutranksHeroesButClicksAreAnswered()
        {
            var d = new VoiceDirector();
            Assert.IsTrue(d.TryStartOverseer(0f, 5f, priority: 1));
            Assert.IsTrue(d.OverseerSpeaking(2f));
            Assert.IsFalse(d.TryStartHero(1, VoiceCue.Claim, false, 2f, 1f));
            Assert.IsTrue(d.TryStartHero(1, VoiceCue.Select, true, 2f, 1f));
            Assert.IsFalse(d.TryStartHero(1, VoiceCue.Select, true, 2.2f, 1f), "double-click debounce");

            Assert.IsFalse(d.TryStartOverseer(3f, 2f, priority: 1), "equal priority waits");
            Assert.IsTrue(d.TryStartOverseer(3f, 2f, priority: 3), "critical interrupts a lesson");
            Assert.IsFalse(d.OverseerSpeaking(5.1f));
            Assert.IsTrue(d.TryStartHero(2, VoiceCue.Claim, false, 5.1f, 1f));
        }

        private static byte[] Wav16(short[] pcm, int channels, int rate)
        {
            var b = new List<byte>();
            void Tag(string t) => b.AddRange(System.Text.Encoding.ASCII.GetBytes(t));
            void U32(int v) => b.AddRange(BitConverter.GetBytes(v));
            void U16(int v) => b.AddRange(BitConverter.GetBytes((short)v));
            Tag("RIFF"); U32(36 + pcm.Length * 2); Tag("WAVE");
            Tag("fmt "); U32(16); U16(1); U16(channels); U32(rate); U32(rate * channels * 2); U16(channels * 2); U16(16);
            Tag("LIST"); U32(3); b.AddRange(new byte[] { 1, 2, 3 }); b.Add(0); // odd chunk + pad byte
            Tag("data"); U32(pcm.Length * 2);
            foreach (short s in pcm) b.AddRange(BitConverter.GetBytes(s));
            return b.ToArray();
        }

        [Test]
        public void WavDecoder_ReadsPcm16AndSkipsExtraChunks()
        {
            byte[] wav = Wav16(new short[] { 0, 16384, -32768, 32767 }, 1, 24000);
            Assert.IsTrue(WavDecoder.TryDecode(wav, out float[] s, out int ch, out int rate));
            Assert.AreEqual(1, ch);
            Assert.AreEqual(24000, rate);
            Assert.AreEqual(4, s.Length);
            Assert.AreEqual(0.5f, s[1], 1e-4f);
            Assert.AreEqual(-1f, s[2], 1e-4f);
        }

        [Test]
        public void WavDecoder_RejectsGarbage()
        {
            Assert.IsFalse(WavDecoder.TryDecode(null, out _, out _, out _));
            Assert.IsFalse(WavDecoder.TryDecode(new byte[64], out _, out _, out _));
            byte[] wav = Wav16(new short[] { 1, 2 }, 1, 24000);
            wav[22] = 6; // six channels
            Assert.IsFalse(WavDecoder.TryDecode(wav, out _, out _, out _));
        }

        // ---- the shipped bank ---------------------------------------------------------------

        private static VoiceBank Shipped()
        {
            var manifest = Resources.Load<TextAsset>("Audio/Voices/voices");
            Assert.IsNotNull(manifest, "Assets/Resources/Audio/Voices/voices.json missing: run Tools/audio/render_voices.py");
            return VoiceBank.Parse(manifest.text);
        }

        private static readonly VoiceCue[] HeroCues =
        {
            VoiceCue.Select, VoiceCue.Claim, VoiceCue.Refused, VoiceCue.Hunt, VoiceCue.Flee, VoiceCue.Repair,
            VoiceCue.Rest, VoiceCue.Wander, VoiceCue.LevelUp, VoiceCue.Hurt, VoiceCue.Down
        };

        [Test]
        public void ShippedBank_EveryClassHasEveryCue()
        {
            var bank = Shipped();
            foreach (SpecialistClass cls in (SpecialistClass[])Enum.GetValues(typeof(SpecialistClass)))
                foreach (VoiceCue cue in HeroCues)
                    Assert.IsTrue(bank.HasBark(VoiceBank.SpeakerFor(cls), cue), $"{cls} has no '{VoiceBank.CueKey(cue)}' bark");
        }

        [Test]
        public void ShippedBank_EveryScriptedGrokLineIsVoiced()
        {
            var bank = Shipped();
            var missing = new List<string>();
            foreach (GrokBeat beat in (GrokBeat[])Enum.GetValues(typeof(GrokBeat)))
            {
                string line = GrokCatalog.Line(beat);
                if (!string.IsNullOrEmpty(line) && !bank.TryGetLine(line, out _)) missing.Add(beat.ToString());
            }
            foreach (FieldInfo f in typeof(OverseerRules).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (!f.IsLiteral || f.FieldType != typeof(string) || !f.Name.StartsWith("Grok")) continue;
                if (!bank.TryGetLine((string)f.GetRawConstantValue(), out _)) missing.Add(f.Name);
            }
            Assert.IsEmpty(missing, "Unvoiced Grok lines (re-run Tools/audio/render_voices.py): " + string.Join(", ", missing));
        }

        [Test]
        public void ShippedBank_ClipsLoad()
        {
            var bank = Shipped();
            Assert.IsTrue(bank.TryPickBark(VoiceBank.SpeakerFor(SpecialistClass.EngineerBot), VoiceCue.Claim, null, out var bark));
            Assert.IsNotNull(Resources.Load<AudioClip>("Audio/Voices/" + bark.Clip), bark.Clip);
            Assert.IsTrue(bank.TryGetLine(GrokCatalog.Line(GrokBeat.Drop), out var line));
            Assert.IsNotNull(Resources.Load<AudioClip>("Audio/Voices/" + line.Clip), line.Clip);
        }
    }
}
