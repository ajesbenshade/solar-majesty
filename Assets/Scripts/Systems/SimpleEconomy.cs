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

        /// <summary>Convert a completed flag bounty into stockpile reward (Phase-0: Metals).</summary>
        public void GrantBountyReward(float bounty)
        {
            int metals = Mathf.Max(1, Mathf.RoundToInt(bounty / 5f));
            _resources.Add(ResourceId.Metals, metals);
        }

        /// <summary>Phase 4B: Extract flags yield regolith + a bit of metals beyond bounty pay.</summary>
        public void GrantExtractYield()
        {
            _resources.Add(ResourceId.Regolith, 12);
            _resources.Add(ResourceId.Metals, 4);
            _resources.Add(ResourceId.WaterIce, 2);
        }

        private void ApplyUpkeep(IReadOnlyList<SpecialistData> livingSpecialists)
        {
            if (BasePowerUpkeep > 0)
                _resources.SpendUpTo(ResourceId.Power, BasePowerUpkeep);

            if (livingSpecialists == null) return;

            float scale = UpkeepIntervalSeconds / 60f; // upkeepPerMinute → this interval

            for (int s = 0; s < livingSpecialists.Count; s++)
            {
                SpecialistData data = livingSpecialists[s];
                if (data == null || data.upkeepPerMinute == null) continue;

                for (int i = 0; i < data.upkeepPerMinute.Length; i++)
                {
                    ResourceAmount c = data.upkeepPerMinute[i];
                    int amt = Mathf.Max(0, Mathf.RoundToInt(c.amount * scale));
                    if (amt > 0)
                        _resources.SpendUpTo(c.resource, amt);
                }
            }
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
