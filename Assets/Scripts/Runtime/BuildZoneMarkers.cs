using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Draws the marked build zones as ground rings while the Build tool is open: trade-post zones
    /// gold, temple zones violet, taken zones grey. The zones that fit the building picked in the
    /// catalog are drawn bright, the rest dim.
    /// </summary>
    public sealed class BuildZoneMarkers : MonoBehaviour
    {
        private const int Segments = 40;
        private GameLoop _loop;
        private readonly List<LineRenderer> _rings = new List<LineRenderer>();
        private readonly List<LineRenderer> _beacons = new List<LineRenderer>();
        private Material _mat;
        private int _builtFor = -1;

        public void Bind(GameLoop loop) => _loop = loop;

        private void LateUpdate()
        {
            var zones = _loop != null ? _loop.BuildZones : null;
            if (zones != null && _builtFor != zones.Count) Rebuild(zones.Count);
            UpdateBeacons(zones);
            bool show = _loop != null && _loop.IsPlaying && _loop.ActiveTool == OverseerTool.Build &&
                        zones != null && zones.Count > 0 && _loop.ZoneRules.enabled;
            if (!show)
            {
                SetAll(false);
                return;
            }

            var picked = _loop.BuildInput != null ? _loop.BuildInput.Selected : null;
            var pickedKind = picked != null ? _loop.ZoneRules.KindFor(picked.category) : BuildZoneKind.None;

            for (int i = 0; i < zones.Count; i++)
            {
                var z = zones[i];
                var ring = _rings[i];
                ring.enabled = true;
                bool taken = _loop.IsZoneTaken(i);
                bool match = pickedKind == z.Kind;
                Color baseColor = z.Kind == BuildZoneKind.Temple
                    ? new Color(0.7f, 0.5f, 1f)
                    : z.Kind == BuildZoneKind.Mine
                        ? new Color(1f, 0.5f, 0.15f)
                        : new Color(1f, 0.82f, 0.25f);
                Color c = taken ? new Color(0.5f, 0.5f, 0.5f) : baseColor;
                c.a = taken ? 0.35f : match ? 1f : 0.45f;
                ring.startColor = c;
                ring.endColor = c;
                ring.widthMultiplier = match && !taken ? 0.55f : 0.22f;
                float ringY = TerrainDataBake.GroundHeight(z.Center.x, z.Center.z) + 0.45f;
                for (int s = 0; s < Segments; s++)
                {
                    float a = Mathf.PI * 2f * s / Segments;
                    ring.SetPosition(s, new Vector3(
                        z.Center.x + Mathf.Cos(a) * z.Radius, ringY, z.Center.z + Mathf.Sin(a) * z.Radius));
                }
            }
        }

        /// <summary>
        /// Light columns mark where a bound building can go. Open ore deposits stay lit all the
        /// time. While the Build tool is open, free zones of the picked kind (gold trade rings
        /// for a landing pad, violet temple sites) get a column too.
        /// </summary>
        private void UpdateBeacons(IReadOnlyList<BuildZone> zones)
        {
            bool playing = _loop != null && _loop.IsPlaying && zones != null && _loop.ZoneRules.enabled;
            bool buildOpen = playing && _loop.ActiveTool == OverseerTool.Build;
            BuildZoneKind pickedKind = BuildZoneKind.None;
            if (buildOpen && _loop.BuildInput != null && _loop.BuildInput.Selected != null)
                pickedKind = _loop.ZoneRules.KindFor(_loop.BuildInput.Selected.category);

            for (int i = 0; i < _beacons.Count; i++)
            {
                var b = _beacons[i];
                if (b == null) continue;
                bool inRange = playing && i < zones.Count;
                bool taken = inRange && _loop.IsZoneTaken(i);
                var kind = inRange ? zones[i].Kind : BuildZoneKind.None;
                bool mineAlways = inRange && kind == BuildZoneKind.Mine && !taken;
                bool pickedColumn = inRange && !taken && pickedKind != BuildZoneKind.None && kind == pickedKind;
                bool on = mineAlways || pickedColumn;
                if (b.enabled != on) b.enabled = on;
                if (!on) continue;
                Color c = kind == BuildZoneKind.Temple
                    ? new Color(0.72f, 0.5f, 1f)
                    : kind == BuildZoneKind.Mine
                        ? new Color(1f, 0.55f, 0.15f)
                        : new Color(1f, 0.84f, 0.28f);
                b.startColor = new Color(c.r, c.g, c.b, 0.9f);
                b.endColor = new Color(c.r, c.g, c.b, 0f);
                float y = TerrainDataBake.GroundHeight(zones[i].Center.x, zones[i].Center.z);
                var baseAt = new Vector3(zones[i].Center.x, y, zones[i].Center.z);
                b.SetPosition(0, baseAt + Vector3.up * 0.4f);
                b.SetPosition(1, baseAt + Vector3.up * 14f);
            }
        }

        private void Rebuild(int count)
        {
            for (int i = 0; i < _rings.Count; i++)
                if (_rings[i] != null) Destroy(_rings[i].gameObject);
            _rings.Clear();
            for (int i = 0; i < _beacons.Count; i++)
                if (_beacons[i] != null) Destroy(_beacons[i].gameObject);
            _beacons.Clear();
            if (_mat == null)
            {
                var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
                if (shader != null) _mat = new Material(shader) { name = "SM_BuildZone" };
            }
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("BuildZone_" + i);
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.loop = true;
                lr.positionCount = Segments;
                lr.useWorldSpace = true;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                if (_mat != null) lr.sharedMaterial = _mat;
                _rings.Add(lr);

                var bgo = new GameObject("OreBeacon_" + i);
                bgo.transform.SetParent(transform, false);
                var beam = bgo.AddComponent<LineRenderer>();
                beam.positionCount = 2;
                beam.useWorldSpace = true;
                beam.widthMultiplier = 0.45f;
                beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                beam.receiveShadows = false;
                beam.enabled = false;
                if (_mat != null) beam.sharedMaterial = _mat;
                _beacons.Add(beam);
            }
            _builtFor = count;
        }

        private void SetAll(bool on)
        {
            for (int i = 0; i < _rings.Count; i++)
                if (_rings[i] != null && _rings[i].enabled != on) _rings[i].enabled = on;
        }
    }
}
