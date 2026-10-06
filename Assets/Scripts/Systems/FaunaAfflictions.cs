using System;
using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// A status a creature's bite puts on robots (Majesty 2 monster unit actions: zombie poison,
    /// rat plague, earth-elemental stun, fire burn, wolf "kicked"...). Robot health is a 0-1 hull,
    /// so damage-over-time magnitudes are hull per tick.
    /// </summary>
    [Serializable]
    public struct FaunaAffliction
    {
        public string id;
        [Tooltip("Word flashed over the robot, e.g. CORRODED.")]
        public string label;
        public FaunaKind kind;
        [Tooltip("Only alpha (elite) creatures of this kind use it.")]
        public bool eliteOnly;
        public StatusKind status;
        public float magnitude;
        [Min(0f)] public float duration;
        [Min(0f)] public float period;
        [Tooltip("Seconds before this creature can use it again.")]
        [Min(0f)] public float cooldown;
        [Tooltip("Metres around the bitten robot also hit (0 = just the bitten one).")]
        [Min(0f)] public float aoeRadius;
    }

    /// <summary>Inspector-editable creature afflictions. Held by GameLoop.</summary>
    [Serializable]
    public sealed class FaunaAfflictionTuning
    {
        public bool enabled = true;
        public FaunaAffliction[] afflictions = Defaults();

        private static FaunaAffliction A(string id, string label, FaunaKind kind, StatusKind status, float mag,
            float dur, float cooldown, float period = 0f, float aoe = 0f, bool elite = false) => new FaunaAffliction
        {
            id = id, label = label, kind = kind, status = status, magnitude = mag, duration = dur,
            period = period, cooldown = cooldown, aoeRadius = aoe, eliteOnly = elite
        };

        /// <summary>
        /// Majesty monsters mapped onto our fauna. Bites already run 0.03-0.18 hull a second, so
        /// the damage-over-time effects add roughly 10% hull each.
        /// </summary>
        public static FaunaAffliction[] Defaults() => new[]
        {
            A("stalker_rend", "BLEEDING", FaunaKind.Stalker, StatusKind.Poison, 0.012f, 8f, 12f, 1f),          // wolf plague bite
            A("alpha_howl", "SHAKEN", FaunaKind.Stalker, StatusKind.Weaken, 0.25f, 8f, 15f, 0f, 5f, true),   // wolf aoe "kicked"
            A("mite_gnaw", "GNAWED", FaunaKind.Mite, StatusKind.Weaken, 0.2f, 8f, 15f),                      // rat plague
            A("leech_drain", "DRAINED", FaunaKind.Leech, StatusKind.Slow, 0.4f, 6f, 15f),                    // manaburn
            A("wisp_chill", "CHILLED", FaunaKind.Wisp, StatusKind.Slow, 0.5f, 5f, 12f),                       // elemental
            A("tick_toxin", "POISONED", FaunaKind.Tick, StatusKind.Poison, 0.008f, 10f, 15f, 1f),            // spider / goblin poison
            A("creeper_entangle", "ENTANGLED", FaunaKind.Creeper, StatusKind.Slow, 0.9f, 3f, 20f),           // roots
            A("hopper_pounce", "STUNNED", FaunaKind.Hopper, StatusKind.Stun, 1f, 1.5f, 20f),                 // earth elemental stun
            A("junk_corrode", "CORRODED", FaunaKind.JunkBot, StatusKind.Poison, 0.01f, 8f, 15f, 1f),         // zombie poison
        };

        /// <summary>Afflictions this creature can use.</summary>
        public void For(FaunaKind kind, bool elite, List<FaunaAffliction> into)
        {
            into.Clear();
            if (!enabled || afflictions == null) return;
            for (int i = 0; i < afflictions.Length; i++)
            {
                var a = afflictions[i];
                if (a.kind != kind) continue;
                if (a.eliteOnly && !elite) continue;
                into.Add(a);
            }
        }
    }

    /// <summary>One creature's affliction cooldowns. Pure C#.</summary>
    public sealed class AfflictionClock
    {
        private readonly Dictionary<string, float> _ready = new Dictionary<string, float>();

        public bool Ready(in FaunaAffliction a, float now) =>
            !_ready.TryGetValue(a.id ?? "", out float at) || now >= at;

        public void Used(in FaunaAffliction a, float now) =>
            _ready[a.id ?? ""] = now + Mathf.Max(0f, a.cooldown);
    }
}
