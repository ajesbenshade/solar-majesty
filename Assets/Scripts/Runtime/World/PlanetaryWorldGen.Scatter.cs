using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// World dressing that makes the map worth looking at: meadow scatter, rock formations and
    /// points of interest, plus the prop registry that lets a building clear the trees, rocks and
    /// grass under its footprint.
    /// </summary>
    public partial class PlanetaryWorldGen
    {
        private readonly List<Transform> _props = new List<Transform>(2048);

        /// <summary>Record a removable prop (tree, shrub, rock, scatter, POI).</summary>
        public void RegisterProp(Transform prop)
        {
            if (prop != null) _props.Add(prop);
        }

        /// <summary>Record every direct child of <paramref name="root"/> except the named ones.</summary>
        public void RegisterChildren(Transform root, params string[] skip)
        {
            if (root == null) return;
            for (int i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                if (skip != null && System.Array.IndexOf(skip, c.name) >= 0) continue;
                _props.Add(c);
            }
        }

        /// <summary>
        /// Remove every registered prop whose footprint touches the rectangle (world XZ, axis
        /// aligned). Buildings call this when they go up, so nothing grows through a roof.
        /// </summary>
        public int ClearPropsInRect(Vector3 center, float halfX, float halfZ)
        {
            int n = 0;
            for (int i = _props.Count - 1; i >= 0; i--)
            {
                var p = _props[i];
                if (p == null)
                {
                    _props.RemoveAt(i);
                    continue;
                }
                Vector3 at = p.position;
                float reach = PropReach(p);
                if (Mathf.Abs(at.x - center.x) > halfX + reach || Mathf.Abs(at.z - center.z) > halfZ + reach) continue;
                _props.RemoveAt(i);
                if (Application.isPlaying) Destroy(p.gameObject);
                else DestroyImmediate(p.gameObject);
                n++;
            }
            return n;
        }

        public int ClearPropsInDisc(Vector3 center, float radius) => ClearPropsInRect(center, radius, radius);

        /// <summary>How far a prop's foliage or rock reaches from its pivot, capped so a big grove
        /// root is not treated as one giant prop.</summary>
        private static float PropReach(Transform p)
        {
            var rend = p.GetComponentInChildren<Renderer>();
            if (rend == null) return 0.3f;
            var e = rend.bounds.extents;
            return Mathf.Clamp(Mathf.Max(e.x, e.z) * 0.7f, 0.2f, 3f);
        }

        // ------------------------------------------------------------------ meadows

        /// <summary>
        /// Grass clumps, flowers and shrubs over the open ground, thicker where the terrain's
        /// splat is green and around forest edges. Keeps off water and the Commons yard.
        /// </summary>
        private void SpawnMeadows(System.Random rng)
        {
            if (_body.MeadowCount <= 0) return;
            var root = new GameObject("Meadows").transform;
            root.SetParent(_worldRoot, false);
            float maxX = _grid != null ? _grid.WorldWidth - 6f : 378f;
            float maxZ = _grid != null ? _grid.WorldHeight - 6f : 378f;

            int placedCount = 0;
            for (int i = 0; i < _body.MeadowCount * 3 && placedCount < _body.MeadowCount; i++)
            {
                var pos = new Vector3(Mathf.Lerp(6f, maxX, (float)rng.NextDouble()), 0f, Mathf.Lerp(6f, maxZ, (float)rng.NextDouble()));
                if (FlatDist(pos, ColonyLayout.CampusOrigin) < VistaExclusion * 1.1f) continue;
                // Patchy: meadows cluster where low-frequency noise is high.
                float field = Mathf.PerlinNoise(pos.x * 0.022f + 3.1f, pos.z * 0.022f + 7.7f);
                if (field < 0.42f && rng.NextDouble() < 0.75) continue;
                if (IsOverWater(pos, 0.6f)) continue;

                double roll = rng.NextDouble();
                GameObject prefab;
                string name;
                float height;
                if (roll < 0.62) { prefab = EnvironmentMeshCatalog.LoadEarthGrass(i); name = "Meadow_Grass"; height = Mathf.Lerp(0.35f, 0.7f, (float)rng.NextDouble()); }
                else if (roll < 0.84) { prefab = EnvironmentMeshCatalog.LoadEarthFlower(i); name = "Meadow_Flower"; height = Mathf.Lerp(0.3f, 0.55f, (float)rng.NextDouble()); }
                else { prefab = EnvironmentMeshCatalog.LoadEarthShrub(i); name = "Meadow_Shrub"; height = Mathf.Lerp(0.5f, 1.1f, (float)rng.NextDouble()); }
                if (prefab == null) continue;

                // A clump: one to four of the same kind close together.
                int clump = 1 + rng.Next(0, 4);
                for (int c = 0; c < clump; c++)
                {
                    var p = pos + new Vector3((float)(rng.NextDouble() - 0.5) * 1.6f, 0f, (float)(rng.NextDouble() - 0.5) * 1.6f);
                    var go = EnvironmentMeshCatalog.InstantiateVendorNature(prefab, name, height * Mathf.Lerp(0.8f, 1.2f, (float)rng.NextDouble()));
                    if (go == null) break;
                    go.transform.SetParent(root, false);
                    go.transform.position = p;
                    go.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                    ColonyVisualUtility.SnapToGround(go, GroundY(p) - 0.02f);
                    RegisterProp(go.transform);
                }
                placedCount++;
            }
        }

        // ------------------------------------------------------------------ rock formations

        /// <summary>
        /// Outcrops: one or two big boulders (3-6 m) with a skirt of smaller rocks round them,
        /// preferring rocky ground. Landmarks the eye can navigate by.
        /// </summary>
        private void SpawnRockFormations(System.Random rng, List<Vector3> placed)
        {
            if (_body.RockFormationCount <= 0) return;
            var root = new GameObject("RockFormations").transform;
            root.SetParent(_worldRoot, false);

            int made = 0;
            for (int attempt = 0; attempt < _body.RockFormationCount * 6 && made < _body.RockFormationCount; attempt++)
            {
                if (!TrySample(rng, placed, VistaExclusion * 1.4f, _body.MinSpacing * 1.4f, out Vector3 pos)) continue;
                if (_bake != null && rng.NextDouble() > 0.35 + 0.65 * _bake.SampleRockiness(pos.x, pos.z)) continue;
                if (IsOverWater(pos, 4f)) continue;

                var formation = new GameObject("RockFormation_" + made).transform;
                formation.SetParent(root, false);
                formation.position = pos;
                int big = 1 + rng.Next(0, 2);
                int skirt = 5 + rng.Next(0, 6);
                for (int i = 0; i < big + skirt; i++)
                {
                    bool isBig = i < big;
                    float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                    float dist = isBig ? (float)rng.NextDouble() * 1.4f : Mathf.Lerp(2.0f, 4.2f, (float)rng.NextDouble());
                    var p = pos + new Vector3(Mathf.Cos(ang) * dist, 0f, Mathf.Sin(ang) * dist);
                    float h = isBig ? Mathf.Lerp(3.0f, 6.0f, (float)rng.NextDouble()) : Mathf.Lerp(0.6f, 1.8f, (float)rng.NextDouble());
                    var prefab = EnvironmentMeshCatalog.LoadRock(made * 13 + i, _body.Id);
                    GameObject rock;
                    if (prefab != null && EnvironmentMeshCatalog.IsVendorNature(prefab))
                        rock = EnvironmentMeshCatalog.InstantiateVendorNature(prefab, "Outcrop", h);
                    else
                    {
                        rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        rock.name = "Outcrop";
                        ColonyVisualUtility.DestroyNow(rock.GetComponent<Collider>());
                        rock.transform.localScale = new Vector3(h * 1.1f, h, h * 0.9f);
                        Tint(rock, Color.Lerp(_body.RockColor, _body.GroundDark, (float)rng.NextDouble() * 0.35f), 0.06f, UnityEngine.Rendering.ShadowCastingMode.On);
                    }
                    if (rock == null) continue;
                    rock.transform.SetParent(formation, true);
                    rock.transform.position = p;
                    rock.transform.rotation = Quaternion.Euler(
                        isBig ? (float)rng.NextDouble() * 10f : (float)rng.NextDouble() * 25f,
                        (float)rng.NextDouble() * 360f,
                        isBig ? (float)rng.NextDouble() * 8f : (float)rng.NextDouble() * 20f);
                    ColonyVisualUtility.SnapToGround(rock, GroundY(p) - h * 0.15f);
                    RegisterProp(rock.transform);
                }
                placed.Add(pos);
                made++;
            }
        }

        // ------------------------------------------------------------------ points of interest

        private enum Poi { StoneCircle, Ruins, Monolith, CrashedProbe, SurveyCamp }

        /// <summary>Stone circles, ruins, monoliths, a crashed probe, an abandoned survey camp.</summary>
        private void SpawnPointsOfInterest(System.Random rng, List<Vector3> placed)
        {
            if (_body.PoiCount <= 0) return;
            var root = new GameObject("PointsOfInterest").transform;
            root.SetParent(_worldRoot, false);
            int made = 0;
            for (int attempt = 0; attempt < _body.PoiCount * 8 && made < _body.PoiCount; attempt++)
            {
                if (!TrySample(rng, placed, VistaExclusion * 2.2f, _body.MinSpacing * 2f, out Vector3 pos)) continue;
                if (IsOverWater(pos, 6f)) continue;
                var kind = (Poi)(made % 5);
                var go = new GameObject("POI_" + kind + "_" + made);
                go.transform.SetParent(root, false);
                go.transform.position = new Vector3(pos.x, GroundY(pos), pos.z);
                go.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                var k = new DetailBatch(go.transform, "POI_" + kind + "_" + made + "_" + _body.Id);
                var r = new System.Random(rng.Next());
                switch (kind)
                {
                    case Poi.StoneCircle: StoneCircle(k, r); break;
                    case Poi.Ruins: Ruins(k, r); break;
                    case Poi.Monolith: Monolith(k, r); break;
                    case Poi.CrashedProbe: CrashedProbe(k, r); break;
                    default: SurveyCamp(k, r); break;
                }
                k.Build();
                RegisterProp(go.transform);
                placed.Add(pos);
                made++;
            }
        }

        private static readonly Color PoiStone = new Color(0.56f, 0.53f, 0.48f);
        private static readonly Color PoiStoneDark = new Color(0.40f, 0.38f, 0.35f);
        private static readonly Color PoiMoss = new Color(0.25f, 0.42f, 0.18f);
        private static readonly Color PoiGlyph = new Color(0.25f, 0.85f, 1f);
        private static readonly Color PoiGlyphEmit = new Color(0.3f, 1.4f, 2.0f);
        private static readonly Color PoiMetal = new Color(0.62f, 0.64f, 0.66f);
        private static readonly Color PoiScorch = new Color(0.24f, 0.19f, 0.15f);
        private static readonly Color PoiCloth = new Color(0.78f, 0.62f, 0.32f);

        private static float Rf(System.Random r, float lo, float hi) => lo + (float)r.NextDouble() * (hi - lo);

        private static void StoneCircle(DetailBatch k, System.Random r)
        {
            k.Cyl(new Vector3(0f, 0.02f, 0f), 11f, 0.03f, new Color(0.42f, 0.36f, 0.27f));
            int n = 9;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                var p = new Vector3(Mathf.Cos(a) * 4.4f, 0f, Mathf.Sin(a) * 4.4f);
                if (i == 4) // a fallen stone
                {
                    k.Box(p + Vector3.up * 0.3f, new Vector3(2.4f, 0.6f, 0.9f), Quaternion.Euler(0f, a * Mathf.Rad2Deg + 90f, 8f), PoiStoneDark);
                    continue;
                }
                float h = Rf(r, 2.2f, 3.2f);
                var rot = Quaternion.Euler(Rf(r, -6f, 6f), -a * Mathf.Rad2Deg + 90f, Rf(r, -6f, 6f));
                k.Box(p + Vector3.up * h * 0.5f, new Vector3(0.95f, h, 0.55f), rot, i % 2 == 0 ? PoiStone : PoiStoneDark);
                k.Box(p + Vector3.up * 0.15f, new Vector3(1.3f, 0.3f, 0.9f), rot, PoiMoss);
                if (i % 3 == 0) k.Box(p + Vector3.up * (h * 0.6f), new Vector3(0.3f, 0.5f, 0.6f), rot, PoiGlyph, PoiGlyphEmit);
            }
            // Lintel over two neighbours and the altar.
            k.Box(new Vector3(4.2f, 3.15f, 1.4f), new Vector3(0.7f, 0.45f, 2.8f), Quaternion.Euler(0f, 0f, 2f), PoiStone);
            k.Box(new Vector3(0f, 0.45f, 0f), new Vector3(2.0f, 0.9f, 1.2f), Quaternion.Euler(0f, 20f, 0f), PoiStoneDark);
            k.Box(new Vector3(0f, 0.92f, 0f), new Vector3(1.6f, 0.06f, 0.8f), Quaternion.Euler(0f, 20f, 0f), PoiGlyph, PoiGlyphEmit * 0.7f);
        }

        private static void Ruins(DetailBatch k, System.Random r)
        {
            k.Box(new Vector3(0f, 0.05f, 0f), new Vector3(9f, 0.1f, 7f), PoiStoneDark);
            for (int x = -3; x <= 3; x++)
            for (int z = -2; z <= 2; z++)
                if (r.NextDouble() < 0.55)
                    k.Box(new Vector3(x * 1.25f, 0.12f, z * 1.25f), new Vector3(1.15f, 0.06f, 1.15f), Quaternion.Euler(0f, Rf(r, -4f, 4f), 0f), PoiStone);
            // Broken walls on two sides, ragged tops.
            for (int i = 0; i < 7; i++)
            {
                float h = Rf(r, 0.6f, 2.8f);
                k.Box(new Vector3(-4.2f, h * 0.5f, -3f + i), new Vector3(0.6f, h, 1.0f), PoiStone);
            }
            for (int i = 0; i < 6; i++)
            {
                float h = Rf(r, 0.4f, 2.2f);
                k.Box(new Vector3(-3.5f + i * 1.05f, h * 0.5f, 3.2f), new Vector3(1.0f, h, 0.6f), i % 2 == 0 ? PoiStone : PoiStoneDark);
            }
            // Columns, one fallen.
            for (int i = 0; i < 3; i++)
            {
                var p = new Vector3(-1.5f + i * 1.8f, 0f, 0.6f);
                float h = i == 1 ? 1.2f : Rf(r, 3.0f, 3.8f);
                k.Cyl(p + Vector3.up * h * 0.5f, 0.55f, h, PoiStone);
                k.Box(p + Vector3.up * 0.15f, new Vector3(0.8f, 0.3f, 0.8f), PoiStoneDark);
                if (i != 1) k.Box(p + Vector3.up * (h + 0.1f), new Vector3(0.8f, 0.2f, 0.8f), PoiStoneDark);
            }
            k.Add(PrimitiveType.Cylinder, new Vector3(0.6f, 0.3f, -1.8f), new Vector3(0.55f, 1.4f, 0.55f), Quaternion.Euler(0f, 30f, 90f), PoiStone);
            for (int i = 0; i < 10; i++)
                k.Box(new Vector3(Rf(r, -4f, 4f), 0.15f, Rf(r, -3f, 3f)), Vector3.one * Rf(r, 0.25f, 0.55f), Quaternion.Euler(Rf(r, 0f, 40f), Rf(r, 0f, 90f), 0f), PoiStoneDark);
            for (int i = 0; i < 5; i++)
                k.Ball(new Vector3(Rf(r, -4f, 4f), 0.2f, Rf(r, -3f, 3f)), new Vector3(0.9f, 0.4f, 0.9f), PoiMoss);
        }

        private static void Monolith(DetailBatch k, System.Random r)
        {
            k.Cyl(new Vector3(0f, 0.02f, 0f), 7f, 0.03f, PoiStoneDark);
            k.Cyl(new Vector3(0f, 0.04f, 0f), 4.2f, 0.03f, PoiStone);
            k.Box(new Vector3(0f, 3.2f, 0f), new Vector3(1.4f, 6.4f, 0.5f), Quaternion.Euler(0f, 0f, 3f), new Color(0.20f, 0.20f, 0.25f));
            for (int i = 0; i < 5; i++)
                k.Box(new Vector3(0f, 1.2f + i * 1.0f, -0.27f), new Vector3(0.9f - i * 0.1f, 0.06f, 0.02f), Quaternion.Euler(0f, 0f, 3f), PoiGlyph, PoiGlyphEmit);
            k.Box(new Vector3(0f, 3.2f, -0.27f), new Vector3(0.06f, 5.4f, 0.02f), Quaternion.Euler(0f, 0f, 3f), PoiGlyph, PoiGlyphEmit * 0.8f);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                k.Box(new Vector3(Mathf.Cos(a) * 2.6f, 0.3f, Mathf.Sin(a) * 2.6f), new Vector3(0.7f, 0.6f, 0.7f), Quaternion.Euler(0f, a * 57f, 0f), PoiStoneDark);
                k.Box(new Vector3(Mathf.Cos(a) * 2.6f, 0.62f, Mathf.Sin(a) * 2.6f), new Vector3(0.2f, 0.04f, 0.2f), PoiGlyph, PoiGlyphEmit);
            }
        }

        private static void CrashedProbe(DetailBatch k, System.Random r)
        {
            // Scorched furrow and a raised rim where it ploughed in.
            k.Box(new Vector3(0f, 0.02f, -3f), new Vector3(2.8f, 0.03f, 9f), PoiScorch);
            for (int i = 0; i < 7; i++)
                k.Ball(new Vector3(Rf(r, -2.2f, 2.2f), 0.15f, 1.5f + Rf(r, -0.5f, 0.8f)), new Vector3(Rf(r, 0.8f, 1.6f), 0.5f, 0.9f), PoiStoneDark);
            // Hull: a tilted cylinder half buried, nose cone, heat shield.
            var tilt = Quaternion.Euler(70f, 0f, 12f);
            k.Add(PrimitiveType.Cylinder, new Vector3(0f, 0.9f, 0f), new Vector3(1.8f, 1.6f, 1.8f), tilt, PoiMetal);
            k.Add(PrimitiveType.Sphere, new Vector3(0.2f, 0.6f, 1.4f), new Vector3(1.8f, 1.8f, 1.6f), tilt, PoiMetal);
            k.Add(PrimitiveType.Cylinder, new Vector3(-0.15f, 1.3f, -1.5f), new Vector3(2.1f, 0.12f, 2.1f), tilt, new Color(0.30f, 0.20f, 0.12f));
            k.Box(new Vector3(0f, 1.6f, -0.4f), new Vector3(0.9f, 0.06f, 1.4f), tilt, new Color(0.96f, 0.42f, 0.08f));
            // One solar wing still on, one snapped off nearby.
            k.Box(new Vector3(1.9f, 1.4f, -0.2f), new Vector3(2.4f, 0.05f, 1.1f), Quaternion.Euler(10f, 0f, 24f), new Color(0.10f, 0.16f, 0.30f), new Color(0.04f, 0.18f, 0.55f));
            k.Box(new Vector3(-2.6f, 0.1f, -2.4f), new Vector3(2.2f, 0.05f, 1.0f), Quaternion.Euler(0f, 35f, 6f), new Color(0.10f, 0.16f, 0.30f), new Color(0.04f, 0.18f, 0.55f));
            k.Rod(new Vector3(0.4f, 1.9f, 0.2f), new Vector3(0.9f, 3.0f, 0.5f), 0.06f, PoiMetal);
            k.Ball(new Vector3(0.9f, 3.05f, 0.5f), 0.16f, new Color(0.9f, 0.16f, 0.08f), new Color(1.9f, 0.22f, 0.06f));
            for (int i = 0; i < 8; i++)
                k.Box(new Vector3(Rf(r, -3f, 3f), 0.08f, Rf(r, -6f, 2f)), new Vector3(Rf(r, 0.2f, 0.6f), 0.08f, Rf(r, 0.2f, 0.6f)), Quaternion.Euler(0f, Rf(r, 0f, 360f), Rf(r, 0f, 30f)), PoiMetal);
        }

        private static void SurveyCamp(DetailBatch k, System.Random r)
        {
            k.Cyl(new Vector3(0f, 0.02f, 0f), 9f, 0.03f, new Color(0.40f, 0.33f, 0.24f));
            // Two A-frame tents.
            for (int t = 0; t < 2; t++)
            {
                var c = new Vector3(-2.2f + t * 3.8f, 0f, 1.2f - t * 0.8f);
                float yaw = 20f + t * 50f;
                k.Box(c + Vector3.up * 0.75f + Quaternion.Euler(0f, yaw, 0f) * new Vector3(-0.55f, 0f, 0f), new Vector3(0.05f, 1.7f, 2.4f),
                    Quaternion.Euler(0f, yaw, 32f), PoiCloth);
                k.Box(c + Vector3.up * 0.75f + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0.55f, 0f, 0f), new Vector3(0.05f, 1.7f, 2.4f),
                    Quaternion.Euler(0f, yaw, -32f), PoiCloth * 0.85f);
            }
            // Crates, a barrel, a dead campfire and a comms mast with a dish.
            for (int i = 0; i < 5; i++)
                k.Box(new Vector3(2.4f + Rf(r, -0.6f, 0.6f), 0.3f + (i > 2 ? 0.6f : 0f), -1.8f + (i % 3) * 0.65f), Vector3.one * 0.6f,
                    Quaternion.Euler(0f, Rf(r, 0f, 30f), 0f), new Color(0.48f, 0.36f, 0.22f));
            k.Cyl(new Vector3(-0.6f, 0.45f, -2.4f), 0.6f, 0.9f, new Color(0.96f, 0.42f, 0.08f));
            k.Cyl(new Vector3(0f, 0.04f, -0.6f), 1.2f, 0.06f, PoiScorch);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                k.Ball(new Vector3(Mathf.Cos(a) * 0.7f, 0.1f, -0.6f + Mathf.Sin(a) * 0.7f), 0.28f, PoiStoneDark);
            }
            k.Rod(new Vector3(-3.4f, 0f, -1.6f), new Vector3(-3.4f, 3.6f, -1.6f), 0.08f, PoiMetal);
            k.Add(PrimitiveType.Sphere, new Vector3(-3.2f, 3.3f, -1.4f), new Vector3(1.2f, 0.2f, 1.2f), Quaternion.Euler(-35f, 40f, 0f), PoiMetal);
            k.Ball(new Vector3(-3.4f, 3.7f, -1.6f), 0.14f, new Color(0.9f, 0.16f, 0.08f), new Color(1.9f, 0.22f, 0.06f));
        }
    }
}
