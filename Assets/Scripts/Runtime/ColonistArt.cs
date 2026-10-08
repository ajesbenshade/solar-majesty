using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Authored colonist suits (art batch 3, Blender/scripts/sm_colonists.py). One prefab per role in
    /// Resources/Colonists/SM_Colonist_&lt;Variant&gt;. Each is a Humanoid (Kevin Iglesias joint names) with an
    /// Animator running SM_Colonist.controller: Idle01 / Walk01_Forward from Human Basic Motions, switched
    /// by the float <see cref="SpeedParam"/>. Root motion is off; the owner moves the transform.
    /// Materials: slot 0 SM_Art_Colonist_Suit (shared palette atlas), slot 1 SM_Art_Colonist_Role_&lt;Variant&gt;
    /// (role trim). Do not run IndustrialArtDressing over these renderers.
    /// </summary>
    public static class ColonistArt
    {
        public const string ResourceDir = "Colonists/";
        public const string PrefabPrefix = "SM_Colonist_";
        public const string SpeedParam = "Speed";
        public static readonly int SpeedHash = Animator.StringToHash(SpeedParam);

        /// <summary>
        /// Ground speed (m/s) the retargeted, in-place Walk01 cycle matches at 1x on the 1.26 m suit
        /// (measured in Unity from planted-foot slide: 0.99 male / 0.91 female clip).
        /// </summary>
        public const float StrideSpeed = 0.95f;

        /// <summary>Fastest playback rate; VillagerAgent's 2.4 m/s lands just under it (a Majesty-style scurry).</summary>
        public const float MaxPlayback = 2.5f;

        public static readonly string[] Variants = { "Engineer", "Hydroponics", "Medic", "Hauler" };

        public static string PrefabName(int salt) =>
            PrefabPrefix + Variants[((salt % Variants.Length) + Variants.Length) % Variants.Length];

        public static GameObject Load(int salt) => Resources.Load<GameObject>(ResourceDir + PrefabName(salt));

        /// <summary>
        /// Instantiate the suit for <paramref name="salt"/> as child "Visual" (faces the parent's +Z).
        /// Returns null when the prefab is missing so callers keep their placeholder.
        /// </summary>
        public static GameObject Attach(Transform parent, int salt)
        {
            if (parent == null) return null;
            var prefab = Load(salt);
            if (prefab == null) return null;
            var visual = Object.Instantiate(prefab, parent, false);
            visual.name = "Visual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            return visual;
        }

        /// <summary>Animator playback rate so feet roughly match ground speed (1x when standing).</summary>
        public static float PlaybackSpeed(float groundSpeed) =>
            groundSpeed < 0.05f ? 1f : Mathf.Clamp(groundSpeed / StrideSpeed, 0.75f, MaxPlayback);

        /// <summary>Drive Idle/Walk on a suit's Animator from the owner's ground speed.</summary>
        public static void Drive(Animator anim, float groundSpeed)
        {
            if (anim == null || anim.runtimeAnimatorController == null) return;
            anim.SetFloat(SpeedHash, groundSpeed);
            anim.speed = PlaybackSpeed(groundSpeed);
        }
    }
}
