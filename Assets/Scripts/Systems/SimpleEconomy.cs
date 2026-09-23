// Timed economy layer: bounty escrow, the upkeep clock and periodic trade-ship landings.
// Pure C#: call Tick(deltaTime) from a future simulation driver.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Thin timer-driven economy on top of ResourceManager.
    /// Does not know about scene objects — pass specialist definitions for upkeep.
    /// </summary>
    public sealed class SimpleEconomy
    {
        private readonly ResourceManager _resources;

        private float _upkeepTimer;
        private float _resupplyTimer;

        public float UpkeepIntervalSeconds { get; set; } = 30f;
        public float ResupplyIntervalSeconds { get; set; } = 90f;
        public bool ResupplyEnabled { get; set; } = true;
        public bool ResupplyRequiresPad { get; set; } = true;
        public bool HasDock { get; set; }
        public int ResupplyDockFee { get; set; }

        /// <summary>Caravan gold per landing (runtime sets it from the pad → market distance).</summary>
        public int CaravanGold { get; set; } = MajestyEconomy.CaravanGoldBase;

        /// <summary>
        /// Where trade gold lands. Runtime points this at the Market (or Commons) till so a tax
        /// collector has to walk it home; returns false to fall back to the stockpile.
        /// </summary>
        public Func<int, bool> TillSink { get; set; }

        public int LastCaravanGold { get; private set; }

        private void PayTrade(int gold)
        {
            if (gold <= 0) return;
            if (TillSink != null && TillSink(gold)) return;
            _resources.Add(ResourceId.Metals, gold);
        }

        public event Action UpkeepApplied;
        public event Action ResupplyArrived;
        public event Action ResupplyWavedOff;

        /// <summary>Seconds until the next specialist/grid upkeep tick.</summary>
        public float UpkeepSecondsLeft => Mathf.Max(0f, _upkeepTimer);

        /// <summary>Seconds until the next Earth resupply package.</summary>
        public float ResupplySecondsLeft => Mathf.Max(0f, _resupplyTimer);

        public string LastUpkeepLine { get; private set; } = "";
        public string LastExtractLine { get; private set; } = "";
        public int LastExtractAmount { get; private set; }
        public string LastResupplyLine { get; private set; } = "";
        public bool LastResupplyDocked { get; private set; }
        public int LastMetalsUpkeep { get; private set; }

        public SimpleEconomy(ResourceManager resources)
        {
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
            _upkeepTimer = UpkeepIntervalSeconds;
            _resupplyTimer = ResupplyIntervalSeconds;
        }

        /// <summary>
        /// Advance timers. Pass currently living specialist definitions for upkeep.
        /// </summary>
        public void Tick(float deltaTime, IReadOnlyList<SpecialistData> livingSpecialists = null)
        {
            if (deltaTime <= 0f) return;

            _upkeepTimer -= deltaTime;
            if (_upkeepTimer <= 0f)
            {
                _upkeepTimer += UpkeepIntervalSeconds;
                UpkeepApplied?.Invoke();
            }

            if (!ResupplyEnabled) return;

            _resupplyTimer -= deltaTime;
            if (_resupplyTimer <= 0f)
            {
                _resupplyTimer += ResupplyIntervalSeconds;
                if (DeliverResupply())
                    ResupplyArrived?.Invoke();
                else
                    ResupplyWavedOff?.Invoke();
            }
        }

        public void ConfigureResupply(float intervalSeconds, int dockFee)
        {
            ResupplyIntervalSeconds = Mathf.Max(20f, intervalSeconds);
            ResupplyDockFee = Mathf.Max(0, dockFee);
            _resupplyTimer = ResupplyIntervalSeconds;
        }

        /// <summary>Live rule change without resetting the incoming ship clock unless it overshoots.</summary>
        public void SetResupplyRules(float intervalSeconds, int dockFee)
        {
            ResupplyIntervalSeconds = Mathf.Max(20f, intervalSeconds);
            ResupplyDockFee = Mathf.Max(0, dockFee);
            if (_resupplyTimer > ResupplyIntervalSeconds)
                _resupplyTimer = ResupplyIntervalSeconds;
        }

        public int EscrowedMetals { get; private set; }

        /// <summary>Metals withdrawn from the stockpile to post a bounty flag.</summary>
        public static int BountyMetalsCost(float bounty) =>
            Mathf.Max(1, Mathf.RoundToInt(bounty));

        /// <summary>True if the colony can escrow this bounty in metals.</summary>
        public bool CanAffordBounty(float bounty) =>
            _resources != null && _resources.Get(ResourceId.Metals) >= BountyMetalsCost(bounty);

        /// <summary>
        /// Escrow metals from the stockpile. Heroes are paid in personal credits on complete —
        /// the colony does not get those metals back.
        /// </summary>
        public bool TryEscrowBounty(float bounty, out int metals)
        {
            metals = BountyMetalsCost(bounty);
            if (_resources == null) return false;
            if (!_resources.TrySpend(ResourceId.Metals, metals)) return false;
            EscrowedMetals += metals;
            return true;
        }

        public void RefundBountyEscrow(int metals)
        {
            if (metals <= 0 || _resources == null) return;
            EscrowedMetals = Mathf.Max(0, EscrowedMetals - metals);
            _resources.Add(ResourceId.Metals, metals);
        }

        /// <summary>Flag completed — metals stay spent, reserved readout clears.</summary>
        public void ReleaseBountyEscrow(int metals)
        {
            if (metals <= 0) return;
            EscrowedMetals = Mathf.Max(0, EscrowedMetals - metals);
        }

        /// <summary>Personal MET a specialist keeps from an extract (colony haul is separate).</summary>
        public static int PersonalExtractMetals(IHarvestable node)
        {
            int purse = OverseerRules.ExtractPurseMet;
            if (node != null && node.NodeType == ResourceNodeType.Metals)
                purse += OverseerRules.ExtractPurseMetBonus;
            return purse;
        }

        /// <summary>Fobot Yard / re-fab — credits (MET) only. ICE argument is ignored.</summary>
        public bool CanAffordRevive(int metals, int ice = 0)
        {
            _ = ice;
            if (_resources == null) return false;
            return _resources.Get(ResourceId.Metals) >= metals;
        }

        public bool TrySpendRevive(int metals, int ice = 0)
        {
            _ = ice;
            if (!CanAffordRevive(metals)) return false;
            if (metals > 0 && !_resources.TrySpend(ResourceId.Metals, metals)) return false;
            return true;
        }

        /// <summary>Marketplace tax preview — matches agent CollectTithe.</summary>
        public static int TitheFromPurse(float credits)
        {
            if (credits <= OverseerRules.TitheFloor) return 0;
            return Mathf.Min(OverseerRules.TitheCap, Mathf.FloorToInt(credits * OverseerRules.TitheRate));
        }

        /// <summary>Live re-price: spend or refund the metals delta for a posted flag.</summary>
        public bool TryAdjustBountyEscrow(FlagHandle flag, float newBounty)
        {
            if (flag == null || _resources == null) return false;
            int want = BountyMetalsCost(newBounty);
            int have = flag.EscrowMetals;
            int delta = want - have;
            if (delta > 0)
            {
                if (!_resources.TrySpend(ResourceId.Metals, delta)) return false;
                EscrowedMetals += delta;
            }
            else if (delta < 0)
            {
                RefundBountyEscrow(-delta);
            }

            flag.EscrowMetals = want;
            return true;
        }

        /// <summary>Phase 4B: Extract flags yield regolith + a bit of metals beyond bounty pay.</summary>
        public void GrantExtractYield() => GrantExtractYield(0);

        /// <summary>
        /// Phase 5D: shared stockpile, campus-framed yield.
        /// Campus A (pad) — balanced package; Campus B outpost — more regolith, leaner metals/ice.
        /// </summary>
        public void GrantExtractYield(int campusIndex)
        {
            GrantExtractYield(campusIndex, null);
        }

        /// <summary>
        /// Phase 6A: if a resource node is in range, harvest from it; otherwise campus fallback.
        /// Efficiency is haul delivered to stockpile (node still loses the full take).
        /// </summary>
        public void GrantExtractYield(int campusIndex, IHarvestable node) =>
            GrantExtractYield(campusIndex, node, 1f, null);

        public void GrantExtractYield(int campusIndex, IHarvestable node, float efficiency, string via)
        {
            float haul = Mathf.Clamp(efficiency, 0.05f, 1.25f);
            string tag = HaulTag(via, haul);

            // Every deposit is worth CRED: ore pays best, ice and rock fields pay less.
            if (node != null && !node.IsDepleted)
            {
                int ore = node.NodeType switch
                {
                    ResourceNodeType.Metals => node.Harvest(8),
                    ResourceNodeType.Ice => node.Harvest(7) * 3 / 4,
                    ResourceNodeType.Fissile => node.Harvest(5),
                    _ => node.Harvest(10) / 2
                };
                int got = Deliver(ResourceId.Metals, Gold(ore), haul);
                RecordExtract($"+{got} CRED {tag}", got);
                return;
            }

            int fallback = Deliver(ResourceId.Metals, Gold(campusIndex <= 0 ? 4 : 3), haul);
            RecordExtract($"+{fallback} CRED {(campusIndex <= 0 ? "campus" : "outpost")} {tag}", fallback);
        }

        /// <summary>Ore units → CRED (one ore chunk is worth <see cref="MajestyEconomy.GoldScale"/>).</summary>
        private static int Gold(int oreUnits) => Mathf.RoundToInt(oreUnits * MajestyEconomy.GoldScale);

        private int Deliver(ResourceId id, int amount, float efficiency)
        {
            int n = Mathf.Max(0, Mathf.RoundToInt(amount * efficiency));
            if (n > 0) _resources.Add(id, n);
            return n;
        }

        private static string HaulTag(string via, float efficiency)
        {
            int pct = Mathf.RoundToInt(efficiency * 100f);
            if (string.IsNullOrEmpty(via))
                return $"loose haul · {pct}%";
            return $"via {via} · {pct}%";
        }

        private void RecordExtract(string line, int amount)
        {
            LastExtractLine = line;
            LastExtractAmount = amount;
        }

        private bool DeliverResupply()
        {
            LastResupplyDocked = false;
            if (ResupplyRequiresPad && !HasDock)
            {
                LastResupplyLine = "Earth ship waved off — no Landing Pad";
                return false;
            }

            if (ResupplyDockFee > 0)
                _resources.SpendUpTo(ResourceId.Metals, ResupplyDockFee);

            LastCaravanGold = Mathf.Max(0, CaravanGold);
            PayTrade(LastCaravanGold);

            LastResupplyDocked = true;
            LastResupplyLine = ResupplyDockFee > 0
                ? $"Ship landed — caravan worth {LastCaravanGold} CRED to the Market till. Dock fee {ResupplyDockFee} CRED already left."
                : $"Ship landed — caravan worth {LastCaravanGold} CRED to the Market till.";
            return true;
        }
    }
}
