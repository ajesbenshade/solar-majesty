using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Turns a specialist's utility options into a Laya <c>choice</c> question and maps the answer
    /// back. Laya only ever picks among options <see cref="SpecialistBrain.CollectOptions"/> already
    /// allowed (the same "model proposes, rules guard" split as the laya-mlx Snake demo), so the
    /// greed gate, panic flee and the no-direct-control rule all still hold.
    /// </summary>
    public static class LayaHeroPolicy
    {
        public const string Instructions =
            "You are this colony specialist. Pick what they do next, true to their personality.";

        public static string BuildState(in SpecialistContext ctx)
        {
            var d = ctx.Data;
            var sb = new StringBuilder(320);
            sb.Append("Autonomous ").Append(d != null ? d.displayName : "specialist").Append(" on a lunar colony. ");
            if (d != null)
            {
                sb.Append("Personality: ")
                  .Append(Word(d.baseGreed, "modest", "fair-minded", "greedy")).Append(", ")
                  .Append(Word(d.courage, "cowardly", "steady", "fearless")).Append(", ")
                  .Append(Word(d.workaholicBias, "lazy", "diligent", "workaholic")).Append(". ")
                  .Append("Likes combat ").Append(Pct(d.combatPreference))
                  .Append(", building ").Append(Pct(d.buildPreference))
                  .Append(", exploring ").Append(Pct(d.explorePreference)).Append(". ");
            }
            sb.Append("Health ").Append(Pct(ctx.HealthNormalized))
              .Append(", fatigue ").Append(Pct(ctx.Fatigue))
              .Append(", hunger for pay ").Append(Pct(ctx.GreedHunger)).Append(". ");
            sb.Append("Currently: ").Append(ctx.CurrentAction).Append('.');
            return sb.ToString();
        }

        /// <summary>Labels + descriptions, index-aligned with <paramref name="decisions"/>.</summary>
        public static List<LayaOption> BuildOptions(in SpecialistContext ctx, IReadOnlyList<BrainDecision> decisions)
        {
            var list = new List<LayaOption>(decisions.Count);
            int bounty = 0;
            for (int i = 0; i < decisions.Count; i++)
            {
                var o = decisions[i];
                string label;
                string desc;
                float dist = Vector3.Distance(ctx.Position, o.TargetPosition);
                switch (o.Action)
                {
                    case SpecialistAction.PursueFlag:
                        bounty++;
                        label = "bounty_" + bounty;
                        var f = o.TargetFlag;
                        dist = f != null ? Vector3.Distance(ctx.Position, f.WorldPosition) : 0f;
                        desc = f?.Data != null
                            ? $"Take the {f.Data.flagType} bounty paying {Mathf.RoundToInt(f.CurrentBounty)} credits, " +
                              $"{Mathf.RoundToInt(dist)}m away, risk {Pct(f.Risk)}, {f.ClaimCount} others on it."
                            : "Take a posted bounty.";
                        break;
                    case SpecialistAction.Hunt:
                        label = "hunt";
                        desc = $"Hunt the fauna {Mathf.RoundToInt(ctx.HuntDistance)}m away for glory; no bounty.";
                        break;
                    case SpecialistAction.Repair:
                        label = "repair";
                        desc = $"Patch a damaged module {Mathf.RoundToInt(ctx.RepairDistance)}m away for a small fee.";
                        break;
                    case SpecialistAction.Rest:
                        label = "rest";
                        desc = "Go back to the inn to rest and heal.";
                        break;
                    case SpecialistAction.Wander:
                        label = "wander";
                        desc = o.Reason switch
                        {
                            "triage" => "Tend to a wounded colleague.",
                            "levy_home" or "levy_collect" => "Walk the tax levy route.",
                            "workshop_duty" => "Work a shift at the guild workshop.",
                            "patrolling" => "Patrol the colony perimeter.",
                            _ => "Wander the frontier, no job."
                        };
                        break;
                    default:
                        label = o.Action.ToString().ToLowerInvariant();
                        desc = "Stand by.";
                        break;
                }
                list.Add(new LayaOption { Label = label, Description = desc });
            }
            return list;
        }

        /// <summary>
        /// Index of the option Laya chose, or -1 to keep the utility pick (option 0). Rejects
        /// labels that were not offered and answers whose top probability is below the floor.
        /// </summary>
        public static int Resolve(IReadOnlyList<LayaOption> offered, LayaChoice answer, float minProbability)
        {
            if (answer == null) return -1;
            int best = -1;
            float bestP = -1f;
            for (int i = 0; i < offered.Count; i++)
            {
                if (!answer.Probabilities.TryGetValue(offered[i].Label, out float p)) continue;
                if (p > bestP)
                {
                    bestP = p;
                    best = i;
                }
            }
            return bestP >= minProbability ? best : -1;
        }

        /// <summary>
        /// Finds <paramref name="chosen"/> among this tick's options, so a late answer is only applied
        /// if that option is still legal. Returns -1 when it no longer is.
        /// </summary>
        public static int Match(IReadOnlyList<BrainDecision> current, in BrainDecision chosen)
        {
            for (int i = 0; i < current.Count; i++)
            {
                var o = current[i];
                if (o.Action != chosen.Action) continue;
                if (o.Action == SpecialistAction.PursueFlag && !ReferenceEquals(o.TargetFlag, chosen.TargetFlag))
                    continue;
                return i;
            }
            return -1;
        }

        static string Pct(float v) => Mathf.RoundToInt(Mathf.Clamp01(v) * 100f) + "%";

        static string Word(float v, string lo, string mid, string hi) =>
            v < 0.35f ? lo : v > 0.65f ? hi : mid;
    }
}
