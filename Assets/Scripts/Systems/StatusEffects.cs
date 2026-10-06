using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Timed conditions (Majesty 2 perks.xml), on robots and fauna alike.
    /// Magnitudes are per kind: health per tick for DoT / regen (fauna HP, or 0-1 hull for robots),
    /// a 0-1 fraction for slow / weaken / armor, a speed bonus fraction for haste.
    /// </summary>
    public enum StatusKind
    {
        /// <summary>Damage every period (state_poison / rogue_poison).</summary>
        Poison,
        /// <summary>Damage every period (state_burn).</summary>
        Burn,
        /// <summary>Healing every period (state_heal_regeneration).</summary>
        Regen,
        /// <summary>Movement x (1 - magnitude); 1 = rooted (state_slow / state_roots).</summary>
        Slow,
        /// <summary>Cannot move or attack (state_stun / state_frozen).</summary>
        Stun,
        /// <summary>Outgoing damage x (1 - magnitude) (state_kicked / state_wither).</summary>
        Weaken,
        /// <summary>Incoming damage x (1 - magnitude) (state_resistant / state_magic_shield).</summary>
        Armor,
        /// <summary>Movement x (1 + magnitude) (state_elf_buff / elixir_speed).</summary>
        Haste,
        /// <summary>Stun cannot land (state_imunity2stun / dwarf berserk).</summary>
        StunImmune
    }

    public struct StatusEffect
    {
        public StatusKind Kind;
        public float Magnitude;
        public float Remaining;
        public float Period;
        public float TickTimer;
    }

    /// <summary>
    /// Active statuses on one unit. Re-applying a kind refreshes it to the stronger magnitude and
    /// the longer duration instead of stacking (Majesty perks of one name do not stack).
    /// </summary>
    public sealed class StatusEffects
    {
        private readonly List<StatusEffect> _active = new List<StatusEffect>(4);

        public int Count => _active.Count;

        public bool Has(StatusKind kind)
        {
            for (int i = 0; i < _active.Count; i++)
                if (_active[i].Kind == kind) return true;
            return false;
        }

        public float Magnitude(StatusKind kind)
        {
            float m = 0f;
            for (int i = 0; i < _active.Count; i++)
                if (_active[i].Kind == kind) m = Mathf.Max(m, _active[i].Magnitude);
            return m;
        }

        /// <summary>Apply or refresh. Returns false if the unit is immune (stun vs StunImmune).</summary>
        public bool Apply(StatusKind kind, float magnitude, float duration, float period = 0f)
        {
            if (duration <= 0f) return false;
            if (kind == StatusKind.Stun && Has(StatusKind.StunImmune)) return false;
            if (kind == StatusKind.StunImmune) Remove(StatusKind.Stun);

            for (int i = 0; i < _active.Count; i++)
            {
                if (_active[i].Kind != kind) continue;
                var e = _active[i];
                e.Magnitude = Mathf.Max(e.Magnitude, magnitude);
                e.Remaining = Mathf.Max(e.Remaining, duration);
                if (period > 0f) e.Period = period;
                _active[i] = e;
                return true;
            }

            _active.Add(new StatusEffect
            {
                Kind = kind, Magnitude = magnitude, Remaining = duration,
                Period = Mathf.Max(0f, period), TickTimer = Mathf.Max(0f, period)
            });
            return true;
        }

        public void Remove(StatusKind kind)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
                if (_active[i].Kind == kind) _active.RemoveAt(i);
        }

        public void Clear() => _active.Clear();

        /// <summary>
        /// Advance timers. Returns the health change this step: damage-over-time negative,
        /// regen positive, delivered in whole-period ticks (a 2 s regen pays every 2 s).
        /// </summary>
        public float Tick(float dt)
        {
            if (!(dt > 0f) || _active.Count == 0) return 0f;
            float delta = 0f;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var e = _active[i];
                float live = Mathf.Min(dt, e.Remaining);
                e.Remaining -= dt;

                if (e.Period > 0f && (e.Kind == StatusKind.Poison || e.Kind == StatusKind.Burn || e.Kind == StatusKind.Regen))
                {
                    e.TickTimer -= live;
                    while (e.TickTimer <= 0f)
                    {
                        e.TickTimer += e.Period;
                        delta += e.Kind == StatusKind.Regen ? e.Magnitude : -e.Magnitude;
                    }
                }

                if (e.Remaining <= 0f) _active.RemoveAt(i);
                else _active[i] = e;
            }
            return delta;
        }

        public bool Stunned => Has(StatusKind.Stun);

        /// <summary>Movement multiplier: 0 when stunned, slow and haste combined otherwise.</summary>
        public float SpeedMul =>
            Stunned ? 0f : Mathf.Max(0f, 1f - Magnitude(StatusKind.Slow)) * (1f + Magnitude(StatusKind.Haste));

        /// <summary>Outgoing damage multiplier (0 when stunned).</summary>
        public float OutgoingMul => Stunned ? 0f : Mathf.Clamp01(1f - Magnitude(StatusKind.Weaken));

        /// <summary>Incoming damage multiplier.</summary>
        public float IncomingMul => Mathf.Clamp01(1f - Magnitude(StatusKind.Armor));
    }
}
