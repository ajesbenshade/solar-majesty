using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Built-in, deterministic reader for flag orders. Handles the common shapes without any model:
    /// "stay away unless you are level 9 or higher", "mechs only", "no scouts", "L5+ and healthy",
    /// "everyone except couriers", "strictly level 3 or lower". Text it cannot read yields no rules
    /// (the flag behaves normally); the optional Laya pass may fill those in.
    /// </summary>
    public static class FlagOrdersParser
    {
        const RegexOptions Opt = RegexOptions.CultureInvariant;

        static readonly (Regex re, int mask)[] ClassWords =
        {
            (W(@"defen[cs]e\s+mechs?"), FlagOrders.Bit(SpecialistClass.DefenseMech)),
            (W(@"sentinel(?:\s+mech)?s?"), FlagOrders.Bit(SpecialistClass.SentinelMech)),
            (W(@"(?<!defen[cs]e\s)(?<!sentinel\s)mechs?|fighters?|warriors?"),
                FlagOrders.Bit(SpecialistClass.DefenseMech) | FlagOrders.Bit(SpecialistClass.SentinelMech)),
            (W(@"engineers?(?:\s+bots?)?"), FlagOrders.Bit(SpecialistClass.EngineerBot)),
            (W(@"scouts?(?:\s+drones?)?|drones?"), FlagOrders.Bit(SpecialistClass.ScoutDrone)),
            (W(@"medics?|healers?|doctors?"), FlagOrders.Bit(SpecialistClass.Medic)),
            (W(@"harvesters?(?:\s+bots?)?"), FlagOrders.Bit(SpecialistClass.HarvesterBot)),
            (W(@"surveyors?(?:\s+bots?)?"), FlagOrders.Bit(SpecialistClass.SurveyorBot)),
            (W(@"terraformers?(?:\s+bots?)?"), FlagOrders.Bit(SpecialistClass.TerraformerBot)),
            (W(@"couriers?(?:\s+bots?)?"), FlagOrders.Bit(SpecialistClass.CourierBot)),
            (W(@"geologists?(?:\s+bots?)?"), FlagOrders.Bit(SpecialistClass.GeologistBot)),
        };

        const string Lvl = @"\b(?:level|l)\s*(\d{1,2})\b";
        static readonly Regex MinSuffix = new Regex(Lvl + @"\s*(?:\+|or\s+(?:higher|above|more|better|up|over))", Opt);
        static readonly Regex MinPrefix = new Regex(@"\b(?:at\s+least|minimum(?:\s+of)?|min\.?)\s*" + Lvl, Opt);
        static readonly Regex MinPost = new Regex(Lvl + @"\s*(?:minimum|min\b|at\s+least)", Opt);
        static readonly Regex Above = new Regex(@"\b(?:above|over|higher\s+than|more\s+than|beyond)\s*" + Lvl, Opt);
        static readonly Regex MaxSuffix = new Regex(Lvl + @"\s*(?:or\s+(?:lower|below|less|under))", Opt);
        static readonly Regex Range = new Regex(Lvl + @"\s*(?:-|to|through)\s*(?:level\s*)?(\d{1,2})\b", Opt);
        static readonly Regex MaxPrefix = new Regex(@"\b(?:at\s+most|maximum(?:\s+of)?|max\.?|up\s+to)\s*" + Lvl, Opt);
        static readonly Regex Below = new Regex(@"\b(?:below|under|lower\s+than|less\s+than|beneath)\s*" + Lvl, Opt);
        static readonly Regex BareLevel = new Regex(Lvl, Opt);

        static readonly Regex HealthPct = new Regex(@"(\d{1,3})\s*%\s*(?:health|hp)|(?:health|hp)\s*(?:of\s+|above\s+|over\s+|at\s+least\s+)?(\d{1,3})\s*%", Opt);
        static readonly Regex Healthy = new Regex(@"\b(?:healthy|full\s+(?:health|hp)|fully\s+healed|unhurt|uninjured)\b", Opt);
        static readonly Regex Hurt = new Regex(@"\b(?:hurt|injured|wounded|damaged|low\s+(?:health|hp)|beat\s+up)\b", Opt);

        static readonly Regex Forbid = new Regex(@"\b(?:stay\s+(?:away|out|off|clear)|keep\b[^.;!?]*?\b(?:away|out|off|clear)|don'?t|do\s+not|never|no|not|nobody|no\s+one|avoid|forbidden|banned|ignore)\b", Opt);
        static readonly Regex Only = new Regex(@"\b(?:only|just|exclusively|reserved\s+for)\b", Opt);
        static readonly Regex Pivot = new Regex(@"\b(?:unless|except(?:\s+(?:if|for|when))?|other\s+than|save\s+for)\b", Opt);
        static readonly Regex Strict = new Regex(@"\b(?:strict(?:ly)?|absolutely|no\s+exceptions?|under\s+no\s+circumstances|must|mandatory|forbidden|orders\s+are\s+orders)\b", Opt);
        static readonly Regex Clauses = new Regex(@"[.;!?,\n]+|\band\b|\bbut\b|\balso\b", Opt);

        static Regex W(string pattern) => new Regex(@"\b(?:" + pattern + @")\b", Opt);

        public static string ShortName(SpecialistClass cls) => cls switch
        {
            SpecialistClass.EngineerBot => "engineers",
            SpecialistClass.ScoutDrone => "scouts",
            SpecialistClass.DefenseMech => "defense mechs",
            SpecialistClass.Medic => "medics",
            SpecialistClass.HarvesterBot => "harvesters",
            SpecialistClass.SurveyorBot => "surveyors",
            SpecialistClass.TerraformerBot => "terraformers",
            SpecialistClass.CourierBot => "couriers",
            SpecialistClass.GeologistBot => "geologists",
            SpecialistClass.SentinelMech => "sentinels",
            _ => cls.ToString()
        };

        public static FlagOrders Parse(string text)
        {
            var o = new FlagOrders { text = text ?? "", source = "rules" };
            if (string.IsNullOrWhiteSpace(text)) return o;

            string t = Normalize(text);
            o.strict = Strict.IsMatch(t);
            t = Regex.Replace(t, @"\bno\s+exceptions?\b", " ", Opt);

            // Clauses split on punctuation, "and", "but". A clause with no polarity words of its own
            // ("no scouts and medics", "level 5+ and healthy") inherits the previous one's.
            bool carryAllow = true;
            foreach (string clause in Clauses.Split(t))
            {
                if (string.IsNullOrWhiteSpace(clause)) continue;
                var pivot = Pivot.Match(clause);
                if (pivot.Success)
                {
                    string before = clause.Substring(0, pivot.Index);
                    string after = clause.Substring(pivot.Index + pivot.Length);
                    bool beforeForbids = string.IsNullOrWhiteSpace(before)
                        ? !carryAllow
                        : Forbid.IsMatch(before);
                    // Without a forbid ("everyone except scouts") the pivot names who is excluded.
                    Apply(o, before, allow: !beforeForbids, conditionsOnly: beforeForbids);
                    Apply(o, after, allow: beforeForbids, conditionsOnly: false);
                    carryAllow = beforeForbids;
                }
                else
                {
                    bool allow = Only.IsMatch(clause) || (!Forbid.IsMatch(clause) && carryAllow);
                    Apply(o, clause, allow, conditionsOnly: false);
                    carryAllow = allow;
                }
            }

            int cap = OverseerRules.LevelCap;
            o.minLevel = o.minLevel > 0 ? Mathf.Clamp(o.minLevel, 1, cap) : 0;
            o.maxLevel = o.maxLevel > 0 ? Mathf.Clamp(o.maxLevel, 1, cap) : 0;
            if (o.minLevel <= 1) o.minLevel = 0;
            if (o.maxLevel >= cap) o.maxLevel = 0;
            if (o.minLevel > 0 && o.maxLevel > 0 && o.minLevel > o.maxLevel) o.maxLevel = 0;
            o.onlyClassMask &= ~o.bannedClassMask;
            return o;
        }

        /// <summary>
        /// Applies one condition segment. <paramref name="allow"/>: the segment describes who MAY go;
        /// otherwise who must NOT. With <paramref name="conditionsOnly"/> the segment is just the
        /// "stay away" half of a pivot, so bare class names there are ignored.
        /// </summary>
        static void Apply(FlagOrders o, string s, bool allow, bool conditionsOnly)
        {
            if (string.IsNullOrWhiteSpace(s)) return;

            var range = Range.Match(s);
            if (range.Success && allow)
            {
                SetMin(o, int.Parse(range.Groups[1].Value));
                SetMax(o, int.Parse(range.Groups[2].Value));
            }
            else if (TryLevel(s, out bool isMin, out int n))
            {
                // "unless level 9+" → min 9. "stay away if level 9+" → max 8. And the mirror for max.
                if (allow == isMin) SetMin(o, isMin ? n : n + 1);
                else SetMax(o, isMin ? n - 1 : n);
            }
            else if (allow && !conditionsOnly)
            {
                var bare = BareLevel.Match(s); // "level 9 heroes only"
                if (bare.Success) SetMin(o, int.Parse(bare.Groups[1].Value));
            }

            var pct = HealthPct.Match(s);
            if (pct.Success)
            {
                string g = pct.Groups[1].Success ? pct.Groups[1].Value : pct.Groups[2].Value;
                o.minHealth = Mathf.Max(o.minHealth, Mathf.Clamp01(int.Parse(g) / 100f));
            }
            else if (allow && Healthy.IsMatch(s)) o.minHealth = Mathf.Max(o.minHealth, 0.7f);
            else if (!allow && Hurt.IsMatch(s)) o.minHealth = Mathf.Max(o.minHealth, 0.5f);

            if (conditionsOnly) return;
            int mask = ClassMask(s);
            if (mask == 0) return;
            if (allow) o.onlyClassMask |= mask;
            else o.bannedClassMask |= mask;
        }

        static bool TryLevel(string s, out bool isMin, out int n)
        {
            Match m;
            if ((m = MinSuffix.Match(s)).Success || (m = MinPrefix.Match(s)).Success || (m = MinPost.Match(s)).Success)
            {
                isMin = true;
                n = int.Parse(m.Groups[1].Value);
                return true;
            }
            if ((m = Above.Match(s)).Success)
            {
                isMin = true;
                n = int.Parse(m.Groups[1].Value) + 1;
                return true;
            }
            if ((m = MaxSuffix.Match(s)).Success || (m = MaxPrefix.Match(s)).Success)
            {
                isMin = false;
                n = int.Parse(m.Groups[1].Value);
                return true;
            }
            if ((m = Below.Match(s)).Success)
            {
                isMin = false;
                n = int.Parse(m.Groups[1].Value) - 1;
                return true;
            }
            isMin = false;
            n = 0;
            return false;
        }

        static int ClassMask(string s)
        {
            int mask = 0;
            for (int i = 0; i < ClassWords.Length; i++)
                if (ClassWords[i].re.IsMatch(s)) mask |= ClassWords[i].mask;
            return mask;
        }

        static void SetMin(FlagOrders o, int n) => o.minLevel = Math.Max(o.minLevel, n);

        static void SetMax(FlagOrders o, int n)
        {
            if (n < 1) return;
            o.maxLevel = o.maxLevel > 0 ? Math.Min(o.maxLevel, n) : n;
        }

        static string Normalize(string text)
        {
            string t = text.ToLowerInvariant().Replace('’', '\'');
            t = Regex.Replace(t, @"\b(?:lvl|lv)\.?(?=\s*\d)", "level", Opt);
            t = Regex.Replace(t, @"\bl(?=\d)", "level ", Opt);
            // Protect "and up" / "and under" from the clause splitter.
            t = Regex.Replace(t, @"\band\s+(?=(?:up|above|higher|over|better|under|below|lower)\b)", "or ", Opt);
            return t;
        }
    }
}
