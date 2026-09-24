using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>What just happened to a hero, worth a line of narration.</summary>
    public enum NarrationKind
    {
        Claim,
        Flee,
        Hunt,
        Repair,
        Rest,
        Wander,
        LevelUp,
        /// <summary>The player's written flag orders exclude this hero.</summary>
        Refused
    }

    /// <summary>Facts about one hero at one moment — the only thing the narrator may talk about.</summary>
    public struct HeroMoment
    {
        public NarrationKind Kind;
        public string ClassName;
        public int Level;
        public float Greed, Courage, Workaholic;
        public float Health01, Fatigue01;
        public int Credits;
        public string World;
        /// <summary>Flag type in play (claim / refusal), or null.</summary>
        public string FlagType;
        public int Bounty;
        /// <summary>Player's written orders on that flag, or null.</summary>
        public string Orders;
        /// <summary>Brain reason code (e.g. "workshop_duty", "flee_to_inn").</summary>
        public string Reason;
    }

    /// <summary>
    /// Builds the request for a small local LLM (any OpenAI-compatible server: llama.cpp,
    /// Ollama, mlx_lm.server, LM Studio) and turns its reply into one safe, short line. The model
    /// only voices what the brain already decided — it never chooses anything.
    /// </summary>
    public static class HeroNarration
    {
        public const string Route = "/v1/chat/completions";
        public const int MaxChars = 64;
        public const int MaxTokens = 48;

        public const string SystemPrompt =
            "You voice autonomous specialists in a Majesty-style space colony game. Reply with ONE " +
            "punchy first-person line, at most 9 words, that the hero mutters right now. Speak in the " +
            "given voice. Be specific and a little funny. Never reuse wording from earlier replies. " +
            "Use only the facts given. Reply in English. No emoji, no " +
            "quotes, no explanations.";

        /// <summary>Example exchanges: small models copy tone and length from these far better than from rules.</summary>
        static readonly string[,] Examples =
        {
            { "Hero: level 3 Defense Mech on Mars. Voice: greedy — always about the money. Now: just took the den bounty paying 600 credits.",
              "Six hundred for a den? I'd clear two." },
            { "Hero: level 1 Scout Drone on Luna. Voice: cowardly — nervous, looking for an exit. Now: badly hurt and running back to the inn.",
              "Nope. Nope. Inn. Now." },
            { "Hero: level 5 Engineer Bot on Earth. Voice: workaholic — lives for the job. Now: is not allowed to take the build bounty because the overseer's orders say \"level 6 or higher\".",
              "Level six? I've welded worse blindfolded." },
        };

        /// <summary>Plain-language facts for the user message.</summary>
        public static string Describe(in HeroMoment m)
        {
            var sb = new StringBuilder(260);
            sb.Append("Hero: level ").Append(Mathf.Max(1, m.Level)).Append(' ').Append(Safe(m.ClassName, "specialist"));
            if (!string.IsNullOrEmpty(m.World)) sb.Append(" on ").Append(m.World);
            sb.Append(". Voice: ").Append(Voice(m)).Append(". Personality: ")
              .Append(Word(m.Greed, "modest", "fair", "greedy")).Append(", ")
              .Append(Word(m.Courage, "cowardly", "steady", "fearless")).Append(", ")
              .Append(Word(m.Workaholic, "lazy", "diligent", "workaholic")).Append(". ");
            sb.Append("Health ").Append(Pct(m.Health01)).Append(", tired ").Append(Pct(m.Fatigue01))
              .Append(", purse ").Append(Mathf.Max(0, m.Credits)).Append(" credits. ");
            sb.Append("Now: ").Append(Situation(m)).Append('.');
            return sb.ToString();
        }

        /// <summary>The hero's strongest trait as a voice direction.</summary>
        public static string Voice(in HeroMoment m)
        {
            float greed = Mathf.Abs(m.Greed - 0.5f), courage = Mathf.Abs(m.Courage - 0.5f), work = Mathf.Abs(m.Workaholic - 0.5f);
            if (greed >= courage && greed >= work)
                return m.Greed >= 0.5f ? "greedy — always about the money" : "modest — happy with little, wry";
            if (courage >= work)
                return m.Courage >= 0.5f ? "fearless — cocky, loves a fight" : "cowardly — nervous, looking for an exit";
            return m.Workaholic >= 0.5f ? "workaholic — lives for the job" : "lazy — would rather be napping";
        }

        static string Situation(in HeroMoment m)
        {
            string flag = Safe(m.FlagType, "a");
            switch (m.Kind)
            {
                case NarrationKind.Claim:
                    return $"just took the {flag} bounty paying {m.Bounty} credits" +
                           (string.IsNullOrEmpty(m.Orders) ? "" : $" (orders on it: \"{Clip(m.Orders, 80)}\")");
                case NarrationKind.Refused:
                    return $"is not allowed to take the {flag} bounty because the overseer's orders say \"{Clip(m.Orders, 80)}\"";
                case NarrationKind.Flee: return "badly hurt and running back to the inn";
                case NarrationKind.Hunt: return "going to hunt nearby hostile fauna, no bounty";
                case NarrationKind.Repair: return "going to patch a damaged colony module for a small fee";
                case NarrationKind.Rest: return "heading to the inn to rest and heal";
                case NarrationKind.LevelUp: return $"just reached level {Mathf.Max(1, m.Level)}";
                default:
                    return m.Reason switch
                    {
                        "workshop_duty" => "working a shift at the guild workshop",
                        "patrolling" => "patrolling the colony perimeter",
                        "triage" or "inn_triage" => "tending the wounded",
                        "levy_collect" or "levy_home" => "walking the tax levy route",
                        _ => "wandering the frontier with no job"
                    };
            }
        }

        /// <summary>OpenAI-compatible chat request. "/no_think" and the template flag turn off Qwen3's thinking.</summary>
        public static string BuildRequest(string model, in HeroMoment m)
        {
            var sb = new StringBuilder(900);
            sb.Append("{\"model\":");
            LocalJson.AppendString(sb, string.IsNullOrEmpty(model) ? "local" : model);
            sb.Append(",\"messages\":[{\"role\":\"system\",\"content\":");
            LocalJson.AppendString(sb, SystemPrompt);
            for (int i = 0; i < Examples.GetLength(0); i++)
            {
                sb.Append("},{\"role\":\"user\",\"content\":");
                LocalJson.AppendString(sb, Examples[i, 0] + " /no_think");
                sb.Append("},{\"role\":\"assistant\",\"content\":");
                LocalJson.AppendString(sb, Examples[i, 1]);
            }
            sb.Append("},{\"role\":\"user\",\"content\":");
            LocalJson.AppendString(sb, Describe(m) + " /no_think");
            sb.Append("}],\"max_tokens\":").Append(MaxTokens)
              .Append(",\"temperature\":1.0,\"top_p\":0.95,\"stream\":false")
              // No newline stop: Qwen3 opens with an empty <think> block; Sanitize keeps the first real line.
              .Append(",\"chat_template_kwargs\":{\"enable_thinking\":false}}");
            return sb.ToString();
        }

        /// <summary>Reads choices[0].message.content and sanitises it. False if nothing usable.</summary>
        public static bool TryParseLine(string json, out string line)
        {
            line = null;
            if (string.IsNullOrEmpty(json)) return false;
            object root;
            try { root = LocalJson.Parse(json); }
            catch (System.FormatException) { return false; }
            if (!(root is Dictionary<string, object> top) ||
                !top.TryGetValue("choices", out var c) || !(c is List<object> choices) || choices.Count == 0 ||
                !(choices[0] is Dictionary<string, object> first))
                return false;
            string content = null;
            if (first.TryGetValue("message", out var msg) && msg is Dictionary<string, object> m &&
                m.TryGetValue("content", out var ct))
                content = ct as string;
            else if (first.TryGetValue("text", out var tx))
                content = tx as string;
            line = Sanitize(content);
            return line != null;
        }

        /// <summary>
        /// One line, printable, no markup (IMGUI rich text), no wrapping quotes, capped length.
        /// Null when the model produced nothing usable (empty, thinking only, refusal boilerplate).
        /// </summary>
        public static string Sanitize(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            string s = raw;
            int think;
            while ((think = s.IndexOf("<think>", System.StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                int end = s.IndexOf("</think>", think, System.StringComparison.OrdinalIgnoreCase);
                if (end < 0)
                {
                    // Unclosed block: small models sometimes put the line itself in there. Keep it
                    // only if it reads like a line, not like reasoning about the prompt.
                    s = s.Remove(think, 7);
                    if (LooksLikeReasoning(s)) return null;
                    break;
                }
                s = s.Remove(think, end + 8 - think);
            }

            var sb = new StringBuilder(s.Length);
            foreach (char ch in s.Trim())
            {
                if (ch == '\n' || ch == '\r') break;           // first line only
                if (ch == '<' || ch == '>' || ch == '*' || ch == '#' || ch == '`') continue;
                if (char.IsControl(ch)) continue;
                if (char.IsSurrogate(ch)) continue;              // emoji
                sb.Append(ch);
            }
            string t = sb.ToString().Trim().Trim('"', '\'', '“', '”', ' ');
            if (t.Length < 3) return null;
            string lower = t.ToLowerInvariant();
            if (lower.Contains("as an ai") || lower.Contains("language model") || lower.StartsWith("i cannot") ||
                lower.StartsWith("i can't help"))
                return null;
            if (t.Length > MaxChars)
            {
                int cut = t.LastIndexOf(' ', MaxChars - 1);
                t = (cut > MaxChars / 2 ? t.Substring(0, cut) : t.Substring(0, MaxChars - 1)).TrimEnd(',', ';', ':') + "…";
            }
            return t;
        }

        static readonly string[] ReasoningOpeners =
            { "okay", "ok,", "let me", "let's see", "hmm", "the hero", "the user", "first,", "so,", "i need to", "alright" };

        /// <summary>True when text is the model thinking about the prompt rather than speaking a line.</summary>
        public static bool LooksLikeReasoning(string s)
        {
            string t = s.TrimStart();
            int nl = t.IndexOf('\n');
            string first = (nl >= 0 ? t.Substring(0, nl) : t).Trim().ToLowerInvariant();
            if (first.Length > MaxChars * 1.3f) return true;
            for (int i = 0; i < ReasoningOpeners.Length; i++)
                if (first.StartsWith(ReasoningOpeners[i], System.StringComparison.Ordinal)) return true;
            return false;
        }

        static string Pct(float v) => Mathf.RoundToInt(Mathf.Clamp01(v) * 100f) + "%";
        static string Word(float v, string lo, string mid, string hi) => v < 0.35f ? lo : v > 0.65f ? hi : mid;
        static string Safe(string s, string fallback) => string.IsNullOrWhiteSpace(s) ? fallback : s;
        static string Clip(string s, int n) => string.IsNullOrEmpty(s) ? "" : (s.Length <= n ? s : s.Substring(0, n));
    }

    /// <summary>
    /// Decides which moments get narrated so a small model on a laptop keeps up: one request in
    /// flight, a short global gap, and per-hero cooldowns. The selected hero and big moments
    /// (claims, flights, level-ups, refusals) get priority; routine wandering is voiced only for
    /// the hero the player is watching. Pure; time is passed in.
    /// </summary>
    public sealed class NarrationScheduler
    {
        public float GlobalGap = 1.5f;
        public float HeroCooldown = 20f;
        public float SelectedCooldown = 7f;
        public float BigMomentCooldown = 6f;
        /// <summary>Give up on a request that has not answered by then (frees the slot).</summary>
        public float Timeout = 8f;

        private readonly Dictionary<int, float> _lastByHero = new Dictionary<int, float>();
        private float _lastStart = float.NegativeInfinity;
        private float _inFlightSince = float.NegativeInfinity;
        private bool _inFlight;

        public bool InFlight => _inFlight;

        public static bool IsBigMoment(NarrationKind k) =>
            k == NarrationKind.Claim || k == NarrationKind.Flee || k == NarrationKind.LevelUp || k == NarrationKind.Refused;

        /// <summary>True if this moment should be sent now; marks the slot busy when it is.</summary>
        public bool TryStart(int heroId, NarrationKind kind, bool selected, float now)
        {
            if (_inFlight && now - _inFlightSince < Timeout) return false;
            _inFlight = false;
            if (now - _lastStart < GlobalGap) return false;
            bool big = IsBigMoment(kind);
            if (!big && !selected) return false;

            float cooldown = selected ? SelectedCooldown : big ? BigMomentCooldown : HeroCooldown;
            if (_lastByHero.TryGetValue(heroId, out float last) && now - last < cooldown) return false;

            _lastByHero[heroId] = now;
            _lastStart = now;
            _inFlight = true;
            _inFlightSince = now;
            return true;
        }

        public void Finish() => _inFlight = false;

        public void Forget(int heroId) => _lastByHero.Remove(heroId);
    }
}
