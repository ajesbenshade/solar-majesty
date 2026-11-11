using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// The game's numbers as one owner's civ sees them. Each function takes today's value from
    /// its existing home (<see cref="MajestyEconomy"/>, <see cref="OverseerRules"/>, the world
    /// profile) and passes it through that owner's <see cref="CivModifiers"/>. Pure functions:
    /// the civ is always an argument, never looked up, so two players can differ. A null or
    /// neutral civ returns today's value exactly.
    ///
    /// Call sites pass <c>GameLoop.Civ</c> today. Nothing here names a civ.
    ///
    /// The perk stats and where they are read (keep this list in step with the call sites):
    /// bounty pay (<see cref="SimpleEconomy.ReleaseBountyEscrow"/>), hire cost (GameLoop hire,
    /// Fobot Yard bill, re-fab), building tax (VillageExpansion tills), collectors (LevyCollector),
    /// trade pay (mine loads, caravans, Market), starting treasury (GameLoop starter stockpile),
    /// Commons health (VillageExpansion), gun range and laser arming (GameLoop batteries and
    /// Watchtower arming, HUD, map overlay), outpost building cost and build time
    /// (<see cref="BuildingPlacer"/>), hero speed (SpecialistAgent.EffectiveMoveSpeed), and the
    /// near-home unit damage (SpecialistAgent strikes).
    /// TODO(civ UI, later PR): tooltips and cards that print these numbers show the civ's value
    /// where they already read it here; the perk deltas ("Scout 120, -20%") are not drawn yet.
    /// </summary>
    public static class CivRules
    {
        private static int Apply(CivModifiers civ, CivStat stat, int value) =>
            civ != null ? civ.Apply(stat, value) : value;

        private static float Apply(CivModifiers civ, CivStat stat, float value) =>
            civ != null ? civ.Apply(stat, value) : value;

        private static int Price(CivModifiers civ, CivStat stat, int value) =>
            civ != null ? civ.ApplyPrice(stat, value) : value;

        // ---- Hires -------------------------------------------------------------------------

        /// <summary>Workshop hire fee for <paramref name="cls"/>. A changed fee rounds up to the next 10.</summary>
        public static int HireCost(CivModifiers civ, SpecialistClass cls) =>
            Price(civ, CivStat.HireCost, MajestyEconomy.HireCost(cls));

        /// <summary>Fobot Yard bill for a known class: built on the hire fee, so it moves with it.</summary>
        public static int YardBill(CivModifiers civ, int level, SpecialistClass cls) =>
            MajestyEconomy.ResurrectCost(HireCost(civ, cls), level);

        /// <summary>
        /// Re-fab price at a workshop. A class workshop charges the hire fee; anything else keeps
        /// <see cref="OverseerRules.RefabMetals"/>, which is not a hire.
        /// </summary>
        public static int RefabMetals(CivModifiers civ, BuildingData data)
        {
            var cls = data != null ? MajestyEconomy.ClassForWorkshop(data.category) : null;
            return cls.HasValue ? HireCost(civ, cls.Value) : OverseerRules.RefabMetals(data);
        }

        /// <summary>Gold a newly hired hero starts with.</summary>
        public static int HeroStartingPurse(CivModifiers civ) =>
            Mathf.Max(0, Apply(civ, CivStat.HeroStartingPurse, MajestyEconomy.HeroStartingPurse));

        // ---- Bounties ----------------------------------------------------------------------

        /// <summary>What a completed bounty of <paramref name="bounty"/> finally costs the treasury.</summary>
        public static int BountyTreasuryCost(CivModifiers civ, int bounty) =>
            bounty <= 0 ? 0 : Mathf.Max(0, Apply(civ, CivStat.BountyTreasuryCost, bounty));

        /// <summary>
        /// Gold the treasury gets back when a bounty it escrowed in full is paid out. The flag,
        /// the pole and the hero's pay stay one number (the escrow); the civ's share comes back
        /// here, on completion. Zero for the neutral civ, and never negative.
        /// </summary>
        public static int BountyRebate(CivModifiers civ, int escrow) =>
            escrow <= 0 ? 0 : Mathf.Max(0, escrow - BountyTreasuryCost(civ, escrow));

        // ---- Income ------------------------------------------------------------------------

        /// <summary>
        /// Daily tax for one building. The Market's take is also trade, so it is the one building
        /// both the tax stat and the trade stat touch.
        /// </summary>
        public static int DailyTax(CivModifiers civ, BuildingCategory cat)
        {
            int tax = Apply(civ, CivStat.BuildingDailyTax, MajestyEconomy.DailyTax(cat));
            if (cat == BuildingCategory.Market)
                tax = Apply(civ, CivStat.TradeIncome, tax);
            return Mathf.Max(0, tax);
        }

        /// <summary>A mine's daily load or a caravan's landing, <paramref name="gold"/> being today's value.</summary>
        public static int TradeGold(CivModifiers civ, int gold) =>
            Mathf.Max(0, Apply(civ, CivStat.TradeIncome, gold));

        /// <summary>Tax collectors each Commons fields.</summary>
        public static int CollectorsPerCommons(CivModifiers civ) =>
            Mathf.Max(0, Apply(civ, CivStat.CollectorsPerCommons, MajestyEconomy.CollectorsPerCommons));

        /// <summary><see cref="MajestyEconomy.CollectorsFor"/> with this civ's collectors per Commons. The cap is shared.</summary>
        public static int CollectorsFor(CivModifiers civ, int commons, int watchtowers)
        {
            if (commons <= 0) return 0;
            int n = commons * CollectorsPerCommons(civ) +
                    Mathf.Max(0, watchtowers) * MajestyEconomy.CollectorsPerWatchtower;
            return Mathf.Clamp(n, 0, MajestyEconomy.MaxCollectors);
        }

        /// <summary>The world's starting gold, before the challenge scale.</summary>
        public static int StartingTreasury(CivModifiers civ, int startMetals) =>
            Mathf.Max(0, Apply(civ, CivStat.StartingTreasury, startMetals));

        // ---- Defence -----------------------------------------------------------------------

        /// <summary>Hit points of a Commons (<see cref="OverseerRules.CommonsHealth"/> for the neutral civ).</summary>
        public static float CommonsHealth(CivModifiers civ) =>
            Mathf.Max(1f, Apply(civ, CivStat.CommonsHealth, OverseerRules.CommonsHealth));

        /// <summary>Reach of Laser Turrets and armed Watchtowers, in metres.</summary>
        public static float GunRange(CivModifiers civ) =>
            Mathf.Max(0f, Apply(civ, CivStat.GunRange, OverseerRules.BatteryRange));

        /// <summary>Damage per second of Laser Turrets and armed Watchtowers, before guild bonuses.</summary>
        public static float GunDamage(CivModifiers civ) =>
            Mathf.Max(0f, Apply(civ, CivStat.GunDamage, OverseerRules.BatteryDps));

        /// <summary>Price of arming a Watchtower with lasers.</summary>
        public static int WatchtowerArmingCost(CivModifiers civ) =>
            Price(civ, CivStat.WatchtowerArmingCost, OverseerRules.WatchtowerLaserCost);

        // ---- Buildings ---------------------------------------------------------------------

        /// <summary>
        /// The outpost buildings: the Watchtower, the Ore Mine and the Landing Pad. Their price
        /// is the one <see cref="CivStat.OutpostBuildingCost"/> changes.
        /// </summary>
        public static bool IsOutpostBuilding(BuildingCategory cat) =>
            cat == BuildingCategory.Watchtower ||
            cat == BuildingCategory.Mine ||
            cat == BuildingCategory.LandingPad;

        /// <summary>
        /// Price of the first building of <paramref name="cat"/>, <paramref name="basePrice"/> being
        /// today's. Only outpost buildings change, and a changed price rounds up to the next 10.
        /// The duplicate surcharge is applied on top of this, as it is to today's price.
        /// </summary>
        public static int BuildingPrice(CivModifiers civ, BuildingCategory cat, int basePrice) =>
            IsOutpostBuilding(cat) && basePrice > 0 ? Price(civ, CivStat.OutpostBuildingCost, basePrice) : basePrice;

        /// <summary>Seconds a player-placed building takes to go up, <paramref name="seconds"/> being today's.</summary>
        public static float BuildSeconds(CivModifiers civ, float seconds) =>
            Mathf.Max(0f, Apply(civ, CivStat.BuildTime, seconds));

        // ---- Heroes in the field -----------------------------------------------------------

        /// <summary>A hero's walking speed before gear and statuses, <paramref name="speed"/> being the class value.</summary>
        public static float HeroMoveSpeed(CivModifiers civ, float speed) =>
            Mathf.Max(0f, Apply(civ, CivStat.HeroMoveSpeed, speed));

        /// <summary>
        /// Factor on a hero's strike damage. Above 1 only when <paramref name="cls"/> is one of the
        /// civ's units marked <see cref="CivUnitDefinition.nearHomeBonus"/> and the hero is near
        /// home (<paramref name="nearHome"/>: within <see cref="OverseerRules.CommonsShadeRadius"/>
        /// of a live Commons). Exactly 1 otherwise, so the neutral civ is unchanged.
        /// </summary>
        public static float StrikeDamageMul(CivModifiers civ, SpecialistClass cls, bool nearHome)
        {
            if (!HasNearHomeBonus(civ, cls) || !nearHome) return 1f;
            int percent = civ.Apply(CivStat.NearHomeUnitDamage, 100);
            return Mathf.Max(0, percent) / 100f;
        }

        /// <summary>
        /// True when this civ's unit for <paramref name="cls"/> is due the near-home bonus. Cheap,
        /// so a caller can skip the distance check for everyone else.
        /// </summary>
        public static bool HasNearHomeBonus(CivModifiers civ, SpecialistClass cls) =>
            civ != null && civ.Changes(CivStat.NearHomeUnitDamage) &&
            civ.HasUnitFor(cls) && civ.UnitFor(cls).nearHomeBonus;
    }
}
