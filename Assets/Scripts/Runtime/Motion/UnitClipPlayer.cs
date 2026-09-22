using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace SolarMajesty
{
    /// <summary>
    /// Plays the Blender Idle / Walk / Strike / Work / Down clips on an imported SM_Unit FBX.
    ///
    /// * Clip names are matched on the text after the last '|' — Blender's FBX exporter names
    ///   takes "SM_Unit_X_Rig|Idle", which Unity keeps verbatim as the clip name.
    /// * States cross-fade instead of snapping.
    /// * Walk playback rate follows ground speed against the stride speed the Blender script
    ///   wrote to Resources/Units/UnitClipMeta.json, so feet stay planted at any move speed.
    /// * Each unit starts its loops at a random phase so a squad never moves in lockstep.
    /// * Off-screen units advance time but skip the graph evaluation.
    ///
    /// Velocity comes from the agent root. Does not score orders or touch SpecialistBrain.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UnitClipPlayer : MonoBehaviour
    {
        private const int IdleSlot = 0;
        private const int WalkSlot = 1;
        private const int StrikeSlot = 2;
        private const int WorkSlot = 3;
        private const int DownSlot = 4;
        private const int SlotCount = 5;

        private const float MoveOn = 0.35f;     // m/s to enter Walk
        private const float MoveOff = 0.15f;    // m/s to leave Walk (hysteresis)
        private const float FadeRate = 7.5f;    // weight units per second (~0.13 s blend)
        private const float DefaultStride = 2.6f;

        private static Dictionary<string, MetaRow> _meta;

        private Animator _animator;
        private PlayableGraph _graph;
        private AnimationMixerPlayable _mixer;
        private readonly AnimationClipPlayable[] _playables = new AnimationClipPlayable[SlotCount];
        private readonly AnimationClip[] _clips = new AnimationClip[SlotCount];
        private readonly float[] _weights = new float[SlotCount];
        private readonly double[] _times = new double[SlotCount];
        private bool _ready;
        private Vector3 _lastPos;
        private float _speed;
        private bool _moving;
        private float _strikeLeft;
        private bool _working;
        private bool _downed;
        private float _walkStride = DefaultStride;
        private float _idleRate = 1f;
        private Transform _mover;
        private Renderer[] _renderers;

        /// <summary>True once clips were found and the graph is running.</summary>
        public bool Ready => _ready;

        /// <summary>True when an authored Down clip exists (UnitMotion should not sag the body).</summary>
        public bool HasDownClip => _ready && _clips[DownSlot] != null;

        public static UnitClipPlayer Bind(GameObject visual, string resourceName)
        {
            if (visual == null || string.IsNullOrEmpty(resourceName)) return null;
            var player = visual.GetComponent<UnitClipPlayer>();
            if (player == null) player = visual.AddComponent<UnitClipPlayer>();
            player.Load(resourceName);
            if (player._ready) return player;

            // A dead player must not linger: UnitMotion and DustStalkerAgent treat the component's
            // presence as "clips own the body" and would switch their procedural motion off.
            if (Application.isPlaying) Destroy(player);
            else DestroyImmediate(player);
            return null;
        }

        /// <summary>Strip "Armature|" / "Rig|" take prefixes: "SM_Unit_X_Rig|Walk" -> "Walk".</summary>
        public static string ClipKey(string clipName)
        {
            if (string.IsNullOrEmpty(clipName)) return string.Empty;
            int bar = clipName.LastIndexOf('|');
            return bar >= 0 ? clipName.Substring(bar + 1) : clipName;
        }

        public void NotifyStrike()
        {
            AnimationClip strike = _clips[StrikeSlot];
            if (!_ready || strike == null || _downed) return;
            if (_strikeLeft <= 0f) _times[StrikeSlot] = 0.0;   // fresh swing starts at anticipation
            _strikeLeft = Mathf.Max(_strikeLeft, strike.length > 0.05f ? strike.length : 0.55f);
        }

        /// <summary>Looping labour clip (building, extracting, repairing). Falls back to Idle.</summary>
        public void SetWorking(bool working)
        {
            if (working && !_working) _times[WorkSlot] = UnityEngine.Random.value * ClipLength(WorkSlot);
            _working = working;
        }

        /// <summary>Downed: plays Down once and holds its last frame until revived.</summary>
        public void SetDowned(bool downed)
        {
            if (downed && !_downed)
            {
                _times[DownSlot] = 0.0;
                _strikeLeft = 0f;
            }
            _downed = downed;
        }

        private void Load(string resourceName)
        {
            _animator = GetComponentInChildren<Animator>(true);
            if (_animator == null) _animator = gameObject.AddComponent<Animator>();
            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            _animator.applyRootMotion = false;

            var clips = Resources.LoadAll<AnimationClip>("Units/" + resourceName);
            if (clips == null || clips.Length == 0) return;
            for (int i = 0; i < clips.Length; i++)
            {
                var clip = clips[i];
                if (clip == null || clip.name.StartsWith("__preview__")) continue;
                switch (ClipKey(clip.name))
                {
                    case "Idle": _clips[IdleSlot] = clip; break;
                    case "Walk": _clips[WalkSlot] = clip; break;
                    case "Strike": _clips[StrikeSlot] = clip; break;
                    case "Work": _clips[WorkSlot] = clip; break;
                    case "Down": _clips[DownSlot] = clip; break;
                }
            }

            if (_clips[IdleSlot] == null && _clips[WalkSlot] == null) return;

            if (TryGetMeta(resourceName, out MetaRow row) && row.walkSpeed > 0.05f)
                _walkStride = row.walkSpeed;

            _graph = PlayableGraph.Create("SM_" + resourceName);
            _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(_graph, "clips", _animator);
            _mixer = AnimationMixerPlayable.Create(_graph, SlotCount);
            output.SetSourcePlayable(_mixer);

            for (int i = 0; i < SlotCount; i++)
            {
                if (_clips[i] == null)
                {
                    _mixer.SetInputWeight(i, 0f);
                    continue;
                }
                var playable = AnimationClipPlayable.Create(_graph, _clips[i]);
                playable.SetApplyFootIK(false);
                _graph.Connect(playable, 0, _mixer, i);
                _playables[i] = playable;
            }

            // Desync crowds: random loop phase and a slight per-unit idle tempo.
            _times[IdleSlot] = UnityEngine.Random.value * ClipLength(IdleSlot);
            _times[WalkSlot] = UnityEngine.Random.value * ClipLength(WalkSlot);
            _idleRate = UnityEngine.Random.Range(0.9f, 1.1f);

            _weights[IdleSlot] = 1f;
            _mixer.SetInputWeight(IdleSlot, 1f);
            _renderers = GetComponentsInChildren<Renderer>(true);
            _graph.Play();
            _ready = true;
            _lastPos = transform.position;
            _graph.Evaluate(0f);
        }

        private float ClipLength(int slot)
        {
            var clip = _clips[slot];
            return clip != null && clip.length > 0.01f ? clip.length : 1f;
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
                _lastPos = _mover.position;
            }

            Vector3 pos = _mover.position;
            Vector3 delta = pos - _lastPos;
            delta.y = 0f;
            _lastPos = pos;
            float instant = delta.magnitude / dt;
            _speed = Mathf.Lerp(_speed, instant, 1f - Mathf.Exp(-12f * dt));
            _moving = _moving ? _speed > MoveOff : _speed > MoveOn;

            if (_strikeLeft > 0f) _strikeLeft -= dt;
            bool striking = _strikeLeft > 0f && _clips[StrikeSlot] != null;
            bool downed = _downed && _clips[DownSlot] != null;
            bool working = _working && _clips[WorkSlot] != null && !_moving;

            int target;
            if (downed) target = DownSlot;
            else if (striking) target = StrikeSlot;
            else if (_moving && _clips[WalkSlot] != null) target = WalkSlot;
            else if (working) target = WorkSlot;
            else target = _clips[IdleSlot] != null ? IdleSlot : WalkSlot;

            float step = FadeRate * dt;
            float sum = 0f;
            for (int i = 0; i < SlotCount; i++)
            {
                float goal = i == target ? 1f : 0f;
                _weights[i] = Mathf.MoveTowards(_weights[i], goal, step);
                if (_clips[i] == null) _weights[i] = 0f;
                sum += _weights[i];
            }
            for (int i = 0; i < SlotCount; i++)
                _mixer.SetInputWeight(i, sum > 1e-4f ? _weights[i] / sum : (i == target ? 1f : 0f));

            float walkRate = Mathf.Clamp(_speed / Mathf.Max(0.2f, _walkStride), 0.55f, 1.9f);
            Advance(IdleSlot, dt * _idleRate, true);
            Advance(WalkSlot, dt * walkRate, true);
            Advance(StrikeSlot, dt, true);
            Advance(WorkSlot, dt, true);
            Advance(DownSlot, dt, false);

            if (IsVisible()) _graph.Evaluate(0f);
        }

        private void Advance(int slot, float dt, bool loop)
        {
            var clip = _clips[slot];
            if (clip == null || !_playables[slot].IsValid()) return;
            // Keep time running only while the slot contributes, so re-entering Idle/Walk
            // resumes smoothly and Strike/Down start where NotifyStrike/SetDowned put them.
            if (_weights[slot] <= 0f && slot != IdleSlot && slot != WalkSlot) return;
            double len = clip.length > 0.01f ? clip.length : 1.0;
            double t = _times[slot] + dt;
            t = loop ? t % len : Math.Min(t, len);
            _times[slot] = t;
            _playables[slot].SetTime(t);
        }

        private bool IsVisible()
        {
            if (_renderers == null || _renderers.Length == 0) return true;
            for (int i = 0; i < _renderers.Length; i++)
            {
                var r = _renderers[i];
                if (r != null && r.isVisible) return true;
            }
            return false;
        }

        private void OnDestroy()
        {
            if (_graph.IsValid()) _graph.Destroy();
        }

        // --- clip metadata written by Blender/scripts/sm_animated_roster.py -------------------

        [Serializable]
        internal sealed class MetaRow
        {
            public string unit;
            public float walkSpeed;
            public float height;
            public string[] clips;
            public bool downHoldsLastFrame;
        }

        [Serializable]
        internal sealed class MetaFile
        {
            public MetaRow[] units;
        }

        private static bool TryGetMeta(string unit, out MetaRow row)
        {
            if (_meta == null)
            {
                _meta = new Dictionary<string, MetaRow>(StringComparer.Ordinal);
                var asset = Resources.Load<TextAsset>("Units/UnitClipMeta");
                if (asset != null)
                {
                    try
                    {
                        var file = JsonUtility.FromJson<MetaFile>(asset.text);
                        if (file != null && file.units != null)
                        {
                            foreach (var r in file.units)
                            {
                                if (r != null && !string.IsNullOrEmpty(r.unit))
                                    _meta[r.unit] = r;
                            }
                        }
                    }
                    catch (ArgumentException e)
                    {
                        Debug.LogWarning($"[UnitClipPlayer] bad UnitClipMeta.json: {e.Message}");
                    }
                }
            }
            return _meta.TryGetValue(unit, out row);
        }

        /// <summary>Stride speed the Walk clip was authored at (m/s), or 0 when unknown.</summary>
        public static float AuthoredWalkSpeed(string unit) =>
            TryGetMeta(unit, out MetaRow row) ? row.walkSpeed : 0f;
    }
}
