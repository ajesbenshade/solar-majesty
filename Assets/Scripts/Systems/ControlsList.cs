using System;
using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// What the Settings → CONTROLS screen shows, built from <see cref="InputBindings"/> so a new
    /// <see cref="GameAction"/> appears on it without touching the HUD. Pure: the HUD only draws
    /// these rows and forwards the key the player pressed.
    /// </summary>
    public static class ControlsList
    {
        public struct Row
        {
            public GameAction Action;
            public string Name;
            /// <summary>The binding, plus its keypad twin when it has one ("= / Keypad +").</summary>
            public string Key;
            /// <summary>Shown but not clickable (Cancel stays on Esc).</summary>
            public bool Fixed;
            public bool IsDefault;
        }

        public sealed class Group
        {
            public string Title;
            public readonly List<Row> Rows = new List<Row>();
        }

        /// <summary>What a key press does while the screen is waiting for a new key.</summary>
        public enum CaptureStep { Ignore, Cancel, Assign }

        /// <summary>Groups in screen order. An action missing from here lands in OTHER.</summary>
        private static readonly (string Title, GameAction[] Actions)[] Layout =
        {
            ("CAMERA", new[]
            {
                GameAction.PanUp, GameAction.PanDown, GameAction.PanLeft, GameAction.PanRight,
                GameAction.ZoomOut, GameAction.ZoomIn,
                GameAction.FocusCampusA, GameAction.FocusCampusB, GameAction.JumpToAlert
            }),
            ("SPEED AND MENUS", new[]
            {
                GameAction.TogglePause, GameAction.SpeedDown, GameAction.SpeedUp,
                GameAction.ToolTech, GameAction.Cancel
            }),
            ("TOOLS", new[]
            {
                GameAction.CycleTool, GameAction.ToolBuild, GameAction.ToolFlag, GameAction.ToolOrbital,
                GameAction.ToolParty, GameAction.DisbandParty, GameAction.Confirm, GameAction.CycleOverlay
            }),
            ("BUILD MENU", new[]
            {
                GameAction.BuildSlot1, GameAction.BuildSlot2, GameAction.BuildSlot3, GameAction.BuildSlot4,
                GameAction.BuildSlot5, GameAction.BuildSlot6, GameAction.BuildSlot7, GameAction.BuildSlot8,
                GameAction.BuildSlot9, GameAction.BuildSlot10
            }),
            ("FLAGS", new[]
            {
                GameAction.FlagExplore, GameAction.FlagClearThreat, GameAction.FlagBuild,
                GameAction.FlagExtract, GameAction.FlagDefend, GameAction.FlagResearchSite,
                GameAction.FlagOutpost, GameAction.FlagTerraform,
                GameAction.BountyUp, GameAction.BountyDown
            }),
            ("BANNERS", new[] { GameAction.PromptYes }),
            ("DEBUG", new[]
            {
                GameAction.DebugHud, GameAction.DebugSeedCampusB, GameAction.DebugNextWorld,
                GameAction.DebugFatigue
            })
        };

        /// <summary>
        /// Clearer wording where the enum name alone is vague. Everything else uses
        /// <see cref="InputBindings.DisplayName"/>.
        /// </summary>
        private static readonly Dictionary<GameAction, string> Names = new Dictionary<GameAction, string>
        {
            { GameAction.ZoomIn, "Zoom in" },
            { GameAction.ZoomOut, "Zoom out" },
            { GameAction.JumpToAlert, "Jump to the most urgent alert" },
            { GameAction.TogglePause, "Hold / resume the world" },
            { GameAction.SpeedDown, "Slower" },
            { GameAction.SpeedUp, "Faster" },
            { GameAction.ToolTech, "Research panel" },
            { GameAction.Cancel, "Back / pause menu" },
            { GameAction.CycleTool, "Cycle Flag → Build → none" },
            { GameAction.ToolBuild, "Build tool" },
            { GameAction.ToolFlag, "Flag tool" },
            { GameAction.ToolOrbital, "Orbital tool" },
            { GameAction.ToolParty, "Form a party" },
            { GameAction.DisbandParty, "Disband the party" },
            { GameAction.Confirm, "Talk to the hero / dismiss a card" },
            { GameAction.CycleOverlay, "Map overlay" },
            { GameAction.FlagExplore, "Explore flag" },
            { GameAction.FlagClearThreat, "Clear Threat flag" },
            { GameAction.FlagBuild, "Build flag" },
            { GameAction.FlagExtract, "Extract flag" },
            { GameAction.FlagDefend, "Defend flag" },
            { GameAction.FlagResearchSite, "Research Site flag" },
            { GameAction.FlagOutpost, "Outpost flag" },
            { GameAction.FlagTerraform, "Terraform flag" },
            { GameAction.BountyUp, "Raise the bounty" },
            { GameAction.BountyDown, "Lower the bounty" },
            { GameAction.PromptYes, "Pay the Fobot Yard / keep overseeing" },
            { GameAction.DebugHud, "Debug HUD" },
            { GameAction.DebugSeedCampusB, "Seed a Campus B bounty" },
            { GameAction.DebugNextWorld, "Next world (Shift: unlock all)" },
            { GameAction.DebugFatigue, "Fatigue every robot" }
        };

        /// <summary>
        /// The controller layout (Docs/CONTROLS.md, Runtime/Camera/GamepadInput.cs). Read-only:
        /// controller buttons are fixed in the Input Manager, not in this table.
        /// </summary>
        public static readonly (string Button, string Action)[] ControllerRows =
        {
            ("Left stick / D-pad", "Pan"),
            ("Right stick X", "Orbit"),
            ("Right stick Y, LT / RT", "Zoom out / in"),
            ("A", "Jump to the most urgent alert"),
            ("B", "Back out of research or a placement tool"),
            ("X", "Hold / resume the world"),
            ("Y", "Research panel"),
            ("LB / RB", "Slower / faster"),
            ("Start", "Pause menu (closes Settings)"),
            ("Back", "Unassigned")
        };

        public static string NameOf(GameAction action) =>
            Names.TryGetValue(action, out string name) ? name : InputBindings.DisplayName(action);

        /// <summary>The live table, grouped for the screen.</summary>
        public static List<Group> Build() => Build(InputBindings.Key);

        /// <summary>The table for any key lookup (tests pass their own).</summary>
        public static List<Group> Build(Func<GameAction, KeyCode> keyOf)
        {
            var groups = new List<Group>();
            var listed = new HashSet<GameAction>();

            for (int g = 0; g < Layout.Length; g++)
            {
                var group = new Group { Title = Layout[g].Title };
                GameAction[] actions = Layout[g].Actions;
                for (int i = 0; i < actions.Length; i++)
                {
                    if (!listed.Add(actions[i])) continue;
                    group.Rows.Add(MakeRow(actions[i], keyOf));
                }
                if (group.Rows.Count > 0) groups.Add(group);
            }

            var other = new Group { Title = "OTHER" };
            foreach (GameAction action in Enum.GetValues(typeof(GameAction)))
            {
                if (listed.Add(action))
                    other.Rows.Add(MakeRow(action, keyOf));
            }
            if (other.Rows.Count > 0) groups.Add(other);
            return groups;
        }

        private static Row MakeRow(GameAction action, Func<GameAction, KeyCode> keyOf)
        {
            KeyCode key = keyOf(action);
            string label = InputBindings.KeyLabel(key);
            KeyCode alias = InputBindings.Alias(action);
            if (alias != KeyCode.None)
                label += " / " + InputBindings.KeyLabel(alias);

            return new Row
            {
                Action = action,
                Name = NameOf(action),
                Key = label,
                Fixed = InputBindings.IsFixed(action),
                IsDefault = key == InputBindings.DefaultKey(action)
            };
        }

        /// <summary>Esc cancels a pending rebind; a key event with no key (a typed character) is ignored.</summary>
        public static CaptureStep CaptureStepFor(KeyCode pressed)
        {
            if (pressed == KeyCode.None) return CaptureStep.Ignore;
            if (pressed == KeyCode.Escape) return CaptureStep.Cancel;
            return CaptureStep.Assign;
        }

        /// <summary>The line shown under the list after a rebind attempt.</summary>
        public static string Message(
            InputBindings.RebindCheck check, GameAction action, KeyCode key, GameAction conflictWith)
        {
            string keyName = InputBindings.KeyLabel(key);
            switch (check)
            {
                case InputBindings.RebindCheck.Ok:
                    return $"{NameOf(action)} is now {keyName}.";
                case InputBindings.RebindCheck.Fixed:
                    return $"{NameOf(action)} always stays on {InputBindings.KeyLabel(InputBindings.DefaultKey(action))}.";
                case InputBindings.RebindCheck.Reserved:
                    return $"{keyName} can't be bound: {InputBindings.ReservedReason(key)}.";
                case InputBindings.RebindCheck.Conflict:
                    return $"{keyName} is already {NameOf(conflictWith)}. Move that first, or pick another key.";
                default:
                    return "";
            }
        }
    }
}
