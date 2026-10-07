// Click-to-select and +/- bounty for posted flags.
// Pure C#: FlagPlacementInput and GameLoop call this; they do not reimplement the rules.

using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    public enum FlagClickAction
    {
        PostNew = 0,
        SelectExisting = 1
    }

    /// <summary>
    /// A placement click that lands on a pole already in the ground selects that flag.
    /// It must not post a second flag or escrow again. +/- then edits that bounty.
    /// </summary>
    public static class FlagClick
    {
        /// <summary>
        /// Poles snap to cell centers 1.5 m apart. Anything inside this radius is the same pole;
        /// the next cell is not.
        /// </summary>
        public const float SamePoleRadius = 0.9f;

        /// <summary>
        /// <paramref name="rayHit"/> wins when the cursor is on a flag's collider.
        /// Otherwise a flag already standing on the snapped cell is the same pole.
        /// </summary>
        public static FlagClickAction Resolve(
            IReadOnlyList<FlagHandle> posted,
            Vector3 snappedWorld,
            FlagHandle rayHit,
            out FlagHandle existing)
        {
            if (rayHit != null)
            {
                existing = rayHit;
                return FlagClickAction.SelectExisting;
            }

            existing = FindOnPole(posted, snappedWorld, SamePoleRadius);
            return existing != null ? FlagClickAction.SelectExisting : FlagClickAction.PostNew;
        }

        public static FlagHandle FindOnPole(IReadOnlyList<FlagHandle> posted, Vector3 world, float radius = SamePoleRadius)
        {
            if (posted == null || radius < 0f) return null;
            float bestSq = radius * radius;
            FlagHandle best = null;
            for (int i = 0; i < posted.Count; i++)
            {
                var flag = posted[i];
                if (flag == null) continue;
                float dx = flag.WorldPosition.x - world.x;
                float dz = flag.WorldPosition.z - world.z;
                float dSq = dx * dx + dz * dz;
                if (dSq > bestSq) continue;
                bestSq = dSq;
                best = flag;
            }
            return best;
        }

        /// <summary>
        /// Raise or lower a posted flag by <paramref name="delta"/>.
        /// Clamped to the flag's min and max. A raise the treasury cannot escrow leaves the flag
        /// and the stockpile unchanged and returns false. Sitting on the floor or the ceiling
        /// returns true and changes nothing.
        /// </summary>
        public static bool TryStepBounty(FlagManager flags, SimpleEconomy economy, FlagHandle flag, float delta)
        {
            if (flags == null || flag?.Data == null) return false;
            if (!flags.TryGet(flag.RuntimeId, out _)) return false;

            float next = Mathf.Clamp(flag.CurrentBounty + delta, flag.Data.minBounty, flag.Data.maxBounty);
            if (Mathf.Approximately(next, flag.CurrentBounty))
                return true;

            if (economy != null && !economy.TryAdjustBountyEscrow(flag, next))
                return false;

            flags.SetBounty(flag, next);
            return true;
        }
    }
}
