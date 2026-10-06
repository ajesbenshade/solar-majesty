using System;
using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>When a robot reaches for an ability.</summary>
    public enum AbilityTrigger
    {
        /// <summary>Attack used on the current target while fighting.</summary>
        Strike,
        /// <summary>Self buff used when a fight starts.</summary>
        Engage,
        /// <summary>Self buff used while hurt in a fight (health below the hurt line).</summary>
        Hurt,
        /// <summary>Self buff used while running for safety.</summary>
        Flee,
        /// <summary>Buff on the ally a medic is patching.</summary>
        Ally
    }

    /// <summary>Which fauna a strike will target (Majesty 2 target groups: beast, undead...).</summary>
    public enum FaunaFilter
    {
        Any,
        /// <summary>Stalkers (Majesty beasts).</summary>
        Stalkers,
        /// <summary>Campus pests.</summary>
        Pests,
        /// <summary>Junk bots (Majesty undead).</summary>
        Junk
    }

    /// <summary>
    /// One class ability (Majesty 2 unit_actions.xml). Damage is in seconds of the robot's normal
    /// strike (Majesty f_skill_mod: a x3 strike = three seconds of hits in one blow).
    /// </summary>
    [Serializable]
    public struct ClassAbilityDef
    {
        public string id;
        public string displayName;
        public SpecialistClass specialistClass;
        public AbilityTrigger trigger;
        [Tooltip("Robot level needed (Majesty 0 / 5 / 10 maps to our 1 / 3 / 6).")]
        [Range(1, 10)] public int minLevel;
        [Min(0f)] public float cooldownSeconds;
        [Tooltip("Seconds of normal strike damage dealt in one blow (0 = no damage).")]
        [Min(0f)] public float damageMul;
        [Tooltip("Metres. 0 = the target only.")]
        [Min(0f)] public float aoeRadius;
        public FaunaFilter filter;
        [Tooltip("Status applied to whoever is hit (or to self / ally for buffs).")]
        public bool appliesStatus;
        public StatusKind status;
        public float statusMagnitude;
        [Min(0f)] public float statusDuration;
        [Min(0f)] public float statusPeriod;

        [Tooltip("Everything hit must attack this robot for tauntDuration (Majesty 2 s_taunt).")]
        public bool taunts;
        [Min(0f)] public float tauntDuration;

        [Tooltip("Companion to deploy (id from AbilityTuning.companions). Empty = none.")]
        public string summonId;
        [Tooltip("Most of this companion one robot may have out at once (Majesty i_cap).")]
        [Min(0)] public int summonCap;
    }

    /// <summary>
    /// A deployable companion (Majesty 2 summon_wolf / summon_bear). It follows its robot,
    /// fights what threatens it, and the heavy one taunts. Hull is in robot hulls (1 = one robot).
    /// </summary>
    [Serializable]
    public struct CompanionDef
    {
        public string id;
        public string displayName;
        [Min(0.1f)] public float hull;
        [Tooltip("Damage per second to fauna (a robot's normal strike is about 8).")]
        [Min(0f)] public float dps;
        [Min(0.1f)] public float moveSpeed;
        [Tooltip("Seconds before it powers down. Majesty summons last until killed; ours expire so they need no saving.")]
        [Min(1f)] public float lifetime;
        [Tooltip("Metres from its robot it will go to fight.")]
        [Min(1f)] public float leash;
        public bool taunts;
        [Min(0f)] public float tauntRadius;
        [Min(0f)] public float tauntCooldown;
        [Min(0f)] public float tauntDuration;
        public Color color;
        [Tooltip("Visual size multiplier.")]
        [Min(0.1f)] public float scale;
    }

    /// <summary>Inspector-editable class abilities and status tuning. Held by GameLoop.</summary>
    [Serializable]
    public sealed class AbilityTuning
    {
        public bool enabled = true;
        [Tooltip("Seconds between any two ability uses by one robot.")]
        [Min(0f)] public float globalCooldown = 1.5f;
        [Tooltip("Hurt-trigger buffs fire below this health.")]
        [Range(0f, 1f)] public float hurtLine = 0.6f;
        [Tooltip("Engage buffs and summons also fire when local danger reaches this, fight or not.")]
        [Range(0f, 1f)] public float threatLine = 0.35f;
        public ClassAbilityDef[] abilities = Defaults();
        public CompanionDef[] companions = DefaultCompanions();

        public bool TryGetCompanion(string id, out CompanionDef def)
        {
            if (companions != null && !string.IsNullOrEmpty(id))
                for (int i = 0; i < companions.Length; i++)
                    if (companions[i].id == id) { def = companions[i]; return true; }
            def = default;
            return false;
        }

        /// <summary>
        /// Majesty summon_wolf (10x its master's health, steady damage) and summon_bear (sturdier
        /// still, with the warrior taunt), resized to our fights.
        /// </summary>
        public static CompanionDef[] DefaultCompanions() => new[]
        {
            new CompanionDef { id = "escort_drone", displayName = "Escort Drone", hull = 2f, dps = 8f, moveSpeed = 4.6f,
                lifetime = 90f, leash = 10f, color = new Color(0.55f, 0.8f, 1f), scale = 0.55f },
            new CompanionDef { id = "hauler_mech", displayName = "Hauler Mech", hull = 5f, dps = 10f, moveSpeed = 3.4f,
                lifetime = 90f, leash = 8f, taunts = true, tauntRadius = 5f, tauntCooldown = 15f, tauntDuration = 6f,
                color = new Color(0.95f, 0.7f, 0.3f), scale = 0.9f },
        };

        private static ClassAbilityDef Strike(SpecialistClass c, string id, string name, int lvl, float cd, float dmg,
            float aoe = 0f, FaunaFilter filter = FaunaFilter.Any) => new ClassAbilityDef
        {
            id = id, displayName = name, specialistClass = c, trigger = AbilityTrigger.Strike,
            minLevel = lvl, cooldownSeconds = cd, damageMul = dmg, aoeRadius = aoe, filter = filter
        };

        private static ClassAbilityDef With(ClassAbilityDef d, StatusKind kind, float mag, float dur, float period = 0f)
        {
            d.appliesStatus = true;
            d.status = kind;
            d.statusMagnitude = mag;
            d.statusDuration = dur;
            d.statusPeriod = period;
            return d;
        }

        private static ClassAbilityDef Taunt(ClassAbilityDef d, float seconds)
        {
            d.taunts = true;
            d.tauntDuration = seconds;
            return d;
        }

        private static ClassAbilityDef Summon(SpecialistClass c, string id, string name, int lvl, float cd,
            string companion, int cap) => new ClassAbilityDef
        {
            id = id, displayName = name, specialistClass = c, trigger = AbilityTrigger.Engage,
            minLevel = lvl, cooldownSeconds = cd, summonId = companion, summonCap = cap
        };

        private static ClassAbilityDef Buff(SpecialistClass c, string id, string name, AbilityTrigger trigger, int lvl,
            float cd, StatusKind kind, float mag, float dur, float period = 0f) => With(new ClassAbilityDef
        {
            id = id, displayName = name, specialistClass = c, trigger = trigger, minLevel = lvl, cooldownSeconds = cd
        }, kind, mag, dur, period);

        /// <summary>
        /// Majesty 2 hero moves mapped onto our classes (same pairing as flag appeal). Durations are
        /// shortened: our fights last seconds, Majesty's minutes.
        /// </summary>
        public static ClassAbilityDef[] Defaults() => new[]
        {
            // Warrior -> Defense Mech: imp attack, taunt, maim (slow), resistance buff.
            Strike(SpecialistClass.DefenseMech, "mech_power_strike", "Power Strike", 1, 10f, 3f),
            Taunt(Strike(SpecialistClass.DefenseMech, "mech_challenge", "Challenge", 3, 15f, 1.5f, 5f), 6f),
            Buff(SpecialistClass.DefenseMech, "mech_bulwark", "Bulwark", AbilityTrigger.Hurt, 3, 60f, StatusKind.Armor, 0.25f, 30f),
            With(Strike(SpecialistClass.DefenseMech, "mech_maim", "Maim", 6, 30f, 5f), StatusKind.Slow, 0.5f, 8f),

            // Ranger -> Scout: beastslayer, stingshot (weaken).
            Strike(SpecialistClass.ScoutDrone, "scout_beastslayer", "Beastslayer", 1, 10f, 4f, 0f, FaunaFilter.Stalkers),
            With(Strike(SpecialistClass.ScoutDrone, "scout_stingshot", "Stingshot", 3, 15f, 1.5f), StatusKind.Weaken, 0.25f, 10f),

            // Rogue -> Harvester: poison, stun strike.
            With(Strike(SpecialistClass.HarvesterBot, "harvester_venom", "Venom Strike", 1, 15f, 1f), StatusKind.Poison, 2f, 10f, 1f),
            With(Strike(SpecialistClass.HarvesterBot, "harvester_stun", "Stun Strike", 3, 20f, 1.5f), StatusKind.Stun, 1f, 3f),

            // Cleric -> Medic: holy attack vs undead, heal-over-time on the patient.
            Strike(SpecialistClass.Medic, "medic_purge", "Purge", 1, 5f, 8f, 0f, FaunaFilter.Junk),
            Buff(SpecialistClass.Medic, "medic_nanite_regen", "Nanite Regen", AbilityTrigger.Ally, 3, 20f, StatusKind.Regen, 0.03f, 20f, 2f),

            // Dwarf -> Sentinel: stun strike, berserk (stun immunity + armor).
            With(Strike(SpecialistClass.SentinelMech, "sentinel_stun_slam", "Stun Slam", 1, 20f, 1.5f), StatusKind.Stun, 1f, 3f),
            Buff(SpecialistClass.SentinelMech, "sentinel_berserk", "Berserk", AbilityTrigger.Engage, 3, 60f, StatusKind.StunImmune, 1f, 30f),
            Buff(SpecialistClass.SentinelMech, "sentinel_plating", "Hardened Plating", AbilityTrigger.Hurt, 6, 60f, StatusKind.Armor, 0.5f, 20f),

            // Elf -> Surveyor: roots, magic arrow.
            With(Strike(SpecialistClass.SurveyorBot, "surveyor_snare", "Snare Shot", 1, 30f, 1.5f), StatusKind.Slow, 0.9f, 6f),
            Strike(SpecialistClass.SurveyorBot, "surveyor_arc_bolt", "Arc Bolt", 3, 15f, 5f),

            // Marksman -> Geologist: hoodshot, sunburst.
            Strike(SpecialistClass.GeologistBot, "geologist_core_shot", "Core Shot", 3, 30f, 5f),
            Strike(SpecialistClass.GeologistBot, "geologist_sunburst", "Sunburst", 6, 30f, 2f, 5f),

            // Beastmaster -> Courier: beastslayer, summoned wolf / bear, and the elf speed buff.
            Strike(SpecialistClass.CourierBot, "courier_beastslayer", "Beastslayer", 1, 10f, 4f, 0f, FaunaFilter.Stalkers),
            Summon(SpecialistClass.CourierBot, "courier_escort", "Escort Drone", 1, 30f, "escort_drone", 1),
            Summon(SpecialistClass.CourierBot, "courier_hauler", "Hauler Mech", 6, 45f, "hauler_mech", 1),
            Buff(SpecialistClass.CourierBot, "courier_afterburner", "Afterburner", AbilityTrigger.Flee, 1, 30f, StatusKind.Haste, 0.5f, 10f),

            // Mage -> Terraformer: fireball (burn), freeze.
            With(Strike(SpecialistClass.TerraformerBot, "terra_thermal", "Thermal Lance", 1, 8f, 2f, 3f), StatusKind.Burn, 3f, 5f, 1f),
            With(Strike(SpecialistClass.TerraformerBot, "terra_cryo", "Cryo Lock", 3, 8f, 3f), StatusKind.Stun, 1f, 4f),

            // Engineer: the dwarf's improved attack.
            Strike(SpecialistClass.EngineerBot, "engineer_weld_strike", "Weld Strike", 1, 10f, 3f),
        };

        public static bool Matches(FaunaFilter filter, FaunaKind kind)
        {
            switch (filter)
            {
                case FaunaFilter.Stalkers: return kind == FaunaKind.Stalker;
                case FaunaFilter.Pests: return DustStalkerFilters.IsPest(kind);
                case FaunaFilter.Junk: return kind == FaunaKind.JunkBot;
                default: return true;
            }
        }
    }

    /// <summary>Pure fauna-kind helpers (the runtime agent has the same rule).</summary>
    public static class DustStalkerFilters
    {
        public static bool IsPest(FaunaKind kind) =>
            kind == FaunaKind.Mite || kind == FaunaKind.Leech ||
            kind == FaunaKind.Wisp || kind == FaunaKind.Tick ||
            kind == FaunaKind.Creeper || kind == FaunaKind.Hopper;
    }

    /// <summary>
    /// One robot's ability cooldowns and choice. Pure C#: the agent asks it what to use, applies
    /// the effect, then reports the use.
    /// </summary>
    public sealed class AbilityBook
    {
        private readonly Dictionary<string, float> _ready = new Dictionary<string, float>();
        private float _globalReady;

        /// <summary>
        /// Best ready ability for this trigger: highest damage first, then the longest cooldown
        /// (the bigger move). Strikes must match the target's kind.
        /// </summary>
        public bool TryPick(AbilityTuning t, SpecialistClass cls, int level, AbilityTrigger trigger,
            FaunaKind? target, float now, out ClassAbilityDef pick, Func<ClassAbilityDef, bool> usable = null)
        {
            pick = default;
            if (t == null || !t.enabled || t.abilities == null || now < _globalReady) return false;
            bool found = false;
            for (int i = 0; i < t.abilities.Length; i++)
            {
                var a = t.abilities[i];
                if (a.specialistClass != cls || a.trigger != trigger) continue;
                if (level < a.minLevel) continue;
                if (_ready.TryGetValue(a.id ?? "", out float at) && now < at) continue;
                if (trigger == AbilityTrigger.Strike &&
                    (!target.HasValue || !AbilityTuning.Matches(a.filter, target.Value))) continue;
                if (usable != null && !usable(a)) continue;
                if (found && (a.damageMul < pick.damageMul ||
                              (Mathf.Approximately(a.damageMul, pick.damageMul) && a.cooldownSeconds <= pick.cooldownSeconds)))
                    continue;
                pick = a;
                found = true;
            }
            return found;
        }

        public void NoteUsed(AbilityTuning t, ClassAbilityDef a, float now)
        {
            _ready[a.id ?? ""] = now + Mathf.Max(0f, a.cooldownSeconds);
            _globalReady = now + (t != null ? Mathf.Max(0f, t.globalCooldown) : 0f);
        }

        public float ReadyIn(string id, float now) =>
            _ready.TryGetValue(id ?? "", out float at) ? Mathf.Max(0f, at - now) : 0f;

        public void Reset()
        {
            _ready.Clear();
            _globalReady = 0f;
        }
    }
}
