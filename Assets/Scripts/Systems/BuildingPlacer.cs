// Player building placement — pure C#, no MonoBehaviour, no specialist commands.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Thin Overseer placement API: validate footprint + cost, enqueue construction.
    /// Specialists are never told to build; they may later chase Build flags on their own.
    /// </summary>
    public sealed class BuildingPlacer
    {
        private readonly ResourceManager _resources;
        private readonly List<ConstructionOrder> _orders = new List<ConstructionOrder>();
        private readonly HashSet<long> _occupiedCells = new HashSet<long>();
        private int _nextId = 1;

        /// <summary>Optional map bounds / terrain rule. Return false to reject.</summary>
        public Func<Vector2Int, BuildingData, bool> ExtraPlacementRule { get; set; }

        public BuildingPlacer(ResourceManager resources)
        {
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
        }

        public IReadOnlyList<ConstructionOrder> Orders => _orders;

        public bool TryPlace(
            BuildingData data,
            Vector2Int gridCell,
            Vector3 worldPosition,
            out ConstructionOrder order,
            out string failReason)
        {
            order = null;
            failReason = null;

            if (data == null)
            {
                failReason = "null_building_data";
                return false;
            }

            if (!CanFit(data, gridCell))
            {
                failReason = "footprint_blocked";
                return false;
            }

            if (ExtraPlacementRule != null && !ExtraPlacementRule(gridCell, data))
            {
                failReason = "extra_rule_rejected";
                return false;
            }

            if (!_resources.CanAfford(data.buildCost))
            {
                failReason = "cannot_afford";
                return false;
            }

            if (!_resources.TrySpend(data.buildCost))
            {
                failReason = "spend_failed";
                return false;
            }

            MarkFootprint(data, gridCell, occupied: true);

            order = new ConstructionOrder
            {
                Id = _nextId++,
                Data = data,
                GridCell = gridCell,
                WorldPosition = worldPosition,
                ProgressSeconds = 0f,
                RequiredSeconds = Mathf.Max(0.1f, data.buildTimeSeconds)
            };

            _orders.Add(order);
            return true;
        }

        /// <summary>Passive construction tick. Heroes are not required for progress.</summary>
        public int TickConstruction(float deltaTime, List<ConstructionOrder> completedBuffer = null)
        {
            if (deltaTime <= 0f) return 0;

            int completed = 0;
            for (int i = _orders.Count - 1; i >= 0; i--)
            {
                ConstructionOrder o = _orders[i];
                if (o.IsComplete) continue;

                o.ProgressSeconds += deltaTime;
                if (!o.IsComplete) continue;

                completed++;
                completedBuffer?.Add(o);
                _orders.RemoveAt(i);
            }

            return completed;
        }

        /// <summary>Optional labor from an Engineer that chose a Build flag on its own.</summary>
        public void ApplyLabor(ConstructionOrder order, float workSeconds)
        {
            if (order == null || order.IsComplete || workSeconds <= 0f) return;
            order.ProgressSeconds += workSeconds;
        }

        public bool Cancel(ConstructionOrder order, bool refund)
        {
            if (order == null || !_orders.Remove(order))
                return false;

            MarkFootprint(order.Data, order.GridCell, occupied: false);

            if (refund && order.Data?.buildCost != null)
            {
                for (int i = 0; i < order.Data.buildCost.Length; i++)
                {
                    ResourceAmount c = order.Data.buildCost[i];
                    _resources.Add(c.resource, c.amount);
                }
            }

            return true;
        }

        public bool CanFit(BuildingData data, Vector2Int origin)
        {
            if (data == null) return false;

            for (int x = 0; x < data.footprintWidth; x++)
            {
                for (int y = 0; y < data.footprintHeight; y++)
                {
                    if (_occupiedCells.Contains(Pack(origin.x + x, origin.y + y)))
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Reserve cells without spending resources (showcase colony / pre-built map blockers).
        /// </summary>
        public void MarkOccupiedRect(Vector2Int origin, int width, int height)
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                    _occupiedCells.Add(Pack(origin.x + x, origin.y + y));
            }
        }

        public bool IsCellOccupied(Vector2Int cell) =>
            _occupiedCells.Contains(Pack(cell.x, cell.y));

        private void MarkFootprint(BuildingData data, Vector2Int origin, bool occupied)
        {
            if (data == null) return;

            for (int x = 0; x < data.footprintWidth; x++)
            {
                for (int y = 0; y < data.footprintHeight; y++)
                {
                    long key = Pack(origin.x + x, origin.y + y);
                    if (occupied) _occupiedCells.Add(key);
                    else _occupiedCells.Remove(key);
                }
            }
        }

        private static long Pack(int x, int y) => ((long)x << 32) ^ (uint)y;
    }
}
