using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Sheet-match accents and head alignment for campus fauna.
    /// Copilot3D / joined FBX often ships a single SM_White slot; IndustrialArtDressing
    /// maps that to a hide, and Dress_ primitives (skipped by that remapper) restore
    /// cyan eyes, orange nubs, and graphite plates from the Imagine turnarounds.
    /// </summary>
    public static class FaunaDressing
    {
        public const string AccentRootName = "Dress_Fauna";

        private static readonly Color CyanEye = new Color(0.22f, 0.82f, 0.98f);
        private static readonly Color CyanEmit = new Color(0.35f, 1.6f, 2.2f);
        private static readonly Color OrangeNub = new Color(0.92f, 0.42f, 0.08f);
        private static readonly Color OrangeEmit = new Color(0.55f, 0.16f, 0.02f);
        private static readonly Color Graphite = new Color(0.26f, 0.27f, 0.29f);
        private static readonly Color WhitePlate = new Color(0.86f, 0.82f, 0.72f);

        /// <summary>Visual mesh, including after UnitMotion reparents it under MotionRoot.</summary>
        public static Transform FindVisual(GameObject root)
        {
            if (root == null) return null;
            var ts = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < ts.Length; i++)
            {
                if (ts[i] != null && ts[i].name == "Visual")
                    return ts[i];
            }
            return null;
        }

        /// <summary>
        /// Keep FBX import X tilt, yaw so the longest horizontal mesh axis maps to parent +Z,
        /// then flip 180 if cyan markings sit at −Z (head was modeled backward).
        /// </summary>
        public static void AlignHead(GameObject root)
        {
            Transform visual = FindVisual(root);
            if (visual == null) return;
            Transform parent = visual.parent != null ? visual.parent : visual;

            Vector3 euler = visual.localEulerAngles;
            float importX = euler.x;
            float importZ = euler.z;

            visual.localRotation = Quaternion.Euler(importX, 0f, importZ);
            MeasureHorizontal(visual, parent, out float extX, out float extZ, out _, out _);

            float yaw = 0f;
            if (extX > extZ * 1.05f)
                yaw = 90f;

            visual.localRotation = Quaternion.Euler(importX, yaw, importZ);
            if (CyanCentroidIsBack(visual, parent))
                yaw += 180f;

            visual.localRotation = Quaternion.Euler(importX, yaw, importZ);
        }

        public static void Paint(GameObject root, FaunaKind kind)
        {
            if (root == null || kind == FaunaKind.JunkBot) return;

            Transform visual = FindVisual(root);
            Transform host = visual != null
                ? (visual.parent != null ? visual.parent : visual)
                : (root.transform.Find("MotionRoot") ?? root.transform);

            ClearAccents(host);
            if (!TryBodyAabb(host, visual != null ? visual : host, out Bounds local))
                local = new Bounds(new Vector3(0f, 0.2f, 0f), new Vector3(0.5f, 0.35f, 0.7f));

            var accent = new GameObject(AccentRootName).transform;
            accent.SetParent(host, false);

            Vector3 c = local.center;
            Vector3 sz = local.size;
            float eyeR = Mathf.Clamp(sz.y * 0.14f, 0.045f, 0.12f);
            float nubR = eyeR * 0.55f;
            float sep = Mathf.Max(0.05f, sz.x * 0.18f);
            float frontZ = c.z + sz.z * 0.42f;
            float eyeY = c.y + sz.y * 0.08f;

            switch (kind)
            {
                case FaunaKind.Mite:
                    Eyes(accent, c.x, eyeY, frontZ, sep, eyeR);
                    Nubs(accent, c.x, eyeY, frontZ + sz.z * 0.04f, sep * 1.35f, nubR);
                    DorsalPlates(accent, c, sz, Graphite, 3);
                    break;
                case FaunaKind.Hopper:
                    Eyes(accent, c.x, eyeY + sz.y * 0.06f, frontZ, sep * 0.7f, eyeR * 0.85f);
                    Nubs(accent, c.x, c.y, c.z, Mathf.Max(sep, sz.x * 0.42f), nubR * 1.15f);
                    Prim(PrimitiveType.Cube, accent, "Dress_Abdomen",
                        new Vector3(c.x, c.y - sz.y * 0.08f, c.z - sz.z * 0.22f),
                        new Vector3(sz.x * 0.55f, sz.y * 0.35f, sz.z * 0.38f),
                        Graphite);
                    break;
                case FaunaKind.Leech:
                    var groove = Prim(PrimitiveType.Capsule, accent, "Dress_Groove",
                        new Vector3(c.x, c.y + sz.y * 0.28f, c.z),
                        new Vector3(sz.x * 0.22f, sz.z * 0.48f, sz.x * 0.22f),
                        CyanEye, CyanEmit);
                    if (groove != null)
                        groove.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    Nubs(accent, c.x, eyeY, frontZ, sep * 0.85f, nubR);
                    break;
                case FaunaKind.Wisp:
                    Prim(PrimitiveType.Sphere, accent, "Dress_Core",
                        c, Vector3.one * Mathf.Clamp(sz.y * 0.42f, 0.12f, 0.28f),
                        CyanEye, CyanEmit);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * Mathf.PI * 2f / 6f;
                        Prim(PrimitiveType.Sphere, accent, "Dress_Nub_" + i,
                            c + new Vector3(Mathf.Sin(a) * sz.x * 0.28f, 0f, Mathf.Cos(a) * sz.z * 0.28f),
                            Vector3.one * nubR, OrangeNub, OrangeEmit);
                    }
                    break;
                case FaunaKind.Tick:
                    Eyes(accent, c.x, eyeY, frontZ, sep, eyeR);
                    Nubs(accent, c.x, c.y, frontZ + sz.z * 0.06f, sep * 1.6f, nubR * 1.4f);
                    break;
                case FaunaKind.Creeper:
                    Eyes(accent, c.x, eyeY, frontZ, sep, eyeR);
                    Nubs(accent, c.x, eyeY + sz.y * 0.06f, frontZ, sep * 1.2f, nubR);
                    Prim(PrimitiveType.Sphere, accent, "Dress_TailNub",
                        new Vector3(c.x, c.y, c.z - sz.z * 0.45f),
                        Vector3.one * nubR * 1.1f, OrangeNub, OrangeEmit);
                    break;
                default: // Stalker
                    Prim(PrimitiveType.Sphere, accent, "Dress_EyeL",
                        new Vector3(c.x - sep, eyeY, frontZ), Vector3.one * eyeR,
                        OrangeNub, OrangeEmit);
                    Prim(PrimitiveType.Sphere, accent, "Dress_EyeR",
                        new Vector3(c.x + sep, eyeY, frontZ), Vector3.one * eyeR,
                        OrangeNub, OrangeEmit);
                    DorsalPlates(accent, c, sz, WhitePlate, 4);
                    break;
            }
        }

        /// <summary>Renderer AABB of the body mesh, excluding IK Legs and Dress_ accents.</summary>
        public static bool MeasureBody(
            GameObject root, out float bodyRadius, out float hipHeight, out float legLength)
        {
            bodyRadius = 0.40f;
            hipHeight = 0.34f;
            legLength = 0.62f;
            if (root == null) return false;

            Transform visual = FindVisual(root);
            Transform probe = visual != null ? visual : root.transform;
            Transform host = probe.parent != null ? probe.parent : probe;
            if (!TryBodyAabb(host, probe, out Bounds local))
                return false;

            bodyRadius = Mathf.Max(0.12f, 0.50f * Mathf.Max(local.extents.x, local.extents.z));
            hipHeight = Mathf.Clamp(local.min.y + local.size.y * 0.42f, 0.10f, 0.95f);
            if (hipHeight < 0.12f)
                hipHeight = Mathf.Clamp(local.size.y * 0.42f, 0.12f, 0.85f);
            float ground = local.min.y;
            legLength = Mathf.Clamp(hipHeight - ground + bodyRadius * 0.35f, 0.28f, 1.9f);
            return true;
        }

        private static void Eyes(Transform host, float x, float y, float z, float sep, float r)
        {
            Prim(PrimitiveType.Sphere, host, "Dress_EyeL",
                new Vector3(x - sep, y, z), Vector3.one * r, CyanEye, CyanEmit);
            Prim(PrimitiveType.Sphere, host, "Dress_EyeR",
                new Vector3(x + sep, y, z), Vector3.one * r, CyanEye, CyanEmit);
        }

        private static void Nubs(Transform host, float x, float y, float z, float sep, float r)
        {
            Prim(PrimitiveType.Sphere, host, "Dress_NubL",
                new Vector3(x - sep, y, z), Vector3.one * r, OrangeNub, OrangeEmit);
            Prim(PrimitiveType.Sphere, host, "Dress_NubR",
                new Vector3(x + sep, y, z), Vector3.one * r, OrangeNub, OrangeEmit);
        }

        private static void DorsalPlates(Transform host, Vector3 c, Vector3 sz, Color color, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 0.5f : i / (float)(count - 1);
                float z = Mathf.Lerp(c.z + sz.z * 0.28f, c.z - sz.z * 0.32f, t);
                float w = sz.x * Mathf.Lerp(0.55f, 0.38f, t);
                Prim(PrimitiveType.Cube, host, "Dress_Plate_" + i,
                    new Vector3(c.x, c.y + sz.y * 0.38f, z),
                    new Vector3(w, Mathf.Max(0.03f, sz.y * 0.10f), sz.z * 0.16f),
                    color);
            }
        }

        private static GameObject Prim(
            PrimitiveType type,
            Transform parent,
            string name,
            Vector3 localPos,
            Vector3 localScale,
            Color color,
            Color emission = default)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = localScale;
            var col = go.GetComponent<Collider>();
            if (col != null) Kill(col);
            IndustrialArtDressing.Tint(go, color, emission.maxColorComponent > 0.01f ? 0.72f : 0.32f, emission);
            return go;
        }

        private static void ClearAccents(Transform host)
        {
            if (host == null) return;
            Transform existing = host.Find(AccentRootName);
            if (existing != null)
                Kill(existing.gameObject);
        }

        private static bool TryBodyAabb(Transform host, Transform visual, out Bounds local)
        {
            local = new Bounds();
            bool any = false;
            var filters = visual.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                var mf = filters[i];
                if (mf == null || mf.sharedMesh == null || ShouldSkip(mf.transform)) continue;
                Bounds mb = mf.sharedMesh.bounds;
                Vector3 c = mb.center;
                Vector3 e = mb.extents;
                for (int xi = -1; xi <= 1; xi += 2)
                for (int yi = -1; yi <= 1; yi += 2)
                for (int zi = -1; zi <= 1; zi += 2)
                {
                    Vector3 world = mf.transform.TransformPoint(
                        c + Vector3.Scale(e, new Vector3(xi, yi, zi)));
                    Vector3 p = host.InverseTransformPoint(world);
                    if (!any)
                    {
                        local = new Bounds(p, Vector3.zero);
                        any = true;
                    }
                    else local.Encapsulate(p);
                }
            }
            return any;
        }

        private static void MeasureHorizontal(
            Transform visual, Transform parent, out float extX, out float extZ, out float minZ, out float maxZ)
        {
            extX = extZ = 0f;
            minZ = maxZ = 0f;
            if (!TryBodyAabb(parent, visual, out Bounds b))
                return;
            extX = b.size.x;
            extZ = b.size.z;
            minZ = b.min.z;
            maxZ = b.max.z;
        }

        private static bool CyanCentroidIsBack(Transform visual, Transform parent)
        {
            var rends = visual.GetComponentsInChildren<Renderer>(true);
            Vector3 acc = Vector3.zero;
            int n = 0;
            for (int i = 0; i < rends.Length; i++)
            {
                var rend = rends[i];
                if (rend == null || ShouldSkip(rend.transform) || !LooksCyan(rend)) continue;
                acc += parent.InverseTransformPoint(rend.bounds.center);
                n++;
            }
            if (n == 0) return false;
            return (acc / n).z < -0.02f;
        }

        private static bool LooksCyan(Renderer rend)
        {
            var mats = rend.sharedMaterials;
            if (mats == null) return false;
            for (int m = 0; m < mats.Length; m++)
            {
                var mat = mats[m];
                if (mat == null) continue;
                string n = mat.name.ToLowerInvariant();
                if (n.Contains("cyan") || n.Contains("sm_scout") || n.Contains("sm_medic"))
                    return true;
                if (mat.HasProperty("_BaseColor"))
                {
                    Color c = mat.GetColor("_BaseColor");
                    if (c.b > 0.65f && c.g > 0.45f && c.r < 0.55f && c.b > c.r + 0.12f)
                        return true;
                }
            }
            return false;
        }

        private static bool ShouldSkip(Transform t)
        {
            while (t != null)
            {
                string n = t.name;
                if (n == "Legs" || n.StartsWith("Dress_") || n.Contains("Label"))
                    return true;
                t = t.parent;
            }
            return false;
        }

        private static void Kill(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }
    }
}
