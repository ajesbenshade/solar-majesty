using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Population, housing, HAB tax, and camp production.
    /// Citizens live in HABs: tax is per person; births need spare beds;
    /// destroyed housing kills occupants (runtime reports the deaths).
    /// </summary>
    public sealed class Settlement
    {
        public const int HousingPerHab = 3;
        public const int MaxVillageHabs = 12;
        public const int MaxVillagers = 8;
        public const int StarterColonists = 2;
        public const int TaxPerCitizen = 2;

        public int Population { get; private set; }
        public int PalaceCount { get; private set; }
        public int PadCount { get; private set; }
        public int CoreHabs { get; set; }
        public int VillageHabs { get; private set; }
        public int Farms { get; private set; }
        public int Mines { get; private set; }
        public int RegolithCamps { get; private set; }
        public int PowerPlants { get; private set; }
        public int LastTax { get; private set; }
        public int LastBirths { get; private set; }
        public int LastDeaths { get; private set; }
        public string LastProductionLine { get; private set; } = "";
        public bool HasOutpost { get; private set; }
        public int GuildCount { get; private set; }
        public int BonusBeds { get; set; }

        /// <summary>Farm/mine/camp tick multiplier (power short pulls this down).</summary>
        public float ProductionScale { get; set; } = 1f;

        public bool HasPalace => PalaceCount > 0;
        public bool HasGuild => GuildCount > 0;

        /// <summary>Preset conquest goal (set per body / campaign beat).</summary>
        public int PopulationGoal { get; private set; } = 12;

        public int Housing => (CoreHabs + VillageHabs) * HousingPerHab + Mathf.Max(0, BonusBeds);
        public int CampCount => Farms + Mines + RegolithCamps;
        public int VacantBeds => Mathf.Max(0, Housing - Population);

        /// <summary>At or over bed cap — unrest, thinner tax.</summary>
        public bool Overcrowded => Population > 0 && Housing > 0 && Population >= Housing;

        /// <summary>Full beds, or still short of the conquest housing goal.</summary>
        public bool HousingTight =>
            Population > 0 && (Population >= Housing || Housing < PopulationGoal);

        /// <summary>Overcrowded (or at cap) — village should grow a new HAB.</summary>
        public bool NeedsVillageHab =>
            HasPalace && CoreHabs > 0 && Population >= Housing && VillageHabs < MaxVillageHabs;

        public bool CanBirth => Population > 0 && Population < Housing;

        public bool MeetsPopulationGoal => Population >= PopulationGoal && Housing >= Population;

        public bool HasPad => PadCount > 0;

        public bool IsSustainable =>
            HasPalace &&
            MeetsPopulationGoal &&
            Farms > 0 &&
            Mines > 0 &&
            StockpileHealthy;

        public bool StockpileHealthy
        {
            get
            {
                if (_resources == null) return false;
                return _resources.Get(ResourceId.WaterIce) >= 8 &&
                       _resources.Get(ResourceId.Metals) >= 12 &&
                       _resources.Get(ResourceId.Regolith) >= 10;
            }
        }

        public string SustainHint
        {
            get
            {
                if (!HasPalace)
                    return "raise the Palace keep first";
                if (CoreHabs <= 0)
                    return "dock a Habitat for colonists";
                if (Housing < PopulationGoal)
                    return $"need {PopulationGoal} beds (housing {Housing} — dock more HAB)";
                if (Population < PopulationGoal)
                    return $"grow to {PopulationGoal} (now {Population}) — births need vacant beds";
                if (Farms <= 0)
                    return FarmYieldScale < 0.8f
                        ? "place a Greenhouse Farm (thin soil here — extract metals too)"
                        : "place a Greenhouse Farm (dock via airlock)";
                if (Mines <= 0)
                    return MineYieldScale < 0.8f
                        ? "place an Ore Mine (poor ore here — ice farms help)"
                        : "place an Ore Mine (dock via airlock)";
                if (!StockpileHealthy)
                    return FarmYieldScale < 0.8f
                        ? "stockpile low — Extract metal/ice nodes; farms are thin on this body"
                        : "stockpile low — Extract flags / wait on farm+mine ticks";
                return "holding sustain — keep ICE/MET/REG above the floor";
            }
        }

        private float _taxTimer;
        private float _prodTimer;
        private float _growTimer;
        private readonly ResourceManager _resources;

        public float TaxInterval { get; set; } = 24f;
        public float ProductionInterval { get; set; } = 8f;
        public float GrowInterval { get; set; } = 18f;
        public float FarmYieldScale { get; set; } = 1f;
        public float MineYieldScale { get; set; } = 1f;
        public bool BirthDue { get; private set; }

        private float _bodyFarm = 1f;
        private float _bodyMine = 1f;
        private float _techFarm;
        private float _techMine;
        private float _terraformFarm;

        public Settlement(ResourceManager resources)
        {
            _resources = resources;
            _taxTimer = TaxInterval;
            _prodTimer = ProductionInterval;
            _growTimer = GrowInterval;
        }

        public void SetPopulationGoal(int goal) =>
            PopulationGoal = Mathf.Clamp(goal, 1, 48);

        public void SetBodyYield(float farmScale, float mineScale)
        {
            _bodyFarm = Mathf.Clamp(farmScale, 0.1f, 3f);
            _bodyMine = Mathf.Clamp(mineScale, 0.1f, 3f);
            RefreshYield();
        }

        public void SetTechYieldBonus(float farmBonus, float mineBonus)
        {
            _techFarm = Mathf.Max(0f, farmBonus);
            _techMine = Mathf.Max(0f, mineBonus);
            RefreshYield();
        }

        public void AddTerraformPulse()
        {
            _terraformFarm = Mathf.Min(0.6f, _terraformFarm + 0.08f);
            RefreshYield();
        }

        private void RefreshYield()
        {
            FarmYieldScale = Mathf.Clamp(_bodyFarm * (1f + _techFarm) + _terraformFarm, 0.1f, 3.5f);
            MineYieldScale = Mathf.Clamp(_bodyMine * (1f + _techMine), 0.1f, 3.5f);
        }

        public void Tick(float dt)
        {
            if (dt <= 0f || _resources == null) return;

            _prodTimer -= dt;
            if (_prodTimer <= 0f)
            {
                _prodTimer += ProductionInterval;
                ProduceCamps();
            }

            _taxTimer -= dt;
            if (_taxTimer <= 0f)
            {
                _taxTimer += TaxInterval;
                CollectTax();
            }

            _growTimer -= dt;
            if (_growTimer <= 0f)
            {
                _growTimer += GrowInterval;
                LastBirths = 0;
                BirthDue = CanBirth;
            }
        }

        public void RegisterPlaced(BuildingCategory cat)
        {
            switch (cat)
            {
                case BuildingCategory.Palace: PalaceCount++; break;
                case BuildingCategory.LandingPad: PadCount++; break;
                case BuildingCategory.Farm: Farms++; break;
                case BuildingCategory.Mine: Mines++; break;
                case BuildingCategory.RegolithCamp: RegolithCamps++; break;
                case BuildingCategory.Habitat: CoreHabs++; break;
                case BuildingCategory.Power: PowerPlants++; break;
                case BuildingCategory.GuildHall: GuildCount++; break;
            }
        }

        public void Unregister(BuildingCategory cat, bool villageHab = false)
        {
            switch (cat)
            {
                case BuildingCategory.Palace:
                    PalaceCount = Mathf.Max(0, PalaceCount - 1);
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
                case BuildingCategory.RegolithCamp:
                    RegolithCamps = Mathf.Max(0, RegolithCamps - 1);
                    break;
                case BuildingCategory.Habitat:
                    if (villageHab)
                        VillageHabs = Mathf.Max(0, VillageHabs - 1);
                    else
                        CoreHabs = Mathf.Max(0, CoreHabs - 1);
                    break;
                case BuildingCategory.Power:
                    PowerPlants = Mathf.Max(0, PowerPlants - 1);
                    break;
                case BuildingCategory.GuildHall:
                    GuildCount = Mathf.Max(0, GuildCount - 1);
                    break;
            }

            if (Population > Housing)
                Population = Housing;
        }

        /// <summary>First HAB on a body: drop a starter crew into the census.</summary>
        public int SeedStarterCrew()
        {
            if (Population > 0) return 0;
            int n = Mathf.Min(StarterColonists, Housing);
            Population = n;
            return n;
        }

        public void RestorePopulation(int pop)
        {
            Population = Mathf.Clamp(pop, 0, Mathf.Max(Housing, pop));
            if (Population > Housing)
                Population = Housing;
        }

        public void ClaimOutpost() => HasOutpost = true;

        public void AddVillageHab()
        {
            VillageHabs++;
        }

        public bool TryBirth()
        {
            BirthDue = false;
            if (!CanBirth) return false;
            Population++;
            LastBirths = 1;
            return true;
        }

        public int KillResidents(int count)
        {
            int killed = Mathf.Clamp(count, 0, Population);
            Population -= killed;
            LastDeaths = killed;
            return killed;
        }

        public void LoseVillageHab()
        {
            VillageHabs = Mathf.Max(0, VillageHabs - 1);
            if (Population > Housing)
                Population = Housing;
        }

        private void ProduceCamps()
        {
            float scale = Mathf.Clamp(ProductionScale, 0.15f, 1.5f);
            int ice = Mathf.Max(0, Mathf.RoundToInt(Farms * 3 * FarmYieldScale * scale));
            int met = Mathf.Max(0, Mathf.RoundToInt(Mines * 4 * MineYieldScale * scale));
            int reg = Mathf.Max(0, Mathf.RoundToInt(RegolithCamps * 6 * scale));
            if (ice > 0)
                _resources.Add(ResourceId.WaterIce, ice);
            if (met > 0)
                _resources.Add(ResourceId.Metals, met);
            if (reg > 0)
                _resources.Add(ResourceId.Regolith, reg);

            if (ice + met + reg <= 0)
            {
                LastProductionLine = "";
                return;
            }

            LastProductionLine = $"camps +{ice} ICE +{met} MET +{reg} REG";
        }

        private void CollectTax()
        {
            float scale = Overcrowded ? 0.65f : 1f;
            LastTax = Mathf.Max(0, Mathf.RoundToInt(Population * TaxPerCitizen * scale));
            if (LastTax > 0)
                _resources.Add(ResourceId.Metals, LastTax);
        }
    }
}
