using System;
using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>The <see cref="SpecialistData"/> numbers a civ unit may replace. Stored by number: never renumber.</summary>
    public enum CivUnitStat
    {
        None = 0,
        Greed = 1,
        Courage = 2,
        WorkaholicBias = 3,
        ExplorePreference = 4,
        BuildPreference = 5,
        CombatPreference = 6,
        ExtractPreference = 7,
        DefendPreference = 8,
        MoveSpeed = 9,
        WorkRate = 10
    }

    /// <summary>Replace one personality number with <see cref="value"/> (the new value, not a delta).</summary>
    [Serializable]
    public struct CivUnitStatChange
    {
        public CivUnitStat stat;
        public float value;

        public CivUnitStatChange(CivUnitStat stat, float value)
        {
            this.stat = stat;
            this.value = value;
        }
    }

    /// <summary>
    /// Retune one existing class ability, found by <see cref="ClassAbilityDef.id"/>.
    /// A zero field means "leave as it is".
    /// </summary>
    [Serializable]
    public struct CivAbilityChange
    {
        public string abilityId;
        [Tooltip("New unlock level. 0 = unchanged.")]
        public int minLevel;
        [Tooltip("New blast radius in metres. 0 = unchanged.")]
        public float aoeRadius;

        public static CivAbilityChange UnlockAt(string abilityId, int level) =>
            new CivAbilityChange { abilityId = abilityId, minLevel = level };

        public static CivAbilityChange Radius(string abilityId, float metres) =>
            new CivAbilityChange { abilityId = abilityId, aoeRadius = metres };

        /// <summary><paramref name="def"/> with this change applied. A different ability comes back untouched.</summary>
        public ClassAbilityDef ApplyTo(ClassAbilityDef def)
        {
            if (string.IsNullOrEmpty(abilityId) || def.id != abilityId) return def;
            if (minLevel > 0) def.minLevel = minLevel;
            if (aoeRadius > 0f) def.aoeRadius = aoeRadius;
            return def;
        }
    }

    /// <summary>
    /// A civ's own unit for one base role. Saves, workshops and hire prices keep using
    /// <see cref="baseClass"/>; this only renames, restyles and retunes it. A definition with no
    /// changes (see <see cref="Base"/>) is the base class exactly as it is today.
    /// </summary>
    [Serializable]
    public sealed class CivUnitDefinition
    {
        public SpecialistClass baseClass;
        [Tooltip("Empty = keep the base class name.")]
        public string displayName = "";
        [Tooltip("Empty = the base class model.")]
        public string modelKey = "";
        public List<CivUnitStatChange> stats = new List<CivUnitStatChange>();
        public List<CivAbilityChange> abilities = new List<CivAbilityChange>();
        [Tooltip("Names a behaviour the game must implement (phase 3). Empty = none. Nothing reads it yet.")]
        public string behaviourTag = "";
        [Tooltip("Strikes get the civ's NearHomeUnitDamage modifier while within the Commons radius (20 m).")]
        public bool nearHomeBonus;

        public string DisplayNameText => Loc.S(displayName);

        /// <summary>True when this is the base class with nothing changed.</summary>
        public bool IsBase =>
            string.IsNullOrEmpty(displayName) && string.IsNullOrEmpty(modelKey) &&
            string.IsNullOrEmpty(behaviourTag) && !nearHomeBonus &&
            (stats == null || stats.Count == 0) && (abilities == null || abilities.Count == 0);

        /// <summary>The base class as it is today: the fallback for a role a civ does not list.</summary>
        public static CivUnitDefinition Base(SpecialistClass cls) => new CivUnitDefinition { baseClass = cls };

        public CivUnitDefinition Clone() => new CivUnitDefinition
        {
            baseClass = baseClass,
            displayName = displayName,
            modelKey = modelKey,
            stats = stats != null ? new List<CivUnitStatChange>(stats) : new List<CivUnitStatChange>(),
            abilities = abilities != null ? new List<CivAbilityChange>(abilities) : new List<CivAbilityChange>(),
            behaviourTag = behaviourTag,
            nearHomeBonus = nearHomeBonus
        };

        /// <summary>The new value for <paramref name="stat"/>, if this unit changes it. The first entry wins.</summary>
        public bool TryGetStat(CivUnitStat stat, out float value)
        {
            if (stats != null)
            {
                for (int i = 0; i < stats.Count; i++)
                {
                    if (stats[i].stat != stat) continue;
                    value = stats[i].value;
                    return true;
                }
            }
            value = 0f;
            return false;
        }

        /// <summary>This unit's change to the ability <paramref name="abilityId"/>, if any. The first entry wins.</summary>
        public bool TryGetAbilityChange(string abilityId, out CivAbilityChange change)
        {
            if (abilities != null && !string.IsNullOrEmpty(abilityId))
            {
                for (int i = 0; i < abilities.Count; i++)
                {
                    if (abilities[i].abilityId != abilityId) continue;
                    change = abilities[i];
                    return true;
                }
            }
            change = default;
            return false;
        }

        /// <summary>
        /// Write this unit's name and numbers onto <paramref name="data"/>. Call it after
        /// <see cref="SpecialistPersonality.Apply"/>, which resets every class to the shared table.
        /// A unit with no changes leaves <paramref name="data"/> exactly as it was. Data of a
        /// different class is ignored. Nothing in the game calls this yet (roster wiring is a later PR).
        /// </summary>
        public void ApplyTo(SpecialistData data)
        {
            if (data == null || data.specialistClass != baseClass) return;
            if (!string.IsNullOrEmpty(displayName)) data.displayName = displayName;
            if (stats == null) return;
            // Last to first, so the first entry for a stat is the one that sticks (as in TryGetStat).
            for (int i = stats.Count - 1; i >= 0; i--)
            {
                float v = stats[i].value;
                switch (stats[i].stat)
                {
                    case CivUnitStat.Greed: data.baseGreed = v; break;
                    case CivUnitStat.Courage: data.courage = v; break;
                    case CivUnitStat.WorkaholicBias: data.workaholicBias = v; break;
                    case CivUnitStat.ExplorePreference: data.explorePreference = v; break;
                    case CivUnitStat.BuildPreference: data.buildPreference = v; break;
                    case CivUnitStat.CombatPreference: data.combatPreference = v; break;
                    case CivUnitStat.ExtractPreference: data.extractPreference = v; break;
                    case CivUnitStat.DefendPreference: data.defendPreference = v; break;
                    case CivUnitStat.MoveSpeed: data.moveSpeed = v; break;
                    case CivUnitStat.WorkRate: data.workRate = v; break;
                }
            }
        }
    }
}
