using System;
using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Flat-shaded geodesic shell (subdivided icosahedron projected onto an ellipsoid) with
    /// every facet inset toward its centroid so a darker undershell shows through as seams,
    /// plus a dark rib mesh along unique edges so the triangle grid reads at play ortho 10.
    /// Coplanar triangular facets, not a scatter of tilted plates or square hull panels.
    /// </summary>
    public static class GeodesicDomeMesh
    {
        /// <param name="frequency">Subdivisions per icosahedron edge (4 → 320 sphere faces).</param>
        /// <param name="radii">Ellipsoid radii (x, y, z).</param>
        /// <param name="minY01">Keep facets whose centroid y / radii.y is at least this (-1..1).</param>
        /// <param name="inset">Fraction each facet shrinks toward its centroid (seam width).</param>
        public static Mesh Build(int frequency, Vector3 radii, float minY01, float inset)
        {
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            ForEachFacet(frequency, radii, minY01, (p0, p1, p2) =>
            {
                Vector3 centroid = (p0 + p1 + p2) / 3f;
                Vector3 n = Vector3.Cross(p1 - p0, p2 - p0);
                if (Vector3.Dot(n, centroid) < 0f)
                {
                    (p1, p2) = (p2, p1);
                    n = -n;
                }
                n.Normalize();
                float k = 1f - Mathf.Clamp01(inset);
                int baseIndex = verts.Count;
                verts.Add(centroid + (p0 - centroid) * k);
                verts.Add(centroid + (p1 - centroid) * k);
                verts.Add(centroid + (p2 - centroid) * k);
                for (int i = 0; i < 3; i++) norms.Add(n);
                uvs.Add(new Vector2(0.1f, 0.1f));
                uvs.Add(new Vector2(0.9f, 0.1f));
                uvs.Add(new Vector2(0.5f, 0.9f));
                tris.Add(baseIndex);
                tris.Add(baseIndex + 1);
                tris.Add(baseIndex + 2);
            });
            return Finish("SM_GeodesicDome", verts, norms, uvs, tris);
        }

        /// <summary>
        /// Dark strut grid along unique facet edges, slightly proud of the cream shell
        /// so the geodesic reads as lines at isometric Game-tab range.
        /// </summary>
        public static Mesh BuildRibs(int frequency, Vector3 radii, float minY01, float halfWidth)
        {
            halfWidth = Mathf.Max(0.012f, halfWidth);
            var edges = new Dictionary<long, Vector3[]>();
            ForEachFacet(frequency, radii, minY01, (p0, p1, p2) =>
            {
                RememberEdge(edges, p0, p1);
                RememberEdge(edges, p1, p2);
                RememberEdge(edges, p2, p0);
            });

            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            foreach (var pair in edges)
            {
                Vector3[] ends = pair.Value;
                EmitRib(verts, norms, uvs, tris, ends[0], ends[1], radii, halfWidth);
            }

            return Finish("SM_GeodesicRibs", verts, norms, uvs, tris);
        }

        private static void ForEachFacet(
            int frequency, Vector3 radii, float minY01, Action<Vector3, Vector3, Vector3> emit)
        {
            frequency = Mathf.Max(1, frequency);
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            Vector3[] ico =
            {
                new Vector3(-1f, t, 0f), new Vector3(1f, t, 0f), new Vector3(-1f, -t, 0f), new Vector3(1f, -t, 0f),
                new Vector3(0f, -1f, t), new Vector3(0f, 1f, t), new Vector3(0f, -1f, -t), new Vector3(0f, 1f, -t),
                new Vector3(t, 0f, -1f), new Vector3(t, 0f, 1f), new Vector3(-t, 0f, -1f), new Vector3(-t, 0f, 1f)
            };
            for (int i = 0; i < ico.Length; i++) ico[i].Normalize();
            int[] faces =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
                1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
                4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
            };

            for (int f = 0; f < faces.Length; f += 3)
            {
                Vector3 a = ico[faces[f]], b = ico[faces[f + 1]], c = ico[faces[f + 2]];
                for (int i = 0; i < frequency; i++)
                {
                    for (int j = 0; j < frequency - i; j++)
                    {
                        EmitIfKept(P(i, j), P(i + 1, j), P(i, j + 1));
                        if (j < frequency - 1 - i)
                            EmitIfKept(P(i + 1, j), P(i + 1, j + 1), P(i, j + 1));
                    }
                }

                Vector3 P(int i, int j)
                {
                    Vector3 p = a + (b - a) * (i / (float)frequency) + (c - a) * (j / (float)frequency);
                    p.Normalize();
                    return new Vector3(p.x * radii.x, p.y * radii.y, p.z * radii.z);
                }

                void EmitIfKept(Vector3 p0, Vector3 p1, Vector3 p2)
                {
                    Vector3 centroid = (p0 + p1 + p2) / 3f;
                    if (radii.y > 0f && centroid.y / radii.y < minY01) return;
                    emit(p0, p1, p2);
                }
            }
        }

        private static void RememberEdge(Dictionary<long, Vector3[]> edges, Vector3 a, Vector3 b)
        {
            long key = EdgeKey(a, b);
            if (!edges.ContainsKey(key))
                edges[key] = new[] { a, b };
        }

        private static long EdgeKey(Vector3 a, Vector3 b)
        {
            int ax = Q(a.x), ay = Q(a.y), az = Q(a.z);
            int bx = Q(b.x), by = Q(b.y), bz = Q(b.z);
            bool aFirst = ax < bx || (ax == bx && (ay < by || (ay == by && az <= bz)));
            int x0 = aFirst ? ax : bx, y0 = aFirst ? ay : by, z0 = aFirst ? az : bz;
            int x1 = aFirst ? bx : ax, y1 = aFirst ? by : ay, z1 = aFirst ? bz : az;
            return Pack3(x0, y0, z0) * 1000003L ^ Pack3(x1, y1, z1);
        }

        private static int Q(float v) => Mathf.RoundToInt(v * 250f);

        private static long Pack3(int x, int y, int z)
        {
            unchecked
            {
                return ((long)(x + 50000) << 40) ^ ((long)(y + 50000) << 20) ^ (uint)(z + 50000);
            }
        }

        private static void EmitRib(
            List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris,
            Vector3 p0, Vector3 p1, Vector3 radii, float halfW)
        {
            Vector3 along = p1 - p0;
            float len = along.magnitude;
            if (len < 0.02f) return;
            along /= len;
            Vector3 mid = (p0 + p1) * 0.5f;
            Vector3 outward = EllipsoidNormal(mid, radii);
            Vector3 side = Vector3.Cross(along, outward);
            if (side.sqrMagnitude < 1e-6f)
                side = Vector3.Cross(along, Vector3.up);
            side.Normalize();
            Vector3 up = Vector3.Cross(side, along).normalized;
            float proud = halfW * 0.55f;
            Vector3 a = p0 + up * proud;
            Vector3 b = p1 + up * proud;
            Vector3 s = side * halfW;
            Vector3 u = up * (halfW * 0.42f);

            Vector3[] c =
            {
                a - s - u, a + s - u, a + s + u, a - s + u,
                b - s - u, b + s - u, b + s + u, b - s + u
            };
            int[] faces =
            {
                0, 1, 5, 0, 5, 4,
                1, 2, 6, 1, 6, 5,
                2, 3, 7, 2, 7, 6,
                3, 0, 4, 3, 4, 7
            };
            int @base = verts.Count;
            for (int i = 0; i < 8; i++)
            {
                verts.Add(c[i]);
                norms.Add(up);
                uvs.Add(new Vector2((i & 1) == 0 ? 0.1f : 0.9f, i < 4 ? 0.1f : 0.9f));
            }

            for (int i = 0; i < faces.Length; i++)
                tris.Add(@base + faces[i]);
        }

        private static Vector3 EllipsoidNormal(Vector3 p, Vector3 radii)
        {
            Vector3 n = new Vector3(
                p.x / Mathf.Max(1e-4f, radii.x * radii.x),
                p.y / Mathf.Max(1e-4f, radii.y * radii.y),
                p.z / Mathf.Max(1e-4f, radii.z * radii.z));
            if (n.sqrMagnitude < 1e-8f) n = Vector3.up;
            n.Normalize();
            return n;
        }

        private static Mesh Finish(
            string name, List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris)
        {
            var mesh = new Mesh { name = name };
            if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
