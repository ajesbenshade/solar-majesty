using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Marks a Commons drawn as a civ castle and keeps what <see cref="BuildingLod"/> and the
    /// dressing need: the model, its three LOD meshes and the civ.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CivCastle : MonoBehaviour
    {
        public CivId Civ;
        public string CastleKey;
        public Transform Model;
        public Renderer[] Lod0 = new Renderer[0];
        public Renderer[] Lod1 = new Renderer[0];
        public Renderer[] Lod2 = new Renderer[0];
        public Renderer[] Core = new Renderer[0];
        /// <summary>The castle's own LODGroup, on <see cref="Model"/>. The only group that holds castle renderers.</summary>
        public LODGroup Group;

        /// <summary>LOD thresholds: the Commons is the biggest building, 18 m.</summary>
        public const float Lod1Below = 0.30f;
        public const float Lod2Below = 0.12f;

        /// <summary>
        /// Point <see cref="Group"/> at the castle's three bodies, with the Core module in every
        /// level. Safe to call again (BuildingLod calls it after dressing).
        /// </summary>
        public LODGroup ConfigureLods()
        {
            if (Group == null || Lod0.Length == 0) return Group;
            Renderer[] Level(Renderer[] body, Renderer[] fallback)
            {
                var src = body != null && body.Length > 0 ? body : fallback;
                var all = new System.Collections.Generic.List<Renderer>(src);
                all.AddRange(Core);
                all.RemoveAll(r => r == null);
                return all.ToArray();
            }
            Group.fadeMode = LODFadeMode.None;
            Group.SetLODs(new[]
            {
                new LOD(Lod1Below, Level(Lod0, Lod0)),
                new LOD(Lod2Below, Level(Lod1, Lod0)),
                new LOD(BuildingLodRules.CullScreenHeight, Level(Lod2, Lod1.Length > 0 ? Lod1 : Lod0))
            });
            Group.RecalculateBounds();
            return Group;
        }

        /// <summary>True when <paramref name="r"/> is part of the castle (any LOD body or the Core).</summary>
        public bool Owns(Renderer r) => IsCastleLod(r) || (r != null && System.Array.IndexOf(Core, r) >= 0);

        /// <summary>Placement ghosts get no LODGroup: show the full-detail body only.</summary>
        public void ShowOnlyLod0()
        {
            for (int i = 0; i < Lod1.Length; i++) if (Lod1[i] != null) Lod1[i].enabled = false;
            for (int i = 0; i < Lod2.Length; i++) if (Lod2[i] != null) Lod2[i].enabled = false;
        }

        public bool IsCastleLod(Renderer r)
        {
            if (r == null) return false;
            return System.Array.IndexOf(Lod0, r) >= 0 || System.Array.IndexOf(Lod1, r) >= 0 ||
                   System.Array.IndexOf(Lod2, r) >= 0;
        }
    }
}
