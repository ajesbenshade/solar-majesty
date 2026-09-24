using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Player's free-text orders on a bounty flag ("stay away unless you're level 9+"), turned into
    /// a small fixed set of rules once at post time. <see cref="FlagOrdersRules"/> enforces them as a
    /// gate in <see cref="SpecialistBrain"/> — the text is never re-read per tick, and ScoreFlag is
    /// untouched. Serialized into saves as-is.
    /// </summary>
    [Serializable]
    public sealed class FlagOrders
    {
        /// <summary>What the player typed, verbatim.</summary>
        public string text = "";
        /// <summary>Lowest hero level allowed; 0 = no limit.</summary>
        public int minLevel;
        /// <summary>Highest hero level allowed; 0 = no limit.</summary>
        public int maxLevel;
        /// <summary>Bit per <see cref="SpecialistClass"/>; 0 = every class may go.</summary>
        public int onlyClassMask;
        /// <summary>Bit per <see cref="SpecialistClass"/> that must stay away.</summary>
        public int bannedClassMask;
        /// <summary>Minimum health 0–1 to take the job; 0 = no limit.</summary>
        public float minHealth;
        /// <summary>
        /// Strict orders bind everyone. Otherwise a greedy, broke hero may bend them — Majesty heroes
        /// are not soldiers.
        /// </summary>
        public bool strict;
        /// <summary>Who understood the text: "rules" (built-in parser) or "laya" (local model).</summary>
        public string source = "rules";

        public bool HasRules =>
            minLevel > 0 || maxLevel > 0 || onlyClassMask != 0 || bannedClassMask != 0 || minHealth > 0f;

        public static int Bit(SpecialistClass cls) => 1 << (int)cls;

        public bool Allows(SpecialistClass cls)
        {
            int bit = Bit(cls);
            if ((bannedClassMask & bit) != 0) return false;
            return onlyClassMask == 0 || (onlyClassMask & bit) != 0;
        }

        /// <summary>Short read-back for the HUD / flag label, e.g. "L9+ · mechs only · strict".</summary>
        public string Summary()
        {
            if (!HasRules) return "";
            var parts = new List<string>(5);
            if (minLevel > 0 && maxLevel > 0) parts.Add($"L{minLevel}–{maxLevel}");
            else if (minLevel > 0) parts.Add($"L{minLevel}+");
            else if (maxLevel > 0) parts.Add($"L{maxLevel} or lower");
            if (onlyClassMask != 0) parts.Add(ClassList(onlyClassMask) + " only");
            if (bannedClassMask != 0) parts.Add("no " + ClassList(bannedClassMask));
            if (minHealth > 0f) parts.Add($"HP {Mathf.RoundToInt(minHealth * 100f)}%+");
            if (strict) parts.Add("strict");
            return string.Join(" · ", parts);
        }

        static string ClassList(int mask)
        {
            var sb = new StringBuilder();
            foreach (SpecialistClass cls in Enum.GetValues(typeof(SpecialistClass)))
            {
                if ((mask & Bit(cls)) == 0) continue;
                if (sb.Length > 0) sb.Append('/');
                sb.Append(FlagOrdersParser.ShortName(cls));
            }
            return sb.ToString();
        }
    }

    /// <summary>Enforces <see cref="FlagOrders"/> for one hero. Pure; used by SpecialistBrain.</summary>
    public static class FlagOrdersRules
    {
        /// <summary>Greed and pay hunger at or above this let a hero bend non-strict orders.</summary>
        public const float BendGreed = 0.7f;
        public const float BendHunger = 0.7f;

        /// <summary>True if the flag's orders let this hero take it.</summary>
        public static bool Permits(in SpecialistContext ctx, FlagHandle flag)
        {
            var o = flag?.Orders;
            if (o == null || !o.HasRules || ctx.Data == null) return true;
            if (Obeys(ctx, o)) return true;
            // Majesty heroes are mercenaries: a greedy, broke one may ignore soft orders.
            return !o.strict && ctx.Data.baseGreed >= BendGreed && ctx.GreedHunger >= BendHunger;
        }

        static bool Obeys(in SpecialistContext ctx, FlagOrders o)
        {
            int level = Mathf.Max(1, ctx.Level);
            if (o.minLevel > 0 && level < o.minLevel) return false;
            if (o.maxLevel > 0 && level > o.maxLevel) return false;
            if (!o.Allows(ctx.Data.specialistClass)) return false;
            if (o.minHealth > 0f && ctx.HealthNormalized < o.minHealth) return false;
            return true;
        }
    }
}
