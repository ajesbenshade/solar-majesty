using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Worker-bot villagers (canon: the player is the central AI, villagers are worker bots;
    /// Blender/scripts/sm_worker_bots.py). One prefab per role in Resources/Colonists/SM_Colonist_&lt;Variant&gt;
    /// (resource names and role IDs unchanged; "Medic" holds the Repair bot). Each is a Humanoid on the
    /// shared colonist skeleton (Kevin Iglesias-style joint names) with an Animator running
    /// SM_Colonist.controller: Idle = the original worker-bot idle (Art/Colonists/Anim/SM_WorkerBot@Idle.fbx)
    /// and Walk = the original "colony stride" lope (Art/Colonists/Anim/SM_Colonist@Stride.fbx,
    /// Blender/scripts/sm_colonist_stride.py), switched by the float <see cref="SpeedParam"/>. Root motion is
    /// off; the owner moves the transform. The pivot is at the soles.
    /// Materials: slot 0 SM_Art_WorkerBot_Chassis (shared palette atlas, emissive sensor), slot 1
    /// SM_Art_WorkerBot_Role_&lt;Variant&gt; (role paint + glowing beacon). Do not run IndustrialArtDressing
    /// over these renderers.
    /// </summary>
    public static class ColonistArt
    {
        /// <summary>
        /// Single switch for the worker-bot meshes hooked up in <see cref="VillagerAgent.Spawn"/>.
        /// On: villagers mount the authored worker bots. Off: <see cref="Attach"/> returns null and
        /// villagers keep the capsule placeholder.
        /// </summary>
        public static readonly bool Enabled = true;

        public const string ResourceDir = "Colonists/";
        public const string PrefabPrefix = "SM_Colonist_";
        public const string SpeedParam = "Speed";
        public static readonly int SpeedHash = Animator.StringToHash(SpeedParam);

        /// <summary>
        /// Ground speed (m/s) the in-place stride cycle covers at 1x on the 1.27 m bot: 1.26 m steps,
        /// 1.2 s per two-step cycle, authored at 2.10 m/s and measured in Unity from the planted foot.
        /// VillagerAgent's 2.4 m/s plays it at ~1.14x, about 1.9 steps/s.
        /// </summary>
        public const float StrideSpeed = 2.1f;

        /// <summary>Length of one step (m) of the stride cycle; steps per second = ground speed / StepLength.</summary>
        public const float StepLength = 1.26f;

        /// <summary>Fastest playback rate (safety cap; the stride no longer gets near it).</summary>
        public const float MaxPlayback = 2.5f;

        public static readonly string[] Variants = { "Engineer", "Hydroponics", "Medic", "Hauler" };

        public static string PrefabName(int salt) =>
            PrefabPrefix + Variants[((salt % Variants.Length) + Variants.Length) % Variants.Length];

        public static GameObject Load(int salt) => Resources.Load<GameObject>(ResourceDir + PrefabName(salt));

        /// <summary>
        /// Instantiate the worker bot for <paramref name="salt"/> as child "Visual" (faces the parent's +Z).
        /// Returns null when <see cref="Enabled"/> is off or the prefab is missing so callers keep their placeholder.
        /// </summary>
        public static GameObject Attach(Transform parent, int salt)
        {
            if (!Enabled || parent == null) return null;
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

        /// <summary>Drive Idle/Walk on a bot's Animator from the owner's ground speed.</summary>
        public static void Drive(Animator anim, float groundSpeed)
        {
            if (anim == null || anim.runtimeAnimatorController == null) return;
            anim.SetFloat(SpeedHash, groundSpeed);
            anim.speed = PlaybackSpeed(groundSpeed);
        }
    }
}
