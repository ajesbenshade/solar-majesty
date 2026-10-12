using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// Runtime input goes through InputBindings, so with untouched bindings every key must do
    /// exactly what its literal KeyCode did before. These pin that, the saved-prefs format, and
    /// the controls screen's list.
    /// </summary>
    public class InputRoutingTests
    {
        [SetUp]
        public void SetUp() => InputBindings.ResetToDefaults();

        [TearDown]
        public void TearDown() => InputBindings.ResetToDefaults();

        /// <summary>The keys the runtime read as literals before routing (GameLoop, camera, flags, build, HUD).</summary>
        private static readonly Dictionary<GameAction, KeyCode> LegacyKeys = new Dictionary<GameAction, KeyCode>
        {
            { GameAction.PanUp, KeyCode.W }, { GameAction.PanDown, KeyCode.S },
            { GameAction.PanLeft, KeyCode.A }, { GameAction.PanRight, KeyCode.D },
            { GameAction.ZoomIn, KeyCode.E }, { GameAction.ZoomOut, KeyCode.Q },
            { GameAction.TogglePause, KeyCode.Space }, { GameAction.SpeedUp, KeyCode.Period },
            { GameAction.SpeedDown, KeyCode.Comma },
            { GameAction.ToolBuild, KeyCode.B }, { GameAction.ToolFlag, KeyCode.G },
            { GameAction.ToolTech, KeyCode.T }, { GameAction.ToolParty, KeyCode.P },
            { GameAction.CycleOverlay, KeyCode.V }, { GameAction.JumpToAlert, KeyCode.Backspace },
            { GameAction.FocusCampusA, KeyCode.F6 }, { GameAction.FocusCampusB, KeyCode.F7 },
            { GameAction.Cancel, KeyCode.Escape }, { GameAction.Confirm, KeyCode.Return },
            { GameAction.CycleTool, KeyCode.Tab }, { GameAction.ToolOrbital, KeyCode.N },
            { GameAction.DisbandParty, KeyCode.LeftBracket },
            { GameAction.BuildSlot1, KeyCode.Alpha1 }, { GameAction.BuildSlot2, KeyCode.Alpha2 },
            { GameAction.BuildSlot3, KeyCode.Alpha3 }, { GameAction.BuildSlot4, KeyCode.Alpha4 },
            { GameAction.BuildSlot5, KeyCode.Alpha5 }, { GameAction.BuildSlot6, KeyCode.Alpha6 },
            { GameAction.BuildSlot7, KeyCode.Alpha7 }, { GameAction.BuildSlot8, KeyCode.Alpha8 },
            { GameAction.BuildSlot9, KeyCode.Alpha9 }, { GameAction.BuildSlot10, KeyCode.Alpha0 },
            { GameAction.FlagExplore, KeyCode.F1 }, { GameAction.FlagClearThreat, KeyCode.F2 },
            { GameAction.FlagBuild, KeyCode.F3 }, { GameAction.FlagExtract, KeyCode.F4 },
            { GameAction.FlagDefend, KeyCode.F5 }, { GameAction.FlagResearchSite, KeyCode.I },
            { GameAction.FlagOutpost, KeyCode.O }, { GameAction.FlagTerraform, KeyCode.U },
            { GameAction.BountyUp, KeyCode.Equals }, { GameAction.BountyDown, KeyCode.Minus },
            { GameAction.PromptYes, KeyCode.Y },
            { GameAction.DebugHud, KeyCode.F8 }, { GameAction.DebugSeedCampusB, KeyCode.F9 },
            { GameAction.DebugNextWorld, KeyCode.F10 }, { GameAction.DebugFatigue, KeyCode.R }
        };

        [Test]
        public void DefaultsAreTheKeysTheGameAlwaysUsed()
        {
            foreach (GameAction action in System.Enum.GetValues(typeof(GameAction)))
            {
                Assert.IsTrue(LegacyKeys.ContainsKey(action), $"{action} has no legacy key pinned here");
                Assert.AreEqual(LegacyKeys[action], InputBindings.DefaultKey(action), action.ToString());
                Assert.AreEqual(LegacyKeys[action], InputBindings.Key(action), action.ToString());
            }
        }

        [Test]
        public void KeypadTwinsAreTheOldSecondKeys()
        {
            Assert.AreEqual(KeyCode.KeypadPlus, InputBindings.Alias(GameAction.BountyUp));
            Assert.AreEqual(KeyCode.KeypadMinus, InputBindings.Alias(GameAction.BountyDown));
            Assert.AreEqual(KeyCode.KeypadEnter, InputBindings.Alias(GameAction.Confirm));
            Assert.AreEqual(KeyCode.None, InputBindings.Alias(GameAction.ToolBuild));
        }

        /// <summary>Saved bindings are "action:key" integers, so the first table's ids must never move.</summary>
        [Test]
        public void OriginalActionIdsAreStable()
        {
            Assert.AreEqual(0, (int)GameAction.PanUp);
            Assert.AreEqual(4, (int)GameAction.ZoomIn);
            Assert.AreEqual(6, (int)GameAction.TogglePause);
            Assert.AreEqual(9, (int)GameAction.ToolBuild);
            Assert.AreEqual(13, (int)GameAction.CycleOverlay);
            Assert.AreEqual(15, (int)GameAction.FocusCampusA);
            Assert.AreEqual(17, (int)GameAction.Cancel);
            Assert.AreEqual(18, (int)GameAction.Confirm);
            Assert.Greater((int)GameAction.CycleTool, (int)GameAction.Confirm);
        }

        [Test]
        public void BuildSlotsFollowTheNumberRow()
        {
            Assert.AreEqual(GameAction.BuildSlot1, InputBindings.BuildSlot(0));
            Assert.AreEqual(GameAction.BuildSlot9, InputBindings.BuildSlot(8));
            Assert.AreEqual(GameAction.BuildSlot10, InputBindings.BuildSlot(9));
            Assert.AreEqual(KeyCode.Alpha0, InputBindings.Key(InputBindings.BuildSlot(9)));
        }

        [Test]
        public void CancelStaysOnEscape()
        {
            Assert.IsTrue(InputBindings.IsFixed(GameAction.Cancel));
            Assert.IsFalse(InputBindings.TryRebind(GameAction.Cancel, KeyCode.H));
            Assert.AreEqual(KeyCode.Escape, InputBindings.Key(GameAction.Cancel));
            Assert.AreEqual(InputBindings.RebindCheck.Fixed, InputBindings.Check(GameAction.Cancel, KeyCode.H, out _));
        }

        [Test]
        public void ReservedKeysAreRefused()
        {
            Assert.AreEqual(InputBindings.RebindCheck.Reserved, InputBindings.Check(GameAction.ToolBuild, KeyCode.Escape, out _));
            Assert.AreEqual(InputBindings.RebindCheck.Reserved, InputBindings.Check(GameAction.ToolBuild, KeyCode.LeftShift, out _));
            Assert.AreEqual(InputBindings.RebindCheck.Reserved, InputBindings.Check(GameAction.ToolBuild, KeyCode.Mouse0, out _));
            Assert.IsFalse(InputBindings.TryRebind(GameAction.ToolBuild, KeyCode.RightShift));
            Assert.AreEqual(KeyCode.B, InputBindings.Key(GameAction.ToolBuild));
        }

        [Test]
        public void AKeypadTwinCountsAsTaken()
        {
            Assert.AreEqual(GameAction.BountyUp, InputBindings.Conflict(GameAction.ToolBuild, KeyCode.KeypadPlus));
            Assert.AreEqual(InputBindings.RebindCheck.Conflict,
                InputBindings.Check(GameAction.ToolBuild, KeyCode.KeypadEnter, out GameAction other));
            Assert.AreEqual(GameAction.Confirm, other);
        }

        [Test]
        public void Merge_EmptySaveIsTheDefaults()
        {
            var merged = InputBindings.Merge("");
            foreach (GameAction action in System.Enum.GetValues(typeof(GameAction)))
                Assert.AreEqual(InputBindings.DefaultKey(action), merged[action]);
        }

        /// <summary>
        /// A save from before the new actions existed: all 19 original entries, with Build moved to
        /// Y. Y is now the banner prompt's default, so the save wins and the prompt starts unbound.
        /// </summary>
        [Test]
        public void Merge_OldSaveKeepsItsChoicesAndNeverDoubleBinds()
        {
            var sb = new System.Text.StringBuilder();
            for (int a = 0; a <= (int)GameAction.Confirm; a++)
            {
                KeyCode key = (GameAction)a == GameAction.ToolBuild ? KeyCode.Y : InputBindings.DefaultKey((GameAction)a);
                if (a > 0) sb.Append(';');
                sb.Append(a).Append(':').Append((int)key);
            }

            var merged = InputBindings.Merge(sb.ToString());

            Assert.AreEqual(KeyCode.Y, merged[GameAction.ToolBuild]);
            Assert.AreEqual(KeyCode.None, merged[GameAction.PromptYes]);
            Assert.AreEqual(KeyCode.N, merged[GameAction.ToolOrbital]);
            Assert.AreEqual(KeyCode.Alpha1, merged[GameAction.BuildSlot1]);
            Assert.AreEqual(KeyCode.W, merged[GameAction.PanUp]);
        }

        [Test]
        public void Merge_IgnoresJunkAndFixedActions()
        {
            var merged = InputBindings.Merge($"x;17:{(int)KeyCode.H};9:abc;999:1;{(int)GameAction.ToolFlag}:{(int)KeyCode.J}");

            Assert.AreEqual(KeyCode.Escape, merged[GameAction.Cancel]);
            Assert.AreEqual(KeyCode.B, merged[GameAction.ToolBuild]);
            Assert.AreEqual(KeyCode.J, merged[GameAction.ToolFlag]);
        }

        [Test]
        public void KeyLabel_ReadsTheNumberRowAndPunctuation()
        {
            Assert.AreEqual("1", InputBindings.KeyLabel(KeyCode.Alpha1));
            Assert.AreEqual("0", InputBindings.KeyLabel(KeyCode.Alpha0));
            Assert.AreEqual("[", InputBindings.KeyLabel(KeyCode.LeftBracket));
            Assert.AreEqual("=", InputBindings.KeyLabel(KeyCode.Equals));
            Assert.AreEqual("-", InputBindings.KeyLabel(KeyCode.Minus));
            Assert.AreEqual("Keypad +", InputBindings.KeyLabel(KeyCode.KeypadPlus));
            Assert.AreEqual("F10", InputBindings.KeyLabel(KeyCode.F10));
            Assert.AreEqual("—", InputBindings.KeyLabel(KeyCode.None));
        }

        [Test]
        public void DisplayName_SpacesNumbers()
        {
            Assert.AreEqual("Build Slot 10", InputBindings.DisplayName(GameAction.BuildSlot10));
            Assert.AreEqual("Focus Campus A", InputBindings.DisplayName(GameAction.FocusCampusA));
        }
    }

    public class ControlsListTests
    {
        private static KeyCode Defaults(GameAction action) => InputBindings.DefaultKey(action);

        [Test]
        public void EveryActionIsListedExactlyOnce()
        {
            var seen = new HashSet<GameAction>();
            foreach (var group in ControlsList.Build(Defaults))
            {
                Assert.IsNotEmpty(group.Rows, group.Title);
                foreach (var row in group.Rows)
                    Assert.IsTrue(seen.Add(row.Action), $"{row.Action} listed twice");
            }
            foreach (GameAction action in System.Enum.GetValues(typeof(GameAction)))
                Assert.IsTrue(seen.Contains(action), $"{action} missing from the controls screen");
        }

        [Test]
        public void KnownActionsAreGroupedNotDumpedInOther()
        {
            foreach (var group in ControlsList.Build(Defaults))
                Assert.AreNotEqual("OTHER", group.Title, "every current action has a group");
        }

        [Test]
        public void RowsShowKeysTwinsAndFixedState()
        {
            var rows = new Dictionary<GameAction, ControlsList.Row>();
            foreach (var group in ControlsList.Build(Defaults))
                foreach (var row in group.Rows)
                    rows[row.Action] = row;

            Assert.AreEqual("B", rows[GameAction.ToolBuild].Key);
            Assert.AreEqual("= / Keypad +", rows[GameAction.BountyUp].Key);
            Assert.AreEqual("Enter / Keypad Enter", rows[GameAction.Confirm].Key);
            Assert.IsTrue(rows[GameAction.Cancel].Fixed);
            Assert.IsFalse(rows[GameAction.ToolBuild].Fixed);
            Assert.IsTrue(rows[GameAction.ToolBuild].IsDefault);
            Assert.AreEqual("Research panel", rows[GameAction.ToolTech].Name);
            Assert.AreEqual("Build Slot 1", rows[GameAction.BuildSlot1].Name);
        }

        [Test]
        public void ARebindShowsAsNotDefault()
        {
            var groups = ControlsList.Build(a => a == GameAction.ToolBuild ? KeyCode.H : Defaults(a));
            foreach (var group in groups)
                foreach (var row in group.Rows)
                {
                    if (row.Action != GameAction.ToolBuild) continue;
                    Assert.AreEqual("H", row.Key);
                    Assert.IsFalse(row.IsDefault);
                    return;
                }
            Assert.Fail("Build tool row missing");
        }

        [Test]
        public void CaptureStep_EscCancelsAndCharactersAreIgnored()
        {
            Assert.AreEqual(ControlsList.CaptureStep.Cancel, ControlsList.CaptureStepFor(KeyCode.Escape));
            Assert.AreEqual(ControlsList.CaptureStep.Ignore, ControlsList.CaptureStepFor(KeyCode.None));
            Assert.AreEqual(ControlsList.CaptureStep.Assign, ControlsList.CaptureStepFor(KeyCode.H));
        }

        [Test]
        public void Message_NamesTheActionAlreadyUsingTheKey()
        {
            string msg = ControlsList.Message(InputBindings.RebindCheck.Conflict,
                GameAction.ToolBuild, KeyCode.G, GameAction.ToolFlag);
            StringAssert.Contains("G is already Flag tool", msg);
        }

        [Test]
        public void Message_ExplainsReservedAndFixed()
        {
            StringAssert.Contains("Shift is the modifier key", ControlsList.Message(
                InputBindings.RebindCheck.Reserved, GameAction.ToolBuild, KeyCode.LeftShift, GameAction.ToolBuild));
            StringAssert.Contains("Esc", ControlsList.Message(
                InputBindings.RebindCheck.Fixed, GameAction.Cancel, KeyCode.H, GameAction.Cancel));
            StringAssert.Contains("Build tool is now H", ControlsList.Message(
                InputBindings.RebindCheck.Ok, GameAction.ToolBuild, KeyCode.H, GameAction.ToolBuild));
        }

        [Test]
        public void ControllerLayoutIsListed()
        {
            var buttons = new HashSet<string>();
            foreach (var row in ControlsList.ControllerRows)
                buttons.Add(row.Button);
            foreach (string b in new[] { "A", "B", "X", "Y", "LB / RB", "Start", "Back" })
                Assert.IsTrue(buttons.Contains(b), b);
        }
    }
}
