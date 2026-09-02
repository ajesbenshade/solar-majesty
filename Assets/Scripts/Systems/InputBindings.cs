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
        Cancel, Confirm
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
            { GameAction.Confirm, KeyCode.Return }
        };

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

        public static bool Down(GameAction action)
        {
            KeyCode key = Key(action);
            return key != KeyCode.None && Input.GetKeyDown(key);
        }

        public static bool Held(GameAction action)
        {
            KeyCode key = Key(action);
            return key != KeyCode.None && Input.GetKey(key);
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
            if (Conflict(action, key).HasValue) return false;

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
        public static string Label(GameAction action)
        {
            KeyCode key = Key(action);
            switch (key)
            {
                case KeyCode.None: return "—";
                case KeyCode.Space: return "Space";
                case KeyCode.Return: return "Enter";
                case KeyCode.Escape: return "Esc";
                case KeyCode.Backspace: return "Backspace";
                case KeyCode.Comma: return ",";
                case KeyCode.Period: return ".";
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
                sb.Append(raw[i]);
            }
            return sb.ToString();
        }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;

            string raw = PlayerPrefs.GetString(PrefsKey, "");
            if (string.IsNullOrEmpty(raw)) return;

            string[] entries = raw.Split(';');
            for (int i = 0; i < entries.Length; i++)
            {
                if (string.IsNullOrEmpty(entries[i])) continue;
                string[] pair = entries[i].Split(':');
                if (pair.Length != 2) continue;
                if (!int.TryParse(pair[0], out int a) || !int.TryParse(pair[1], out int k)) continue;
                if (!Enum.IsDefined(typeof(GameAction), a) || !Enum.IsDefined(typeof(KeyCode), k)) continue;

                Current[(GameAction)a] = (KeyCode)k;
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
            PlayerPrefs.SetString(PrefsKey, sb.ToString());
            PlayerPrefs.Save();
        }
    }
}
