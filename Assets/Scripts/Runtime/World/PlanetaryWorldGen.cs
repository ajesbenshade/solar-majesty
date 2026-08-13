using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SolarMajesty
{
    /// <summary>
    /// Seeded procedural world contents outside campus footprints.
    /// Driven by <see cref="CelestialBodyProfile"/> so Luna, Mars, and future bodies share one generator.
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
            SpawnDunes(rng, placed);
            SpawnRocks(rng, placed);
            SpawnResourceNodes(rng, placed);
            SpawnLairs(rng, placed);

            Debug.Log(
                $"[WorldGen] {_body.DisplayName} seed={seed} " +
                $"craters={_body.CraterCount} dunes={_body.DuneCount} rocks={_body.RockCount} " +
                $"nodes={_nodes.Count} lairs={_lairs.Count}");
        }

        public void SpawnLairStalkers(Transform threatParent)
        {
            for (int i = 0; i < _lairs.Count; i++)
                _lairs[i]?.SpawnInitial(threatParent);
        }

        public void TickLairs()
        {
            for (int i = 0; i < _lairs.Count; i++)
                _lairs[i]?.Tick();
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

                int budget = 2 + (rng.NextDouble() < 0.35 ? 1 : 0);
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
