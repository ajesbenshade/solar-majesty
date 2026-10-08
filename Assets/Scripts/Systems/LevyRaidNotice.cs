namespace SolarMajesty
{
    /// <summary>Why Haul's bag got lighter. Destroyed outranks a mugging, which outranks a hit.</summary>
    public enum LevyLossCause
    {
        Hit = 0,
        Mugged = 1,
        Destroyed = 2
    }

    /// <summary>
    /// One toast per raid. Hits inside <see cref="WindowSeconds"/> add up; a death or a
    /// deposit flushes early. The hint rides along the first time, then at most every few minutes.
    /// </summary>
    public sealed class LevyRaidNotice
    {
        public const float WindowSeconds = 18f;
        public const float HintIntervalSeconds = 180f;
        public const string Hint = "Guard Haul's route with a Defend flag.";

        bool _open;
        float _start;
        int _sum;
        LevyLossCause _cause;
        string _attacker;
        string _victim = "Haul";
        bool _force;

        public static bool ShouldRob(bool hostile, float damage, int carry) =>
            hostile && damage > 0f && carry > 0;

        public static string Compose(int amount, LevyLossCause cause, string attacker, string victim, bool includeHint)
        {
            if (amount <= 0) return null;
            string who = string.IsNullOrEmpty(victim) ? "Haul" : victim;
            string facts;
            switch (cause)
            {
                case LevyLossCause.Destroyed:
                    facts = $"{who} was destroyed; {amount} EU lost.";
                    break;
                case LevyLossCause.Mugged:
                    facts = string.IsNullOrEmpty(attacker)
                        ? $"{who} was mugged for {amount} EU."
                        : $"{Article(attacker)} {attacker} mugged {who} for {amount} EU.";
                    break;
                default:
                    facts = string.IsNullOrEmpty(attacker)
                        ? $"{who} was hit and dropped {amount} EU."
                        : $"{who} was hit by {Article(attacker).ToLowerInvariant()} {attacker} and dropped {amount} EU.";
                    break;
            }
            return includeHint ? facts + " " + Hint : facts;
        }

        public void Note(int amount, LevyLossCause cause, string attacker, string victim, float now)
        {
            if (amount <= 0) return;
            if (!_open)
            {
                _open = true;
                _start = now;
                _sum = 0;
                _cause = cause;
                _attacker = attacker;
                _victim = string.IsNullOrEmpty(victim) ? "Haul" : victim;
                _force = false;
            }
            else if (Rank(cause) > Rank(_cause))
            {
                _cause = cause;
                if (!string.IsNullOrEmpty(attacker)) _attacker = attacker;
            }
            else if (Rank(cause) == Rank(_cause) && !string.IsNullOrEmpty(attacker))
            {
                _attacker = attacker;
            }

            if (cause == LevyLossCause.Destroyed) _force = true;
            _sum += amount;
        }

        /// <summary>
        /// Emits the summed line when the window has elapsed, the raid was fatal, or <paramref name="force"/>
        /// (Haul made it to the chest). A zero total never speaks.
        /// </summary>
        public bool TryFlush(float now, bool force, ref float lastHintAt, out string message)
        {
            message = null;
            if (!_open || _sum <= 0)
            {
                if (_open) Clear();
                return false;
            }
            if (!force && !_force && now - _start < WindowSeconds) return false;

            bool hint = lastHintAt < 0f || now - lastHintAt >= HintIntervalSeconds;
            message = Compose(_sum, _cause, _attacker, _victim, hint);
            if (hint) lastHintAt = now;
            Clear();
            return message != null;
        }

        void Clear()
        {
            _open = false;
            _sum = 0;
            _force = false;
            _attacker = null;
            _victim = "Haul";
            _cause = LevyLossCause.Hit;
        }

        static int Rank(LevyLossCause cause)
        {
            switch (cause)
            {
                case LevyLossCause.Destroyed: return 3;
                case LevyLossCause.Mugged: return 2;
                default: return 1;
            }
        }

        static string Article(string name)
        {
            if (string.IsNullOrEmpty(name)) return "A";
            char c = char.ToLowerInvariant(name[0]);
            return c == 'a' || c == 'e' || c == 'i' || c == 'o' || c == 'u' ? "An" : "A";
        }
    }
}
