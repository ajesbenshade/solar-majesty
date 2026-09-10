using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Dressing-only colonist: Human Basic Motions walk/idle on a dummy, painted as a
    /// spacesuit. Pings between two dirt points. No NavMeshAgent, no flags, no brain.
    /// </summary>
    public sealed class SuitCrossingWalker : MonoBehaviour
    {
        private const float SuitScale = 0.72f;
        private const string BaseSlot = "HumanM@Idle01";

        private Vector3 _a;
        private Vector3 _b;
        private float _speed = 1.15f;
        private bool _toB = true;
        private float _pause;
        private Animator _anim;
        private AnimatorOverrideController _override;
        private AnimationClip _idle;
        private AnimationClip _walk;
        private bool _playingWalk;
        private bool _clipBound;

        public static GameObject Spawn(
            Transform parent, Vector3 worldPos, float yawDeg, int salt, VendorDressingKit kit)
        {
            if (kit == null || !kit.HasWalker) return null;
            bool female = (salt % 2) == 1;
            var dummy = kit.Dummy(female);
            if (dummy == null) return null;

            var root = new GameObject("Dress_SuitCrossing_" + salt);
            root.transform.SetParent(parent, false);
            root.transform.position = worldPos;
            root.transform.rotation = Quaternion.Euler(0f, yawDeg, 0f);

            var visual = Object.Instantiate(dummy, root.transform, false);
            visual.name = "Visual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one * SuitScale;
            StripGameplay(visual);
            DressAsSuit(visual);

            var walker = root.AddComponent<SuitCrossingWalker>();
            walker._a = worldPos;
            walker._b = worldPos;
            walker._speed = 0.95f + (salt % 3) * 0.12f;
            walker._idle = kit.Idle(female);
            walker._walk = kit.Walk(female);
            walker.BindAnimator(visual, kit, female);
            walker.PlayWalk(true);
            return root;
        }

        public void SetPath(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            _a = a;
            _b = b;
            if ((a - b).sqrMagnitude < 0.25f)
                _b = a + transform.forward * 4f;
        }

        private void BindAnimator(GameObject visual, VendorDressingKit kit, bool female)
        {
            _anim = visual.GetComponentInChildren<Animator>();
            if (_anim == null) _anim = visual.AddComponent<Animator>();
            var avatar = kit.AvatarFor(female);
            if (avatar != null) _anim.avatar = avatar;
            _anim.applyRootMotion = false;
            _anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            _override = new AnimatorOverrideController(kit.motionsController);
            _anim.runtimeAnimatorController = _override;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (_pause > 0f)
            {
                _pause -= dt;
                PlayWalk(false);
                return;
            }

            Vector3 dest = _toB ? _b : _a;
            Vector3 from = transform.position;
            dest.y = from.y;
            Vector3 next = Vector3.MoveTowards(from, dest, _speed * dt);
            Vector3 delta = next - from;
            delta.y = 0f;
            transform.position = next;
            if (delta.sqrMagnitude > 1e-6f)
            {
                Quaternion look = Quaternion.LookRotation(delta.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, look, 420f * dt);
            }

            PlayWalk(true);

            if ((next - dest).sqrMagnitude < 0.04f)
            {
                _toB = !_toB;
                _pause = 0.45f;
            }
        }

        private void PlayWalk(bool walking)
        {
            if (_anim == null || _override == null) return;
            if (_clipBound && _playingWalk == walking) return;
            _clipBound = true;
            _playingWalk = walking;
            AnimationClip clip = walking ? _walk : _idle;
            if (clip == null) clip = walking ? _idle : _walk;
            if (clip == null) return;
            _override[BaseSlot] = clip;
            _anim.Play("BaseAnimation", 0, 0f);
        }

        private static void StripGameplay(GameObject root)
        {
            foreach (var col in root.GetComponentsInChildren<Collider>(true))
            {
                if (Application.isPlaying) Object.Destroy(col);
                else Object.DestroyImmediate(col);
            }
            foreach (var rb in root.GetComponentsInChildren<Rigidbody>(true))
            {
                if (Application.isPlaying) Object.Destroy(rb);
                else Object.DestroyImmediate(rb);
            }
        }

        private static void DressAsSuit(GameObject visual)
        {
            var white = new Color(0.86f, 0.82f, 0.74f);
            var carbon = new Color(0.16f, 0.15f, 0.14f);
            var cyan = new Color(0.22f, 0.82f, 0.98f);
            var cyanEmit = new Color(0.18f, 0.55f, 0.72f);
            var orange = new Color(0.92f, 0.42f, 0.08f);
            var steel = new Color(0.42f, 0.44f, 0.48f);

            var rends = visual.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
            {
                if (rends[i] == null || rends[i].name.StartsWith("Dress_")) continue;
                IndustrialArtDressing.Tint(rends[i].gameObject, white, 0.28f);
            }

            Transform head = FindBone(visual.transform, "B-head");
            Transform chest = FindBone(visual.transform, "B-chest") ?? FindBone(visual.transform, "B-spine");
            Transform handL = FindBone(visual.transform, "B-hand.L");
            Transform handR = FindBone(visual.transform, "B-hand.R");
            Transform footL = FindBone(visual.transform, "B-foot.L") ?? FindBone(visual.transform, "B-toe.L");
            Transform footR = FindBone(visual.transform, "B-foot.R") ?? FindBone(visual.transform, "B-toe.R");

            if (head != null)
            {
                Prim(head, "Dress_SuitVisor", PrimitiveType.Sphere,
                    new Vector3(0f, 0.06f, 0.11f), new Vector3(0.16f, 0.12f, 0.10f), cyan, cyanEmit);
            }
            if (chest != null)
            {
                Prim(chest, "Dress_SuitPack", PrimitiveType.Cube,
                    new Vector3(0f, 0.05f, -0.14f), new Vector3(0.22f, 0.28f, 0.10f), steel);
                Prim(chest, "Dress_SuitTrim", PrimitiveType.Cube,
                    new Vector3(0f, 0.08f, 0.12f), new Vector3(0.06f, 0.16f, 0.02f), orange);
            }
            if (handL != null)
                Prim(handL, "Dress_SuitGlove_L", PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.08f, orange);
            if (handR != null)
                Prim(handR, "Dress_SuitGlove_R", PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.08f, orange);
            if (footL != null)
                Prim(footL, "Dress_SuitBoot_L", PrimitiveType.Cube,
                    new Vector3(0f, -0.02f, 0.04f), new Vector3(0.08f, 0.06f, 0.14f), carbon);
            if (footR != null)
                Prim(footR, "Dress_SuitBoot_R", PrimitiveType.Cube,
                    new Vector3(0f, -0.02f, 0.04f), new Vector3(0.08f, 0.06f, 0.14f), carbon);
        }

        private static Transform FindBone(Transform root, string name)
        {
            var ts = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < ts.Length; i++)
            {
                if (ts[i] != null && ts[i].name == name)
                    return ts[i];
            }
            return null;
        }

        private static void Prim(
            Transform parent, string name, PrimitiveType type, Vector3 localPos, Vector3 localScale,
            Color color, Color emission = default)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = localScale;
            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Object.Destroy(col);
                else Object.DestroyImmediate(col);
            }
            IndustrialArtDressing.Tint(go, color, emission.maxColorComponent > 0.01f ? 0.62f : 0.28f, emission);
        }
    }
}
