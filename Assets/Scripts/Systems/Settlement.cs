using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Population, housing, HAB tax, and camp production.
    /// Campaign sustain gate uses <see cref="PopulationGoal"/> + camp balance.
    /// </summary>
    public sealed class Settlement
    {
        public const int HousingPerHab = 3;
        public const int MaxVillageHabs = 12;
        public const int MaxVillagers = 8;

        public int Population { get; private set; } = 6;
        public int CoreHabs { get; set; } = 3;
        public int VillageHabs { get; private set; }
        public int Farms { get; private set; }
        public int Mines { get; private set; }
        public int RegolithCamps { get; private set; }
        public int LastTax { get; private set; }

        /// <summary>Preset conquest goal (set per body / campaign beat).</summary>
        public int PopulationGoal { get; private set; } = 12;

        public int Housing => (CoreHabs + VillageHabs) * HousingPerHab;
        public int CampCount => Farms + Mines + RegolithCamps;
        public int TargetPopulation { get; private set; } = 6;
        public bool NeedsVillageHab =>
            TargetPopulation > Housing && VillageHabs < MaxVillageHabs;

        public bool MeetsPopulationGoal => Population >= PopulationGoal && Housing >= Population;

        /// <summary>
        /// Colony can feed itself: pop at goal, farm+mine online, and stockpile not empty.
        /// </summary>
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
        public float GrowInterval { get; set; } = 16f;

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
            RecalcTarget();

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
                if (Population < TargetPopulation && Population < Housing)
                    Population++;
            }
        }

        public void RegisterPlaced(BuildingCategory cat)
        {
            switch (cat)
            {
                case BuildingCategory.Farm: Farms++; break;
                case BuildingCategory.Mine: Mines++; break;
                case BuildingCategory.RegolithCamp: RegolithCamps++; break;
                case BuildingCategory.Habitat: CoreHabs++; break;
            }
        }

        public void AddVillageHab()
        {
            VillageHabs++;
        }

        public void LoseVillageHab()
        {
            VillageHabs = Mathf.Max(0, VillageHabs - 1);
            if (Population > Housing)
                Population = Housing;
        }

        public void RecalcTarget()
        {
            int wealth = 0;
            if (_resources != null)
            {
                wealth = _resources.Get(ResourceId.Regolith)
                         + _resources.Get(ResourceId.Metals)
                         + _resources.Get(ResourceId.WaterIce);
            }
            int fromWealth = wealth / 18;
            int fromCamps = CampCount * 2;
            int natural = 6 + fromWealth + fromCamps;
            TargetPopulation = Mathf.Clamp(Mathf.Max(PopulationGoal, natural), 6, 36);
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
            int habs = CoreHabs + VillageHabs;
            LastTax = habs * 2;
            if (LastTax > 0)
                _resources.Add(ResourceId.Metals, LastTax);
        }
    }
}
