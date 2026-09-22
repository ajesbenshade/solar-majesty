using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace SolarMajesty
{
    /// <summary>
    /// Plays Blender Idle / Walk / Strike clips on an imported SM_Unit FBX.
    /// Velocity comes from the agent root. Does not score orders or touch SpecialistBrain.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UnitClipPlayer : MonoBehaviour
    {
        private const float MoveThreshold = 0.12f;

        private Animator _animator;
        private PlayableGraph _graph;
        private AnimationMixerPlayable _mixer;
        private AnimationClipPlayable _idle;
        private AnimationClipPlayable _walk;
        private AnimationClipPlayable _strike;
        private AnimationClip _idleClip;
        private AnimationClip _walkClip;
        private AnimationClip _strikeClip;
        private bool _ready;
        private Vector3 _lastPos;
        private float _strikeLeft;
        private Transform _mover;

        public static UnitClipPlayer Bind(GameObject visual, string resourceName)
        {
            if (visual == null || string.IsNullOrEmpty(resourceName)) return null;
            var player = visual.GetComponent<UnitClipPlayer>();
            if (player == null) player = visual.AddComponent<UnitClipPlayer>();
            player.Load(resourceName);
            return player._ready ? player : null;
        }

        public void NotifyStrike()
        {
            if (_ready && _strikeClip != null)
                _strikeLeft = Mathf.Max(_strikeLeft, _strikeClip.length > 0.05f ? _strikeClip.length : 0.55f);
        }

        private void Load(string resourceName)
        {
            _animator = GetComponentInChildren<Animator>(true);
            if (_animator == null) _animator = gameObject.AddComponent<Animator>();
            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var clips = Resources.LoadAll<AnimationClip>("Units/" + resourceName);
            if (clips == null) return;
            for (int i = 0; i < clips.Length; i++)
            {
                var clip = clips[i];
                if (clip == null || clip.name.StartsWith("__preview__")) continue;
                if (clip.name == "Idle") _idleClip = clip;
                else if (clip.name == "Walk") _walkClip = clip;
                else if (clip.name == "Strike") _strikeClip = clip;
            }

            if (_idleClip == null && _walkClip == null) return;

            _graph = PlayableGraph.Create("SM_" + resourceName);
            _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(_graph, "clips", _animator);
            _mixer = AnimationMixerPlayable.Create(_graph, 3);
            output.SetSourcePlayable(_mixer);

            _idle = Connect(0, _idleClip);
            _walk = Connect(1, _walkClip);
            _strike = Connect(2, _strikeClip);
            _mixer.SetInputWeight(0, 1f);
            _mixer.SetInputWeight(1, 0f);
            _mixer.SetInputWeight(2, 0f);
            _graph.Play();
            _ready = true;
            _lastPos = transform.position;
        }

        private AnimationClipPlayable Connect(int index, AnimationClip clip)
        {
            if (clip == null)
            {
                _mixer.SetInputWeight(index, 0f);
                return default;
            }

            var playable = AnimationClipPlayable.Create(_graph, clip);
            _graph.Connect(playable, 0, _mixer, index);
            return playable;
        }

        private void LateUpdate()
        {
            if (!_ready) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (_mover == null)
            {
                var motion = GetComponentInParent<UnitMotion>();
                _mover = motion != null ? motion.transform : transform;
            }

            Vector3 pos = _mover.position;
            Vector3 delta = pos - _lastPos;
            delta.y = 0f;
            _lastPos = pos;
            float speed = delta.magnitude / dt;
            bool moving = speed > MoveThreshold;

            if (_strikeLeft > 0f) _strikeLeft -= dt;
            bool striking = _strikeLeft > 0f && _strikeClip != null;

            _mixer.SetInputWeight(0, !moving && !striking ? 1f : 0f);
            _mixer.SetInputWeight(1, moving && !striking ? 1f : 0f);
            _mixer.SetInputWeight(2, striking ? 1f : 0f);

            Advance(_idle, _idleClip, dt);
            Advance(_walk, _walkClip, dt);
            Advance(_strike, _strikeClip, dt);
            _graph.Evaluate(dt);
        }

        private static void Advance(AnimationClipPlayable playable, AnimationClip clip, float dt)
        {
            if (clip == null || !playable.IsValid()) return;
            double t = playable.GetTime() + dt;
            if (clip.length > 0.01f && t > clip.length) t %= clip.length;
            playable.SetTime(t);
        }

        private void OnDestroy()
        {
            if (_graph.IsValid()) _graph.Destroy();
        }
    }
}
