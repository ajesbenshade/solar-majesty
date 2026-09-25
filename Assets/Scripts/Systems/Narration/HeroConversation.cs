using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>Who the player is talking to, as the model is allowed to know them.</summary>
    public struct HeroPersona
    {
        public string Name;
        public string Designation;
        public string ClassName;
        public int Level;
        public string Rank;
        public float Greed, Courage, Workaholic;
        public float Health01, Fatigue01;
        public int Credits;
        public string World;
        /// <summary>What the hero is doing right now, in plain words.</summary>
        public string Situation;
        /// <summary>Recent things that happened to this hero, oldest first.</summary>
        public IReadOnlyList<string> Recent;
    }

    public sealed class ChatLine
    {
        public bool FromPlayer;
        public string Text;

        public ChatLine(bool fromPlayer, string text)
        {
            FromPlayer = fromPlayer;
            Text = text;
        }
    }

    /// <summary>
    /// One hero's side of the conversation: what was said, and what happened to them lately.
    /// Kept short on purpose — a small model answers faster and stays in character better with a
    /// few turns of context than with the whole transcript.
    /// </summary>
    public sealed class HeroChatLog
    {
        public const int MaxLines = 24;
        public const int MaxEvents = 6;

        private readonly List<ChatLine> _lines = new List<ChatLine>(MaxLines);
        private readonly List<string> _events = new List<string>(MaxEvents);

        public IReadOnlyList<ChatLine> Lines => _lines;
        public IReadOnlyList<string> Events => _events;

        public void Add(bool fromPlayer, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            _lines.Add(new ChatLine(fromPlayer, text));
            if (_lines.Count > MaxLines) _lines.RemoveAt(0);
        }

        /// <summary>Remember a moment; the same moment twice in a row is kept once.</summary>
        public void Remember(string moment)
        {
            if (string.IsNullOrWhiteSpace(moment)) return;
            if (_events.Count > 0 && _events[_events.Count - 1] == moment) return;
            _events.Add(moment);
            if (_events.Count > MaxEvents) _events.RemoveAt(0);
        }
    }

    /// <summary>
    /// Talk to a hero through the same small local model that narrates them. Pure: builds the
    /// streaming chat request, reads server-sent deltas, and cleans replies for the HUD.
    ///
    /// Speed comes from three things: replies are streamed so the first words show within a few
    /// hundred milliseconds, the persona prompt is stable so the server reuses its cached prefix,
    /// and only the last <see cref="HistoryLines"/> lines are sent. Conversation is roleplay only —
    /// like narration, the model never changes what the hero decides.
    /// </summary>
    public static class HeroConversation
    {
        public const int MaxReplyTokens = 96;
        public const int MaxReplyChars = 260;
        public const int MaxPlayerChars = 160;
        public const int HistoryLines = 8;

        public static string SystemPrompt(in HeroPersona p)
        {
            var sb = new StringBuilder(900);
            sb.Append("You are ").Append(Safe(p.Name, "a robot"));
            if (!string.IsNullOrEmpty(p.Designation)) sb.Append(" \"").Append(p.Designation).Append('"');
            sb.Append(", a level ").Append(Mathf.Max(1, p.Level)).Append(' ').Append(Safe(p.ClassName, "specialist"));
            if (!string.IsNullOrEmpty(p.Rank)) sb.Append(" (").Append(p.Rank.ToLowerInvariant()).Append(" service record)");
            sb.Append(", one of the autonomous robot heroes of a Majesty-style space colony");
            if (!string.IsNullOrEmpty(p.World)) sb.Append(" on ").Append(p.World);
            sb.Append(". The player is the Overseer, the colony AI: it posts bounty flags and builds, but you choose your own work. ");

            var m = new HeroMoment { Greed = p.Greed, Courage = p.Courage, Workaholic = p.Workaholic };
            sb.Append("Your voice: ").Append(HeroNarration.Voice(m)).Append(". Personality: ")
              .Append(Word(p.Greed, "modest", "fair", "greedy")).Append(", ")
              .Append(Word(p.Courage, "cowardly", "steady", "fearless")).Append(", ")
              .Append(Word(p.Workaholic, "lazy", "diligent", "workaholic")).Append(". ");
            sb.Append("Right now you are ").Append(Safe(p.Situation, "between jobs")).Append(". ");
            sb.Append("Health ").Append(Pct(p.Health01)).Append(", tired ").Append(Pct(p.Fatigue01))
              .Append(", purse ").Append(Mathf.Max(0, p.Credits)).Append(" credits. ");
            if (p.Recent != null && p.Recent.Count > 0)
            {
                sb.Append("Lately: ");
                for (int i = 0; i < p.Recent.Count; i++)
                {
                    if (i > 0) sb.Append("; ");
                    sb.Append(p.Recent[i]);
                }
                sb.Append(". ");
            }
            // Tuned against Qwen3-1.7B: quoted example lines get parroted verbatim, so the style is
            // described instead, and "not an assistant" stops the "let me know if…" register.
            sb.Append("Stay in character. Answer in one or two short spoken sentences, under 35 words. ")
              .Append("No emoji, no stage directions, no lists. ")
              .Append("You are a frontier robot, not an assistant: never offer help, never ask what they want, never say 'let me know'. Have opinions. ")
              .Append("You cannot promise to change what you do; if asked, name your price. ")
              .Append("Answer exactly what was asked, in your own words; never repeat an earlier reply. ")
              .Append("Talk like a blunt, funny frontier worker: contractions, short clauses, concrete details from your situation. /no_think");
            return sb.ToString();
        }

        /// <summary>OpenAI-compatible streaming chat request.</summary>
        public static string BuildRequest(string model, in HeroPersona p, IReadOnlyList<ChatLine> history, bool stream = true, int maxTokens = MaxReplyTokens)
        {
            var sb = new StringBuilder(1600);
            sb.Append("{\"model\":");
            LocalJson.AppendString(sb, string.IsNullOrEmpty(model) ? "local" : model);
            sb.Append(",\"messages\":[{\"role\":\"system\",\"content\":");
            LocalJson.AppendString(sb, SystemPrompt(p));
            sb.Append('}');
            if (history != null)
            {
                int start = Mathf.Max(0, history.Count - HistoryLines);
                // A reply must follow a player line; drop a leading hero line cut loose by the window.
                while (start < history.Count && !history[start].FromPlayer) start++;
                for (int i = start; i < history.Count; i++)
                {
                    var line = history[i];
                    sb.Append(",{\"role\":").Append(line.FromPlayer ? "\"user\"" : "\"assistant\"").Append(",\"content\":");
                    LocalJson.AppendString(sb, line.Text);
                    sb.Append('}');
                }
            }
            sb.Append("],\"max_tokens\":").Append(maxTokens)
              .Append(",\"temperature\":0.8,\"top_p\":0.95,\"presence_penalty\":0.6,\"frequency_penalty\":0.3")
              .Append(",\"stream\":").Append(stream ? "true" : "false")
              .Append(HeroNarration.NoThinkingFields).Append('}');
            return sb.ToString();
        }

        /// <summary>
        /// One server-sent-events line. <paramref name="piece"/> is the new text (may be empty);
        /// <paramref name="done"/> is true at <c>[DONE]</c> or a finish reason. False for lines
        /// that carry nothing (comments, keep-alives, blank lines).
        /// </summary>
        public static bool TryParseDelta(string sseLine, out string piece, out bool done)
        {
            piece = null;
            done = false;
            if (string.IsNullOrEmpty(sseLine)) return false;
            string line = sseLine.Trim();
            if (!line.StartsWith("data:")) return false;
            string data = line.Substring(5).Trim();
            if (data == "[DONE]")
            {
                done = true;
                return true;
            }
            object root;
            try { root = LocalJson.Parse(data); }
            catch (System.FormatException) { return false; }
            if (!(root is Dictionary<string, object> top) ||
                !top.TryGetValue("choices", out var c) || !(c is List<object> choices) || choices.Count == 0 ||
                !(choices[0] is Dictionary<string, object> first))
                return false;
            if (first.TryGetValue("delta", out var d) && d is Dictionary<string, object> delta &&
                delta.TryGetValue("content", out var ct))
                piece = ct as string;
            else if (first.TryGetValue("text", out var tx))
                piece = tx as string;
            if (first.TryGetValue("finish_reason", out var fr) && fr is string reason && reason.Length > 0)
                done = true;
            piece ??= "";
            return true;
        }

        /// <summary>Text to show while a reply is still streaming: hides thinking, trims, strips markup.</summary>
        public static string StreamingView(string raw, string speaker = null)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            string s = StripThinking(raw, out bool openThink);
            if (openThink) return "";
            return Tidy(s, false, speaker);
        }

        /// <summary>The finished reply: one tidy paragraph, capped at a sentence boundary. Null if unusable.</summary>
        public static string CleanReply(string raw, string speaker = null, IReadOnlyList<ChatLine> history = null)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            string s = StripThinking(raw, out bool openThink);
            if (openThink && HeroNarration.LooksLikeReasoning(s)) return null;
            s = Tidy(s, true, speaker);
            s = DropFiller(s, history);
            if (s.Length < 2) return null;
            string lower = s.ToLowerInvariant();
            if (lower.Contains("as an ai") || lower.Contains("language model")) return null;
            return s;
        }

        // Small models slide into a helpdesk register and echo their own earlier lines however the
        // prompt asks; these sentences are removed after the fact, as long as something is left.
        static readonly string[] AssistantTells =
        {
            "let me know", "feel free", "how can i help", "anything else", "is there anything",
            "happy to help", "glad to help", "if you need", "just say the word"
        };

        /// <summary>Drops helpdesk sentences and sentences repeated from earlier replies, keeping at least one.</summary>
        public static string DropFiller(string reply, IReadOnlyList<ChatLine> history)
        {
            if (string.IsNullOrEmpty(reply)) return reply;
            var said = new HashSet<string>();
            if (history != null)
                for (int i = 0; i < history.Count; i++)
                    if (!history[i].FromPlayer)
                        foreach (string old in Sentences(history[i].Text)) said.Add(Key(old));

            var kept = new List<string>();
            foreach (string sentence in Sentences(reply))
            {
                string k = Key(sentence);
                bool tell = false;
                for (int i = 0; i < AssistantTells.Length && !tell; i++) tell = k.Contains(AssistantTells[i]);
                if (!tell && !said.Contains(k)) kept.Add(sentence);
            }
            return kept.Count == 0 ? reply : string.Join(" ", kept);
        }

        static IEnumerable<string> Sentences(string text)
        {
            int start = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (!IsSentenceEnd(text[i]) && text[i] != '…') continue;
                int end = i + 1;
                while (end < text.Length && (IsSentenceEnd(text[end]) || text[end] == '"' || text[end] == '”')) end++;
                string piece = text.Substring(start, end - start).Trim();
                if (piece.Length > 0) yield return piece;
                start = end;
                i = end - 1;
            }
            if (start < text.Length)
            {
                string rest = text.Substring(start).Trim();
                if (rest.Length > 0) yield return rest;
            }
        }

        static string Key(string sentence) => sentence.Trim().TrimEnd('.', '!', '?', '…').ToLowerInvariant();

        /// <summary>A moment worth remembering, as a short past-tense clause; null for routine ones.</summary>
        public static string EventLine(in HeroMoment m)
        {
            string flag = string.IsNullOrWhiteSpace(m.FlagType) ? "a" : m.FlagType;
            switch (m.Kind)
            {
                case NarrationKind.Claim:
                    return m.Bounty > 0 ? $"took the {flag} bounty for {m.Bounty} credits" : $"took the {flag} bounty";
                case NarrationKind.Refused:
                    return string.IsNullOrEmpty(m.Orders)
                        ? $"was barred from the {flag} bounty by the Overseer's orders"
                        : $"was barred from the {flag} bounty by the Overseer's orders \"{Clip(m.Orders, 60)}\"";
                case NarrationKind.Flee: return "got badly hurt and ran for the inn";
                case NarrationKind.Hunt: return "went hunting fauna";
                case NarrationKind.Repair: return "patched a damaged colony module";
                case NarrationKind.Rest: return "rested up at the inn";
                case NarrationKind.LevelUp: return $"reached level {Mathf.Max(1, m.Level)}";
                default: return null;
            }
        }

        /// <summary>First preferred model the server lists (<c>/v1/models</c> body), or null to keep the configured one.</summary>
        public static string PickModel(string modelsJson, IReadOnlyList<string> preferred)
        {
            if (string.IsNullOrEmpty(modelsJson) || preferred == null) return null;
            object root;
            try { root = LocalJson.Parse(modelsJson); }
            catch (System.FormatException) { return null; }
            if (!(root is Dictionary<string, object> top) || !top.TryGetValue("data", out var d) || !(d is List<object> list))
                return null;
            var ids = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var item in list)
                if (item is Dictionary<string, object> entry && entry.TryGetValue("id", out var id) && id is string s)
                    ids.Add(s);
            for (int i = 0; i < preferred.Count; i++)
                if (ids.Contains(preferred[i])) return preferred[i];
            return null;
        }

        /// <summary>Non-streamed reply body (a server that ignored <c>"stream"</c>): choices[0].message.content.</summary>
        public static bool TryParseMessage(string json, out string content)
        {
            content = null;
            if (string.IsNullOrWhiteSpace(json)) return false;
            object root;
            try { root = LocalJson.Parse(json.Trim()); }
            catch (System.FormatException) { return false; }
            if (!(root is Dictionary<string, object> top) ||
                !top.TryGetValue("choices", out var c) || !(c is List<object> choices) || choices.Count == 0 ||
                !(choices[0] is Dictionary<string, object> first))
                return false;
            if (first.TryGetValue("message", out var msg) && msg is Dictionary<string, object> m &&
                m.TryGetValue("content", out var ct))
                content = ct as string;
            else if (first.TryGetValue("text", out var tx))
                content = tx as string;
            return !string.IsNullOrEmpty(content);
        }

        /// <summary>The server's error text (<c>{"error":{"message":…}}</c> or <c>{"error":"…"}</c>), capped for the HUD.</summary>
        public static string ErrorMessage(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return null;
            object root;
            try { root = LocalJson.Parse(body.Trim()); }
            catch (System.FormatException) { return null; }
            if (!(root is Dictionary<string, object> top) || !top.TryGetValue("error", out var e)) return null;
            string text = e as string;
            if (text == null && e is Dictionary<string, object> err && err.TryGetValue("message", out var msg))
                text = msg as string;
            return string.IsNullOrEmpty(text) ? null : Clip(text, 90);
        }

        /// <summary>Player input: one line, printable, capped.</summary>
        public static string CleanPlayerLine(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var sb = new StringBuilder(raw.Length);
            foreach (char ch in raw)
            {
                if (char.IsControl(ch)) continue;
                sb.Append(ch);
            }
            string t = sb.ToString().Trim();
            if (t.Length > MaxPlayerChars) t = t.Substring(0, MaxPlayerChars);
            return t.Length == 0 ? null : t;
        }

        static string StripThinking(string raw, out bool openThink)
        {
            openThink = false;
            string s = raw;
            int think;
            while ((think = s.IndexOf("<think>", System.StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                int end = s.IndexOf("</think>", think, System.StringComparison.OrdinalIgnoreCase);
                if (end < 0)
                {
                    openThink = true;
                    return s.Substring(think + 7);
                }
                s = s.Remove(think, end + 8 - think);
            }
            // A stray closing tag (the opener was in an earlier chunk we never saw).
            int close = s.IndexOf("</think>", System.StringComparison.OrdinalIgnoreCase);
            if (close >= 0) s = s.Substring(close + 8);
            return s;
        }

        /// <summary>Collapse whitespace, drop markup and *stage directions*, strip a "Name:" prefix, cap length.</summary>
        static string Tidy(string s, bool final, string speaker)
        {
            var sb = new StringBuilder(s.Length);
            bool inStage = false;
            bool space = false;
            foreach (char ch in s)
            {
                if (ch == '*') { inStage = !inStage; continue; }
                if (inStage) continue;
                if (ch == '<' || ch == '>' || ch == '#' || ch == '`' || ch == '_') continue;
                if (char.IsSurrogate(ch)) continue; // emoji
                if (char.IsWhiteSpace(ch) || char.IsControl(ch))
                {
                    space = sb.Length > 0;
                    continue;
                }
                if (space) sb.Append(' ');
                space = false;
                sb.Append(ch);
            }
            string t = sb.ToString();

            // "Rivet: Sure thing." → "Sure thing." Only the hero's own name, so "Look: …" survives.
            int colon = t.IndexOf(':');
            if (!string.IsNullOrEmpty(speaker) && colon > 0 && colon <= 40 && colon + 1 < t.Length &&
                t.StartsWith(speaker, System.StringComparison.OrdinalIgnoreCase))
                t = t.Substring(colon + 1).TrimStart();
            t = t.Trim().Trim('"', '“', '”').Trim();

            if (t.Length > MaxReplyChars)
            {
                int cut = LastSentenceEnd(t, MaxReplyChars);
                t = cut > MaxReplyChars / 3 ? t.Substring(0, cut + 1) : t.Substring(0, MaxReplyChars - 1).TrimEnd() + "…";
            }
            else if (final && t.Length > 0 && !IsSentenceEnd(t[t.Length - 1]) && t[t.Length - 1] != '…')
            {
                // Cut off by the token cap: end on the last whole sentence when there is one.
                int cut = LastSentenceEnd(t, t.Length);
                if (cut > t.Length / 3) t = t.Substring(0, cut + 1);
            }
            return t;
        }

        static int LastSentenceEnd(string t, int within)
        {
            for (int i = Mathf.Min(within, t.Length) - 1; i >= 0; i--)
                if (IsSentenceEnd(t[i])) return i;
            return -1;
        }

        static bool IsSentenceEnd(char c) => c == '.' || c == '!' || c == '?';
        static string Pct(float v) => Mathf.RoundToInt(Mathf.Clamp01(v) * 100f) + "%";
        static string Word(float v, string lo, string mid, string hi) => v < 0.35f ? lo : v > 0.65f ? hi : mid;
        static string Safe(string s, string fallback) => string.IsNullOrWhiteSpace(s) ? fallback : s;
        static string Clip(string s, int n) => string.IsNullOrEmpty(s) ? "" : (s.Length <= n ? s : s.Substring(0, n));
    }
}
