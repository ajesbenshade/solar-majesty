// Player building placement — pure C#, no MonoBehaviour, no specialist commands.
// Lego campus: modules expose cardinal airlock sockets; airlocks dock only there;
// every non-palace module must dock to an airlock end.

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
        public enum Cardinal
        {
            East = 0,
            West = 1,
            North = 2,
            South = 3
        }

        public readonly struct CampusPiece
        {
            public readonly Vector2Int Origin;
            public readonly int Width;
            public readonly int Height;
            public readonly BuildingCategory Category;

            public CampusPiece(Vector2Int origin, int width, int height, BuildingCategory category)
            {
                Origin = origin;
                Width = Mathf.Max(1, width);
                Height = Mathf.Max(1, height);
                Category = category;
            }

            public bool IsAirlock => BuildingPlacer.IsAirlock(Category);
            public bool IsModule => !IsAirlock && Category != BuildingCategory.Inn;
        }

        private readonly ResourceManager _resources;
        private readonly List<ConstructionOrder> _orders = new List<ConstructionOrder>();
        private readonly HashSet<long> _occupiedCells = new HashSet<long>();
        private readonly HashSet<long> _campusCells = new HashSet<long>();
        private readonly List<CampusPiece> _pieces = new List<CampusPiece>();
        private int _nextId = 1;

        /// <summary>Airlock junction footprint (always square).</summary>
        public const int AirlockSize = 2;

        /// <summary>The waystation inn is occupied but never part of the tube graph.</summary>
        public static bool RequiresCampusLink(BuildingCategory cat) =>
            cat != BuildingCategory.Inn;

        /// <summary>Airlock junction piece — docks modules on cardinal faces.</summary>
        public static bool IsAirlock(BuildingCategory cat) =>
            cat == BuildingCategory.Utility;

        /// <summary>Optional map bounds / terrain rule. Return false to reject.</summary>
        public Func<Vector2Int, BuildingData, bool> ExtraPlacementRule { get; set; }

        /// <summary>When set, non-palace buildings are rejected until a palace exists.</summary>
        public Func<bool> HasPalace { get; set; }

        public BuildingPlacer(ResourceManager resources)
        {
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
        }

        public IReadOnlyList<ConstructionOrder> Orders => _orders;
        public IReadOnlyList<CampusPiece> Pieces => _pieces;

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
            return CanFitRect(origin, data.footprintWidth, data.footprintHeight);
        }

        public bool CanFitRect(Vector2Int origin, int width, int height)
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                if (_occupiedCells.Contains(Pack(origin.x + x, origin.y + y)))
                    return false;
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

        /// <summary>Campus graph (excludes the disconnected waystation inn).</summary>
        public void MarkCampusRect(Vector2Int origin, int width, int height)
        {
            MarkOccupiedRect(origin, width, height);
            SeedCampusClaim(origin, width, height);
        }

        /// <summary>
        /// Soft claim zone: campus adjacency without blocking the cells.
        /// Used for empty-start levels so the first modules must land on the drop site.
        /// </summary>
        public void SeedCampusClaim(Vector2Int origin, int width, int height)
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                _campusCells.Add(Pack(origin.x + x, origin.y + y));
        }

        public bool HasCampus => _campusCells.Count > 0;

        /// <summary>Register a pre-built module/airlock so Lego docks work (village / showcase).</summary>
        public void RegisterPiece(Vector2Int origin, int width, int height, BuildingCategory category)
        {
            if (category == BuildingCategory.Inn) return;
            _pieces.Add(new CampusPiece(origin, width, height, category));
            if (RequiresCampusLink(category))
                SeedCampusClaim(origin, width, height);
        }

        public bool TouchesCampus(Vector2Int origin, int width, int height)
        {
            if (_campusCells.Count == 0) return true;

            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            int[] dx = { 1, -1, 0, 0 };
            int[] dy = { 0, 0, 1, -1 };
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                int cx = origin.x + x;
                int cy = origin.y + y;
                if (_campusCells.Contains(Pack(cx, cy)))
                    return true;
                for (int i = 0; i < 4; i++)
                {
                    if (_campusCells.Contains(Pack(cx + dx[i], cy + dy[i])))
                        return true;
                }
            }
            return false;
        }

        public bool OverlapsSoftClaim(Vector2Int origin, int width, int height)
        {
            if (_campusCells.Count == 0) return true;
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                if (_campusCells.Contains(Pack(origin.x + x, origin.y + y)))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Airlock origin centered on a module face midline (axis of symmetry).
        /// </summary>
        public static Vector2Int AirlockOriginOnModuleFace(CampusPiece module, Cardinal face)
        {
            int mw = module.Width;
            int mh = module.Height;
            int mx = module.Origin.x;
            int my = module.Origin.y;
            int midY = my + CenterOffset(mh, AirlockSize);
            int midX = mx + CenterOffset(mw, AirlockSize);
            switch (face)
            {
                case Cardinal.East: return new Vector2Int(mx + mw, midY);
                case Cardinal.West: return new Vector2Int(mx - AirlockSize, midY);
                case Cardinal.North: return new Vector2Int(midX, my + mh);
                default: return new Vector2Int(midX, my - AirlockSize);
            }
        }

        /// <summary>
        /// Module origin so its face midline docks onto an airlock end.
        /// </summary>
        public static Vector2Int ModuleOriginOnAirlockFace(
            CampusPiece airlock,
            int moduleW,
            int moduleH,
            Cardinal airlockFace)
        {
            int ax = airlock.Origin.x;
            int ay = airlock.Origin.y;
            int aw = airlock.Width;
            int ah = airlock.Height;
            moduleW = Mathf.Max(1, moduleW);
            moduleH = Mathf.Max(1, moduleH);
            int midY = ay + CenterOffset(ah, moduleH);
            int midX = ax + CenterOffset(aw, moduleW);
            switch (airlockFace)
            {
                case Cardinal.East: return new Vector2Int(ax + aw, midY);
                case Cardinal.West: return new Vector2Int(ax - moduleW, midY);
                case Cardinal.North: return new Vector2Int(midX, ay + ah);
                default: return new Vector2Int(midX, ay - moduleH);
            }
        }

        /// <summary>True if this airlock origin is exactly on a module symmetry-axis socket.</summary>
        public bool IsValidAirlockDock(Vector2Int origin)
        {
            if (!CanFitRect(origin, AirlockSize, AirlockSize))
                return false;

            for (int i = 0; i < _pieces.Count; i++)
            {
                var piece = _pieces[i];
                if (!piece.IsModule) continue;
                for (int f = 0; f < 4; f++)
                {
                    if (AirlockOriginOnModuleFace(piece, (Cardinal)f) == origin)
                        return true;
                }
            }
            return false;
        }

        /// <summary>True if this module origin docks flush to an airlock end (centered).</summary>
        public bool IsValidModuleDock(Vector2Int origin, int width, int height)
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            if (!CanFitRect(origin, width, height))
                return false;

            for (int i = 0; i < _pieces.Count; i++)
            {
                var piece = _pieces[i];
                if (!piece.IsAirlock) continue;
                for (int f = 0; f < 4; f++)
                {
                    if (ModuleOriginOnAirlockFace(piece, width, height, (Cardinal)f) == origin)
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Snap cursor cell to the nearest legal Lego dock for this building.
        /// Palace snaps onto the soft claim; airlocks → module sockets; modules → airlock ends.
        /// </summary>
        public bool TrySnapDock(BuildingData data, Vector2Int preferred, out Vector2Int snapped)
        {
            snapped = preferred;
            if (data == null) return false;

            if (data.category == BuildingCategory.Palace)
            {
                if (CanFit(data, preferred) && OverlapsSoftClaim(preferred, data.footprintWidth, data.footprintHeight))
                {
                    snapped = preferred;
                    return true;
                }
                return CanFit(data, preferred);
            }

            if (data.category == BuildingCategory.Inn)
            {
                snapped = preferred;
                return CanFit(data, preferred);
            }

            int bestDist = int.MaxValue;
            Vector2Int best = preferred;
            bool found = false;

            if (IsAirlock(data.category))
            {
                for (int i = 0; i < _pieces.Count; i++)
                {
                    var piece = _pieces[i];
                    if (!piece.IsModule) continue;
                    for (int f = 0; f < 4; f++)
                    {
                        Vector2Int o = AirlockOriginOnModuleFace(piece, (Cardinal)f);
                        if (!CanFitRect(o, AirlockSize, AirlockSize)) continue;
                        int d = Manhattan(preferred, o);
                        if (d < bestDist)
                        {
                            bestDist = d;
                            best = o;
                            found = true;
                        }
                    }
                }
            }
            else
            {
                int w = data.footprintWidth;
                int h = data.footprintHeight;
                for (int i = 0; i < _pieces.Count; i++)
                {
                    var piece = _pieces[i];
                    if (!piece.IsAirlock) continue;
                    for (int f = 0; f < 4; f++)
                    {
                        Vector2Int o = ModuleOriginOnAirlockFace(piece, w, h, (Cardinal)f);
                        if (!CanFitRect(o, w, h)) continue;
                        int d = Manhattan(preferred, o);
                        if (d < bestDist)
                        {
                            bestDist = d;
                            best = o;
                            found = true;
                        }
                    }
                }
            }

            if (!found) return false;
            // Only snap when the cursor is near a socket (avoid teleporting across the map).
            if (bestDist > 10) return false;
            snapped = best;
            return true;
        }

        public bool SharesCampusEdge(Vector2Int origin, int width, int height)
        {
            if (_campusCells.Count == 0) return true;

            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);

            for (int y = 0; y < height; y++)
            {
                if (_campusCells.Contains(Pack(origin.x - 1, origin.y + y))) return true;
                if (_campusCells.Contains(Pack(origin.x + width, origin.y + y))) return true;
            }

            for (int x = 0; x < width; x++)
            {
                if (_campusCells.Contains(Pack(origin.x + x, origin.y - 1))) return true;
                if (_campusCells.Contains(Pack(origin.x + x, origin.y + height))) return true;
            }

            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                if (_campusCells.Contains(Pack(origin.x + x, origin.y + y)))
                    return true;
            }

            return false;
        }

        private void MarkFootprint(BuildingData data, Vector2Int origin, bool occupied)
        {
            if (data == null) return;

            bool campus = RequiresCampusLink(data.category);
            for (int x = 0; x < data.footprintWidth; x++)
            {
                for (int y = 0; y < data.footprintHeight; y++)
                {
                    long key = Pack(origin.x + x, origin.y + y);
                    if (occupied)
                    {
                        _occupiedCells.Add(key);
                        if (campus) _campusCells.Add(key);
                    }
                    else
                    {
                        _occupiedCells.Remove(key);
                        if (campus) _campusCells.Remove(key);
                    }
                }
            }

            if (data.category == BuildingCategory.Inn)
                return;

            if (occupied)
            {
                _pieces.Add(new CampusPiece(origin, data.footprintWidth, data.footprintHeight, data.category));
            }
            else
            {
                for (int i = _pieces.Count - 1; i >= 0; i--)
                {
                    var p = _pieces[i];
                    if (p.Origin == origin && p.Category == data.category &&
                        p.Width == data.footprintWidth && p.Height == data.footprintHeight)
                    {
                        _pieces.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        /// <summary>Align a child span on a parent span midline (Lego centering).</summary>
        private static int CenterOffset(int parentSpan, int childSpan) =>
            (parentSpan - childSpan) / 2;

        private static int Manhattan(Vector2Int a, Vector2Int b) =>
            Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

        private static long Pack(int x, int y) => ((long)x << 32) ^ (uint)y;
    }
}
