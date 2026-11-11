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

        /// <summary>LOD thresholds: the Commons is the biggest building, 18 m.</summary>
        public const float Lod1Below = 0.30f;
        public const float Lod2Below = 0.12f;

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
