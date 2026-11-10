// Shareable seed strings for generated star systems — pure C#.

namespace SolarMajesty
{
    /// <summary>
    /// A star system seed as text a player can type or share ("7K2M-9AX"). Seven Crockford
    /// base-32 characters carry the 32-bit seed. Reading ignores case, spaces and dashes, and
    /// takes O for 0 and I or L for 1, so a code survives being read aloud.
    /// </summary>
    public static class SystemSeedCode
    {
        private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        public const int Length = 7;

        /// <summary>The seed as seven characters with no dash ("7K2M9AX"): the form world keys use.</summary>
        public static string Compact(int seed)
        {
            uint v = unchecked((uint)seed);
            var chars = new char[Length];
            for (int i = Length - 1; i >= 0; i--)
            {
                chars[i] = Alphabet[(int)(v & 31u)];
                v >>= 5;
            }
            return new string(chars);
        }

        /// <summary>The seed as shown to a player ("7K2M-9AX").</summary>
        public static string Format(int seed)
        {
            string c = Compact(seed);
            return c.Substring(0, 4) + "-" + c.Substring(4);
        }

        /// <summary>Read a code in either form. False when it is not seven valid characters or names more than 32 bits.</summary>
        public static bool TryParse(string text, out int seed)
        {
            seed = 0;
            if (string.IsNullOrEmpty(text)) return false;
            ulong v = 0;
            int count = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = char.ToUpperInvariant(text[i]);
                if (c == '-' || c == ' ') continue;
                if (c == 'O') c = '0';
                else if (c == 'I' || c == 'L') c = '1';
                int digit = Alphabet.IndexOf(c);
                if (digit < 0 || ++count > Length) return false;
                v = (v << 5) | (uint)digit;
            }
            if (count != Length || v > uint.MaxValue) return false;
            seed = unchecked((int)(uint)v);
            return true;
        }

        /// <summary>
        /// Turn whatever a player typed into a seed: a code when it reads as one, otherwise a
        /// stable hash of the text, so "my homeworld" is a valid seed too.
        /// </summary>
        public static int FromText(string text)
        {
            if (TryParse(text, out int seed)) return seed;
            uint h = 2166136261u;
            string s = (text ?? "").Trim().ToLowerInvariant();
            for (int i = 0; i < s.Length; i++)
            {
                h ^= s[i];
                h *= 16777619u;
            }
            return unchecked((int)h);
        }
    }
}
