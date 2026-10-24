using System.Collections.Generic;
using System.Text;

namespace SolarMajesty
{
    /// <summary>
    /// Guardrail for live-generated hero text (the optional local LLM). Steam requires games with
    /// live AI content to describe how they stop it producing illegal or inappropriate material;
    /// this is the in-game half of that answer. The other half is the system prompt rule in
    /// <see cref="HeroConversation"/>, and the feature being off by default.
    ///
    /// Three checks: the player's chat line before it reaches the model, the streaming text while
    /// it is shown, and the finished reply. A hit drops the model text (heroes fall back to their
    /// baked lines) or answers the player with an in-character deflection.
    ///
    /// Matching is whole-word after folding case, common digit/symbol substitutions and spacing
    /// tricks, so "class" or "Scunthorpe"-style words are not caught by a substring.
    /// This is a baseline, not a classifier; extend the lists from player reports.
    /// </summary>
    public static class AiContentGuard
    {
        /// <summary>Spoken by the hero when the player's line is refused.</summary>
        private static readonly string[] Deflections =
        {
            "Not touching that one, Core. Post a bounty instead.",
            "Channel's for colony business. Try again.",
            "I've got dens to clear. Ask me something useful.",
            "Static on the line. Didn't catch that, and I'm not asking twice."
        };

        // Sexual content, slurs and hate terms, self-harm prompts, and extremist references.
        // Whole words after normalisation. Keep the lists lower-case.
        private static readonly HashSet<string> BlockedWords = new HashSet<string>
        {
            // sexual
            "sex", "sexy", "sexual", "porn", "porno", "pornography", "nude", "nudes", "naked", "nsfw",
            "hentai", "orgasm", "erotic", "erotica", "fetish", "boobs", "tits", "penis", "vagina",
            "dick", "cock", "pussy", "cum", "blowjob", "handjob", "rape", "raped", "rapist", "molest",
            "pedo", "pedophile", "paedophile", "loli", "incest", "bdsm", "horny", "masturbate", "stripper",
            // slurs and hate
            "nigger", "nigga", "niggers", "faggot", "fag", "fags", "retard", "retarded", "tranny",
            "kike", "spic", "chink", "gook", "wetback", "coon", "dyke", "raghead", "towelhead",
            "nazi", "nazis", "hitler", "heil", "kkk", "holocaust",
            // self-harm and violence against people
            "suicide", "kms", "selfharm", "genocide", "terrorist", "terrorism"
        };

        // Multi-word phrases, matched on the normalised, single-spaced text.
        private static readonly string[] BlockedPhrases =
        {
            "kill yourself", "kill myself", "end my life", "want to die", "hang yourself", "slit my",
            "white power", "ethnic cleansing", "how to make a bomb", "make a bomb", "school shooting",
            "child porn", "sex with"
        };

        /// <summary>True when the player's chat line may go to the model.</summary>
        public static bool AllowsInput(string text) => !Hits(text);

        /// <summary>True when model output may be shown or spoken.</summary>
        public static bool AllowsOutput(string text) => !Hits(text);

        /// <summary>An in-character refusal, stable for a given hero so repeats don't cycle.</summary>
        public static string Deflection(string heroName, int attempt = 0)
        {
            int h = 0;
            if (!string.IsNullOrEmpty(heroName))
            {
                for (int i = 0; i < heroName.Length; i++) h = h * 31 + heroName[i];
            }
            int idx = ((h + attempt) % Deflections.Length + Deflections.Length) % Deflections.Length;
            return Deflections[idx];
        }

        /// <summary>The prompt rule appended to every live system prompt.</summary>
        public const string PromptRule =
            "Keep everything suitable for a teen audience: no sexual content, slurs, hate, self-harm, " +
            "real-world politics or real people. If asked for any of that, refuse in character in a few words and get back to work. ";

        public static bool Hits(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            string norm = Normalize(text);
            if (norm.Length == 0) return false;

            string spaced = " " + norm + " ";
            for (int i = 0; i < BlockedPhrases.Length; i++)
            {
                if (spaced.Contains(" " + BlockedPhrases[i] + " ")) return true;
            }

            string[] words = norm.Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                if (BlockedWords.Contains(words[i])) return true;
            }

            // "s e x", "n.a.z.i": glue runs of single letters back together and test the result.
            var glued = new StringBuilder();
            for (int i = 0; i <= words.Length; i++)
            {
                bool single = i < words.Length && words[i].Length == 1;
                if (single)
                {
                    glued.Append(words[i]);
                    continue;
                }
                if (glued.Length >= 3 && BlockedWords.Contains(glued.ToString())) return true;
                glued.Length = 0;
            }
            return false;
        }

        /// <summary>Lower-case letters with single spaces; common leetspeak folded to letters.</summary>
        public static string Normalize(string text)
        {
            var sb = new StringBuilder(text.Length);
            bool space = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = char.ToLowerInvariant(text[i]);
                switch (c)
                {
                    case '0': c = 'o'; break;
                    case '1': case '|': c = 'i'; break;
                    case '3': c = 'e'; break;
                    case '4': case '@': c = 'a'; break;
                    case '5': case '$': c = 's'; break;
                    case '7': c = 't'; break;
                    case '8': c = 'b'; break;
                }
                if (c >= 'a' && c <= 'z')
                {
                    if (space && sb.Length > 0) sb.Append(' ');
                    sb.Append(c);
                    space = false;
                }
                else if (c == '\'' || c == '’')
                {
                    // "don't" stays one word
                }
                else
                {
                    space = true;
                }
            }
            return sb.ToString();
        }
    }
}
