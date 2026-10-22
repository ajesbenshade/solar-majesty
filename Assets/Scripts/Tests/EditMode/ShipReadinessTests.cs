using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    public class InputBindingsTests
    {
        private string _dir;

        [SetUp]
        public void SetUp()
        {
            // Rebinds save through GamePrefs; keep them out of the real editor profile.
            _dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sm-bindings-" + System.Guid.NewGuid().ToString("N"));
            GamePrefs.DirectoryOverride = _dir;
            InputBindings.Reload();
            InputBindings.ResetToDefaults();
        }

        [TearDown]
        public void TearDown()
        {
            InputBindings.ResetToDefaults();
            GamePrefs.DirectoryOverride = null;
            InputBindings.Reload();
            if (System.IO.Directory.Exists(_dir)) System.IO.Directory.Delete(_dir, true);
        }

        /// <summary>Saved bindings are action numbers; renumbering would move a player's keys.</summary>
        [Test]
        public void OriginalActions_KeepTheirSavedNumbers()
        {
            GameAction[] original =
            {
                GameAction.PanUp, GameAction.PanDown, GameAction.PanLeft, GameAction.PanRight,
                GameAction.ZoomIn, GameAction.ZoomOut,
                GameAction.TogglePause, GameAction.SpeedUp, GameAction.SpeedDown,
                GameAction.ToolBuild, GameAction.ToolFlag, GameAction.ToolTech, GameAction.ToolParty,
                GameAction.CycleOverlay, GameAction.JumpToAlert,
                GameAction.FocusCampusA, GameAction.FocusCampusB,
                GameAction.Cancel, GameAction.Confirm
            };
            for (int i = 0; i < original.Length; i++)
                Assert.AreEqual(i, (int)original[i], original[i].ToString());
        }

        /// <summary>With default bindings every hotkey is the one the game shipped with.</summary>
        [Test]
        public void Defaults_MatchTheShippedKeys()
        {
            Assert.AreEqual(KeyCode.W, InputBindings.DefaultKey(GameAction.PanUp));
            Assert.AreEqual(KeyCode.S, InputBindings.DefaultKey(GameAction.PanDown));
            Assert.AreEqual(KeyCode.A, InputBindings.DefaultKey(GameAction.PanLeft));
            Assert.AreEqual(KeyCode.D, InputBindings.DefaultKey(GameAction.PanRight));
            Assert.AreEqual(KeyCode.E, InputBindings.DefaultKey(GameAction.ZoomIn));
            Assert.AreEqual(KeyCode.Q, InputBindings.DefaultKey(GameAction.ZoomOut));
            Assert.AreEqual(KeyCode.T, InputBindings.DefaultKey(GameAction.ToolTech));
            Assert.AreEqual(KeyCode.P, InputBindings.DefaultKey(GameAction.ToolParty));
            Assert.AreEqual(KeyCode.N, InputBindings.DefaultKey(GameAction.ToolOrbital));
            Assert.AreEqual(KeyCode.Tab, InputBindings.DefaultKey(GameAction.CycleTool));
            Assert.AreEqual(KeyCode.LeftBracket, InputBindings.DefaultKey(GameAction.DisbandParty));
            Assert.AreEqual(KeyCode.F1, InputBindings.DefaultKey(GameAction.FlagExplore));
            Assert.AreEqual(KeyCode.F2, InputBindings.DefaultKey(GameAction.FlagClearThreat));
            Assert.AreEqual(KeyCode.F3, InputBindings.DefaultKey(GameAction.FlagBuild));
            Assert.AreEqual(KeyCode.F4, InputBindings.DefaultKey(GameAction.FlagExtract));
            Assert.AreEqual(KeyCode.F5, InputBindings.DefaultKey(GameAction.FlagDefend));
            Assert.AreEqual(KeyCode.I, InputBindings.DefaultKey(GameAction.FlagResearchSite));
            Assert.AreEqual(KeyCode.O, InputBindings.DefaultKey(GameAction.FlagOutpost));
            Assert.AreEqual(KeyCode.U, InputBindings.DefaultKey(GameAction.FlagTerraform));
            Assert.AreEqual(KeyCode.Equals, InputBindings.DefaultKey(GameAction.BountyUp));
            Assert.AreEqual(KeyCode.Minus, InputBindings.DefaultKey(GameAction.BountyDown));
            Assert.AreEqual(KeyCode.Y, InputBindings.DefaultKey(GameAction.PayYard));
        }

        [Test]
        public void ExpandKeyTokens_UsesTheCurrentKeys()
        {
            Assert.AreEqual("Post F5/F2.", InputBindings.ExpandKeyTokens("Post {key:FlagDefend}/{key:FlagClearThreat}."));
            Assert.AreEqual("press +", InputBindings.ExpandKeyTokens("press {key:BountyUp}"));
            Assert.AreEqual("no tokens", InputBindings.ExpandKeyTokens("no tokens"));
        }

        [Test]
        public void ExpandKeyTokens_LeavesUnknownTokens()
        {
            const string text = "{key:Nope} {key:3} {Key:PanUp} {key:PanUp";
            Assert.AreEqual(text, InputBindings.ExpandKeyTokens(text));
        }

        [Test]
        public void ExpandKeyTokens_FollowsARebind()
        {
            Assert.IsTrue(InputBindings.TryRebind(GameAction.FlagDefend, KeyCode.K));
            Assert.AreEqual("Post K.", InputBindings.ExpandKeyTokens("Post {key:FlagDefend}."));
            Assert.AreEqual("K", InputBindings.Hint(GameAction.FlagDefend));
        }

        /// <summary>Token copy must read exactly as it did before keys became rebindable.</summary>
        [Test]
        public void TokenCopy_ExpandsToTheOriginalText()
        {
            Assert.AreEqual(
                "4/4  A soil creeper is on the yard. Press G, F5 Defend, and post it on the bug. If the pole says ignored, build a Defense workshop (key 2).",
                InputBindings.ExpandKeyTokens(FirstHourTutorial.PestBeat));
            Assert.AreEqual(
                "1/4  WORKSHOP — press B, then 1. Place the Engineer workshop anywhere near the Commons. Wait until the robot is standing.",
                InputBindings.ExpandKeyTokens(FirstHourTutorial.WorkshopBeat));
            StringAssert.Contains("Soil creepers chew farms (F5).",
                InputBindings.ExpandKeyTokens(CelestialBodyCatalog.Earth().Briefing));
            foreach (CelestialBodyId id in CelestialBodyCatalog.All)
                StringAssert.DoesNotContain("{key:", InputBindings.ExpandKeyTokens(CelestialBodyCatalog.Get(id).Briefing), id.ToString());
        }

        [Test]
        public void Bindings_RoundTripThroughGamePrefs()
        {
            Assert.IsTrue(InputBindings.TryRebind(GameAction.PanUp, KeyCode.Z));
            Assert.IsTrue(System.IO.File.Exists(GamePrefs.FilePath));

            GamePrefs.Reload();
            InputBindings.Reload();

            Assert.AreEqual(KeyCode.Z, InputBindings.Key(GameAction.PanUp));
            Assert.AreEqual(KeyCode.S, InputBindings.Key(GameAction.PanDown));
        }

        /// <summary>
        /// A save from before an action existed: its default is kept unless the player already
        /// used that key, in which case it starts unbound instead of firing twice.
        /// </summary>
        [Test]
        public void OldSave_NewActionDoesNotStealARebindKey()
        {
            GamePrefs.SetString("SM_Bindings", $"{(int)GameAction.ToolBuild}:{(int)KeyCode.Y}");
            InputBindings.Reload();

            Assert.AreEqual(KeyCode.Y, InputBindings.Key(GameAction.ToolBuild));
            Assert.AreEqual(KeyCode.None, InputBindings.Key(GameAction.PayYard));
            Assert.AreEqual(KeyCode.F5, InputBindings.Key(GameAction.FlagDefend));
        }

        [Test]
        public void TryRebind_RefusesReservedKeys()
        {
            Assert.IsFalse(InputBindings.TryRebind(GameAction.ToolBuild, KeyCode.Alpha1));
            Assert.IsFalse(InputBindings.TryRebind(GameAction.ToolBuild, KeyCode.LeftShift));
            Assert.AreEqual(KeyCode.B, InputBindings.Key(GameAction.ToolBuild));
        }

        [Test]
        public void DisplayName_ReadsWellForFlags()
        {
            Assert.AreEqual("Flag: Clear Threat", InputBindings.DisplayName(GameAction.FlagClearThreat));
            Assert.AreEqual("Form Party", InputBindings.DisplayName(GameAction.ToolParty));
        }

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
            Assert.IsTrue(InputBindings.TryRebind(GameAction.ToolBuild, KeyCode.K));
            Assert.AreEqual(KeyCode.K, InputBindings.Key(GameAction.ToolBuild));
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
            InputBindings.TryRebind(GameAction.ToolBuild, KeyCode.K);

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
