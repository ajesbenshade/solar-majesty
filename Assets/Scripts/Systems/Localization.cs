using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// String table lookup.
    ///
    /// Source-keyed (gettext style): the English text is the key, so call sites stay readable and
    /// translators work from the real sentence. <c>Loc.S("Settings")</c> for plain text,
    /// <c>Loc.F("{0} EU a day", tax)</c> for text with values. An untranslated string degrades to
    /// English, never to a key. <c>Loc.T(key, fallback)</c> remains for the few ids that are not
    /// sentences.
    ///
    /// Tables are JSON in <c>Resources/Locale/&lt;code&gt;.json</c>, generated and kept in sync by
    /// <c>Tools/loc/extract.py</c>, which collects every S/F/T literal from the code. Drop a new
    /// file there and the language appears in Settings. The pseudo-language <c>qps</c> (dev tools)
    /// accents and pads every wrapped string, so anything still plain English on screen is not
    /// wrapped yet, and anything clipped will not fit a longer language.
    /// </summary>
    public static class Loc
    {
        public const string DefaultLanguage = "en";
        public const string PseudoLanguage = "qps";
        private const string PrefsKey = "SM_Language";
        private const string ResourceRoot = "Locale/";

        [Serializable]
        private sealed class Entry
        {
            public string en;   // source text (S/F), or empty for keyed entries
            public string key;  // T() id, or empty for source entries
            public string tr;   // translation; empty means untranslated
        }

        [Serializable]
        private sealed class TableFile
        {
            public string code;
            public string name;     // the language's own name, shown in Settings
            public List<Entry> strings = new List<Entry>();
        }

        public readonly struct LanguageInfo
        {
            public readonly string Code;
            public readonly string Name;
            public LanguageInfo(string code, string name) { Code = code; Name = name; }
        }

        private static readonly Dictionary<string, string> BySource = new Dictionary<string, string>(1024, StringComparer.Ordinal);
        private static readonly Dictionary<string, string> ByKey = new Dictionary<string, string>(64, StringComparer.Ordinal);
        private static readonly HashSet<string> Missing = new HashSet<string>(StringComparer.Ordinal);
        private static List<LanguageInfo> _available;
        private static bool _loaded;

        public static string Language { get; private set; } = DefaultLanguage;

        public static bool IsPseudo => Language == PseudoLanguage;

        /// <summary>Raised after the language changes, so UI can rebuild fonts and cached text.</summary>
        public static event Action Changed;

        /// <summary>English, every shipped table, and the pseudo-language when dev tools are on.</summary>
        public static IReadOnlyList<LanguageInfo> Available
        {
            get
            {
                if (_available != null) return _available;
                _available = new List<LanguageInfo> { new LanguageInfo(DefaultLanguage, "English") };
                foreach (var asset in Resources.LoadAll<TextAsset>(ResourceRoot.TrimEnd('/')))
                {
                    if (asset == null || asset.name == DefaultLanguage) continue;
                    var file = ParseFile(asset.text);
                    if (file == null || string.IsNullOrEmpty(file.code)) continue;
                    _available.Add(new LanguageInfo(file.code, string.IsNullOrEmpty(file.name) ? file.code : file.name));
                }
                if (DevTools.Enabled) _available.Add(new LanguageInfo(PseudoLanguage, "Pseudo (QA)"));
                return _available;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _available = null;
            _loaded = false;
            Changed = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void LoadOnBoot() => Load();

        /// <summary>Apply the saved language (or <c>-language xx</c>). Safe to call more than once.</summary>
        public static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            string arg = ArgValue("-language");
            Apply(!string.IsNullOrEmpty(arg) ? arg : GamePrefs.GetString(PrefsKey, DefaultLanguage));
        }

        public static void SetLanguage(string code)
        {
            _loaded = true;
            Apply(code);
            GamePrefs.SetString(PrefsKey, Language);
            GamePrefs.Save();
        }

        private static void Apply(string code)
        {
            if (string.IsNullOrEmpty(code)) code = DefaultLanguage;
            Language = code;
            BySource.Clear();
            ByKey.Clear();
            Missing.Clear();

            if (code != DefaultLanguage && code != PseudoLanguage)
            {
                var asset = Resources.Load<TextAsset>(ResourceRoot + code);
                var file = asset != null ? ParseFile(asset.text) : null;
                if (file == null)
                {
                    Debug.LogWarning($"[Loc] No table for '{code}'; falling back to English.");
                    Language = DefaultLanguage;
                }
                else
                {
                    foreach (var e in file.strings)
                    {
                        if (e == null || string.IsNullOrEmpty(e.tr)) continue;
                        if (!string.IsNullOrEmpty(e.en)) BySource[e.en] = e.tr;
                        else if (!string.IsNullOrEmpty(e.key)) ByKey[e.key] = e.tr;
                    }
                }
            }
            Changed?.Invoke();
        }

        /// <summary>Translate a plain English string.</summary>
        public static string S(string english)
        {
            if (string.IsNullOrEmpty(english) || Language == DefaultLanguage) return english;
            if (IsPseudo) return Pseudo(english);
            if (BySource.TryGetValue(english, out string tr)) return tr;
            Missing.Add(english);
            return english;
        }

        /// <summary>
        /// Translate an English format string, then fill it: <c>Loc.F("{0} EU a day", tax)</c>.
        /// Numbers format in the invariant culture so a translation cannot change how values read.
        /// A translation with broken placeholders falls back to English rather than throwing.
        /// </summary>
        public static string F(string englishFormat, params object[] args)
        {
            string format = S(englishFormat);
            try
            {
                return string.Format(CultureInfo.InvariantCulture, format, args);
            }
            catch (FormatException)
            {
                Debug.LogWarning($"[Loc] Bad placeholders in the {Language} text for \"{englishFormat}\".");
                return string.Format(CultureInfo.InvariantCulture, englishFormat, args);
            }
        }

        /// <summary>
        /// Translate by id. The English text is passed in as the fallback so an untranslated or
        /// mistyped key degrades to readable English rather than showing the key itself.
        /// </summary>
        public static string T(string key, string fallback)
        {
            if (string.IsNullOrEmpty(key)) return fallback;
            if (Language == DefaultLanguage) return fallback;
            if (IsPseudo) return Pseudo(fallback);
            if (ByKey.TryGetValue(key, out string value) && !string.IsNullOrEmpty(value))
                return value;
            Missing.Add(key);
            return fallback;
        }

        /// <summary>Strings asked for but not translated in the current language, for the QA report.</summary>
        public static IReadOnlyCollection<string> MissingKeys => Missing;

        public static int Count => BySource.Count + ByKey.Count;

        /// <summary>The display name of a language code, in that language.</summary>
        public static string NameOf(string code)
        {
            foreach (var l in Available)
                if (l.Code == code) return l.Name;
            return code;
        }

        // ---- pseudo-localization ---------------------------------------------------------------------

        private const string Plain = "abcdeghiklnorstuwyzABCDEGHIKLNORSTUWYZ";
        private const string Accented = "àƀçđéĝĥîķĺñöŕšţûŵýžÀƁÇĐÉĜĤÎĶĹÑÖŔŠŢÛŴÝŽ";

        /// <summary>
        /// "Settings" → "[Šéţţîñĝš·····]": accented so wrapped text is obvious, ~35 % longer so
        /// layouts that only just fit English show up clipped. Placeholders ({0}, {key:X}) are kept.
        /// </summary>
        public static string Pseudo(string english)
        {
            if (string.IsNullOrEmpty(english)) return english;
            var sb = new StringBuilder(english.Length * 2 + 4);
            sb.Append('[');
            int depth = 0;
            int letters = 0;
            foreach (char c in english)
            {
                if (c == '{') depth++;
                if (depth == 0)
                {
                    int i = Plain.IndexOf(c);
                    sb.Append(i >= 0 ? Accented[i] : c);
                    if (char.IsLetter(c)) letters++;
                }
                else sb.Append(c);
                if (c == '}' && depth > 0) depth--;
            }
            int pad = Mathf.Max(1, Mathf.CeilToInt(letters * 0.35f));
            sb.Append(new string('·', pad)).Append(']');
            return sb.ToString();
        }

        // ---- files -------------------------------------------------------------------------------------

        private static TableFile ParseFile(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                var file = JsonUtility.FromJson<TableFile>(json);
                return file != null && file.strings != null ? file : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Loc] Unreadable table: " + e.Message);
                return null;
            }
        }

        private static string ArgValue(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }
    }
}
