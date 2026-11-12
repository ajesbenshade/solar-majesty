using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// What the HUD says about the player's civ: the badge, signature unit names, and the perk
    /// delta printed next to a price ("Herald 120 EU, -20%"). Pure functions over the owner's
    /// <see cref="CivModifiers"/> (GameLoop.Civ today, one per player later); nothing is looked up
    /// or stored here, and nothing names a civ. The neutral civ, and a null civ, get no badge, no
    /// delta and the base class names.
    ///
    /// Every charged number comes from the same <see cref="CivRules"/> call the game charges with,
    /// and the base number from the same call with no civ, so a tooltip can never show one price
    /// and charge another.
    ///
    /// Player copy: the canon scan (PlayerFacingCanonTests) reads this file like any other.
    /// </summary>
    public static class CivHud
    {
        // ---- badge ------------------------------------------------------------------------------

        /// <summary>
        /// The definition the badge draws, or null when there is no badge: a null civ, the neutral
        /// civ (tutorial, old saves), or an id the catalog does not know.
        /// </summary>
        public static CivDefinition BadgeCiv(CivModifiers civ)
        {
            if (civ == null) return null;
            var def = CivCatalog.Get(civ.Id);
            return def == null || def.id == CivId.None ? null : def;
        }

        public static bool HasBadge(CivModifiers civ) => BadgeCiv(civ) != null;

        /// <summary>The badge's words in HUD caps ("TETHER EXCHANGE"), or null when there is no badge.</summary>
        public static string BadgeText(CivModifiers civ)
        {
            var def = BadgeCiv(civ);
            if (def == null) return null;
            string name = def.DisplayNameText;
            return string.IsNullOrEmpty(name) ? null : name.ToUpperInvariant();
        }

        /// <summary>
        /// Longest first, for a narrow badge: the full name, then its last word ("EXCHANGE",
        /// "ACCORD"). Empty when there is no badge.
        /// </summary>
        public static string[] BadgeVariants(CivModifiers civ)
        {
            string full = BadgeText(civ);
            if (string.IsNullOrEmpty(full)) return new string[0];
            int space = full.LastIndexOf(' ');
            if (space <= 0 || space >= full.Length - 1) return new[] { full };
            return new[] { full, full.Substring(space + 1) };
        }

        /// <summary>The colour the civ is recognised by (its team secondary), for the badge stripe.</summary>
        public static Color BadgeColour(CivModifiers civ) => civ != null ? civ.TeamSecondary : Color.white;

        /// <summary>
        /// The badge's tooltip: the civ's pitch and perks, and the Commons hit points as this civ
        /// has them. <paramref name="commonsHp"/> / <paramref name="commonsMax"/> are the live
        /// Commons' numbers; pass a negative max when there is no Commons standing.
        /// </summary>
        public static HudTip BadgeTip(CivModifiers civ, float commonsHp, float commonsMax)
        {
            var def = BadgeCiv(civ);
            if (def == null) return default;
            string hp = commonsMax > 0f
                ? Loc.F("Commons {0} HP.", HpText(commonsHp, commonsMax))
                : Loc.F("Commons {0} HP.", CommonsMaxHp(civ));
            string body = def.PitchText + ". " + def.SummaryText + "\n" + hp;
            return new HudTip(BadgeText(civ), body);
        }

        // ---- Commons hit points ---------------------------------------------------------------

        /// <summary>A Commons' full hit points for this civ, as VillageExpansion builds it.</summary>
        public static int CommonsMaxHp(CivModifiers civ) => Mathf.RoundToInt(CivRules.CommonsHealth(civ));

        /// <summary>
        /// "current/max" in whole hit points. A standing building never reads 0: the current
        /// value rounds up, so 0.4 HP left shows as 1.
        /// </summary>
        public static string HpText(float current, float max)
        {
            int m = Mathf.Max(0, Mathf.RoundToInt(max));
            int c = Mathf.Clamp(Mathf.CeilToInt(current - 0.001f), 0, m);
            return c + "/" + m;
        }

        // ---- unit names -------------------------------------------------------------------------

        private static readonly Dictionary<SpecialistClass, string> BaseNames = new Dictionary<SpecialistClass, string>();

        /// <summary>Today's name for <paramref name="cls"/> ("Defense Mech"), from the shared personality table.</summary>
        public static string BaseUnitName(SpecialistClass cls)
        {
            if (BaseNames.TryGetValue(cls, out string name)) return Loc.S(name);
            var data = ScriptableObject.CreateInstance<SpecialistData>();
            data.specialistClass = cls;
            SpecialistPersonality.Apply(data);
            name = string.IsNullOrEmpty(data.displayName) ? cls.ToString() : data.displayName;
            Object.DestroyImmediate(data);
            BaseNames[cls] = name;
            return Loc.S(name);
        }

        /// <summary>
        /// The name a unit of <paramref name="cls"/> wears for this civ: its signature name
        /// ("Oathblade") when the civ fields its own unit for the role, otherwise
        /// <paramref name="baseName"/> (today's class name) unchanged.
        /// </summary>
        public static string UnitName(CivModifiers civ, SpecialistClass cls, string baseName)
        {
            if (civ != null && civ.HasUnitFor(cls))
            {
                string own = civ.UnitFor(cls).DisplayNameText;
                if (!string.IsNullOrEmpty(own)) return own;
            }
            return baseName;
        }

        /// <summary><see cref="UnitName(CivModifiers, SpecialistClass, string)"/> with today's class name as the fallback.</summary>
        public static string UnitName(CivModifiers civ, SpecialistClass cls) => UnitName(civ, cls, BaseUnitName(cls));

        // ---- price deltas -------------------------------------------------------------------------

        /// <summary>
        /// Whole-percent change from <paramref name="basePrice"/> to <paramref name="price"/>,
        /// rounding half away from zero (150 -> 120 is -20, 400 -> 250 is -38). Zero when the base is not positive.
        /// </summary>
        public static int PercentChange(int basePrice, int price)
        {
            if (basePrice <= 0) return 0;
            long num = ((long)price - basePrice) * 100L;
            long half = basePrice / 2;
            long pct = num >= 0 ? (num + half) / basePrice : -((-num + half) / basePrice);
            return (int)pct;
        }

        /// <summary>
        /// The short delta printed after a price: "-20%", "+20%". Empty when the price is
        /// unchanged (never "+0%"). A change too small to show as a percent reads in EU ("-5 EU").
        /// </summary>
        public static string Delta(int basePrice, int price)
        {
            if (price == basePrice || basePrice <= 0) return "";
            int pct = PercentChange(basePrice, price);
            if (pct != 0) return (pct > 0 ? "+" : "-") + Mathf.Abs(pct) + "%";
            int eu = price - basePrice;
            return (eu > 0 ? "+" : "-") + Mathf.Abs(eu) + " EU";
        }

        /// <summary><paramref name="cost"/> followed by <paramref name="delta"/> ("120 EU, -20%"); just the cost when there is no delta.</summary>
        public static string WithDelta(string cost, string delta) =>
            string.IsNullOrEmpty(delta) ? cost : cost + ", " + delta;

        /// <summary>A charged price with its delta: "120 EU, -20%", or "150 EU" when the civ does not move it.</summary>
        public static string PriceText(int basePrice, int price) =>
            WithDelta(Loc.F("{0:N0} EU", price), Delta(basePrice, price));

        // ---- hires ------------------------------------------------------------------------------

        /// <summary>The hire fee the game charges for <paramref name="cls"/> (GameLoop.TryFabricateRobot).</summary>
        public static int HirePrice(CivModifiers civ, SpecialistClass cls) => CivRules.HireCost(civ, cls);

        /// <summary>"Hire: Herald 120 EU, -20%." The unit's civ name, the charged fee and the perk delta.</summary>
        public static string HireLine(CivModifiers civ, SpecialistClass cls) =>
            Loc.F("Hire: {0} {1}.", UnitName(civ, cls),
                PriceText(CivRules.HireCost(null, cls), HirePrice(civ, cls)));

        /// <summary>The hire line for a workshop's build-list entry, or null when the building hires no bot.</summary>
        public static string WorkshopHireLine(CivModifiers civ, BuildingCategory cat)
        {
            var cls = MajestyEconomy.ClassForWorkshop(cat);
            return cls.HasValue ? HireLine(civ, cls.Value) : null;
        }

        /// <summary>Re-fab price at a workshop, as charged (<see cref="CivRules.RefabMetals"/>), with its delta.</summary>
        public static string RefabPriceText(CivModifiers civ, BuildingData data) =>
            PriceText(CivRules.RefabMetals(null, data), CivRules.RefabMetals(civ, data));

        // ---- bounties -----------------------------------------------------------------------------

        /// <summary>
        /// What a finished bounty of <paramref name="bounty"/> finally costs the till: the escrow
        /// less the civ's share that comes back on completion (SimpleEconomy.ReleaseBountyEscrow).
        /// </summary>
        public static int BountyTillPrice(CivModifiers civ, int bounty) =>
            bounty <= 0 ? 0 : bounty - CivRules.BountyRebate(civ, bounty);

        /// <summary>True when this civ's till pays less (or more) than the posted bounty.</summary>
        public static bool BountyChanged(CivModifiers civ, int bounty) => BountyTillPrice(civ, bounty) != bounty;

        /// <summary>
        /// The price line on a flag's tooltip. Unchanged: "Bounty 400 EU." Changed: the bounty, then
        /// what the till pays in the end with its delta ("360 EU, -10%").
        /// </summary>
        public static string FlagPriceLine(CivModifiers civ, int bounty)
        {
            int till = BountyTillPrice(civ, bounty);
            if (till == bounty) return Loc.F("Bounty {0:N0} EU.", bounty);
            return Loc.F("Bounty {0:N0} EU. The till pays {1}; the rest comes back when a bot finishes.",
                bounty, PriceText(bounty, till));
        }

        /// <summary>
        /// The extra line on the bounty readout's tooltip, or null when the civ does not change
        /// what the till pays: "The till pays 360 EU, -10% once a bot finishes it."
        /// </summary>
        public static string BountyTillLine(CivModifiers civ, int bounty)
        {
            if (!BountyChanged(civ, bounty)) return null;
            return Loc.F("The till pays {0} once a bot finishes it.", PriceText(bounty, BountyTillPrice(civ, bounty)));
        }

        // ---- buildings ----------------------------------------------------------------------------

        /// <summary>Gold in a cost, the way the build list sums it.</summary>
        public static int SumCost(ResourceAmount[] cost)
        {
            if (cost == null) return 0;
            int total = 0;
            for (int i = 0; i < cost.Length; i++)
                total += cost[i].amount;
            return total;
        }

        /// <summary>
        /// The perk delta on a building's price: the civ's first price against today's
        /// (<see cref="CivRules.BuildingPrice"/>, the call the placer charges with). Empty for a
        /// building the civ does not price differently. The duplicate surcharge goes on top of
        /// both alike, so the delta holds for every copy.
        /// </summary>
        public static string BuildingDelta(CivModifiers civ, BuildingData data)
        {
            if (data == null || data.buildCost == null) return "";
            int first = SumCost(data.buildCost);
            int metals = 0;
            for (int i = 0; i < data.buildCost.Length; i++)
            {
                if (data.buildCost[i].resource == ResourceId.Metals)
                    metals += data.buildCost[i].amount;
            }
            int civMetals = CivRules.BuildingPrice(civ, data.category, metals);
            return Delta(first, first - metals + civMetals);
        }

        /// <summary>Laser arming price as charged (<see cref="CivRules.WatchtowerArmingCost"/>), with its delta.</summary>
        public static string ArmingPriceText(CivModifiers civ) =>
            PriceText(CivRules.WatchtowerArmingCost(null), CivRules.WatchtowerArmingCost(civ));
    }
}
