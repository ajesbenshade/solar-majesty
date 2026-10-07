using System;
using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Stalker den. Owns a budget of fauna; when they die the lair clears.
    /// Prefers the authored Resources/Dens/StalkerDen prefab. The procedural hive
    /// mound remains the fallback when that prefab is missing.
    /// </summary>
    public class StalkerLair : MonoBehaviour
    {
        [SerializeField] private int stalkerBudget = 2;
        [SerializeField] private float clearRadius = 8f;
        [SerializeField] private bool cleared;

        private readonly List<DustStalkerAgent> _spawned = new List<DustStalkerAgent>(4);
        private GameLoop _loop;
        private bool _expansionSpawned;

        public bool IsCleared => cleared;
        public bool ExpansionSpawned => _expansionSpawned;
        public bool IsScouted { get; private set; }
        public int StalkerBudget => stalkerBudget;
        public float ClearRadius => clearRadius;
        public Vector3 WorldPosition => transform.position;
        public IReadOnlyList<DustStalkerAgent> Spawned => _spawned;

        public void Configure(GameLoop loop, int budget, float radius, Color? rimColor = null, Color? pitColor = null,
            Color? soilColor = null, Color? stoneColor = null)
        {
            _loop = loop;
            _soil = soilColor;
            _stone = stoneColor;
            stalkerBudget = Mathf.Max(1, budget);
            clearRadius = Mathf.Max(4f, radius);
            cleared = false;
            gameObject.name = "StalkerLair";
            BuildMarker(
                rimColor ?? new Color(0.18f, 0.08f, 0.08f),
                pitColor ?? new Color(0.08f, 0.04f, 0.05f));
            ApplyFoggedLook();
        }

        public void SpawnInitial(Transform parent)
        {
            if (cleared || _loop == null) return;
            for (int i = 0; i < stalkerBudget; i++)
            {
                // Out in front of the mound, not inside the rock.
                float ang = (Mathf.PI * 2f * i) / stalkerBudget + 0.4f;
                Vector3 offset = new Vector3(Mathf.Cos(ang) * 5.5f, 0f, Mathf.Sin(ang) * 5.5f);
                var stalker = _loop.SpawnStalkerAt(transform.position + offset, parent);
                if (stalker != null)
                    _spawned.Add(stalker);
            }
        }

        public void Tick(int campusPieces = 0)
        {
            if (cleared) return;

            for (int i = _spawned.Count - 1; i >= 0; i--)
            {
                if (_spawned[i] == null || !_spawned[i].IsAlive)
                    _spawned.RemoveAt(i);
            }

            // ClearThreat posted on the den itself depletes the lair even if fauna wandered.
            if (HasActiveClearThreatNearby())
            {
                ForceClear();
                return;
            }

            if (_spawned.Count == 0)
            {
                MarkCleared();
                return;
            }

            TryExpansionRestock(campusPieces);
        }

        /// <summary>
        /// Campus growth restocks one extra stalker once — expansion response, not a timed wave.
        /// </summary>
        private void TryExpansionRestock(int campusPieces)
        {
            if (cleared || _expansionSpawned || _loop == null) return;
            if (campusPieces < 8) return;
            _expansionSpawned = true;
            Vector3 offset = -transform.forward * 5.5f;
            var extra = _loop.SpawnStalkerAt(transform.position + offset, transform.parent);
            if (extra != null)
                _spawned.Add(extra);
        }

        /// <summary>Continue restore. Does not kill fauna — RestoreFauna owns living threats.</summary>
        public void RestoreChart(bool wasCleared, bool wasScouted, bool expansionSpawned = false)
        {
            _spawned.Clear();
            _expansionSpawned = expansionSpawned;
            cleared = false;
            IsScouted = false;
            gameObject.name = "StalkerLair";
            ApplyLook();
            // Scouting is independent history, even when the den was later cleared.
            if (wasScouted) MarkScouted();
            // The clear already happened. Do not log it or play the claim ring again.
            if (wasCleared) MarkCleared(false);
            else if (!wasScouted) ApplyFoggedLook();
        }

        public void TrackRestoredFauna(DustStalkerAgent agent)
        {
            if (!cleared && agent != null && agent.IsAlive && !_spawned.Contains(agent))
                _spawned.Add(agent);
        }

        /// <summary>ClearThreat near this den — kill remaining fauna and silence the lair.</summary>
        public void ForceClear()
        {
            if (cleared) return;
            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null && _spawned[i].IsAlive)
                    _spawned[i].ApplyClearThreatKill();
            }
            _spawned.Clear();
            MarkCleared();
        }

        private bool HasActiveClearThreatNearby()
        {
            if (_loop == null || _loop.Flags == null) return false;
            var list = _loop.Flags.Flags;
            Vector3 me = transform.position;
            float r = clearRadius;
            float rSq = r * r;
            for (int i = 0; i < list.Count; i++)
            {
                var f = list[i];
                if (f == null || f.Data == null) continue;
                if (f.Data.flagType != FlagType.ClearThreat) continue;
                float dx = f.WorldPosition.x - me.x;
                float dz = f.WorldPosition.z - me.z;
                if (dx * dx + dz * dz > rSq) continue;
                // Only deplete once a specialist is actually working the den, not on claim alone.
                if (_loop.Flags.GetWorkRemaining(f) < f.PostedWork - 0.05f)
                    return true;
            }
            return false;
        }

        public void MarkScouted()
        {
            if (cleared || IsScouted) return;
            IsScouted = true;
            SetMarkerVisible(true);
            ApplyScoutedLook();
        }

        /// <summary>Continue restore. No claim ring — the clear already happened.</summary>
        public void RestoreFromSave(bool wasCleared, bool wasScouted)
        {
            RestoreChart(wasCleared, wasScouted);
        }

        /// <summary>Rebind a restored stalker so Tick does not treat the den as empty.</summary>
        public void BindRestored(DustStalkerAgent agent)
        {
            if (cleared || agent == null || !agent.IsAlive) return;
            if (!_spawned.Contains(agent))
                _spawned.Add(agent);
        }

        private void ApplyFoggedLook()
        {
            if (IsScouted || cleared) return;
            SetMarkerVisible(false);
            var hint = transform.Find("FogHint");
            if (hint == null)
            {
                // Uncharted: trampled earth and a few gnawed bones hint something lives here.
                var go = Prim(PrimitiveType.Cylinder, "FogHint",
                    new Vector3(0f, 0.03f, 0f), new Vector3(6f, 0.02f, 5f), Quaternion.Euler(0f, 18f, 0f));
                Paint(go, (_soil ?? new Color(0.36f, 0.27f, 0.18f)) * 0.85f, default);
                for (int i = 0; i < 4; i++)
                {
                    float a = i * 1.7f + 0.4f;
                    var bone = Prim(PrimitiveType.Capsule, "Bone",
                        new Vector3(Mathf.Cos(a) * 1.6f, 0.09f, Mathf.Sin(a) * 1.3f),
                        new Vector3(0.12f, 0.38f, 0.12f), Quaternion.Euler(90f, a * 57f, 0f));
                    bone.transform.SetParent(go.transform, true);
                    Paint(bone, new Color(0.86f, 0.82f, 0.72f), default);
                }
                hint = go.transform;
            }
            hint.gameObject.SetActive(true);
        }

        private void SetMarkerVisible(bool on)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child.name == "FogHint")
                {
                    child.gameObject.SetActive(!on);
                    continue;
                }
                if (child.name == "SurveyBeacon")
                {
                    child.gameObject.SetActive(on && IsScouted && !cleared);
                    continue;
                }
                child.gameObject.SetActive(on);
            }
        }

        /// <summary>Charted: the den keeps its look and gains a small survey beacon.</summary>
        private void ApplyScoutedLook()
        {
            var beacon = transform.Find("SurveyBeacon");
            if (beacon == null)
            {
                beacon = new GameObject("SurveyBeacon").transform;
                beacon.SetParent(transform, false);
                beacon.localPosition = new Vector3(4.6f, 0f, -3.8f);
                var pole = Prim(PrimitiveType.Cylinder, "SurveyPole", new Vector3(0f, 1.1f, 0f), new Vector3(0.12f, 1.1f, 0.12f), Quaternion.identity);
                pole.transform.SetParent(beacon, false);
                Paint(pole, new Color(0.3f, 0.32f, 0.35f), default);
                var lamp = Prim(PrimitiveType.Sphere, "SurveyLamp", new Vector3(0f, 2.3f, 0f), Vector3.one * 0.32f, Quaternion.identity);
                lamp.transform.SetParent(beacon, false);
                Paint(lamp, new Color(0.28f, 0.78f, 0.92f), new Color(0.3f, 1.2f, 1.6f));
                var flag = Prim(PrimitiveType.Cube, "SurveyFlag", new Vector3(0.32f, 1.85f, 0f), new Vector3(0.6f, 0.36f, 0.03f), Quaternion.identity);
                flag.transform.SetParent(beacon, false);
                Paint(flag, new Color(0.28f, 0.78f, 0.92f), default);
            }
            beacon.gameObject.SetActive(!cleared);
        }

        private void MarkCleared(bool announce = true)
        {
            if (cleared) return;
            cleared = true;
            gameObject.name = "StalkerLair_Cleared";
            SetMarkerVisible(true);
            var hint = transform.Find("FogHint");
            if (hint != null) hint.gameObject.SetActive(false);
            ApplyClearedLook();
            if (!announce) return;
            if (Application.isPlaying) DemoVfx.ClaimRing(transform.position, new Color(0.35f, 0.9f, 0.55f));
            Debug.Log("[Lair] Cleared stalker den.");
        }

        // ------------------------------------------------------------------ the den itself

        private struct DenPart
        {
            public Renderer Rend;
            public Color Color;
            public Color Emission;
            public bool Authored;
        }

        private readonly List<DenPart> _parts = new List<DenPart>(96);
        private bool _authoredDen;

        /// <summary>Resources path of the authored den. Mouth faces -Z, pivot at ground centre.</summary>
        internal const string DenResourcePath = "Dens/StalkerDen";

        /// <summary>
        /// Uniform scale for the authored den under the 45° mouth pivot.
        /// SM_StalkerDen is already in metres (about 13 m across and 8.5 m tall), matching
        /// the procedural clearing (12.5 × 11) and hive (about 8 m), so the fit stays 1.
        /// </summary>
        internal const float AuthoredDenScale = 1f;

        private static readonly Color DefaultSoil = new Color(0.40f, 0.31f, 0.22f);
        private static readonly Color DefaultStone = new Color(0.52f, 0.48f, 0.43f);

        private static Func<GameObject> _denPrefabLoader;

        /// <summary>EditMode seam. Null uses Resources. A loader that returns null forces the primitive den.</summary>
        internal static void SetDenPrefabLoaderForTests(Func<GameObject> loader) => _denPrefabLoader = loader;

        internal static void ResetDenPrefabLoaderForTests() => _denPrefabLoader = null;

        private static GameObject LoadDenPrefab()
        {
            if (_denPrefabLoader != null)
                return _denPrefabLoader();
            return Resources.Load<GameObject>(DenResourcePath);
        }

        /// <summary>
        /// Authored den when Resources/Dens/StalkerDen is present; otherwise the procedural hive.
        /// Either way the mesh sits under a pivot turned 45 degrees, mouth toward the play camera.
        /// The procedural mound is a dirt clearing, a ring of pale angular stone and standing slabs,
        /// a tall dark chitin hive with glowing veins, three secondary spires, a lit cave mouth
        /// framed by bone tusks, glowing egg pods, a half-buried ribcage and a slime pool.
        /// </summary>
        private void BuildMarker(Color rimColor, Color pitColor)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                DestroyImmediate(transform.GetChild(i).gameObject);
            _parts.Clear();
            _body = new GameObject("DenBody").transform;
            _body.SetParent(transform, false);
            _body.localRotation = Quaternion.Euler(0f, 45f, 0f);
            _authoredDen = false;
            if (TryAttachAuthoredDen())
            {
                // Artist pivot is ground centre. The primitive bounds-seat would lift buried
                // stone and float the soil, so the authored den is not snapped.
                ApplyLook();
                return;
            }

            var rng = new System.Random(Mathf.RoundToInt(transform.position.x * 13f + transform.position.z * 7f));
            float R(float lo, float hi) => lo + (float)rng.NextDouble() * (hi - lo);
            // The world's own rock and soil, so a Luna den is grey regolith and a Mars den rust.
            Color stone = _stone ?? DefaultStone;
            Color stoneDark = stone * 0.72f;
            stoneDark.a = 1f;
            Color dirt = Color.Lerp(_soil ?? DefaultSoil, pitColor, 0.15f);
            Color chitin = Color.Lerp(new Color(0.30f, 0.15f, 0.22f), rimColor, 0.25f);
            Color chitinHi = chitin * 1.45f;
            Color bone = new Color(0.88f, 0.83f, 0.70f);
            Color vein = new Color(1.8f, 0.55f, 0.12f);
            Color podGlow = new Color(1.6f, 0.35f, 0.12f);
            Color glowBase = new Color(0.6f, 0.2f, 0.05f);

            // Clearing: trampled dirt, darker round the mouth.
            Part(PrimitiveType.Cylinder, "Clearing", new Vector3(0f, 0.02f, 0f), new Vector3(12.5f, 0.02f, 11f), Quaternion.Euler(0f, 12f, 0f), dirt);
            Part(PrimitiveType.Cylinder, "Trample", new Vector3(0f, 0.035f, -2.2f), new Vector3(6.5f, 0.02f, 4.5f), Quaternion.identity, dirt * 0.72f);

            // Stone outcrop round the back half: angular blocks and standing slabs.
            for (int i = 0; i < 9; i++)
            {
                float a = Mathf.Lerp(15f, 165f, i / 8f) * Mathf.Deg2Rad;
                float dist = R(3.4f, 4.6f);
                float s = R(1.3f, 2.4f);
                Part(PrimitiveType.Cube, "Stone_" + i,
                    new Vector3(Mathf.Cos(a) * dist, s * 0.35f, Mathf.Sin(a) * dist),
                    new Vector3(s * R(0.9f, 1.4f), s, s * R(0.8f, 1.2f)),
                    Quaternion.Euler(R(-14f, 14f), R(0f, 90f), R(-14f, 14f)), i % 2 == 0 ? stone : stoneDark);
            }
            for (int i = 0; i < 4; i++)
            {
                float a = (35f + i * 37f) * Mathf.Deg2Rad;
                float tall = R(2.6f, 4.2f);
                Part(PrimitiveType.Cube, "Slab_" + i,
                    new Vector3(Mathf.Cos(a) * 4.9f, tall * 0.45f, Mathf.Sin(a) * 4.9f),
                    new Vector3(R(0.7f, 1.1f), tall, R(0.4f, 0.6f)),
                    Quaternion.Euler(R(-8f, 8f), -a * Mathf.Rad2Deg + 90f, R(-10f, 10f)), stone);
            }

            // The hive: a tapering stack of chitin, leaning back a little.
            float[] hy = { 1.2f, 2.9f, 4.4f, 5.7f };
            float[] hw = { 4.4f, 3.3f, 2.3f, 1.4f };
            for (int i = 0; i < hy.Length; i++)
                Part(PrimitiveType.Sphere, "Hive_" + i, new Vector3(0f, hy[i], 0.9f + i * 0.25f),
                    new Vector3(hw[i], hw[i] * 0.85f, hw[i]), Quaternion.Euler(0f, i * 25f, 0f), i % 2 == 0 ? chitin : chitinHi);
            Part(PrimitiveType.Capsule, "HiveSpike", new Vector3(0f, 7.1f, 1.9f), new Vector3(0.45f, 1.1f, 0.45f), Quaternion.Euler(-12f, 0f, 0f), chitinHi);
            // Chitin ridges and glowing veins up the hive.
            for (int i = 0; i < 6; i++)
            {
                float a = (i * 60f + 30f) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var tilt = Quaternion.LookRotation(dir) * Quaternion.Euler(-18f, 0f, 0f);
                Part(PrimitiveType.Capsule, "Ridge_" + i, new Vector3(0f, 2.6f, 1.2f) + dir * 1.55f, new Vector3(0.35f, 1.9f, 0.35f), tilt, chitinHi);
                Part(PrimitiveType.Capsule, "Vein_" + i, new Vector3(0f, 2.9f, 1.2f) + dir * 1.7f, new Vector3(0.09f, 1.6f, 0.09f), tilt, glowBase, vein);
            }

            // Secondary spires.
            Vector3[] spires = { new Vector3(-2.6f, 0f, 1.6f), new Vector3(2.5f, 0f, 1.9f), new Vector3(-0.8f, 0f, 3.6f) };
            for (int s = 0; s < spires.Length; s++)
            {
                float k = R(0.55f, 0.8f) * 1.4f;
                for (int i = 0; i < 3; i++)
                    Part(PrimitiveType.Sphere, "Spire_" + s + "_" + i, spires[s] + new Vector3(0f, (0.8f + i * 1.25f) * k, 0f),
                        Vector3.one * hw[i] * k * 0.5f, Quaternion.identity, i % 2 == 0 ? chitin : chitinHi);
                Part(PrimitiveType.Capsule, "SpireTip_" + s, spires[s] + new Vector3(0f, 4.5f * k, 0.1f),
                    new Vector3(0.25f, 0.8f, 0.25f) * k, Quaternion.Euler(-10f, 0f, 0f), chitinHi);
                Part(PrimitiveType.Sphere, "SpireGlow_" + s, spires[s] + new Vector3(0f, 2.4f * k, -0.4f * k),
                    new Vector3(0.35f, 0.5f, 0.2f) * k, Quaternion.identity, glowBase, vein);
            }

            // Cave mouth at the hive's foot: dark opening, bright core, bone tusks round it.
            Part(PrimitiveType.Sphere, "Maw", new Vector3(0f, 0.95f, -1.15f), new Vector3(2.3f, 1.9f, 1.1f), Quaternion.identity, new Color(0.05f, 0.03f, 0.03f));
            Part(PrimitiveType.Sphere, "MawGlow", new Vector3(0f, 0.85f, -1.62f), new Vector3(1.1f, 0.9f, 0.35f), Quaternion.identity,
                glowBase, new Color(2.2f, 0.55f, 0.1f));
            for (int i = 0; i < 6; i++)
            {
                float a = Mathf.Lerp(-150f, -30f, i / 5f) * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Cos(a) * 1.35f, 0.9f - Mathf.Sin(a) * 0.9f, -1.45f);
                Part(PrimitiveType.Capsule, "Tusk_" + i, p, new Vector3(0.16f, 0.6f, 0.16f),
                    Quaternion.Euler(-35f, 0f, Mathf.Cos(a) * -40f), bone);
            }

            // Egg pods, glowing, round the front of the hive.
            for (int i = 0; i < 12; i++)
            {
                float a = R(195f, 345f) * Mathf.Deg2Rad;
                float dist = R(2.4f, 3.9f);
                float size = R(0.55f, 1.0f);
                Part(PrimitiveType.Sphere, "Pod_" + i, new Vector3(Mathf.Cos(a) * dist, size * 0.5f, Mathf.Sin(a) * dist + 0.4f),
                    new Vector3(size, size * 1.3f, size), Quaternion.Euler(R(-14f, 14f), 0f, R(-14f, 14f)),
                    new Color(0.6f, 0.18f, 0.10f), podGlow * R(0.55f, 1f));
            }
            Part(PrimitiveType.Cylinder, "Slime", new Vector3(1.2f, 0.05f, -3.1f), new Vector3(2.6f, 0.03f, 1.7f), Quaternion.Euler(0f, 25f, 0f),
                new Color(0.32f, 0.62f, 0.16f), new Color(0.2f, 0.6f, 0.08f));

            // Half-buried ribcage and a skull off to the side.
            var ribAt = new Vector3(-3.2f, 0f, -2.4f);
            for (int i = 0; i < 6; i++)
            {
                float z = -0.9f + i * 0.36f;
                for (int sgn = -1; sgn <= 1; sgn += 2)
                    Part(PrimitiveType.Capsule, "Rib_" + i + "_" + sgn, ribAt + new Vector3(sgn * 0.45f, 0.55f, z),
                        new Vector3(0.12f, 0.7f, 0.12f), Quaternion.Euler(0f, 0f, sgn * 28f), bone);
            }
            Part(PrimitiveType.Capsule, "RibSpine", ribAt + new Vector3(0f, 1.05f, 0f), new Vector3(0.16f, 1.2f, 0.16f), Quaternion.Euler(90f, 0f, 0f), bone);
            Part(PrimitiveType.Sphere, "Skull", ribAt + new Vector3(0.2f, 0.3f, -1.5f), new Vector3(0.7f, 0.6f, 0.85f), Quaternion.Euler(0f, 20f, 0f), bone);

            // Loose stones round the rim.
            for (int i = 0; i < 12; i++)
            {
                float a = R(0f, 360f) * Mathf.Deg2Rad;
                float dist = R(4.6f, 6.0f);
                float size = R(0.35f, 0.9f);
                Part(PrimitiveType.Cube, "Rubble_" + i, new Vector3(Mathf.Cos(a) * dist, size * 0.3f, Mathf.Sin(a) * dist),
                    new Vector3(size, size * 0.7f, size * R(0.8f, 1.2f)), Quaternion.Euler(R(-20f, 20f), R(0f, 90f), R(-20f, 20f)),
                    i % 3 == 0 ? stoneDark : stone);
            }

            ColonyVisualUtility.SnapToGround(gameObject, TerrainDataBake.GroundHeight(transform.position));
            ApplyLook();
        }

        private Transform _body;
        private Color? _soil;
        private Color? _stone;

        private void Part(PrimitiveType type, string name, Vector3 pos, Vector3 scale, Quaternion rot, Color color,
            Color emission = default)
        {
            var go = Prim(type, name, pos, scale, rot);
            if (_body != null)
            {
                go.transform.SetParent(_body, false);
                go.transform.localPosition = pos;
                go.transform.localScale = scale;
                go.transform.localRotation = rot;
            }
            var rend = go.GetComponent<Renderer>();
            if (rend == null) return;
            _parts.Add(new DenPart { Rend = rend, Color = color, Emission = emission });
        }

        private GameObject Prim(
            PrimitiveType type, string name, Vector3 localPos, Vector3 scale, Quaternion rot)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.transform.localRotation = rot;
            ColonyVisualUtility.DestroyNow(go.GetComponent<Collider>());
            return go;
        }

        /// <summary>Active colours; a cleared den goes grey and its pods go dark.</summary>
        private void ApplyLook()
        {
            for (int i = 0; i < _parts.Count; i++)
            {
                var p = _parts[i];
                if (p.Rend == null) continue;
                if (p.Authored)
                {
                    TintAuthored(p);
                    continue;
                }
                if (!cleared)
                {
                    Paint(p.Rend.gameObject, p.Color, p.Emission);
                    continue;
                }
                Paint(p.Rend.gameObject, DimDenTint(p.Color), default);
            }

            if (_authoredDen)
                ApplyAuthoredDenState();
        }

        /// <summary>Same grey/dim the procedural den uses, applied as a multiplier on a tint.</summary>
        private static Color DimDenTint(Color c)
        {
            float grey = c.grayscale;
            return Color.Lerp(c, new Color(grey, grey, grey), 0.7f) * 0.7f;
        }

        /// <summary>
        /// Property blocks only. Soil and stone take the planet tints; every other renderer stays white.
        /// Emission stays the material's own value (the glow map carries the orange). A cleared den
        /// scales that emission to zero — never writes an orange into _EmissionColor.
        /// </summary>
        private void TintAuthored(DenPart p)
        {
            Color tint = cleared ? DimDenTint(p.Color) : p.Color;
            Color emission = cleared ? Color.black : p.Emission;
            _block ??= new MaterialPropertyBlock();
            p.Rend.GetPropertyBlock(_block);
            _block.SetColor("_BaseColor", tint);
            _block.SetColor("_Color", tint);
            _block.SetColor("_EmissionColor", emission);
            p.Rend.SetPropertyBlock(_block);
        }

        private void ApplyAuthoredDenState()
        {
            Transform active = FindDescendant(_body, "Den_Active");
            Transform ruined = FindDescendant(_body, "Den_Ruined");
            if (active != null) active.gameObject.SetActive(!cleared);
            if (ruined != null) ruined.gameObject.SetActive(cleared);
        }

        private bool TryAttachAuthoredDen()
        {
            GameObject prefab = LoadDenPrefab();
            if (prefab == null) return false;

            // false: keep the prefab's local pose under the 45° pivot. The mesh has no baked
            // rotation; identity keeps the mouth on the pivot's -Z, toward the play camera.
            GameObject instance = Object.Instantiate(prefab, _body, false);
            instance.name = prefab.name;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one * AuthoredDenScale;
            _authoredDen = true;
            CacheAuthoredTints(instance);
            return true;
        }

        private void CacheAuthoredTints(GameObject instance)
        {
            Color soil = _soil ?? DefaultSoil;
            Color stone = _stone ?? DefaultStone;
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer rend = renderers[i];
                if (rend == null) continue;
                Color tint = Color.white;
                if (rend.gameObject.name == "Den_Soil") tint = soil;
                else if (rend.gameObject.name == "Den_Stone") tint = stone;

                Color emission = Color.black;
                Material mat = rend.sharedMaterial;
                if (mat != null && mat.HasProperty("_EmissionColor"))
                    emission = mat.GetColor("_EmissionColor");

                _parts.Add(new DenPart
                {
                    Rend = rend,
                    Color = tint,
                    Emission = emission,
                    Authored = true
                });
            }
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            if (root == null) return null;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == name) return child;
                Transform nested = FindDescendant(child, name);
                if (nested != null) return nested;
            }
            return null;
        }

        private void ApplyClearedLook()
        {
            ApplyLook();
            var beacon = transform.Find("SurveyBeacon");
            if (beacon != null) beacon.gameObject.SetActive(false);
        }

        private static Shader _lit;
        private static MaterialPropertyBlock _block;
        private static Material _plain, _glow;

        /// <summary>Shared URP materials (plain / emissive); colour per renderer via a property block.</summary>
        private static void Paint(GameObject go, Color c, Color emission)
        {
            var rend = go.GetComponent<Renderer>();
            if (rend == null) return;
            bool glow = emission.maxColorComponent > 0.01f;
            _lit ??= Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default");
            if (_plain == null)
            {
                _plain = new Material(_lit) { name = "SM_Den" };
                if (_plain.HasProperty("_Smoothness")) _plain.SetFloat("_Smoothness", 0.12f);
            }
            if (_glow == null)
            {
                _glow = new Material(_lit) { name = "SM_DenGlow" };
                if (_glow.HasProperty("_Smoothness")) _glow.SetFloat("_Smoothness", 0.4f);
                _glow.EnableKeyword("_EMISSION");
                _glow.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            rend.sharedMaterial = glow ? _glow : _plain;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            _block ??= new MaterialPropertyBlock();
            rend.GetPropertyBlock(_block);
            _block.SetColor("_BaseColor", c);
            _block.SetColor("_Color", c);
            _block.SetColor("_EmissionColor", glow ? emission : Color.black);
            rend.SetPropertyBlock(_block);
        }
    }
}
