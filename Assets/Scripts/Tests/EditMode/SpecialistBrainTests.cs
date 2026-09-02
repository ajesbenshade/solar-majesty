using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// Characterisation tests. These lock the CURRENT behaviour of SpecialistBrain scoring so a
    /// refactor elsewhere cannot silently move the greed gate. ScoreFlag is frozen by design rule —
    /// if one of these fails, the scoring changed and that needs explicit design sign-off, not a
    /// test update.
    /// </summary>
    public class SpecialistBrainTests
    {
        private const float Tol = 1e-4f;

        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _created.Count; i++)
            {
                if (_created[i] != null)
                    Object.DestroyImmediate(_created[i]);
            }
            _created.Clear();
        }

        /// <summary>Neutral specialist: every personality dial at the asset default.</summary>
        private SpecialistData MakeSpecialist(
            SpecialistClass cls = SpecialistClass.EngineerBot,
            float greed = 0.6f,
            float courage = 0.5f)
        {
            var data = ScriptableObject.CreateInstance<SpecialistData>();
            _created.Add(data);
            data.specialistClass = cls;
            data.baseGreed = greed;
            data.courage = courage;
            data.workaholicBias = 0.4f;
            data.explorePreference = 0.5f;
            data.buildPreference = 0.5f;
            data.combatPreference = 0.5f;
            data.extractPreference = 0.5f;
            data.defendPreference = 0.5f;
            return data;
        }

        private FlagData MakeFlagData(FlagType type = FlagType.Build)
        {
            var data = ScriptableObject.CreateInstance<FlagData>();
            _created.Add(data);
            data.flagType = type;
            data.stronglyAttracts = null;
            return data;
        }

        private FlagHandle MakeFlag(FlagData data, float bounty, Vector3 pos, float risk = 0f, int claims = 0)
        {
            return new FlagHandle
            {
                Data = data,
                WorldPosition = pos,
                CurrentBounty = bounty,
                Risk = risk,
                ClaimCount = claims,
                RuntimeId = new object()
            };
        }

        private SpecialistContext MakeContext(SpecialistData data)
        {
            return new SpecialistContext
            {
                Data = data,
                Position = Vector3.zero,
                Fatigue = 0f,
                GreedHunger = 0f,
                HealthNormalized = 1f,
                SafetyPosition = new Vector3(0f, 0f, -5f),
                CurrentAction = SpecialistAction.Idle
            };
        }

        // ---------------------------------------------------------------
        // Frozen score values
        // ---------------------------------------------------------------

        /// <summary>
        /// Hand-computed from ScoreFlag: greed 0.3*(0.55+0.6*0.7)=0.291, preference 0.5*0.9=0.45,
        /// distance penalty clamp01(10/45)*0.55=0.1222222. No risk, crowd, or fatigue.
        /// </summary>
        [Test]
        public void ScoreFlag_UnderpaidBuildFlag_ScoresExactly()
        {
            var brain = new SpecialistBrain();
            var data = MakeSpecialist();
            var ctx = MakeContext(data);
            var flag = MakeFlag(MakeFlagData(), 30f, new Vector3(10f, 0f, 0f));

            brain.WouldTakeFlag(ctx, flag, 0f, out float score);

            Assert.AreEqual(0.6187778f, score, Tol);
        }

        /// <summary>Same flag at a bounty that clears the gate: 0.59*0.97 + 0.45 - 0.1222222.</summary>
        [Test]
        public void ScoreFlag_PaidBuildFlag_ScoresExactly()
        {
            var brain = new SpecialistBrain();
            var data = MakeSpecialist();
            var ctx = MakeContext(data);
            var flag = MakeFlag(MakeFlagData(), 59f, new Vector3(10f, 0f, 0f));

            brain.WouldTakeFlag(ctx, flag, 0f, out float score);

            Assert.AreEqual(0.9000778f, score, Tol);
        }

        [Test]
        public void ScoreFlag_IsAlwaysClamped()
        {
            var brain = new SpecialistBrain();
            var data = MakeSpecialist();
            var ctx = MakeContext(data);
            var rich = MakeFlag(MakeFlagData(), 100000f, Vector3.zero);

            brain.WouldTakeFlag(ctx, rich, 0f, out float score);

            Assert.LessOrEqual(score, 1f);
            Assert.GreaterOrEqual(score, 0f);
        }

        // ---------------------------------------------------------------
        // The greed gate — the game's signature moment
        // ---------------------------------------------------------------

        /// <summary>
        /// The headline beat: a well-scoring flag is still refused because the pay is below the
        /// hero's asking price. Score passes acceptance; the greed gate is what says no.
        /// </summary>
        [Test]
        public void GreedGate_RefusesUnderpaidFlag_EvenWhenScoreIsHigh()
        {
            var brain = new SpecialistBrain();
            var data = MakeSpecialist();
            var ctx = MakeContext(data);
            var flag = MakeFlag(MakeFlagData(), 30f, new Vector3(10f, 0f, 0f));

            bool would = brain.WouldTakeFlag(ctx, flag, 0f, out float score);
            var reason = brain.ExplainFlag(ctx, flag, 0f);

            Assert.IsFalse(would, "underpaid flag should be refused");
            Assert.Greater(score, 0.53f, "score itself clears acceptance; only greed blocks it");
            Assert.AreEqual(FlagRefusalKind.Greed, reason);
        }

        /// <summary>Raise the bounty over the ask and the same hero accepts.</summary>
        [Test]
        public void GreedGate_AcceptsOnceBountyClearsAsk()
        {
            var brain = new SpecialistBrain();
            var data = MakeSpecialist();
            var ctx = MakeContext(data);
            var flag = MakeFlag(MakeFlagData(), 59f, new Vector3(10f, 0f, 0f));

            Assert.IsTrue(brain.WouldTakeFlag(ctx, flag, 0f, out _));
            Assert.AreEqual(FlagRefusalKind.WouldTake, brain.ExplainFlag(ctx, flag, 0f));
        }

        /// <summary>need = 18 + greed*95, gate opens at 78% of it. For greed 0.6 that is 58.5.</summary>
        [Test]
        public void GreedGate_BoundaryIsSeventyEightPercentOfAsk()
        {
            var brain = new SpecialistBrain();
            var data = MakeSpecialist();
            var ctx = MakeContext(data);
            var pos = new Vector3(10f, 0f, 0f);

            Assert.IsFalse(brain.WouldTakeFlag(ctx, MakeFlag(MakeFlagData(), 58.0f, pos), 0f, out _));
            Assert.IsTrue(brain.WouldTakeFlag(ctx, MakeFlag(MakeFlagData(), 58.5f, pos), 0f, out _));
        }

        /// <summary>A starving hero takes work it would otherwise refuse.</summary>
        [Test]
        public void GreedGate_HungerAboveThreshold_BypassesTheGate()
        {
            var brain = new SpecialistBrain();
            var data = MakeSpecialist();
            var ctx = MakeContext(data);
            ctx.GreedHunger = 0.8f;
            var flag = MakeFlag(MakeFlagData(), 30f, new Vector3(10f, 0f, 0f));

            Assert.IsTrue(brain.WouldTakeFlag(ctx, flag, 0f, out _));
        }

        [Test]
        public void GreedAsk_MatchesTheGateUsedByTheBrain()
        {
            var brain = new SpecialistBrain();
            var data = MakeSpecialist();
            var ctx = MakeContext(data);
            int ask = OverseerRules.GreedAsk(data);

            Assert.IsTrue(
                brain.WouldTakeFlag(ctx, MakeFlag(MakeFlagData(), ask, new Vector3(10f, 0f, 0f)), 0f, out _),
                "the displayed ask must be a bounty the hero actually accepts");
        }

        // ---------------------------------------------------------------
        // Refusal reasons
        // ---------------------------------------------------------------

        [Test]
        public void Refusal_BeyondConsiderRange_IsTooFar()
        {
            var brain = new SpecialistBrain();
            var data = MakeSpecialist();
            var ctx = MakeContext(data);
            var flag = MakeFlag(MakeFlagData(), 500f, new Vector3(5000f, 0f, 0f));

            Assert.IsFalse(brain.WouldTakeFlag(ctx, flag, 0f, out _));
            Assert.AreEqual(FlagRefusalKind.TooFar, brain.ExplainFlag(ctx, flag, 0f));
        }

        [Test]
        public void Refusal_BadlyHurt_IsHurt()
        {
            var brain = new SpecialistBrain();
            var data = MakeSpecialist();
            var ctx = MakeContext(data);
            ctx.HealthNormalized = 0.2f;
            var flag = MakeFlag(MakeFlagData(), 500f, new Vector3(10f, 0f, 0f));

            Assert.AreEqual(FlagRefusalKind.Hurt, brain.ExplainFlag(ctx, flag, 0f));
        }

        [Test]
        public void Refusal_LowPreferenceAndLowScore_IsNotMyJob()
        {
            var brain = new SpecialistBrain();
            var data = MakeSpecialist();
            data.combatPreference = 0.05f;
            data.defendPreference = 0.05f;
            var ctx = MakeContext(data);
            var flag = MakeFlag(MakeFlagData(FlagType.ClearThreat), 1f, new Vector3(30f, 0f, 0f));

            Assert.AreEqual(FlagRefusalKind.NotMyJob, brain.ExplainFlag(ctx, flag, 0f));
        }

        [Test]
        public void Refusal_NullFlag_IsIgnored()
        {
            var brain = new SpecialistBrain();
            var ctx = MakeContext(MakeSpecialist());

            Assert.AreEqual(FlagRefusalKind.Ignored, brain.ExplainFlag(ctx, null, 0f));
        }

        // ---------------------------------------------------------------
        // Score monotonicity — the levers the player actually pulls
        // ---------------------------------------------------------------

        [Test]
        public void RaisingBounty_NeverLowersScore()
        {
            var brain = new SpecialistBrain();
            var data = MakeSpecialist();
            var ctx = MakeContext(data);
            var pos = new Vector3(12f, 0f, 0f);
            float previous = -1f;

            for (int bounty = 0; bounty <= 120; bounty += 10)
            {
                brain.WouldTakeFlag(ctx, MakeFlag(MakeFlagData(), bounty, pos), 0f, out float score);
                Assert.GreaterOrEqual(score, previous, $"score dropped at bounty {bounty}");
                previous = score;
            }
        }

        [Test]
        public void GreaterDistance_LowersScore()
        {
            var brain = new SpecialistBrain();
            var data = MakeSpecialist();
            var ctx = MakeContext(data);

            brain.WouldTakeFlag(ctx, MakeFlag(MakeFlagData(), 60f, new Vector3(5f, 0f, 0f)), 0f, out float near);
            brain.WouldTakeFlag(ctx, MakeFlag(MakeFlagData(), 60f, new Vector3(40f, 0f, 0f)), 0f, out float far);

            Assert.Greater(near, far);
        }

        [Test]
        public void MoreClaims_LowersScore()
        {
            var brain = new SpecialistBrain();
            var data = MakeSpecialist();
            var ctx = MakeContext(data);
            var pos = new Vector3(10f, 0f, 0f);

            brain.WouldTakeFlag(ctx, MakeFlag(MakeFlagData(), 60f, pos, 0f, 0), 0f, out float alone);
            brain.WouldTakeFlag(ctx, MakeFlag(MakeFlagData(), 60f, pos, 0f, 3), 0f, out float crowded);

            Assert.Greater(alone, crowded, "crowd penalty should spread heroes across flags");
        }

        [Test]
        public void HigherBodyDanger_LowersScoreForCowards()
        {
            var brain = new SpecialistBrain();
            var data = MakeSpecialist(courage: 0.2f);
            var ctx = MakeContext(data);
            var flag = MakeFlag(MakeFlagData(), 60f, new Vector3(10f, 0f, 0f), 0.5f);

            brain.WouldTakeFlag(ctx, flag, 0f, out float calm);
            brain.WouldTakeFlag(ctx, flag, 1f, out float dangerous);

            Assert.Greater(calm, dangerous);
        }

        // ---------------------------------------------------------------
        // Evaluate — top-level arbitration
        // ---------------------------------------------------------------

        [Test]
        public void Evaluate_NullData_IsIdle()
        {
            var brain = new SpecialistBrain();
            var decision = brain.Evaluate(new SpecialistContext(), new List<FlagHandle>());

            Assert.AreEqual(SpecialistAction.Idle, decision.Action);
            Assert.AreEqual("missing_data", decision.Reason);
        }

        [Test]
        public void Evaluate_BadlyHurt_FleesToSafety()
        {
            var brain = new SpecialistBrain();
            var ctx = MakeContext(MakeSpecialist());
            ctx.HealthNormalized = 0.2f;

            var decision = brain.Evaluate(ctx, new List<FlagHandle>());

            Assert.AreEqual(SpecialistAction.Flee, decision.Action);
            Assert.AreEqual(ctx.SafetyPosition, decision.TargetPosition);
        }

        [Test]
        public void Evaluate_Exhausted_Rests()
        {
            var brain = new SpecialistBrain();
            var ctx = MakeContext(MakeSpecialist());
            ctx.Fatigue = 1f;

            var decision = brain.Evaluate(ctx, new List<FlagHandle>());

            Assert.AreEqual(SpecialistAction.Rest, decision.Action);
        }

        /// <summary>A healthy idle hero always has somewhere to be — it never stands still.</summary>
        [Test]
        public void Evaluate_NoFlags_FallsBackToVocation()
        {
            var brain = new SpecialistBrain();
            var ctx = MakeContext(MakeSpecialist());

            var decision = brain.Evaluate(ctx, new List<FlagHandle>());

            Assert.AreEqual(SpecialistAction.Wander, decision.Action);
            Assert.AreNotEqual(SpecialistAction.Idle, decision.Action);
        }

        [Test]
        public void Evaluate_WellPaidFlag_IsPursued()
        {
            var brain = new SpecialistBrain();
            var ctx = MakeContext(MakeSpecialist());
            var flag = MakeFlag(MakeFlagData(), 90f, new Vector3(10f, 0f, 0f));

            var decision = brain.Evaluate(ctx, new List<FlagHandle> { flag });

            Assert.AreEqual(SpecialistAction.PursueFlag, decision.Action);
            Assert.AreSame(flag, decision.TargetFlag);
        }

        [Test]
        public void Evaluate_UnderpaidFlag_IsNotPursued()
        {
            var brain = new SpecialistBrain();
            var ctx = MakeContext(MakeSpecialist());
            var flag = MakeFlag(MakeFlagData(), 5f, new Vector3(10f, 0f, 0f));

            var decision = brain.Evaluate(ctx, new List<FlagHandle> { flag });

            Assert.AreNotEqual(SpecialistAction.PursueFlag, decision.Action);
        }

        /// <summary>Hysteresis stops heroes flip-flopping between two near-identical flags.</summary>
        [Test]
        public void Evaluate_PrefersCurrentFlagOnATie()
        {
            var brain = new SpecialistBrain();
            var ctx = MakeContext(MakeSpecialist());
            var a = MakeFlag(MakeFlagData(), 90f, new Vector3(10f, 0f, 0f));
            var b = MakeFlag(MakeFlagData(), 90f, new Vector3(-10f, 0f, 0f));
            ctx.CurrentFlag = b;

            var decision = brain.Evaluate(ctx, new List<FlagHandle> { a, b });

            Assert.AreSame(b, decision.TargetFlag);
        }

        /// <summary>The player posts bounties; the brain chooses. It must never crash on junk input.</summary>
        [Test]
        public void Evaluate_ToleratesNullEntriesInFlagList()
        {
            var brain = new SpecialistBrain();
            var ctx = MakeContext(MakeSpecialist());
            var flags = new List<FlagHandle> { null, new FlagHandle(), MakeFlag(MakeFlagData(), 90f, Vector3.right) };

            Assert.DoesNotThrow(() => brain.Evaluate(ctx, flags));
            Assert.DoesNotThrow(() => brain.Evaluate(ctx, null));
        }
    }
}
