using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Stance a fauna mob holds between Laya answers. Every stance is a subset of the scripted
    /// role, so the model can make mobs smarter or warier but never stronger.
    /// </summary>
    public enum MobTactic
    {
        /// <summary>Scripted behaviour: ambush collectors, then raid the role's target.</summary>
        Role,
        /// <summary>Skip structure raids; only jump travelling tax collectors.</summary>
        Ambush,
        /// <summary>No raids or ambushes; roam, aggro and bite only.</summary>
        Prowl
    }

    /// <summary>Builds the Laya question for a mob's stance and maps the answer back.</summary>
    public static class LayaMobPolicy
    {
        public const string Instructions =
            "You are this lunar creature. Choose how it behaves for the next few seconds.";

        public struct MobState
        {
            public string Kind;
            public string RaidTarget;
            public float Health01;
            public float DistanceToCampus;
            public bool Frenzy;
            public bool CollectorNearby;
            public bool RaidTargetNearby;
            public bool RecentlyHit;
        }

        public static string BuildState(in MobState s) =>
            $"Hostile lunar fauna: a {s.Kind}. Health {Mathf.RoundToInt(Mathf.Clamp01(s.Health01) * 100f)}%. " +
            $"{Mathf.RoundToInt(s.DistanceToCampus)}m from the colony campus. " +
            (s.Frenzy ? "In a feeding frenzy. " : "") +
            (s.RecentlyHit ? "Just took hits from a colony defender. " : "") +
            (s.CollectorNearby ? "A tax collector robot is walking the road nearby carrying gold. " : "") +
            (s.RaidTargetNearby ? $"A {s.RaidTarget} is within reach to raid." : $"No {s.RaidTarget} in reach.");

        /// <summary>Only legal stances are offered: Role is always first (the scripted fallback).</summary>
        public static List<LayaOption> BuildOptions(in MobState s, List<MobTactic> tactics)
        {
            tactics.Clear();
            var list = new List<LayaOption>(3);
            tactics.Add(MobTactic.Role);
            list.Add(new LayaOption
            {
                Label = "raid",
                Description = s.RaidTargetNearby
                    ? $"Raid the {s.RaidTarget}, and jump any collector on the way."
                    : "Push toward the colony to find something to raid."
            });
            if (s.CollectorNearby)
            {
                tactics.Add(MobTactic.Ambush);
                list.Add(new LayaOption { Label = "ambush", Description = "Ambush the travelling tax collector and steal its gold." });
            }
            tactics.Add(MobTactic.Prowl);
            list.Add(new LayaOption { Label = "prowl", Description = "Hang back and prowl; avoid the colony's defenders." });
            return list;
        }

        /// <summary>Chosen tactic, or <see cref="MobTactic.Role"/> when Laya is unsure or off-list.</summary>
        public static MobTactic Resolve(
            IReadOnlyList<LayaOption> offered, IReadOnlyList<MobTactic> tactics, LayaChoice answer, float minProbability)
        {
            int i = LayaHeroPolicy.Resolve(offered, answer, minProbability);
            return i >= 0 && i < tactics.Count ? tactics[i] : MobTactic.Role;
        }
    }
}
