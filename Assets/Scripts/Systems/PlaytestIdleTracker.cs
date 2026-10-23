using System;

namespace SolarMajesty
{
    /// <summary>
    /// Idle gaps for playtest telemetry: no meaningful input for longer than the threshold while
    /// the world is running. One gap is reported once, when input comes back (or the session
    /// pauses or quits). Crossing the threshold is reported separately so the tutorial can nudge.
    /// Pure: the caller decides what counts as input and when the clock runs.
    /// </summary>
    public sealed class PlaytestIdleTracker
    {
        public enum Change
        {
            None = 0,
            /// <summary>This tick pushed the quiet time over the threshold. Fires once per gap.</summary>
            Crossed = 1,
            /// <summary>Input came back after a gap. <see cref="LastGapSeconds"/> holds its length.</summary>
            Ended = 2
        }

        public const float DefaultThresholdSeconds = 30f;

        /// <summary>
        /// A frame longer than this is a hitch or an unfocused window, not a player staring at the
        /// screen. Only this much of it counts.
        /// </summary>
        public const float MaxStepSeconds = 1f;

        public float Threshold { get; }

        /// <summary>Seconds without input while the clock was running.</summary>
        public float Quiet { get; private set; }

        public bool InGap => Quiet >= Threshold;

        public float LastGapSeconds { get; private set; }

        public PlaytestIdleTracker(float thresholdSeconds = DefaultThresholdSeconds)
        {
            Threshold = thresholdSeconds > 0f ? thresholdSeconds : DefaultThresholdSeconds;
        }

        /// <param name="dt">Unscaled seconds since the last tick.</param>
        /// <param name="counting">False while paused, held, in a menu, or unfocused. Quiet time freezes.</param>
        /// <param name="input">A key, button, or wheel this frame.</param>
        public Change Tick(float dt, bool counting, bool input)
        {
            if (input)
            {
                bool ended = InGap;
                if (ended) LastGapSeconds = Quiet;
                Quiet = 0f;
                return ended ? Change.Ended : Change.None;
            }

            if (!counting || dt <= 0f) return Change.None;

            bool was = InGap;
            Quiet += Math.Min(dt, MaxStepSeconds);
            return !was && InGap ? Change.Crossed : Change.None;
        }

        /// <summary>
        /// Close an open gap without input (pause opened, session quit). True when there was a
        /// gap to report. Quiet time always resets.
        /// </summary>
        public bool TryClose(out float seconds)
        {
            bool open = InGap;
            seconds = open ? Quiet : 0f;
            if (open) LastGapSeconds = Quiet;
            Quiet = 0f;
            return open;
        }
    }

    /// <summary>
    /// Seconds spent on each tutorial step, on whatever clock the caller passes (play seconds, so
    /// pause and menus do not count).
    /// </summary>
    public sealed class TutorialStepClock
    {
        public int Step { get; private set; } = -1;
        public double EnteredAt { get; private set; }

        public void Reset(int step, double now)
        {
            Step = step;
            EnteredAt = now;
        }

        /// <summary>Seconds on the current step so far.</summary>
        public float Since(double now) =>
            Step < 0 ? 0f : (float)Math.Max(0.0, now - EnteredAt);

        /// <summary>Start timing <paramref name="step"/>; returns the seconds spent on the step being left.</summary>
        public float Enter(int step, double now)
        {
            float spent = Since(now);
            Step = step;
            EnteredAt = now;
            return spent;
        }
    }
}
