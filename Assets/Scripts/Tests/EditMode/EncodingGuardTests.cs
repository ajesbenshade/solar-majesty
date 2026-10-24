using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// Guards against double-encoded text. UTF-8 bytes read as a legacy code page and saved again
    /// turn an em dash into three junk characters and a middle dot into two (the Jex rename once
    /// did this). The junk characters are written as escapes here so this file stays clean itself.
    /// </summary>
    public class EncodingGuardTests
    {
        // Greek capital gamma, box-drawing top-right/vertical-right/double-up-and-right pieces, C-cedilla:
        // the lead characters of a UTF-8 dash, middle dot or accented letter read as CP437.
        private static readonly Regex Mojibake = new Regex("[\u0393\u252C\u251C\u2556\u00C7]");

        private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        private static void Scan(string path, List<string> hits)
        {
            string[] lines = File.ReadAllText(path, new System.Text.UTF8Encoding(false)).Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (!Mojibake.IsMatch(lines[i])) continue;
                string rel = path.Substring(ProjectRoot.Length + 1).Replace('\\', '/');
                hits.Add(rel + ":" + (i + 1));
                if (hits.Count >= 40) return;
            }
        }

        [Test]
        public void ScriptsAndVoiceJson_ContainNoDoubleEncodedText()
        {
            var hits = new List<string>();
            int scanned = 0;
            foreach (string file in Directory.GetFiles(Path.Combine(Application.dataPath, "Scripts"), "*.cs", SearchOption.AllDirectories))
            {
                scanned++;
                Scan(file, hits);
            }
            Assert.Greater(scanned, 100, "Assets/Scripts was not found or is nearly empty");

            string[] json =
            {
                Path.Combine(Application.dataPath, "Resources", "Audio", "Voices", "voices.json"),
                Path.Combine(ProjectRoot, "Tools", "audio", "cast.json"),
                Path.Combine(ProjectRoot, "Tools", "audio", "voice_lines.json"),
            };
            int jsonScanned = 0;
            foreach (string file in json)
            {
                if (!File.Exists(file)) continue;
                jsonScanned++;
                Scan(file, hits);
            }
            Assert.GreaterOrEqual(jsonScanned, 1, "No voice json found to scan");

            Assert.IsEmpty(hits, "Double-encoded characters (UTF-8 read as CP437) at: " + string.Join(", ", hits));
        }

        [Test]
        public void JexPrefix_IsNameEmDashSpace()
        {
            string dash = "\u2014";
            Assert.AreEqual("Jex", JexCatalog.Speaker);
            Assert.AreEqual("Jex " + dash + " hello", JexCatalog.Format("hello"));
            Assert.IsTrue(JexCatalog.Say(JexBeat.FirstFlag).StartsWith("Jex " + dash + " ", StringComparison.Ordinal));
            Assert.AreEqual(4, JexCatalog.Format("x").IndexOf(dash, StringComparison.Ordinal), "Jex, space, then one dash character");
            // VoiceBank has to strip exactly that prefix, or baked clips go silent.
            Assert.AreEqual("nope nope", VoiceBank.NormalizeLine("Jex " + dash + " Nope. Nope."));
            Assert.AreEqual(VoiceBank.LineKey(JexCatalog.Line(JexBeat.FirstFlag)), VoiceBank.LineKey(JexCatalog.Say(JexBeat.FirstFlag)));
        }
    }
}