using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace SolarMajesty
{
    /// <summary>
    /// What the terrain kits of generated worlds put on the map (see <see cref="TerrainDataBake"/>):
    /// the sea of an archipelago, the lava of a volcanic world and the loose sand of a dune sea,
    /// with the questions the rest of the game asks about them.
    /// </summary>
    public partial class PlanetaryWorldGen
    {
        /// <summary>Health a robot loses each second it stands in lava (1 = all of it).</summary>
        public const float LavaDamagePerSecond = 0.06f;

        /// <summary>How much of its speed a robot loses on the loosest sand.</summary>
        public const float LooseSandSlow = 0.3f;

        public bool HasSea => _bake != null && _bake.HasSea;
        public bool HasLava => _bake != null && _bake.Lava != null;
        public bool HasLooseSand => _bake != null && _bake.LooseSand != null;

        /// <summary>True where lava runs (plus a margin in metres).</summary>
        public bool IsOverLava(Vector3 world, float margin = 0f)
        {
            if (!HasLava) return false;
            if (_bake.Sample(_bake.Lava, world.x, world.z) > TerrainDataBake.LavaEdge) return true;
            if (margin < 0.75f) return false;
            return _bake.Sample(_bake.Lava, world.x - margin, world.z) > TerrainDataBake.LavaEdge ||
                   _bake.Sample(_bake.Lava, world.x + margin, world.z) > TerrainDataBake.LavaEdge ||
                   _bake.Sample(_bake.Lava, world.x, world.z - margin) > TerrainDataBake.LavaEdge ||
                   _bake.Sample(_bake.Lava, world.x, world.z + margin) > TerrainDataBake.LavaEdge;
        }

        /// <summary>Travel speed scale of the ground at a point: below 1 on loose sand.</summary>
        public float TravelScaleAt(Vector3 world) =>
            HasLooseSand ? 1f - LooseSandSlow * Mathf.Clamp01(_bake.Sample(_bake.LooseSand, world.x, world.z)) : 1f;

        /// <summary>
        /// A spot that shows what this world's kit is: a point on a lava flow, the shore
        /// nearest the colony, or the first dune out. False on a world with none of them.
        /// </summary>
        public bool TryKitVista(out Vector3 at)
        {
            at = ColonyLayout.CampusOrigin;
            if (_bake == null) return false;
            if (HasLava && _bake.LavaFlows.Count > 0)
            {
                TerrainRiver flow = _bake.LavaFlows[0];
                at = flow.Points[flow.Points.Count / 3];
                at.y = 0f;
                return true;
            }
            if (!HasSea && !HasLooseSand) return false;
            for (float d = 30f; d < 180f; d += 4f)
            for (int a = 0; a < 12; a++)
            {
                float ang = a * Mathf.PI / 6f;
                Vector3 p = ColonyLayout.CampusOrigin + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * d;
                bool hit = HasSea ? SeaAt(p, 0f) : _bake.Sample(_bake.LooseSand, p.x, p.z) > 0.8f;
                if (!hit) continue;
                at = p;
                return true;
            }
            return false;
        }

        private bool SeaAt(Vector3 world, float margin)
        {
            if (_bake.SampleHeight(world.x, world.z) < TerrainDataBake.SeaLevel) return true;
            if (margin < 0.75f) return false;
            return _bake.SampleHeight(world.x - margin, world.z) < TerrainDataBake.SeaLevel ||
                   _bake.SampleHeight(world.x + margin, world.z) < TerrainDataBake.SeaLevel ||
                   _bake.SampleHeight(world.x, world.z - margin) < TerrainDataBake.SeaLevel ||
                   _bake.SampleHeight(world.x, world.z + margin) < TerrainDataBake.SeaLevel;
        }

        /// <summary>The sea surface, and the walkable ground cut away under it.</summary>
        private void SpawnSea()
        {
            if (!HasSea) return;
            var root = new GameObject("Sea").transform;
            root.SetParent(_worldRoot, false);

            var surface = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            surface.name = "SeaSurface";
            surface.transform.SetParent(root, false);
            surface.transform.position = new Vector3(_bake.WorldWidth * 0.5f, TerrainDataBake.SeaLevel, _bake.WorldHeight * 0.5f);
            // Wider than the map, so the water runs out to the horizon.
            float span = Mathf.Max(_bake.WorldWidth, _bake.WorldHeight) * 2.4f;
            surface.transform.localScale = new Vector3(span, 0.02f, span);
            Object.Destroy(surface.GetComponent<Collider>());
            StylizedWaterVisual.Apply(surface, _body.WaterDeep, Color.Lerp(_body.WaterDeep, _body.WaterShallow, 0.5f));

            // One carve box per run of sea cells in a row keeps the obstacle count in the low hundreds.
            int c = TerrainDataBake.SeaCellsPerSide;
            float cw = _bake.WorldWidth / c, ch = _bake.WorldHeight / c;
            int boxes = 0;
            for (int z = 0; z < c; z++)
            {
                int x = 0;
                while (x < c)
                {
                    if (!_bake.SeaCells[z * c + x]) { x++; continue; }
                    int start = x;
                    while (x < c && _bake.SeaCells[z * c + x]) x++;
                    var carve = new GameObject("SeaCarve");
                    carve.transform.SetParent(root, false);
                    carve.transform.position = new Vector3((start + x) * 0.5f * cw, 0f, (z + 0.5f) * ch);
                    var obstacle = carve.AddComponent<NavMeshObstacle>();
                    obstacle.carving = true;
                    obstacle.carveOnlyStationary = true;
                    obstacle.shape = NavMeshObstacleShape.Box;
                    obstacle.size = new Vector3((x - start) * cw, 4f, ch);
                    boxes++;
                }
            }
            Debug.Log($"[WorldGen] {_body.DisplayName} sea: {boxes} carve boxes.");
        }

        /// <summary>Glowing ribbons along the lava channels the bake carved.</summary>
        private void SpawnLava()
        {
            if (!HasLava || _bake.LavaFlows.Count == 0) return;
            var root = new GameObject("Lava").transform;
            root.SetParent(_worldRoot, false);

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color")
                            ?? Shader.Find("Sprites/Default");
            // Brighter than white, so it glows under bloom and stays readable without it.
            var glow = new Color(2.3f, 0.72f, 0.12f, 1f);
            var material = new Material(shader) { name = "SM_Lava" };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", glow);
            material.color = glow;

            for (int i = 0; i < _bake.LavaFlows.Count; i++)
            {
                Mesh mesh = BuildRiverRibbon(_bake.LavaFlows[i], 0.92f);
                if (mesh == null) continue;
                var ribbon = new GameObject($"LavaFlow_{i}");
                ribbon.transform.SetParent(root, false);
                ribbon.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                ribbon.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = ribbon.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }
    }
}
