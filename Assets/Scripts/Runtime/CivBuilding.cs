using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Marks a non-castle building drawn from the colony's civ mesh
    /// (<see cref="CivBuildingLoader"/>). Holds the authored LOD bodies when the FBX names them
    /// <c>_LOD0</c>..<c>_LOD2</c>, the same convention as <see cref="CivCastle"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CivBuilding : MonoBehaviour
    {
        public CivId Civ;
        public BuildingCategory Category;
        /// <summary>Citadel file stem: Ziggurat, Ring, Dome or Arcology.</summary>
        public string Architecture;
        /// <summary>Resources path the mesh was loaded from.</summary>
        public string ResourcePath;
        public Transform Model;
        public Renderer[] Lod0 = new Renderer[0];
        public Renderer[] Lod1 = new Renderer[0];
        public Renderer[] Lod2 = new Renderer[0];
        /// <summary>Meshes with no LOD suffix. Drawn in every level.</summary>
        public Renderer[] Rest = new Renderer[0];
        /// <summary>The model's own LODGroup. Null when the FBX has no <c>_LOD0</c> mesh; <see cref="BuildingLod"/> then groups the building the usual way.</summary>
        public LODGroup Group;

        /// <summary>
        /// LOD0 → LOD1, as a fraction of screen height. A 6 m plot at play zoom (ortho 18) covers
        /// about 0.167 of the screen, and about 0.05 on the Very Low bias, so this stays under
        /// both and play zoom keeps LOD0. The castle's 0.30 is for the 18 m Commons.
        /// </summary>
        public const float Lod1Below = BuildingLodRules.DetailScreenHeight;

        /// <summary>LOD1 → LOD2. Still above <see cref="BuildingLodRules.CullScreenHeight"/>.</summary>
        public const float Lod2Below = 0.02f;

        /// <summary>Point <see cref="Group"/> at the three bodies. Safe to call again.</summary>
        public LODGroup ConfigureLods()
        {
            if (Group == null || Lod0 == null || Lod0.Length == 0) return Group;
            Renderer[] Level(Renderer[] body, Renderer[] fallback)
            {
                var src = body != null && body.Length > 0 ? body : fallback;
                var all = new List<Renderer>(src);
                if (Rest != null) all.AddRange(Rest);
                all.RemoveAll(r => r == null);
                return all.ToArray();
            }
            Group.fadeMode = LODFadeMode.None;
            Group.SetLODs(new[]
            {
                new LOD(Lod1Below, Level(Lod0, Lod0)),
                new LOD(Lod2Below, Level(Lod1, Lod0)),
                new LOD(BuildingLodRules.CullScreenHeight, Level(Lod2, Lod1 != null && Lod1.Length > 0 ? Lod1 : Lod0))
            });
            Group.RecalculateBounds();
            return Group;
        }

        /// <summary>Placement ghosts get no LODGroup: show the full-detail body only.</summary>
        public void ShowOnlyLod0()
        {
            if (Lod1 != null)
                for (int i = 0; i < Lod1.Length; i++) if (Lod1[i] != null) Lod1[i].enabled = false;
            if (Lod2 != null)
                for (int i = 0; i < Lod2.Length; i++) if (Lod2[i] != null) Lod2[i].enabled = false;
        }
    }
}
