using System;
using System.IO;
using NUnit.Framework;

namespace SolarMajesty.Tests
{
    public class BuildVersionTests
    {
        private const string Sha = "c7014870d2a1b3c4d5e6f708192a3b4c5d6e7f80";

        [Test]
        public void Resolve_TagPushBecomesThePlainVersion()
        {
            Assert.AreEqual("1.2.3", BuildVersion.Resolve(null, "refs/tags/v1.2.3", Sha));
            Assert.AreEqual("1.2.3", BuildVersion.Resolve("", "v1.2.3", Sha));
            Assert.AreEqual("0.9.0-rc.1", BuildVersion.Resolve(null, "v0.9.0-rc.1", Sha));
        }

        [Test]
        public void Resolve_UntaggedBuildIsDevPlusShortSha()
        {
            Assert.AreEqual("0.0.0-dev+c701487", BuildVersion.Resolve(null, "refs/heads/main", Sha));
            Assert.AreEqual("0.0.0-dev+c701487", BuildVersion.Resolve(null, null, Sha));
            Assert.AreEqual("0.0.0-dev", BuildVersion.Resolve(null, null, null));
        }

        [Test]
        public void Resolve_ExplicitRequestWinsAndLosesItsV()
        {
            Assert.AreEqual("2.0.0", BuildVersion.Resolve("v2.0.0", "v1.2.3", Sha));
            Assert.AreEqual("0.0.0-dev+abc1234", BuildVersion.Resolve("0.0.0-dev+abc1234", null, Sha));
        }

        [Test]
        public void Resolve_GameCiNoneIsTreatedAsNoRequest()
        {
            Assert.AreEqual("1.2.3", BuildVersion.Resolve("none", "v1.2.3", Sha));
            Assert.AreEqual("0.0.0-dev+c701487", BuildVersion.Resolve("NONE", "", Sha));
        }

        [Test]
        public void TryParseTag_RejectsNonVersions()
        {
            Assert.IsFalse(BuildVersion.TryParseTag("main", out _));
            Assert.IsFalse(BuildVersion.TryParseTag("v1.2", out _));
            Assert.IsFalse(BuildVersion.TryParseTag("v1.2.3.4", out _));
            Assert.IsFalse(BuildVersion.TryParseTag("v1.2.3 beta", out _));
            Assert.IsFalse(BuildVersion.TryParseTag("v1..3", out _));
            Assert.IsFalse(BuildVersion.TryParseTag("release-1.2.3", out _));
            Assert.IsTrue(BuildVersion.TryParseTag("V10.20.30+build.7", out string v));
            Assert.AreEqual("10.20.30+build.7", v);
        }

        [Test]
        public void ShortSha_OnlyAcceptsHex()
        {
            Assert.AreEqual("c701487", BuildVersion.ShortSha(Sha));
            Assert.AreEqual("abc", BuildVersion.ShortSha("ABC"));
            Assert.AreEqual("", BuildVersion.ShortSha("not-a-sha"));
            Assert.AreEqual("", BuildVersion.ShortSha("  "));
        }

        [Test]
        public void NumericCoreAndBuildCode_IgnoreSuffixes()
        {
            Assert.AreEqual("1.2.3", BuildVersion.NumericCore("1.2.3-rc.1+abc"));
            Assert.AreEqual("0.0.0", BuildVersion.NumericCore("0.0.0-dev+c701487"));
            Assert.AreEqual("0.0.0", BuildVersion.NumericCore("garbage"));
            Assert.AreEqual(1002003, BuildVersion.BuildCode("1.2.3"));
            Assert.AreEqual(0, BuildVersion.BuildCode("0.0.0-dev"));
            Assert.Less(BuildVersion.BuildCode("1.9.9"), BuildVersion.BuildCode("1.10.0"));
        }

        [Test]
        public void StreamingAssetsDir_MatchesUnityPlayerLayouts()
        {
            string win = BuildVersion.StreamingAssetsDir(Path.Combine("out", "SolarMajesty.exe"), false);
            Assert.AreEqual(Path.Combine("out", "SolarMajesty_Data", "StreamingAssets"), win);

            string linux = BuildVersion.StreamingAssetsDir(Path.Combine("out", "SolarMajesty.x86_64"), false);
            Assert.AreEqual(Path.Combine("out", "SolarMajesty_Data", "StreamingAssets"), linux);

            string mac = BuildVersion.StreamingAssetsDir(Path.Combine("out", "SolarMajesty.app"), true);
            Assert.AreEqual(Path.Combine("out", "SolarMajesty.app", "Contents", "Resources", "Data", "StreamingAssets"), mac);

            Assert.IsNull(BuildVersion.StreamingAssetsDir(null, false));
        }

        [Test]
        public void Json_RoundTripsFieldsIncludingEscapes()
        {
            string json = BuildVersion.ToJson("1.2.3", Sha, "Std\"Win\\64", new DateTime(2026, 10, 11, 8, 30, 0, DateTimeKind.Utc));

            Assert.AreEqual("1.2.3", BuildVersion.ReadField(json, "version"));
            Assert.AreEqual(Sha, BuildVersion.ReadField(json, "commit"));
            Assert.AreEqual("Std\"Win\\64", BuildVersion.ReadField(json, "platform"));
            Assert.AreEqual("2026-10-11T08:30:00Z", BuildVersion.ReadField(json, "builtUtc"));
            Assert.IsNull(BuildVersion.ReadField(json, "missing"));
        }

        [Test]
        public void Label_ShowsTheCommitOnlyWhenTheVersionHidesIt()
        {
            Assert.AreEqual("v1.2.3", BuildVersion.Label("1.2.3", Sha));
            Assert.AreEqual("v0.0.0-dev+c701487", BuildVersion.Label("0.0.0-dev+c701487", Sha));
            Assert.AreEqual("v1.0.0-rc.1 (c701487)", BuildVersion.Label("1.0.0-rc.1", Sha));
        }

        [Test]
        public void LoadLabel_ReadsTheStampAndFallsBackWithoutOne()
        {
            string dir = Path.Combine(Path.GetTempPath(), "sm-buildinfo-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                Assert.AreEqual("v1.0", BuildVersion.LoadLabel(dir, "1.0"));

                File.WriteAllText(Path.Combine(dir, BuildVersion.FileName),
                    BuildVersion.ToJson("0.4.0", Sha, "StandaloneLinux64", DateTime.UtcNow));
                Assert.AreEqual("v0.4.0", BuildVersion.LoadLabel(dir, "1.0"));

                Assert.AreEqual("v0.0.0-dev", BuildVersion.LoadLabel(null, null));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }
    }
}
