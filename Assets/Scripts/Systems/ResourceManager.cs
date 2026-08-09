// Local stockpile — pure C#, no MonoBehaviour.

using System;
using System.Collections.Generic;

namespace SolarMajesty
{
    /// <summary>
    /// Thin stockpile API used by BuildingPlacer and SimpleEconomy.
    /// The brain does not spend resources directly; completed flags pay through economy later.
    /// </summary>
    public sealed class ResourceManager
    {
        private readonly Dictionary<ResourceId, int> _stock = new Dictionary<ResourceId, int>();

        public event Action<ResourceId, int> StockChanged;

        public ResourceManager()
        {
            foreach (ResourceId id in Enum.GetValues(typeof(ResourceId)))
                _stock[id] = 0;
        }

        public ResourceManager(IEnumerable<ResourceData> catalog) : this()
        {
            if (catalog == null) return;
            foreach (ResourceData def in catalog)
            {
                if (def == null) continue;
                _stock[def.id] = def.startingAmount;
            }
        }

        public IReadOnlyDictionary<ResourceId, int> Stock => _stock;

        public int Get(ResourceId id) =>
            _stock.TryGetValue(id, out int v) ? v : 0;

        public void Set(ResourceId id, int amount)
        {
            _stock[id] = amount;
            StockChanged?.Invoke(id, amount);
        }

        public void Add(ResourceId id, int delta)
        {
            if (delta == 0) return;
            int next = Get(id) + delta;
            _stock[id] = next;
            StockChanged?.Invoke(id, next);
        }

        public bool CanAfford(ResourceAmount[] costs)
        {
            if (costs == null || costs.Length == 0)
                return true;

            for (int i = 0; i < costs.Length; i++)
            {
                if (Get(costs[i].resource) < costs[i].amount)
                    return false;
            }

            return true;
        }

        public bool TrySpend(ResourceAmount[] costs)
        {
            if (!CanAfford(costs))
                return false;

            if (costs == null)
                return true;

            for (int i = 0; i < costs.Length; i++)
                Add(costs[i].resource, -costs[i].amount);

            return true;
        }

        public bool TrySpend(ResourceId id, int amount)
        {
            if (amount < 0 || Get(id) < amount)
                return false;
            Add(id, -amount);
            return true;
        }

        public int SpendUpTo(ResourceId id, int amount)
        {
            if (amount <= 0) return 0;
            int pay = Math.Min(amount, Get(id));
            if (pay > 0) Add(id, -pay);
            return pay;
        }

        public string DebugSummary() =>
            $"Regolith={Get(ResourceId.Regolith)} Ice={Get(ResourceId.WaterIce)} " +
            $"Metals={Get(ResourceId.Metals)} Power={Get(ResourceId.Power)}";
    }
}
