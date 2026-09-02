using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    public class InputBindingsTests
    {
        [SetUp]
        public void SetUp() => InputBindings.ResetToDefaults();

        [TearDown]
        public void TearDown() => InputBindings.ResetToDefaults();

        [Test]
        public void EveryActionHasADefaultKey()
        {
            foreach (GameAction action in System.Enum.GetValues(typeof(GameAction)))
                Assert.AreNotEqual(KeyCode.None, InputBindings.DefaultKey(action), $"{action} has no default");
        }

        [Test]
        public void DefaultsAreUnique()
        {
            var seen = new System.Collections.Generic.Dictionary<KeyCode, GameAction>();

            foreach (GameAction action in System.Enum.GetValues(typeof(GameAction)))
            {
                KeyCode key = InputBindings.DefaultKey(action);
                Assert.IsFalse(seen.ContainsKey(key),
                    $"{action} and {(seen.ContainsKey(key) ? seen[key].ToString() : "")} both default to {key}");
                seen[key] = action;
            }
        }

        [Test]
        public void TryRebind_AssignsAFreeKey()
        {
            Assert.IsTrue(InputBindings.TryRebind(GameAction.ToolBuild, KeyCode.Y));
            Assert.AreEqual(KeyCode.Y, InputBindings.Key(GameAction.ToolBuild));
        }

        /// <summary>Double-assigning would leave one action silently unreachable.</summary>
        [Test]
        public void TryRebind_RefusesAKeyAlreadyInUse()
        {
            KeyCode flagKey = InputBindings.Key(GameAction.ToolFlag);

            Assert.IsFalse(InputBindings.TryRebind(GameAction.ToolBuild, flagKey));
            Assert.AreNotEqual(flagKey, InputBindings.Key(GameAction.ToolBuild));
        }

        [Test]
        public void Conflict_NamesTheOffendingAction()
        {
            KeyCode flagKey = InputBindings.Key(GameAction.ToolFlag);
            GameAction? conflict = InputBindings.Conflict(GameAction.ToolBuild, flagKey);

            Assert.IsTrue(conflict.HasValue);
            Assert.AreEqual(GameAction.ToolFlag, conflict.Value);
        }

        [Test]
        public void Conflict_IgnoresTheActionsOwnKey()
        {
            KeyCode own = InputBindings.Key(GameAction.ToolBuild);

            Assert.IsFalse(InputBindings.Conflict(GameAction.ToolBuild, own).HasValue);
        }

        [Test]
        public void ResetToDefaults_UndoesRebinds()
        {
            KeyCode original = InputBindings.DefaultKey(GameAction.ToolBuild);
            InputBindings.TryRebind(GameAction.ToolBuild, KeyCode.Y);

            InputBindings.ResetToDefaults();

            Assert.AreEqual(original, InputBindings.Key(GameAction.ToolBuild));
            Assert.IsTrue(InputBindings.IsDefault(GameAction.ToolBuild));
        }

        [Test]
        public void DisplayName_SplitsCamelCase()
        {
            Assert.AreEqual("Cycle Overlay", InputBindings.DisplayName(GameAction.CycleOverlay));
            Assert.AreEqual("Pan Up", InputBindings.DisplayName(GameAction.PanUp));
        }

        [Test]
        public void Label_ReadsPunctuationKeys()
        {
            InputBindings.TryRebind(GameAction.Confirm, KeyCode.None);
            Assert.AreEqual(".", InputBindings.Label(GameAction.SpeedUp));
            Assert.AreEqual(",", InputBindings.Label(GameAction.SpeedDown));
            Assert.AreEqual("Space", InputBindings.Label(GameAction.TogglePause));
        }
    }

    public class AccessibilityTests
    {
        [TearDown]
        public void TearDown() => DemoSettings.ColorBlindMode = 0;

        [Test]
        public void Adapt_IsIdentityWhenOff()
        {
            DemoSettings.ColorBlindMode = (int)ColorBlindMode.Off;
            var red = new Color(0.9f, 0.2f, 0.15f);

            Assert.AreEqual(red, Accessibility.Adapt(red));
        }

        /// <summary>Red and green must not converge; that is the whole point of the mode.</summary>
        [Test]
        public void Adapt_SeparatesRedFromGreen()
        {
            DemoSettings.ColorBlindMode = (int)ColorBlindMode.Deuteranopia;

            Color red = Accessibility.Adapt(new Color(0.9f, 0.2f, 0.15f));
            Color green = Accessibility.Adapt(new Color(0.25f, 0.85f, 0.3f));

            float distance = Mathf.Abs(red.r - green.r) + Mathf.Abs(red.g - green.g) + Mathf.Abs(red.b - green.b);
            Assert.Greater(distance, 0.4f, "adapted red and green are still too close");
        }

        [Test]
        public void Adapt_LeavesNeutralsAlone()
        {
            DemoSettings.ColorBlindMode = (int)ColorBlindMode.Deuteranopia;
            var grey = new Color(0.5f, 0.5f, 0.5f);

            Color adapted = Accessibility.Adapt(grey);

            Assert.AreEqual(grey.r, adapted.r, 0.02f);
            Assert.AreEqual(grey.g, adapted.g, 0.02f);
            Assert.AreEqual(grey.b, adapted.b, 0.02f);
        }

        [Test]
        public void Adapt_PreservesAlpha()
        {
            DemoSettings.ColorBlindMode = (int)ColorBlindMode.Deuteranopia;
            var c = new Color(0.9f, 0.2f, 0.15f, 0.42f);

            Assert.AreEqual(0.42f, Accessibility.Adapt(c).a, 1e-3f);
        }

        /// <summary>Colour must never be the only carrier of severity.</summary>
        [Test]
        public void SeverityPrefix_DistinguishesLevelsInText()
        {
            Assert.AreNotEqual(
                Accessibility.SeverityPrefix(AlertSeverity.Critical),
                Accessibility.SeverityPrefix(AlertSeverity.Warning));
            Assert.AreEqual("", Accessibility.SeverityPrefix(AlertSeverity.Info));
        }
    }

    public class LocalizationTests
    {
        [SetUp]
        public void SetUp() => Loc.SetLanguage(Loc.DefaultLanguage);

        [Test]
        public void T_FallsBackToEnglish()
        {
            Assert.AreEqual("Raise Colony Commons", Loc.T("tutorial.commons", "Raise Colony Commons"));
        }

        [Test]
        public void T_NullOrEmptyKey_ReturnsFallback()
        {
            Assert.AreEqual("text", Loc.T(null, "text"));
            Assert.AreEqual("text", Loc.T("", "text"));
        }

        [Test]
        public void SetLanguage_MissingTable_StaysOnEnglish()
        {
            Loc.SetLanguage("zz");

            Assert.AreEqual("English text", Loc.T("any.key", "English text"));
        }

        [Test]
        public void SetLanguage_NullFallsBackToDefault()
        {
            Loc.SetLanguage(null);

            Assert.AreEqual(Loc.DefaultLanguage, Loc.Language);
        }
    }
}
