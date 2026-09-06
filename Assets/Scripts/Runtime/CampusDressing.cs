using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Phase 4 campus kit: pressurized tube cladding on the square Lego docks,
    /// shield bubbles — visuals only, not a pathing graph.
    /// HAB / Commons / pad / extractor / solar field / Defense bunker live in HeroBuildingKits.
    /// Junction turrets sit on airlock hubs (ColonyVisualUtility).
    /// Airlock hubs are panel-lined square primitives; docks stay Lego.
    /// Round tube cladding spans hub → module hull on a shared DockY / DockBore.
    /// RefreshTubes hides every stub and hull-drum port first, then enables docked faces only
    /// (white tube + one orange collar). Unused Commons / HAB / LAB / PWR sockets stay clean.
    /// Live dock sleeves and CommonsPort groups start off so FindPieceGo misses cannot
    /// leave still5-style orange rings showing. still16 dark-box leftover was wrap
    /// Dress_HubDoor on the hub itself (ColonyVisualUtility) — unused faces stay
    /// clean white plates. FindPieceGo walks VillageRing / Buildings so still-chain
    /// airlocks get docked collars. No CampusTubeRoot corridor is spawned in the
    /// HAB gap — still21 tube runs live under <see cref="TubeRunRootName"/> and
    /// skip airlock-linked pairs.
    /// </summary>
    public static class CampusDressing
    {
        private const int MaxProps = 96;
        private const string TubeRootName = "CampusTubeRoot";
        public const string TubeRunRootName = "CampusDress_TubeRuns";
        public const int MaxTubeRunGapCells = 8;

        private static int _count;
        private static Shader _lit;

        public static void Reset()
        {
            _count = 0;
            DestroyNamed(TubeRootName);
            DestroyNamed(TubeRunRootName);
        }

        private static void DestroyNamed(string name)
        {
            var existing = GameObject.Find(name);
            if (existing == null) return;
            existing.name = name + "_old";
            if (Application.isPlaying) Object.Destroy(existing);
            else Object.DestroyImmediate(existing);
        }

        public static void DressPlaced(BuildingData data, GameObject go, CelestialBodyProfile body)
        {
            if (data == null || go == null) return;

            if (data.category == BuildingCategory.Utility)
                return;

            if (data.category == BuildingCategory.Defense ||
                data.category == BuildingCategory.Commons)
                SpawnShieldBubble(go.transform, data.category == BuildingCategory.Commons);

            if (data.category == BuildingCategory.Commons ||
                data.category == BuildingCategory.Power ||
                data.category == BuildingCategory.Defense)
                SpawnStatusPip(go.transform, data.category);

            Vector3 origin = go.transform.position;
            // Dressing must die with the module — parent to the building, not the campus root.
            // Hoppers raiding a HAB used to leave an orange packed-dust disc behind.
            Transform parent = go.transform;
            if (data.category != BuildingCategory.Commons)
                SpawnApron(origin, body, parent, data.category);

            if (_count >= MaxProps) return;

            float yaw = _count * 1.618f * Mathf.PI;
            Vector3 offset = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw)) * 2.55f;
            Vector3 offsetB = new Vector3(Mathf.Cos(yaw + 2.1f), 0f, Mathf.Sin(yaw + 2.1f)) * 2.2f;

            SpawnCrate(origin + offset, body, parent);
            SpawnBarrel(origin + offsetB, body, parent);
            _count += 2;

            if (body != null && body.Kit == TerrainKit.IceCrust)
            {
                SpawnFrostSlab(origin + offset * 0.4f, body, parent);
                _count++;
            }
            else if (body != null && body.Kit == TerrainKit.AsteroidField)
            {
                SpawnOreChunk(origin - offset * 0.35f, body, parent);
                _count++;
            }

            if (data.category == BuildingCategory.LandingPad)
            {
                SpawnPylon(origin - offset * 0.55f, body, parent);
                SpawnBollard(origin + offset * 1.15f, body, parent);
                SpawnSpool(origin - offsetB * 0.9f, body, parent);
                _count += 3;
            }

            if (ColonyStructure.IsWorkshopCategory(data.category) ||
                data.category == BuildingCategory.Inn)
            {
                SpawnPallet(origin + offset * 0.72f, body, parent);
                SpawnSpool(origin - offsetB * 0.7f, body, parent);
                _count += 2;
            }

            if (data.category == BuildingCategory.Farm ||
                data.category == BuildingCategory.Mine ||
                data.category == BuildingCategory.RegolithCamp ||
                data.category == BuildingCategory.Mining)
            {
                SpawnBollard(origin - offset * 0.95f, body, parent);
                SpawnCone(origin + offsetB * 1.05f, body, parent);
                _count += 2;
            }

            if (data.category == BuildingCategory.Power)
            {
                SpawnSpool(origin + offset * 0.8f, body, parent);
                SpawnCone(origin - offsetB * 0.85f, body, parent);
                _count += 2;
            }

            if (data.category == BuildingCategory.Laboratory)
            {
                SpawnBollard(origin + offset * 0.88f, body, parent);
                SpawnCone(origin - offsetB * 0.92f, body, parent);
                _count += 2;
            }

            // Overview isometric: denser crates / cones on Mars only. Earth meadow stays sparse.
            if (body != null && body.Id == CelestialBodyId.Mars && _count < MaxProps - 3)
            {
                SpawnPallet(origin + offsetB * 1.25f, body, parent);
                SpawnCone(origin - offset * 1.2f, body, parent);
                _count += 2;
                if (data.category == BuildingCategory.LandingPad)
                {
                    SpawnCrate(origin - offsetB * 1.15f, body, parent);
                    _count++;
                }
            }
        }

        /// <summary>
        /// Corrugated corridor cladding spanning each airlock ↔ module dock.
        /// Dressing only — colliders stripped, NavMesh unchanged.
        /// </summary>
        public static void RefreshTubes(BuildingPlacer placer, IsoGrid grid, Transform parent)
        {
            DestroyNamed(TubeRootName);
            DestroyNamed(TubeRunRootName);
            if (placer == null || grid == null) return;

            var pieces = placer.Pieces;
            if (pieces == null || pieces.Count == 0) return;

            HideUnusedDockSleeves(placer, grid, parent);
            int runs = SpawnTubeRuns(placer, grid, parent);
            int docks = 0;
            for (int a = 0; a < pieces.Count; a++)
            {
                if (!pieces[a].IsAirlock) continue;
                for (int m = 0; m < pieces.Count; m++)
                {
                    if (!pieces[m].IsModule) continue;
                    for (int f = 0; f < 4; f++)
                    {
                        if (BuildingPlacer.AirlockOriginOnModuleFace(
                                pieces[m], (BuildingPlacer.Cardinal)f) == pieces[a].Origin)
                        {
                            docks++;
                            break;
                        }
                    }
                }
            }
            Debug.Log($"[CampusDressing] docks={docks} runs={runs} pieces={pieces.Count}");
        }

        /// <summary>
        /// still21 sparse cluster: white pressurized runs between cardinal neighbors
        /// that are not already airlock-docked. Dressing only — not a pathing graph,
        /// not a fourth CampusTubeRoot in the HAB join.
        /// </summary>
        private static int SpawnTubeRuns(BuildingPlacer placer, IsoGrid grid, Transform parent)
        {
            var pieces = placer.Pieces;
            if (pieces == null || pieces.Count < 2) return 0;

            var root = new GameObject(TubeRunRootName);
            if (parent != null) root.transform.SetParent(parent, false);

            int runs = 0;
            var seen = new HashSet<long>();
            for (int i = 0; i < pieces.Count; i++)
            {
                for (int j = i + 1; j < pieces.Count; j++)
                {
                    if (StillCampusDensity.AreAirlockLinked(placer, pieces[i], pieces[j]))
                        continue;
                    if (!StillCampusDensity.TryCardinalNeighbor(
                            pieces[i], pieces[j], MaxTubeRunGapCells, out _, out bool eastWest))
                        continue;
                    long key = TubePairKey(i, j);
                    if (!seen.Add(key)) continue;
                    if (!SpawnTubeRun(grid, pieces[i], pieces[j], eastWest, root.transform, runs))
                        continue;
                    runs++;
                }
            }

            if (runs == 0)
            {
                if (Application.isPlaying) Object.Destroy(root);
                else Object.DestroyImmediate(root);
            }
            return runs;
        }

        private static long TubePairKey(int a, int b) =>
            a < b ? ((long)a << 32) ^ (uint)b : ((long)b << 32) ^ (uint)a;

        private static bool SpawnTubeRun(
            IsoGrid grid,
            BuildingPlacer.CampusPiece a,
            BuildingPlacer.CampusPiece b,
            bool eastWest,
            Transform parent,
            int index)
        {
            Vector3 aC = PieceCenter(grid, a);
            Vector3 bC = PieceCenter(grid, b);
            if (eastWest)
            {
                aC.z = (aC.z + bC.z) * 0.5f;
                bC.z = aC.z;
            }
            else
            {
                aC.x = (aC.x + bC.x) * 0.5f;
                bC.x = aC.x;
            }

            Vector3 delta = bC - aC;
            delta.y = 0f;
            float dist = delta.magnitude;
            if (dist < 1.15f) return false;

            // Stay off hulls so this is a corridor, not a stacked HAB-gap collar.
            float inset = Mathf.Min(1.65f, dist * 0.28f);
            float len = dist - inset * 2f;
            if (len < 1.05f) return false;

            Vector3 dir = delta / dist;
            Vector3 mid = (aC + bC) * 0.5f + new Vector3(0f, ColonyVisualUtility.DockY, 0f);
            Quaternion rot = Quaternion.LookRotation(dir) * Quaternion.Euler(90f, 0f, 0f);
            float bore = ColonyVisualUtility.DockBore * 0.78f;

            var group = new GameObject("Dress_CampusTubeRun_" + index);
            group.transform.SetParent(parent, false);
            group.transform.position = mid;

            var tube = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tube.name = group.name + "_Tube";
            tube.transform.SetParent(group.transform, false);
            tube.transform.localPosition = Vector3.zero;
            tube.transform.localRotation = rot;
            tube.transform.localScale = new Vector3(bore, len * 0.5f, bore);
            Object.Destroy(tube.GetComponent<Collider>());
            Tint(tube, new Color(0.97f, 0.97f, 0.99f), 0.42f, new Color(0.22f, 0.22f, 0.24f));

            var rib = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rib.name = group.name + "_Rib";
            rib.transform.SetParent(group.transform, false);
            rib.transform.localPosition = Vector3.zero;
            rib.transform.localRotation = rot;
            rib.transform.localScale = new Vector3(bore * 1.08f, 0.04f, bore * 1.08f);
            Object.Destroy(rib.GetComponent<Collider>());
            Tint(rib, new Color(0.12f, 0.12f, 0.13f), 0.22f);

            Vector3 half = dir * (len * 0.5f);
            SpawnTubeRunCollar(group.transform, group.name + "_CollarA", mid - half, rot, bore);
            SpawnTubeRunCollar(group.transform, group.name + "_CollarB", mid + half, rot, bore);
            return true;
        }

        private static void SpawnTubeRunCollar(
            Transform parent, string name, Vector3 world, Quaternion rot, float bore)
        {
            var collar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            collar.name = name;
            collar.transform.SetParent(parent, true);
            collar.transform.position = world;
            collar.transform.rotation = rot;
            collar.transform.localScale = new Vector3(bore * 1.28f, 0.07f, bore * 1.28f);
            Object.Destroy(collar.GetComponent<Collider>());
            Tint(collar, new Color(0.96f, 0.42f, 0.08f), 0.32f, new Color(0.45f, 0.14f, 0.02f));
        }

        private static Vector3 PieceCenter(IsoGrid grid, BuildingPlacer.CampusPiece piece)
        {
            Vector3 a = grid.CellToWorld(piece.Origin);
            Vector3 b = grid.CellToWorld(piece.Origin + new Vector2Int(piece.Width - 1, piece.Height - 1));
            return (a + b) * 0.5f;
        }

        /// <summary>
        /// Group-root names RefreshTubes toggles. still5 leftover orange rings were
        /// CommonsPort / HabPort hull-drum collars — not only Dress_TubeArm / DockSleeve
        /// / CommonsStub. Children (_Tube / _Ring / _Well / _Collar) stay parented;
        /// toggling them independently leaves Unity activeSelf stuck off when the group
        /// turns back on.
        /// </summary>
        public static bool IsDockDressName(string n)
        {
            if (string.IsNullOrEmpty(n)) return false;
            return n.StartsWith("Dress_TubeArm")
                || n.StartsWith("DockSleeve")
                || n.StartsWith("CommonsStub")
                || n.StartsWith("CommonsPort")
                || n.StartsWith("HabPort")
                || n.StartsWith("LabPort")
                || n.StartsWith("PwrPort")
                || n.StartsWith("HullPort")
                || n.StartsWith("DockPort")
                || n.StartsWith("DrumPort")
                || n.StartsWith("ModulePort")
                || n.StartsWith("CardinalPort");
        }

        public static bool IsDockDressRoot(Transform t)
        {
            if (t == null || !IsDockDressName(t.name)) return false;
            Transform p = t.parent;
            return p == null || !IsDockDressName(p.name);
        }

        public static void SetLiveDockDressActive(Transform root, bool on)
        {
            if (root == null) return;
            var ts = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < ts.Length; i++)
            {
                Transform t = ts[i];
                if (t == null || t == root) continue;
                if (IsDockDressRoot(t))
                    t.gameObject.SetActive(on);
            }
        }

        /// <summary>
        /// Unused cardinal sleeves read as orange hatches / leftover tubes. Hide every
        /// stub first, then enable only faces that actually dock. Find-misses leave
        /// unused arms off so the white square hub can read.
        /// </summary>
        private static void HideUnusedDockSleeves(BuildingPlacer placer, IsoGrid grid, Transform parent)
        {
            if (placer == null || grid == null || parent == null) return;
            var pieces = placer.Pieces;
            if (pieces == null) return;

            HideAllDockDress(parent);

            for (int m = 0; m < pieces.Count; m++)
            {
                var module = pieces[m];
                if (!module.IsModule) continue;

                bool east = false, west = false, north = false, south = false;
                for (int a = 0; a < pieces.Count; a++)
                {
                    var airlock = pieces[a];
                    if (!airlock.IsAirlock) continue;
                    if (BuildingPlacer.AirlockOriginOnModuleFace(module, BuildingPlacer.Cardinal.East) == airlock.Origin)
                        east = true;
                    if (BuildingPlacer.AirlockOriginOnModuleFace(module, BuildingPlacer.Cardinal.West) == airlock.Origin)
                        west = true;
                    if (BuildingPlacer.AirlockOriginOnModuleFace(module, BuildingPlacer.Cardinal.North) == airlock.Origin)
                        north = true;
                    if (BuildingPlacer.AirlockOriginOnModuleFace(module, BuildingPlacer.Cardinal.South) == airlock.Origin)
                        south = true;
                }

                GameObject go = FindPieceGo(parent, PieceCenter(grid, module), ModuleHint(module.Category));
                if (go == null)
                    go = FindPieceGo(parent, PieceCenter(grid, module), null);
                if (go == null) continue;
                SetPrefixActive(go.transform, "DockSleeve_E", east);
                SetPrefixActive(go.transform, "DockSleeve_W", west);
                SetPrefixActive(go.transform, "DockSleeve_N", north);
                SetPrefixActive(go.transform, "DockSleeve_S", south);
                // Hull drum ports + DockSleeve: white tube + one orange collar on
                // docked faces only. still5 unused Commons rings were CommonsPort_*.
                SetDockPorts(go.transform, "CommonsPort", north, east, south, west);
                SetDockPorts(go.transform, "HabPort", north, east, south, west);
                SetDockPorts(go.transform, "LabPort", north, east, south, west);
                SetDockPorts(go.transform, "PwrPort", north, east, south, west);
                SetDockPorts(go.transform, "HullPort", north, east, south, west);
                SetDockPorts(go.transform, "DockPort", north, east, south, west);
                SetDockPorts(go.transform, "DrumPort", north, east, south, west);
                SetDockPorts(go.transform, "ModulePort", north, east, south, west);
                SetDockPorts(go.transform, "CardinalPort", north, east, south, west);
            }

            for (int a = 0; a < pieces.Count; a++)
            {
                var airlock = pieces[a];
                if (!airlock.IsAirlock) continue;

                bool east = false, west = false, north = false, south = false;
                for (int m = 0; m < pieces.Count; m++)
                {
                    var module = pieces[m];
                    if (!module.IsModule) continue;
                    if (BuildingPlacer.ModuleOriginOnAirlockFace(
                            airlock, module.Width, module.Height, BuildingPlacer.Cardinal.East) == module.Origin)
                        east = true;
                    if (BuildingPlacer.ModuleOriginOnAirlockFace(
                            airlock, module.Width, module.Height, BuildingPlacer.Cardinal.West) == module.Origin)
                        west = true;
                    if (BuildingPlacer.ModuleOriginOnAirlockFace(
                            airlock, module.Width, module.Height, BuildingPlacer.Cardinal.North) == module.Origin)
                        north = true;
                    if (BuildingPlacer.ModuleOriginOnAirlockFace(
                            airlock, module.Width, module.Height, BuildingPlacer.Cardinal.South) == module.Origin)
                        south = true;
                }

                GameObject go = FindPieceGo(parent, PieceCenter(grid, airlock), "Airlock");
                if (go == null)
                    go = FindPieceGo(parent, PieceCenter(grid, airlock), "Junction");
                if (go == null)
                    go = FindPieceGo(parent, PieceCenter(grid, airlock), "PlusConnector");
                if (go == null) continue;
                SetPrefixActive(go.transform, "Dress_TubeArm_E", east);
                SetPrefixActive(go.transform, "Dress_TubeArm_W", west);
                SetPrefixActive(go.transform, "Dress_TubeArm_N", north);
                SetPrefixActive(go.transform, "Dress_TubeArm_S", south);
            }
        }

        private static string ModuleHint(BuildingCategory cat)
        {
            switch (cat)
            {
                case BuildingCategory.Commons: return "Commons";
                case BuildingCategory.Habitat: return "Habitat";
                case BuildingCategory.Laboratory: return "Lab";
                case BuildingCategory.Power: return "Power";
                default: return null;
            }
        }

        private static void HideAllDockDress(Transform parent)
        {
            SetLiveDockDressActive(parent, false);
        }

        private static GameObject FindPieceGo(Transform parent, Vector3 center, string preferContains)
        {
            GameObject best = null;
            float maxSq = 8f * 8f;
            float bestSq = maxSq;
            int bestRank = 0;
            var roots = new List<Transform>(32);
            CollectPieceRoots(parent, roots);
            for (int i = 0; i < roots.Count; i++)
            {
                Transform t = roots[i];
                if (t == null) continue;
                string n = t.name;
                float dx = t.position.x - center.x;
                float dz = t.position.z - center.z;
                float sq = dx * dx + dz * dz;
                if (sq > maxSq) continue;
                if (!string.IsNullOrEmpty(preferContains) &&
                    n.IndexOf(preferContains, System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                int rank = PieceRank(n);
                if (best == null || rank > bestRank || (rank == bestRank && sq < bestSq))
                {
                    best = t.gameObject;
                    bestSq = sq;
                    bestRank = rank;
                }
            }
            return best;
        }

        /// <summary>
        /// Still-chain airlock/HAB live under VillageRing (sibling of Buildings).
        /// Walk those containers so RefreshTubes can enable docked collars.
        /// </summary>
        private static void CollectPieceRoots(Transform parent, List<Transform> dest)
        {
            if (parent == null || dest == null) return;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform t = parent.GetChild(i);
                if (t == null) continue;
                string n = t.name;
                if (n == "VillageRing" || n == "Buildings" || n.StartsWith("CampusDress"))
                {
                    CollectPieceRoots(t, dest);
                    continue;
                }
                if (n == "IsoGrid" || n == "Flags" || n == "SpecialistSpawn" || n == "Main Camera")
                    continue;
                if (n.StartsWith("CampusTube") || n.StartsWith("DropZone") || n.StartsWith("Dress_") ||
                    n.StartsWith("Site_") || n.StartsWith("Ghost") || n.StartsWith("Claim"))
                    continue;
                dest.Add(t);
            }
        }

        private static int PieceRank(string n)
        {
            if (n.StartsWith("Bld_") || n.StartsWith("Airlock") || n.StartsWith("Mod_") ||
                n.StartsWith("PlusConnector") || n.StartsWith("VillageAirlock") ||
                n.StartsWith("VillageHAB"))
                return 3;
            if (n.Contains("Commons") || n.Contains("Habitat") || n.Contains("Junction") ||
                n.Contains("Airlock"))
                return 2;
            return 1;
        }

        private static void SetDockPorts(Transform root, string prefix, bool north, bool east, bool south, bool west)
        {
            SetPrefixActive(root, prefix + "_N", north);
            SetPrefixActive(root, prefix + "_E", east);
            SetPrefixActive(root, prefix + "_S", south);
            SetPrefixActive(root, prefix + "_W", west);
        }

        private static void SetPrefixActive(Transform root, string prefix, bool on)
        {
            var ts = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < ts.Length; i++)
            {
                Transform t = ts[i];
                if (t == null || t == root) continue;
                if (t.name.StartsWith(prefix) && IsDockDressRoot(t))
                    t.gameObject.SetActive(on);
            }
        }

        private static void SpawnShieldBubble(Transform root, bool commons)
        {
            var bubble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bubble.name = "Dress_Shield";
            bubble.transform.SetParent(root, false);
            bubble.transform.localPosition = new Vector3(0f, commons ? 2.4f : 1.6f, 0f);
            bubble.transform.localScale = commons
                ? new Vector3(8.6f, 5.2f, 8.6f)
                : new Vector3(5.4f, 3.2f, 5.4f);
            Object.Destroy(bubble.GetComponent<Collider>());
            var rend = bubble.GetComponent<Renderer>();
            if (rend == null) return;
            var mat = NewLit("SM_DressShield");
            var c = new Color(0.35f, 0.72f, 1f, 0.16f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_Color")) mat.color = c;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.82f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.05f);
            ColonyVisualUtility.ApplyTransparent(mat);
            rend.sharedMaterial = mat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>
        /// Packed-dust apron under modules so campus reads as flattened paths vs wild regolith.
        /// Visual only — colliders stripped.
        /// </summary>
        private static void SpawnApron(
            Vector3 origin, CelestialBodyProfile body, Transform parent, BuildingCategory cat)
        {
            bool mars = body != null && body.Id == CelestialBodyId.Mars;
            float dia = cat == BuildingCategory.LandingPad ? 6.4f
                : cat == BuildingCategory.Commons ? 5.1f
                : 4.2f;
            if (mars && cat != BuildingCategory.Commons)
                dia *= 1.12f;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Dress_Apron";
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.position = origin + Vector3.up * 0.03f;
            go.transform.localScale = new Vector3(dia, 0.025f, dia);
            Object.Destroy(go.GetComponent<Collider>());
            Color packed = body != null
                ? Color.Lerp(body.GroundDark, body.GroundLight, 0.18f) * 0.82f
                : new Color(0.18f, 0.18f, 0.19f);
            Tint(go, packed, 0.05f);
        }

        /// <summary>
        /// Mockup floating status pip (star/shield language). Dressing only — not selectable.
        /// </summary>
        private static void SpawnStatusPip(Transform root, BuildingCategory cat)
        {
            bool commons = cat == BuildingCategory.Commons;
            var pip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pip.name = commons ? "Dress_StatusStar" : "Dress_StatusShield";
            pip.transform.SetParent(root, false);
            pip.transform.localPosition = new Vector3(0f, commons ? 5.85f : 3.35f, 0f);
            pip.transform.localScale = new Vector3(0.32f, 0.32f, 0.32f);
            Object.Destroy(pip.GetComponent<Collider>());
            Color glow = commons
                ? new Color(0.95f, 0.78f, 0.22f)
                : new Color(0.28f, 0.72f, 1f);
            Tint(pip, glow, 0.62f, glow * 1.6f);

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Dress_StatusRing";
            ring.transform.SetParent(pip.transform, false);
            ring.transform.localPosition = Vector3.zero;
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            ring.transform.localScale = new Vector3(1.55f, 0.08f, 1.55f);
            Object.Destroy(ring.GetComponent<Collider>());
            Tint(ring, glow, 0.45f, glow * 0.8f);
        }

        private static void SpawnCone(Vector3 world, CelestialBodyProfile body, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Dress_Cone";
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.position = world + Vector3.up * 0.28f;
            go.transform.localScale = new Vector3(0.18f, 0.26f, 0.18f);
            Object.Destroy(go.GetComponent<Collider>());
            Tint(go, body != null
                ? Color.Lerp(new Color(0.96f, 0.42f, 0.08f), body.SunColor, 0.1f)
                : new Color(0.96f, 0.42f, 0.08f), 0.22f);
            ColonyVisualUtility.SnapToGround(go);

            var cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cap.name = "Dress_ConeCap";
            cap.transform.SetParent(go.transform, false);
            cap.transform.localPosition = new Vector3(0f, 1.05f, 0f);
            cap.transform.localScale = new Vector3(0.55f, 0.35f, 0.55f);
            Object.Destroy(cap.GetComponent<Collider>());
            Tint(cap, new Color(0.92f, 0.93f, 0.94f));
        }

        private static void SpawnCrate(Vector3 world, CelestialBodyProfile body, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Dress_Crate";
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.position = world + Vector3.up * 0.28f;
            go.transform.localScale = new Vector3(0.55f, 0.42f, 0.7f);
            go.transform.rotation = Quaternion.Euler(0f, world.x * 17f, 0f);
            Object.Destroy(go.GetComponent<Collider>());
            Tint(go, body != null
                ? Color.Lerp(new Color(0.78f, 0.8f, 0.82f), body.RockColor, body.Kit == TerrainKit.AsteroidField ? 0.55f : 0.35f)
                : new Color(0.78f, 0.8f, 0.82f));
            ColonyVisualUtility.SnapToGround(go);
        }

        private static void SpawnBarrel(Vector3 world, CelestialBodyProfile body, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Dress_Barrel";
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.position = world + Vector3.up * 0.38f;
            go.transform.localScale = new Vector3(0.38f, 0.36f, 0.38f);
            Object.Destroy(go.GetComponent<Collider>());
            Tint(go, body != null
                ? Color.Lerp(new Color(0.88f, 0.90f, 0.92f), body.RockColor, 0.22f)
                : new Color(0.88f, 0.90f, 0.92f));
            ColonyVisualUtility.SnapToGround(go);

            var band = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            band.name = "Dress_BarrelBand";
            band.transform.SetParent(go.transform, false);
            band.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            band.transform.localScale = new Vector3(1.12f, 0.12f, 1.12f);
            Object.Destroy(band.GetComponent<Collider>());
            Tint(band, new Color(0.96f, 0.42f, 0.08f));
        }

        private static void SpawnSpool(Vector3 world, CelestialBodyProfile body, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Dress_Spool";
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.position = world + Vector3.up * 0.22f;
            go.transform.localScale = new Vector3(0.72f, 0.16f, 0.72f);
            go.transform.rotation = Quaternion.Euler(90f, world.z * 13f, 0f);
            Object.Destroy(go.GetComponent<Collider>());
            Tint(go, new Color(0.12f, 0.12f, 0.13f), 0.22f);
            ColonyVisualUtility.SnapToGround(go);

            var hub = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            hub.name = "Dress_SpoolHub";
            hub.transform.SetParent(go.transform, false);
            hub.transform.localPosition = Vector3.zero;
            hub.transform.localScale = new Vector3(0.42f, 1.4f, 0.42f);
            Object.Destroy(hub.GetComponent<Collider>());
            Tint(hub, new Color(0.96f, 0.42f, 0.08f));
        }

        private static void SpawnPallet(Vector3 world, CelestialBodyProfile body, Transform parent)
        {
            var deck = GameObject.CreatePrimitive(PrimitiveType.Cube);
            deck.name = "Dress_Pallet";
            if (parent != null) deck.transform.SetParent(parent, true);
            deck.transform.position = world + Vector3.up * 0.08f;
            deck.transform.localScale = new Vector3(1.05f, 0.08f, 0.72f);
            deck.transform.rotation = Quaternion.Euler(0f, world.x * 11f, 0f);
            Object.Destroy(deck.GetComponent<Collider>());
            Tint(deck, new Color(0.18f, 0.18f, 0.19f), 0.18f);

            var crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crate.name = "Dress_PalletCrate";
            crate.transform.SetParent(deck.transform, false);
            crate.transform.localPosition = new Vector3(0f, 2.8f, 0f);
            crate.transform.localScale = new Vector3(0.72f, 4.2f, 0.78f);
            Object.Destroy(crate.GetComponent<Collider>());
            Tint(crate, new Color(0.86f, 0.87f, 0.89f));
            ColonyVisualUtility.SnapToGround(deck);
        }

        private static void SpawnBollard(Vector3 world, CelestialBodyProfile body, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Dress_Bollard";
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.position = world + Vector3.up * 0.38f;
            go.transform.localScale = new Vector3(0.14f, 0.36f, 0.14f);
            Object.Destroy(go.GetComponent<Collider>());
            Tint(go, new Color(0.08f, 0.08f, 0.09f), 0.22f);
            ColonyVisualUtility.SnapToGround(go);

            var lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lamp.name = "Dress_BollardLamp";
            lamp.transform.SetParent(go.transform, false);
            lamp.transform.localPosition = new Vector3(0f, 1.05f, 0f);
            lamp.transform.localScale = new Vector3(1.15f, 0.42f, 1.15f);
            Object.Destroy(lamp.GetComponent<Collider>());
            Color glow = new Color(0.22f, 0.84f, 0.98f);
            Tint(lamp, glow, 0.55f, glow * 1.2f);
        }

        private static void SpawnFrostSlab(Vector3 world, CelestialBodyProfile body, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Dress_Frost";
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.position = world + Vector3.up * 0.08f;
            go.transform.localScale = new Vector3(1.15f, 0.08f, 0.85f);
            go.transform.rotation = Quaternion.Euler(0f, world.z * 11f, 0f);
            Object.Destroy(go.GetComponent<Collider>());
            Tint(go, Color.Lerp(new Color(0.78f, 0.9f, 0.96f), body != null ? body.GroundLight : Color.cyan, 0.35f));
            ColonyVisualUtility.SnapToGround(go);
        }

        private static void SpawnOreChunk(Vector3 world, CelestialBodyProfile body, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Dress_Ore";
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.position = world + Vector3.up * 0.22f;
            go.transform.localScale = new Vector3(0.48f, 0.32f, 0.42f);
            Object.Destroy(go.GetComponent<Collider>());
            Tint(go, body != null ? Color.Lerp(body.RockColor, new Color(0.55f, 0.42f, 0.28f), 0.4f) : new Color(0.4f, 0.32f, 0.22f));
            ColonyVisualUtility.SnapToGround(go);
        }

        private static void SpawnPylon(Vector3 world, CelestialBodyProfile body, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Dress_Pylon";
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.position = world + Vector3.up * 0.55f;
            go.transform.localScale = new Vector3(0.18f, 0.55f, 0.18f);
            Object.Destroy(go.GetComponent<Collider>());
            Tint(go, body != null && body.Kit == TerrainKit.IceCrust
                ? new Color(0.45f, 0.82f, 0.95f)
                : new Color(0.96f, 0.42f, 0.08f));
            ColonyVisualUtility.SnapToGround(go);

            var cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cap.name = "Beacon";
            cap.transform.SetParent(go.transform, false);
            cap.transform.localPosition = new Vector3(0f, 1.05f, 0f);
            cap.transform.localScale = new Vector3(1.4f, 0.45f, 1.4f);
            Object.Destroy(cap.GetComponent<Collider>());
            Color glow = body != null
                ? Color.Lerp(new Color(0.96f, 0.42f, 0.08f), body.SunColor, 0.25f)
                : new Color(0.96f, 0.42f, 0.08f);
            Tint(cap, glow, 0.4f, glow);
        }

        /// <summary>
        /// Empty-drop claim: carbon disc, orange/cyan chevrons, bollards.
        /// Visual only — placement still uses the soft claim in BuildingPlacer.
        /// </summary>
        public static GameObject DressClaimDisc(
            Transform parent, Vector3 world, string objectName, Color accent, float diameter)
        {
            var root = new GameObject(objectName);
            if (parent != null) root.transform.SetParent(parent, true);
            root.transform.position = world;

            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "ClaimDisc";
            disc.transform.SetParent(root.transform, false);
            disc.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            disc.transform.localScale = new Vector3(diameter, 0.04f, diameter);
            Object.Destroy(disc.GetComponent<Collider>());
            Tint(disc, new Color(0.10f, 0.11f, 0.12f), 0.18f);

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "ClaimRing";
            ring.transform.SetParent(root.transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.07f, 0f);
            ring.transform.localScale = new Vector3(diameter * 1.06f, 0.02f, diameter * 1.06f);
            Object.Destroy(ring.GetComponent<Collider>());
            Tint(ring, accent, 0.32f, accent * 0.35f);

            var inner = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            inner.name = "ClaimInner";
            inner.transform.SetParent(root.transform, false);
            inner.transform.localPosition = new Vector3(0f, 0.075f, 0f);
            inner.transform.localScale = new Vector3(diameter * 0.42f, 0.018f, diameter * 0.42f);
            Object.Destroy(inner.GetComponent<Collider>());
            Tint(inner, Color.Lerp(new Color(0.88f, 0.90f, 0.93f), accent, 0.15f), 0.22f);

            for (int i = 0; i < 4; i++)
            {
                float ang = i * 90f * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang));
                var chevron = GameObject.CreatePrimitive(PrimitiveType.Cube);
                chevron.name = "ClaimChevron_" + i;
                chevron.transform.SetParent(root.transform, false);
                chevron.transform.localPosition = dir * (diameter * 0.28f) + new Vector3(0f, 0.09f, 0f);
                chevron.transform.localRotation = Quaternion.Euler(0f, i * 90f, 0f);
                chevron.transform.localScale = new Vector3(0.22f, 0.03f, diameter * 0.18f);
                Object.Destroy(chevron.GetComponent<Collider>());
                Tint(chevron, accent, 0.28f);

                var bollard = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                bollard.name = "ClaimBollard_" + i;
                bollard.transform.SetParent(root.transform, false);
                bollard.transform.localPosition = dir * (diameter * 0.48f) + new Vector3(0f, 0.42f, 0f);
                bollard.transform.localScale = new Vector3(0.16f, 0.38f, 0.16f);
                Object.Destroy(bollard.GetComponent<Collider>());
                Tint(bollard, new Color(0.08f, 0.08f, 0.09f), 0.22f);

                var lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                lamp.name = "ClaimLamp_" + i;
                lamp.transform.SetParent(root.transform, false);
                lamp.transform.localPosition = dir * (diameter * 0.48f) + new Vector3(0f, 0.82f, 0f);
                lamp.transform.localScale = new Vector3(0.18f, 0.12f, 0.18f);
                Object.Destroy(lamp.GetComponent<Collider>());
                var glow = Color.Lerp(accent, new Color(0.22f, 0.84f, 0.98f), 0.35f);
                Tint(lamp, glow, 0.55f, glow * 1.4f);
            }

            ColonyVisualUtility.SnapToGround(root);
            return root;
        }

        /// <summary>
        /// still19 density cue: under-construction HAB socket (white foundation +
        /// yellow crane + incomplete cladding). Visual only — caller occupies the cells.
        /// </summary>
        public static GameObject SpawnStillHabSocket(Vector3 world, Transform parent)
        {
            var root = new GameObject("Site_StillHabSocket");
            if (parent != null) root.transform.SetParent(parent, true);
            root.transform.position = world;

            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "SocketDisc";
            disc.transform.SetParent(root.transform, false);
            disc.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            disc.transform.localScale = new Vector3(5.1f, 0.05f, 5.1f);
            Object.Destroy(disc.GetComponent<Collider>());
            Tint(disc, new Color(0.94f, 0.94f, 0.96f), 0.18f);

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "SocketRing";
            ring.transform.SetParent(root.transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.10f, 0f);
            ring.transform.localScale = new Vector3(5.35f, 0.03f, 5.35f);
            Object.Destroy(ring.GetComponent<Collider>());
            Tint(ring, new Color(0.96f, 0.42f, 0.08f), 0.28f);

            var rib = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rib.name = "SocketFrame";
            rib.transform.SetParent(root.transform, false);
            rib.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            rib.transform.localScale = new Vector3(3.4f, 0.08f, 3.4f);
            Object.Destroy(rib.GetComponent<Collider>());
            Tint(rib, new Color(0.18f, 0.18f, 0.19f), 0.22f);

            for (int i = 0; i < 4; i++)
            {
                float ang = i * 90f * Mathf.Deg2Rad;
                var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                panel.name = "SocketClad_" + i;
                panel.transform.SetParent(root.transform, false);
                panel.transform.localPosition = new Vector3(Mathf.Cos(ang) * 1.55f, 1.05f, Mathf.Sin(ang) * 1.55f);
                panel.transform.localScale = new Vector3(0.08f, 1.7f, 1.55f);
                panel.transform.localRotation = Quaternion.Euler(0f, -i * 90f, 0f);
                Object.Destroy(panel.GetComponent<Collider>());
                Tint(panel, i % 2 == 0 ? new Color(0.86f, 0.87f, 0.89f) : new Color(0.12f, 0.12f, 0.13f));
            }

            var mast = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mast.name = "SocketCraneMast";
            mast.transform.SetParent(root.transform, false);
            mast.transform.localPosition = new Vector3(1.7f, 2.15f, 1.3f);
            mast.transform.localScale = new Vector3(0.16f, 4.2f, 0.16f);
            Object.Destroy(mast.GetComponent<Collider>());
            Tint(mast, new Color(0.95f, 0.82f, 0.12f), 0.32f);

            var jib = GameObject.CreatePrimitive(PrimitiveType.Cube);
            jib.name = "SocketCraneJib";
            jib.transform.SetParent(root.transform, false);
            jib.transform.localPosition = new Vector3(0.15f, 4.15f, 1.3f);
            jib.transform.localScale = new Vector3(3.1f, 0.12f, 0.16f);
            Object.Destroy(jib.GetComponent<Collider>());
            Tint(jib, new Color(0.95f, 0.82f, 0.12f), 0.32f);

            ColonyVisualUtility.SnapToGround(root);
            return root;
        }

        private static void Tint(GameObject go, Color c, float smooth = 0.28f, Color emission = default)
        {
            var rend = go.GetComponent<Renderer>();
            if (rend == null) return;
            var mat = NewLit(go.name);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            else if (mat.HasProperty("_Color")) mat.color = c;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smooth);
            if (emission.maxColorComponent > 0.01f && mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission);
            }
            rend.sharedMaterial = mat;
        }

        private static Material NewLit(string name)
        {
            if (_lit == null)
                _lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default");
            return new Material(_lit) { name = name };
        }
    }
}
