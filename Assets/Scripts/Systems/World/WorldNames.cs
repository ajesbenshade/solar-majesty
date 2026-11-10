// Names for generated stars and worlds — pure C#.

using System.Text;

namespace SolarMajesty
{
    /// <summary>Seeded names: a star gets two or three syllables, its bodies a Roman numeral.</summary>
    public static class WorldNames
    {
        private static readonly string[] Onsets =
        {
            "b", "c", "d", "f", "g", "h", "k", "l", "m", "n", "p", "r", "s", "t", "v", "z",
            "br", "cr", "dr", "kr", "th", "sh", "tr", "vr", "st", "sk"
        };
        private static readonly string[] Vowels = { "a", "e", "i", "o", "u", "ae", "ia", "eo", "ai", "ou" };
        private static readonly string[] Codas = { "", "", "", "n", "r", "s", "l", "x", "th", "m", "nd", "rn" };
        private static readonly string[] Numerals = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" };

        public static string Star(SimRng rng)
        {
            int syllables = rng.Next(2, 4);
            var sb = new StringBuilder();
            for (int i = 0; i < syllables; i++)
            {
                sb.Append(Onsets[rng.Next(0, Onsets.Length)]);
                sb.Append(Vowels[rng.Next(0, Vowels.Length)]);
                // Only the last syllable closes, so names stay easy to say.
                if (i == syllables - 1) sb.Append(Codas[rng.Next(0, Codas.Length)]);
            }
            sb[0] = char.ToUpperInvariant(sb[0]);
            return sb.ToString();
        }

        public static string Roman(int n) => n >= 1 && n <= Numerals.Length ? Numerals[n - 1] : n.ToString();

        /// <summary>Four capital letters for the HUD and file names, from the star name and the place.</summary>
        public static string ShortCode(string star, int place)
        {
            var sb = new StringBuilder(4);
            for (int i = 0; i < star.Length && sb.Length < 3; i++)
            {
                if (char.IsLetter(star[i])) sb.Append(char.ToUpperInvariant(star[i]));
            }
            while (sb.Length < 3) sb.Append('X');
            sb.Append((char)('0' + place % 10));
            return sb.ToString();
        }
    }
}
