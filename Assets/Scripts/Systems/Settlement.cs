using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Colony census for the energy economy: which buildings stand, the Majesty day clock that
    /// pays their daily energy into tills, and the treasury goal the sustain objective measures.
    /// All currency is energy units. Mines pay a daily load by distance (see VillageExpansion).
    /// </summary>
    public sealed class Settlement
    {
        public int CommonsCount { get; private set; }
        public int PadCount { get; private set; }
        public int CoreHabs { get; set; }
        public int VillageHabs { get; private set; }
        public int Farms { get; private set; }
        public int Mines { get; private set; }
        public int SolarFarms { get; private set; }
        /// <summary>House tax the HABs pay per day (paid into each HAB's till).</summary>
        public int LastTax { get; private set; }
        /// <summary>Last collector deposit into the treasury.</summary>
        public int LastLevyDeposited { get; private set; }
        public int LastDelivered => LastLevyDeposited;
        /// <summary>Last till or bag stolen by a pest.</summary>
        public int LastLevyStolen { get; private set; }
        public string LastProductionLine { get; private set; } = "";
        public bool HasOutpost { get; private set; }
        public int GuildCount { get; private set; }

        public bool HasCommons => CommonsCount > 0;
        public bool HasGuild => GuildCount > 0;
        public bool HasPad => PadCount > 0;
        public bool EverHadHab { get; private set; }
        public int Habs => CoreHabs + VillageHabs;

        // ---------------------------------------------------------------- worker drones
        // The economy's worker drones (not the heroes) live at the charging stations. These are
        // the pre-gold-economy population rules, renamed: 3 berths a station, a starter crew of 2
        // with the first station, one more drone every 18 s while a berth is free, and drones
        // that lose their berth (a station destroyed) are lost. Gene Vault adds 3 berths and,
        // with the Climate Loom, shortens the interval. Homesteader reads the peak.

        /// <summary>Drones one charging station houses (the old HAB bed count).</summary>
        public const int DronesPerCharger = 3;
        /// <summary>Drones that arrive with the first charging station.</summary>
        public const int StarterDrones = 2;
        /// <summary>Seconds between drones while a berth is free (the old birth interval).</summary>
        public const float BaseDroneInterval = 18f;

        /// <summary>Worker drones housed at the charging stations.</summary>
        public int Drones { get; private set; }
        /// <summary>Extra berths from research (Gene Vault).</summary>
        public int BonusBerths { get; set; }
        /// <summary>Research scale on <see cref="BaseDroneInterval"/> (Gene Vault, Climate Loom).</summary>
        public float DroneIntervalScale { get; set; } = 1f;
        public int DroneCapacity => DroneCapacityFor(Habs, BonusBerths);
        public int FreeBerths => Mathf.Max(0, DroneCapacity - Drones);
        public float DroneInterval => BaseDroneInterval * Mathf.Clamp(DroneIntervalScale, 0.25f, 2f);
        /// <summary>Seconds until the next drone, while a berth is free.</summary>
        public float DroneTimer => _droneTimer;
        public bool CanAddDrone => Drones > 0 && Drones < DroneCapacity;

        public static int DroneCapacityFor(int chargers, int bonusBerths = 0) =>
            chargers <= 0 ? 0 : chargers * DronesPerCharger + Mathf.Max(0, bonusBerths);

        /// <summary>A save's drone count, clamped to the berths standing; a charged yard with none gets the starter crew.</summary>
        public void RestoreDrones(int drones, float timer = 0f)
        {
            Drones = Mathf.Clamp(drones, 0, DroneCapacity);
            if (Drones <= 0) SeedStarterDrones();
            _droneTimer = timer > 0f ? Mathf.Min(timer, DroneInterval) : DroneInterval;
        }

        private void SeedStarterDrones()
        {
            if (Drones > 0 || Habs <= 0) return;
            Drones = Mathf.Min(StarterDrones, DroneCapacity);
        }

        private void ClampDrones()
        {
            if (Drones > DroneCapacity) Drones = DroneCapacity;
        }

        private void TickDrones(float dt)
        {
            if (!CanAddDrone)
            {
                _droneTimer = DroneInterval;
                return;
            }
            _droneTimer -= dt;
            if (_droneTimer > 0f) return;
            _droneTimer += DroneInterval;
            if (_droneTimer <= 0f) _droneTimer = DroneInterval;
            Drones++;
        }

        private float _droneTimer = BaseDroneInterval;

        /// <summary>Sustain objective: treasury to hold and income to keep (set per body).</summary>
        public int TreasuryGoal { get; private set; } = 5000;
        public int IncomeGoalPerMin { get; private set; } = OverseerRules.SustainMetPerMin;
        public float NetMetalsPerMin { get; private set; }

        public int Treasury => _resources != null ? _resources.Get(ResourceId.Metals) : 0;

        public bool IsSustainable =>
            HasCommons &&
            Treasury >= TreasuryGoal &&
            NetMetalsPerMin >= IncomeGoalPerMin;

        public string SustainHint
        {
            get
            {
                if (!HasCommons)
                    return Loc.S("raise Colony Commons first");
                if (Treasury < TreasuryGoal)
                    return Loc.F("grow the treasury to {0:N0} EU (now {1:N0})", TreasuryGoal, Treasury);
                if (NetMetalsPerMin < IncomeGoalPerMin)
                    return Loc.F("keep income at {0:N0}+ EU/min — Haul, mines, till share", IncomeGoalPerMin);
                return Loc.S("holding — keep the treasury and income up");
            }
        }

        private float _taxTimer;
        private float _prodTimer;
        private readonly ResourceManager _resources;

        /// <summary>One Majesty day: every building pays its daily tax into its till.</summary>
        public float TaxInterval { get; set; } = MajestyEconomy.DaySeconds;
        public float ProductionInterval { get; set; } = 8f;
        public float MineYieldScale { get; set; } = 1f;

        private float _bodyMine = 1f;
        private float _techMine;

        public Settlement(ResourceManager resources)
        {
            _resources = resources;
            _taxTimer = TaxInterval;
            _prodTimer = ProductionInterval;
        }

        public void SetTreasuryGoal(int treasury, int incomePerMin)
        {
            TreasuryGoal = Mathf.Max(0, treasury);
            IncomeGoalPerMin = Mathf.Max(0, incomePerMin);
        }

        public void SetBodyYield(float mineScale)
        {
            _bodyMine = Mathf.Clamp(mineScale, 0.1f, 3f);
            RefreshYield();
        }

        public void SetTechYieldBonus(float mineBonus)
        {
            _techMine = Mathf.Max(0f, mineBonus);
            RefreshYield();
        }

        public void SetIncomeRate(float metPerMin) => NetMetalsPerMin = metPerMin;

        /// <summary>Terraform work on farms raises their daily tax (capped at +60%).</summary>
        public float FarmTaxScale { get; private set; } = 1f;

        public void AddTerraformPulse(float amount = 0.08f) =>
            FarmTaxScale = Mathf.Min(1.6f, FarmTaxScale + Mathf.Max(0f, amount));

        private void RefreshYield()
        {
            MineYieldScale = Mathf.Clamp(_bodyMine * (1f + _techMine), 0.1f, 3.5f);
        }

        public void Tick(float dt)
        {
            if (dt <= 0f || _resources == null) return;

            TickDrones(dt);

            _taxTimer -= dt;
            if (_taxTimer <= 0f)
            {
                _taxTimer += TaxInterval;
                PendingDays++;
                LastTax = Habs * MajestyEconomy.DailyTax(BuildingCategory.Habitat);
            }
        }

        public void RegisterPlaced(BuildingCategory cat)
        {
            switch (cat)
            {
                case BuildingCategory.Commons: CommonsCount++; break;
                case BuildingCategory.LandingPad: PadCount++; break;
                case BuildingCategory.Farm: Farms++; break;
                case BuildingCategory.Mine: Mines++; break;
                case BuildingCategory.Power: SolarFarms++; break;
                case BuildingCategory.Habitat: CoreHabs++; EverHadHab = true; SeedStarterDrones(); break;
                case BuildingCategory.GuildHall: GuildCount++; break;
            }
        }

        public void Unregister(BuildingCategory cat, bool villageHab = false)
        {
            switch (cat)
            {
                case BuildingCategory.Commons:
                    CommonsCount = Mathf.Max(0, CommonsCount - 1);
                    break;
                case BuildingCategory.LandingPad:
                    PadCount = Mathf.Max(0, PadCount - 1);
                    break;
                case BuildingCategory.Farm:
                    Farms = Mathf.Max(0, Farms - 1);
                    break;
                case BuildingCategory.Mine:
                    Mines = Mathf.Max(0, Mines - 1);
                    break;
                case BuildingCategory.Power:
                    SolarFarms = Mathf.Max(0, SolarFarms - 1);
                    break;
                case BuildingCategory.Habitat:
                    if (villageHab)
                        VillageHabs = Mathf.Max(0, VillageHabs - 1);
                    else
                        CoreHabs = Mathf.Max(0, CoreHabs - 1);
                    break;
                case BuildingCategory.GuildHall:
                    GuildCount = Mathf.Max(0, GuildCount - 1);
                    break;
            }
            ClampDrones();
        }

        public void ClaimOutpost() => HasOutpost = true;

        public void AddVillageHab()
        {
            VillageHabs++;
            EverHadHab = true;
            SeedStarterDrones();
        }

        public void LoseVillageHab()
        {
            VillageHabs = Mathf.Max(0, VillageHabs - 1);
            ClampDrones();
        }

        /// <summary>Majesty days elapsed since the runtime last paid civic daily tax into tills.</summary>
        public int PendingDays { get; private set; }

        public int TakePendingDays()
        {
            int n = PendingDays;
            PendingDays = 0;
            return n;
        }

        /// <summary>When true, mine gold waits in the Mine's till for a collector instead of teleporting.</summary>
        public bool RouteCampGoldToTills { get; set; }

        public int PendingCampGold { get; private set; }

        public int TakePendingCampGold()
        {
            int n = PendingCampGold;
            PendingCampGold = 0;
            return n;
        }

        public void NoteLevyDelivered(int amount) => NoteLevyDeposited(amount);

        public void NoteLevyDeposited(int amount)
        {
            LastLevyDeposited = Mathf.Max(0, amount);
            if (amount > 0)
                _resources?.Add(ResourceId.Metals, amount);
        }

        public void NoteLevyStolen(int amount)
        {
            LastLevyStolen = Mathf.Max(0, amount);
        }
    }
}
