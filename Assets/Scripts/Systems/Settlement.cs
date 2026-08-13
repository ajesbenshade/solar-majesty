using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Population, housing, HAB tax, and camp production. Grows with the stockpile + camps.
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

        public int ExtraHousing { get; private set; }
        public int ExtraIcePerTick { get; private set; }
        public int ExtraMetalsPerTick { get; private set; }
        public int ExtraRegolithPerTick { get; private set; }

        public int Housing => (CoreHabs + VillageHabs) * HousingPerHab + ExtraHousing;
        public int CampCount => Farms + Mines + RegolithCamps;
        public int TargetPopulation { get; private set; } = 6;
        public bool NeedsVillageHab =>
            TargetPopulation > Housing && VillageHabs < MaxVillageHabs;

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

        public void NotifyUpgrade(BuildingCategory cat)
        {
            switch (cat)
            {
                case BuildingCategory.Farm:
                    ExtraIcePerTick += 2;
                    break;
                case BuildingCategory.Mine:
                    ExtraMetalsPerTick += 3;
                    break;
                case BuildingCategory.RegolithCamp:
                    ExtraRegolithPerTick += 4;
                    break;
                case BuildingCategory.Habitat:
                    ExtraHousing++;
                    break;
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
            TargetPopulation = Mathf.Clamp(6 + fromWealth + fromCamps, 6, 24);
        }

        private void ProduceCamps()
        {
            if (Farms > 0 || ExtraIcePerTick > 0)
                _resources.Add(ResourceId.WaterIce, Farms * 3 + ExtraIcePerTick);
            if (Mines > 0 || ExtraMetalsPerTick > 0)
                _resources.Add(ResourceId.Metals, Mines * 4 + ExtraMetalsPerTick);
            if (RegolithCamps > 0 || ExtraRegolithPerTick > 0)
                _resources.Add(ResourceId.Regolith, RegolithCamps * 6 + ExtraRegolithPerTick);
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
