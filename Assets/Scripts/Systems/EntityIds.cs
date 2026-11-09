// Stable entity ids — pure C#, no MonoBehaviour.

namespace SolarMajesty
{
    /// <summary>
    /// Hands out ids that name a robot for the whole run, across save and load. Commands refer to
    /// robots by these and never by object reference. Zero means "no id".
    /// </summary>
    public sealed class EntityIds
    {
        private int _next = 1;

        /// <summary>The id the next <see cref="Next"/> call returns. Saved with the world.</summary>
        public int NextId
        {
            get => _next;
            set => _next = value > 0 ? value : 1;
        }

        public int Next() => _next++;

        /// <summary>Mark a restored id as taken so it is never handed out again.</summary>
        public void Reserve(int id)
        {
            if (id >= _next) _next = id + 1;
        }
    }
}
