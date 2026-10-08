using System;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>Per-class override of the motive numbers (Majesty 2 tunes warriors, mages, rogues and clerics separately).</summary>
    [Serializable]
    public struct ClassMotiveOverride
    {
        public SpecialistClass specialistClass;
        [Min(1)] public int maxWorkTasks;
        [Min(1)] public int maxRelaxTasks;
        [Tooltip("Flee when health falls below this fraction (0 = use the global panic rules).")]
        [Range(0f, 1f)] public float healthReflex;

        /// <summary>Our default retreat line (1 - panicInjury) that Majesty values are mapped around.</summary>
        public const float BaselineReflex = 0.45f;

        /// <summary>
        /// Map a Majesty 2 hero's Motivator max_tasks and HealthReflex health_percent onto ours.
        /// Majesty heroes retreat at 5-50% health (median 20%). Our robots go down at 2% and bites
        /// run up to 18%/s, so the raw values would be lethal. Instead the class keeps its
        /// position relative to the median around our 45% line: reflex = 0.45 + (pct - 20) x 0.006,
        /// clamped to 30-60%. Warrior 20% -> 45%, rogue 50% -> 60%, cleric 5% -> 36%.
        /// </summary>
        public static ClassMotiveOverride FromMajesty(SpecialistClass cls, int work, int relax, float healthPercent) =>
            new ClassMotiveOverride
            {
                specialistClass = cls,
                maxWorkTasks = Mathf.Max(1, work),
                maxRelaxTasks = Mathf.Max(1, relax),
                healthReflex = Mathf.Clamp(BaselineReflex + (healthPercent - 20f) * 0.006f, 0.3f, 0.6f)
            };

        /// <summary>Same class mapping as <see cref="ClassAllure.Defaults"/>. Engineer and Terraformer use the globals.</summary>
        public static ClassMotiveOverride[] Defaults() => new[]
        {
            FromMajesty(SpecialistClass.ScoutDrone, 12, 3, 35f),    // ranger
            FromMajesty(SpecialistClass.DefenseMech, 12, 3, 20f),   // warrior
            FromMajesty(SpecialistClass.Medic, 12, 3, 5f),          // cleric
            FromMajesty(SpecialistClass.HarvesterBot, 12, 3, 50f),  // rogue
            FromMajesty(SpecialistClass.SentinelMech, 12, 3, 10f),  // dwarf
            FromMajesty(SpecialistClass.SurveyorBot, 15, 5, 30f),   // elf
            FromMajesty(SpecialistClass.GeologistBot, 12, 3, 20f),  // marksman
            FromMajesty(SpecialistClass.CourierBot, 12, 3, 20f),    // beastmaster
        };
    }

    /// <summary>
    /// Every number the specialist utility brain uses, as one Inspector-editable block.
    /// Hold one in a MonoBehaviour with [SerializeField] (GameLoop does) and hand it to
    /// <see cref="SpecialistBrain.Tuning"/>. Defaults reproduce the original hand-tuned
    /// values, except the three curve exponents in "Distance" and "Threat and survival",
    /// which are the new non-linear pacing.
    ///
    /// Scores are 0..1. Bounties are judged in "brain units" (CRED / <see cref="MajestyEconomy.GoldScale"/>).
    /// </summary>
    [Serializable]
    public sealed class SpecialistBrainTuning
    {
        /// <summary>
        /// The tuning the running game uses. <see cref="OverseerRules.GreedAsk"/> reads it so the
        /// ask shown in the UI always matches the gate the brain applies.
        /// </summary>
        public static SpecialistBrainTuning Active = new SpecialistBrainTuning();

        [Header("Perception")]
        [Tooltip("Base flag consideration radius (m). Replay rules scale this at runtime.")]
        public float considerRange = 80f;
        [Tooltip("Minimum radius every hero considers, before the explore bonus (m).")]
        public float considerBase = 40f;
        [Tooltip("Extra radius (m) per point of the hero's explorePreference (0..1).")]
        public float considerExplorePerPoint = 35f;
        [Tooltip("The wide-ranging heroes use at least considerRange x this.")]
        public float considerRangeFactor = 0.7f;
        [Tooltip("Score bonus for the flag a hero already follows (stops flip-flopping).")]
        public float currentFlagHysteresis = 0.15f;

        [Header("Greed (credits)")]
        [Tooltip("Global greed multiplier. >1 makes every hero pickier and costlier; <1 more eager.")]
        [Range(0.25f, 2f)] public float greedMultiplier = 1f;
        [Tooltip("Bounty (brain units) that counts as a full-value payday.")]
        public float bountyReference = 100f;
        [Tooltip("Score a full payday gives a hero with zero greed.")]
        public float greedScoreBase = 0.55f;
        [Tooltip("Extra payday score per point of greed.")]
        public float greedScorePerGreed = 0.7f;
        [Tooltip("Extra payday score when the hero is broke (GreedHunger 1).")]
        public float hungerScoreBonus = 0.18f;
        [Tooltip("Asking price = (base + greed x perGreed) x fraction, in brain units.")]
        public float gateBase = 18f;
        public float gatePerGreed = 95f;
        public float gateFraction = 0.78f;
        [Tooltip("A hero this broke (GreedHunger) takes underpaid work anyway.")]
        [Range(0f, 1f)] public float hungerBypass = 0.75f;
        [Tooltip("Score needed to act = base + greed x perGreed - hunger x relief, then clamped.")]
        public float acceptBase = 0.38f;
        public float acceptPerGreed = 0.25f;
        public float acceptHungerRelief = 0.22f;
        public float acceptMin = 0.22f;
        public float acceptMax = 0.72f;

        [Header("Preference")]
        public float preferenceWeight = 0.9f;
        [Tooltip("Bonus when the flag names the hero's class as strongly attracted.")]
        public float attractBonus = 0.22f;
        [Tooltip("Workshop-linked flags within this radius (m) get the workshop bonus.")]
        public float workshopBonusRange = 14f;

        [Header("Distance (non-linear)")]
        [Tooltip("Distance (m) at which the penalty is at its maximum.")]
        public float distanceReference = 45f;
        [Tooltip("1 = linear. >1 keeps nearby flags cheap and makes far flags steeply worse.")]
        [Range(0.5f, 3f)] public float distanceExponent = 1.5f;
        public float distanceMaxPenalty = 0.55f;
        public float fatiguePenalty = 0.25f;
        [Tooltip("Fatigue penalty grows with distance / this (m).")]
        public float fatigueDistanceReference = 30f;
        public float crowdPenaltyPerClaim = 0.18f;

        [Header("Threat and survival (non-linear)")]
        [Tooltip("How much ambient body danger adds to a flag's own risk.")]
        public float bodyDangerWeight = 0.4f;
        [Tooltip("1 = linear. >1 shrugs off minor threats and deters sharply on big ones.")]
        [Range(0.5f, 3f)] public float threatExponent = 1.5f;
        [Tooltip("Risk penalty = threat^exp x (this - courage) x weight.")]
        public float riskCourageOffset = 1.15f;
        public float riskWeight = 1f;
        [Tooltip("Extra risk aversion as hull health drops: x(1 + (1-health)^2 x this).")]
        public float survivalFearWeight = 2.5f;
        [Tooltip("Threat a full-health hero shrugs off = base + courage x perCourage.")]
        public float fearThresholdBase = 0.35f;
        public float fearThresholdCourage = 0.5f;
        [Tooltip("At zero health that threshold shrinks to this fraction.")]
        [Range(0f, 1f)] public float fearMinHealthFactor = 0.4f;
        [Tooltip("Threat above the fear threshold costs this much score per point.")]
        public float fearSlope = 2f;
        [Tooltip("Broke-but-healthy heroes raise their fear threshold by this (x health).")]
        public float desperationCapacity = 0.2f;
        [Tooltip("Broke-but-healthy heroes discount the risk penalty by up to this fraction.")]
        [Range(0f, 1f)] public float desperationRiskDiscount = 0.5f;

        [Header("Panic and rest")]
        [Tooltip("Injury (1 - health) above this always panics.")]
        public float panicInjury = 0.55f;
        [Tooltip("Injury above this panics when danger is high and courage low.")]
        public float panicInjuryNervous = 0.32f;
        public float panicBodyDanger = 0.4f;
        public float panicCourage = 0.55f;
        [Tooltip("Panic only triggers below this health.")]
        public float panicHealthCeiling = 0.62f;
        [Tooltip("Once fleeing, keep going until health reaches this. Clearly above the enter line so a hero bouncing through 0.49–0.65 cannot flip back to workshop duty. The inn heal is what climbs to this line.")]
        [Range(0.5f, 1f)] public float fleeResumeHealth = 0.8f;
        [Tooltip("A new action has to stick at least this long. Stops a decision from flipping on every think.")]
        public float decisionDwellSeconds = 1.75f;
        public float fleeScore = 0.95f;
        public float restFatigueWeight = 0.7f;
        public float restInjuryWeight = 0.55f;
        [Tooltip("Rest score is scaled by (this - workaholicBias).")]
        public float restWorkaholicOffset = 1.1f;
        [Tooltip("Rest score above this overrides everything (forced rest).")]
        public float restForced = 0.78f;
        [Tooltip("Rest score above this beats wandering.")]
        public float restMild = 0.45f;
        [Tooltip("Rest score above this is offered to the Laya chooser.")]
        public float restOption = 0.3f;

        [Header("Hunting")]
        public float huntMinHealth = 0.38f;
        public float huntMinCombatPreference = 0.2f;
        public float huntDistanceReference = 28f;
        public float huntDistancePenalty = 0.55f;
        public float huntPreferenceWeight = 0.95f;
        public float huntCourageBoost = 0.45f;
        public float huntFearWeight = 0.5f;
        public float huntDangerWeight = 0.35f;
        public float huntHysteresis = 0.12f;

        [Header("Repair")]
        public float repairDistanceReference = 36f;
        public float repairDistancePenalty = 0.45f;
        public float repairPreferenceWeight = 0.95f;
        public float repairNeedWeight = 0.55f;
        public float repairGreedWeight = 0.12f;
        public float repairFatiguePenalty = 0.18f;
        [Tooltip("Repair needs only this fraction of the normal acceptance score.")]
        public float repairAcceptFactor = 0.82f;

        [Header("Pay duty")]
        [Tooltip("Score of walking carried guild tax home. Heroes finish a flag or hunt first.")]
        public float dutyScore = 0.6f;

        [Header("Idle behaviour scores")]
        public float workshopWanderScore = 0.34f;
        public float wanderScore = 0.28f;
        public float levyScore = 0.36f;
        public float triageScore = 0.48f;

        [Header("Motives (work / safety / relaxation)")]
        [Tooltip("Off = legacy behaviour (fatigue and instantaneous danger only).")]
        public bool motivesEnabled = true;
        [Tooltip("Work tasks (flag, hunt, repair) a hero does before a relaxation break.")]
        [Min(1)] public int maxWorkTasks = 12;
        [Tooltip("Rest / wander tasks in a break before heroes go back to work.")]
        [Min(1)] public int maxRelaxTasks = 3;
        [Tooltip("A break always ends after this many seconds.")]
        public float relaxMaxSeconds = 90f;
        [Tooltip("During a break a task needs this much extra score; a lavish bounty still lures them out.")]
        public float relaxFlagPremium = 0.2f;
        public float relaxScore = 0.5f;
        [Tooltip("A decision only counts as a new task after this many seconds.")]
        public float minTaskSeconds = 6f;
        [Tooltip("Safety eases down toward danger at this rate per second (slow = brave).")]
        public float safetyFallRate = 0.12f;
        [Tooltip("...and back up at this rate (fast = a hero shakes off a scare once safe). Majesty 2 ratio 4:1.")]
        public float safetyRiseRate = 0.5f;
        [Tooltip("How much ambient body danger lowers the safety target.")]
        public float safetyDangerWeight = 1f;
        [Tooltip("How much a building safety field (Commons, guilds, houses) lifts the safety target.")]
        public float safetyFieldWeight = 0.35f;
        [Tooltip("Safety below this makes a hurt hero retreat even if the danger spike has passed.")]
        [Range(0f, 1f)] public float safetyFleeBelow = 0.25f;
        [Tooltip("A shaken hero only goes back out once safety is above this.")]
        [Range(0f, 1f)] public float safetyResumeAbove = 0.6f;
        [Tooltip("Multipliers on the preference score by flag kind. Majesty 2 ratio is attack 2 : protect 1.5 : explore 1.")]
        public float flagAttackWeight = 1f;
        public float flagDefendWeight = 1f;
        public float flagExploreWeight = 1f;
        [Tooltip("Per-class task budgets and retreat health (Majesty 2 Motivator / HealthReflex). Unlisted classes use the globals.")]
        public ClassMotiveOverride[] classMotives = ClassMotiveOverride.Defaults();

        [Header("Per-class flag appeal (Majesty 2 AllureFactors)")]
        [Tooltip("Classes not listed are neutral (all 1).")]
        public ClassAllure[] classAllure = ClassAllure.Defaults();

        public ClassAllure AllureFor(SpecialistClass cls)
        {
            if (classAllure != null)
                for (int i = 0; i < classAllure.Length; i++)
                    if (classAllure[i].specialistClass == cls) return classAllure[i];
            return ClassAllure.Neutral(cls);
        }

        public int MaxWorkTasksFor(SpecialistClass cls)
        {
            if (classMotives != null)
                for (int i = 0; i < classMotives.Length; i++)
                    if (classMotives[i].specialistClass == cls && classMotives[i].maxWorkTasks > 0)
                        return classMotives[i].maxWorkTasks;
            return Mathf.Max(1, maxWorkTasks);
        }

        public int MaxRelaxTasksFor(SpecialistClass cls)
        {
            if (classMotives != null)
                for (int i = 0; i < classMotives.Length; i++)
                    if (classMotives[i].specialistClass == cls && classMotives[i].maxRelaxTasks > 0)
                        return classMotives[i].maxRelaxTasks;
            return Mathf.Max(1, maxRelaxTasks);
        }

        /// <summary>Health fraction below which this class retreats. Defaults to the global panic rule.</summary>
        public float HealthReflexFor(SpecialistClass cls)
        {
            if (classMotives != null)
                for (int i = 0; i < classMotives.Length; i++)
                    if (classMotives[i].specialistClass == cls && classMotives[i].healthReflex > 0f)
                        return classMotives[i].healthReflex;
            return 1f - panicInjury;
        }

        /// <summary>Greed after the global multiplier.</summary>
        public float EffectiveGreed(float baseGreed) => Mathf.Clamp(baseGreed * greedMultiplier, 0f, 2f);

        /// <summary>Smallest bounty (brain units) the greed gate accepts for this hero.</summary>
        public float GateBounty(float baseGreed) =>
            (gateBase + EffectiveGreed(baseGreed) * gatePerGreed) * gateFraction;
    }
}
