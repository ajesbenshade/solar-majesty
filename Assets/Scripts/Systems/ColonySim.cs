// The colony simulation core — pure C#, no MonoBehaviour.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// What the scene contributes to a simulation step: the parts that still live on
    /// MonoBehaviours (village, robots, fauna, visuals). A sim stepped without a host runs the
    /// pure systems alone, which is what tests and replays do.
    /// </summary>
    public interface IColonySimHost
    {
        /// <summary>After the settlement ticks.</summary>
        void TickVillage(float dt);

        void ResearchInputs(out int labs, out int workers, out float rateMultiplier, out float extraRate);

        /// <summary>After research ticks, before guild benefits.</summary>
        void AfterResearch();

        /// <summary>After guild benefits, before orbital cooldowns: roster, flag interest, ecology, threat.</summary>
        void TickWorld(float dt);

        /// <summary>After orbital cooldowns.</summary>
        void TickBoard(float dt);

        /// <summary>Orders that finished on this economy beat. The list is reused; do not hold it.</summary>
        void OnConstructionCompleted(List<ConstructionOrder> completed);

        /// <summary>Called before the economy ticks on a beat; returns the robots drawing upkeep.</summary>
        IReadOnlyList<SpecialistData> PrepareEconomyBeat();

        void AfterEconomyBeat();
    }

    /// <summary>
    /// One colony's simulation state and its fixed step. Owns the clock, the seeded random stream,
    /// the command queue and the pure systems, so a colony can run without a scene: in a test, in
    /// a replay, or as a second overseer in the same process.
    /// </summary>
    public sealed class ColonySim : IOverseerCommandExecutor
    {
        /// <summary>Construction and the economy advance on this beat, not every step.</summary>
        public const float EconomyBeatSeconds = 0.25f;

        /// <summary>Whose colony this is. Commands carry the same id (<see cref="OverseerCommand.overseer"/>).</summary>
        public int OverseerId { get; set; } = OverseerCommand.LocalOverseer;

        public SimClock Clock { get; } = new SimClock();
        public SimRng Rng { get; }
        public EntityIds EntityIds { get; } = new EntityIds();
        public OverseerCommandQueue Commands { get; } = new OverseerCommandQueue();

        public ResourceManager Resources { get; }
        public FlagManager Flags { get; }
        public BuildingPlacer Placer { get; }
        public SimpleEconomy Economy { get; }
        public Settlement Settlement { get; }
        public ResearchManager Research { get; }
        public GuildBenefitDirector GuildBenefits { get; }
        public OrbitalDirector Orbital { get; }

        /// <summary>Build menu, in the order <see cref="OverseerCommandKind.PlaceBuilding"/> indexes it.</summary>
        public IReadOnlyList<BuildingData> BuildCatalog { get; set; }

        /// <summary>Flag palette lookup for <see cref="OverseerCommandKind.PostFlag"/>.</summary>
        public Func<FlagType, FlagData> FlagCatalog { get; set; }

        private readonly List<ConstructionOrder> _completed = new List<ConstructionOrder>(8);
        private float _beat;

        public ColonySim(int seed, EconomyTuning economyTuning = null, OrbitalTuning orbitalTuning = null)
        {
            Rng = new SimRng(seed);
            Resources = new ResourceManager();
            Flags = new FlagManager();
            Placer = new BuildingPlacer(Resources);
            Economy = new SimpleEconomy(Resources, economyTuning);
            Settlement = new Settlement(Resources);
            Research = new ResearchManager(Resources);
            GuildBenefits = new GuildBenefitDirector();
            Orbital = new OrbitalDirector(orbitalTuning ?? new OrbitalTuning());
        }

        /// <summary>
        /// One fixed simulation step. Safe to run several times in a frame (catch-up) and zero
        /// times in a frame (paused or a very fast frame).
        /// </summary>
        public void Step(float dt, IColonySimHost host = null)
        {
            Settlement.Tick(dt);
            host?.TickVillage(dt);

            int labs = 0, workers = 0;
            float rate = 1f, extra = 0f;
            host?.ResearchInputs(out labs, out workers, out rate, out extra);
            Research.Tick(dt, labs, workers, rate, extra);
            host?.AfterResearch();

            GuildBenefits.Tick(dt);
            host?.TickWorld(dt);
            Orbital.Tick(dt);
            host?.TickBoard(dt);

            _beat += dt;
            if (_beat < EconomyBeatSeconds) return;

            _completed.Clear();
            Placer.TickConstruction(_beat, _completed);
            host?.OnConstructionCompleted(_completed);
            Economy.Tick(_beat, host?.PrepareEconomyBeat());
            host?.AfterEconomyBeat();
            _beat = 0f;
        }

        /// <summary>
        /// Run whole steps with no scene: drain the command queue, step, repeat. Commands are
        /// applied by this class, so only the scene-free part of each one takes effect.
        /// </summary>
        public void RunSteps(int steps)
        {
            for (int i = 0; i < steps; i++)
            {
                Commands.Drain(Clock.TotalSteps, this);
                Clock.Restore(Clock.TotalSteps + 1);
                Step(Clock.StepSeconds);
            }
        }

        /// <summary>
        /// Re-run a recorded command log from the current state up to <paramref name="untilTick"/>.
        /// Each command is applied on the tick it was recorded on.
        /// </summary>
        public void Replay(IReadOnlyList<OverseerCommand> log, long untilTick)
        {
            int next = 0;
            while (Clock.TotalSteps < untilTick)
            {
                while (log != null && next < log.Count && log[next].tick <= Clock.TotalSteps)
                    Commands.Enqueue(log[next++]);
                RunSteps(1);
            }
        }

        /// <summary>Return a flag's escrow and take it down. Returns the metals refunded.</summary>
        public int CancelFlag(FlagHandle flag)
        {
            if (flag == null) return 0;
            int refund = flag.EscrowMetals;
            Economy.RefundBountyEscrow(refund);
            Flags.Cancel(flag);
            Economy.MatchReserved(FlagBountySync.Sum(Flags.Flags));
            return refund;
        }

        /// <summary>
        /// The scene-free part of each command: treasury, flags and construction orders. Commands
        /// that act on robots, structures or the clock speed need a scene and do nothing here.
        /// </summary>
        public void Execute(in OverseerCommand command)
        {
            switch (command.kind)
            {
                case OverseerCommandKind.PlaceBuilding:
                    BuildingData building = BuildCatalog != null && command.content >= 0 &&
                                            command.content < BuildCatalog.Count
                        ? BuildCatalog[command.content]
                        : null;
                    if (building != null)
                        Placer.TryPlace(building, command.Cell, command.World, out _, out _);
                    break;
                case OverseerCommandKind.PostFlag:
                    FlagData flagData = FlagCatalog?.Invoke((FlagType)command.content);
                    if (flagData == null) break;
                    FlagHandle posted = FlagBountySync.TryPost(Flags, Economy, flagData, command.World, command.amount);
                    if (posted != null && !string.IsNullOrWhiteSpace(command.text))
                        posted.Orders = FlagOrdersParser.Parse(command.text);
                    break;
                case OverseerCommandKind.CancelFlag:
                    if (Flags.TryGet(command.target, out FlagHandle cancelled))
                        CancelFlag(cancelled);
                    break;
                case OverseerCommandKind.StepBounty:
                    if (Flags.TryGet(command.target, out FlagHandle stepped))
                        FlagClick.TryStepBounty(Flags, Economy, stepped, command.amount);
                    break;
            }
        }

        /// <summary>
        /// A fingerprint of the scene-free state. Two sims that agree on it agree on the clock, the
        /// random stream, the stockpile, every flag and every construction order.
        /// </summary>
        public ulong StateHash()
        {
            ulong h = 14695981039346656037UL; // FNV-1a
            Mix(ref h, Clock.TotalSteps);
            Mix(ref h, Rng.State);
            Mix(ref h, EntityIds.NextId);
            Mix(ref h, Resources.Get(ResourceId.Regolith));
            Mix(ref h, Resources.Get(ResourceId.WaterIce));
            Mix(ref h, Resources.Get(ResourceId.Metals));
            Mix(ref h, Resources.Get(ResourceId.Power));
            Mix(ref h, Economy.EscrowedMetals);

            IReadOnlyList<FlagHandle> flags = Flags.Flags;
            Mix(ref h, flags.Count);
            for (int i = 0; i < flags.Count; i++)
            {
                FlagHandle f = flags[i];
                Mix(ref h, f.RuntimeId is int id ? id : 0);
                Mix(ref h, f.Data != null ? (int)f.Data.flagType : -1);
                Mix(ref h, f.WorldPosition.x);
                Mix(ref h, f.WorldPosition.z);
                Mix(ref h, f.CurrentBounty);
                Mix(ref h, f.EscrowMetals);
                Mix(ref h, Flags.GetWorkRemaining(f));
            }

            IReadOnlyList<ConstructionOrder> orders = Placer.Orders;
            Mix(ref h, orders.Count);
            for (int i = 0; i < orders.Count; i++)
            {
                ConstructionOrder o = orders[i];
                Mix(ref h, o.Id);
                Mix(ref h, o.GridCell.x);
                Mix(ref h, o.GridCell.y);
                Mix(ref h, o.ProgressSeconds);
            }
            Mix(ref h, Placer.Pieces.Count);
            return h;
        }

        private static void Mix(ref ulong h, long value)
        {
            for (int i = 0; i < 8; i++)
            {
                h ^= (byte)(value >> (i * 8));
                h *= 1099511628211UL;
            }
        }

        private static void Mix(ref ulong h, float value) => Mix(ref h, BitConverter.SingleToInt32Bits(value));
    }
}
