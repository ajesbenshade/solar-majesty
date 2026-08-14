using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SolarMajesty
{
    /// <summary>
    /// Seeded procedural world contents outside campus footprints.
    /// Driven by <see cref="CelestialBodyProfile"/> so each body keeps its kit
    /// (Earth hydrology, Luna craters, Mars dunes, Belt islets, Europa ice).
    /// </summary>
    public class PlanetaryWorldGen : MonoBehaviour
    {
        private readonly List<ResourceNode> _nodes = new List<ResourceNode>(16);
        private readonly List<StalkerLair> _lairs = new List<StalkerLair>(8);
        private Transform _worldRoot;
        private GameLoop _loop;
        private IsoGrid _grid;
        private CelestialBodyProfile _body;

        public CelestialBodyProfile Body => _body;
        public IReadOnlyList<ResourceNode> Nodes => _nodes;
        public IReadOnlyList<StalkerLair> Lairs => _lairs;
        public int UnclearedLairCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _lairs.Count; i++)
                    if (_lairs[i] != null && !_lairs[i].IsCleared) n++;
                return n;
            }
        }

        public void Generate(GameLoop loop, IsoGrid grid, int seed) =>
            Generate(loop, grid, seed, _body ?? CelestialBodyCatalog.Luna());

        public void Generate(GameLoop loop, IsoGrid grid, int seed, CelestialBodyProfile body)
        {
            _loop = loop;
            _grid = grid;
            _body = body ?? CelestialBodyCatalog.Luna();
            _nodes.Clear();
            _lairs.Clear();

            if (_worldRoot != null)
                Destroy(_worldRoot.gameObject);

            _worldRoot = new GameObject($"World_{_body.ShortCode}").transform;
            _worldRoot.SetParent(transform, false);

            var rng = new System.Random(seed);
            var placed = new List<Vector3>(256)
            {
                ColonyLayout.CampusOrigin,
                ColonyLayout.CampusBOrigin,
                ColonyLayout.PartySpawn,
                ColonyLayout.PartySpawnB,
                ColonyLayout.InnOutpost
            };

            SpawnCraters(rng, placed);
            SpawnLakes(rng, placed);
            SpawnRivers(rng, placed);
            SpawnForestPatches(rng, placed);
            SpawnDunes(rng, placed);
            SpawnAsteroidIslets(rng, placed);
            SpawnIcePlates(rng, placed);
            SpawnRocks(rng, placed);
            SpawnResourceNodes(rng, placed);
            SpawnLairs(rng, placed);

            Debug.Log(
                $"[WorldGen] {_body.DisplayName} seed={seed} " +
                $"craters={_body.CraterCount} lakes={_body.LakeCount} rivers={_body.RiverCount} " +
                $"forests={_body.ForestPatchCount} dunes={_body.DuneCount} rocks={_body.RockCount} " +
                $"nodes={_nodes.Count} lairs={_lairs.Count}");
        }

        public void SpawnLairStalkers(Transform threatParent)
        {
            for (int i = 0; i < _lairs.Count; i++)
                _lairs[i]?.SpawnInitial(threatParent);
        }

        public void TickLairs(int campusPieces = 0)
        {
            for (int i = 0; i < _lairs.Count; i++)
                _lairs[i]?.Tick(campusPieces);
        }

        public ResourceNode FindNearestNode(Vector3 world, float maxDist = 10f)
        {
            ResourceNode best = null;
            float bestSq = maxDist * maxDist;
            for (int i = 0; i < _nodes.Count; i++)
            {
                var n = _nodes[i];
                if (n == null || n.IsDepleted) continue;
                float limit = Mathf.Max(maxDist, n.HarvestRadius);
                float limitSq = limit * limit;
                Vector3 p = n.WorldPosition;
                float dx = p.x - world.x;
                float dz = p.z - world.z;
                float dSq = dx * dx + dz * dz;
                if (dSq > limitSq) continue;
                if (dSq < bestSq)
                {
                    bestSq = dSq;
                    best = n;
                }
            }
            return best;
        }

        public ResourceNode FindNearestNodeAny(Vector3 world, float maxDist = 12f)
        {
            ResourceNode best = null;
            float bestSq = maxDist * maxDist;
            for (int i = 0; i < _nodes.Count; i++)
            {
                var n = _nodes[i];
                if (n == null) continue;
                Vector3 p = n.WorldPosition;
                float dx = p.x - world.x;
                float dz = p.z - world.z;
                float dSq = dx * dx + dz * dz;
                if (dSq < bestSq)
                {
                    bestSq = dSq;
                    best = n;
                }
            }
            return best;
        }

        public StalkerLair FindNearestLair(Vector3 world, float maxDist = 12f)
        {
            StalkerLair best = null;
            float bestSq = maxDist * maxDist;
            for (int i = 0; i < _lairs.Count; i++)
            {
                var lair = _lairs[i];
                if (lair == null || lair.IsCleared) continue;
                float limit = Mathf.Max(maxDist, lair.ClearRadius);
                float limitSq = limit * limit;
                Vector3 p = lair.WorldPosition;
                float dx = p.x - world.x;
                float dz = p.z - world.z;
                float dSq = dx * dx + dz * dz;
                if (dSq > limitSq) continue;
                if (dSq < bestSq)
                {
                    bestSq = dSq;
                    best = lair;
                }
            }
            return best;
        }

        private void SpawnCraters(System.Random rng, List<Vector3> placed)
        {
            if (_body.CraterCount <= 0) return;

            var root = new GameObject("Craters").transform;
            root.SetParent(_worldRoot, false);

            for (int i = 0; i < _body.CraterCount; i++)
            {
                if (!TrySample(rng, placed, _body.CampusExclusion * 0.85f, _body.MinSpacing * 0.7f, out Vector3 pos))
                    continue;

                float radius = Mathf.Lerp(2.2f, 9.5f, (float)rng.NextDouble());
                int sizeClass = radius < 4f ? 0 : radius < 7f ? 1 : 2;
                GameObject prefab = BuildingVisualCatalog.LoadCrater(sizeClass);
                GameObject crater;
                if (prefab != null)
                {
                    crater = ColonyVisualUtility.InstantiateOriented(prefab, pos, root, (float)rng.NextDouble() * 360f);
                    crater.name = $"Crater_{i}";
                    float native = sizeClass == 0 ? 5f : sizeClass == 1 ? 9f : 14f;
                    crater.transform.localScale = Vector3.one * (radius * 2f / native);
                    ColonyVisualUtility.EnsureUrpMaterials(crater);
                }
                else
                {
                    crater = new GameObject($"Crater_{i}");
                    crater.transform.SetParent(root, false);
                    crater.transform.position = pos;

                    var rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    rim.name = "Rim";
                    rim.transform.SetParent(crater.transform, false);
                    rim.transform.localPosition = new Vector3(0f, 0.04f, 0f);
                    rim.transform.localScale = new Vector3(radius * 2f, 0.06f, radius * 2f);
                    Object.Destroy(rim.GetComponent<Collider>());
                    Tint(rim, _body.CraterRim);

                    var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    floor.name = "Floor";
                    floor.transform.SetParent(crater.transform, false);
                    floor.transform.localPosition = new Vector3(0f, 0.01f, 0f);
                    floor.transform.localScale = new Vector3(radius * 1.35f, 0.03f, radius * 1.35f);
                    Object.Destroy(floor.GetComponent<Collider>());
                    Tint(floor, _body.CraterFloor);
                }

                ColonyVisualUtility.SnapToGround(crater);
                placed.Add(pos);
            }
        }

        /// <summary>Irregular multi-lobe lakes (Earth). Visual only.</summary>
        private void SpawnLakes(System.Random rng, List<Vector3> placed)
        {
            if (_body.LakeCount <= 0) return;

            var root = new GameObject("Lakes").transform;
            root.SetParent(_worldRoot, false);

            for (int i = 0; i < _body.LakeCount; i++)
            {
                if (!TrySample(rng, placed, _body.CampusExclusion * 1.1f, _body.MinSpacing * 1.4f, out Vector3 pos))
                    continue;

                float baseR = Mathf.Lerp(5.5f, 14f, (float)rng.NextDouble());
                var lake = new GameObject($"Lake_{i}");
                lake.transform.SetParent(root, false);
                lake.transform.position = pos;

                int lobes = 4 + rng.Next(0, 5);
                for (int L = 0; L < lobes; L++)
                {
                    float ang = (L / (float)lobes) * Mathf.PI * 2f + (float)rng.NextDouble() * 0.7f;
                    float dist = baseR * Mathf.Lerp(0.05f, 0.55f, (float)rng.NextDouble());
                    float rx = baseR * Mathf.Lerp(0.45f, 1.15f, (float)rng.NextDouble());
                    float rz = baseR * Mathf.Lerp(0.35f, 1.05f, (float)rng.NextDouble());
                    Color water = Color.Lerp(_body.WaterDeep, _body.WaterShallow, (float)rng.NextDouble() * 0.7f);

                    var lobe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    lobe.name = $"Lobe_{L}";
                    lobe.transform.SetParent(lake.transform, false);
                    lobe.transform.localPosition = new Vector3(Mathf.Cos(ang) * dist, 0.015f, Mathf.Sin(ang) * dist);
                    lobe.transform.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                    lobe.transform.localScale = new Vector3(rx * 2f, 0.025f, rz * 2f);
                    Object.Destroy(lobe.GetComponent<Collider>());
                    Tint(lobe, water, 0.72f);

                    // Soft shoreline under the water rim.
                    var shore = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    shore.name = $"Shore_{L}";
                    shore.transform.SetParent(lake.transform, false);
                    shore.transform.localPosition = lobe.transform.localPosition + new Vector3(0f, -0.01f, 0f);
                    shore.transform.localRotation = lobe.transform.localRotation;
                    shore.transform.localScale = new Vector3(rx * 2.25f, 0.02f, rz * 2.25f);
                    Object.Destroy(shore.GetComponent<Collider>());
                    Tint(shore, Color.Lerp(_body.GroundDark, _body.WaterDeep, 0.25f), 0.05f);
                }

                ColonyVisualUtility.SnapToGround(lake);
                placed.Add(pos);
            }
        }

        /// <summary>Meandering river polylines made of irregular water segments.</summary>
        private void SpawnRivers(System.Random rng, List<Vector3> placed)
        {
            if (_body.RiverCount <= 0) return;

            var root = new GameObject("Rivers").transform;
            root.SetParent(_worldRoot, false);
            float maxX = _grid != null ? _grid.WorldWidth - 8f : 370f;
            float maxZ = _grid != null ? _grid.WorldHeight - 8f : 370f;

            for (int i = 0; i < _body.RiverCount; i++)
            {
                if (!TrySampleRiverEndpoints(rng, maxX, maxZ, out Vector3 start, out Vector3 end))
                    continue;

                var river = new GameObject($"River_{i}");
                river.transform.SetParent(root, false);

                int segs = 14 + rng.Next(0, 10);
                float seedX = (float)rng.NextDouble() * 100f;
                float seedZ = (float)rng.NextDouble() * 100f;
                float amp = Mathf.Lerp(6f, 18f, (float)rng.NextDouble());
                Vector3 prev = Vector3.zero;
                bool hasPrev = false;

                for (int s = 0; s <= segs; s++)
                {
                    float t = s / (float)segs;
                    Vector3 p = Vector3.Lerp(start, end, t);
                    Vector3 dir = (end - start).normalized;
                    Vector3 side = new Vector3(-dir.z, 0f, dir.x);
                    float wander = (Mathf.PerlinNoise(seedX + t * 3.2f, seedZ) - 0.5f) * 2f * amp;
                    wander += (Mathf.PerlinNoise(seedX + t * 7.1f, seedZ + 4f) - 0.5f) * amp * 0.45f;
                    p += side * wander;
                    p.y = 0f;

                    if (FlatDist(p, ColonyLayout.CampusOrigin) < _body.CampusExclusion * 0.9f ||
                        FlatDist(p, ColonyLayout.CampusBOrigin) < _body.CampusExclusion * 0.75f)
                    {
                        hasPrev = false;
                        continue;
                    }

                    if (hasPrev)
                    {
                        Vector3 mid = (prev + p) * 0.5f;
                        Vector3 delta = p - prev;
                        float len = delta.magnitude;
                        if (len > 0.4f)
                        {
                            float yaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
                            float width = Mathf.Lerp(1.6f, 3.4f, Mathf.PerlinNoise(seedX + t * 5f, seedZ + 2f));
                            width *= Mathf.Lerp(0.75f, 1.35f, (float)rng.NextDouble());

                            var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                            seg.name = $"Seg_{s}";
                            seg.transform.SetParent(river.transform, false);
                            seg.transform.position = mid + Vector3.up * 0.02f;
                            seg.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                            seg.transform.localScale = new Vector3(width, 0.04f, len * 1.08f);
                            Object.Destroy(seg.GetComponent<Collider>());
                            Color water = Color.Lerp(_body.WaterDeep, _body.WaterShallow,
                                0.25f + 0.5f * Mathf.PerlinNoise(seedX + t, seedZ));
                            Tint(seg, water, 0.7f);
                        }
                    }

                    prev = p;
                    hasPrev = true;
                }

                placed.Add(Vector3.Lerp(start, end, 0.5f));
            }
        }

        private bool TrySampleRiverEndpoints(
            System.Random rng, float maxX, float maxZ, out Vector3 start, out Vector3 end)
        {
            for (int attempt = 0; attempt < 40; attempt++)
            {
                // Prefer edge-to-edge or edge-to-interior so rivers read as watercourses.
                start = SampleEdgePoint(rng, maxX, maxZ);
                end = rng.NextDouble() < 0.55
                    ? SampleEdgePoint(rng, maxX, maxZ)
                    : new Vector3(
                        Mathf.Lerp(8f, maxX, (float)rng.NextDouble()),
                        0f,
                        Mathf.Lerp(8f, maxZ, (float)rng.NextDouble()));

                if (FlatDist(start, end) < 55f) continue;
                if (FlatDist(start, ColonyLayout.CampusOrigin) < _body.CampusExclusion &&
                    FlatDist(end, ColonyLayout.CampusOrigin) < _body.CampusExclusion)
                    continue;
                return true;
            }

            start = end = Vector3.zero;
            return false;
        }

        private static Vector3 SampleEdgePoint(System.Random rng, float maxX, float maxZ)
        {
            int edge = rng.Next(0, 4);
            float u = (float)rng.NextDouble();
            return edge switch
            {
                0 => new Vector3(Mathf.Lerp(4f, maxX, u), 0f, 4f),
                1 => new Vector3(Mathf.Lerp(4f, maxX, u), 0f, maxZ),
                2 => new Vector3(4f, 0f, Mathf.Lerp(4f, maxZ, u)),
                _ => new Vector3(maxX, 0f, Mathf.Lerp(4f, maxZ, u))
            };
        }

        /// <summary>Irregular forest clumps — canopy blobs + trunk props.</summary>
        private void SpawnForestPatches(System.Random rng, List<Vector3> placed)
        {
            if (_body.ForestPatchCount <= 0) return;

            var root = new GameObject("Forests").transform;
            root.SetParent(_worldRoot, false);

            for (int i = 0; i < _body.ForestPatchCount; i++)
            {
                if (!TrySample(rng, placed, _body.CampusExclusion * 0.95f, _body.MinSpacing * 0.85f, out Vector3 pos))
                    continue;

                float patchR = Mathf.Lerp(6f, 16f, (float)rng.NextDouble());
                var patch = new GameObject($"Forest_{i}");
                patch.transform.SetParent(root, false);
                patch.transform.position = pos;

                int trees = 8 + rng.Next(0, 18);
                for (int t = 0; t < trees; t++)
                {
                    // Rejection sampling for irregular density (not a filled disc).
                    float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                    float rad = patchR * Mathf.Sqrt((float)rng.NextDouble());
                    float dens = Mathf.PerlinNoise(pos.x * 0.05f + rad * 0.2f, pos.z * 0.05f + ang);
                    if (dens < 0.28f && rng.NextDouble() < 0.55) continue;

                    Vector3 local = new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);
                    if (FlatDist(pos + local, ColonyLayout.CampusOrigin) < _body.CampusExclusion * 0.8f)
                        continue;

                    SpawnTree(patch.transform, local, rng);
                }

                ColonyVisualUtility.SnapToGround(patch);
                placed.Add(pos);
            }
        }

        private void SpawnTree(Transform parent, Vector3 local, System.Random rng)
        {
            var tree = new GameObject("Tree");
            tree.transform.SetParent(parent, false);
            tree.transform.localPosition = local;

            float h = Mathf.Lerp(1.1f, 2.6f, (float)rng.NextDouble());
            float trunkR = Mathf.Lerp(0.12f, 0.28f, (float)rng.NextDouble());

            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Trunk";
            trunk.transform.SetParent(tree.transform, false);
            trunk.transform.localPosition = new Vector3(0f, h * 0.28f, 0f);
            trunk.transform.localScale = new Vector3(trunkR * 2f, h * 0.28f, trunkR * 2f);
            Object.Destroy(trunk.GetComponent<Collider>());
            Tint(trunk, Color.Lerp(_body.ForestTrunk, _body.RockColor, (float)rng.NextDouble() * 0.25f), 0.08f);

            int canopies = 1 + (rng.NextDouble() < 0.45 ? 1 : 0) + (rng.NextDouble() < 0.2 ? 1 : 0);
            for (int c = 0; c < canopies; c++)
            {
                float cr = Mathf.Lerp(0.9f, 2.1f, (float)rng.NextDouble());
                var canopy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                canopy.name = $"Canopy_{c}";
                canopy.transform.SetParent(tree.transform, false);
                canopy.transform.localPosition = new Vector3(
                    (float)(rng.NextDouble() - 0.5) * cr * 0.35f,
                    h * Mathf.Lerp(0.55f, 0.95f, (float)rng.NextDouble()),
                    (float)(rng.NextDouble() - 0.5) * cr * 0.35f);
                canopy.transform.localScale = new Vector3(
                    cr * Mathf.Lerp(0.85f, 1.25f, (float)rng.NextDouble()),
                    cr * Mathf.Lerp(0.55f, 0.95f, (float)rng.NextDouble()),
                    cr * Mathf.Lerp(0.85f, 1.25f, (float)rng.NextDouble()));
                Object.Destroy(canopy.GetComponent<Collider>());
                Color leaf = Color.Lerp(
                    _body.ForestCanopy,
                    _body.GroundLight,
                    (float)rng.NextDouble() * 0.35f);
                Tint(canopy, leaf, 0.12f);
            }
        }

        private void SpawnDunes(System.Random rng, List<Vector3> placed)
        {
            if (_body.DuneCount <= 0) return;

            var root = new GameObject("Dunes").transform;
            root.SetParent(_worldRoot, false);

            for (int i = 0; i < _body.DuneCount; i++)
            {
                if (!TrySample(rng, placed, _body.CampusExclusion * 0.75f, _body.MinSpacing * 0.55f, out Vector3 pos))
                    continue;

                var dune = new GameObject($"Dune_{i}");
                dune.transform.SetParent(root, false);
                dune.transform.position = pos;
                dune.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 180f, 0f);

                var mound = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                mound.name = "Ridge";
                mound.transform.SetParent(dune.transform, false);
                mound.transform.localPosition = new Vector3(0f, 0.18f, 0f);
                mound.transform.localScale = new Vector3(
                    Mathf.Lerp(3.2f, 6.5f, (float)rng.NextDouble()),
                    Mathf.Lerp(0.25f, 0.55f, (float)rng.NextDouble()),
                    Mathf.Lerp(1.1f, 2.2f, (float)rng.NextDouble()));
                Object.Destroy(mound.GetComponent<Collider>());
                Tint(mound, _body.DuneColor);

                ColonyVisualUtility.SnapToGround(dune);
                placed.Add(pos);
            }
        }

        private void SpawnAsteroidIslets(System.Random rng, List<Vector3> placed)
        {
            if (_body.AsteroidIsletCount <= 0) return;

            var root = new GameObject("AsteroidIslets").transform;
            root.SetParent(_worldRoot, false);

            for (int i = 0; i < _body.AsteroidIsletCount; i++)
            {
                if (!TrySample(rng, placed, _body.CampusExclusion * 0.7f, _body.MinSpacing * 0.85f, out Vector3 pos))
                    continue;

                var islet = new GameObject($"Islet_{i}");
                islet.transform.SetParent(root, false);
                islet.transform.position = pos;

                int chunks = 3 + rng.Next(0, 4);
                for (int c = 0; c < chunks; c++)
                {
                    bool cube = rng.NextDouble() < 0.45;
                    var chunk = GameObject.CreatePrimitive(cube ? PrimitiveType.Cube : PrimitiveType.Sphere);
                    chunk.name = $"Chunk_{c}";
                    chunk.transform.SetParent(islet.transform, false);
                    float ang = c * 2.1f + (float)rng.NextDouble();
                    float dist = Mathf.Lerp(0.2f, 1.4f, (float)rng.NextDouble());
                    chunk.transform.localPosition = new Vector3(
                        Mathf.Cos(ang) * dist,
                        Mathf.Lerp(0.25f, 1.1f, (float)rng.NextDouble()),
                        Mathf.Sin(ang) * dist);
                    float s = Mathf.Lerp(0.55f, 1.8f, (float)rng.NextDouble());
                    chunk.transform.localScale = new Vector3(
                        s * Mathf.Lerp(0.7f, 1.4f, (float)rng.NextDouble()),
                        s * Mathf.Lerp(0.45f, 1.1f, (float)rng.NextDouble()),
                        s * Mathf.Lerp(0.7f, 1.4f, (float)rng.NextDouble()));
                    chunk.transform.localRotation = Quaternion.Euler(
                        (float)rng.NextDouble() * 50f,
                        (float)rng.NextDouble() * 360f,
                        (float)rng.NextDouble() * 50f);
                    Object.Destroy(chunk.GetComponent<Collider>());
                    Color tint = Color.Lerp(_body.RockColor, _body.GroundLight, (float)rng.NextDouble() * 0.45f);
                    if (rng.NextDouble() < 0.2f)
                        tint = Color.Lerp(tint, new Color(0.55f, 0.52f, 0.42f), 0.4f);
                    Tint(chunk, tint);
                }

                ColonyVisualUtility.SnapToGround(islet);
                placed.Add(pos);
            }
        }

        private void SpawnIcePlates(System.Random rng, List<Vector3> placed)
        {
            if (_body.IcePlateCount <= 0) return;

            var root = new GameObject("IcePlates").transform;
            root.SetParent(_worldRoot, false);

            for (int i = 0; i < _body.IcePlateCount; i++)
            {
                if (!TrySample(rng, placed, _body.CampusExclusion * 0.8f, _body.MinSpacing * 0.9f, out Vector3 pos))
                    continue;

                var plate = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                plate.name = $"IcePlate_{i}";
                plate.transform.SetParent(root, false);
                plate.transform.position = pos + Vector3.up * 0.02f;
                float r = Mathf.Lerp(3.2f, 8.5f, (float)rng.NextDouble());
                plate.transform.localScale = new Vector3(
                    r * 2f,
                    0.04f,
                    r * 2f * Mathf.Lerp(0.65f, 1.2f, (float)rng.NextDouble()));
                plate.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                Object.Destroy(plate.GetComponent<Collider>());
                Color ice = Color.Lerp(_body.WaterShallow, _body.GroundLight, 0.45f);
                Tint(plate, ice, 0.55f);
                ColonyVisualUtility.SnapToGround(plate);
                placed.Add(pos);
            }
        }

        private void SpawnRocks(System.Random rng, List<Vector3> placed)
        {
            var root = new GameObject("Rocks").transform;
            root.SetParent(_worldRoot, false);

            for (int i = 0; i < _body.RockCount; i++)
            {
                if (!TrySample(rng, placed, _body.CampusExclusion * 0.7f, 2.2f, out Vector3 pos))
                    continue;

                var rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rock.name = $"Rock_{i}";
                rock.transform.SetParent(root, false);
                rock.transform.position = pos + Vector3.up * 0.15f;
                float s = Mathf.Lerp(0.25f, 0.85f, (float)rng.NextDouble());
                rock.transform.localScale = new Vector3(
                    s * Mathf.Lerp(0.7f, 1.3f, (float)rng.NextDouble()),
                    s * Mathf.Lerp(0.4f, 0.9f, (float)rng.NextDouble()),
                    s * Mathf.Lerp(0.7f, 1.3f, (float)rng.NextDouble()));
                rock.transform.rotation = Quaternion.Euler(
                    (float)rng.NextDouble() * 25f,
                    (float)rng.NextDouble() * 360f,
                    (float)rng.NextDouble() * 25f);
                Object.Destroy(rock.GetComponent<Collider>());
                Tint(rock, Color.Lerp(_body.RockColor, _body.GroundDark, (float)rng.NextDouble() * 0.4f));
                ColonyVisualUtility.SnapToGround(rock);
            }
        }

        private void SpawnResourceNodes(System.Random rng, List<Vector3> placed)
        {
            var root = new GameObject("ResourceNodes").transform;
            root.SetParent(_worldRoot, false);

            var bag = BuildResourceBag(rng);
            int count = Mathf.Min(_body.ResourceNodeCount, bag.Count);
            for (int i = 0; i < count; i++)
            {
                if (!TrySample(rng, placed, _body.CampusExclusion, _body.MinSpacing, out Vector3 pos))
                    continue;

                var type = bag[i];
                if (type == ResourceNodeType.Ice && pos.z < ColonyLayout.GroundCenter.z)
                {
                    if (rng.NextDouble() < _body.IcePolarBias)
                        type = ResourceNodeType.Regolith;
                }

                int yield = type switch
                {
                    ResourceNodeType.Metals => 28 + rng.Next(0, 16),
                    ResourceNodeType.Ice => 22 + rng.Next(0, 12),
                    ResourceNodeType.Fissile => 14 + rng.Next(0, 10),
                    _ => 36 + rng.Next(0, 20)
                };
                yield = Mathf.Max(4, Mathf.RoundToInt(yield * Mathf.Max(0.25f, _body.ExtractYieldScale)));

                var go = new GameObject($"Node_{type}_{i}");
                go.transform.SetParent(root, false);
                go.transform.position = pos;
                var node = go.AddComponent<ResourceNode>();
                node.Configure(type, yield, 7f, _body.SoilNodeColor);
                _nodes.Add(node);
                placed.Add(pos);
            }
        }

        private List<ResourceNodeType> BuildResourceBag(System.Random rng)
        {
            var weights = _body.ResourceWeights;
            if (weights == null || weights.Length < 4)
                weights = new[] { 5, 3, 2, 2 };

            var bag = new List<ResourceNodeType>(16);
            Add(ResourceNodeType.Regolith, weights[0]);
            Add(ResourceNodeType.Metals, weights[1]);
            Add(ResourceNodeType.Ice, weights[2]);
            Add(ResourceNodeType.Fissile, weights[3]);

            // Shuffle so placement order isn't type-clustered.
            for (int i = bag.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (bag[i], bag[j]) = (bag[j], bag[i]);
            }
            return bag;

            void Add(ResourceNodeType t, int w)
            {
                int n = Mathf.Max(0, w);
                for (int i = 0; i < n; i++) bag.Add(t);
            }
        }

        private void SpawnLairs(System.Random rng, List<Vector3> placed)
        {
            var root = new GameObject("StalkerLairs").transform;
            root.SetParent(_worldRoot, false);

            for (int i = 0; i < _body.LairCount; i++)
            {
                if (!TrySample(rng, placed, _body.CampusExclusion * 1.05f, _body.MinSpacing * 1.2f, out Vector3 pos))
                    continue;

                int budget = Mathf.Max(1, _body.LairStalkerBudget) + (rng.NextDouble() < 0.28 ? 1 : 0);
                var go = new GameObject($"Lair_{i}");
                go.transform.SetParent(root, false);
                go.transform.position = pos;
                var lair = go.AddComponent<StalkerLair>();
                lair.Configure(_loop, budget, 8f, _body.LairRim, _body.LairPit);
                _lairs.Add(lair);
                placed.Add(pos);
            }
        }

        private bool TrySample(
            System.Random rng,
            List<Vector3> placed,
            float exclusion,
            float spacing,
            out Vector3 pos)
        {
            float minX = 4f;
            float minZ = 4f;
            float maxX = _grid != null ? _grid.WorldWidth - 8f : 370f;
            float maxZ = _grid != null ? _grid.WorldHeight - 8f : 370f;

            for (int attempt = 0; attempt < 80; attempt++)
            {
                float x = Mathf.Lerp(minX, maxX, (float)rng.NextDouble());
                float z = Mathf.Lerp(minZ, maxZ, (float)rng.NextDouble());
                pos = new Vector3(x, 0f, z);

                if (FlatDist(pos, ColonyLayout.CampusOrigin) < exclusion) continue;
                if (FlatDist(pos, ColonyLayout.CampusBOrigin) < exclusion * 0.85f) continue;

                bool ok = true;
                for (int i = 0; i < placed.Count; i++)
                {
                    if (FlatDist(pos, placed[i]) < spacing)
                    {
                        ok = false;
                        break;
                    }
                }
                if (!ok) continue;
                return true;
            }

            pos = Vector3.zero;
            return false;
        }

        private static float FlatDist(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        public static void Tint(
            GameObject go,
            Color c,
            float smoothness = 0.06f,
            ShadowCastingMode shadows = ShadowCastingMode.Off)
        {
            var rend = go.GetComponent<Renderer>();
            if (rend == null) return;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                                   ?? Shader.Find("Sprites/Default"));
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            else if (mat.HasProperty("_Color")) mat.color = c;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            rend.sharedMaterial = mat;
            rend.shadowCastingMode = shadows;
        }
    }
}
