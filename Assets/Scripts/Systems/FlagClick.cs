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

            // The reserved metals are the bounty. A drifted pair snaps together before the step
            // so +/- cannot raise a number the treasury never held.
            FlagBountySync.AdoptReserved(flag);

            float next = Mathf.Clamp(flag.CurrentBounty + delta, flag.Data.minBounty, flag.Data.maxBounty);
            int cost = SimpleEconomy.BountyMetalsCost(next);
            if (cost == flag.EscrowMetals && Mathf.Approximately(cost, flag.CurrentBounty))
                return true;

            if (economy != null && !economy.TryAdjustBountyEscrow(flag, cost))
                return false;

            flags.SetBounty(flag, cost);
            flag.EscrowMetals = cost;
            if (economy != null)
                economy.MatchReserved(FlagBountySync.Sum(flags.Flags));
            return true;
        }
    }

    public enum FlagBountyEdit
    {
        None = 0,
        PostedFlag = 1,
        NextPost = 2
    }

    /// <summary>
    /// One bounty number. The flag panel, the pole, the escrow on that flag, and the
    /// treasury's reserved total all read it. Keys and the panel's − / + both call
    /// <see cref="Apply"/>.
    /// </summary>
    public static class FlagBountySync
    {
        /// <summary>
        /// A selected pole is edited. With the flag tool armed and nothing selected, +/− only
        /// sets the next post. Otherwise it does not touch any flag.
        /// </summary>
        public static FlagBountyEdit EditFor(bool flagSelected, bool flagToolArmed)
        {
            if (flagSelected) return FlagBountyEdit.PostedFlag;
            if (flagToolArmed) return FlagBountyEdit.NextPost;
            return FlagBountyEdit.None;
        }

        /// <summary>Closing the flag panel drops the pole selection so +/− cannot keep editing it.</summary>
        public static bool ClearSelectionOnTool(bool toolIsFlag) => !toolIsFlag;

        /// <summary>What the panel prints. A selected pole shows that pole; otherwise the next post.</summary>
        public static int PanelAmount(FlagHandle selected, float nextPostBounty, FlagData armed)
        {
            if (selected != null) return Amount(selected);
            return PostCost(armed, nextPostBounty);
        }

        /// <summary>The number on the pole and the bounty a hero is paid.</summary>
        public static int Amount(FlagHandle flag)
        {
            if (flag == null) return 0;
            if (flag.EscrowMetals > 0) return flag.EscrowMetals;
            return flag.CurrentBounty > 0f ? SimpleEconomy.BountyMetalsCost(flag.CurrentBounty) : 0;
        }

        /// <summary>IN BOUNTIES: the metals reserved on flags that are still standing.</summary>
        public static int Sum(IReadOnlyList<FlagHandle> flags)
        {
            int sum = 0;
            if (flags == null) return 0;
            for (int i = 0; i < flags.Count; i++)
            {
                var flag = flags[i];
                if (flag == null) continue;
                sum += flag.EscrowMetals;
            }
            return sum;
        }

        /// <summary>The integer a new post will escrow. Clamped to the flag type before any metals move.</summary>
        public static int PostCost(FlagData data, float requested)
        {
            float clamped = data != null
                ? Mathf.Clamp(requested, data.minBounty, data.maxBounty)
                : requested;
            return SimpleEconomy.BountyMetalsCost(clamped);
        }

        public static float StepPending(float pending, FlagData armed, float delta)
        {
            float next = pending + delta;
            if (armed != null)
                next = Mathf.Clamp(next, armed.minBounty, armed.maxBounty);
            return next;
        }

        /// <summary>
        /// If the treasury reserved a different number than the bounty on the handle, the reserved
        /// metals win. Nothing is spent or refunded here.
        /// </summary>
        public static void AdoptReserved(FlagHandle flag)
        {
            if (flag == null || flag.EscrowMetals <= 0) return;
            flag.CurrentBounty = flag.EscrowMetals;
        }

        /// <summary>
        /// Keys and both − / + buttons. A selected pole steps through the treasury.
        /// The flag tool with nothing selected only moves the next-post bounty.
        /// </summary>
        public static bool Apply(
            FlagManager flags,
            SimpleEconomy economy,
            FlagHandle selected,
            bool flagToolArmed,
            ref float nextPostBounty,
            FlagData armed,
            float delta,
            out bool treasuryShort)
        {
            treasuryShort = false;
            switch (EditFor(selected != null, flagToolArmed))
            {
                case FlagBountyEdit.PostedFlag:
                    if (flags == null || selected == null || !flags.TryGet(selected.RuntimeId, out _))
                        return false;
                    bool ok = FlagClick.TryStepBounty(flags, economy, selected, delta);
                    if (!ok) treasuryShort = true;
                    return ok;
                case FlagBountyEdit.NextPost:
                    nextPostBounty = StepPending(nextPostBounty, armed, delta);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Escrow and post the same integer. Returns null when the treasury cannot cover it.
        /// </summary>
        public static FlagHandle TryPost(
            FlagManager flags,
            SimpleEconomy economy,
            FlagData data,
            Vector3 world,
            float requested)
        {
            if (flags == null || data == null) return null;
            int cost = PostCost(data, requested);
            if (economy != null && !economy.TryEscrowBounty(cost, out _))
                return null;

            var handle = flags.Post(data, world, cost);
            handle.CurrentBounty = cost;
            handle.EscrowMetals = cost;
            if (economy != null)
                economy.MatchReserved(Sum(flags.Flags));
            return handle;
        }
    }
}
