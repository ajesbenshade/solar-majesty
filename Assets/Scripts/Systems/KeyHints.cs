using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Fills key tokens in hint copy from the live bindings, so a rebound key never leaves the
    /// tutorial naming the old one. A token is a GameAction name in braces: "press {ToolBuild}".
    /// </summary>
    public static class KeyHints
    {
        private static readonly Regex Token = new Regex(@"\{([A-Za-z0-9]+)\}", RegexOptions.Compiled);

        /// <summary>The template with every {GameAction} token replaced by its current key.</summary>
        public static string Fill(string template) => Fill(template, InputBindings.Key);

        /// <summary>The same fill against any key lookup (tests pass their own).</summary>
        public static string Fill(string template, Func<GameAction, KeyCode> keyOf)
        {
            if (string.IsNullOrEmpty(template) || keyOf == null) return template;
            return Token.Replace(template, m =>
                Enum.TryParse(m.Groups[1].Value, out GameAction action)
                    ? Label(action, keyOf(action))
                    : m.Value);
        }

        /// <summary>
        /// Hint wording for a key. The bounty keys read as + and − on their default keys
        /// (Equals and Minus), which is what the flag panel buttons show.
        /// </summary>
        public static string Label(GameAction action, KeyCode key)
        {
            if (action == GameAction.BountyUp && (key == KeyCode.Equals || key == KeyCode.Plus || key == KeyCode.KeypadPlus))
                return "+";
            if (action == GameAction.BountyDown && (key == KeyCode.Minus || key == KeyCode.KeypadMinus))
                return "−";
            return InputBindings.KeyLabel(key);
        }
    }
}
