using System;
using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Every rebindable action. Adding one here and giving it a default is all that is needed for
    /// it to appear in the controls screen.
    /// </summary>
    public enum GameAction
    {
        PanUp, PanDown, PanLeft, PanRight,
        ZoomIn, ZoomOut,
        TogglePause, SpeedUp, SpeedDown,
        ToolBuild, ToolFlag, ToolTech, ToolParty,
        CycleOverlay, JumpToAlert,
        FocusCampusA, FocusCampusB,
        Cancel, Confirm,

        // Everything below was added after the first table shipped. Saved bindings store each
        // action's integer value, so new actions only ever go on the end of this list.
        CycleTool, ToolOrbital, DisbandParty,
        BuildSlot1, BuildSlot2, BuildSlot3, BuildSlot4, BuildSlot5,
        BuildSlot6, BuildSlot7, BuildSlot8, BuildSlot9, BuildSlot10,
        FlagExplore, FlagClearThreat, FlagBuild, FlagExtract, FlagDefend,
        FlagResearchSite, FlagOutpost, FlagTerraform,
        BountyUp, BountyDown,
        PromptYes,
        DebugHud, DebugSeedCampusB, DebugNextWorld, DebugFatigue
    }

    /// <summary>
    /// Keyboard rebinding.
    ///
    /// The project had roughly a hundred literal KeyCode checks scattered through the runtime, so
    /// no key could be changed and no controls screen could exist. Input now resolves through this
    /// table, which also means the controls screen is generated from the action list rather than
    /// hand-maintained alongside it.
    ///
    /// A binding is rejected if it would shadow an existing one, so a player cannot lock themselves
    /// out of a control by double-assigning a key.
    /// </summary>
    public static class InputBindings
    {
        private const string PrefsKey = "SM_Bindings";

        private static readonly Dictionary<GameAction, KeyCode> Defaults = new Dictionary<GameAction, KeyCode>
        {
            { GameAction.PanUp, KeyCode.W },
            { GameAction.PanDown, KeyCode.S },
            { GameAction.PanLeft, KeyCode.A },
            { GameAction.PanRight, KeyCode.D },
            { GameAction.ZoomIn, KeyCode.E },
            { GameAction.ZoomOut, KeyCode.Q },
            { GameAction.TogglePause, KeyCode.Space },
            { GameAction.SpeedUp, KeyCode.Period },
            { GameAction.SpeedDown, KeyCode.Comma },
            { GameAction.ToolBuild, KeyCode.B },
            { GameAction.ToolFlag, KeyCode.G },
            { GameAction.ToolTech, KeyCode.T },
            { GameAction.ToolParty, KeyCode.P },
            { GameAction.CycleOverlay, KeyCode.V },
            { GameAction.JumpToAlert, KeyCode.Backspace },
            { GameAction.FocusCampusA, KeyCode.F6 },
            { GameAction.FocusCampusB, KeyCode.F7 },
            { GameAction.Cancel, KeyCode.Escape },
            { GameAction.Confirm, KeyCode.Return },
            { GameAction.CycleTool, KeyCode.Tab },
            { GameAction.ToolOrbital, KeyCode.N },
            { GameAction.DisbandParty, KeyCode.LeftBracket },
            { GameAction.BuildSlot1, KeyCode.Alpha1 },
            { GameAction.BuildSlot2, KeyCode.Alpha2 },
            { GameAction.BuildSlot3, KeyCode.Alpha3 },
            { GameAction.BuildSlot4, KeyCode.Alpha4 },
            { GameAction.BuildSlot5, KeyCode.Alpha5 },
            { GameAction.BuildSlot6, KeyCode.Alpha6 },
            { GameAction.BuildSlot7, KeyCode.Alpha7 },
            { GameAction.BuildSlot8, KeyCode.Alpha8 },
            { GameAction.BuildSlot9, KeyCode.Alpha9 },
            { GameAction.BuildSlot10, KeyCode.Alpha0 },
            { GameAction.FlagExplore, KeyCode.F1 },
            { GameAction.FlagClearThreat, KeyCode.F2 },
            { GameAction.FlagBuild, KeyCode.F3 },
            { GameAction.FlagExtract, KeyCode.F4 },
            { GameAction.FlagDefend, KeyCode.F5 },
            { GameAction.FlagResearchSite, KeyCode.I },
            { GameAction.FlagOutpost, KeyCode.O },
            { GameAction.FlagTerraform, KeyCode.U },
            { GameAction.BountyUp, KeyCode.Equals },
            { GameAction.BountyDown, KeyCode.Minus },
            { GameAction.PromptYes, KeyCode.Y },
            { GameAction.DebugHud, KeyCode.F8 },
            { GameAction.DebugSeedCampusB, KeyCode.F9 },
            { GameAction.DebugNextWorld, KeyCode.F10 },
            { GameAction.DebugFatigue, KeyCode.R }
        };

        /// <summary>
        /// Keypad twins that always fire their action as well as its binding, whatever the
        /// binding is. They count as taken, so nothing else can be bound to them.
        /// </summary>
        private static readonly Dictionary<GameAction, KeyCode> Aliases = new Dictionary<GameAction, KeyCode>
        {
            { GameAction.BountyUp, KeyCode.KeypadPlus },
            { GameAction.BountyDown, KeyCode.KeypadMinus },
            { GameAction.Confirm, KeyCode.KeypadEnter }
        };

        /// <summary>Why a rebind would be refused, or Ok.</summary>
        public enum RebindCheck { Ok, NoKey, Fixed, Reserved, Conflict }

        private static readonly Dictionary<GameAction, KeyCode> Current =
            new Dictionary<GameAction, KeyCode>(Defaults);

        private static bool _loaded;

        public static event Action Changed;

        public static IReadOnlyDictionary<GameAction, KeyCode> Bindings
        {
            get
            {
                Load();
                return Current;
            }
        }

        public static KeyCode Key(GameAction action)
        {
            Load();
            return Current.TryGetValue(action, out KeyCode key) ? key : KeyCode.None;
        }

        public static KeyCode DefaultKey(GameAction action) =>
            Defaults.TryGetValue(action, out KeyCode key) ? key : KeyCode.None;

        public static bool IsDefault(GameAction action) => Key(action) == DefaultKey(action);

        /// <summary>The keypad twin that also fires this action, or None.</summary>
        public static KeyCode Alias(GameAction action) =>
            Aliases.TryGetValue(action, out KeyCode key) ? key : KeyCode.None;

        /// <summary>
        /// Cancel stays on Esc: it is the universal back key (pause menu, closing panels, text
        /// boxes, and cancelling a rebind on the controls screen), so moving it could strand
        /// the player.
        /// </summary>
        public static bool IsFixed(GameAction action) => action == GameAction.Cancel;

        /// <summary>
        /// Keys no action may take, with the reason the controls screen shows, or null. Esc is
        /// the fixed back key, Shift is the modifier (keep placing, add to the selection, play a
        /// locked world), and mouse buttons are clicks rather than hotkeys.
        /// </summary>
        public static string ReservedReason(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.Escape: return "Esc is always Back";
                case KeyCode.LeftShift:
                case KeyCode.RightShift: return "Shift is the modifier key";
            }
            if (key >= KeyCode.Mouse0 && key <= KeyCode.Mouse6) return "mouse buttons are not hotkeys";
            return null;
        }

        /// <summary>The action for a 0-based build menu row (keys 1 … 9, then 0).</summary>
        public static GameAction BuildSlot(int index) =>
            (GameAction)((int)GameAction.BuildSlot1 + Mathf.Clamp(index, 0, 9));

        /// <summary>
        /// True while an in-game text box (flag orders) has keyboard focus. Every hotkey reader
        /// stands down so typing does not trigger gameplay keys.
        /// </summary>
        public static bool TextEntryActive;

        public static bool Down(GameAction action)
        {
            if (TextEntryActive) return false;
            KeyCode key = Key(action);
            return key != KeyCode.None && Input.GetKeyDown(key);
        }

        public static bool Held(GameAction action)
        {
            if (TextEntryActive) return false;
            KeyCode key = Key(action);
            return key != KeyCode.None && Input.GetKey(key);
        }

        /// <summary><see cref="Down"/>, or the action's keypad twin (see <see cref="Alias"/>).</summary>
        public static bool DownOrAlias(GameAction action)
        {
            if (Down(action)) return true;
            if (TextEntryActive) return false;
            KeyCode alias = Alias(action);
            return alias != KeyCode.None && Input.GetKeyDown(alias);
        }

        /// <summary>
        /// Pressed this frame even while a text box has focus. Only for the banner prompts that
        /// never checked <see cref="TextEntryActive"/>, so they keep behaving exactly as before.
        /// </summary>
        public static bool DownEvenWhileTyping(GameAction action)
        {
            KeyCode key = Key(action);
            return key != KeyCode.None && Input.GetKeyDown(key);
        }

        /// <summary>True when an IMGUI key event's key is this action's binding.</summary>
        public static bool Matches(GameAction action, KeyCode pressed) =>
            pressed != KeyCode.None && Key(action) == pressed;

        /// <summary>The action already using this key (as its binding or keypad twin), or null.</summary>
        public static GameAction? Conflict(GameAction action, KeyCode key)
        {
            Load();
            foreach (var kv in Current)
            {
                if (kv.Key != action && kv.Value == key)
                    return kv.Key;
            }
            foreach (var kv in Aliases)
            {
                if (kv.Key != action && kv.Value == key)
                    return kv.Key;
            }
            return null;
        }

        /// <summary>Whether a rebind would be accepted, and if not, why.</summary>
        public static RebindCheck Check(GameAction action, KeyCode key, out GameAction conflictWith)
        {
            conflictWith = action;
            if (key == KeyCode.None) return RebindCheck.NoKey;
            if (IsFixed(action)) return RebindCheck.Fixed;
            if (ReservedReason(key) != null) return RebindCheck.Reserved;
            GameAction? other = Conflict(action, key);
            if (other.HasValue)
            {
                conflictWith = other.Value;
                return RebindCheck.Conflict;
            }
            return RebindCheck.Ok;
        }

        /// <summary>Assign a key. Fails rather than creating a duplicate the player cannot see.</summary>
        public static bool TryRebind(GameAction action, KeyCode key)
        {
            Load();
            if (Check(action, key, out _) != RebindCheck.Ok) return false;

            Current[action] = key;
            Save();
            Changed?.Invoke();
            return true;
        }

        public static void ResetToDefaults()
        {
            Load();
            Current.Clear();
            foreach (var kv in Defaults)
                Current[kv.Key] = kv.Value;
            Save();
            Changed?.Invoke();
        }

        /// <summary>Readable key name for the controls screen and hint text.</summary>
        public static string Label(GameAction action) => KeyLabel(Key(action));

        /// <summary>Readable name for any key: "1" rather than "Alpha1", "[" rather than "LeftBracket".</summary>
        public static string KeyLabel(KeyCode key)
        {
            if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9)
                return ((int)(key - KeyCode.Alpha0)).ToString();
            if (key >= KeyCode.Keypad0 && key <= KeyCode.Keypad9)
                return "Keypad " + (int)(key - KeyCode.Keypad0);
            switch (key)
            {
                case KeyCode.None: return "—";
                case KeyCode.Space: return "Space";
                case KeyCode.Return: return "Enter";
                case KeyCode.KeypadEnter: return "Keypad Enter";
                case KeyCode.KeypadPlus: return "Keypad +";
                case KeyCode.KeypadMinus: return "Keypad -";
                case KeyCode.KeypadMultiply: return "Keypad *";
                case KeyCode.KeypadDivide: return "Keypad /";
                case KeyCode.KeypadPeriod: return "Keypad .";
                case KeyCode.Escape: return "Esc";
                case KeyCode.Backspace: return "Backspace";
                case KeyCode.Comma: return ",";
                case KeyCode.Period: return ".";
                case KeyCode.Equals: return "=";
                case KeyCode.Minus: return "-";
                case KeyCode.Plus: return "+";
                case KeyCode.LeftBracket: return "[";
                case KeyCode.RightBracket: return "]";
                case KeyCode.Semicolon: return ";";
                case KeyCode.Quote: return "'";
                case KeyCode.BackQuote: return "`";
                case KeyCode.Slash: return "/";
                case KeyCode.Backslash: return "\\";
                case KeyCode.UpArrow: return "Up";
                case KeyCode.DownArrow: return "Down";
                case KeyCode.LeftArrow: return "Left";
                case KeyCode.RightArrow: return "Right";
                case KeyCode.PageUp: return "Page Up";
                case KeyCode.PageDown: return "Page Down";
                case KeyCode.LeftControl: return "Left Ctrl";
                case KeyCode.RightControl: return "Right Ctrl";
                case KeyCode.LeftAlt: return "Left Alt";
                case KeyCode.RightAlt: return "Right Alt";
                default: return key.ToString();
            }
        }

        /// <summary>Human-readable action name, generated from the enum rather than a parallel table.</summary>
        public static string DisplayName(GameAction action)
        {
            string raw = action.ToString();
            var sb = new System.Text.StringBuilder(raw.Length + 4);
            for (int i = 0; i < raw.Length; i++)
            {
                if (i > 0 && char.IsUpper(raw[i]) && !char.IsUpper(raw[i - 1]))
                    sb.Append(' ');
                else if (i > 0 && char.IsDigit(raw[i]) && !char.IsDigit(raw[i - 1]))
                    sb.Append(' ');
                sb.Append(raw[i]);
            }
            return sb.ToString();
        }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;

            string raw = GamePrefs.GetString(PrefsKey, "");
            if (string.IsNullOrEmpty(raw)) return;

            Dictionary<GameAction, KeyCode> merged = Merge(raw);
            Current.Clear();
            foreach (var kv in merged)
                Current[kv.Key] = kv.Value;
        }

        /// <summary>
        /// The table a saved string produces: the defaults, overlaid with what was saved. Fixed
        /// actions keep their default. A save written before an action existed may already use
        /// that action's default key for something else; the saved choice wins and the newer
        /// action starts unbound ("—" on the controls screen), so no key fires two actions.
        /// Public for the tests.
        /// </summary>
        public static Dictionary<GameAction, KeyCode> Merge(string raw)
        {
            var result = new Dictionary<GameAction, KeyCode>(Defaults);
            if (string.IsNullOrEmpty(raw)) return result;

            var saved = new Dictionary<GameAction, KeyCode>();
            string[] entries = raw.Split(';');
            for (int i = 0; i < entries.Length; i++)
            {
                if (string.IsNullOrEmpty(entries[i])) continue;
                string[] pair = entries[i].Split(':');
                if (pair.Length != 2) continue;
                if (!int.TryParse(pair[0], out int a) || !int.TryParse(pair[1], out int k)) continue;
                if (!Enum.IsDefined(typeof(GameAction), a) || !Enum.IsDefined(typeof(KeyCode), k)) continue;
                if (IsFixed((GameAction)a)) continue;

                saved[(GameAction)a] = (KeyCode)k;
            }

            foreach (var kv in saved)
                result[kv.Key] = kv.Value;

            foreach (GameAction action in Defaults.Keys)
            {
                if (saved.ContainsKey(action)) continue;
                KeyCode key = result[action];
                foreach (var kv in saved)
                {
                    if (kv.Value != key) continue;
                    result[action] = KeyCode.None;
                    break;
                }
            }
            return result;
        }

        private static void Save()
        {
            var sb = new System.Text.StringBuilder();
            bool first = true;
            foreach (var kv in Current)
            {
                if (!first) sb.Append(';');
                sb.Append((int)kv.Key).Append(':').Append((int)kv.Value);
                first = false;
            }
            GamePrefs.SetString(PrefsKey, sb.ToString());
            GamePrefs.Save();
        }
    }
}
