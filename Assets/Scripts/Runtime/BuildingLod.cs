using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Puts a LODGroup on a spawned building: LOD0 is every part, LOD1 drops the small detail
    /// parts, and the building culls below a tiny screen height. Which parts count as detail and
    /// the thresholds live in <see cref="BuildingLodRules"/>.
    ///
    /// Only the renderers present now are assigned. Anything added later (selection ring, status
    /// orb, labels, construction crane, campus dressing) is outside the group and renders as it
    /// always did. Overlays and ghosts are skipped. ConstructionStages toggles renderer.enabled,
    /// which still wins over the LOD.
    /// </summary>
    public static class BuildingLod
    {
        public static LODGroup Apply(GameObject root)
        {
            if (root == null || root.name.StartsWith("Ghost")) return null;
            var castle = root.GetComponent<CivCastle>();
            if (castle != null && castle.Lod0.Length > 0) return ApplyCastle(root, castle);

            Transform top = root.transform;
            var found = root.GetComponentsInChildren<Renderer>();
            var rends = new List<Renderer>(found.Length);
            var extents = new List<float>(found.Length);
            var dressing = new List<bool>(found.Length);
            Bounds total = default;

            for (int i = 0; i < found.Length; i++)
            {
                var r = found[i];
                if (r == null || !(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
                if (UnderOverlay(r.transform, top, out bool dress)) continue;

                Bounds b = r.bounds;
                if (rends.Count == 0) total = b;
                else total.Encapsulate(b);
                rends.Add(r);
                extents.Add(Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)));
                dressing.Add(dress);
            }
            if (rends.Count == 0) return null;

            float size = Mathf.Max(total.size.x, Mathf.Max(total.size.y, total.size.z));
            List<int> keep = BuildingLodRules.SelectLod1(extents, dressing, size);

            var group = root.GetComponent<LODGroup>();
            if (group == null) group = root.AddComponent<LODGroup>();
            // Cross-fading needs LOD_FADE_CROSSFADE in every hull shader; a pop at this size is invisible.
            group.fadeMode = LODFadeMode.None;

            Renderer[] all = rends.ToArray();
            LOD[] lods;
            if (keep.Count < all.Length)
            {
                var lod1 = new Renderer[keep.Count];
                for (int i = 0; i < keep.Count; i++)
                    lod1[i] = all[keep[i]];
                lods = new[]
                {
                    new LOD(BuildingLodRules.DetailScreenHeight, all),
                    new LOD(BuildingLodRules.CullScreenHeight, lod1)
                };
            }
            else
            {
                // Nothing small enough to drop: one level that only culls.
                lods = new[] { new LOD(BuildingLodRules.CullScreenHeight, all) };
            }

            group.SetLODs(lods);
            group.RecalculateBounds();
            return group;
        }

        /// <summary>
        /// A civ castle brings its own three LOD meshes (3D Artist: about 6-9k, 4k and 1.5k
        /// triangles). Everything else on the building (the Core module, per-world dressing) is
        /// in every level, so only the castle body steps down.
        /// </summary>
        private static LODGroup ApplyCastle(GameObject root, CivCastle castle)
        {
            Transform top = root.transform;
            var shared = new List<Renderer>();
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                if (r == null || !(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
                if (castle.IsCastleLod(r)) continue;
                if (UnderOverlay(r.transform, top, out _)) continue;
                shared.Add(r);
            }
            Renderer[] Level(Renderer[] body, Renderer[] fallback)
            {
                var src = body != null && body.Length > 0 ? body : fallback;
                var all = new List<Renderer>(src);
                all.AddRange(shared);
                return all.ToArray();
            }
            var l0 = Level(castle.Lod0, castle.Lod0);
            var l1 = Level(castle.Lod1, castle.Lod0);
            var l2 = Level(castle.Lod2, castle.Lod1.Length > 0 ? castle.Lod1 : castle.Lod0);

            var group = root.GetComponent<LODGroup>();
            if (group == null) group = root.AddComponent<LODGroup>();
            group.fadeMode = LODFadeMode.None;
            group.SetLODs(new[]
            {
                new LOD(CivCastle.Lod1Below, l0),
                new LOD(CivCastle.Lod2Below, l1),
                new LOD(BuildingLodRules.CullScreenHeight, l2)
            });
            group.RecalculateBounds();
            return group;
        }

        /// <summary>
        /// Walk up to the building root: true if any level is an overlay. Reports whether the
        /// renderer sits under the per-world dressing.
        /// </summary>
        private static bool UnderOverlay(Transform t, Transform top, out bool dressing)
        {
            dressing = false;
            while (t != null && t != top)
            {
                string n = t.name;
                if (BuildingLodRules.IsOverlay(n)) return true;
                if (BuildingLodRules.IsDressing(n)) dressing = true;
                t = t.parent;
            }
            return false;
        }
    }
}
