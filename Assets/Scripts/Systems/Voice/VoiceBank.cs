using System;
using System.Collections.Generic;
using System.Text;

namespace SolarMajesty
{
    /// <summary>Moments a character can voice. Names match the cue keys in Tools/audio/voice_lines.json.</summary>
    public enum VoiceCue
    {
        Select,
        Claim,
        Refused,
        Hunt,
        Flee,
        Repair,
        Rest,
        Wander,
        LevelUp,
        Hurt,
        Down,
        /// <summary>An Overseer line baked from its exact text (GrokCatalog, OverseerRules, …).</summary>
        Line,
        AlertWarning,
        AlertCritical,
        ModuleLost,
        HeroDown,
        Victory,
        Defeat
    }

    /// <summary>One baked clip from Assets/Resources/Audio/Voices/voices.json.</summary>
    public sealed class VoiceClipInfo
    {
        public string Speaker;
        public VoiceCue Cue;
        public int Take;
        public string Key;
        public string Text;
        /// <summary>Resource name under Resources/Audio/Voices/.</summary>
        public string Clip;
        public float Seconds;
    }

    /// <summary>
    /// The baked voice bank: barks per speaker and cue, plus Overseer lines keyed by their text.
    ///
    /// The bank is produced offline by Tools/audio/render_voices.py (Kokoro-82M TTS plus robot
    /// processing), so voices work with no local server running. Overseer lines are keyed by a
    /// hash of their normalised text rather than an id, so any code path that logs a scripted
    /// line gets its voice without having to know the bank exists.
    /// </summary>
    public sealed class VoiceBank
    {
        public const string OverseerSpeaker = "overseer";
        public const string HeroPrefix = "hero.";

        private readonly Dictionary<string, List<VoiceClipInfo>> _barks = new Dictionary<string, List<VoiceClipInfo>>();
        private readonly Dictionary<string, VoiceClipInfo> _lines = new Dictionary<string, VoiceClipInfo>();
        private readonly Dictionary<string, int> _lastTake = new Dictionary<string, int>();

        public int Count { get; private set; }
        public int LineCount => _lines.Count;

        public static string SpeakerFor(SpecialistClass cls) => HeroPrefix + cls;

        public static string CueKey(VoiceCue cue)
        {
            switch (cue)
            {
                case VoiceCue.LevelUp: return "levelup";
                case VoiceCue.AlertWarning: return "alert_warning";
                case VoiceCue.AlertCritical: return "alert_critical";
                case VoiceCue.ModuleLost: return "module_lost";
                case VoiceCue.HeroDown: return "hero_down";
                default: return cue.ToString().ToLowerInvariant();
            }
        }

        public static bool TryParseCue(string key, out VoiceCue cue)
        {
            foreach (VoiceCue c in (VoiceCue[])Enum.GetValues(typeof(VoiceCue)))
            {
                if (!string.Equals(CueKey(c), key, StringComparison.Ordinal)) continue;
                cue = c;
                return true;
            }
            cue = VoiceCue.Line;
            return false;
        }

        /// <summary>Narration kinds map one-to-one onto hero cues.</summary>
        public static VoiceCue CueFor(NarrationKind kind)
        {
            switch (kind)
            {
                case NarrationKind.Claim: return VoiceCue.Claim;
                case NarrationKind.Flee: return VoiceCue.Flee;
                case NarrationKind.Hunt: return VoiceCue.Hunt;
                case NarrationKind.Repair: return VoiceCue.Repair;
                case NarrationKind.Rest: return VoiceCue.Rest;
                case NarrationKind.LevelUp: return VoiceCue.LevelUp;
                case NarrationKind.Refused: return VoiceCue.Refused;
                default: return VoiceCue.Wander;
            }
        }

        /// <summary>Parse voices.json. Unknown cues and malformed entries are skipped, not fatal.</summary>
        public static VoiceBank Parse(string json)
        {
            var bank = new VoiceBank();
            if (string.IsNullOrEmpty(json)) return bank;

            object root;
            try { root = LocalJson.Parse(json); }
            catch (Exception) { return bank; }

            if (!(root is Dictionary<string, object> map) ||
                !map.TryGetValue("clips", out object clipsObj) ||
                !(clipsObj is List<object> clips))
                return bank;

            for (int i = 0; i < clips.Count; i++)
            {
                if (!(clips[i] is Dictionary<string, object> c)) continue;
                string speaker = Str(c, "speaker");
                string clip = Str(c, "clip");
                if (string.IsNullOrEmpty(speaker) || string.IsNullOrEmpty(clip)) continue;
                if (!TryParseCue(Str(c, "cue"), out VoiceCue cue)) continue;

                var info = new VoiceClipInfo
                {
                    Speaker = speaker,
                    Cue = cue,
                    Take = (int)Num(c, "take"),
                    Key = Str(c, "key"),
                    Text = Str(c, "text"),
                    Clip = clip,
                    Seconds = (float)Num(c, "seconds")
                };
                bank.Add(info);
            }
            return bank;
        }

        public void Add(VoiceClipInfo info)
        {
            if (info == null) return;
            Count++;
            if (info.Cue == VoiceCue.Line)
            {
                string key = string.IsNullOrEmpty(info.Key) ? LineKey(info.Text) : info.Key;
                _lines[key] = info;
                return;
            }

            string slot = Slot(info.Speaker, info.Cue);
            if (!_barks.TryGetValue(slot, out var list))
            {
                list = new List<VoiceClipInfo>(3);
                _barks[slot] = list;
            }
            list.Add(info);
        }

        public bool HasBark(string speaker, VoiceCue cue) =>
            _barks.TryGetValue(Slot(speaker, cue), out var list) && list.Count > 0;

        /// <summary>
        /// Pick a take for this speaker and cue, never the same take twice in a row: hearing the
        /// identical "Ow!" back to back is what makes barks sound canned.
        /// </summary>
        public bool TryPickBark(string speaker, VoiceCue cue, Random rng, out VoiceClipInfo info)
        {
            info = null;
            string slot = Slot(speaker, cue);
            if (!_barks.TryGetValue(slot, out var list) || list.Count == 0) return false;

            int pick = list.Count == 1 ? 0 : (rng != null ? rng.Next(list.Count) : 0);
            if (list.Count > 1 && _lastTake.TryGetValue(slot, out int last) && pick == last)
                pick = (pick + 1 + (rng != null ? rng.Next(list.Count - 1) : 0)) % list.Count;
            _lastTake[slot] = pick;
            info = list[pick];
            return true;
        }

        /// <summary>A baked Overseer clip for this exact line (speaker prefix and punctuation ignored).</summary>
        public bool TryGetLine(string text, out VoiceClipInfo info)
        {
            info = null;
            if (string.IsNullOrEmpty(text)) return false;
            return _lines.TryGetValue(LineKey(text), out info);
        }

        private static string Slot(string speaker, VoiceCue cue) => speaker + "|" + (int)cue;

        // ---- line keys (mirror of Tools/audio/voice_common.py) --------------------------------

        private static readonly string[] SpeakerPrefixes = { "Grok — ", "Grok - ", "Grok: " };

        /// <summary>
        /// Lower-case ASCII letters and digits, single spaces between runs, speaker prefix removed.
        /// Must match normalize_line in voice_common.py exactly, or baked lines stop being found.
        /// </summary>
        public static string NormalizeLine(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            string t = text.Trim();
            for (int i = 0; i < SpeakerPrefixes.Length; i++)
            {
                if (!t.StartsWith(SpeakerPrefixes[i], StringComparison.Ordinal)) continue;
                t = t.Substring(SpeakerPrefixes[i].Length);
                break;
            }

            var sb = new StringBuilder(t.Length);
            bool pendingSpace = false;
            for (int i = 0; i < t.Length; i++)
            {
                char ch = char.ToLowerInvariant(t[i]);
                bool keep = (ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9');
                if (!keep)
                {
                    pendingSpace = true;
                    continue;
                }
                if (pendingSpace && sb.Length > 0) sb.Append(' ');
                sb.Append(ch);
                pendingSpace = false;
            }
            return sb.ToString();
        }

        /// <summary>FNV-1a 32 of the normalised line as 8 lower-case hex chars.</summary>
        public static string LineKey(string text)
        {
            string n = NormalizeLine(text);
            uint h = 0x811C9DC5u;
            for (int i = 0; i < n.Length; i++)
            {
                h ^= n[i];
                h *= 0x01000193u;
            }
            return h.ToString("x8");
        }

        private static string Str(Dictionary<string, object> m, string k) =>
            m.TryGetValue(k, out object v) ? v as string : null;

        private static double Num(Dictionary<string, object> m, string k)
        {
            if (!m.TryGetValue(k, out object v) || v == null) return 0;
            switch (v)
            {
                case double d: return d;
                case float f: return f;
                case int i: return i;
                case long l: return l;
                default: return 0;
            }
        }
    }
}
