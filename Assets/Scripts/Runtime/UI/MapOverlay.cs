using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    public enum MapOverlayMode
    {
        None = 0,
        /// <summary>Which modules are powered and which are starving the grid.</summary>
        Power = 1,
        /// <summary>Where fauna pressure is coming from.</summary>
        Danger = 2,
        /// <summary>What the Defense Batteries and Defend flags actually cover.</summary>
        Coverage = 3
    }

    public struct LegendEntry
    {
        public Color Color;
        public string Label;

        public LegendEntry(Color color, string label)
        {
            Color = color;
            Label = label;
        }
    }

    /// <summary>
    /// Ground-projected information overlays.
    ///
    /// The colony has three invisible systems the player is expected to reason about — the power
    /// budget, where threat comes from, and what defence actually covers — and none of them were
    /// visible in the world. Each overlay draws flat discs and rings on the ground so the answer is
    /// spatial rather than a number in a panel.
    /// </summary>
    public sealed class MapOverlay : MonoBehaviour
    {
        private static readonly Color PowerOk = new Color(0.35f, 0.85f, 0.45f, 0.30f);
        private static readonly Color PowerShort = new Color(0.95f, 0.35f, 0.25f, 0.34f);
        private static readonly Color DangerHot = new Color(0.95f, 0.28f, 0.20f, 0.30f);
        private static readonly Color DangerWarm = new Color(0.96f, 0.66f, 0.22f, 0.24f);
        private static readonly Color CoverGood = new Color(0.35f, 0.72f, 0.98f, 0.24f);
        private static readonly Color CoverGap = new Color(0.60f, 0.60f, 0.66f, 0.18f);

        private GameLoop _loop;
        private Transform _root;
        private readonly List<GameObject> _pool = new List<GameObject>(48);
        private int _used;
        private Material _material;
        private float _refreshTimer;

        public static MapOverlay Ensure(GameLoop loop)
        {
            if (loop == null) return null;
            var overlay = loop.GetComponent<MapOverlay>();
            if (overlay == null) overlay = loop.gameObject.AddComponent<MapOverlay>();
            overlay._loop = loop;
            return overlay;
        }

        public static string TitleFor(MapOverlayMode mode)
        {
            switch (mode)
            {
                case MapOverlayMode.Power: return "POWER GRID";
                case MapOverlayMode.Danger: return "THREAT PRESSURE";
                case MapOverlayMode.Coverage: return "DEFENCE COVERAGE";
                default: return "";
            }
        }

        public static List<LegendEntry> LegendFor(MapOverlayMode mode)
        {
            var list = new List<LegendEntry>(3);
            switch (mode)
            {
                case MapOverlayMode.Power:
                    list.Add(new LegendEntry(PowerOk, "Powered"));
                    list.Add(new LegendEntry(PowerShort, "Browning out"));
                    break;
                case MapOverlayMode.Danger:
                    list.Add(new LegendEntry(DangerHot, "Active den"));
                    list.Add(new LegendEntry(DangerWarm, "Roaming fauna"));
                    break;
                case MapOverlayMode.Coverage:
                    list.Add(new LegendEntry(CoverGood, "Battery / watch"));
                    list.Add(new LegendEntry(CoverGap, "Uncovered module"));
                    break;
            }
            return list;
        }

        private void Awake()
        {
            if (_loop == null) _loop = GetComponent<GameLoop>();
            _root = new GameObject("MapOverlay").transform;
            _root.SetParent(transform, false);
        }

        private void LateUpdate()
        {
            if (_loop == null) return;

            MapOverlayMode mode = _loop.OverlayMode;
            if (mode == MapOverlayMode.None || _loop.Screen != DemoScreen.Playing)
            {
                if (_used > 0) HideAll();
                return;
            }

            // Overlays are a reading aid, not an animation; four rebuilds a second is plenty.
            _refreshTimer -= Time.unscaledDeltaTime;
            if (_refreshTimer > 0f) return;
            _refreshTimer = 0.25f;

            _used = 0;
            switch (mode)
            {
                case MapOverlayMode.Power: BuildPower(); break;
                case MapOverlayMode.Danger: BuildDanger(); break;
                case MapOverlayMode.Coverage: BuildCoverage(); break;
            }
            HideUnused();
        }

        private void BuildPower()
        {
            bool short0 = _loop.Economy != null && _loop.Economy.PowerShort;
            Color c = short0 ? PowerShort : PowerOk;

            var structures = _loop.Village != null ? _loop.Village.Structures : null;
            if (structures == null) return;

            for (int i = 0; i < structures.Count; i++)
            {
                var s = structures[i];
                if (s == null || !s.IsAlive) continue;
                Disc(s.WorldPosition, 3.4f, c);
            }
        }

        private void BuildDanger()
        {
            var world = _loop.World;
            if (world != null)
            {
                var lairs = world.Lairs;
                for (int i = 0; i < lairs.Count; i++)
                {
                    var lair = lairs[i];
                    if (lair == null || lair.IsCleared) continue;
                    Disc(lair.transform.position, 16f, DangerHot);
                }
            }

            var stalkers = _loop.Stalkers;
            if (stalkers == null) return;
            for (int i = 0; i < stalkers.Count; i++)
            {
                var s = stalkers[i];
                if (s == null) continue;
                Disc(s.transform.position, 7f, DangerWarm);
            }
        }

        private void BuildCoverage()
        {
            var structures = _loop.Village != null ? _loop.Village.Structures : null;
            if (structures == null) return;

            // Batteries first so their discs sit under the uncovered markers.
            var covered = new List<Vector3>(8);
            for (int i = 0; i < structures.Count; i++)
            {
                var s = structures[i];
                if (s == null || !s.IsAlive) continue;
                if (s.Category != BuildingCategory.Defense) continue;
                Disc(s.WorldPosition, OverseerRules.BatteryRange, CoverGood);
                covered.Add(s.WorldPosition);
            }

            for (int i = 0; i < structures.Count; i++)
            {
                var s = structures[i];
                if (s == null || !s.IsAlive || s.Category == BuildingCategory.Defense) continue;

                bool safe = false;
                for (int c = 0; c < covered.Count; c++)
                {
                    float dx = covered[c].x - s.WorldPosition.x;
                    float dz = covered[c].z - s.WorldPosition.z;
                    if (dx * dx + dz * dz <= OverseerRules.BatteryRange * OverseerRules.BatteryRange)
                    {
                        safe = true;
                        break;
                    }
                }

                if (!safe) Disc(s.WorldPosition, 3.0f, CoverGap);
            }
        }

        private void Disc(Vector3 at, float radius, Color color)
        {
            GameObject go = Take();
            go.transform.position = new Vector3(at.x, 0.06f, at.z);
            go.transform.localScale = new Vector3(radius * 2f, 0.01f, radius * 2f);

            var rend = go.GetComponent<Renderer>();
            if (rend == null) return;

            var block = new MaterialPropertyBlock();
            rend.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color);
            rend.SetPropertyBlock(block);
        }

        private GameObject Take()
        {
            if (_used < _pool.Count)
            {
                GameObject reused = _pool[_used++];
                reused.SetActive(true);
                return reused;
            }

            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "OverlayDisc";
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.SetParent(_root, false);

            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rend.receiveShadows = false;
                rend.sharedMaterial = EnsureMaterial();
            }

            _pool.Add(go);
            _used++;
            return go;
        }

        /// <summary>One shared transparent material; per-disc colour rides on a property block.</summary>
        private Material EnsureMaterial()
        {
            if (_material != null) return _material;

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Universal Render Pipeline/Lit");
            _material = new Material(shader) { name = "SM_OverlayDisc" };
            _material.SetFloat("_Surface", 1f);      // Transparent
            _material.SetFloat("_Blend", 0f);        // Alpha
            _material.SetFloat("_ZWrite", 0f);
            _material.renderQueue = 3050;
            _material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _material.enableInstancing = true;
            return _material;
        }

        private void HideUnused()
        {
            for (int i = _used; i < _pool.Count; i++)
            {
                if (_pool[i] != null) _pool[i].SetActive(false);
            }
        }

        private void HideAll()
        {
            for (int i = 0; i < _pool.Count; i++)
            {
                if (_pool[i] != null) _pool[i].SetActive(false);
            }
            _used = 0;
        }
    }
}
