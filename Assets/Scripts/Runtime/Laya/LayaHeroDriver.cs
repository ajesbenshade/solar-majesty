using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Per-specialist glue between <see cref="SpecialistBrain"/> and <see cref="LayaBridge"/>.
    /// Each think tick: collect the options the utility gates allow, apply the latest Laya answer
    /// if its pick is still legal, and send a fresh question. Answers land one tick late
    /// (~0.5 s), which suits Majesty pacing and keeps inference off the frame.
    /// </summary>
    public sealed class LayaHeroDriver
    {
        /// <summary>A held Laya pick expires after this, so a stale choice cannot pin a hero.</summary>
        public float HoldSeconds = 4f;

        /// <summary>True when the last <see cref="Decide"/> returned Laya's pick, not the utility pick.</summary>
        public bool LastFromLaya { get; private set; }

        readonly List<BrainDecision> _now = new List<BrainDecision>(8);
        List<BrainDecision> _sent;
        List<LayaOption> _sentOptions;
        LayaTicket _ticket;
        BrainDecision _held;
        bool _hasHeld;
        float _heldUntil;

        public BrainDecision Decide(
            SpecialistBrain brain, in SpecialistContext ctx, IReadOnlyList<FlagHandle> flags, float bodyDanger)
        {
            LastFromLaya = false;
            var bridge = LayaBridge.Instance;
            bool choosable = brain.CollectOptions(ctx, flags, bodyDanger, _now);
            BrainDecision utility = _now[0];
            if (bridge == null || !choosable)
            {
                _ticket = null;
                _hasHeld = false;
                return utility;
            }

            if (_ticket != null && _ticket.Done)
            {
                int idx = LayaHeroPolicy.Resolve(_sentOptions, _ticket.Answer, bridge.minProbability);
                _ticket = null;
                _hasHeld = false;
                if (idx > 0) // 0 is the utility pick itself: nothing to override
                {
                    _held = _sent[idx];
                    _hasHeld = true;
                    _heldUntil = Time.time + HoldSeconds;
                    bridge.Overrides++;
                }
            }

            if (_ticket == null && bridge.CanAsk)
            {
                _sent = new List<BrainDecision>(_now);
                _sentOptions = LayaHeroPolicy.BuildOptions(ctx, _sent);
                string body = LayaProtocol.BuildChoiceRequest(
                    LayaHeroPolicy.BuildState(ctx), LayaHeroPolicy.Instructions, _sentOptions);
                _ticket = bridge.Ask(body);
            }

            if (_hasHeld && Time.time < _heldUntil)
            {
                int m = LayaHeroPolicy.Match(_now, _held);
                if (m >= 0)
                {
                    LastFromLaya = true;
                    return _now[m]; // fresh score/target from this tick
                }
            }
            _hasHeld = false;
            return utility;
        }
    }
}
