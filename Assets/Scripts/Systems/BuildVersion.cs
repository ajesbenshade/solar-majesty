// Build version and build-info stamp — pure C#, no MonoBehaviour.

using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace SolarMajesty
{
    /// <summary>
    /// One rule for what version a build carries, shared by the editor build commands and the game.
    /// A tagged release (<c>v1.2.3</c>) is <c>1.2.3</c>; anything else is
    /// <c>0.0.0-dev+&lt;shortsha&gt;</c>, so a playtest zip can always be traced to its commit.
    /// The build writes <see cref="FileName"/> into StreamingAssets; <see cref="LoadLabel"/> reads
    /// it back for a title-screen or pause-menu corner.
    /// </summary>
    public static class BuildVersion
    {
        public const string FileName = "build-info.json";
        public const string DevCore = "0.0.0";

        /// <summary>
        /// Picks the version: an explicit request wins (command line or SM_VERSION), then a
        /// <c>vX.Y.Z</c> tag or ref, then the dev fallback built from the commit.
        /// </summary>
        public static string Resolve(string requested, string tagOrRef, string commit)
        {
            if (!string.IsNullOrWhiteSpace(requested))
            {
                string r = requested.Trim();
                // game-ci passes "none" when versioning is off; treat it as no request.
                if (!string.Equals(r, "none", StringComparison.OrdinalIgnoreCase))
                    return TryParseTag(r, out string fromRequest) ? fromRequest : r;
            }

            if (TryParseTag(tagOrRef, out string fromTag))
                return fromTag;

            return Dev(commit);
        }

        /// <summary><c>0.0.0-dev+abc1234</c>, or <c>0.0.0-dev</c> when the commit is unknown.</summary>
        public static string Dev(string commit)
        {
            string sha = ShortSha(commit);
            return sha.Length == 0 ? DevCore + "-dev" : DevCore + "-dev+" + sha;
        }

        /// <summary>First seven hex characters of a commit id; empty when it is not one.</summary>
        public static string ShortSha(string commit)
        {
            if (string.IsNullOrWhiteSpace(commit)) return "";
            string c = commit.Trim().ToLowerInvariant();
            foreach (char ch in c)
            {
                bool hex = (ch >= '0' && ch <= '9') || (ch >= 'a' && ch <= 'f');
                if (!hex) return "";
            }
            return c.Length > 7 ? c.Substring(0, 7) : c;
        }

        /// <summary>
        /// Accepts <c>v1.2.3</c>, <c>1.2.3</c>, <c>refs/tags/v1.2.3</c> and pre-release / build
        /// suffixes (<c>v1.2.3-rc.1+meta</c>). Returns the version without the leading v.
        /// </summary>
        public static bool TryParseTag(string tag, out string version)
        {
            version = null;
            if (string.IsNullOrWhiteSpace(tag)) return false;

            string t = tag.Trim();
            const string refsTags = "refs/tags/";
            if (t.StartsWith(refsTags, StringComparison.Ordinal)) t = t.Substring(refsTags.Length);
            if (t.Length > 0 && (t[0] == 'v' || t[0] == 'V')) t = t.Substring(1);

            if (!TryParseCore(t, out _, out _, out _)) return false;

            // Suffix may only use semver's identifier characters.
            for (int i = CoreLength(t); i < t.Length; i++)
            {
                char ch = t[i];
                bool ok = char.IsLetterOrDigit(ch) || ch == '.' || ch == '-' || ch == '+';
                if (!ok || ch > 127) return false;
            }

            version = t;
            return true;
        }

        /// <summary><c>1.2.3</c> from <c>1.2.3-rc.1+abc</c>; <see cref="DevCore"/> if unparseable.</summary>
        public static string NumericCore(string version)
        {
            return TryParseCore(version, out int major, out int minor, out int patch)
                ? major + "." + minor + "." + patch
                : DevCore;
        }

        /// <summary>
        /// Monotonic integer for platforms that want one: <c>major*1000000 + minor*1000 + patch</c>
        /// (the scheme game-ci uses for Android). Zero if unparseable.
        /// </summary>
        public static int BuildCode(string version)
        {
            if (!TryParseCore(version, out int major, out int minor, out int patch)) return 0;
            long code = major * 1000000L + Math.Min(minor, 999) * 1000L + Math.Min(patch, 999);
            return code > int.MaxValue ? int.MaxValue : (int)code;
        }

        /// <summary>
        /// Where the build-info file goes inside a built player. Windows / Linux:
        /// <c>&lt;name&gt;_Data/StreamingAssets</c> beside the executable; macOS:
        /// <c>&lt;name&gt;.app/Contents/Resources/Data/StreamingAssets</c>.
        /// </summary>
        public static string StreamingAssetsDir(string playerPath, bool macAppBundle)
        {
            if (string.IsNullOrEmpty(playerPath)) return null;
            if (macAppBundle)
                return Path.Combine(playerPath, "Contents", "Resources", "Data", "StreamingAssets");

            string dir = Path.GetDirectoryName(playerPath) ?? "";
            string name = Path.GetFileNameWithoutExtension(playerPath);
            return Path.Combine(dir, name + "_Data", "StreamingAssets");
        }

        /// <summary>Small flat JSON object; hand-written so it needs no serializer.</summary>
        public static string ToJson(string version, string commit, string platform, DateTime builtUtc)
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            Field(sb, "version", version, false);
            Field(sb, "commit", commit ?? "", false);
            Field(sb, "platform", platform ?? "", false);
            Field(sb, "builtUtc", builtUtc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture), true);
            sb.Append("}\n");
            return sb.ToString();
        }

        /// <summary>Reads one string field back out of <see cref="ToJson"/> output.</summary>
        public static string ReadField(string json, string field)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(field)) return null;
            string key = "\"" + field + "\"";
            int k = json.IndexOf(key, StringComparison.Ordinal);
            if (k < 0) return null;
            int colon = json.IndexOf(':', k + key.Length);
            if (colon < 0) return null;
            int open = json.IndexOf('"', colon + 1);
            if (open < 0) return null;

            var sb = new StringBuilder();
            for (int i = open + 1; i < json.Length; i++)
            {
                char ch = json[i];
                if (ch == '"') return sb.ToString();
                if (ch == '\\' && i + 1 < json.Length)
                {
                    char next = json[++i];
                    sb.Append(next == 'n' ? '\n' : next == 't' ? '\t' : next);
                    continue;
                }
                sb.Append(ch);
            }
            return null;
        }

        /// <summary><c>v1.2.3</c>, or <c>v0.0.0-dev (abc1234)</c> when the version lacks the commit.</summary>
        public static string Label(string version, string commit)
        {
            string v = string.IsNullOrWhiteSpace(version) ? Dev(commit) : version.Trim();
            string sha = ShortSha(commit);
            bool showSha = sha.Length > 0 && v.IndexOf(sha, StringComparison.Ordinal) < 0 && v.Contains("-");
            return "v" + v + (showSha ? " (" + sha + ")" : "");
        }

        /// <summary>
        /// Label for the running game: the stamped build-info if the build has one, otherwise
        /// <paramref name="fallbackVersion"/> (pass <c>Application.version</c>). Never throws.
        /// </summary>
        public static string LoadLabel(string streamingAssetsPath, string fallbackVersion)
        {
            try
            {
                string path = string.IsNullOrEmpty(streamingAssetsPath) ? null : Path.Combine(streamingAssetsPath, FileName);
                if (path != null && File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    string v = ReadField(json, "version");
                    if (!string.IsNullOrWhiteSpace(v))
                        return Label(v, ReadField(json, "commit"));
                }
            }
            catch (Exception)
            {
                // Unreadable stamp (Android jar paths, permissions): fall through to the fallback.
            }
            return "v" + (string.IsNullOrWhiteSpace(fallbackVersion) ? DevCore + "-dev" : fallbackVersion);
        }

        private static void Field(StringBuilder sb, string name, string value, bool last)
        {
            sb.Append("  \"").Append(name).Append("\": \"");
            foreach (char ch in value ?? "")
            {
                if (ch == '"' || ch == '\\') sb.Append('\\').Append(ch);
                else if (ch == '\n') sb.Append("\\n");
                else if (ch == '\t') sb.Append("\\t");
                else if (ch >= ' ') sb.Append(ch);
            }
            sb.Append(last ? "\"\n" : "\",\n");
        }

        private static bool TryParseCore(string version, out int major, out int minor, out int patch)
        {
            major = minor = patch = 0;
            if (string.IsNullOrEmpty(version)) return false;
            int len = CoreLength(version);
            if (len == 0) return false;
            string[] parts = version.Substring(0, len).Split('.');
            return parts.Length == 3
                   && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out major)
                   && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out minor)
                   && int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out patch);
        }

        /// <summary>Length of the leading <c>digits.digits.digits</c> run, or 0.</summary>
        private static int CoreLength(string s)
        {
            int dots = 0, i = 0, digitsInPart = 0;
            for (; i < s.Length; i++)
            {
                char ch = s[i];
                if (ch >= '0' && ch <= '9') { digitsInPart++; continue; }
                if (ch == '.' && digitsInPart > 0 && dots < 2) { dots++; digitsInPart = 0; continue; }
                break;
            }
            if (dots != 2 || digitsInPart == 0) return 0;
            // The core must end at the string end or a semver separator.
            if (i < s.Length && s[i] != '-' && s[i] != '+') return 0;
            return i;
        }
    }
}
