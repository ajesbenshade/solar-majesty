using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    public class PlayerFacingCanonTests
    {
        [Test]
        public void TitleWorldList_OnlyShowsEarthLunaMars([Values(true, false)] bool firstHourDemo)
        {
            bool prior = DemoSettings.FirstHourDemo;
            try
            {
                DemoSettings.FirstHourDemo = firstHourDemo;
                var shown = new List<CelestialBodyId>();
                foreach (CelestialBodyId id in Enum.GetValues(typeof(CelestialBodyId)))
                    if (DemoSlice.ShowBody(id)) shown.Add(id);
                CollectionAssert.AreEqual(
                    new[] { CelestialBodyId.Earth, CelestialBodyId.Luna, CelestialBodyId.Mars }, shown);
                // No "Continue to"/"Next:" offer past Mars.
                Assert.IsFalse(CampaignProgress.NextAfter(CelestialBodyId.Mars).HasValue);
            }
            finally
            {
                DemoSettings.FirstHourDemo = prior;
            }
        }

        private static readonly Regex Forbidden = new Regex(
            @"\b(overseer|overseeing|oversee|houses?|purses?|chest|" +
            // Retired kingdom / court voice and the old faction names.
            @"court|crown|reigns?|kingdom|compact|freeholds?|sol authority|tithe|plaza|party|conquest|conquer|" +
            // Retired building and crew words: charging stations, bots and the Fobot Yard replace them.
            @"habs?|beds|inn|heroes|hero lines|" +
            // Retired ship names: player text says Colony Lifter.
            @"lunar rocket|mars ship|belt hauler)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // The campaign ends at Mars. Belt and Europa stay catalog-only, so their names may only
        // appear in the files that define or persist those catalog bodies.
        private static readonly Regex OuterWorlds = new Regex(
            @"\b(belt|europa)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly HashSet<string> OuterWorldFiles = new HashSet<string>
        {
            "Systems/World/CelestialBodyCatalog.cs", // catalog-only body definitions
            "Systems/FlagDecreeIds.cs", // body ID tokens
            "Systems/SaveGame.cs", // save body names
            "Systems/DeveloperPrefsShield.cs", // dev prefs keys
            "Runtime/SolarSystemTitleView.cs" // unrendered orrery pick captions (bodies hidden by DemoSlice.ShowBody)
        };

        // Tokenize before looking for words: comments, character literals and diagnostic/editor
        // calls must not turn into text hits. Strings include regular, interpolated and verbatim forms.
        private static readonly Regex Tokens = new Regex(
            @"(?<comment>//[^\r\n]*|/\*[\s\S]*?\*/)|" +
            "(?<literal>\\$?@\"(?:\"\"|[^\"])*\"|@\\$\"(?:\"\"|[^\"])*\"|\\$?\"(?:\\\\[\\s\\S]|[^\"\\\\])*\")|" +
            @"(?<character>'(?:\\.|[^'\\])*')|" +
            @"(?<skip>(?:\b(?:UnityEngine\s*\.\s*)?Debug\s*\.\s*Log(?:Warning|Error|Exception|Assertion|Format|WarningFormat|ErrorFormat)?|\[\s*(?:UnityEngine\s*\.\s*)?Tooltip(?:Attribute)?)\s*\()|" +
            @"(?<paren>[()])");

        // File + literal snippet, never line numbers. Each exception is deliberately narrow.
        private static readonly KeyValuePair<string, string>[] Allowed =
        {
            Pair("Runtime/SpecialistAgent.cs", "purse="), // diagnostic state dump
            Pair("Runtime/SuitCrossingWalker.cs", "B-chest"), // bone ID
            Pair("Systems/Voice/VoiceBank.cs", "\"overseer\""), // speaker constant and FX switch
            Pair("Systems/CompactJex.cs", "Pests drained {amount} EU from {n} {purses}."), // interpolation variable
            Pair("Systems/StillCampusDensity.cs", "hab={Hab}"), // diagnostic dump
            Pair("Systems/StillCampusDensity.cs", "inn={Inn}"), // diagnostic dump
            Pair("Systems/StillCampusDensity.cs", "workshop+inn"), // layout key
            Pair("Systems/StillCampusDensity.cs", "\"inn+wonder\""), // layout key
            Pair("Systems/StillCampusDensity.cs", "\"inn\""), // layout key
            Pair("Systems/SpecialistFlavor.cs", "\"party\""), // reason ID
            Pair("Runtime/SpecialistAgent.cs", "\"inn\""), // reason ID
            Pair("Runtime/HeroBuildingDetail.cs", "\"Hab\""), // art key
            Pair("Runtime/GameLoop.cs", "\"inn\""), // asset name token
            Pair("Runtime/GameLoop.cs", "\"hab\""), // asset name token
            Pair("Runtime/GameLoop.cs", "\"HAB\""), // asset path check
            Pair("Runtime/GameLoop.cs", "{party.Count}"), // interpolation variable
            Pair("Runtime/OverseerHud.cs", "\"BEDS\""), // legacy chip label match
            Pair("Runtime/OverseerHud.cs", "{beds}"), // interpolation variable
            Pair("Editor/BuildCommands.cs", "THIRD-PARTY-NOTICES"), // file name
            Pair("Systems/Narration/HeroConversation.cs", "never \\\"Overseer\\\""), // instruction to avoid the name
            Pair("Assets/Resources/Audio/Voices/voices.json", "\"speaker\":\"overseer\"") // metadata only; text is checked separately
        };

        private static KeyValuePair<string, string> Pair(string file, string snippet) =>
            new KeyValuePair<string, string>(file, snippet);

        [Test]
        public void PlayerFacingStrings_UseCoreChargersBatteriesAndTill()
        {
            var failures = new List<string>();
            string scripts = Path.Combine(Application.dataPath, "Scripts");
            foreach (string file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
            {
                string relative = file.Substring(scripts.Length + 1).Replace('\\', '/');
                // MajestyAnalog.cs is dev-only: only EditMode assertions consume its catalog; no player-facing callers.
                if (relative.StartsWith("Tests/", StringComparison.Ordinal) || relative == "Systems/MajestyAnalog.cs")
                    continue; // Tests and the internal comparison table are not player copy.

                // Scan a conservative superset of player copy. This covers indirect constants,
                // return values and data fields as well as HUD/Log.Push/tooltip/bark call sites.
                ScanCSharp(relative, File.ReadAllText(file), failures);
            }

            const string voiceFile = "Assets/Resources/Audio/Voices/voices.json";
            var root = (Dictionary<string, object>)LocalJson.Parse(
                File.ReadAllText(Path.Combine(Application.dataPath, "Resources/Audio/Voices/voices.json")));
            Assert.IsTrue(root.ContainsKey("clips"), "Voice bank must contain clips to scan.");
            foreach (object entry in (List<object>)root["clips"])
            {
                var clip = (Dictionary<string, object>)entry;
                // Cast, speaker, clip, cue and key are IDs. Only text is player-facing.
                if (clip.TryGetValue("text", out object value) && value is string line)
                    Check(voiceFile, line, failures);
            }
            Assert.That(failures, Is.Empty, "Non-canon player text:\n" + string.Join("\n", failures));
        }

        private static void ScanCSharp(string file, string source, List<string> failures)
        {
            int skippedDepth = 0;
            foreach (Match token in Tokens.Matches(source))
            {
                if (token.Groups["skip"].Success) { skippedDepth++; continue; }
                if (skippedDepth > 0)
                {
                    if (token.Groups["paren"].Success)
                        skippedDepth += token.Value == "(" ? 1 : -1;
                    continue;
                }
                if (token.Groups["literal"].Success)
                    Check(file, token.Value, failures);
            }
        }

        private static void Check(string file, string literal, List<string> failures)
        {
            if (OuterWorlds.IsMatch(literal) && !OuterWorldFiles.Contains(file))
            {
                failures.Add(file + ": " + literal);
                return;
            }
            if (!Forbidden.IsMatch(literal)) return;
            foreach (var allowed in Allowed)
                if (file == allowed.Key && literal.Contains(allowed.Value)) return;
            failures.Add(file + ": " + literal);
        }

        [Test]
        public void Scanner_IgnoresCommentsDiagnosticsAndInspectorTooltips()
        {
            var failures = new List<string>();
            ScanCSharp("sample.cs", "// house\n/* purse */\n[Tooltip(\"houses\")] " +
                "Debug.Log(Format(\"overseer\")); Debug.LogWarning(\"chest\"); " +
                "Log.Push(\"house\"); string bark = @\"purses\"; " +
                "string hint = $\"chest {amount}\"; Tooltip(\"house\");", failures);
            Assert.AreEqual(4, failures.Count);
        }

        [Test]
        public void Scanner_CoversMultilineCopyAndDoesNotAllowOtherFiles()
        {
            var failures = new List<string>();
            ScanCSharp("sample.cs", "string text = @\"First line\nOverseer\"; " +
                "Log.Push(\"Guard post and levy chest.\");", failures);
            Check("other.cs", "B-chest", failures);
            Check("Systems/CompactJex.cs", "A purse is full.", failures);
            Assert.AreEqual(4, failures.Count);
        }

        [Test]
        public void Scanner_CatchesRetiredVoiceWithWordBoundaries()
        {
            var failures = new List<string>();
            ScanCSharp("sample.cs", "a = \"The court does not curtsy.\"; b = \"Raise the first HAB.\"; " +
                "c = \"Heroes buy with EU.\"; d = \"Stage the Lunar Rocket.\"; e = \"Europa is next.\"; " +
                "f = \"Back to the inn!\"; g = \"Keep overseeing.\"; h = \"The Compact charters.\";", failures);
            Assert.AreEqual(8, failures.Count, string.Join("\n", failures));

            failures.Clear();
            // Word boundaries: longer words that merely contain a banned stem stay legal.
            ScanCSharp("sample.cs", "a = \"Inner dens\"; b = \"Courtyard\"; c = \"Habitat\"; " +
                "d = \"Hero\"; e = \"Beltway\"; f = \"Compactor\"; g = \"ACH_SOLAR_CONQUEST\";", failures);
            Assert.AreEqual(0, failures.Count, string.Join("\n", failures));

            failures.Clear();
            Check("Systems/World/CelestialBodyCatalog.cs", "\"Europa\"", failures);
            Check("Systems/AdvisorToastCatalog.cs", "\"Europa\"", failures);
            Assert.AreEqual(1, failures.Count);
        }
    }
}
