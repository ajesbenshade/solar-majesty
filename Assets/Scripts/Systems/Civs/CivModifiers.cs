using System;
using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// One civ's rules, built from one <see cref="CivDefinition"/>: number changes, building art
    /// and unit lookups. A plain object, not static and not a scene object. <c>GameLoop</c> owns
    /// one today; multiplayer builds one per player and resolves it from the owner. It copies what
    /// it needs at construction, so editing the definition afterwards changes nothing.
    ///
    /// The maths is pure and fixed: for one stat, every Add is summed onto the base first, then
    /// every Multiply is applied as a whole percent in a single integer division, rounding half
    /// away from zero. The result does not depend on the order the modifiers are listed in, and
    /// uses no clock, randomness or floating point. A stat with no modifiers returns the base
    /// value untouched, so the neutral civ reproduces today's game exactly.
    /// </summary>
    public sealed class CivModifiers
    {
        /// <summary>At most this many Multiply entries per stat are used (keeps the product inside a long).</summary>
        public const int MaxMultipliersPerStat = 2;

        /// <summary>The largest percent a Multiply may carry (x10). Larger ones are ignored.</summary>
        public const int MaxPercent = 1000;

        private struct StatRule
        {
            public long Add;
            /// <summary>Product of the percents.</summary>
            public long Numerator;
            /// <summary>100 per multiplier.</summary>
            public long Denominator;
            public int Multipliers;
        }

        private readonly Dictionary<CivStat, StatRule> _rules = new Dictionary<CivStat, StatRule>();
        private readonly Dictionary<BuildingCategory, string> _art = new Dictionary<BuildingCategory, string>();
        private readonly Dictionary<SpecialistClass, CivUnitDefinition> _units =
            new Dictionary<SpecialistClass, CivUnitDefinition>();

        public CivId Id { get; }
        public string CastleKey { get; }
        public Color TeamPrimary { get; }
        public Color TeamSecondary { get; }

        /// <summary>
        /// Optional per-player colour, for telling apart two players who picked the same civ.
        /// It belongs to the owner, not the civ, so it is given here and not stored on the
        /// definition. The castle's Accent slot draws it (capped by <see cref="CivAccents.ForCastle"/>).
        /// TODO(multiplayer): pick accents per player slot.
        /// </summary>
        public Color? Accent { get; }

        /// <summary>True when this civ changes no number, art or unit.</summary>
        public bool IsIdentity => _rules.Count == 0 && _art.Count == 0 && _units.Count == 0;

        /// <summary>A null definition gives the neutral civ.</summary>
        public CivModifiers(CivDefinition def, Color? accent = null)
        {
            Accent = accent;
            if (def == null)
            {
                Id = CivId.None;
                CastleKey = CivDefinition.SharedArtKey;
                TeamPrimary = Color.white;
                TeamSecondary = Color.white;
                return;
            }

            Id = def.id;
            CastleKey = def.castleKey ?? CivDefinition.SharedArtKey;
            TeamPrimary = def.teamPrimary;
            TeamSecondary = def.teamSecondary;

            if (def.modifiers != null)
            {
                for (int i = 0; i < def.modifiers.Count; i++)
                    AddRule(def.modifiers[i]);
            }

            if (def.buildingArt != null)
            {
                for (int i = 0; i < def.buildingArt.Count; i++)
                {
                    var entry = def.buildingArt[i];
                    if (string.IsNullOrEmpty(entry.artKey) || _art.ContainsKey(entry.building)) continue;
                    _art.Add(entry.building, entry.artKey);
                }
            }

            if (def.roster != null)
            {
                for (int i = 0; i < def.roster.Count; i++)
                {
                    var unit = def.roster[i];
                    if (unit == null || unit.IsBase || _units.ContainsKey(unit.baseClass)) continue;
                    _units.Add(unit.baseClass, unit.Clone());
                }
            }
        }

        /// <summary>A fresh service for <paramref name="id"/> from the catalog. Unknown ids give the neutral civ.</summary>
        public static CivModifiers For(CivId id, Color? accent = null) =>
            new CivModifiers(CivCatalog.Get(id), accent);

        private void AddRule(CivModifier m)
        {
            if (m.stat == CivStat.None) return;
            if (!_rules.TryGetValue(m.stat, out var rule))
                rule = new StatRule { Add = 0, Numerator = 1, Denominator = 1, Multipliers = 0 };

            if (m.op == CivModifierOp.Add)
            {
                if (m.value == 0) return;
                rule.Add += m.value;
            }
            else
            {
                // 100% changes nothing; a negative percent has no meaning.
                if (m.value == 100 || m.value < 0) return;
                if (m.value > MaxPercent)
                {
                    Debug.LogWarning($"[Civ] {Id}: {m.value}% on {m.stat} is over {MaxPercent}%; ignored.");
                    return;
                }
                if (rule.Multipliers >= MaxMultipliersPerStat)
                {
                    Debug.LogWarning($"[Civ] {Id}: more than {MaxMultipliersPerStat} multipliers on {m.stat}; the extra one is ignored.");
                    return;
                }
                rule.Numerator *= m.value;
                rule.Denominator *= 100;
                rule.Multipliers++;
            }
            _rules[m.stat] = rule;
        }

        /// <summary>True when this civ changes <paramref name="stat"/> at all.</summary>
        public bool Changes(CivStat stat) => _rules.ContainsKey(stat);

        /// <summary><paramref name="baseValue"/> after this civ's changes to <paramref name="stat"/>.</summary>
        public int Apply(CivStat stat, int baseValue)
        {
            if (!_rules.TryGetValue(stat, out var rule)) return baseValue;
            long result = ApplyRule(rule, baseValue);
            if (result > int.MaxValue) return int.MaxValue;
            if (result < int.MinValue) return int.MinValue;
            return (int)result;
        }

        /// <summary>
        /// <see cref="Apply(CivStat,int)"/> for a price: a changed price rounds up to the next 10,
        /// as building prices already do. An unchanged stat returns the base value as it is.
        /// </summary>
        public int ApplyPrice(CivStat stat, int basePrice)
        {
            if (!_rules.ContainsKey(stat)) return basePrice;
            int price = Apply(stat, basePrice);
            if (price <= 0) return 0;
            long rounded = ((long)price + 9L) / 10L * 10L;
            return rounded > int.MaxValue ? int.MaxValue : (int)rounded;
        }

        /// <summary>
        /// For the few stats the game keeps as floats (gun range, damage per second). The value is
        /// worked in whole thousandths with the same integer maths, so two machines given the same
        /// base agree exactly. An unchanged stat returns <paramref name="baseValue"/> bit for bit.
        /// </summary>
        public float Apply(CivStat stat, float baseValue)
        {
            if (!_rules.TryGetValue(stat, out var rule)) return baseValue;
            long milli = (long)Math.Round(baseValue * 1000.0, MidpointRounding.AwayFromZero);
            var scaled = rule;
            scaled.Add = rule.Add * 1000L;
            return ApplyRule(scaled, milli) / 1000f;
        }

        private static long ApplyRule(StatRule rule, long baseValue)
        {
            long sum = baseValue + rule.Add;
            if (rule.Multipliers == 0) return sum;
            return DivideRounded(sum * rule.Numerator, rule.Denominator);
        }

        /// <summary>Integer division rounding half away from zero. <paramref name="denominator"/> is positive.</summary>
        private static long DivideRounded(long numerator, long denominator)
        {
            long half = denominator / 2;
            return numerator >= 0
                ? (numerator + half) / denominator
                : -((-numerator + half) / denominator);
        }

        /// <summary>
        /// The art this civ uses for <paramref name="building"/>, or
        /// <see cref="CivDefinition.SharedArtKey"/> when it has none of its own.
        /// </summary>
        public string ArtKeyFor(BuildingCategory building) =>
            _art.TryGetValue(building, out string key) ? key : CivDefinition.SharedArtKey;

        /// <summary>True when this civ has its own art for <paramref name="building"/>.</summary>
        public bool HasArtFor(BuildingCategory building) => _art.ContainsKey(building);

        /// <summary>
        /// This civ's unit for the role <paramref name="baseClass"/>. A role the civ does not list
        /// comes back as the base class with no changes (<see cref="CivUnitDefinition.IsBase"/>).
        /// </summary>
        public CivUnitDefinition UnitFor(SpecialistClass baseClass) =>
            _units.TryGetValue(baseClass, out var unit) ? unit : CivUnitDefinition.Base(baseClass);

        /// <summary>True when this civ fields its own unit for <paramref name="baseClass"/>.</summary>
        public bool HasUnitFor(SpecialistClass baseClass) => _units.ContainsKey(baseClass);
    }
}
