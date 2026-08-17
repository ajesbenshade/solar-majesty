// Timed economy layer: specialist upkeep + periodic Earth Starship resupply.
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

        /// <summary>Flat power drain each upkeep tick (base outpost draw).</summary>
        public int BasePowerUpkeep { get; set; } = 1;

        /// <summary>Package delivered from Earth each resupply.</summary>
        public ResourceAmount[] ResupplyPackage { get; set; } =
        {
            new ResourceAmount(ResourceId.Metals, 25),
            new ResourceAmount(ResourceId.WaterIce, 15),
            new ResourceAmount(ResourceId.Power, 10)
        };

        public event Action UpkeepApplied;
        public event Action ResupplyArrived;
        public event Action ResupplyWavedOff;

        /// <summary>Seconds until the next specialist/grid upkeep tick.</summary>
        public float UpkeepSecondsLeft => Mathf.Max(0f, _upkeepTimer);

        /// <summary>Seconds until the next Earth resupply package.</summary>
        public float ResupplySecondsLeft => Mathf.Max(0f, _resupplyTimer);

        /// <summary>Power generated each upkeep interval (from Power Nodes / arrays).</summary>
        public int PowerGen { get; set; }

        /// <summary>Power consumed each upkeep interval (modules + robots + base draw).</summary>
        public int PowerDraw { get; set; }

        public bool PowerShort =>
            PowerDraw > PowerGen && _resources.Get(ResourceId.Power) < 8;

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
                ApplyUpkeep(livingSpecialists);
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
        public void GrantExtractYield(int campusIndex, ResourceNode node) =>
            GrantExtractYield(campusIndex, node, 1f, null);

        public void GrantExtractYield(int campusIndex, ResourceNode node, float efficiency, string via)
        {
            float haul = Mathf.Clamp(efficiency, 0.05f, 1.25f);
            string tag = HaulTag(via, haul);

            if (node != null && !node.IsDepleted)
            {
                switch (node.NodeType)
                {
                    case ResourceNodeType.Metals:
                    {
                        int took = node.Harvest(8);
                        int got = Deliver(ResourceId.Metals, took, haul);
                        Deliver(ResourceId.Regolith, 2, haul);
                        RecordExtract($"+{got} MET {tag}", got);
                        break;
                    }
                    case ResourceNodeType.Ice:
                    {
                        int took = node.Harvest(7);
                        int got = Deliver(ResourceId.WaterIce, took, haul);
                        Deliver(ResourceId.Regolith, 3, haul);
                        RecordExtract($"+{got} ICE {tag}", got);
                        break;
                    }
                    case ResourceNodeType.Fissile:
                    {
                        int took = node.Harvest(5);
                        int got = Deliver(ResourceId.Power, took, haul);
                        Deliver(ResourceId.Metals, 1, haul);
                        RecordExtract($"+{got} PWR {tag}", got);
                        break;
                    }
                    default:
                    {
                        int took = node.Harvest(10);
                        int got = Deliver(ResourceId.Regolith, took, haul);
                        Deliver(ResourceId.Metals, 2, haul);
                        RecordExtract($"+{got} REG {tag}", got);
                        break;
                    }
                }
                return;
            }

            if (campusIndex <= 0)
            {
                int got = Deliver(ResourceId.Regolith, 12, haul);
                Deliver(ResourceId.Metals, 4, haul);
                Deliver(ResourceId.WaterIce, 2, haul);
                RecordExtract($"+{got} REG campus {tag}", got);
            }
            else
            {
                int got = Deliver(ResourceId.Regolith, 16, haul);
                Deliver(ResourceId.Metals, 3, haul);
                Deliver(ResourceId.WaterIce, 1, haul);
                RecordExtract($"+{got} REG outpost {tag}", got);
            }
        }

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

        private void ApplyUpkeep(IReadOnlyList<SpecialistData> livingSpecialists)
        {
            if (PowerGen > 0)
                _resources.Add(ResourceId.Power, PowerGen);

            int gridDraw = Mathf.Max(0, PowerDraw) + Mathf.Max(0, BasePowerUpkeep);
            int spentPower = gridDraw > 0
                ? _resources.SpendUpTo(ResourceId.Power, gridDraw)
                : 0;

            int spentMet = 0;
            int spentIce = 0;
            if (livingSpecialists != null)
            {
                float scale = UpkeepIntervalSeconds / 60f; // upkeepPerMinute → this interval

                for (int s = 0; s < livingSpecialists.Count; s++)
                {
                    SpecialistData data = livingSpecialists[s];
                    if (data == null || data.upkeepPerMinute == null) continue;

                    for (int i = 0; i < data.upkeepPerMinute.Length; i++)
                    {
                        ResourceAmount c = data.upkeepPerMinute[i];
                        int amt = Mathf.Max(0, Mathf.RoundToInt(c.amount * scale));
                        if (amt <= 0) continue;
                        int paid = _resources.SpendUpTo(c.resource, amt);
                        if (c.resource == ResourceId.Power) spentPower += paid;
                        else if (c.resource == ResourceId.Metals) spentMet += paid;
                        else if (c.resource == ResourceId.WaterIce) spentIce += paid;
                    }
                }
            }

            LastMetalsUpkeep = spentMet;
            LastUpkeepLine = PowerGen > 0
                ? $"grid +{PowerGen}/−{spentPower} PWR"
                : $"upkeep −{spentPower} PWR";
            if (spentMet > 0) LastUpkeepLine += $" −{spentMet} MET";
            if (spentIce > 0) LastUpkeepLine += $" −{spentIce} ICE";
        }

        private bool DeliverResupply()
        {
            LastResupplyDocked = false;
            if (ResupplyPackage == null)
            {
                LastResupplyLine = "Earth ship empty";
                return false;
            }

            if (ResupplyRequiresPad && !HasDock)
            {
                LastResupplyLine = "Earth ship waved off — no Landing Pad";
                return false;
            }

            if (ResupplyDockFee > 0)
                _resources.SpendUpTo(ResourceId.Metals, ResupplyDockFee);

            for (int i = 0; i < ResupplyPackage.Length; i++)
            {
                ResourceAmount p = ResupplyPackage[i];
                if (p.amount > 0)
                    _resources.Add(p.resource, p.amount);
            }

            LastResupplyDocked = true;
            LastResupplyLine = ResupplyDockFee > 0
                ? $"Earth resupply docked (−{ResupplyDockFee} MET fee)"
                : "Earth resupply docked";
            return true;
        }
    }
}
