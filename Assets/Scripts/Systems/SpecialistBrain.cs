using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Pure decision core for autonomous specialists.
    /// The player never forces a job — only posts flags and hopes personality + greed accepts it.
    /// </summary>
    public sealed class SpecialistBrain
    {
        public float ConsiderRange = 55f;
        public float CurrentFlagHysteresis = 0.15f;   // stickiness to current job
        public float RestThreshold = 0.72f;          // fatigue level that strongly pushes Rest

        public BrainDecision Evaluate(
            in SpecialistContext ctx,
            IReadOnlyList<FlagHandle> openFlags,
            float bodyDanger = 0.3f)
        {
            if (ctx.Data == null)
                return BrainDecision.Idle(0f, "missing_data");

            // 1. Strong rest pressure when exhausted or hurt
            float restScore = CalculateRestScore(ctx);
            if (restScore > 0.78f)
                return BrainDecision.Rest(restScore, "exhausted_or_hurt");

            // 2. Score every open flag
            FlagHandle bestFlag = null;
            float bestScore = -1f;
            string bestReason = "none";

            if (openFlags != null)
            {
                for (int i = 0; i < openFlags.Count; i++)
                {
                    var flag = openFlags[i];
                    if (flag == null || flag.Data == null) continue;

                    float dist = Vector3.Distance(ctx.Position, flag.WorldPosition);
                    if (ConsiderRange > 0f && dist > ConsiderRange) continue;

                    float score = ScoreFlag(ctx, flag, dist, bodyDanger);

                    // Commitment bonus
                    if (ctx.CurrentFlag != null &&
                        ReferenceEquals(ctx.CurrentFlag.RuntimeId, flag.RuntimeId))
                    {
                        score += CurrentFlagHysteresis;
                    }

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestFlag = flag;
                        bestReason = $"flag_{flag.Data.flagType}";
                    }
                }
            }

            // 3. Only accept if the score is high enough (greed gate)
            float acceptanceThreshold = 0.38f + (ctx.Data.baseGreed * 0.25f);

            if (bestFlag != null && bestScore >= acceptanceThreshold)
                return BrainDecision.Pursue(bestFlag, bestScore, bestReason);

            // 4. Fall back to rest or idle
            if (restScore > 0.45f)
                return BrainDecision.Rest(restScore, "mild_fatigue");

            return BrainDecision.Idle(0.2f, "no_attractive_flag");
        }

        float CalculateRestScore(in SpecialistContext ctx)
        {
            float fatigue = ctx.Fatigue;
            float injury = 1f - ctx.HealthNormalized;

            float score = fatigue * 0.7f + injury * 0.55f;

            // Personality: some specialists hate resting
            score *= (1.1f - ctx.Data.workaholicBias);

            return Mathf.Clamp01(score);
        }

        float ScoreFlag(in SpecialistContext ctx, FlagHandle flag, float distance, float bodyDanger)
        {
            var data = ctx.Data;
            var fdata = flag.Data;

            // --- Base reward attractiveness (greed) ---
            float bountyFactor = Mathf.Clamp01(flag.CurrentBounty / 100f); // normalize around 100 as "good"
            float greedScore = bountyFactor * (0.55f + data.baseGreed * 0.7f);

            // --- Task preference ---
            float preference = data.GetPreference(fdata.flagType); // 0-1
            float preferenceScore = preference * 0.9f;

            // --- Distance penalty ---
            float distPenalty = Mathf.Clamp01(distance / 45f) * 0.55f;

            // --- Risk evaluation ---
            float risk = flag.Risk + bodyDanger * 0.4f;
            float courage = data.courage; // 0 = coward, 1 = fearless
            float riskPenalty = risk * (1.15f - courage);

            // --- Competition penalty (too many already going) ---
            float crowdPenalty = Mathf.Clamp01(flag.ClaimCount * 0.18f);

            // --- Fatigue makes long trips less attractive ---
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
