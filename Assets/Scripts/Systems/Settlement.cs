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
        public int CoreHabs { get; set; }
        public int VillageHabs { get; private set; }
        public int Farms { get; private set; }
        public int Mines { get; private set; }
        public int RegolithCamps { get; private set; }
        public int LastTax { get; private set; }
        public int LastBirths { get; private set; }
        public int LastDeaths { get; private set; }

        public bool HasPalace => PalaceCount > 0;

        /// <summary>Preset conquest goal (set per body / campaign beat).</summary>
        public int PopulationGoal { get; private set; } = 12;

        public int Housing => (CoreHabs + VillageHabs) * HousingPerHab;
        public int CampCount => Farms + Mines + RegolithCamps;
        public int VacantBeds => Mathf.Max(0, Housing - Population);

        /// <summary>Overcrowded (or at cap) — village should grow a new HAB.</summary>
        public bool NeedsVillageHab =>
            HasPalace && CoreHabs > 0 && Population >= Housing && VillageHabs < MaxVillageHabs;

        public bool CanBirth => Population > 0 && Population < Housing;

        public bool MeetsPopulationGoal => Population >= PopulationGoal && Housing >= Population;

        public bool IsSustainable =>
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
                if (!MeetsPopulationGoal)
                    return $"grow to {PopulationGoal} (now {Population}, housing {Housing})";
                if (Farms <= 0 || Mines <= 0)
                    return "place a Farm and a Mine";
                if (!StockpileHealthy)
                    return "stockpile low — extract / produce";
                return "holding sustain";
            }
        }

        private float _taxTimer;
        private float _prodTimer;
        private float _growTimer;
        private readonly ResourceManager _resources;

        public float TaxInterval { get; set; } = 24f;
        public float ProductionInterval { get; set; } = 8f;
        public float GrowInterval { get; set; } = 18f;
        public bool BirthDue { get; private set; }

        public Settlement(ResourceManager resources)
        {
            _resources = resources;
            _taxTimer = TaxInterval;
            _prodTimer = ProductionInterval;
            _growTimer = GrowInterval;
        }

        public void SetPopulationGoal(int goal) =>
            PopulationGoal = Mathf.Clamp(goal, 1, 48);

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
                case BuildingCategory.Farm: Farms++; break;
                case BuildingCategory.Mine: Mines++; break;
                case BuildingCategory.RegolithCamp: RegolithCamps++; break;
                case BuildingCategory.Habitat: CoreHabs++; break;
            }
        }

        public void Unregister(BuildingCategory cat, bool villageHab = false)
        {
            switch (cat)
            {
                case BuildingCategory.Palace:
                    PalaceCount = Mathf.Max(0, PalaceCount - 1);
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
            if (Farms > 0)
                _resources.Add(ResourceId.WaterIce, Farms * 3);
            if (Mines > 0)
                _resources.Add(ResourceId.Metals, Mines * 4);
            if (RegolithCamps > 0)
                _resources.Add(ResourceId.Regolith, RegolithCamps * 6);
        }

        private void CollectTax()
        {
            LastTax = Population * TaxPerCitizen;
            if (LastTax > 0)
                _resources.Add(ResourceId.Metals, LastTax);
        }
    }
}
