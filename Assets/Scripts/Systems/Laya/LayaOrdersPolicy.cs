using System;
using System.Collections.Generic;

namespace SolarMajesty
{
    /// <summary>
    /// Reads flag orders the built-in parser could not, with the local Laya model: one request of
    /// fixed-menu questions (minimum level, which class, strict or not). The model can only pick
    /// menu entries, so the result is always a valid <see cref="FlagOrders"/>.
    /// </summary>
    public static class LayaOrdersPolicy
    {
        public const string MinLevelId = "min_level";
        public const string ClassId = "who";
        public const string StrictId = "strict";
        /// <summary>Top probability needed before a menu pick becomes a rule.</summary>
        public const float MinProbability = 0.5f;
        public const float StrictProbability = 0.7f;

        public static string BuildState(string ordersText) =>
            $"Written orders pinned to a bounty flag for autonomous colony heroes: \"{ordersText}\"";

        public static List<LayaQuestion> BuildQuestions()
        {
            var levels = new List<LayaOption> { new LayaOption { Label = "any", Description = "No level requirement; any hero may go." } };
            for (int l = 2; l <= OverseerRules.LevelCap; l++)
                levels.Add(new LayaOption { Label = "level_" + l, Description = $"Only heroes level {l} or higher may go." });

            var classes = new List<LayaOption> { new LayaOption { Label = "anyone", Description = "Any kind of hero may go." } };
            foreach (SpecialistClass cls in Enum.GetValues(typeof(SpecialistClass)))
                classes.Add(new LayaOption { Label = cls.ToString(), Description = $"Only {FlagOrdersParser.ShortName(cls)} may go." });

            return new List<LayaQuestion>
            {
                new LayaQuestion { Id = MinLevelId, Type = "choice", Instructions = "What minimum hero level do these orders require?", Options = levels },
                new LayaQuestion { Id = ClassId, Type = "choice", Instructions = "Which kind of hero do these orders allow?", Options = classes },
                new LayaQuestion { Id = StrictId, Type = "noul", Instructions = "Are these orders absolute, with no exceptions allowed?" }
            };
        }

        /// <summary>Fills <paramref name="orders"/> from confident answers. True if any rule was set.</summary>
        public static bool Apply(FlagOrders orders, IReadOnlyDictionary<string, LayaChoice> answers)
        {
            if (orders == null || answers == null) return false;
            bool any = false;

            if (Top(answers, MinLevelId, out string lvl) && lvl.StartsWith("level_", StringComparison.Ordinal) &&
                int.TryParse(lvl.Substring(6), out int n) && n >= 2 && n <= OverseerRules.LevelCap)
            {
                orders.minLevel = n;
                any = true;
            }

            if (Top(answers, ClassId, out string who) && who != "anyone" &&
                Enum.TryParse(who, out SpecialistClass cls) && Enum.IsDefined(typeof(SpecialistClass), cls))
            {
                orders.onlyClassMask = FlagOrders.Bit(cls);
                any = true;
            }

            if (any && answers.TryGetValue(StrictId, out var s) && !float.IsNaN(s.Noul) && s.Noul >= StrictProbability)
                orders.strict = true;

            if (any) orders.source = "laya";
            return any;
        }

        static bool Top(IReadOnlyDictionary<string, LayaChoice> answers, string id, out string label)
        {
            label = null;
            if (!answers.TryGetValue(id, out var a) || a.Probabilities.Count == 0) return false;
            float best = -1f;
            foreach (var kv in a.Probabilities)
            {
                if (kv.Value > best)
                {
                    best = kv.Value;
                    label = kv.Key;
                }
            }
            return best >= MinProbability;
        }
    }
}
