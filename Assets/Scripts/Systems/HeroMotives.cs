using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Per-hero motive state, after Majesty 2's work / safety / relaxation Motivator.
    /// All three read 0..1.
    ///   Safety     - eases down toward danger slowly and back up fast, so a hero tolerates a scare
    ///                for a moment but stays shaken until it has really recovered.
    ///   Work       - the share of the work-task budget left before a break.
    ///   Relaxation - the share of the break left once one starts (1 while working).
    /// A hero works through <c>maxWorkTasks</c> tasks, then takes <c>maxRelaxTasks</c> rest or
    /// wander tasks, then returns to work. Pure C#; the agent ticks it and the brain reads it.
    /// </summary>
    public sealed class HeroMotives
    {
        public float Safety { get; private set; } = 1f;
        public float Work => Relaxing ? 0f : Mathf.Clamp01(1f - (float)WorkTasks / Mathf.Max(1, _maxWork));
        public float Relaxation => Relaxing ? Mathf.Clamp01(1f - (float)RelaxTasks / Mathf.Max(1, _maxRelax)) : 1f;

        public bool Relaxing { get; private set; }
        /// <summary>Scared badly enough to stay home until safety recovers.</summary>
        public bool Shaken { get; private set; }
        /// <summary>
        /// Hurt badly enough to run for the inn. Stays set until health clears
        /// <see cref="SpecialistBrainTuning.fleeResumeHealth"/> (the inn heal climbs there).
        /// </summary>
        public bool FleeLatched { get; private set; }
        public int WorkTasks { get; private set; }
        public int RelaxTasks { get; private set; }

        private int _maxWork = 12;
        private int _maxRelax = 3;
        private float _phaseSeconds;
        private float _lastTaskAt = -999f;
        private SpecialistAction _lastAction = SpecialistAction.Idle;
        private object _lastKey;
        private bool _holdValid;
        private float _holdUntil;
        private SpecialistAction _holdAction = SpecialistAction.Idle;
        private string _holdReason = "";
        private BrainDecision _held;

        /// <summary>Forget everything (hero respawned / revived).</summary>
        public void Reset()
        {
            Safety = 1f;
            Relaxing = false;
            Shaken = false;
            FleeLatched = false;
            WorkTasks = RelaxTasks = 0;
            _phaseSeconds = 0f;
            _lastTaskAt = -999f;
            _lastAction = SpecialistAction.Idle;
            _lastKey = null;
            _holdValid = false;
            _holdUntil = 0f;
            _holdAction = SpecialistAction.Idle;
            _holdReason = "";
            _held = default;
        }

        /// <summary>
        /// Keep <paramref name="proposed"/> from replacing the last action until the dwell elapses.
        /// Entering flee, and leaving flee because health cleared the resume line, break the dwell
        /// so a fresh wound is not ignored and a healed hero is not stuck running.
        /// </summary>
        public BrainDecision HoldDecision(BrainDecision proposed, float now, float minSeconds, bool force)
        {
            bool changed = !_holdValid ||
                           proposed.Action != _holdAction ||
                           proposed.Reason != _holdReason;
            if (_holdValid && changed && !force && now < _holdUntil)
                return _held;

            if (changed)
            {
                _holdAction = proposed.Action;
                _holdReason = proposed.Reason ?? "";
                _holdUntil = now + Mathf.Max(0f, minSeconds);
                _holdValid = true;
            }
            _held = proposed;
            return proposed;
        }

        /// <summary>
        /// Apply dwell, then latch flee until health is back above the resume line.
        /// Reaching the inn is what applies that heal (<c>TickFlee</c>); the latch drops once
        /// health crosses <see cref="SpecialistBrainTuning.fleeResumeHealth"/>, including there.
        /// </summary>
        public BrainDecision Commit(BrainDecision proposed, SpecialistBrainTuning tuning, float health, float now, bool atInn)
        {
            if (tuning == null) tuning = new SpecialistBrainTuning();
            float exit = tuning.fleeResumeHealth;
            bool healed = health >= exit || (atInn && health >= exit);
            bool force = proposed.Action == SpecialistAction.Flee ||
                         (_holdValid && _holdAction == SpecialistAction.Flee && healed);
            var decision = HoldDecision(proposed, now, tuning.decisionDwellSeconds, force);
            if (decision.Action == SpecialistAction.Flee)
                FleeLatched = true;
            else if (healed)
                FleeLatched = false;
            return decision;
        }

        /// <summary>Advance safety and the phase clock by simulation seconds.</summary>
        public void Tick(float dt, SpecialistBrainTuning t, SpecialistClass cls, float health01, float bodyDanger,
            float safetyField = 0f)
        {
            if (!(dt > 0f) || t == null || !t.motivesEnabled) return;
            _maxWork = t.MaxWorkTasksFor(cls);
            _maxRelax = t.MaxRelaxTasksFor(cls);

            // Danger pushes safety down; nearby buildings (Majesty 2 safety emitters) push it back up.
            float target = Mathf.Min(
                Mathf.Clamp01(health01),
                1f - Mathf.Clamp01(bodyDanger) * t.safetyDangerWeight +
                Mathf.Clamp01(safetyField) * t.safetyFieldWeight);
            target = Mathf.Clamp01(target);
            float rate = target < Safety ? t.safetyFallRate : t.safetyRiseRate;
            Safety = Mathf.MoveTowards(Safety, target, rate * dt);
            if (Safety < t.safetyFleeBelow) Shaken = true;
            else if (Safety > t.safetyResumeAbove) Shaken = false;

            if (Relaxing)
            {
                _phaseSeconds += dt;
                if (_phaseSeconds >= t.relaxMaxSeconds) EndBreak();
            }
        }

        /// <summary>
        /// Report the decision the hero just committed to. <paramref name="key"/> identifies the
        /// target (flag id) so a new flag counts as a new task even with the same action.
        /// </summary>
        public void NoteDecision(SpecialistBrainTuning t, SpecialistAction action, object key, float now)
        {
            if (t == null || !t.motivesEnabled) return;
            bool changed = action != _lastAction || !ReferenceEquals(key, _lastKey);
            _lastAction = action;
            _lastKey = key;
            if (!changed || now - _lastTaskAt < t.minTaskSeconds) return;

            bool work = action == SpecialistAction.PursueFlag ||
                        action == SpecialistAction.Hunt ||
                        action == SpecialistAction.Repair;
            bool relax = action == SpecialistAction.Rest || action == SpecialistAction.Wander;

            if (!Relaxing && work)
            {
                _lastTaskAt = now;
                if (++WorkTasks >= _maxWork) StartBreak();
            }
            else if (Relaxing && relax)
            {
                _lastTaskAt = now;
                if (++RelaxTasks >= _maxRelax) EndBreak();
            }
        }

        private void StartBreak()
        {
            Relaxing = true;
            RelaxTasks = 0;
            _phaseSeconds = 0f;
        }

        private void EndBreak()
        {
            Relaxing = false;
            WorkTasks = 0;
            RelaxTasks = 0;
            _phaseSeconds = 0f;
        }
    }
}
