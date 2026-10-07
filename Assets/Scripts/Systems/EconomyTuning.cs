using System;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>One station-price override: this building category costs <see cref="cost"/> CRED for the first build.</summary>
    [Serializable]
    public struct BuildingCostOverride
    {
        public BuildingCategory category;
        [Min(0)] public int cost;
    }

    /// <summary>
    /// Inspector-editable economy numbers. Hold one in a MonoBehaviour with [SerializeField]
    /// (GameLoop does). Defaults match the original Majesty 2 mapping, so an untouched
    /// component plays exactly as before.
    /// </summary>
    [Serializable]
    public sealed class EconomyTuning
    {
        [Header("Timed intervals (simulation seconds, never frames)")]
        [Tooltip("Seconds between upkeep ticks.")]
        [Min(1f)] public float upkeepIntervalSeconds = 30f;
        [Tooltip("Base seconds between Earth resupply ships, before body / tech / replay scaling.")]
        [Min(1f)] public float resupplyBaseSeconds = 90f;
        [Tooltip("Resupply interval never drops below this, however much scaling is stacked.")]
        [Min(1f)] public float minResupplySeconds = 20f;
        [Tooltip("If a single Tick covers many intervals (long hitch, fast-forward), fire at most this many, then drop the backlog.")]
        [Range(1, 64)] public int maxCatchUpTicks = 8;

        [Header("Hero tax carry (Majesty 2 TaxCashCard)")]
        [Tooltip("On: heroes carry their guild tax and walk it in. Off: tax lands in the till instantly.")]
        public bool taxCarryEnabled = true;
        [Tooltip("Carried tax that sends a hero to its guild to pay (Majesty 2 SatietyBarrier 250).")]
        [Min(1)] public int taxCarryBarrier = 250;
        [Tooltip("Metres from the guild at which the tax is handed in.")]
        public float taxPayArrive = 3.5f;
        [Tooltip("Share of carried tax dropped (lost) when a hero is downed.")]
        [Range(0f, 1f)] public float taxDropOnDown = 0.5f;

        [Header("Caravan / trade-post payout (Majesty 2 caravans.set)")]
        [Tooltip("Our metres -> Majesty map units. 2.5 matches the old payout at 50 m.")]
        [Min(0.01f)] public float caravanDistanceScale = 2.5f;
        [Tooltip("Payout curve: x = Majesty distance, y = gold. Linear between points, flat beyond the ends.")]
        public Vector2[] caravanCurve = DefaultCaravanCurve();

        /// <summary>Majesty 2 table: 200 gold at 25 units, +100 per 25, up to 2,100 at 500 (gold = 100 + 4 x distance).</summary>
        public static Vector2[] DefaultCaravanCurve()
        {
            var pts = new Vector2[20];
            for (int i = 0; i < pts.Length; i++)
            {
                float d = 25f * (i + 1);
                pts[i] = new Vector2(d, 100f + 4f * d);
            }
            return pts;
        }

        /// <summary>Gold for a route of this many of our metres.</summary>
        public int CaravanGoldFor(float meters)
        {
            var c = caravanCurve;
            if (c == null || c.Length == 0) return 0;
            float x = Mathf.Max(0f, meters) * Mathf.Max(0.01f, caravanDistanceScale);
            if (x <= c[0].x) return Mathf.RoundToInt(c[0].y);
            for (int i = 1; i < c.Length; i++)
            {
                if (x > c[i].x) continue;
                float span = Mathf.Max(0.0001f, c[i].x - c[i - 1].x);
                return Mathf.RoundToInt(Mathf.Lerp(c[i - 1].y, c[i].y, (x - c[i - 1].x) / span));
            }
            return Mathf.RoundToInt(c[c.Length - 1].y);
        }

        [Header("Per-class shopping (Majesty 2 PurchaseManager)")]
        [Tooltip("Classes not listed shop with Medium need for everything (the original behaviour).")]
        public ClassShopPriority[] classShopPriorities = ClassShopPriority.Defaults();

        public ClassShopPriority ShopPriorityFor(SpecialistClass cls)
        {
            if (classShopPriorities != null)
                for (int i = 0; i < classShopPriorities.Length; i++)
                    if (classShopPriorities[i].specialistClass == cls) return classShopPriorities[i];
            return ClassShopPriority.Neutral(cls);
        }

        [Header("Extract yield (ore units per harvest, converted to CRED by GoldScale)")]
        public int metalsPerHarvest = 8;
        [Tooltip("Ice is harvested at this many units and paid at 3/4.")]
        public int icePerHarvest = 7;
        public int fissilePerHarvest = 5;
        [Tooltip("Any other deposit is harvested at this many units and paid at 1/2.")]
        public int otherPerHarvest = 10;
        [Tooltip("Ore units paid when there is no node: campus A / outposts.")]
        public int campusFallbackOre = 4;
        public int outpostFallbackOre = 3;

        [Header("Station building costs")]
        [Tooltip("Multiplies every first-build price (0.5 = half price). Rounded up to the nearest 10.")]
        [Range(0.1f, 5f)] public float buildingCostScale = 1f;
        [Tooltip("Each extra building of a type costs this x the previous one.")]
        [Range(1f, 3f)] public float duplicateMultiplier = 1.5f;
        [Tooltip("Per-category first-build price, replacing the Majesty 2 table (scale still applies).")]
        public BuildingCostOverride[] buildingCostOverrides = Array.Empty<BuildingCostOverride>();
    }
}
