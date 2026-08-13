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
        private static readonly Vector3[] Cardinals =
        {
            new Vector3(1f, 0f, 0f),
            new Vector3(-1f, 0f, 0f),
            new Vector3(0f, 0f, 1f)
        };

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
            SpawnInn();
        }

        public void Tick(float dt)
        {
            if (_loop == null || _loop.Settlement == null) return;
            _expandCooldown = Mathf.Max(0f, _expandCooldown - dt);
            Prune();

            var set = _loop.Settlement;
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
                BuildingCategory.Habitat => StructureRole.Core,
                _ => StructureRole.Core
            };

            var st = go.GetComponent<ColonyStructure>() ?? go.AddComponent<ColonyStructure>();
            float hp = role == StructureRole.Inn ? 80f : role == StructureRole.Workshop ? 70f : 48f;
            st.Configure(role, this, hp, cat, data);
            if (!_structures.Contains(st))
                _structures.Add(st);
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
                if (s.IsWorkshop && d < bestShopD)
                {
                    bestShopD = d;
                    bestShop = s;
                }
                else if (!s.IsWorkshop && s.HasOpenSlot() && d < bestJobD)
                {
                    bestJobD = d;
                    bestJob = s;
                }
            }
            return bestShop != null ? bestShop : bestJob;
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

        public void OnVillageHabDestroyed(ColonyStructure hab)
        {
            _loop?.Settlement?.LoseVillageHab();
            _structures.Remove(hab);
        }

        private void SpawnInn()
        {
            Vector3 pos = ColonyLayout.InnOutpost;
            GameObject prefab = BuildingVisualCatalog.LoadPrefab(BuildingCategory.Inn);
            GameObject go;
            if (prefab != null)
            {
                go = ColonyVisualUtility.InstantiateOriented(prefab, pos, _root, 0f);
                go.transform.localScale = Vector3.one * ColonyLayout.ModuleScale * 1.05f;
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.transform.SetParent(_root, false);
                go.transform.position = pos;
                go.transform.localScale = new Vector3(3.2f, 1.4f, 3.2f);
            }

            go.name = "WaystationInn";
            ColonyVisualUtility.EnsureUrpMaterials(go);
            ColonyVisualUtility.SnapToGround(go);
            CampusNavMesh.AddObstacle(go);

            var st = go.AddComponent<ColonyStructure>();
            st.Configure(StructureRole.Inn, this, 90f, BuildingCategory.Inn);
            _structures.Add(st);

            if (_loop != null && _loop.Placer != null && _loop.Grid != null)
            {
                Vector2Int cell = FootprintOrigin(pos, 4, 4);
                _loop.Placer.MarkOccupiedRect(cell, 4, 4);
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
            if (!TryNextSlot(out Vector3 habPos, out Vector3 plusPos)) return;
            if (!_loop.Resources.TrySpend(cost)) return;

            SpawnConnector(plusPos);
            SpawnHab(habPos);
            _loop.Settlement.AddVillageHab();
            _expandCooldown = 12f;

            Debug.Log($"[Village] HAB + plus @ {habPos} pop={_loop.Settlement.Population}/{_loop.Settlement.Housing}");
        }

        private bool TryNextSlot(out Vector3 habPos, out Vector3 plusPos)
        {
            habPos = Vector3.zero;
            plusPos = Vector3.zero;
            if (_loop.Grid == null || _loop.Placer == null) return false;

            Vector3 origin = ColonyLayout.CampusOrigin;
            for (int d = 0; d < Cardinals.Length; d++)
            {
                Vector3 dir = Cardinals[d];
                for (float dist = 6f; dist <= 72f; dist += 1.5f)
                {
                    Vector3 plus = origin + dir * dist;
                    Vector3 hab = plus + dir * 4.5f;

                    if (Flat(hab, ColonyLayout.InnOutpost) < 10f) continue;
                    if (Flat(hab, ColonyLayout.CampusBOrigin) < 16f) continue;
                    if (!_loop.Grid.InBounds(_loop.Grid.WorldToCell(hab))) continue;

                    Vector2Int plusCell = FootprintOrigin(plus, 2, 2);
                    Vector2Int habCell = FootprintOrigin(hab, 4, 4);
                    if (!_loop.Placer.CanFitRect(plusCell, 2, 2)) continue;
                    if (!_loop.Placer.CanFitRect(habCell, 4, 4)) continue;
                    if (!_loop.Placer.TouchesCampus(plusCell, 2, 2)) continue;

                    plusPos = plus;
                    habPos = hab;
                    return true;
                }
            }

            return false;
        }

        private void SpawnConnector(Vector3 mid)
        {
            var go = ColonyVisualUtility.SpawnPlusConnector(mid, _root, ColonyLayout.ModuleScale);
            go.name = "VillagePlus";
            CampusNavMesh.AddObstacle(go);
            if (_loop.Placer != null && _loop.Grid != null)
                _loop.Placer.MarkCampusRect(FootprintOrigin(mid, 2, 2), 2, 2);
        }

        private ColonyStructure SpawnHab(Vector3 pos)
        {
            GameObject prefab = BuildingVisualCatalog.LoadPrefab(BuildingCategory.Habitat);
            GameObject go;
            if (prefab != null)
            {
                go = ColonyVisualUtility.InstantiateOriented(prefab, pos, _root, 0f);
                go.transform.localScale = Vector3.one * ColonyLayout.ModuleScale;
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.transform.SetParent(_root, false);
                go.transform.position = pos;
            }

            go.name = $"VillageHAB_{_structures.Count}";
            ColonyVisualUtility.EnsureUrpMaterials(go);
            ColonyVisualUtility.SnapToGround(go);
            CampusNavMesh.AddObstacle(go);

            if (_loop.Placer != null && _loop.Grid != null)
                _loop.Placer.MarkCampusRect(FootprintOrigin(pos, 4, 4), 4, 4);

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
