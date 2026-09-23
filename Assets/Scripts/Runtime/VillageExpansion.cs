using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// The colony's buildings and their tills: daily energy tax, tax collectors, and villagers
    /// who slowly raise houses and solar farms around the Commons while the yard is safe.
    /// </summary>
    public class VillageExpansion : MonoBehaviour
    {
        private readonly List<ColonyStructure> _structures = new List<ColonyStructure>(24);
        private GameLoop _loop;
        private Transform _root;

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
            Prune();

            var set = _loop.Settlement;
            int before = TotalSittingLevy();
            PayDailyTaxes(set);
            if (TotalSittingLevy() > before)
                _loop.NotifyLevySitting(TotalSittingLevy());
            TickLevyPurses(dt);
            TickLevySit(dt);
            _collectors?.Tick(dt);
            TickVillageGrowth(dt, set);
        }

        // ---------------------------------------------------------------- villagers build

        private BuildingData _projData;
        private ConstructionOrder _proj;
        private GameObject _projSite;
        private readonly List<VillagerAgent> _builders = new List<VillagerAgent>(2);
        private float _growCooldown = 20f;
        private int _projSalt;

        /// <summary>What the villagers are doing, for the HUD.</summary>
        public string VillageStatus { get; private set; } = "villagers settling in";
        public float VillageProgress01 => _proj != null && _proj.RequiredSeconds > 0f
            ? Mathf.Clamp01(_proj.ProgressSeconds / _proj.RequiredSeconds) : 0f;
        public bool VillageHalted { get; private set; }
        public bool VillageBuilding => _proj != null;

        private void TickVillageGrowth(float dt, Settlement set)
        {
            if (set == null || !set.HasCommons || _loop.Placer == null) return;

            if (_proj == null)
            {
                VillageHalted = false;
                _growCooldown -= dt;
                if (_growCooldown > 0f)
                {
                    VillageStatus = "villagers resting";
                    return;
                }
                var next = VillageGrowth.NextProject(set.Habs, set.SolarFarms);
                if (!next.HasValue)
                {
                    VillageStatus = "village full";
                    _growCooldown = 30f;
                    return;
                }
                var data = _loop.VillageData(next.Value);
                if (data == null || !_loop.TryFindVillagePlot(data, _projSalt++, out Vector2Int cell, out Vector3 world))
                {
                    VillageStatus = "no open ground near the Commons";
                    _growCooldown = 10f;
                    return;
                }
                if (!_loop.IsSettlementSafe(world))
                {
                    VillageStatus = "waiting — enemies near the settlement";
                    VillageHalted = true;
                    _growCooldown = 3f;
                    return;
                }
                StartProject(data, cell, world);
                return;
            }

            bool safe = _loop.IsSettlementSafe(_proj.WorldPosition);
            VillageHalted = !safe;
            string what = VillageName(_projData.category);
            if (!safe)
            {
                VillageStatus = $"{what} halted — enemies near";
                return;
            }
            _proj.ProgressSeconds += dt;
            VillageStatus = $"raising a {what}";
            if (!_proj.IsComplete) return;
            FinishProject();
        }

        private void StartProject(BuildingData data, Vector2Int cell, Vector3 world)
        {
            _projData = data;
            int w = Mathf.Max(1, data.footprintWidth), h = Mathf.Max(1, data.footprintHeight);
            _loop.Placer.MarkOccupiedRect(cell, w, h);
            _proj = new ConstructionOrder
            {
                Data = data,
                GridCell = cell,
                WorldPosition = world,
                RequiredSeconds = VillageGrowth.BuildSeconds(data.category)
            };
            _projSite = new GameObject($"VillageSite_{data.category}");
            _projSite.transform.SetParent(_root, true);
            _projSite.transform.position = world + Vector3.up * 0.05f;
            _projSite.AddComponent<ConstructionSiteVisual>().Bind(_proj);

            var hub = CommonsHub();
            Vector3 home = hub != null ? hub.WorldPosition : world;
            for (int i = 0; i < 2; i++)
            {
                var v = VillagerAgent.Spawn(_root, home + new Vector3(i * 1.2f, 0f, 1.5f), world + new Vector3(i * 1.5f - 0.75f, 0f, 0f));
                if (v != null) _builders.Add(v);
            }
            _loop.LogOverseer($"Villagers stake out a {VillageName(data.category)} — keep the yard clear.");
        }

        private void FinishProject()
        {
            var data = _projData;
            var cell = _proj.GridCell;
            int w = Mathf.Max(1, data.footprintWidth), h = Mathf.Max(1, data.footprintHeight);
            _loop.Placer.ClearOccupiedRect(cell, w, h);
            bool raised = _loop.RaiseVillageBuilding(data, cell);
            ClearProject();
            _growCooldown = VillageGrowth.CooldownSeconds;
            if (raised)
                _loop.LogOverseer($"Villagers raised a {VillageName(data.category)}. It pays {MajestyEconomy.DailyTax(data.category)} EU a day.");
        }

        private void ClearProject()
        {
            for (int i = 0; i < _builders.Count; i++)
                if (_builders[i] != null) Object.Destroy(_builders[i].gameObject);
            _builders.Clear();
            if (_projSite != null) Object.Destroy(_projSite);
            _projSite = null;
            _proj = null;
            _projData = null;
        }

        private static string VillageName(BuildingCategory cat) =>
            cat == BuildingCategory.Power ? "solar farm" : "house";

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
                BuildingCategory.Market => StructureRole.Core,
                BuildingCategory.Blacksmith => StructureRole.Core,
                BuildingCategory.FobotYard => StructureRole.Core,
                BuildingCategory.Watchtower => StructureRole.Core,
                BuildingCategory.AidStation => StructureRole.Core,
                BuildingCategory.Commons => StructureRole.Core,
                BuildingCategory.Habitat => StructureRole.Core,
                _ => StructureRole.Core
            };

            var st = go.GetComponent<ColonyStructure>();
            if (st == null)
                st = go.AddComponent<ColonyStructure>();
            float hp = cat == BuildingCategory.Commons ? 140f
                : ColonyStructure.IsWonderCategory(cat) ? 120f
                : role == StructureRole.Inn ? 80f
                : role == StructureRole.Workshop || role == StructureRole.Guild ? 70f
                : 48f;
            st.Configure(role, this, hp, cat, data);
            if (cat == BuildingCategory.GuildHall)
                InheritGuildClass(st);
            if (!_structures.Contains(st))
                _structures.Add(st);
        }

        private void InheritGuildClass(ColonyStructure hall)
        {
            if (hall == null || hall.ClassLocked || hall.HasPreferredClass) return;
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
                if (s == null || !s.IsAlive) continue;
                bool match = (s.HasPreferredClass && s.PreferredClass == cls) ||
                             s.AcceptsGuard(cls) ||
                             s.AcceptsHealer(cls);
                if (!match) continue;
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
                int lost = st.CollectLevy();
                if (lost > 0)
                {
                    set.NoteLevyStolen(lost);
                    _loop?.NoteLevyStolen(lost, st.WorldPosition, fromHab: true);
                }
                set.Unregister(st.Category, st.IsVillageHab);
            }
            _structures.Remove(st);
            _loop?.NotifyStructureDestroyed(st);
        }

        public void OnStructureDestroyed(ColonyStructure st)
        {
            // OnDestroy also runs during travel, scene reload and application shutdown.
            // Combat loss is reported explicitly by NotifyCollapsed before destruction.
            // Treating teardown as a loss here rewrites saves with an emptying colony.
            if (st != null) _structures.Remove(st);
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

        public int TotalSittingLevy()
        {
            int n = 0;
            for (int i = 0; i < _structures.Count; i++)
            {
                var s = _structures[i];
                if (s != null && s.IsAlive)
                    n += s.LevyPurse;
            }

            return n;
        }

        public ColonyStructure NearestLevyStop(Vector3 from, float maxDist)
        {
            ColonyStructure best = null;
            float bestD = maxDist;
            int bestPurse = 0;
            for (int i = 0; i < _structures.Count; i++)
            {
                var s = _structures[i];
                if (s == null || !s.IsAlive || !s.IsResidential || s.LevyPurse <= 0) continue;
                float d = Flat(from, s.WorldPosition);
                if (d > bestD) continue;
                if (best != null && Mathf.Abs(d - bestD) < 0.5f && s.LevyPurse < bestPurse)
                    continue;
                best = s;
                bestD = d;
                bestPurse = s.LevyPurse;
            }

            return best;
        }

        public ColonyStructure NearestWatchtower(Vector3 from, float maxDist)
        {
            ColonyStructure best = null;
            float bestD = maxDist;
            for (int i = 0; i < _structures.Count; i++)
            {
                var s = _structures[i];
                if (s == null || !s.IsAlive || !s.IsWatchtower) continue;
                float d = Flat(from, s.WorldPosition);
                if (d < bestD)
                {
                    bestD = d;
                    best = s;
                }
            }

            return best;
        }

        public ColonyStructure NearestLevyChest(Vector3 from)
        {
            var commons = CommonsHub();
            var tower = NearestWatchtower(from, 120f);
            float dc = commons != null ? Flat(from, commons.WorldPosition) : -1f;
            float dt = tower != null ? Flat(from, tower.WorldPosition) : -1f;
            return LevyRun.PreferWatchtower(dc, dt) ? tower : commons;
        }

        /// <summary>Nearest standing building with gold in its till (pests rob these, not the treasury).</summary>
        public ColonyStructure NearestTill(Vector3 from, float maxDist)
        {
            ColonyStructure best = null;
            float bestD = maxDist;
            for (int i = 0; i < _structures.Count; i++)
            {
                var s = _structures[i];
                if (s == null || !s.IsAlive || s.LevyPurse <= 0 || s.IsTreasuryChest) continue;
                float d = Flat(from, s.WorldPosition);
                if (d < bestD)
                {
                    bestD = d;
                    best = s;
                }
            }
            return best;
        }

        public ColonyStructure CommonsHub()
        {
            for (int i = 0; i < _structures.Count; i++)
            {
                var s = _structures[i];
                if (s != null && s.IsAlive && s.Category == BuildingCategory.Commons)
                    return s;
            }

            return null;
        }

        public int DepositLevy(int total)
        {
            if (total <= 0) return 0;
            var stops = new System.Collections.Generic.List<ColonyStructure>(8);
            var weights = new System.Collections.Generic.List<int>(8);
            for (int i = 0; i < _structures.Count; i++)
            {
                var s = _structures[i];
                if (s == null || !s.IsAlive || !s.IsResidential) continue;
                stops.Add(s);
                weights.Add(1);
            }

            if (stops.Count == 0)
            {
                var commons = CommonsHub();
                if (commons == null) return 0;
                commons.AddLevy(total);
                return total;
            }

            int[] split = LevyRun.SplitByWeights(total, weights.ToArray());
            int used = 0;
            for (int i = 0; i < stops.Count && i < split.Length; i++)
            {
                stops[i].AddLevy(split[i]);
                used += split[i];
            }

            return used;
        }

        private void TickLevySit(float dt)
        {
            for (int i = 0; i < _structures.Count; i++)
            {
                var s = _structures[i];
                if (s == null || !s.IsAlive) continue;
                s.TickLevySit(dt);
                // Only house purses get nibbled by pests; shop and guild tills are locked up.
                if (!s.IsResidential) continue;
                if (s.LevyPurse <= 0 || s.LevySitSeconds < LevyRun.SitStealSeconds) continue;
                int stole = s.StealLevy(LevyRun.SitStealAmount);
                if (stole > 0)
                    _loop.NotifyLevyStolen(stole, s.DisplayName);
            }
        }

        private LevyCollectorDirector _collectors;

        public LevyCollectorDirector Collectors => _collectors ??= new LevyCollectorDirector(this, _loop);

        /// <summary>Alive structures of a category (Majesty duplicate pricing counts these).</summary>
        public int CountAlive(BuildingCategory cat)
        {
            int n = 0;
            for (int i = 0; i < _structures.Count; i++)
            {
                var s = _structures[i];
                if (s != null && s.IsAlive && s.Category == cat) n++;
            }
            return n;
        }


        /// <summary>
        /// Majesty daily tax: Commons (Palace) 50, Market 250, Farm 50 land in their own tills each
        /// day; mine output waits in the Mine's till. Houses are paid through the Settlement levy.
        /// </summary>
        /// <summary>Tax the standing buildings, houses included, accrue into their tills each day.</summary>
        public int DailyBuildingTax()
        {
            int total = 0;
            var set = _loop != null ? _loop.Settlement : null;
            for (int i = 0; i < _structures.Count; i++)
            {
                var s = _structures[i];
                if (s != null && s.IsAlive) total += DailyEnergy(s, set);
            }
            return total;
        }

        /// <summary>
        /// Energy a building puts in its till each day. Mines are the trade posts: their load is
        /// worth more the farther they stand from the Commons, and a collector must walk it home.
        /// </summary>
        private int DailyEnergy(ColonyStructure s, Settlement set)
        {
            if (s.Category == BuildingCategory.Mine)
            {
                var hub = CommonsHub();
                float d = hub != null ? Flat(s.WorldPosition, hub.WorldPosition) : 0f;
                float yield = set != null ? set.MineYieldScale : 1f;
                return Mathf.RoundToInt(MajestyEconomy.MineDailyEnergy(d) * yield);
            }
            int tax = MajestyEconomy.DailyTax(s.Category);
            if (s.Category == BuildingCategory.Farm && set != null)
                tax = Mathf.RoundToInt(tax * set.FarmTaxScale);
            return tax;
        }

        private void PayDailyTaxes(Settlement set)
        {
            _collectors ??= new LevyCollectorDirector(this, _loop);
            int days = set.TakePendingDays();
            for (int d = 0; d < days; d++)
            {
                for (int i = 0; i < _structures.Count; i++)
                {
                    var s = _structures[i];
                    if (s == null || !s.IsAlive) continue;
                    int tax = DailyEnergy(s, set);
                    if (tax > 0) s.AccrueLevy(tax);
                }
            }

            int campGold = set.TakePendingCampGold();
            if (campGold > 0)
            {
                var mines = new List<ColonyStructure>(4);
                for (int i = 0; i < _structures.Count; i++)
                {
                    var s = _structures[i];
                    if (s != null && s.IsAlive && s.Category == BuildingCategory.Mine) mines.Add(s);
                }
                if (mines.Count == 0)
                {
                    var hub = CommonsHub();
                    if (hub != null) hub.AccrueLevy(campGold);
                    else _loop.Resources?.Add(ResourceId.Metals, campGold);
                }
                else
                {
                    int each = campGold / mines.Count;
                    int rest = campGold - each * mines.Count;
                    for (int i = 0; i < mines.Count; i++)
                        mines[i].AccrueLevy(each + (i == 0 ? rest : 0));
                }
            }
        }

        public ColonyStructure NearestPower(Vector3 from, float maxDist) =>
            NearestByCategory(from, maxDist, BuildingCategory.Power);

        /// <summary>
        /// Prefer a matching drop-off (Mine for ore, Farm for ice, …) within haul range;
        /// otherwise the nearest pad / Commons / camp.
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

        /// <summary>
        /// Phase 4 still helper: dock one airlock + HAB onto Commons without spending stockpile.
        /// Prefer East/West/North (South hugs the Inn). Skips village Inn/CampusB distance filters
        /// that were rejecting every face on a fresh Mars drop. HAB continues in the same
        /// cardinal as the Commons dock — Opposite(face) sat the 4×4 on Commons and failed CanFit.
        /// </summary>
        public bool StampStillCampusChain()
        {
            if (_loop == null || _loop.Placer == null || _loop.Grid == null) return false;

            if (!TryFindStillSlot(out Vector2Int airlockCell, out Vector2Int habCell, out var face))
            {
                Debug.LogWarning(
                    $"[Village] StampStillCampusChain failed — pieces={_loop.Placer.Pieces.Count} " +
                    $"hasCommonsModule={_loop.Placer.HasCommonsModule}");
                return false;
            }

            SpawnConnector(airlockCell);
            SpawnHab(habCell);
            if (_loop.Settlement != null)
                _loop.Settlement.AddVillageHab();
            _loop.NotifyCampusExpanded();
            Debug.Log($"[Village] Still campus stamped — {face} airlock {airlockCell} HAB {habCell}");
            return true;
        }

        private bool TryFindStillSlot(
            out Vector2Int airlockCell,
            out Vector2Int habCell,
            out BuildingPlacer.Cardinal face)
        {
            airlockCell = default;
            habCell = default;
            face = BuildingPlacer.Cardinal.East;

            BuildingPlacer.Cardinal[] order =
            {
                BuildingPlacer.Cardinal.East,
                BuildingPlacer.Cardinal.West,
                BuildingPlacer.Cardinal.North,
                BuildingPlacer.Cardinal.South
            };

            var pieces = _loop.Placer.Pieces;
            for (int i = 0; i < pieces.Count; i++)
            {
                var module = pieces[i];
                if (module.Category != BuildingCategory.Commons && !module.IsModule)
                    continue;
                // Prefer Commons; allow any module as fallback.
                bool isCommons = module.Category == BuildingCategory.Commons;
                if (!isCommons && HasCommonsPiece(pieces))
                    continue;

                for (int f = 0; f < order.Length; f++)
                {
                    face = order[f];
                    BuildingPlacer.CardinalExpansionOrigins(module, face, 4, 4, out Vector2Int aCell, out Vector2Int hCell);
                    if (!_loop.Placer.CanFitRect(aCell, 2, 2))
                    {
                        Debug.Log(
                            $"[Village] StampStill skip {face}: airlock CanFit {aCell} " +
                            DescribeBlocked(aCell, 2, 2));
                        continue;
                    }
                    if (!_loop.Grid.InBounds(aCell) ||
                        !_loop.Grid.InBounds(new Vector2Int(aCell.x + 1, aCell.y + 1)))
                    {
                        Debug.Log($"[Village] StampStill skip {face}: airlock OOB {aCell}");
                        continue;
                    }

                    if (!_loop.Placer.CanFitRect(hCell, 4, 4))
                    {
                        Debug.Log(
                            $"[Village] StampStill skip {face}: HAB CanFit {hCell} " +
                            DescribeBlocked(hCell, 4, 4));
                        continue;
                    }
                    if (!_loop.Grid.InBounds(hCell) ||
                        !_loop.Grid.InBounds(new Vector2Int(hCell.x + 3, hCell.y + 3)))
                    {
                        Debug.Log($"[Village] StampStill skip {face}: HAB OOB {hCell}");
                        continue;
                    }

                    airlockCell = aCell;
                    habCell = hCell;
                    return true;
                }
            }

            return false;
        }

        private static bool HasCommonsPiece(System.Collections.Generic.IReadOnlyList<BuildingPlacer.CampusPiece> pieces)
        {
            for (int i = 0; i < pieces.Count; i++)
            {
                if (pieces[i].Category == BuildingCategory.Commons)
                    return true;
            }
            return false;
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
                    BuildingPlacer.CardinalExpansionOrigins(module, face, 4, 4, out Vector2Int aCell, out Vector2Int hCell);
                    if (!_loop.Placer.CanFitRect(aCell, 2, 2)) continue;
                    if (!_loop.Grid.InBounds(aCell) ||
                        !_loop.Grid.InBounds(new Vector2Int(aCell.x + 1, aCell.y + 1)))
                        continue;

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

        private string DescribeBlocked(Vector2Int origin, int width, int height)
        {
            if (_loop?.Placer == null)
                return "no placer";
            if (!_loop.Placer.TryFirstOccupiedCell(origin, width, height, out Vector2Int cell))
                return "no occupied cell";
            if (_loop.Placer.TryGetPieceAt(cell, out var piece))
                return $"occupied {cell} overlaps {piece.Category} {piece.Origin} {piece.Width}x{piece.Height}";
            return $"occupied {cell}";
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

        public int AccrueLevy(int total)
        {
            if (total <= 0) return 0;
            int n = 0;
            for (int i = 0; i < _structures.Count; i++)
            {
                var s = _structures[i];
                if (s == null || !s.IsAlive || !s.IsResidential || s.Residents <= 0) continue;
                n++;
            }

            if (n <= 0) return 0;

            var habs = new ColonyStructure[n];
            var residents = new int[n];
            var purses = new int[n];
            int w = 0;
            for (int i = 0; i < _structures.Count; i++)
            {
                var s = _structures[i];
                if (s == null || !s.IsAlive || !s.IsResidential || s.Residents <= 0) continue;
                habs[w] = s;
                residents[w] = s.Residents;
                purses[w] = s.LevyPurse;
                w++;
            }

            int given = LevyMath.Accrue(total, residents, purses);
            for (int i = 0; i < n; i++)
                habs[i].SetLevyPurse(purses[i]);
            return given;
        }

        public int SittingLevy()
        {
            int sum = 0;
            for (int i = 0; i < _structures.Count; i++)
            {
                var s = _structures[i];
                if (s == null || !s.IsAlive) continue;
                sum += s.LevyPurse;
            }
            return sum;
        }

        public ColonyStructure RichestLevyHab(Vector3 from)
        {
            ColonyStructure best = null;
            int bestPurse = 0;
            float bestD = 999f;
            for (int i = 0; i < _structures.Count; i++)
            {
                var s = _structures[i];
                if (s == null || !s.IsAlive || !s.IsResidential || s.LevyPurse <= 0) continue;
                float d = Flat(from, s.WorldPosition);
                if (s.LevyPurse > bestPurse || (s.LevyPurse == bestPurse && d < bestD))
                {
                    bestPurse = s.LevyPurse;
                    bestD = d;
                    best = s;
                }
            }

            return best;
        }

        public ColonyStructure NearestStaleLevyHab(Vector3 from, float maxDist)
        {
            ColonyStructure best = null;
            float bestD = maxDist;
            for (int i = 0; i < _structures.Count; i++)
            {
                var s = _structures[i];
                if (s == null || !s.LevyStale) continue;
                float d = Flat(from, s.WorldPosition);
                if (d < bestD)
                {
                    bestD = d;
                    best = s;
                }
            }

            return best;
        }

        public ColonyStructure NearestResidential(Vector3 from, float maxDist)
        {
            ColonyStructure best = null;
            float bestD = maxDist;
            for (int i = 0; i < _structures.Count; i++)
            {
                var s = _structures[i];
                if (s == null || !s.IsAlive || !s.IsResidential) continue;
                float d = Flat(from, s.WorldPosition);
                if (d < bestD)
                {
                    bestD = d;
                    best = s;
                }
            }

            return best;
        }

        /// <summary>
        /// Idle Courier: pick up a HAB purse or drop it at Commons. No player order.
        /// </summary>
        public bool TryCourierLevy(SpecialistAgent courier)
        {
            if (courier == null || _loop == null) return false;
            if (courier.IsIncapacitated) return false;
            Vector3 at = courier.transform.position;

            if (courier.LevyCarry <= 0)
            {
                ColonyStructure hab = null;
                float bestD = OverseerRules.LevyArrive;
                for (int i = 0; i < _structures.Count; i++)
                {
                    var s = _structures[i];
                    if (s == null || !s.IsAlive || !s.IsResidential || s.LevyPurse <= 0) continue;
                    float d = Flat(at, s.WorldPosition);
                    if (d < bestD)
                    {
                        bestD = d;
                        hab = s;
                    }
                }

                if (hab == null) return false;
                int take = hab.CollectLevy();
                if (take <= 0) return false;
                courier.AddLevyCarry(take);
                return true;
            }

            var commons = NearestByCategory(at, 80f, BuildingCategory.Commons);
            if (commons == null || !commons.IsAlive) return false;
            if (Flat(at, commons.WorldPosition) > OverseerRules.LevyArrive) return false;
            int carried = courier.TakeLevyCarry();
            if (carried <= 0) return false;
            _loop.NoteLevyDeposited(carried, commons.WorldPosition);
            return true;
        }

        private void TickLevyPurses(float dt)
        {
            for (int i = 0; i < _structures.Count; i++)
                _structures[i]?.TickLevy(dt);
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
