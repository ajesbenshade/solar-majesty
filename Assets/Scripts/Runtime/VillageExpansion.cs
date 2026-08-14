using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Waystation inn (disconnected) plus cardinal HAB expansion through plus connectors.
    /// Economy stays inside the campus graph — no outdoor villagers.
    /// South is reserved for the inn outpost, so auto-growth walks east / west / north.
    /// </summary>
    public class VillageExpansion : MonoBehaviour
    {
        private readonly List<ColonyStructure> _structures = new List<ColonyStructure>(24);
        private GameLoop _loop;
        private Transform _root;
        private float _expandCooldown;

        public IReadOnlyList<ColonyStructure> Structures => _structures;
        public Vector3 InnPosition => ColonyLayout.InnOutpost;

        public void Bind(GameLoop loop)
        {
            _loop = loop;
            _root = new GameObject("VillageRing").transform;
            _root.SetParent(transform, false);
            if (_loop != null && _loop.SpawnWaystationInn)
                SpawnInn();
        }

        public void Tick(float dt)
        {
            if (_loop == null || _loop.Settlement == null) return;
            if (_loop.Placer == null || !_loop.Placer.HasCampus) return;
            _expandCooldown = Mathf.Max(0f, _expandCooldown - dt);
            Prune();

            var set = _loop.Settlement;
            if (set.BirthDue)
                TryBirth(set);
            if (set.NeedsVillageHab && _expandCooldown <= 0f)
                TryExpandVillage();
        }

        public void RegisterPlacedBuilding(BuildingCategory cat, GameObject go, Vector3 world) =>
            RegisterPlacedBuilding(null, cat, go, world);

        public void RegisterPlacedBuilding(BuildingData data, BuildingCategory cat, GameObject go, Vector3 world)
        {
            if (go == null || _loop?.Settlement == null) return;
            if (data != null) cat = data.category;
            if (cat == BuildingCategory.Utility)
                return;

            _loop.Settlement.RegisterPlaced(cat);

            StructureRole role = cat switch
            {
                BuildingCategory.Inn => StructureRole.Inn,
                BuildingCategory.Farm => StructureRole.Camp,
                BuildingCategory.Mine => StructureRole.Camp,
                BuildingCategory.RegolithCamp => StructureRole.Camp,
                BuildingCategory.ScoutWorkshop => StructureRole.Workshop,
                BuildingCategory.EngineerWorkshop => StructureRole.Workshop,
                BuildingCategory.DefenseWorkshop => StructureRole.Workshop,
                BuildingCategory.MedicWorkshop => StructureRole.Workshop,
                BuildingCategory.HarvesterWorkshop => StructureRole.Workshop,
                BuildingCategory.SurveyorWorkshop => StructureRole.Workshop,
                BuildingCategory.TerraformerWorkshop => StructureRole.Workshop,
                BuildingCategory.CourierWorkshop => StructureRole.Workshop,
                BuildingCategory.GeologistWorkshop => StructureRole.Workshop,
                BuildingCategory.SentinelWorkshop => StructureRole.Workshop,
                BuildingCategory.GuildHall => StructureRole.Guild,
                BuildingCategory.Palace => StructureRole.Core,
                BuildingCategory.Habitat => StructureRole.Core,
                _ => StructureRole.Core
            };

            var st = go.GetComponent<ColonyStructure>();
            if (st == null)
                st = go.AddComponent<ColonyStructure>();
            float hp = cat == BuildingCategory.Palace ? 140f
                : ColonyStructure.IsWonderCategory(cat) ? 120f
                : role == StructureRole.Inn ? 80f
                : role == StructureRole.Workshop || role == StructureRole.Guild ? 70f
                : 48f;
            st.Configure(role, this, hp, cat, data);
            if (cat == BuildingCategory.GuildHall)
                InheritGuildClass(st);
            if (!_structures.Contains(st))
                _structures.Add(st);

            if (st.IsResidential && _loop.Settlement.Population <= 0)
            {
                int n = _loop.Settlement.SeedStarterCrew();
                if (n > 0)
                    st.SetResidents(n);
            }
        }

        private void InheritGuildClass(ColonyStructure hall)
        {
            if (hall == null) return;
            ColonyStructure best = null;
            float bestD = 36f;
            for (int i = 0; i < _structures.Count; i++)
            {
                var s = _structures[i];
                if (s == null || !s.IsAlive || !s.IsWorkshop || !s.HasPreferredClass) continue;
                float d = Flat(hall.WorldPosition, s.WorldPosition);
                if (d < bestD)
                {
                    bestD = d;
                    best = s;
                }
            }
            if (best != null)
                hall.SetPreferredClass(best.PreferredClass);
        }

        public void RegisterRestoredVillageHab(GameObject go)
        {
            if (go == null || _loop?.Settlement == null) return;
            var st = go.GetComponent<ColonyStructure>();
            if (st == null)
                st = go.AddComponent<ColonyStructure>();
            st.Configure(StructureRole.VillageHab, this, 48f, BuildingCategory.Habitat);
            if (!_structures.Contains(st))
                _structures.Add(st);
            _loop.Settlement.AddVillageHab();
        }

        public ColonyStructure NearestDutyFor(SpecialistClass cls, Vector3 from, float maxDist)
        {
            ColonyStructure bestShop = null;
            ColonyStructure bestJob = null;
            float bestShopD = maxDist;
            float bestJobD = maxDist;
            for (int i = 0; i < _structures.Count; i++)
            {
                var s = _structures[i];
                if (s == null || !s.IsAlive || !s.HasPreferredClass) continue;
                if (s.PreferredClass != cls) continue;
                float d = Flat(from, s.WorldPosition);
                if ((s.IsWorkshop || s.IsGuild) && d < bestShopD)
                {
                    bestShopD = d;
                    bestShop = s;
                }
                else if (!s.IsWorkshop && !s.IsGuild && s.HasOpenSlot() && d < bestJobD)
                {
                    bestJobD = d;
                    bestJob = s;
                }
            }
            return bestShop != null ? bestShop : bestJob;
        }

        public ColonyStructure FindNear(Vector3 world, float maxDist = 3f)
        {
            ColonyStructure best = null;
            float bestD = maxDist;
            for (int i = 0; i < _structures.Count; i++)
            {
                var s = _structures[i];
                if (s == null || !s.IsAlive) continue;
                float d = Flat(world, s.WorldPosition);
                if (d < bestD)
                {
                    bestD = d;
                    best = s;
                }
            }
            return best;
        }

        public ColonyStructure NearestDamaged(Vector3 from, float maxDist)
        {
            ColonyStructure best = null;
            float bestD = maxDist;
            for (int i = 0; i < _structures.Count; i++)
            {
                var s = _structures[i];
                if (s == null || !s.NeedsRepair) continue;
                float d = Flat(from, s.WorldPosition);
                if (d < bestD)
                {
                    bestD = d;
                    best = s;
                }
            }
            return best;
        }

        public ColonyStructure FindVacantHab()
        {
            ColonyStructure best = null;
            int bestSpare = -1;
            for (int i = 0; i < _structures.Count; i++)
            {
                var s = _structures[i];
                if (s == null || !s.HasVacancy) continue;
                int spare = s.ResidentCapacity - s.Residents;
                if (spare > bestSpare)
                {
                    bestSpare = spare;
                    best = s;
                }
            }
            return best;
        }

        public void NotifyCollapsed(ColonyStructure st)
        {
            if (st == null) return;
            var set = _loop?.Settlement;
            if (set != null)
            {
                int killed = set.KillResidents(st.Residents);
                if (killed > 0)
                    st.SetResidents(0);
                set.Unregister(st.Category, st.IsVillageHab);
            }
            _structures.Remove(st);
            _loop?.NotifyStructureDestroyed(st);
        }

        public void OnStructureDestroyed(ColonyStructure st)
        {
            if (st == null) return;
            _structures.Remove(st);
            _loop?.NotifyStructureDestroyed(st);
        }

        public void RegisterShowcase(ColonyStructure st)
        {
            if (st == null || _structures.Contains(st)) return;
            _structures.Add(st);
        }

        public ColonyStructure NearestVillageHab(Vector3 from, float maxDist)
        {
            ColonyStructure best = null;
            float bestD = maxDist;
            for (int i = 0; i < _structures.Count; i++)
            {
                var s = _structures[i];
                if (s == null || !s.IsAlive || !s.IsVillageHab) continue;
                float d = Flat(from, s.WorldPosition);
                if (d < bestD)
                {
                    bestD = d;
                    best = s;
                }
            }
            return best;
        }

        public ColonyStructure NearestByCategory(Vector3 from, float maxDist, params BuildingCategory[] cats)
        {
            ColonyStructure best = null;
            float bestD = maxDist;
            for (int i = 0; i < _structures.Count; i++)
            {
                var s = _structures[i];
                if (s == null || !s.IsAlive) continue;
                if (!Matches(s.Category, cats)) continue;
                float d = Flat(from, s.WorldPosition);
                if (d < bestD)
                {
                    bestD = d;
                    best = s;
                }
            }
            return best;
        }

        public ColonyStructure NearestExtractor(Vector3 from, float maxDist) =>
            NearestByCategory(from, maxDist, BuildingCategory.Farm, BuildingCategory.Mine, BuildingCategory.RegolithCamp);

        public ColonyStructure NearestPower(Vector3 from, float maxDist) =>
            NearestByCategory(from, maxDist, BuildingCategory.Power);

        /// <summary>
        /// Prefer a matching drop-off (Mine for ore, Farm for ice, …) within haul range;
        /// otherwise the nearest pad / palace / camp.
        /// </summary>
        public bool TryFindDropOff(
            Vector3 from,
            ResourceNodeType node,
            out ColonyStructure site,
            out float dist,
            out bool matching)
        {
            site = null;
            dist = ExtractLogistics.MaxHaul;
            matching = false;
            ColonyStructure bestMatch = null;
            ColonyStructure bestAny = null;
            float bestMatchD = ExtractLogistics.MaxHaul;
            float bestAnyD = ExtractLogistics.MaxHaul;

            for (int i = 0; i < _structures.Count; i++)
            {
                var s = _structures[i];
                if (s == null || !s.IsAlive) continue;
                if (!ExtractLogistics.IsDropOff(s.Category)) continue;
                float d = Flat(from, s.WorldPosition);
                if (d >= ExtractLogistics.MaxHaul) continue;
                if (d < bestAnyD)
                {
                    bestAnyD = d;
                    bestAny = s;
                }
                if (ExtractLogistics.Prefers(s.Category, node) && d < bestMatchD)
                {
                    bestMatchD = d;
                    bestMatch = s;
                }
            }

            if (bestMatch != null)
            {
                site = bestMatch;
                dist = bestMatchD;
                matching = true;
                return true;
            }

            if (bestAny != null)
            {
                site = bestAny;
                dist = bestAnyD;
                matching = false;
                return true;
            }

            return false;
        }

        private static bool Matches(BuildingCategory cat, BuildingCategory[] cats)
        {
            if (cats == null) return false;
            for (int i = 0; i < cats.Length; i++)
            {
                if (cats[i] == cat) return true;
            }
            return false;
        }

        public void OnVillageHabDestroyed(ColonyStructure hab)
        {
            NotifyCollapsed(hab);
        }

        private void TryBirth(Settlement set)
        {
            if (set == null || !set.BirthDue) return;
            var hab = FindVacantHab();
            if (hab == null || !hab.TryAddResident())
                return;
            if (set.TryBirth())
                Debug.Log($"[Village] Birth in {hab.DisplayName} — pop {set.Population}/{set.Housing}");
            else
                hab.SetResidents(Mathf.Max(0, hab.Residents - 1));
        }

        private void SpawnInn()
        {
            Vector3 pos = ColonyLayout.InnOutpost;
            float cell = _loop != null && _loop.Grid != null
                ? _loop.Grid.CellSize
                : ColonyLayout.DefaultCellSize;
            GameObject go = ModularBuildingFactory.Spawn(
                BuildingCategory.Inn,
                pos,
                _root,
                4, 4, cell);

            go.name = "WaystationInn";
            CampusNavMesh.AddObstacle(go);

            var st = go.AddComponent<ColonyStructure>();
            st.Configure(StructureRole.Inn, this, 90f, BuildingCategory.Inn);
            _structures.Add(st);

            if (_loop != null && _loop.Placer != null && _loop.Grid != null)
            {
                Vector2Int origin = FootprintOrigin(pos, 4, 4);
                _loop.Placer.MarkOccupiedRect(origin, 4, 4);
            }
        }

        private void TryExpandVillage()
        {
            if (_loop.Resources == null) return;
            var cost = new[]
            {
                new ResourceAmount(ResourceId.Metals, 18),
                new ResourceAmount(ResourceId.Regolith, 12)
            };
            if (!_loop.Resources.CanAfford(cost)) return;
            if (!TryNextSlot(out Vector2Int airlockCell, out Vector2Int habCell)) return;
            if (!_loop.Resources.TrySpend(cost)) return;

            SpawnConnector(airlockCell);
            SpawnHab(habCell);
            _loop.Settlement.AddVillageHab();
            _loop.NotifyCampusExpanded();
            _expandCooldown = 12f;

            Debug.Log($"[Village] HAB + airlock @ {habCell}");
        }

        private bool TryNextSlot(out Vector2Int airlockCell, out Vector2Int habCell)
        {
            airlockCell = default;
            habCell = default;
            if (_loop.Grid == null || _loop.Placer == null) return false;

            var pieces = _loop.Placer.Pieces;
            for (int i = 0; i < pieces.Count; i++)
            {
                var module = pieces[i];
                if (!module.IsModule) continue;
                for (int f = 0; f < 4; f++)
                {
                    var face = (BuildingPlacer.Cardinal)f;
                    Vector2Int aCell = BuildingPlacer.AirlockOriginOnModuleFace(module, face);
                    if (!_loop.Placer.CanFitRect(aCell, 2, 2)) continue;
                    if (!_loop.Grid.InBounds(aCell) ||
                        !_loop.Grid.InBounds(new Vector2Int(aCell.x + 1, aCell.y + 1)))
                        continue;

                    var outFace = Opposite(face);
                    Vector2Int hCell = BuildingPlacer.ModuleOriginOnAirlockFace(
                        new BuildingPlacer.CampusPiece(aCell, 2, 2, BuildingCategory.Utility),
                        4, 4, outFace);
                    if (!_loop.Placer.CanFitRect(hCell, 4, 4)) continue;
                    if (!_loop.Grid.InBounds(hCell) ||
                        !_loop.Grid.InBounds(new Vector2Int(hCell.x + 3, hCell.y + 3)))
                        continue;

                    Vector3 habWorld = _loop.Grid.CellToWorld(hCell) + FootprintCenterOffset(4, 4);
                    if (Flat(habWorld, ColonyLayout.InnOutpost) < 10f) continue;
                    if (Flat(habWorld, ColonyLayout.CampusBOrigin) < 16f) continue;

                    airlockCell = aCell;
                    habCell = hCell;
                    return true;
                }
            }

            return false;
        }

        private static BuildingPlacer.Cardinal Opposite(BuildingPlacer.Cardinal face)
        {
            switch (face)
            {
                case BuildingPlacer.Cardinal.East: return BuildingPlacer.Cardinal.West;
                case BuildingPlacer.Cardinal.West: return BuildingPlacer.Cardinal.East;
                case BuildingPlacer.Cardinal.North: return BuildingPlacer.Cardinal.South;
                default: return BuildingPlacer.Cardinal.North;
            }
        }

        private Vector3 FootprintCenterOffset(int w, int h)
        {
            float cell = _loop.Grid.CellSize;
            return new Vector3((w - 1) * 0.5f * cell, 0f, (h - 1) * 0.5f * cell);
        }

        private void SpawnConnector(Vector2Int cell)
        {
            Vector3 mid = _loop.Grid.CellToWorld(cell) + FootprintCenterOffset(2, 2);
            float cellSize = _loop.Grid.CellSize;
            var go = ModularBuildingFactory.Spawn(
                BuildingCategory.Utility,
                mid,
                _root,
                2, 2, cellSize);
            go.name = "VillageAirlock";
            CampusNavMesh.AddObstacle(go);
            _loop.Placer.MarkCampusRect(cell, 2, 2);
            _loop.Placer.RegisterPiece(cell, 2, 2, BuildingCategory.Utility);
        }

        private ColonyStructure SpawnHab(Vector2Int cell)
        {
            Vector3 pos = _loop.Grid.CellToWorld(cell) + FootprintCenterOffset(4, 4);
            float cellSize = _loop.Grid.CellSize;
            GameObject go = ModularBuildingFactory.Spawn(
                BuildingCategory.Habitat,
                pos,
                _root,
                4, 4, cellSize);

            go.name = $"VillageHAB_{_structures.Count}";
            CampusNavMesh.AddObstacle(go);

            _loop.Placer.MarkCampusRect(cell, 4, 4);
            _loop.Placer.RegisterPiece(cell, 4, 4, BuildingCategory.Habitat);

            var st = go.AddComponent<ColonyStructure>();
            st.Configure(StructureRole.VillageHab, this, 48f, BuildingCategory.Habitat);
            _structures.Add(st);
            return st;
        }

        private Vector2Int FootprintOrigin(Vector3 world, int w, int h)
        {
            float cell = _loop.Grid.CellSize;
            float halfW = (w * cell) * 0.5f;
            float halfH = (h * cell) * 0.5f;
            Vector3 corner = world - new Vector3(halfW, 0f, halfH) + new Vector3(cell * 0.5f, 0f, cell * 0.5f);
            return _loop.Grid.WorldToCell(corner);
        }

        private void Prune()
        {
            for (int i = _structures.Count - 1; i >= 0; i--)
            {
                if (_structures[i] == null) _structures.RemoveAt(i);
            }
        }

        private static float Flat(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
