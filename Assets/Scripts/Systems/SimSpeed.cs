using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Player-facing game speed. Index 0 is a non-modal pause: the world holds but the HUD, camera,
    /// and inspection stay live, which is how a colony sim lets you read a crisis before answering it.
    /// The pause menu is a separate, modal thing.
    /// </summary>
    public static class SimSpeed
    {
        public static readonly float[] Multipliers = { 0f, 1f, 2f, 3f };

        private const string PrefsKey = "SM_GameSpeed";
        private const int NormalIndex = 1;

        private static int _index = NormalIndex;

        /// <summary>Index into <see cref="Multipliers"/>.</summary>
        public static int Index => _index;

        public static float Multiplier => Multipliers[_index];

        public static bool IsPaused => _index == 0;

        /// <summary>Speed to resume to after an unpause.</summary>
        public static int LastRunningIndex { get; private set; } = NormalIndex;

        public static string Label => IsPaused ? "HOLD" : $"{Multipliers[_index]:0}x";

        public static void Set(int index)
        {
            _index = Mathf.Clamp(index, 0, Multipliers.Length - 1);
            if (_index > 0)
            {
                LastRunningIndex = _index;
                PlayerPrefs.SetInt(PrefsKey, _index);
            }
        }

        public static void Faster() => Set(_index + 1);

        public static void Slower() => Set(_index - 1);

        public static void TogglePause() => Set(IsPaused ? LastRunningIndex : 0);

        /// <summary>Resume at the player's last chosen speed (used when leaving a menu).</summary>
        public static void Resume() => Set(LastRunningIndex);

        /// <summary>Back to 1x without touching the stored preference.</summary>
        public static void ResetToNormal()
        {
            _index = NormalIndex;
            LastRunningIndex = NormalIndex;
        }

        public static void Load()
        {
            int saved = PlayerPrefs.GetInt(PrefsKey, NormalIndex);
            LastRunningIndex = Mathf.Clamp(saved, 1, Multipliers.Length - 1);
            _index = LastRunningIndex;
        }
    }
}
