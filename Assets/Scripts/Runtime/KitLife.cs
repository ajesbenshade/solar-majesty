using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    public enum LifeMotion
    {
        /// <summary>Continuous rotation about a local axis (turbine rotors, radar, fans).</summary>
        Spin,
        /// <summary>Back-and-forth yaw about local up (tracking dishes, cameras, turrets idling).</summary>
        Sweep,
        /// <summary>Short on/off flashes (aviation beacons, status lamps).</summary>
        Blink,
        /// <summary>Slow emission breathing (reactor glow, signage).</summary>
        Pulse,
        /// <summary>Small vertical bob (pumps, pistons, drill heads).</summary>
        Bob
    }

    /// <summary>One animated part of a building.</summary>
    public struct LifeEntry
    {
        public LifeMotion Motion;
        public Transform Part;
        public Renderer Renderer;
        public Vector3 Axis;
        /// <summary>Spin: degrees per second. Sweep: half-arc degrees. Bob: amplitude metres.</summary>
        public float Amount;
        /// <summary>Seconds per cycle (Sweep, Blink, Pulse, Bob).</summary>
        public float Period;
        /// <summary>Blink: fraction of the period lit. Pulse: lowest emission fraction.</summary>
        public float Shape;
        public float Phase;
        public Quaternion BaseRotation;
        public Vector3 BasePosition;
    }

    /// <summary>
    /// Registration API kit builders use to make parts move or glow: <c>KitLife.Spin(root, rotor, 90f)</c>.
    /// Entries live on a <see cref="BuildingLife"/> component on the building root, which animates them.
    /// Animated parts must be their own GameObjects (not merged by <see cref="DetailBatch"/>); build a
    /// <see cref="Pivot"/> at the rotation centre and parent the moving prims under it.
    /// </summary>
    public static class KitLife
    {
        /// <summary>Empty transform at <paramref name="localPos"/> to rotate or bob a group of parts about.</summary>
        public static Transform Pivot(Transform parent, string name, Vector3 localPos, Quaternion localRot = default)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot == default ? Quaternion.identity : localRot;
            return go.transform;
        }

        public static void Spin(Transform root, Transform part, float degreesPerSecond, Vector3 localAxis = default) =>
            Add(root, new LifeEntry
            {
                Motion = LifeMotion.Spin, Part = part, Amount = degreesPerSecond,
                Axis = localAxis == default ? Vector3.up : localAxis.normalized
            });

        public static void Sweep(Transform root, Transform part, float halfArcDegrees, float period) =>
            Add(root, new LifeEntry { Motion = LifeMotion.Sweep, Part = part, Amount = halfArcDegrees, Period = period, Axis = Vector3.up });

        public static void Blink(Transform root, Renderer lamp, float period = 1.6f, float duty = 0.18f) =>
            Add(root, new LifeEntry { Motion = LifeMotion.Blink, Renderer = lamp, Part = lamp != null ? lamp.transform : null, Period = period, Shape = duty });

        public static void Pulse(Transform root, Renderer glow, float period = 3.5f, float lowest = 0.45f) =>
            Add(root, new LifeEntry { Motion = LifeMotion.Pulse, Renderer = glow, Part = glow != null ? glow.transform : null, Period = period, Shape = lowest });

        public static void Bob(Transform root, Transform part, float amplitude, float period) =>
            Add(root, new LifeEntry { Motion = LifeMotion.Bob, Part = part, Amount = amplitude, Period = period, Axis = Vector3.up });

        private static void Add(Transform root, LifeEntry e)
        {
            if (root == null || e.Part == null) return;
            var life = root.GetComponent<BuildingLife>();
            if (life == null) life = root.gameObject.AddComponent<BuildingLife>();
            e.BaseRotation = e.Part.localRotation;
            e.BasePosition = e.Part.localPosition;
            // Neighbours out of step, so a row of beacons doesn't flash in unison.
            e.Phase = Mathf.Repeat((root.position.x * 0.37f + root.position.z * 0.61f + life.Entries.Count * 0.23f), 1f);
            life.Entries.Add(e);
        }
    }

    /// <summary>
    /// Animates a building's registered <see cref="LifeEntry"/> parts. Kept deliberately cheap:
    /// one component per building, transforms and one MaterialPropertyBlock per glowing renderer.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildingLife : MonoBehaviour
    {
        public readonly List<LifeEntry> Entries = new List<LifeEntry>();
        private MaterialPropertyBlock _block;
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private readonly Dictionary<Renderer, Color> _baseEmission = new Dictionary<Renderer, Color>();

        private void Update()
        {
            float t = Time.time;
            for (int i = 0; i < Entries.Count; i++)
            {
                var e = Entries[i];
                if (e.Part == null) continue;
                switch (e.Motion)
                {
                    case LifeMotion.Spin:
                        e.Part.localRotation = e.BaseRotation * Quaternion.AngleAxis(e.Amount * t + e.Phase * 360f, e.Axis);
                        break;
                    case LifeMotion.Sweep:
                        float s = Mathf.Sin((t / Mathf.Max(0.1f, e.Period) + e.Phase) * Mathf.PI * 2f);
                        e.Part.localRotation = e.BaseRotation * Quaternion.AngleAxis(e.Amount * s, e.Axis);
                        break;
                    case LifeMotion.Bob:
                        float b = Mathf.Sin((t / Mathf.Max(0.1f, e.Period) + e.Phase) * Mathf.PI * 2f);
                        e.Part.localPosition = e.BasePosition + e.Axis * (e.Amount * b);
                        break;
                    case LifeMotion.Blink:
                        float cyc = Mathf.Repeat(t / Mathf.Max(0.1f, e.Period) + e.Phase, 1f);
                        SetGlow(e.Renderer, cyc < e.Shape ? 1f : 0.08f);
                        break;
                    case LifeMotion.Pulse:
                        float p = 0.5f + 0.5f * Mathf.Sin((t / Mathf.Max(0.1f, e.Period) + e.Phase) * Mathf.PI * 2f);
                        SetGlow(e.Renderer, Mathf.Lerp(e.Shape, 1f, p));
                        break;
                }
            }
        }

        private void SetGlow(Renderer r, float k)
        {
            if (r == null || !r.enabled) return;
            if (!_baseEmission.TryGetValue(r, out var baseC))
            {
                var m = r.sharedMaterial;
                baseC = m != null && m.HasProperty(EmissionId) ? m.GetColor(EmissionId) : Color.black;
                _baseEmission[r] = baseC;
            }
            _block ??= new MaterialPropertyBlock();
            r.GetPropertyBlock(_block);
            _block.SetColor(EmissionId, baseC * k);
            r.SetPropertyBlock(_block);
        }
    }
}
