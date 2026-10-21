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
        private static readonly Regex Forbidden = new Regex(
            @"\b(overseer|houses?|purses?|chest)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

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
            Pair("Systems/CompactGrok.cs", "Pests drained {amount} EU from {n} {purses}."), // interpolation variable
            Pair("Systems/Narration/HeroConversation.cs", "never \\\"Overseer\\\""), // instruction to avoid the name
            Pair("Systems/OverseerRules.cs", "Haul walked the batteries home. EU is in the chest. Do not click the crate."), // separate PR
            Pair("Assets/Resources/Audio/Voices/voices.json", "Haul walked the batteries home. EU is in the chest. Do not click the crate."), // same deferred line
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
            Check("Systems/CompactGrok.cs", "A purse is full.", failures);
            Assert.AreEqual(4, failures.Count);
        }
    }
}
