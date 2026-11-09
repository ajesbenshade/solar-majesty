// A run's rules as plain data — pure C#, no MonoBehaviour.

using System;

namespace SolarMajesty
{
    /// <summary>
    /// The rules a colony runs under (<see cref="ReplayRules"/>), copied out of the process-wide
    /// settings so a sim, a command log or a second overseer can carry its own.
    /// </summary>
    [Serializable]
    public struct RunRules
    {
        public int mode;
        public int challenge;
        public int stance;
        public bool ironman;

        public static RunRules Current() => new RunRules
        {
            mode = (int)ReplayRules.Mode,
            challenge = (int)ReplayRules.Challenge,
            stance = (int)ReplayRules.Stance,
            ironman = ReplayRules.IronmanRun
        };
    }
}
