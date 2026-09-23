using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Colony census for a gold-only economy: which buildings stand, the Majesty day clock that
    /// pays their daily tax into tills, mine gold waiting for a collector, and the treasury goal
    /// the sustain objective is measured against. There is no population, ICE, regolith or power.
    /// </summary>
    public sealed class Settlement
    {
        public int CommonsCount { get; private set; }
        public int PadCount { get; private set; }
        public int CoreHabs { get; set; }
        public int VillageHabs { get; private set; }
        public int Farms { get; private set; }
        public int Mines { get; private set; }
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
                    return "raise Colony Commons first";
                if (Treasury < TreasuryGoal)
                    return $"grow the treasury to {TreasuryGoal:N0} CRED (now {Treasury:N0})";
                if (NetMetalsPerMin < IncomeGoalPerMin)
                    return $"keep income at {IncomeGoalPerMin:N0}+ CRED/min — tax collectors, mines, guild tax";
                return "holding — keep the treasury and income up";
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

            _prodTimer -= dt;
            if (_prodTimer <= 0f)
            {
                _prodTimer += ProductionInterval;
                ProduceMines();
            }

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
                case BuildingCategory.Habitat: CoreHabs++; EverHadHab = true; break;
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
        }

        public void ClaimOutpost() => HasOutpost = true;

        public void AddVillageHab()
        {
            VillageHabs++;
            EverHadHab = true;
        }

        public void LoseVillageHab() => VillageHabs = Mathf.Max(0, VillageHabs - 1);

        private void ProduceMines()
        {
            int met = Mathf.Max(0, Mathf.RoundToInt(Mines * 40 * MineYieldScale));
            if (met <= 0)
            {
                LastProductionLine = "";
                return;
            }
            // Majesty: gold sits in the building until a tax collector walks it home.
            if (RouteCampGoldToTills) PendingCampGold += met;
            else _resources.Add(ResourceId.Metals, met);
            LastProductionLine = $"mines +{met} CRED";
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
