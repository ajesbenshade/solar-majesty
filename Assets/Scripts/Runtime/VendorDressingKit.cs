using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Serialized refs to Asset Store packs that live outside Resources:
    /// Kevin Iglesias Human Basic Motions (colonist walk/idle) and Polytope
    /// Studio nature (Earth trees/rocks/grass). Built by VendorDressingKitBuilder.
    /// Dressing only — no FlagTypes, no SpecialistBrain.
    /// </summary>
    [CreateAssetMenu(menuName = "Solar Majesty/Vendor Dressing Kit", fileName = "VendorDressingKit")]
    public sealed class VendorDressingKit : ScriptableObject
    {
        public const string ResourcePath = "Dressing/VendorDressingKit";

        [Header("Human Basic Motions")]
        public GameObject dummyMale;
        public GameObject dummyFemale;
        public Avatar maleAvatar;
        public Avatar femaleAvatar;
        public RuntimeAnimatorController motionsController;
        public AnimationClip idleMale;
        public AnimationClip walkMale;
        public AnimationClip idleFemale;
        public AnimationClip walkFemale;

        [Header("Earth nature (Polytope)")]
        public GameObject[] earthTrees;
        public GameObject[] earthRocks;
        public GameObject[] earthShrubs;
        public GameObject[] earthGrass;
        public GameObject[] earthFlowers;

        private static VendorDressingKit _cached;

        public static VendorDressingKit Load()
        {
            if (_cached == null)
                _cached = Resources.Load<VendorDressingKit>(ResourcePath);
            return _cached;
        }

        public bool HasWalker =>
            (dummyMale != null || dummyFemale != null) &&
            motionsController != null &&
            (walkMale != null || walkFemale != null);

        public bool HasEarthNature =>
            earthTrees != null && earthTrees.Length > 0;

        public GameObject Dummy(bool female) =>
            female && dummyFemale != null ? dummyFemale : dummyMale != null ? dummyMale : dummyFemale;

        public Avatar AvatarFor(bool female) =>
            female && femaleAvatar != null ? femaleAvatar : maleAvatar != null ? maleAvatar : femaleAvatar;

        public AnimationClip Idle(bool female) =>
            female && idleFemale != null ? idleFemale : idleMale != null ? idleMale : idleFemale;

        public AnimationClip Walk(bool female) =>
            female && walkFemale != null ? walkFemale : walkMale != null ? walkMale : walkFemale;

        public static GameObject Pick(GameObject[] list, int salt)
        {
            if (list == null || list.Length == 0) return null;
            for (int i = 0; i < list.Length; i++)
            {
                var go = list[Mathf.Abs(salt + i) % list.Length];
                if (go != null) return go;
            }
            return null;
        }
    }
}
