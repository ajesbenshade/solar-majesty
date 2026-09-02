// Fixed simulation tick — pure C#, no MonoBehaviour.

namespace SolarMajesty
{
    /// <summary>
    /// Accumulates frame time and hands the simulation a whole number of fixed steps.
    /// Everything downstream sees the same <see cref="StepSeconds"/> regardless of frame rate, so
    /// economy and settlement outcomes stop depending on the machine the game runs on.
    /// </summary>
    public sealed class SimClock
    {
        /// <summary>20 Hz. Fine enough for colony pacing, coarse enough to stay cheap.</summary>
        public const float DefaultStepSeconds = 0.05f;

        /// <summary>
        /// Ceiling on catch-up work in a single frame. Without this, a long stall (asset load,
        /// alt-tab, breakpoint) queues a backlog that takes longer to simulate than it did to
        /// accrue, and the game never recovers.
        /// </summary>
        public const int DefaultMaxStepsPerFrame = 8;

        public float StepSeconds { get; }
        public int MaxStepsPerFrame { get; set; }

        /// <summary>Steps simulated since the clock was created or reset.</summary>
        public long TotalSteps { get; private set; }

        public double ElapsedSeconds => TotalSteps * (double)StepSeconds;

        /// <summary>True when the last Advance hit the ceiling and discarded a backlog.</summary>
        public bool DroppedTime { get; private set; }

        /// <summary>How far into the next step we are (0-1). For render interpolation.</summary>
        public float Alpha => _accumulator / StepSeconds;

        private float _accumulator;

        public SimClock(float stepSeconds = DefaultStepSeconds, int maxStepsPerFrame = DefaultMaxStepsPerFrame)
        {
            StepSeconds = stepSeconds > 0f ? stepSeconds : DefaultStepSeconds;
            MaxStepsPerFrame = maxStepsPerFrame > 0 ? maxStepsPerFrame : DefaultMaxStepsPerFrame;
        }

        /// <summary>
        /// Feed elapsed (already speed-scaled) seconds; returns how many fixed steps to run now.
        /// </summary>
        public int Advance(float deltaSeconds)
        {
            DroppedTime = false;
            if (deltaSeconds > 0f)
                _accumulator += deltaSeconds;

            int steps = 0;
            while (_accumulator >= StepSeconds && steps < MaxStepsPerFrame)
            {
                _accumulator -= StepSeconds;
                steps++;
            }

            if (_accumulator >= StepSeconds)
            {
                DroppedTime = true;
                _accumulator = 0f;
            }

            TotalSteps += steps;
            return steps;
        }

        public void Reset()
        {
            _accumulator = 0f;
            TotalSteps = 0;
            DroppedTime = false;
        }
    }
}
