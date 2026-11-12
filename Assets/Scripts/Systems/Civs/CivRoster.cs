using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Puts a civ's roster into play. The game builds each class's data once from the shared
    /// personality table (<see cref="SpecialistPersonality.Apply"/>), and abilities from the one
    /// shared <see cref="AbilityTuning"/>; these helpers give back the owner's version of either.
    /// A class the civ does not list, and the neutral civ, get the very same object back, so
    /// today's game is untouched. Nothing here names a civ: everything goes through
    /// <see cref="CivModifiers.UnitFor"/>.
    ///
    /// Saves, workshops and hire prices keep using the base class number; only the data the
    /// spawned bot carries (name and personality numbers) and the ability table it reads change.
    /// </summary>
    public static class CivRoster
    {
        /// <summary>
        /// <paramref name="baseData"/> as <paramref name="civ"/> fields it. When the civ has its own
        /// unit for the class this is a new copy (the caller owns it and destroys it when done)
        /// with the unit's name and numbers written on; otherwise it is <paramref name="baseData"/>
        /// itself. The shared data, which may be a project asset, is never written to.
        /// </summary>
        public static SpecialistData UnitData(SpecialistData baseData, CivModifiers civ)
        {
            if (baseData == null || civ == null || !civ.HasUnitFor(baseData.specialistClass)) return baseData;
            var copy = Object.Instantiate(baseData);
            copy.name = baseData.name;
            copy.hideFlags = HideFlags.DontSave;
            civ.UnitFor(baseData.specialistClass).ApplyTo(copy);
            return copy;
        }

        /// <summary>
        /// The ability table <paramref name="civ"/>'s bots read. Each ability is passed through the
        /// civ's unit for its class (<see cref="CivUnitDefinition.TryGetAbilityChange"/>). When no
        /// ability changes, <paramref name="baseTuning"/> itself comes back; otherwise a copy, so
        /// the shared table is never written to.
        /// </summary>
        public static AbilityTuning Abilities(AbilityTuning baseTuning, CivModifiers civ)
        {
            if (baseTuning == null || civ == null || baseTuning.abilities == null) return baseTuning;
            ClassAbilityDef[] changed = null;
            for (int i = 0; i < baseTuning.abilities.Length; i++)
            {
                var def = baseTuning.abilities[i];
                if (!civ.HasUnitFor(def.specialistClass)) continue;
                if (!civ.UnitFor(def.specialistClass).TryGetAbilityChange(def.id, out var change)) continue;
                changed ??= (ClassAbilityDef[])baseTuning.abilities.Clone();
                changed[i] = change.ApplyTo(def);
            }
            if (changed == null) return baseTuning;
            return new AbilityTuning
            {
                enabled = baseTuning.enabled,
                globalCooldown = baseTuning.globalCooldown,
                hurtLine = baseTuning.hurtLine,
                threatLine = baseTuning.threatLine,
                abilities = changed,
                companions = baseTuning.companions
            };
        }

        /// <summary>The ability <paramref name="id"/> in <paramref name="tuning"/>, if it has one.</summary>
        public static bool TryFind(AbilityTuning tuning, string id, out ClassAbilityDef def)
        {
            if (tuning != null && tuning.abilities != null && !string.IsNullOrEmpty(id))
            {
                for (int i = 0; i < tuning.abilities.Length; i++)
                {
                    if (tuning.abilities[i].id != id) continue;
                    def = tuning.abilities[i];
                    return true;
                }
            }
            def = default;
            return false;
        }
    }
}
