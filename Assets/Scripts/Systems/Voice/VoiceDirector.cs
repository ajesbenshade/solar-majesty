using System.Collections.Generic;

namespace SolarMajesty
{
    /// <summary>
    /// Decides which character gets to speak. Pure; tested by VoiceTests.
    ///
    /// Twenty robots each barking on every decision change is noise, and noise teaches the player
    /// to mute voices. So the Overseer outranks everyone, the selected hero outranks the crowd,
    /// only big moments are voiced for unselected heroes, and a couple of voices at most overlap.
    /// </summary>
    public sealed class VoiceDirector
    {
        /// <summary>Most hero voices allowed at once.</summary>
        public int MaxHeroVoices = 2;
        /// <summary>Gap between any two hero lines starting.</summary>
        public float HeroGap = 0.9f;
        /// <summary>Per-hero cooldown after a big moment (claim, flee, level up, refused, down).</summary>
        public float BigCooldown = 5f;
        /// <summary>Per-hero cooldown after a routine moment (only voiced for the selected hero).</summary>
        public float RoutineCooldown = 9f;
        /// <summary>Hurt barks are rate-limited across the whole colony, not just per hero.</summary>
        public float HurtGap = 2.5f;
        public float HurtCooldown = 4f;
        /// <summary>Re-clicking the same hero does not re-trigger their acknowledgement.</summary>
        public float SelectDebounce = 0.5f;

        private readonly Dictionary<int, float> _heroNext = new Dictionary<int, float>();
        private readonly Dictionary<int, float> _heroBusyUntil = new Dictionary<int, float>();
        private readonly List<float> _heroEnds = new List<float>(4);
        private float _nextHero;
        private float _nextHurt;
        private float _overseerUntil;
        private int _overseerPriority;
        private int _lastSelectHero = int.MinValue;
        private float _lastSelectAt = -100f;

        public bool OverseerSpeaking(float now) => now < _overseerUntil;

        public static bool IsBigMoment(VoiceCue cue) =>
            cue == VoiceCue.Claim || cue == VoiceCue.Flee || cue == VoiceCue.LevelUp ||
            cue == VoiceCue.Refused || cue == VoiceCue.Down;

        /// <summary>
        /// Can this hero say this now? On true the slot is taken for <paramref name="seconds"/>.
        /// </summary>
        public bool TryStartHero(int heroId, VoiceCue cue, bool selected, float now, float seconds)
        {
            Expire(now);

            if (cue == VoiceCue.Select)
            {
                // The player asked; answer unless it's a double-click on the same hero.
                if (heroId == _lastSelectHero && now - _lastSelectAt < SelectDebounce) return false;
                _lastSelectHero = heroId;
                _lastSelectAt = now;
                Take(heroId, now, seconds, 0f);
                return true;
            }

            // Nobody talks over the Overseer except to answer a click.
            if (OverseerSpeaking(now)) return false;
            if (_heroBusyUntil.TryGetValue(heroId, out float busy) && now < busy) return false;
            if (_heroEnds.Count >= MaxHeroVoices) return false;
            if (now < _nextHero) return false;
            if (_heroNext.TryGetValue(heroId, out float next) && now < next) return false;

            float cooldown;
            if (cue == VoiceCue.Hurt)
            {
                if (now < _nextHurt) return false;
                _nextHurt = now + HurtGap;
                cooldown = HurtCooldown;
            }
            else if (IsBigMoment(cue))
                cooldown = BigCooldown;
            else if (selected)
                cooldown = RoutineCooldown;
            else
                return false;

            Take(heroId, now, seconds, cooldown);
            return true;
        }

        /// <summary>
        /// Can the Overseer start a line? Higher priority interrupts a lower one already playing;
        /// equal or lower priority waits its turn (the caller drops it — stale advice is worse
        /// than none).
        /// </summary>
        public bool TryStartOverseer(float now, float seconds, int priority)
        {
            if (OverseerSpeaking(now) && priority <= _overseerPriority) return false;
            _overseerUntil = now + seconds;
            _overseerPriority = priority;
            return true;
        }

        public void Forget(int heroId)
        {
            _heroNext.Remove(heroId);
            _heroBusyUntil.Remove(heroId);
        }

        private void Take(int heroId, float now, float seconds, float cooldown)
        {
            float end = now + seconds;
            _heroEnds.Add(end);
            _heroBusyUntil[heroId] = end;
            _heroNext[heroId] = end + cooldown;
            _nextHero = now + HeroGap;
        }

        private void Expire(float now)
        {
            for (int i = _heroEnds.Count - 1; i >= 0; i--)
                if (_heroEnds[i] <= now) _heroEnds.RemoveAt(i);
        }
    }
}
