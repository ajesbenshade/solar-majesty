using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Flat-shaded geodesic shell (subdivided icosahedron projected onto an ellipsoid) with
    /// every facet inset toward its centroid so a darker undershell shows through as seams.
    /// Used for the Commons dome: coplanar triangular facets, not a scatter of tilted plates.
    /// </summary>
    public static class GeodesicDomeMesh
    {
        /// <param name="frequency">Subdivisions per icosahedron edge (4 → 320 sphere faces).</param>
        /// <param name="radii">Ellipsoid radii (x, y, z).</param>
        /// <param name="minY01">Keep facets whose centroid y / radii.y is at least this (-1..1).</param>
        /// <param name="inset">Fraction each facet shrinks toward its centroid (seam width).</param>
        public static Mesh Build(int frequency, Vector3 radii, float minY01, float inset)
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

            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            for (int f = 0; f < faces.Length; f += 3)
            {
                Vector3 a = ico[faces[f]], b = ico[faces[f + 1]], c = ico[faces[f + 2]];
                for (int i = 0; i < frequency; i++)
                {
                    for (int j = 0; j < frequency - i; j++)
                    {
                        Emit(P(i, j), P(i + 1, j), P(i, j + 1));
                        if (j < frequency - 1 - i)
                            Emit(P(i + 1, j), P(i + 1, j + 1), P(i, j + 1));
                    }
                }

                Vector3 P(int i, int j)
                {
                    Vector3 p = a + (b - a) * (i / (float)frequency) + (c - a) * (j / (float)frequency);
                    p.Normalize();
                    return new Vector3(p.x * radii.x, p.y * radii.y, p.z * radii.z);
                }
            }

            var mesh = new Mesh { name = "SM_GeodesicDome" };
            if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;

            void Emit(Vector3 p0, Vector3 p1, Vector3 p2)
            {
                Vector3 centroid = (p0 + p1 + p2) / 3f;
                if (radii.y > 0f && centroid.y / radii.y < minY01) return;
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
            }
        }
    }
}
