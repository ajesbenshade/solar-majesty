using System;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// The Overseer's satellite constellation: the sci-fi stand-in for Majesty 2's ruler spells.
    /// Values in comments are the Majesty spell each power is modelled on.
    /// </summary>
    public enum OrbitalPowerId
    {
        KineticLance = 0,    // lightning
        OrbitalBarrage = 1,  // thunderstorm
        EmpSnare = 2,        // roots / petrify
        MedDrop = 3,         // heal
        AegisField = 4,      // divine shield
        ReviveBeacon = 5,    // resurrect
        RepairSwarm = 6,     // building heal
        SurveySweep = 7,     // recon
        TillAudit = 8        // extortion
    }

    /// <summary>What a power needs under the cursor.</summary>
    public enum OrbitalTarget
    {
        /// <summary>Nearest hostile within the radius (or every hostile in it, for area powers).</summary>
        Fauna,
        /// <summary>Nearest standing robot within the radius.</summary>
        Robot,
        /// <summary>Downed robots within the radius.</summary>
        DownedRobot,
        /// <summary>Nearest damaged building within the radius.</summary>
        Structure,
        /// <summary>Any point on the map.</summary>
        Ground,
        /// <summary>The whole colony; cast straight from the list, no targeting.</summary>
        Colony
    }

    [Serializable]
    public struct OrbitalPowerDef
    {
        public OrbitalPowerId id;
        public string displayName;
        public string description;
        public OrbitalTarget target;
        [Tooltip("1 = Orbital Uplink research, 2 = Orbital Constellation research.")]
        [Range(1, 2)] public int tier;
        [Tooltip("CRED at the uplink; multiplied by the range band at the target.")]
        [Min(0)] public int cost;
        [Min(0f)] public float cooldownSeconds;
        [Tooltip("Metres: pick radius for single-target powers, blast radius for area powers.")]
        [Min(0f)] public float radius;
        [Tooltip("Damage (fauna HP), heal / repair (0-1 health), or till share (0-1), by power.")]
        public float magnitude;
        [Min(0f)] public float durationSeconds;
        [Tooltip("Hits everything in the radius instead of the nearest target.")]
        public bool area;
    }

    /// <summary>Majesty 2 spell range ranges: cost multiplier by distance from the nearest casting building.</summary>
    [Serializable]
    public struct UplinkRangeBand
    {
        [Tooltip("Upper edge of the band in Majesty map units.")]
        public float maxUnits;
        public float costMul;
    }

    /// <summary>Inspector-editable orbital support settings. Held by GameLoop.</summary>
    [Serializable]
    public sealed class OrbitalTuning
    {
        public bool enabled = true;
        [Tooltip("Off = every power is available from the start (playtesting).")]
        public bool requireResearch = true;
        [Tooltip("Our metres -> Majesty map units for the range bands (same scale as caravans).")]
        [Min(0.01f)] public float rangeScale = 2.5f;
        [Tooltip("Buildings that act as uplinks; cost rises with distance from the nearest one.")]
        public BuildingCategory[] uplinkCategories =
        {
            BuildingCategory.Commons, BuildingCategory.Laboratory, BuildingCategory.Defense
        };
        public UplinkRangeBand[] rangeBands = DefaultBands();
        public OrbitalPowerDef[] powers = DefaultPowers();

        /// <summary>Majesty 2 spells.xml ranges: 0-25 x1, -50 x3, -100 x5, -150 x7, -200 x9, -250 x10, beyond x15.</summary>
        public static UplinkRangeBand[] DefaultBands() => new[]
        {
            new UplinkRangeBand { maxUnits = 25f, costMul = 1f },
            new UplinkRangeBand { maxUnits = 50f, costMul = 3f },
            new UplinkRangeBand { maxUnits = 100f, costMul = 5f },
            new UplinkRangeBand { maxUnits = 150f, costMul = 7f },
            new UplinkRangeBand { maxUnits = 200f, costMul = 9f },
            new UplinkRangeBand { maxUnits = 250f, costMul = 10f },
            new UplinkRangeBand { maxUnits = 1e6f, costMul = 15f },
        };

        /// <summary>
        /// Majesty prices kept (CRED is Majesty gold). Damage is sized to our fauna: a stalker has
        /// 28 HP, a mite 16, an alpha raider about 112.
        /// </summary>
        public static OrbitalPowerDef[] DefaultPowers() => new[]
        {
            new OrbitalPowerDef { id = OrbitalPowerId.KineticLance, displayName = "Kinetic Lance",
                description = "Tungsten rod from orbit. Kills a stalker outright.",
                target = OrbitalTarget.Fauna, tier = 1, cost = 250, cooldownSeconds = 3f, radius = 6f, magnitude = 40f },
            new OrbitalPowerDef { id = OrbitalPowerId.OrbitalBarrage, displayName = "Orbital Barrage",
                description = "Saturation strike. Hits every hostile in 7 m.",
                target = OrbitalTarget.Fauna, tier = 2, cost = 1000, cooldownSeconds = 20f, radius = 7f, magnitude = 60f, area = true },
            new OrbitalPowerDef { id = OrbitalPowerId.EmpSnare, displayName = "EMP Snare",
                description = "Locks every hostile in 6 m in place for 6 s.",
                target = OrbitalTarget.Fauna, tier = 2, cost = 500, cooldownSeconds = 15f, radius = 6f, durationSeconds = 6f, area = true },
            new OrbitalPowerDef { id = OrbitalPowerId.MedDrop, displayName = "Med-Drop",
                description = "Repair pod for one robot: +60% hull.",
                target = OrbitalTarget.Robot, tier = 1, cost = 250, cooldownSeconds = 3f, radius = 6f, magnitude = 0.6f },
            new OrbitalPowerDef { id = OrbitalPowerId.AegisField, displayName = "Aegis Field",
                description = "Shield projector. One robot takes no damage for 15 s.",
                target = OrbitalTarget.Robot, tier = 2, cost = 500, cooldownSeconds = 10f, radius = 6f, durationSeconds = 15f },
            new OrbitalPowerDef { id = OrbitalPowerId.ReviveBeacon, displayName = "Revive Beacon",
                description = "Reboots downed robots in 6 m on the spot. Wrecks still need the Fobot Yard.",
                target = OrbitalTarget.DownedRobot, tier = 2, cost = 750, cooldownSeconds = 30f, radius = 6f, area = true },
            new OrbitalPowerDef { id = OrbitalPowerId.RepairSwarm, displayName = "Repair Swarm",
                description = "Nanite drop. Restores 50% of a damaged building.",
                target = OrbitalTarget.Structure, tier = 2, cost = 750, cooldownSeconds = 10f, radius = 8f, magnitude = 0.5f },
            new OrbitalPowerDef { id = OrbitalPowerId.SurveySweep, displayName = "Survey Sweep",
                description = "Satellite pass. Charts every den within 30 m.",
                target = OrbitalTarget.Ground, tier = 1, cost = 250, cooldownSeconds = 20f, radius = 30f },
            new OrbitalPowerDef { id = OrbitalPowerId.TillAudit, displayName = "Till Audit",
                description = "Remote audit: 30% of every till straight to the treasury. Free.",
                target = OrbitalTarget.Colony, tier = 1, cost = 0, cooldownSeconds = 180f, magnitude = 0.3f },
        };

        public bool TryGet(OrbitalPowerId id, out OrbitalPowerDef def)
        {
            if (powers != null)
                for (int i = 0; i < powers.Length; i++)
                    if (powers[i].id == id) { def = powers[i]; return true; }
            def = default;
            return false;
        }

        /// <summary>Cost multiplier for a target this many metres from the nearest uplink.</summary>
        public float RangeMultiplier(float uplinkMeters)
        {
            if (rangeBands == null || rangeBands.Length == 0) return 1f;
            if (float.IsInfinity(uplinkMeters)) return rangeBands[rangeBands.Length - 1].costMul;
            float units = Mathf.Max(0f, uplinkMeters) * Mathf.Max(0.01f, rangeScale);
            for (int i = 0; i < rangeBands.Length; i++)
                if (units <= rangeBands[i].maxUnits) return Mathf.Max(0f, rangeBands[i].costMul);
            return Mathf.Max(0f, rangeBands[rangeBands.Length - 1].costMul);
        }

        public static TechId TechFor(int tier) => tier >= 2 ? TechId.OrbitalConstellation : TechId.OrbitalUplink;
    }

    public enum OrbitalCastCheck
    {
        Ok,
        Disabled,
        Unknown,
        Locked,
        Cooling,
        TooPoor
    }

    /// <summary>
    /// Cooldowns, prices and gates for orbital powers. Pure C#: GameLoop finds targets, spends
    /// the treasury and applies effects.
    /// </summary>
    public sealed class OrbitalDirector
    {
        private OrbitalTuning _tuning;
        private float[] _cooldown = new float[Enum.GetValues(typeof(OrbitalPowerId)).Length];

        public OrbitalDirector(OrbitalTuning tuning) => SetTuning(tuning);

        public OrbitalTuning Tuning => _tuning;

        public void SetTuning(OrbitalTuning tuning) => _tuning = tuning ?? new OrbitalTuning();

        public void Tick(float dt)
        {
            if (!(dt > 0f)) return;
            for (int i = 0; i < _cooldown.Length; i++)
                if (_cooldown[i] > 0f) _cooldown[i] = Mathf.Max(0f, _cooldown[i] - dt);
        }

        public float ReadyIn(OrbitalPowerId id)
        {
            int i = (int)id;
            return i >= 0 && i < _cooldown.Length ? _cooldown[i] : 0f;
        }

        public bool IsUnlocked(OrbitalPowerId id, Func<TechId, bool> isUnlocked)
        {
            if (!_tuning.TryGet(id, out var def)) return false;
            if (!_tuning.requireResearch) return true;
            return isUnlocked != null && isUnlocked(OrbitalTuning.TechFor(def.tier));
        }

        /// <summary>CRED to cast at a target this many metres from the nearest uplink.</summary>
        public int CostAt(OrbitalPowerId id, float uplinkMeters)
        {
            if (!_tuning.TryGet(id, out var def)) return 0;
            if (def.cost <= 0) return 0;
            return Mathf.CeilToInt(def.cost * _tuning.RangeMultiplier(uplinkMeters));
        }

        public OrbitalCastCheck Check(OrbitalPowerId id, Func<TechId, bool> isUnlocked, int treasury, float uplinkMeters)
        {
            if (!_tuning.enabled) return OrbitalCastCheck.Disabled;
            if (!_tuning.TryGet(id, out _)) return OrbitalCastCheck.Unknown;
            if (!IsUnlocked(id, isUnlocked)) return OrbitalCastCheck.Locked;
            if (ReadyIn(id) > 0f) return OrbitalCastCheck.Cooling;
            if (CostAt(id, uplinkMeters) > treasury) return OrbitalCastCheck.TooPoor;
            return OrbitalCastCheck.Ok;
        }

        public void StartCooldown(OrbitalPowerId id)
        {
            if (!_tuning.TryGet(id, out var def)) return;
            int i = (int)id;
            if (i >= 0 && i < _cooldown.Length) _cooldown[i] = Mathf.Max(0f, def.cooldownSeconds);
        }

        /// <summary>Remaining cooldowns by power id, for saving.</summary>
        public float[] CaptureCooldowns() => (float[])_cooldown.Clone();

        public void RestoreCooldowns(float[] remaining)
        {
            Array.Clear(_cooldown, 0, _cooldown.Length);
            if (remaining == null) return;
            for (int i = 0; i < remaining.Length && i < _cooldown.Length; i++)
                _cooldown[i] = Mathf.Max(0f, remaining[i]);
        }
    }
}
