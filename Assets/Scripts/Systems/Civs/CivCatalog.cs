using System;
using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// The civilizations as data. The table below is the default. A <see cref="CivDatabase"/> at
    /// <c>Resources/Data/CivDatabase</c> overrides it entry by entry (see Docs/DATA_CATALOGS.md).
    /// <see cref="CivId.None"/> is always the built-in neutral civ; an asset cannot change it.
    ///
    /// DRAFT NUMBERS: the perk values are the design doc's proposals and have not been playtested.
    /// This holds definitions only. Which civ a player has is never stored here.
    /// </summary>
    public static class CivCatalog
    {
        private static CivDefinition[] _defaults;
        private static IReadOnlyList<CivDefinition> _all;

        public static IReadOnlyList<CivDefinition> All
        {
            get
            {
                if (_all != null) return _all;
                var db = CatalogDatabase.Load<CivDatabase>(CivDatabase.ResourcePath, out bool settled);
                var resolved = Resolve(db);
                if (settled) _all = resolved;
                return resolved;
            }
        }

        /// <summary>The built-in table, ignoring any database asset.</summary>
        public static IReadOnlyList<CivDefinition> Defaults => _defaults ??= Build();

        /// <summary>True when a database asset is supplying civs.</summary>
        public static bool UsingDatabase { get; private set; }

        /// <summary>Use <paramref name="db"/> from now on; null pins the built-in table.</summary>
        public static void UseDatabase(CivDatabase db) => _all = Resolve(db);

        /// <summary>Forget the resolved table; the next read loads the Resources asset again.</summary>
        public static void ReloadDatabase()
        {
            _all = null;
            UsingDatabase = false;
        }

        private static IReadOnlyList<CivDefinition> Resolve(CivDatabase db)
        {
            UsingDatabase = db != null && db.civs != null && db.civs.Count > 0;
            if (!UsingDatabase) return Defaults;
            return CatalogDatabase.Merge(
                Defaults,
                db.ToDefs(Default),
                d => d.id,
                id => id != CivId.None && Enum.IsDefined(typeof(CivId), id),
                "CivDatabase");
        }

        /// <summary>The built-in definition for <paramref name="id"/>, or null.</summary>
        public static CivDefinition Default(CivId id) => Find(Defaults, id);

        /// <summary>The definition for <paramref name="id"/>, or null when the id is not a civ.</summary>
        public static CivDefinition Get(CivId id) => Find(All, id);

        private static CivDefinition Find(IReadOnlyList<CivDefinition> list, CivId id)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].id == id)
                    return list[i];
            }
            return null;
        }

        /// <summary>True when <paramref name="saved"/> is the number of a civ this build knows (None included).</summary>
        public static bool IsKnown(int saved) => Enum.IsDefined(typeof(CivId), saved);

        /// <summary><paramref name="saved"/> as a civ; anything this build does not know becomes None.</summary>
        public static CivId FromSaved(int saved) => IsKnown(saved) ? (CivId)saved : CivId.None;

        /// <summary>The civs a player can pick, in card order. The neutral civ and placeholders are not among them.</summary>
        public static List<CivDefinition> Playable()
        {
            var list = new List<CivDefinition>();
            var all = All;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null && all[i].id != CivId.None && all[i].playable)
                    list.Add(all[i]);
            }
            list.Sort((a, b) => ((int)a.id).CompareTo((int)b.id));
            return list;
        }

        private static CivDefinition[] Build()
        {
            return new[]
            {
                // Neutral: no modifiers, shared art, base classes. Reproduces the game as it is today.
                new CivDefinition
                {
                    id = CivId.None,
                    displayName = "Standard",
                    pitch = "The colony as it is today",
                    summary = "No perks and no drawback.",
                    castleKey = CivDefinition.SharedArtKey,
                    // Today's white hull and orange stripe, as a placeholder until the palette PR
                    // reads the kit's real constants.
                    teamPrimary = new Color(0.90f, 0.89f, 0.86f),
                    teamSecondary = new Color(0.96f, 0.42f, 0.08f)
                },

                new CivDefinition
                {
                    id = CivId.LumenConcord,
                    displayName = "Lumen Concord",
                    pitch = "Heroes and bounties",
                    summary = "Bounties cost the treasury 10% less. Hires 20% cheaper. Buildings pay 20% less tax.",
                    castleKey = "castle.ziggurat_keep",
                    playable = true,
                    teamPrimary = new Color(0.93f, 0.90f, 0.82f),   // ivory
                    teamSecondary = new Color(0.66f, 0.44f, 0.20f), // bronze
                    modifiers = new List<CivModifier>
                    {
                        CivModifier.Percent(CivStat.BountyTreasuryCost, 90),
                        CivModifier.Percent(CivStat.HireCost, 80),
                        CivModifier.Percent(CivStat.BuildingDailyTax, 80, tradeOff: true)
                    },
                    roster = new List<CivUnitDefinition>
                    {
                        Unit(SpecialistClass.DefenseMech, "Oathblade",
                            Stats((CivUnitStat.Greed, 0.18f))),
                        Unit(SpecialistClass.SurveyorBot, "Lumen Archer",
                            abilities: Abilities(CivAbilityChange.UnlockAt("surveyor_arc_bolt", 1))),
                        Unit(SpecialistClass.Medic, "Chorister",
                            Stats((CivUnitStat.CombatPreference, 0.35f))),
                        // New behaviour (phase 3): nearby heroes join its party when it takes a flag.
                        Unit(SpecialistClass.ScoutDrone, "Herald", behaviourTag: "herald_rally"),
                        Unit(SpecialistClass.EngineerBot, "Mason",
                            Stats((CivUnitStat.Greed, 0.70f)))
                    }
                },

                new CivDefinition
                {
                    id = CivId.TetherExchange,
                    displayName = "Tether Exchange",
                    pitch = "Economy and trade",
                    summary = "Three tax collectors per Commons. Trade pays 20% more. Starting treasury 15% smaller.",
                    castleKey = "castle.ring_citadel",
                    playable = true,
                    teamPrimary = new Color(0.16f, 0.17f, 0.19f),   // graphite
                    teamSecondary = new Color(0.55f, 0.36f, 0.86f), // violet
                    modifiers = new List<CivModifier>
                    {
                        CivModifier.Add(CivStat.CollectorsPerCommons, 1),
                        CivModifier.Percent(CivStat.TradeIncome, 120),
                        CivModifier.Percent(CivStat.StartingTreasury, 85, tradeOff: true)
                    },
                    roster = new List<CivUnitDefinition>
                    {
                        // New behaviour (phase 3): escorts a loaded tax collector.
                        Unit(SpecialistClass.SentinelMech, "Gate Warden", behaviourTag: "gate_warden_escort"),
                        Unit(SpecialistClass.GeologistBot, "Assayer",
                            Stats((CivUnitStat.ExtractPreference, 1.0f)),
                            Abilities(CivAbilityChange.UnlockAt("geologist_core_shot", 1))),
                        Unit(SpecialistClass.Medic, "Fixer",
                            Stats((CivUnitStat.MoveSpeed, 4.0f), (CivUnitStat.Courage, 0.35f))),
                        Unit(SpecialistClass.CourierBot, "Runner",
                            Stats((CivUnitStat.ExtractPreference, 0.85f)),
                            Abilities(CivAbilityChange.UnlockAt("courier_hauler", 3))),
                        Unit(SpecialistClass.HarvesterBot, "Prospector",
                            Stats((CivUnitStat.WorkRate, 1.40f)))
                    }
                },

                new CivDefinition
                {
                    id = CivId.BastionMandate,
                    displayName = "Bastion Mandate",
                    pitch = "Defence and turtle",
                    summary = "Commons health 200. Guns reach 21 m. Watchtower lasers cost 250. Hires 20% dearer.",
                    castleKey = "castle.dome_bunker",
                    playable = true,
                    teamPrimary = new Color(0.56f, 0.64f, 0.50f),   // sage
                    teamSecondary = new Color(0.541f, 0.231f, 0.141f), // oxide red, about #8A3B24
                    modifiers = new List<CivModifier>
                    {
                        CivModifier.Add(CivStat.CommonsHealth, 60),
                        CivModifier.Add(CivStat.GunRange, 3),
                        // The gun damage perk (4 -> 5) moved off the civ onto the Bulwark and the
                        // Picket as a near-home bonus; see NearHomeTag below.
                        CivModifier.Add(CivStat.WatchtowerArmingCost, -150),
                        CivModifier.Percent(CivStat.HireCost, 120, tradeOff: true)
                    },
                    roster = new List<CivUnitDefinition>
                    {
                        // TODO(design): "prefers flags near home" needs a number (ClassAllure.distanceMul)
                        // before it can be data; the doc gives none.
                        Unit(SpecialistClass.DefenseMech, "Bulwark",
                            Stats((CivUnitStat.MoveSpeed, 2.7f)),
                            Abilities(CivAbilityChange.UnlockAt("mech_bulwark", 1)),
                            NearHomeTag),
                        // TODO(design): "homebody" needs the same distance number as the Bulwark.
                        Unit(SpecialistClass.TerraformerBot, "Siege Lancer",
                            abilities: Abilities(CivAbilityChange.Radius("terra_thermal", 4f))),
                        // New behaviour (phase 3): also repairs damaged buildings.
                        Unit(SpecialistClass.Medic, "Field Welder", behaviourTag: "field_welder_repair"),
                        Unit(SpecialistClass.ScoutDrone, "Picket",
                            Stats((CivUnitStat.ExplorePreference, 0.8f), (CivUnitStat.DefendPreference, 0.5f)),
                            behaviourTag: NearHomeTag),
                        Unit(SpecialistClass.EngineerBot, "Sapper",
                            Stats((CivUnitStat.Courage, 0.40f)))
                    }
                },

                // Placeholder: the id and the colours are real, the rest waits for the design.
                // Until then it plays as the neutral civ and is not offered on the cards.
                new CivDefinition
                {
                    id = CivId.CivD,
                    displayName = "CivD",
                    pitch = "",
                    summary = "",
                    castleKey = CivDefinition.SharedArtKey,
                    playable = false,
                    teamPrimary = new Color(0.059f, 0.165f, 0.165f),  // deep teal-black, #0F2A2A
                    teamSecondary = new Color(0.122f, 0.643f, 0.388f) // emerald, #1FA463
                }
            };
        }

        /// <summary>
        /// Marks a unit that is due a bonus while fighting near home (the Mandate's old gun damage
        /// perk, moved onto its Bulwark and Picket). TODO: nothing reads this yet; it needs a
        /// radius, an amount and a hook in hero combat, none of which the design has fixed.
        /// </summary>
        public const string NearHomeTag = "near_home_guard";

        private static CivUnitDefinition Unit(
            SpecialistClass baseClass,
            string name,
            List<CivUnitStatChange> stats = null,
            List<CivAbilityChange> abilities = null,
            string behaviourTag = "")
        {
            return new CivUnitDefinition
            {
                baseClass = baseClass,
                displayName = name,
                stats = stats ?? new List<CivUnitStatChange>(),
                abilities = abilities ?? new List<CivAbilityChange>(),
                behaviourTag = behaviourTag
            };
        }

        private static List<CivUnitStatChange> Stats(params (CivUnitStat stat, float value)[] changes)
        {
            var list = new List<CivUnitStatChange>(changes.Length);
            for (int i = 0; i < changes.Length; i++)
                list.Add(new CivUnitStatChange(changes[i].stat, changes[i].value));
            return list;
        }

        private static List<CivAbilityChange> Abilities(params CivAbilityChange[] changes) =>
            new List<CivAbilityChange>(changes);
    }
}
