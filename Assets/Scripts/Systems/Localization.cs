using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// String table lookup.
    ///
    /// There are roughly 2,360 English string literals in the codebase and no way to translate any
    /// of them. Externalising all of them at once is not realistic, so this provides the lookup and
    /// the fallback: <c>Loc.T("key", "English default")</c> returns the default until a translation
    /// exists, which means call sites can migrate one at a time without breaking anything.
    ///
    /// Tables are CSV in Resources/Locale/&lt;code&gt;.csv as <c>key,translated text</c>.
    /// </summary>
    public static class Loc
    {
        public const string DefaultLanguage = "en";
        private const string PrefsKey = "SM_Language";
        private const string ResourceRoot = "Locale/";

        private static readonly Dictionary<string, string> Table = new Dictionary<string, string>(256);
        private static readonly HashSet<string> Missing = new HashSet<string>();

        public static string Language { get; private set; } = DefaultLanguage;

        /// <summary>Languages with a table shipped. English is always present as the source.</summary>
        public static readonly string[] Available = { "en" };

        public static void Load()
        {
            string saved = PlayerPrefs.GetString(PrefsKey, DefaultLanguage);
            SetLanguage(saved);
        }

        public static void SetLanguage(string code)
        {
            if (string.IsNullOrEmpty(code)) code = DefaultLanguage;

            Language = code;
            Table.Clear();
            Missing.Clear();

            PlayerPrefs.SetString(PrefsKey, code);
            PlayerPrefs.Save();

            if (code == DefaultLanguage) return;   // English is the source; no table needed.

            var asset = Resources.Load<TextAsset>(ResourceRoot + code);
            if (asset == null)
            {
                Debug.LogWarning($"[Loc] No table for '{code}'; falling back to English.");
                return;
            }

            Parse(asset.text);
        }

        /// <summary>
        /// Translate. The English text is passed in as the fallback so an untranslated or
        /// mistyped key degrades to readable English rather than showing the key itself.
        /// </summary>
        public static string T(string key, string fallback)
        {
            if (string.IsNullOrEmpty(key)) return fallback;
            if (Table.TryGetValue(key, out string value) && !string.IsNullOrEmpty(value))
                return value;

            if (Language != DefaultLanguage)
                Missing.Add(key);
            return fallback;
        }

        /// <summary>Keys requested but not translated. Feeds the extraction report.</summary>
        public static IReadOnlyCollection<string> MissingKeys => Missing;

        public static int Count => Table.Count;

        /// <summary>
        /// Minimal CSV: one key,value pair per line. Values may contain commas, so only the first
        /// comma splits. A leading '#' is a comment.
        /// </summary>
        private static void Parse(string csv)
        {
            if (string.IsNullOrEmpty(csv)) return;

            string[] lines = csv.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim('\r', ' ', '\t');
                if (line.Length == 0 || line[0] == '#') continue;

                int split = line.IndexOf(',');
                if (split <= 0 || split >= line.Length - 1) continue;

                string key = line.Substring(0, split).Trim();
                string value = line.Substring(split + 1).Trim();
                if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
                    value = value.Substring(1, value.Length - 2);

                if (key.Length > 0)
                    Table[key] = value.Replace("\\n", "\n");
            }
        }
    }
}
