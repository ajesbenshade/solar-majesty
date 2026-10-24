using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    public class KeyHintsTests
    {
        [Test]
        public void Fill_UsesTheBoundKeys_SoARebindChangesTheHint()
        {
            const string template = "Press {ToolFlag}, then {FlagBuild}. Raise with {BountyUp}, lower with {BountyDown}.";
            Assert.AreEqual("Press G, then F3. Raise with +, lower with −.",
                KeyHints.Fill(template, InputBindings.DefaultKey));

            KeyCode Rebound(GameAction a) => a switch
            {
                GameAction.ToolFlag => KeyCode.H,
                GameAction.FlagBuild => KeyCode.Alpha7,
                GameAction.BountyUp => KeyCode.RightBracket,
                GameAction.BountyDown => KeyCode.LeftBracket,
                _ => InputBindings.DefaultKey(a)
            };
            Assert.AreEqual("Press H, then 7. Raise with ], lower with [.", KeyHints.Fill(template, Rebound));
        }

        [Test]
        public void Fill_LeavesUnknownTokensAndPlainTextAlone()
        {
            Assert.AreEqual("{NotAnAction} stays", KeyHints.Fill("{NotAnAction} stays", InputBindings.DefaultKey));
            Assert.AreEqual("no tokens", KeyHints.Fill("no tokens", InputBindings.DefaultKey));
            Assert.IsNull(KeyHints.Fill(null, InputBindings.DefaultKey));
        }
    }
}
