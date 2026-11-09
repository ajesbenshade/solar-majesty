// Several overseers, one process — pure C#, no MonoBehaviour.

using System.Collections.Generic;

namespace SolarMajesty
{
    /// <summary>
    /// The colonies taking part in one game, one <see cref="ColonySim"/> per overseer. Commands are
    /// routed to the colony of the overseer who issued them, and every colony steps together, so
    /// a rival AI or a second player is another entry here and not a second copy of the game.
    /// </summary>
    public sealed class OverseerSession
    {
        // Kept in overseer-id order: stepping order must not depend on who joined first.
        private readonly List<ColonySim> _sims = new List<ColonySim>(4);

        public IReadOnlyList<ColonySim> Sims => _sims;

        /// <summary>Add a colony for <paramref name="overseerId"/>. False when that overseer already has one.</summary>
        public bool Add(int overseerId, ColonySim sim)
        {
            if (sim == null || TryGet(overseerId, out _)) return false;
            sim.OverseerId = overseerId;
            int at = 0;
            while (at < _sims.Count && _sims[at].OverseerId < overseerId) at++;
            _sims.Insert(at, sim);
            return true;
        }

        public bool TryGet(int overseerId, out ColonySim sim)
        {
            for (int i = 0; i < _sims.Count; i++)
            {
                if (_sims[i].OverseerId != overseerId) continue;
                sim = _sims[i];
                return true;
            }
            sim = null;
            return false;
        }

        /// <summary>Queue a command on its overseer's colony. False when no such overseer is in the game.</summary>
        public bool Submit(OverseerCommand command)
        {
            if (!TryGet(command.overseer, out ColonySim sim)) return false;
            sim.Commands.Enqueue(command);
            return true;
        }

        /// <summary>Step every colony together, with no scene (see <see cref="ColonySim.RunSteps"/>).</summary>
        public void RunSteps(int steps)
        {
            for (int s = 0; s < steps; s++)
            {
                for (int i = 0; i < _sims.Count; i++)
                    _sims[i].RunSteps(1);
            }
        }

        /// <summary>One fingerprint for the whole game: every colony's hash, in overseer order.</summary>
        public ulong StateHash()
        {
            ulong h = 14695981039346656037UL;
            for (int i = 0; i < _sims.Count; i++)
            {
                h ^= (ulong)_sims[i].OverseerId;
                h *= 1099511628211UL;
                h ^= _sims[i].StateHash();
                h *= 1099511628211UL;
            }
            return h;
        }
    }
}
