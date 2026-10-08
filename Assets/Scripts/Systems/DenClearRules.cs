using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Who earned a fauna kill. The last hero to land a hit is the killing blow.
    /// If that blow had no hero (a turret, a drone with no owner, a bare status tick),
    /// the hero who dealt the most damage is paid instead. No hero at all stays unpaid.
    /// </summary>
    public sealed class DamageLedger
    {
        public object LastHitter { get; private set; }
        private readonly Dictionary<object, float> _totals = new Dictionary<object, float>();

        public void Note(object hitter, float amount)
        {
            if (hitter == null || amount <= 0f) return;
            LastHitter = hitter;
            if (_totals.TryGetValue(hitter, out float prev))
                _totals[hitter] = prev + amount;
            else
                _totals[hitter] = amount;
        }

        public object Payee()
        {
            if (LastHitter != null) return LastHitter;
            object best = null;
            float bestAmount = 0f;
            foreach (var pair in _totals)
            {
                if (pair.Value <= bestAmount) continue;
                bestAmount = pair.Value;
                best = pair.Key;
            }
            return best;
        }
    }

    /// <summary>
    /// Clear Threat poles on one den are one job. The treasury already escrowed every
    /// posting, so the surviving pole keeps the sum and pays it once.
    /// </summary>
    public static class ClearThreatMerge
    {
        public static FlagHandle Nearest(IReadOnlyList<FlagHandle> flags, Vector3 world, float radius)
        {
            if (flags == null || radius < 0f) return null;
            float bestSq = radius * radius;
            FlagHandle best = null;
            for (int i = 0; i < flags.Count; i++)
            {
                var flag = flags[i];
                if (!IsClear(flag)) continue;
                float dx = flag.WorldPosition.x - world.x;
                float dz = flag.WorldPosition.z - world.z;
                float dSq = dx * dx + dz * dz;
                if (dSq > bestSq) continue;
                bestSq = dSq;
                best = flag;
            }
            return best;
        }

        /// <summary>Fold every same-den cluster. Returns how many extra poles were removed.</summary>
        public static int CollapseAll(FlagManager flags, float radius)
        {
            if (flags == null) return 0;
            int removed = 0;
            for (int i = 0; i < flags.Flags.Count; i++)
            {
                var flag = flags.Flags[i];
                if (!IsClear(flag)) continue;
                int n = Collapse(flags, flag.WorldPosition, radius);
                if (n <= 0) continue;
                removed += n;
                i = -1;
            }
            return removed;
        }

        /// <summary>Fold poles near <paramref name="den"/> into one. The keeper's escrow becomes the sum.</summary>
        public static int Collapse(FlagManager flags, Vector3 den, float radius)
        {
            if (flags == null) return 0;
            Collect(flags, den, radius, out FlagHandle keeper, out int pay, out List<FlagHandle> extras);
            if (keeper == null || extras.Count == 0) return 0;
            keeper.CurrentBounty = pay;
            keeper.EscrowMetals = pay;
            for (int i = 0; i < extras.Count; i++)
                flags.Cancel(extras[i]);
            return extras.Count;
        }

        /// <summary>
        /// Remove every Clear Threat on this den and return the summed escrow for a single payout.
        /// </summary>
        public static int Take(FlagManager flags, Vector3 den, float radius, out int escrow)
        {
            escrow = 0;
            if (flags == null) return 0;
            Collect(flags, den, radius, out FlagHandle keeper, out int pay, out List<FlagHandle> extras);
            if (keeper == null) return 0;
            escrow = pay;
            for (int i = 0; i < extras.Count; i++)
                flags.Cancel(extras[i]);
            flags.Cancel(keeper);
            return pay;
        }

        static void Collect(
            FlagManager flags, Vector3 den, float radius,
            out FlagHandle keeper, out int pay, out List<FlagHandle> extras)
        {
            keeper = null;
            pay = 0;
            extras = new List<FlagHandle>();
            float bestDone = -1f;
            float rSq = radius * radius;
            var list = flags.Flags;
            for (int i = 0; i < list.Count; i++)
            {
                var flag = list[i];
                if (!IsClear(flag)) continue;
                float dx = flag.WorldPosition.x - den.x;
                float dz = flag.WorldPosition.z - den.z;
                if (dx * dx + dz * dz > rSq) continue;
                pay += FlagBountySync.Amount(flag);
                float done = flag.PostedWork - flags.GetWorkRemaining(flag);
                if (keeper == null || done > bestDone)
                {
                    if (keeper != null) extras.Add(keeper);
                    keeper = flag;
                    bestDone = done;
                }
                else
                    extras.Add(flag);
            }
        }

        static bool IsClear(FlagHandle flag) =>
            flag != null && flag.Data != null && flag.Data.flagType == FlagType.ClearThreat;
    }

    /// <summary>Which saved fauna a Continue is allowed to put back.</summary>
    public static class FaunaRestoreCap
    {
        public static bool[] Keep(int[] lairIndex, float[] health, int cap)
        {
            int n = lairIndex == null ? 0 : lairIndex.Length;
            var keep = new bool[n];
            if (n == 0 || cap <= 0) return keep;

            var seen = new List<int>();
            for (int i = 0; i < n; i++)
            {
                int lair = lairIndex[i];
                bool known = false;
                for (int s = 0; s < seen.Count; s++)
                {
                    if (seen[s] != lair) continue;
                    known = true;
                    break;
                }
                if (!known) seen.Add(lair);
            }

            for (int s = 0; s < seen.Count; s++)
            {
                int lair = seen[s];
                for (int k = 0; k < cap; k++)
                {
                    int best = -1;
                    float bestHealth = float.NegativeInfinity;
                    for (int i = 0; i < n; i++)
                    {
                        if (keep[i] || lairIndex[i] != lair) continue;
                        float h = health != null && i < health.Length ? health[i] : 0f;
                        if (h <= bestHealth) continue;
                        bestHealth = h;
                        best = i;
                    }
                    if (best < 0) break;
                    keep[best] = true;
                }
            }
            return keep;
        }
    }

    /// <summary>
    /// A scrapped robot is a wreck of that name, not a vacancy the next sibling fills.
    /// </summary>
    public static class ScrapRoster
    {
        public static bool IsSameHero(SpecialistRecord a, SpecialistRecord b)
        {
            if (a.Class != b.Class) return false;
            if (string.IsNullOrEmpty(a.Name) || string.IsNullOrEmpty(b.Name)) return false;
            return a.Name == b.Name;
        }

        /// <summary>
        /// Keep the corpse unless that same hero is already in the living list.
        /// An unnamed corpse is dropped when any living robot of the class exists,
        /// which is how older snapshots stored one record per class.
        /// </summary>
        public static bool ShouldKeepCorpse(IReadOnlyList<SpecialistRecord> living, SpecialistRecord corpse)
        {
            if (living == null) return true;
            bool unnamed = string.IsNullOrEmpty(corpse.Name);
            for (int i = 0; i < living.Count; i++)
            {
                if (living[i].Corpse) continue;
                if (IsSameHero(living[i], corpse)) return false;
                if (unnamed && living[i].Class == corpse.Class) return false;
            }
            return true;
        }
    }

    /// <summary>One overseer line per window, summing every purse a pest walked off with.</summary>
    public sealed class PurseTheftWindow
    {
        public readonly float Seconds;
        private readonly List<string> _places = new List<string>();
        private float _start = -1f;
        private int _amount;

        public PurseTheftWindow(float seconds)
        {
            Seconds = seconds > 0f ? seconds : 60f;
        }

        public int Amount => _amount;
        public int Buildings => _places.Count;

        /// <summary>
        /// Add a theft. When this opens a new window, <paramref name="flushed"/> is the
        /// previous window's line.
        /// </summary>
        public bool Note(float now, int amount, string where, out string flushed)
        {
            flushed = null;
            if (amount <= 0) return false;
            bool closed = false;
            if (_start >= 0f && now - _start >= Seconds)
            {
                flushed = Line();
                closed = true;
                Clear();
            }
            if (_start < 0f) _start = now;
            _amount += amount;
            AddPlace(where);
            return closed;
        }

        public bool Flush(float now, bool force, out string line)
        {
            line = null;
            if (_amount <= 0 || _start < 0f) return false;
            if (!force && now - _start < Seconds) return false;
            line = Line();
            Clear();
            return true;
        }

        string Line() => CompactGrok.PestsLifted(_amount, _places.Count);

        void AddPlace(string where)
        {
            if (string.IsNullOrEmpty(where)) where = "a building";
            for (int i = 0; i < _places.Count; i++)
            {
                if (_places[i] == where) return;
            }
            _places.Add(where);
        }

        void Clear()
        {
            _start = -1f;
            _amount = 0;
            _places.Clear();
        }
    }

    /// <summary>
    /// The settings slider previews a scale and writes it when the mouse comes up,
    /// so the panel does not resize under the cursor mid-drag.
    /// </summary>
    public struct HudScaleGesture
    {
        public bool Dragging;
        public float Preview;
        public float Applied;

        public float Shown => Dragging ? Preview : Applied;

        public void SyncApplied(float applied)
        {
            Applied = applied;
            if (!Dragging) Preview = applied;
        }

        public void Cancel()
        {
            Dragging = false;
            Preview = Applied;
        }

        public void Drag(float slider)
        {
            float snapped = HudScaleMath.RoundToStep(slider);
            if (Mathf.Approximately(snapped, HudScaleMath.RoundToStep(Shown))) return;
            Dragging = true;
            Preview = snapped;
        }

        public bool TryCommit(bool mouseUp, out float commit)
        {
            commit = Preview;
            if (!Dragging || !mouseUp) return false;
            Dragging = false;
            Applied = HudScaleMath.RoundToStep(Preview);
            commit = Applied;
            return true;
        }
    }

    /// <summary>One floating label for every flag of the same type on the same target.</summary>
    public static class FlagLabelStack
    {
        public struct Item
        {
            public Vector3 World;
            public string Title;
            public int Bounty;
            public int Order;
            public FlagType Type;
        }

        public static string Caption(string title, int count, int bounty)
        {
            if (count <= 1) return null;
            if (string.IsNullOrEmpty(title)) title = "Flag";
            return title + " ×" + count.ToString() + " — " + bounty.ToString("N0") + " EU";
        }

        public static bool IsRepresentative(IReadOnlyList<Item> all, int index, float radius)
        {
            if (all == null || index < 0 || index >= all.Count) return true;
            return Representative(all, index, radius) == index;
        }

        public static void Sum(IReadOnlyList<Item> all, int index, float radius, out int count, out int bounty)
        {
            count = 0;
            bounty = 0;
            if (all == null || index < 0 || index >= all.Count) return;
            var me = all[index];
            float rSq = radius * radius;
            for (int i = 0; i < all.Count; i++)
            {
                var other = all[i];
                if (other.Type != me.Type) continue;
                float dx = other.World.x - me.World.x;
                float dz = other.World.z - me.World.z;
                if (dx * dx + dz * dz > rSq) continue;
                count++;
                bounty += other.Bounty;
            }
            if (count == 0)
            {
                count = 1;
                bounty = me.Bounty;
            }
        }

        static int Representative(IReadOnlyList<Item> all, int index, float radius)
        {
            var me = all[index];
            int best = index;
            int bestOrder = me.Order;
            float rSq = radius * radius;
            for (int i = 0; i < all.Count; i++)
            {
                if (i == index) continue;
                var other = all[i];
                if (other.Type != me.Type) continue;
                float dx = other.World.x - me.World.x;
                float dz = other.World.z - me.World.z;
                if (dx * dx + dz * dz > rSq) continue;
                if (other.Order < bestOrder || (other.Order == bestOrder && i < best))
                {
                    best = i;
                    bestOrder = other.Order;
                }
            }
            return best;
        }
    }
}
