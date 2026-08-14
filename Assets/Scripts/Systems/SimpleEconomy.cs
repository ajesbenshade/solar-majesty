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
                DeliverResupply();
                ResupplyArrived?.Invoke();
            }
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
        /// </summary>
        public void GrantExtractYield(int campusIndex, ResourceNode node)
        {
            if (node != null && !node.IsDepleted)
            {
                switch (node.NodeType)
                {
                    case ResourceNodeType.Metals:
                    {
                        int took = node.Harvest(8);
                        if (took > 0) _resources.Add(ResourceId.Metals, took);
                        _resources.Add(ResourceId.Regolith, 2);
                        RecordExtract($"+{took} MET from ore node", took);
                        break;
                    }
                    case ResourceNodeType.Ice:
                    {
                        int took = node.Harvest(7);
                        if (took > 0) _resources.Add(ResourceId.WaterIce, took);
                        _resources.Add(ResourceId.Regolith, 3);
                        RecordExtract($"+{took} ICE from ice node", took);
                        break;
                    }
                    case ResourceNodeType.Fissile:
                    {
                        int took = node.Harvest(5);
                        if (took > 0) _resources.Add(ResourceId.Power, took);
                        _resources.Add(ResourceId.Metals, 1);
                        RecordExtract($"+{took} PWR from fissile node", took);
                        break;
                    }
                    default:
                    {
                        int took = node.Harvest(10);
                        if (took > 0) _resources.Add(ResourceId.Regolith, took);
                        _resources.Add(ResourceId.Metals, 2);
                        RecordExtract($"+{took} REG from deposit", took);
                        break;
                    }
                }
                return;
            }

            if (campusIndex <= 0)
            {
                _resources.Add(ResourceId.Regolith, 12);
                _resources.Add(ResourceId.Metals, 4);
                _resources.Add(ResourceId.WaterIce, 2);
                RecordExtract("+12 REG campus extract", 12);
            }
            else
            {
                _resources.Add(ResourceId.Regolith, 16);
                _resources.Add(ResourceId.Metals, 3);
                _resources.Add(ResourceId.WaterIce, 1);
                RecordExtract("+16 REG outpost extract", 16);
            }
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

            LastUpkeepLine = PowerGen > 0
                ? $"grid +{PowerGen}/−{spentPower} PWR"
                : $"upkeep −{spentPower} PWR";
            if (spentMet > 0) LastUpkeepLine += $" −{spentMet} MET";
            if (spentIce > 0) LastUpkeepLine += $" −{spentIce} ICE";
        }

        private void DeliverResupply()
        {
            if (ResupplyPackage == null) return;

            for (int i = 0; i < ResupplyPackage.Length; i++)
            {
                ResourceAmount p = ResupplyPackage[i];
                if (p.amount > 0)
                    _resources.Add(p.resource, p.amount);
            }
        }
    }
}
