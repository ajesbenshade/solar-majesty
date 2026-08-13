using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Majesty-style utility AI: bounties, fear, opportunistic hunting, and kingdom vocation.
    /// The player never forces a job — only posts flags and hopes personality + greed accepts it.
    /// </summary>
    public sealed class SpecialistBrain
    {
        public float ConsiderRange = 80f;
        public float CurrentFlagHysteresis = 0.15f;
        public float RestThreshold = 0.72f;

        public BrainDecision Evaluate(
            in SpecialistContext ctx,
            IReadOnlyList<FlagHandle> openFlags,
            float bodyDanger = 0.3f)
        {
            if (ctx.Data == null)
                return BrainDecision.Idle(0f, "missing_data");

            var data = ctx.Data;
            Vector3 inn = ctx.SafetyPosition.sqrMagnitude > 0.01f
                ? ctx.SafetyPosition
                : ctx.Position;

            float injury = 1f - ctx.HealthNormalized;
            float restScore = CalculateRestScore(ctx);

            // 1. Panic — Majesty heroes drop quests and run to the inn when badly hurt.
            bool panicked = injury > 0.55f ||
                            (injury > 0.32f && bodyDanger > 0.4f && data.courage < 0.55f);
            if (panicked && ctx.HealthNormalized < 0.62f)
                return BrainDecision.Flee(inn, 0.95f, "flee_to_inn");

            // 2. Exhaustion / injury — rest at the inn, not in the field.
            if (restScore > 0.78f)
                return BrainDecision.Rest(restScore, "exhausted_or_hurt", inn);

            // 3. Player bounties (greed gate). Broke heroes take cheaper flags.
            FlagHandle bestFlag = null;
            float bestFlagScore = -1f;
            string bestFlagReason = "none";
            float consider = 40f + data.explorePreference * 35f;
            if (ConsiderRange > 0f)
                consider = Mathf.Max(consider, ConsiderRange * 0.7f);

            if (openFlags != null)
            {
                for (int i = 0; i < openFlags.Count; i++)
                {
                    var flag = openFlags[i];
                    if (flag == null || flag.Data == null) continue;

                    float dist = Vector3.Distance(ctx.Position, flag.WorldPosition);
                    if (consider > 0f && dist > consider) continue;

                    float score = ScoreFlag(ctx, flag, dist, bodyDanger);
                    if (ctx.CurrentFlag != null &&
                        ReferenceEquals(ctx.CurrentFlag.RuntimeId, flag.RuntimeId))
                    {
                        score += CurrentFlagHysteresis;
                    }

                    if (score > bestFlagScore)
                    {
                        bestFlagScore = score;
                        bestFlag = flag;
                        bestFlagReason = $"flag_{flag.Data.flagType}";
                    }
                }
            }

            float acceptance = 0.38f + data.baseGreed * 0.25f - ctx.GreedHunger * 0.22f;
            acceptance = Mathf.Clamp(acceptance, 0.22f, 0.72f);
            bool takeFlag = bestFlag != null && bestFlagScore >= acceptance;

            // 4. Opportunistic hunt — warriors engage nearby fauna without a posted bounty.
            float huntScore = -1f;
            if (ctx.HasHunt && data.specialistClass != SpecialistClass.Medic &&
                data.combatPreference >= 0.2f && ctx.HealthNormalized > 0.38f)
            {
                huntScore = ScoreHunt(ctx, bodyDanger);
                if (ctx.CurrentAction == SpecialistAction.Hunt)
                    huntScore += 0.12f;
            }

            if (huntScore >= acceptance && huntScore > bestFlagScore)
                return BrainDecision.Hunt(ctx.HuntPosition, huntScore, "hunt_fauna");

            if (takeFlag)
                return BrainDecision.Pursue(bestFlag, bestFlagScore, bestFlagReason);

            if (data.specialistClass == SpecialistClass.Medic && ctx.HasPatient &&
                ctx.HealthNormalized > 0.38f)
            {
                return BrainDecision.Wander(ctx.PatientPosition, 0.48f, "triage");
            }

            // 5. Mild rest if worn, else kingdom vocation (never stand still).
            if (restScore > 0.45f)
                return BrainDecision.Rest(restScore, "mild_fatigue", inn);

            string vocation = data.specialistClass switch
            {
                SpecialistClass.DefenseMech => ctx.HasWorkshop ? "workshop_duty" : "patrolling",
                SpecialistClass.EngineerBot => ctx.HasWorkshop ? "workshop_duty" : "town_tinker",
                SpecialistClass.Medic => "inn_triage",
                _ => ctx.HasWorkshop ? "workshop_duty" : "wandering_frontier"
            };
            Vector3 dest = data.specialistClass == SpecialistClass.Medic
                ? inn
                : (ctx.VocationPosition.sqrMagnitude > 0.01f ? ctx.VocationPosition : inn);
            return BrainDecision.Wander(dest, ctx.HasWorkshop ? 0.34f : 0.28f, vocation);
        }

        float CalculateRestScore(in SpecialistContext ctx)
        {
            float fatigue = ctx.Fatigue;
            float injury = 1f - ctx.HealthNormalized;
            float score = fatigue * 0.7f + injury * 0.55f;
            score *= (1.1f - ctx.Data.workaholicBias);
            return Mathf.Clamp01(score);
        }

        float ScoreHunt(in SpecialistContext ctx, float bodyDanger)
        {
            var data = ctx.Data;
            float distPenalty = Mathf.Clamp01(ctx.HuntDistance / 28f) * 0.55f;
            float courageBoost = data.courage * 0.45f;
            float pref = data.combatPreference * 0.95f;
            float fear = (1f - ctx.HealthNormalized) * (1.1f - data.courage) * 0.5f;
            float danger = bodyDanger * (1.05f - data.courage) * 0.35f;
            return Mathf.Clamp01(pref + courageBoost - distPenalty - fear - danger);
        }

        float ScoreFlag(in SpecialistContext ctx, FlagHandle flag, float distance, float bodyDanger)
        {
            var data = ctx.Data;
            var fdata = flag.Data;

            float bountyFactor = Mathf.Clamp01(flag.CurrentBounty / 100f);
            float greedScore = bountyFactor * (0.55f + data.baseGreed * 0.7f);
            greedScore += ctx.GreedHunger * 0.18f * bountyFactor;

            float preferenceScore = data.GetPreference(fdata.flagType) * 0.9f;
            if (fdata.stronglyAttracts != null)
            {
                for (int i = 0; i < fdata.stronglyAttracts.Length; i++)
                {
                    if (fdata.stronglyAttracts[i] == data.specialistClass)
                    {
                        preferenceScore += 0.22f;
                        break;
                    }
                }
            }

            if (ctx.HasWorkshop)
            {
                float toShop = Vector3.Distance(flag.WorldPosition, ctx.WorkshopPosition);
                if (toShop < 14f)
                    preferenceScore += ctx.FlagWorkshopBonus * (1f - toShop / 14f);
            }
            float distPenalty = Mathf.Clamp01(distance / 45f) * 0.55f;
            float risk = flag.Risk + bodyDanger * 0.4f;
            float riskPenalty = risk * (1.15f - data.courage);
            float crowdPenalty = Mathf.Clamp01(flag.ClaimCount * 0.18f);
            float fatiguePenalty = ctx.Fatigue * 0.25f * (distance / 30f);

            float finalScore =
                greedScore +
                preferenceScore -
                distPenalty -
                riskPenalty -
                crowdPenalty -
                fatiguePenalty;

            return Mathf.Clamp01(finalScore);
        }
    }
}
