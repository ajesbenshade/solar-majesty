using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Player-facing game speed. Index 0 is a non-modal pause: the world holds but the HUD, camera,
    /// and inspection stay live, which is how a colony sim lets you read a crisis before answering it.
    /// The pause menu is a separate, modal thing.
    ///
    /// "1×" is the colony's calm default pace, <see cref="BasePace"/> of real time; every speed
    /// scales the whole world together (days, day/night, heroes, building, taxes), so balance holds
    /// at any setting. 2× is the pace the game used to run at.
    /// </summary>
    public static class SimSpeed
    {
        /// <summary>World pace at 1×, as a fraction of real time.</summary>
        public const float BasePace = 0.5f;

        /// <summary>Player-facing steps. Index 0 is the hold.</summary>
        public static readonly float[] Multipliers = { 0f, 0.25f, 0.5f, 0.75f, 1f, 1.5f, 2f, 3f, 4f };

        // v2: the old key held an index into the four-step table (0/1/2/3×), which would now pick
        // the wrong speed. A fresh key starts everyone at the new 1×.
        private const string PrefsKey = "SM_GameSpeed_v2";
        private const int NormalIndex = 4;

        private static int _index = NormalIndex;

        /// <summary>Index into <see cref="Multipliers"/>.</summary>
        public static int Index => _index;

        public static float Multiplier => Multipliers[_index];

        /// <summary>What <c>Time.timeScale</c> should be: the chosen step times <see cref="BasePace"/>.</summary>
        public static float TimeScale => Multiplier * BasePace;

        public static bool IsPaused => _index == 0;

        public static bool IsSlowest => _index <= 1;

        public static bool IsFastest => _index >= Multipliers.Length - 1;

        /// <summary>Speed to resume to after an unpause.</summary>
        public static int LastRunningIndex { get; private set; } = NormalIndex;

        public static string Label => IsPaused ? "HOLD" : LabelFor(_index);

        public static string LabelFor(int index)
        {
            float m = Multipliers[Mathf.Clamp(index, 0, Multipliers.Length - 1)];
            return m <= 0f ? "HOLD" : $"{m:0.##}×";
        }

        public static void Set(int index)
        {
            _index = Mathf.Clamp(index, 0, Multipliers.Length - 1);
            if (_index > 0)
            {
                LastRunningIndex = _index;
                PlayerPrefs.SetInt(PrefsKey, _index);
            }
        }

        /// <summary>One step faster; from the hold, resumes at the speed the player was running.</summary>
        public static void Faster() => Set(IsPaused ? LastRunningIndex : _index + 1);

        /// <summary>One step slower, stopping at the slowest running speed — the hold is Space.</summary>
        public static void Slower() => Set(Mathf.Max(1, _index - 1));

        public static void TogglePause() => Set(IsPaused ? LastRunningIndex : 0);

        /// <summary>Resume at the player's last chosen speed (used when leaving a menu).</summary>
        public static void Resume() => Set(LastRunningIndex);

        /// <summary>Back to 1× without touching the stored preference.</summary>
        public static void ResetToNormal()
        {
            _index = NormalIndex;
            LastRunningIndex = NormalIndex;
        }

        /// <summary>The player's saved speed, so a new session starts at the pace they like.</summary>
        public static void Load()
        {
            int saved = PlayerPrefs.GetInt(PrefsKey, NormalIndex);
            LastRunningIndex = Mathf.Clamp(saved, 1, Multipliers.Length - 1);
            _index = LastRunningIndex;
        }
    }
}
