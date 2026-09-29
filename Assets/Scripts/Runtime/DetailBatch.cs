using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SolarMajesty
{
    /// <summary>
    /// Builds hundreds of small kit parts (vents, rails, lights, greebles) and merges them into a
    /// handful of meshes: one per construction stage, height band and colour. A building keeps a
    /// few dozen renderers however dense its detail is, and <see cref="ConstructionStages"/> can
    /// still reveal it stage by stage and bottom-up (each merged mesh is named <c>Dress_S{n}_…</c>).
    ///
    /// Merged meshes are cached per <c>cacheKey</c>, so the second habitat of a size reuses the
    /// first one's geometry; materials are shared per colour. Parts are authored in the root's
    /// local space.
    /// </summary>
    public sealed class DetailBatch
    {
        /// <summary>Height of each reveal band. Smaller bands sweep smoother but cost renderers.</summary>
        private const float BandHeight = 1.1f;

        private struct Part
        {
            public PrimitiveType Type;
            public Matrix4x4 Matrix;
        }

        private struct Key
        {
            public int Stage, Band;
            public Color Color, Emission;
            public bool Plated;
        }

        private sealed class Group
        {
            public Key Key;
            public readonly List<Part> Parts = new List<Part>();
        }

        private sealed class CachedGroup
        {
            public Key Key;
            public Mesh Mesh;
        }

        private static readonly Dictionary<string, List<CachedGroup>> MeshCache = new Dictionary<string, List<CachedGroup>>();
        private static readonly Dictionary<string, Material> MaterialCache = new Dictionary<string, Material>();
        private static readonly Dictionary<PrimitiveType, Mesh> PrimMeshes = new Dictionary<PrimitiveType, Mesh>();
        private static Shader _hull, _lit;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            MeshCache.Clear();
            MaterialCache.Clear();
            PrimMeshes.Clear();
        }

        private readonly Transform _root;
        private readonly string _cacheKey;
        private readonly bool _cached;
        private readonly List<Group> _groups = new List<Group>();

        /// <summary>Stage new parts go to until changed (see <see cref="ConstructionStages"/>).</summary>
        public int Stage = ConstructionStages.FitOut;

        public DetailBatch(Transform root, string cacheKey)
        {
            _root = root;
            _cacheKey = cacheKey;
            _cached = !string.IsNullOrEmpty(cacheKey) && MeshCache.ContainsKey(cacheKey);
        }

        public void Box(Vector3 pos, Vector3 size, Color c, Color emit = default) =>
            Add(PrimitiveType.Cube, pos, size, Quaternion.identity, c, emit);

        public void Box(Vector3 pos, Vector3 size, Quaternion rot, Color c, Color emit = default) =>
            Add(PrimitiveType.Cube, pos, size, rot, c, emit);

        /// <summary>Cylinder of full height <paramref name="height"/> (Unity's is 2 m tall at scale 1).</summary>
        public void Cyl(Vector3 pos, float diameter, float height, Color c, Color emit = default) =>
            Add(PrimitiveType.Cylinder, pos, new Vector3(diameter, height * 0.5f, diameter), Quaternion.identity, c, emit);

        public void Cyl(Vector3 pos, float diameter, float height, Quaternion rot, Color c, Color emit = default) =>
            Add(PrimitiveType.Cylinder, pos, new Vector3(diameter, height * 0.5f, diameter), rot, c, emit);

        public void Ball(Vector3 pos, float diameter, Color c, Color emit = default) =>
            Add(PrimitiveType.Sphere, pos, Vector3.one * diameter, Quaternion.identity, c, emit);

        public void Ball(Vector3 pos, Vector3 size, Color c, Color emit = default) =>
            Add(PrimitiveType.Sphere, pos, size, Quaternion.identity, c, emit);

        /// <summary>A round bar from <paramref name="a"/> to <paramref name="b"/>.</summary>
        public void Rod(Vector3 a, Vector3 b, float diameter, Color c, Color emit = default)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 1e-4f) return;
            Add(PrimitiveType.Cylinder, (a + b) * 0.5f, new Vector3(diameter, len * 0.5f, diameter),
                Quaternion.FromToRotation(Vector3.up, d / len), c, emit);
        }

        /// <summary>A square beam from <paramref name="a"/> to <paramref name="b"/>.</summary>
        public void Beam(Vector3 a, Vector3 b, float thick, Color c, Color emit = default)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 1e-4f) return;
            Add(PrimitiveType.Cube, (a + b) * 0.5f, new Vector3(thick, len, thick),
                Quaternion.FromToRotation(Vector3.up, d / len), c, emit);
        }

        /// <summary>Large surface part that should carry the hull shader's panel seams.</summary>
        public void Plate(Vector3 pos, Vector3 size, Quaternion rot, Color c) =>
            Add(PrimitiveType.Cube, pos, size, rot, c, default, plated: true);

        public void Add(PrimitiveType type, Vector3 pos, Vector3 scale, Quaternion rot, Color c,
            Color emit = default, bool plated = false)
        {
            if (_cached) return;
            int band = Mathf.Max(0, Mathf.FloorToInt(pos.y / BandHeight));
            var key = new Key
            {
                Stage = Mathf.Clamp(Stage, 0, ConstructionStages.Count - 1),
                Band = band,
                Color = c,
                Emission = emit.maxColorComponent > 0.01f ? emit : Color.clear,
                Plated = plated
            };
            Group g = null;
            for (int i = _groups.Count - 1; i >= 0; i--)
            {
                if (Same(_groups[i].Key, key)) { g = _groups[i]; break; }
            }
            if (g == null)
            {
                g = new Group { Key = key };
                _groups.Add(g);
            }
            g.Parts.Add(new Part { Type = type, Matrix = Matrix4x4.TRS(pos, rot, scale) });
        }

        /// <summary>Merge everything added so far into renderers under the root.</summary>
        public void Build()
        {
            if (_root == null) return;
            List<CachedGroup> built;
            if (_cached)
                built = MeshCache[_cacheKey];
            else
            {
                built = new List<CachedGroup>(_groups.Count);
                for (int i = 0; i < _groups.Count; i++)
                    built.Add(new CachedGroup { Key = _groups[i].Key, Mesh = Merge(_groups[i]) });
                if (!string.IsNullOrEmpty(_cacheKey))
                    MeshCache[_cacheKey] = built;
            }

            for (int i = 0; i < built.Count; i++)
            {
                var g = built[i];
                var go = new GameObject($"Dress_S{g.Key.Stage}_Detail_{g.Key.Band}_{i}");
                go.transform.SetParent(_root, false);
                go.AddComponent<MeshFilter>().sharedMesh = g.Mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = MaterialFor(g.Key);
                bool glow = g.Key.Emission.maxColorComponent > 0.01f;
                mr.shadowCastingMode = glow ? ShadowCastingMode.Off : ShadowCastingMode.On;
                mr.receiveShadows = true;
            }
            _groups.Clear();
        }

        private static bool Same(in Key a, in Key b) =>
            a.Stage == b.Stage && a.Band == b.Band && a.Plated == b.Plated &&
            a.Color == b.Color && a.Emission == b.Emission;

        private static Mesh Merge(Group g)
        {
            int vCount = 0, iCount = 0;
            for (int i = 0; i < g.Parts.Count; i++)
            {
                var m = PrimMesh(g.Parts[i].Type);
                vCount += m.vertexCount;
                iCount += (int)m.GetIndexCount(0);
            }

            var verts = new List<Vector3>(vCount);
            var norms = new List<Vector3>(vCount);
            var uvs = new List<Vector2>(vCount);
            var tris = new List<int>(iCount);
            var srcV = new List<Vector3>();
            var srcN = new List<Vector3>();
            var srcUv = new List<Vector2>();
            var srcT = new List<int>();

            for (int i = 0; i < g.Parts.Count; i++)
            {
                var part = g.Parts[i];
                var m = PrimMesh(part.Type);
                m.GetVertices(srcV);
                m.GetNormals(srcN);
                m.GetUVs(0, srcUv);
                m.GetTriangles(srcT, 0);
                Matrix4x4 mat = part.Matrix;
                Matrix4x4 nmat = mat.inverse.transpose;
                int baseV = verts.Count;
                for (int v = 0; v < srcV.Count; v++)
                {
                    verts.Add(mat.MultiplyPoint3x4(srcV[v]));
                    norms.Add(nmat.MultiplyVector(srcN[v]).normalized);
                    uvs.Add(v < srcUv.Count ? srcUv[v] : Vector2.zero);
                }
                for (int t = 0; t < srcT.Count; t++)
                    tris.Add(baseV + srcT[t]);
            }

            var mesh = new Mesh { name = "Detail" };
            if (verts.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh PrimMesh(PrimitiveType type)
        {
            if (PrimMeshes.TryGetValue(type, out var mesh) && mesh != null) return mesh;
            var go = GameObject.CreatePrimitive(type);
            mesh = go.GetComponent<MeshFilter>().sharedMesh;
            ColonyVisualUtility.DestroyNow(go);
            PrimMeshes[type] = mesh;
            return mesh;
        }

        /// <summary>Shared kit material for a colour — same look as HeroBuildingKits' small parts.</summary>
        public static Material SharedMaterial(Color c, Color emit = default, bool plated = false) =>
            MaterialFor(new Key { Color = c, Emission = emit.maxColorComponent > 0.01f ? emit : Color.clear, Plated = plated });

        private static Material MaterialFor(in Key k)
        {
            string id = $"{(Color32)k.Color}|{k.Emission.r:0.00},{k.Emission.g:0.00},{k.Emission.b:0.00}|{k.Plated}";
            if (MaterialCache.TryGetValue(id, out var mat) && mat != null) return mat;

            if (_lit == null)
            {
                _hull = Shader.Find("SolarMajesty/Hull");
                _lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            }
            bool hull = _hull != null;
            mat = new Material(hull ? _hull : _lit) { name = "SM_Detail_" + id };
            Color c = k.Color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_Color")) mat.color = c;
            bool dark = c.maxColorComponent < 0.2f;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", dark ? 0.34f : 0.26f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", dark ? 0.18f : 0.08f);
            if (hull)
            {
                mat.SetFloat("_PanelScale", k.Plated ? 0.9f : 6f);
                mat.SetFloat("_PanelWidth", 0.016f);
                mat.SetFloat("_PanelDarken", k.Plated ? 0.28f : 0f);
                mat.SetFloat("_PanelBevel", k.Plated ? 0.20f : 0f);
                mat.SetColor("_WearColor", new Color(0.42f, 0.37f, 0.32f, 1f));
                mat.SetFloat("_WearAmount", dark ? 0.08f : 0.12f);
                mat.SetFloat("_WearScale", 7f);
                mat.SetColor("_DustColor", new Color(0.58f, 0.36f, 0.22f, 1f));
                mat.SetFloat("_DustAmount", dark ? 0.05f : 0.12f);
                mat.SetFloat("_DustSharpness", 3.6f);
                mat.SetFloat("_EmissionBandWidth", 0f);
            }
            if (k.Emission.maxColorComponent > 0.01f && mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", k.Emission);
            }
            MaterialCache[id] = mat;
            return mat;
        }
    }
}
