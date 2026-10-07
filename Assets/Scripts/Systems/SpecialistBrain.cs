using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Majesty-style utility AI: bounties, fear, opportunistic hunting, and kingdom vocation.
    /// The player never forces a job — only posts flags and hopes personality + greed accepts it.
    /// Every threshold lives in <see cref="SpecialistBrainTuning"/> so it can be tuned in the Inspector.
    /// </summary>
    public sealed class SpecialistBrain
    {
        private SpecialistBrainTuning _tuning = new SpecialistBrainTuning();

        public SpecialistBrainTuning Tuning
        {
            get => _tuning;
            set => _tuning = value ?? new SpecialistBrainTuning();
        }

        /// <summary>Replay-rule multiplier on <see cref="SpecialistBrainTuning.considerRange"/>.</summary>
        public float ConsiderRangeScale = 1f;

        /// <summary>Effective flag consideration radius: tuning range x replay scale.</summary>
        public float ConsiderRange => _tuning.considerRange * ConsiderRangeScale;

        public float CurrentFlagHysteresis => _tuning.currentFlagHysteresis;

        public BrainDecision Evaluate(
            in SpecialistContext ctx,
            IReadOnlyList<FlagHandle> openFlags,
            float bodyDanger = 0.3f)
        {
            if (ctx.Data == null)
                return BrainDecision.Idle(0f, "missing_data");

            var t = _tuning;
            var data = ctx.Data;
            Vector3 inn = ctx.SafetyPosition.sqrMagnitude > 0.01f
                ? ctx.SafetyPosition
                : ctx.Position;

            float restScore = CalculateRestScore(ctx);

            // 1. Panic — Majesty heroes drop quests and run to the inn when badly hurt.
            if (IsPanicked(ctx, bodyDanger))
                return BrainDecision.Flee(inn, t.fleeScore, "flee_to_inn");

            // 2. Exhaustion / injury — rest at the inn, not in the field.
            if (restScore > t.restForced)
                return BrainDecision.Rest(restScore, "exhausted_or_hurt", inn);

            // 2b. Pay duty: a hero with a full tax purse walks it to the guild, but finishes
            //     the flag or fight it is already on first (Majesty 2 TaxCashCard).
            if (ctx.HasDutyWalk &&
                ctx.CurrentAction != SpecialistAction.PursueFlag &&
                ctx.CurrentAction != SpecialistAction.Hunt)
            {
                return BrainDecision.Wander(ctx.DutyPosition, t.dutyScore, "pay_duty");
            }

            // 3. Player bounties (greed gate). Broke heroes take cheaper flags.
            FlagHandle bestFlag = null;
            float bestFlagScore = -1f;
            string bestFlagReason = "none";
            float consider = ConsiderDistance(data);

            if (openFlags != null)
            {
                for (int i = 0; i < openFlags.Count; i++)
                {
                    var flag = openFlags[i];
                    if (flag == null || flag.Data == null) continue;
                    if (!FlagOrdersRules.Permits(ctx, flag)) continue; // player's written orders

                    float dist = Vector3.Distance(ctx.Position, flag.WorldPosition);
                    if (consider > 0f && dist > consider) continue;

                    float score = ScoreFlag(ctx, flag, dist, bodyDanger);
                    if (ctx.CurrentFlag != null &&
                        ReferenceEquals(ctx.CurrentFlag.RuntimeId, flag.RuntimeId))
                    {
                        score += t.currentFlagHysteresis;
                    }

                    if (score > bestFlagScore)
                    {
                        bestFlagScore = score;
                        bestFlag = flag;
                        bestFlagReason = $"flag_{flag.Data.flagType}";
                    }
                }
            }

            float acceptance = Acceptance(ctx);
            bool takeFlag = bestFlag != null && bestFlagScore >= acceptance;
            if (takeFlag && !PassesGreedGate(data, bestFlag.CurrentBounty, ctx.GreedHunger))
                takeFlag = false;

            // 4. Opportunistic hunt — warriors engage nearby fauna without a posted bounty.
            float huntScore = -1f;
            if (CanHunt(ctx))
            {
                huntScore = ScoreHunt(ctx, bodyDanger);
                if (ctx.CurrentAction == SpecialistAction.Hunt)
                    huntScore += t.huntHysteresis;
            }

            if (huntScore >= acceptance && huntScore > bestFlagScore)
                return BrainDecision.Hunt(ctx.HuntPosition, huntScore, "hunt_fauna");

            // 4b. Engineers patch damaged modules for a small personal payday.
            float repairScore = -1f;
            if (data.specialistClass == SpecialistClass.EngineerBot && ctx.HasRepair)
            {
                repairScore = ScoreRepair(ctx);
                if (ctx.CurrentAction == SpecialistAction.Repair)
                    repairScore += t.huntHysteresis;
            }

            if (repairScore >= acceptance * t.repairAcceptFactor &&
                repairScore > bestFlagScore &&
                repairScore > huntScore)
            {
                return BrainDecision.Repair(ctx.RepairPosition, repairScore, "repair_module");
            }

            if (takeFlag)
                return BrainDecision.Pursue(bestFlag, bestFlagScore, bestFlagReason);

            if (data.specialistClass == SpecialistClass.Medic && ctx.HasPatient &&
                ctx.HealthNormalized > t.huntMinHealth)
            {
                return BrainDecision.Wander(ctx.PatientPosition, t.triageScore, "triage");
            }

            // 5a. Scheduled break: a hero that has done its share of work goes to the inn.
            if (IsRelaxing(ctx))
                return BrainDecision.Rest(Mathf.Max(restScore, t.relaxScore), "relax_break", inn);

            // 5. Mild rest if worn, else kingdom vocation (never stand still).
            if (restScore > t.restMild)
                return BrainDecision.Rest(restScore, "mild_fatigue", inn);

            // Courier levy is a wander bias, not a flag. ScoreFlag is untouched.
            if (data.specialistClass == SpecialistClass.CourierBot && ctx.HasLevyWalk)
            {
                string levyReason = ctx.LevyCarrying ? "levy_home" : "levy_collect";
                return BrainDecision.Wander(ctx.LevyPosition, t.levyScore, levyReason);
            }

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
            return BrainDecision.Wander(dest, ctx.HasWorkshop ? t.workshopWanderScore : t.wanderScore, vocation);
        }

        /// <summary>
        /// Every option the utility gates would allow right now, for an external chooser (the local
        /// Laya model) to pick between. Always includes <see cref="Evaluate"/>'s own pick first.
        /// Returns false when the choice is forced (panic flee / exhaustion) and must not be
        /// overridden. Reuses the scorers; does not change Evaluate.
        /// </summary>
        public bool CollectOptions(
            in SpecialistContext ctx,
            IReadOnlyList<FlagHandle> openFlags,
            float bodyDanger,
            List<BrainDecision> into,
            int maxFlags = 3)
        {
            into.Clear();
            var utility = Evaluate(ctx, openFlags, bodyDanger);
            into.Add(utility);
            if (ctx.Data == null) return false;
            if (utility.Action == SpecialistAction.Flee || utility.Reason == "exhausted_or_hurt")
                return false;

            var t = _tuning;
            var data = ctx.Data;
            Vector3 inn = ctx.SafetyPosition.sqrMagnitude > 0.01f ? ctx.SafetyPosition : ctx.Position;
            float acceptance = Acceptance(ctx);

            if (openFlags != null)
            {
                var taken = new List<BrainDecision>();
                for (int i = 0; i < openFlags.Count; i++)
                {
                    var flag = openFlags[i];
                    if (!WouldTakeFlag(ctx, flag, bodyDanger, out float score)) continue;
                    taken.Add(BrainDecision.Pursue(flag, score, $"flag_{flag.Data.flagType}"));
                }
                taken.Sort((a, b) => b.Score.CompareTo(a.Score));
                for (int i = 0; i < taken.Count && i < maxFlags; i++)
                    AddUnique(into, taken[i]);
            }

            if (CanHunt(ctx))
            {
                float hunt = ScoreHunt(ctx, bodyDanger);
                if (hunt >= acceptance)
                    AddUnique(into, BrainDecision.Hunt(ctx.HuntPosition, hunt, "hunt_fauna"));
            }

            if (data.specialistClass == SpecialistClass.EngineerBot && ctx.HasRepair)
            {
                float repair = ScoreRepair(ctx);
                if (repair >= acceptance * t.repairAcceptFactor)
                    AddUnique(into, BrainDecision.Repair(ctx.RepairPosition, repair, "repair_module"));
            }

            float rest = CalculateRestScore(ctx);
            if (rest > t.restOption)
                AddUnique(into, BrainDecision.Rest(rest, "mild_fatigue", inn));

            if (utility.Action != SpecialistAction.Wander)
            {
                Vector3 dest = ctx.VocationPosition.sqrMagnitude > 0.01f ? ctx.VocationPosition : inn;
                AddUnique(into, BrainDecision.Wander(dest, t.wanderScore, "wandering_frontier"));
            }
            return into.Count > 1;
        }

        static void AddUnique(List<BrainDecision> list, BrainDecision d)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var o = list[i];
                if (o.Action != d.Action) continue;
                if (d.Action == SpecialistAction.PursueFlag)
                {
                    if (ReferenceEquals(o.TargetFlag, d.TargetFlag)) return;
                    continue;
                }
                return; // one option per non-flag action
            }
            list.Add(d);
        }

        /// <summary>
        /// True if this hero would Pursue the flag right now. Uses the same score + greed gate as Evaluate.
        /// </summary>
        public bool WouldTakeFlag(in SpecialistContext ctx, FlagHandle flag, float bodyDanger, out float score)
        {
            score = 0f;
            if (ctx.Data == null || flag?.Data == null) return false;

            if (IsPanicked(ctx, bodyDanger)) return false;
            if (CalculateRestScore(ctx) > _tuning.restForced) return false;
            if (!FlagOrdersRules.Permits(ctx, flag)) return false;

            float dist = Vector3.Distance(ctx.Position, flag.WorldPosition);
            float consider = ConsiderDistance(ctx.Data);
            if (consider > 0f && dist > consider) return false;

            score = ScoreFlag(ctx, flag, dist, bodyDanger);
            if (score < Acceptance(ctx)) return false;
            return PassesGreedGate(ctx.Data, flag.CurrentBounty, ctx.GreedHunger);
        }

        /// <summary>
        /// Why WouldTakeFlag is false. Reads the same gates as Evaluate.
        /// </summary>
        public FlagRefusalKind ExplainFlag(in SpecialistContext ctx, FlagHandle flag, float bodyDanger)
        {
            if (ctx.Data == null || flag?.Data == null) return FlagRefusalKind.Ignored;

            if (IsPanicked(ctx, bodyDanger)) return FlagRefusalKind.Hurt;
            if (CalculateRestScore(ctx) > _tuning.restForced) return FlagRefusalKind.Hurt;
            if (!FlagOrdersRules.Permits(ctx, flag)) return FlagRefusalKind.Orders;

            float dist = Vector3.Distance(ctx.Position, flag.WorldPosition);
            float consider = ConsiderDistance(ctx.Data);
            if (consider > 0f && dist > consider) return FlagRefusalKind.TooFar;

            float score = ScoreFlag(ctx, flag, dist, bodyDanger);
            float acceptance = Acceptance(ctx);

            float huntScore = -1f;
            if (CanHunt(ctx))
            {
                huntScore = ScoreHunt(ctx, bodyDanger);
                if (ctx.CurrentAction == SpecialistAction.Hunt)
                    huntScore += _tuning.huntHysteresis;
            }

            if (huntScore >= acceptance && huntScore > score)
                return FlagRefusalKind.Hunting;

            if (score < acceptance)
            {
                if (ctx.Data.GetPreference(flag.Data.flagType) < 0.25f)
                    return FlagRefusalKind.NotMyJob;
                return FlagRefusalKind.Ignored;
            }

            if (!PassesGreedGate(ctx.Data, flag.CurrentBounty, ctx.GreedHunger))
                return FlagRefusalKind.Greed;

            return FlagRefusalKind.WouldTake;
        }

        // ------------------------------------------------------------------ shared gates

        /// <summary>Badly hurt, or hurt-and-nervous in a dangerous place: drop everything and run.</summary>
        bool IsPanicked(in SpecialistContext ctx, float bodyDanger)
        {
            var t = _tuning;
            float injury = 1f - ctx.HealthNormalized;
            float courage = EffectiveCourage(ctx);
            float reflex = t.HealthReflexFor(ctx.Data.specialistClass);
            bool panicked = injury > 1f - reflex ||
                            (injury > t.panicInjuryNervous && bodyDanger > t.panicBodyDanger &&
                             courage < t.panicCourage);
            // A scare lingers: stay home until safety has recovered, not just until danger passes.
            if (t.motivesEnabled && ctx.Motives != null && ctx.Motives.Shaken)
                panicked = true;
            return panicked && ctx.HealthNormalized < t.panicHealthCeiling;
        }

        /// <summary>Score a task must reach before this hero commits. Greedy heroes are pickier; broke ones aren't.</summary>
        float Acceptance(in SpecialistContext ctx)
        {
            var t = _tuning;
            float greed = t.EffectiveGreed(ctx.Data.baseGreed);
            float a = t.acceptBase + greed * t.acceptPerGreed - ctx.GreedHunger * t.acceptHungerRelief;
            a = Mathf.Clamp(a, t.acceptMin, t.acceptMax);
            // On a break only a standout task tempts a hero out.
            if (IsRelaxing(ctx)) a += t.relaxFlagPremium;
            return a;
        }

        bool IsRelaxing(in SpecialistContext ctx) =>
            _tuning.motivesEnabled && ctx.Motives != null && ctx.Motives.Relaxing;

        /// <summary>How far (m) this hero will look for flags.</summary>
        public float ConsiderDistance(SpecialistData data)
        {
            var t = _tuning;
            float consider = t.considerBase + data.explorePreference * t.considerExplorePerPoint;
            float range = ConsiderRange;
            if (range > 0f)
                consider = Mathf.Max(consider, range * t.considerRangeFactor);
            return consider;
        }

        bool CanHunt(in SpecialistContext ctx) =>
            ctx.HasHunt && ctx.Data.specialistClass != SpecialistClass.Medic &&
            ctx.Data.combatPreference >= _tuning.huntMinCombatPreference &&
            ctx.HealthNormalized > _tuning.huntMinHealth;

        /// <summary>Greedy heroes skip underpaid jobs unless starving.</summary>
        bool PassesGreedGate(SpecialistData data, float bounty, float hunger)
        {
            if (data == null) return false;
            bounty = MajestyEconomy.ToBrain(bounty); // CRED → the 1/10 units this gate was tuned on
            if (bounty + 0.01f >= _tuning.GateBounty(data.baseGreed)) return true;
            return hunger > _tuning.hungerBypass;
        }

        float CalculateRestScore(in SpecialistContext ctx)
        {
            var t = _tuning;
            float injury = 1f - ctx.HealthNormalized;
            float score = ctx.Fatigue * t.restFatigueWeight + injury * t.restInjuryWeight;
            score *= (t.restWorkaholicOffset - ctx.Data.workaholicBias);
            return Mathf.Clamp01(score);
        }

        float ScoreHunt(in SpecialistContext ctx, float bodyDanger)
        {
            var t = _tuning;
            var data = ctx.Data;
            float courage = EffectiveCourage(ctx);
            float distPenalty = Mathf.Clamp01(ctx.HuntDistance / t.huntDistanceReference) * t.huntDistancePenalty;
            float courageBoost = courage * t.huntCourageBoost;
            float pref = data.combatPreference * t.huntPreferenceWeight;
            float fear = (1f - ctx.HealthNormalized) * (1.1f - courage) * t.huntFearWeight;
            float danger = bodyDanger * (1.05f - courage) * t.huntDangerWeight;
            return Mathf.Clamp01(pref + courageBoost - distPenalty - fear - danger);
        }

        float ScoreRepair(in SpecialistContext ctx)
        {
            var t = _tuning;
            var data = ctx.Data;
            float pref = data.buildPreference * t.repairPreferenceWeight;
            float need = ctx.RepairNeed * t.repairNeedWeight;
            float greed = ctx.GreedHunger * t.repairGreedWeight;
            float distPenalty = Mathf.Clamp01(ctx.RepairDistance / t.repairDistanceReference) * t.repairDistancePenalty;
            float fatiguePenalty = ctx.Fatigue * t.repairFatiguePenalty;
            return Mathf.Clamp01(pref + need + greed - distPenalty - fatiguePenalty);
        }

        float FlagKindWeight(FlagType type)
        {
            switch (type)
            {
                case FlagType.ClearThreat: return _tuning.flagAttackWeight;
                case FlagType.DefendArea: return _tuning.flagDefendWeight;
                case FlagType.Explore: return _tuning.flagExploreWeight;
                default: return 1f;
            }
        }

        static float EffectiveCourage(in SpecialistContext ctx) =>
            ctx.CourageEffective > 0.01f ? ctx.CourageEffective : (ctx.Data != null ? ctx.Data.courage : 0.5f);

        /// <summary>
        /// Flag desirability, 0..1. Three non-linear terms keep heroes from ignoring good work or
        /// suiciding on it:
        ///   distance — penalty grows as (d/ref)^distanceExponent: near flags are cheap, far ones steeply worse;
        ///   threat   — penalty grows as threat^threatExponent, scaled by (offset - courage) and by how
        ///              hurt the hull is, so weak heroes avoid danger a healthy one shrugs off;
        ///   fear     — threat beyond what this hero can survive (courage x health) costs extra, while a
        ///              broke-but-healthy hero (GreedHunger) tolerates more. Broke and hurt does not.
        /// </summary>
        float ScoreFlag(in SpecialistContext ctx, FlagHandle flag, float distance, float bodyDanger)
        {
            var t = _tuning;
            var data = ctx.Data;
            var fdata = flag.Data;
            float courage = EffectiveCourage(ctx);
            float health = Mathf.Clamp01(ctx.HealthNormalized);
            float hunger = Mathf.Clamp01(ctx.GreedHunger);
            float greed = t.EffectiveGreed(data.baseGreed);

            float bountyFactor = Mathf.Clamp01(MajestyEconomy.ToBrain(flag.CurrentBounty) / t.bountyReference);
            float greedScore = bountyFactor * (t.greedScoreBase + greed * t.greedScorePerGreed);
            greedScore += hunger * t.hungerScoreBonus * bountyFactor;

            float preferenceScore = data.GetPreference(fdata.flagType) * t.preferenceWeight;
            var allure = t.AllureFor(data.specialistClass);
            preferenceScore *= FlagKindWeight(fdata.flagType) * allure.KindMul(fdata.flagType);
            if (fdata.stronglyAttracts != null)
            {
                for (int i = 0; i < fdata.stronglyAttracts.Length; i++)
                {
                    if (fdata.stronglyAttracts[i] == data.specialistClass)
                    {
                        preferenceScore += t.attractBonus;
                        break;
                    }
                }
            }

            if (ctx.HasWorkshop)
            {
                float toShop = Vector3.Distance(flag.WorldPosition, ctx.WorkshopPosition);
                if (toShop < t.workshopBonusRange)
                    preferenceScore += ctx.FlagWorkshopBonus * (1f - toShop / t.workshopBonusRange);
            }

            float distPenalty =
                Mathf.Pow(Mathf.Clamp01(distance / t.distanceReference), t.distanceExponent) * t.distanceMaxPenalty *
                allure.distanceMul;

            float threat = Mathf.Max(0f, flag.Risk + bodyDanger * t.bodyDangerWeight);
            float fragility = 1f + (1f - health) * (1f - health) * t.survivalFearWeight;
            float desperation = hunger * health * t.desperationRiskDiscount;
            float riskPenalty = Mathf.Pow(threat, t.threatExponent) *
                                Mathf.Max(0f, t.riskCourageOffset - courage) *
                                t.riskWeight * fragility * (1f - desperation);

            float survivable =
                (t.fearThresholdBase + courage * t.fearThresholdCourage) *
                Mathf.Lerp(t.fearMinHealthFactor, 1f, health) +
                hunger * health * t.desperationCapacity;
            float fearPenalty = Mathf.Max(0f, threat - survivable) * t.fearSlope;
            riskPenalty *= allure.dangerMul;
            fearPenalty *= allure.dangerMul;

            float crowdPenalty = Mathf.Clamp01(flag.ClaimCount * t.crowdPenaltyPerClaim);
            float fatiguePenalty = ctx.Fatigue * t.fatiguePenalty * (distance / t.fatigueDistanceReference);

            float finalScore =
                greedScore +
                preferenceScore -
                distPenalty -
                riskPenalty -
                fearPenalty -
                crowdPenalty -
                fatiguePenalty;

            return Mathf.Clamp01(finalScore);
        }
    }
}
