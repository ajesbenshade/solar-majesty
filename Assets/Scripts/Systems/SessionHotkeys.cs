namespace SolarMajesty
{
    /// <summary>
    /// What Esc does. Placement eats the key so the pause menu does not open on top of a ghost.
    /// </summary>
    public static class SessionHotkeys
    {
        public enum EscapeAction
        {
            Ignore = 0,
            CloseSettings = 1,
            CancelPlacement = 2,
            TogglePause = 3
        }

        public static EscapeAction OnEscape(bool settingsOpen, bool onTitle, bool playing, bool placementArmed)
        {
            if (settingsOpen) return EscapeAction.CloseSettings;
            if (onTitle) return EscapeAction.Ignore;
            if (playing && placementArmed) return EscapeAction.CancelPlacement;
            return EscapeAction.TogglePause;
        }
    }
}
