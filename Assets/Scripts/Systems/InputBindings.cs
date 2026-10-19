using System;
using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Every rebindable action. Adding one here and giving it a default is all that is needed for
    /// it to appear in the controls screen.
    ///
    /// Saved bindings store the numeric value, so members are only ever appended: reordering would
    /// hand a player's keys to the wrong actions.
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
        ToolOrbital, CycleTool, DisbandParty,
        FlagExplore, FlagClearThreat, FlagBuild, FlagExtract, FlagDefend,
        FlagResearchSite, FlagOutpost, FlagTerraform,
        BountyUp, BountyDown,
        PayYard
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
            { GameAction.ToolOrbital, KeyCode.N },
            { GameAction.CycleTool, KeyCode.Tab },
            { GameAction.DisbandParty, KeyCode.LeftBracket },
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
            { GameAction.PayYard, KeyCode.Y }
        };

        /// <summary>Names the camel-case split would get wrong or leave vague.</summary>
        private static readonly Dictionary<GameAction, string> Names = new Dictionary<GameAction, string>
        {
            { GameAction.ToolBuild, "Build Catalog" },
            { GameAction.ToolFlag, "Bounty Flags" },
            { GameAction.ToolTech, "Research" },
            { GameAction.ToolParty, "Form Party" },
            { GameAction.ToolOrbital, "Orbital Support" },
            { GameAction.JumpToAlert, "Jump to Alert" },
            { GameAction.FlagExplore, "Flag: Explore" },
            { GameAction.FlagClearThreat, "Flag: Clear Threat" },
            { GameAction.FlagBuild, "Flag: Build Here" },
            { GameAction.FlagExtract, "Flag: Extract" },
            { GameAction.FlagDefend, "Flag: Defend" },
            { GameAction.FlagResearchSite, "Flag: Research Site" },
            { GameAction.FlagOutpost, "Flag: Outpost" },
            { GameAction.FlagTerraform, "Flag: Terraform" },
            { GameAction.BountyUp, "Raise Bounty" },
            { GameAction.BountyDown, "Lower Bounty" },
            { GameAction.PayYard, "Fobot Yard / Continue" }
        };

        private static readonly Dictionary<GameAction, KeyCode> Current =
            new Dictionary<GameAction, KeyCode>(Defaults);

        private static readonly Dictionary<GameAction, string> LabelCache = new Dictionary<GameAction, string>();

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

        /// <summary>
        /// True while an in-game text box (flag orders) has keyboard focus. Every hotkey reader
        /// stands down so typing does not trigger gameplay keys.
        /// </summary>
        public static bool TextEntryActive;

        /// <summary>Controller hooks, set by <c>GamepadInput</c> (Runtime) so every hotkey also has a button.</summary>
        public static Func<GameAction, bool> PadDown;
        public static Func<GameAction, bool> PadHeld;

        /// <summary>True while the controller is the active device, so hints name buttons instead of keys.</summary>
        public static Func<bool> PadHints;

        public static bool Down(GameAction action)
        {
            if (TextEntryActive) return false;
            KeyCode key = Key(action);
            if (key != KeyCode.None && Input.GetKeyDown(key)) return true;
            return PadDown != null && PadDown(action);
        }

        public static bool Held(GameAction action)
        {
            if (TextEntryActive) return false;
            KeyCode key = Key(action);
            if (key != KeyCode.None && Input.GetKey(key)) return true;
            return PadHeld != null && PadHeld(action);
        }

        /// <summary>The action already using this key, or null.</summary>
        public static GameAction? Conflict(GameAction action, KeyCode key)
        {
            Load();
            foreach (var kv in Current)
            {
                if (kv.Key != action && kv.Value == key)
                    return kv.Key;
            }
            return null;
        }

        /// <summary>Assign a key. Fails rather than creating a duplicate the player cannot see.</summary>
        public static bool TryRebind(GameAction action, KeyCode key)
        {
            Load();
            if (key == KeyCode.None) return false;
            if (IsReserved(key)) return false;
            if (Conflict(action, key).HasValue) return false;

            Current[action] = key;
            LabelCache.Clear();
            Save();
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Keys the game keeps for itself: number keys pick build slots and modifiers change clicks,
        /// so an action bound there would fire alongside them.
        /// </summary>
        public static bool IsReserved(KeyCode key) =>
            (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9) ||
            (key >= KeyCode.Mouse0 && key <= KeyCode.Mouse6) ||
            key == KeyCode.LeftShift || key == KeyCode.RightShift ||
            key == KeyCode.LeftControl || key == KeyCode.RightControl ||
            key == KeyCode.LeftAlt || key == KeyCode.RightAlt || key == KeyCode.AltGr ||
            key == KeyCode.LeftCommand || key == KeyCode.RightCommand ||
            key == KeyCode.LeftWindows || key == KeyCode.RightWindows;

        public static void ResetToDefaults()
        {
            Load();
            Current.Clear();
            foreach (var kv in Defaults)
                Current[kv.Key] = kv.Value;
            LabelCache.Clear();
            Save();
            Changed?.Invoke();
        }

        /// <summary>
        /// Drop the in-memory table so the next read comes from <see cref="GamePrefs"/> again
        /// (after a cloud sync, or a test pointing prefs somewhere else).
        /// </summary>
        public static void Reload()
        {
            _loaded = false;
            Current.Clear();
            foreach (var kv in Defaults)
                Current[kv.Key] = kv.Value;
            LabelCache.Clear();
        }

        /// <summary>Readable key name for the controls screen and hint text.</summary>
        public static string Label(GameAction action)
        {
            if (PadHints != null && PadHints())
            {
                PadButton pad = GamepadMap.ButtonFor(action);
                if (pad != PadButton.None) return GamepadMap.Label(pad);
            }
            Load();
            if (LabelCache.TryGetValue(action, out string cached)) return cached;
            string label = KeyLabel(Key(action));
            LabelCache[action] = label; // hints are rebuilt on every IMGUI pass
            return label;
        }

        /// <summary>The key as the HUD names it. Equals and Minus read as the bounty's + and −.</summary>
        public static string KeyLabel(KeyCode key)
        {
            if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9) return ((int)(key - KeyCode.Alpha0)).ToString();
            if (key >= KeyCode.Keypad0 && key <= KeyCode.Keypad9) return "Num " + (int)(key - KeyCode.Keypad0);
            switch (key)
            {
                case KeyCode.None: return "—";
                case KeyCode.Space: return "Space";
                case KeyCode.Return: return "Enter";
                case KeyCode.KeypadEnter: return "Num Enter";
                case KeyCode.Escape: return "Esc";
                case KeyCode.Backspace: return "Backspace";
                case KeyCode.Comma: return ",";
                case KeyCode.Period: return ".";
                case KeyCode.Equals: return "+";
                case KeyCode.Minus: return "−";
                case KeyCode.KeypadPlus: return "Num +";
                case KeyCode.KeypadMinus: return "Num −";
                case KeyCode.KeypadMultiply: return "Num *";
                case KeyCode.KeypadDivide: return "Num /";
                case KeyCode.KeypadPeriod: return "Num .";
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
                case KeyCode.PageUp: return "PgUp";
                case KeyCode.PageDown: return "PgDn";
                default: return key.ToString();
            }
        }

        /// <summary>The current key for hint text, so copy follows a rebind.</summary>
        public static string Hint(GameAction action) => Label(action);

        /// <summary>
        /// Replaces <c>{key:Action}</c> with that action's current key, for copy that lives in
        /// constants (tutorial beats, world briefings). Unknown tokens stay as written so a typo
        /// shows up on screen instead of going blank.
        /// </summary>
        public static string ExpandKeyTokens(string text)
        {
            const string open = "{key:";
            if (string.IsNullOrEmpty(text)) return text;
            int at = text.IndexOf(open, StringComparison.Ordinal);
            if (at < 0) return text;

            var sb = new System.Text.StringBuilder(text.Length);
            int from = 0;
            while (at >= 0)
            {
                int close = text.IndexOf('}', at + open.Length);
                if (close < 0) break;
                string name = text.Substring(at + open.Length, close - at - open.Length);
                sb.Append(text, from, at - from);
                // IsDefined on the string matches names only, so "{key:3}" is not read as an action.
                if (Enum.IsDefined(typeof(GameAction), name))
                    sb.Append(Label((GameAction)Enum.Parse(typeof(GameAction), name)));
                else
                    sb.Append(text, at, close - at + 1);
                from = close + 1;
                at = text.IndexOf(open, from, StringComparison.Ordinal);
            }
            sb.Append(text, from, text.Length - from);
            return sb.ToString();
        }

        /// <summary>Human-readable action name: the camel-case split, unless a clearer name is listed.</summary>
        public static string DisplayName(GameAction action)
        {
            if (Names.TryGetValue(action, out string named)) return named;
            string raw = action.ToString();
            var sb = new System.Text.StringBuilder(raw.Length + 4);
            for (int i = 0; i < raw.Length; i++)
            {
                if (i > 0 && char.IsUpper(raw[i]) && !char.IsUpper(raw[i - 1]))
                    sb.Append(' ');
                sb.Append(raw[i]);
            }
            return sb.ToString();
        }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            LabelCache.Clear();

            string raw = GamePrefs.GetString(PrefsKey, "");
            if (string.IsNullOrEmpty(raw)) return;

            var saved = new HashSet<GameAction>();
            string[] entries = raw.Split(';');
            for (int i = 0; i < entries.Length; i++)
            {
                if (string.IsNullOrEmpty(entries[i])) continue;
                string[] pair = entries[i].Split(':');
                if (pair.Length != 2) continue;
                if (!int.TryParse(pair[0], out int a) || !int.TryParse(pair[1], out int k)) continue;
                if (!Enum.IsDefined(typeof(GameAction), a) || !Enum.IsDefined(typeof(KeyCode), k)) continue;

                Current[(GameAction)a] = (KeyCode)k;
                saved.Add((GameAction)a);
            }

            // An action added after the player saved gets its default, unless they already put that
            // key on something else; then it starts unbound rather than firing alongside theirs.
            foreach (var kv in Defaults)
            {
                if (saved.Contains(kv.Key)) continue;
                foreach (GameAction other in saved)
                {
                    if (Current[other] != kv.Value) continue;
                    Current[kv.Key] = KeyCode.None;
                    break;
                }
            }
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
